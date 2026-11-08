"""One read-only hosted Win32 probe. Builds no product and launches no qualification cell."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

from eligibility import assess

ROOT = Path(__file__).resolve().parents[3]
PINNED_START = 'd2529cb10196589422650f35f647019965ff11be'
SOURCE = Path(__file__).parent / 'desktop_probe'
CORE_SHA256 = '69d56e12535d3a8f2f8f89ac8f97577e6dc788d26f2e7b69be8fb5f6ff17ff28'


def require(value, message):
    if not value: raise ValueError(message)


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read_json(path):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'Duplicate JSON field: ' + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding='utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda value: require(False, 'Non-finite JSON'))


def write(path, data):
    with Path(path).open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(data, stream, indent=2); stream.write('\n')


def guard(env):
    expected = {'GITHUB_ACTIONS': 'true', 'RUNNER_ENVIRONMENT': 'github-hosted', 'RUNNER_OS': 'Windows',
                'RUNNER_ARCH': 'X64', 'GITHUB_REPOSITORY': 'DouglasNLima/local-docs-studio',
                'GITHUB_EVENT_NAME': 'workflow_dispatch', 'GITHUB_REF': 'refs/heads/feature/native-ux-performance-contract',
                'GITHUB_ACTOR': 'DouglasNLima', 'GITHUB_TRIGGERING_ACTOR': 'DouglasNLima',
                'GITHUB_RUN_ATTEMPT': '1', 'GOAL77_READ_ONLY_DISCOVERY': 'true'}
    require(all(env.get(k) == v for k, v in expected.items()), 'Read-only hosted discovery guard failed')
    revision = env.get('EXPECTED_INFRASTRUCTURE_SHA', '')
    require(len(revision) == 40 and all(c in '0123456789abcdef' for c in revision) and revision == env.get('GITHUB_SHA'), 'Exact reviewed discovery source required')
    return revision


def require_safe_buffer_ownership():
    # Run42500925642 reached a native heap-corruption exit at the second
    # HIGHCONTRAST GET-buffer release. Retain that implementation for review;
    # do not execute it again until a separately reviewed ownership repair.
    raise ValueError('BLOCKED: read-only probe buffer ownership is unqualified after run42500925642')


def collect_probe(binary, child_env, output):
    timed_out = False
    with (output / 'desktop.json').open('xb') as stdout, (output / 'stderr.txt').open('xb') as stderr:
        child = subprocess.Popen([str(binary)], env=child_env, stdout=stdout, stderr=stderr,
                                 stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            child.wait(timeout=30)
        except subprocess.TimeoutExpired:
            timed_out = True
            child.kill(); child.wait(timeout=5)
        finally:
            write(output / 'child-process.json', {'processId': child.pid, 'exitCode': child.returncode,
                  'exitCodeHex': None if child.returncode is None else format(child.returncode & 0xffffffff, '08x'),
                  'timedOut': timed_out, 'qualificationProcess': False, 'retryCount': 0})
    require(not timed_out and child.returncode == 0, 'Read-only probe failed; exact child result and partial output retained')
    return child.pid


def verify_probe(observation, binary_files, revision, child_pid):
    require(type(observation['processId']) is int and observation['processId'] == child_pid, 'Read-only probe PID differs')
    require(observation['runtimeVersion'] == '10.0.14' and observation['coreSha256'] == CORE_SHA256,
            'Read-only probe loaded a different runtime')
    require(observation['processSha256'] == binary_files['DesktopProbe.exe']
            and observation['assemblySha256'] == binary_files['DesktopProbe.dll']
            and observation['sourceRevision'].endswith('+' + revision), 'Observed probe source/binary identity differs')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    revision = guard(os.environ)
    require_safe_buffer_ownership()
    require(os.name == 'nt', 'Windows only')
    git = lambda *a: subprocess.check_output(['git', '-C', str(ROOT), *a]).decode().strip()
    require(git('rev-parse', 'HEAD') == revision and not git('status', '--porcelain', '--untracked-files=all'), 'Exact clean checkout required')
    require(subprocess.run(['git', '-C', str(ROOT), 'merge-base', '--is-ancestor', PINNED_START, revision]).returncode == 0, 'Unrelated discovery source')
    output = Path(args.output).resolve(); output.mkdir(parents=True, exist_ok=False)
    try:
        sys.path.insert(0, str(ROOT / 'scripts/native/qualification'))
        import environment_lock
        actual = environment_lock.identity()
        write(output / 'environment.json', actual)
        original = read_json(ROOT / 'docs/native/evidence/hosted-environment-lock/environment-lock.json')
        environment_lock.check_identity(original['environment'], actual)
        require(subprocess.check_output(['dotnet', '--version']).decode().strip() == '10.0.403', 'Exact observed SDK10.0.403 required')
        for item in original['sdk']['identityFiles']:
            require(digest(item['path']) == item['sha256'], 'SDK identity file changed')
        build = output / 'probe-build'; build.mkdir()
        copied = {}
        for name in ('DesktopProbe.csproj', 'desktop.manifest', 'Program.cs', 'HighContrastReader.cs'):
            shutil.copyfile(SOURCE / name, build / name); copied[name] = digest(build / name)
        write(build / 'global.json', {'sdk': {'version': '10.0.403', 'rollForward': 'disable', 'allowPrerelease': False}})
        with (output / 'build.log').open('xb') as log:
            subprocess.run(['dotnet', 'build', 'DesktopProbe.csproj', '-c', 'Release', '--nologo', '-v', 'minimal',
                            '-p:SourceRevisionId=' + revision], cwd=build, stdout=log, stderr=subprocess.STDOUT, check=True, timeout=180)
        binary = build / 'bin/Release/net10.0-windows/win-x64/DesktopProbe.exe'
        binary_files = {p.name: digest(p) for p in sorted(binary.parent.iterdir()) if p.is_file()}
        write(output / 'probe-identity.json', {'sourceCommit': revision, 'sourceTree': git('rev-parse', 'HEAD^{tree}'),
              'sources': copied, 'binaryFiles': binary_files, 'sdk': '10.0.403',
              'implementation': {p.name: digest(p) for p in [Path(__file__), Path(__file__).parent / 'eligibility.py']}})
        child_env = {k: v for k, v in os.environ.items() if not k.upper().startswith(('DOTNET_', 'COMPLUS_', 'CORECLR_', 'COR_', 'STARTUP_HOOKS', 'LENS_'))
                     and k not in ('GH_TOKEN', 'GITHUB_TOKEN')}
        child_env.update(DOTNET_ROLL_FORWARD='Disable', DOTNET_MULTILEVEL_LOOKUP='0', DOTNET_EnableDiagnostics='0')
        child_pid = collect_probe(binary, child_env, output)
        observation = read_json(output / 'desktop.json')
        verify_probe(observation, binary_files, revision, child_pid)
        write(output / 'eligibility.json', assess(observation))
        require(not git('status', '--porcelain', '--untracked-files=all'), 'Discovery changed tracked source')
        write(output / 'terminal.json', {'status': 'READ_ONLY_DISCOVERY_COMPLETE', 'sourceCommit': revision,
              'runId': os.environ['GITHUB_RUN_ID'], 'attempt': 1, 'discoveryProcesses': 1,
              'qualificationProcesses': 0, 'appearanceMutations': 0, 'displayMutations': 0, 'inputMutations': 0,
              'campaignExecutionApproved': False, 'executionStatus': 'NOT_AUTHORISED'})
        return 0  # Discovery success is independent from BLOCKED infrastructure eligibility.
    except Exception as error:
        write(output / 'failure.json', {'status': 'DISCOVERY_FAILED', 'error': str(error), 'qualificationProcesses': 0,
                                       'campaignExecutionApproved': False, 'executionStatus': 'NOT_AUTHORISED'})
        raise
    finally:
        write(output / 'files.json', {p.relative_to(output).as_posix(): digest(p) for p in sorted(output.rglob('*')) if p.is_file()})


if __name__ == '__main__':
    try: sys.exit(main())
    except Exception as error: print('BLOCKED: ' + str(error), file=sys.stderr); sys.exit(2)
