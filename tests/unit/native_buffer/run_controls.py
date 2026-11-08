"""Local, simulated buffers only. Builds the real collector but NEVER executes it.

Requires the observed local MSVC/SDK/Core versions; installs nothing. Every run
uses a new ignored directory. Failure logs and exit codes survive unchanged.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).parent
sys.path.insert(0, str(ROOT / 'scripts/native/qualification_hosted'))
from discover_desktop import read_json
from eligibility import HC_OWNERSHIP_CONTRACT

SDK = '10.0.401'
RUNTIME = '10.0.12'  # Local simulation runtime; NOT the hosted qualification runtime.
WINDOWS_SDK = '10.0.22621.0'


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def validate_reports(native, managed, imports):
    require(all(type(native[key]) is int for key in ('nativeChecks', 'pointerBytes', 'structureBytes', 'wcharBytes',
            'boolBytes', 'allocations', 'ownerReleases', 'borrowerReleaseDenials', 'activeAllocations', 'actualHighContrastCalls'))
            and isinstance(native['offsets'], list) and all(type(n) is int for n in native['offsets']), 'Invalid native evidence types')
    require(all(type(managed[key]) is int for key in ('tests', 'allocations', 'ownerReleases', 'borrowerReleaseDenials',
            'activeAllocations', 'actualHighContrastCalls', 'uiCalls')) and isinstance(managed['passed'], list)
            and isinstance(managed['assemblies'], list) and all(isinstance(name, str) for name in managed['assemblies']),
            'Invalid managed evidence types')
    require(native['nativeChecks'] == 21 and native['pointerBytes'] == 8 and native['structureBytes'] == 16
            and native['offsets'] == [0, 4, 8] and native['wcharBytes'] == 2 and native['boolBytes'] == 4,
            'Native ABI or control count differs')
    require(native['allocations'] == native['ownerReleases'] == 2 and native['borrowerReleaseDenials'] == 1
            and native['activeAllocations'] == native['actualHighContrastCalls'] == 0, 'Native ownership controls failed')
    require(managed['status'] == 'PASS_CONTROLLED_SIMULATION_ONLY' and managed['tests'] == 20
            and len(managed['passed']) == len(set(managed['passed'])) == 20
            and all(isinstance(name, str) and name for name in managed['passed']), 'Incomplete managed controls')
    require(managed['runtime'] == RUNTIME and managed['architecture'] == 'X64'
            and re.fullmatch('[0-9a-f]{64}', managed['coreSha256']) is not None, 'Wrong managed runtime identity')
    require(type(managed['allocations']) is int and managed['allocations'] > 32
            and managed['allocations'] == managed['ownerReleases'] and managed['borrowerReleaseDenials'] == 2
            and managed['activeAllocations'] == managed['actualHighContrastCalls'] == managed['uiCalls'] == 0
            and managed['liveCertification'] is False and managed['ownershipContract'] == HC_OWNERSHIP_CONTRACT,
            'Contradictory managed ownership evidence')
    require({'System.Private.CoreLib', 'NativeBufferTests'} <= set(managed['assemblies'])
            and len(managed['assemblies']) == len(set(managed['assemblies'])) and all(managed['assemblies'])
            and not set(managed['assemblies']) & {'PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Windows.Forms'},
            'Missing, duplicate or UI assembly identity')
    validate_imports(imports)


def validate_imports(imports):
    require(len(imports) == 2, 'Missing native import inspection')
    for report in imports:
        modules = re.findall(r'^\s+([A-Za-z0-9_.-]+\.dll)\s*$', report, re.MULTILINE | re.IGNORECASE)
        require(bool(modules) and all(re.fullmatch(r'(kernel32|vcruntime140(?:_1)?|msvcp140|api-ms-win-crt-[a-z0-9-]+)\.dll',
                                                 name.lower()) for name in modules), 'Unexpected native dependency')
        require('SystemParametersInfo' not in report and 'GetProcAddress' not in report and 'LoadLibrary' not in report,
                'Native control can reach UI or dynamic imports')


def run(output):
    require(os.name == 'nt' and not os.environ.get('GITHUB_ACTIONS'), 'Local Windows controls only; no hosted execution')
    output = output.resolve()
    require(output.is_relative_to(ROOT / 'artifacts') and not output.exists(), 'Use a new repository artifacts subdirectory')
    output.mkdir(parents=True)
    receipt = {'status': 'FAIL_OR_INCOMPLETE', 'sdk': SDK, 'runtime': RUNTIME, 'windowsSdk': WINDOWS_SDK,
               'actualHighContrastCalls': 0, 'collectorExecuted': False, 'hostedExecution': False,
               'qualificationProcesses': 0, 'commands': [], 'sources': {}}
    env = dict(os.environ, DOTNET_ROLL_FORWARD='Disable', DOTNET_CLI_TELEMETRY_OPTOUT='1',
               DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_NOLOGO='1')

    def command(name, args, cwd=output, command_env=None, timeout=120):
        args = list(map(str, args))
        args[0] = shutil.which(args[0], path=(command_env or env)['PATH']) or args[0]
        stdout, stderr = output / (name + '.stdout.txt'), output / (name + '.stderr.txt')
        entry = {'name': name, 'args': list(map(str, args)), 'exitCode': None, 'timedOut': False}
        receipt['commands'].append(entry)
        with stdout.open('xb') as out, stderr.open('xb') as err:
            try:
                child = subprocess.run(list(map(str, args)), cwd=cwd, env=command_env or env,
                                       stdout=out, stderr=err, timeout=timeout, check=False)
                entry['exitCode'] = child.returncode
            except subprocess.TimeoutExpired:
                entry['timedOut'] = True
                raise
        require(entry['exitCode'] == 0, name + ' failed; exact logs retained')
        return stdout.read_text(encoding='utf-8-sig')

    try:
        vcvars = Path(os.environ['ProgramFiles(x86)']) / 'Microsoft Visual Studio/2022/BuildTools/VC/Auxiliary/Build/vcvars64.bat'
        require(vcvars.is_file(), 'Required local VC toolchain unavailable')
        # Fixed toolchain command, no user text or credentials interpolated.
        vc = subprocess.run(f'cmd.exe /d /s /c "call "{vcvars}" -vcvars_ver=14.44 {WINDOWS_SDK} >nul && set"',
                            capture_output=True, text=True, check=False, timeout=30)
        receipt['vcSetupExitCode'] = vc.returncode
        (output / 'vc-setup.stderr.txt').write_text(vc.stderr, encoding='utf-8')
        require(vc.returncode == 0, 'Local VC environment setup failed')
        env.update((key.upper(), value) for key, value in (line.split('=', 1) for line in vc.stdout.splitlines()
                                                        if '=' in line and not line.startswith('=')))
        require(env['VCTOOLSVERSION'].rstrip('\\/') == '14.44.35207' and env['WINDOWSSDKVERSION'].rstrip('\\/') == WINDOWS_SDK,
                'Moving VC/Windows SDK identity')
        (output / 'global.json').write_text(json.dumps({'sdk': {'version': SDK, 'rollForward': 'disable'}}), encoding='utf-8')
        require(command('sdk', ['dotnet', '--version']).strip() == SDK, 'Wrong SDK')
        command('runtimes', ['dotnet', '--list-runtimes'])
        for source in (HERE / 'fixture.cpp', HERE / 'ManagedControls.cs', HERE / 'NativeBufferTests.csproj',
                       ROOT / 'scripts/native/qualification_hosted/desktop_probe/HighContrastReader.cs'):
            shutil.copyfile(source, output / source.name)
            receipt['sources'][source.relative_to(ROOT).as_posix()] = digest(source)
        receipt['sources'][Path(__file__).relative_to(ROOT).as_posix()] = digest(__file__)
        common = ['cl.exe', '/nologo', '/std:c++20', '/EHsc', '/W4', '/WX', '/O2', '/MD', str(output / 'fixture.cpp')]
        command('native-dll-build', common + ['/LD', '/Fo:fixture-dll.obj', '/Fe:NativeBufferFixture.dll'])
        command('native-test-build', common + ['/DNATIVE_SELF_TEST', '/Fo:fixture-test.obj', '/Fe:NativeBufferSelfTest.exe'])
        imports = [command('imports-' + name, ['dumpbin.exe', '/imports', name])
                   for name in ('NativeBufferFixture.dll', 'NativeBufferSelfTest.exe')]
        validate_imports(imports)  # Must reject before either control executable is run.
        command('managed-build', ['dotnet', 'build', 'NativeBufferTests.csproj', '-c', 'Release', '-o', 'managed', '--nologo'])
        shutil.copyfile(output / 'NativeBufferFixture.dll', output / 'managed/NativeBufferFixture.dll')
        command('native-controls', [output / 'NativeBufferSelfTest.exe'], timeout=30)
        native = read_json(output / 'native-controls.stdout.txt')
        command('managed-controls', ['dotnet', 'exec', '--roll-forward', 'Disable',
                                     '--fx-version', RUNTIME, output / 'managed/NativeBufferTests.dll'], timeout=30)
        managed = read_json(output / 'managed-controls.stdout.txt')
        validate_reports(native, managed, imports)
        probe = output / 'collector-compile-only'; probe.mkdir()
        for source in (ROOT / 'scripts/native/qualification_hosted/desktop_probe').iterdir():
            if source.suffix in ('.cs', '.csproj', '.manifest'):
                shutil.copyfile(source, probe / source.name)
                receipt['sources'][source.relative_to(ROOT).as_posix()] = digest(source)
        command('collector-build-only', ['dotnet', 'build', 'DesktopProbe.csproj', '-c', 'Release', '-o', 'compiled', '--nologo'], cwd=probe)
        receipt.update(status='PASS_CONTROLLED_SIMULATION_ONLY', native=native, managed=managed,
                       vcToolsVersion=env['VCTOOLSVERSION'], compilerSha256=digest(shutil.which('cl.exe', path=env['PATH'])),
                       windowsHeaderSha256=digest(Path(env['WINDOWSSDKDIR']) / 'Include' / WINDOWS_SDK / 'um/WinUser.h'))
    finally:
        receipt['files'] = {p.relative_to(output).as_posix(): digest(p) for p in output.rglob('*') if p.is_file()}
        (output / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'status': receipt['status'], 'nativeChecks': native['nativeChecks'], 'managedTests': managed['tests'],
                      'receipt': str(output / 'receipt.json')}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    run(parser.parse_args().output)
