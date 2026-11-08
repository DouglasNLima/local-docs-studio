"""Discovery/build only. No qualification entry point, input, window or appearance API."""
import argparse
import hashlib
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import urllib.parse
import urllib.request
import zipfile

import environment_lock as lock
import qualification as q
from sources import ROOT, CANDIDATE, BASELINE, git, json_new, sha, write_new

START = '4a340fb66ae410f5216310845848197518d4975c'
DOWNLOADS = ROOT / 'docs/native/qualification/runtime-downloads.json'
METADATA = 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json'


def clean_environment():
    env = {k: v for k, v in os.environ.items() if not k.upper().startswith(('LENS_', 'DOTNET_', 'COMPLUS_', 'CORECLR_', 'COR_', 'STARTUP_HOOKS'))}
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1', DOTNET_EnableDiagnostics='0',
               DOTNET_CLI_USE_MSBUILD_SERVER='0', MSBUILDDISABLENODEREUSE='1')
    return env


def run(command, env, timeout=120):
    result = subprocess.run(command, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            env=env, timeout=timeout, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    if result.returncode:
        raise ValueError(f'Console command failed ({result.returncode}): {command!r}\n' + result.stdout.decode('utf-8', errors='replace'))
    return result.stdout


def fetch(url, path, maximum):
    def allowed(value):
        parsed = urllib.parse.urlsplit(value)
        return parsed.scheme == 'https' and parsed.netloc == 'builds.dotnet.microsoft.com' and parsed.path.startswith('/dotnet/') and not parsed.query and not parsed.fragment
    if not allowed(url):
        raise ValueError('Untrusted runtime download URL')
    with urllib.request.urlopen(url, timeout=60) as response:
        if not allowed(response.geturl()):
            raise ValueError('Runtime download redirected outside the approved origin')
        with Path(path).open('xb') as out:
            size = 0
            while chunk := response.read(1024 * 1024):
                size += len(chunk)
                if size > maximum: raise ValueError('Download exceeds fixed byte bound')
                out.write(chunk)


def select_archives(metadata, version):
    releases = [r for r in metadata['releases'] if r['release-version'] == version]
    if len(releases) != 1: raise ValueError('Required runtime release is absent or duplicated: ' + version)
    result = {}
    for component, name in [('runtime', 'dotnet-runtime-win-x64.zip'), ('windowsdesktop', 'windowsdesktop-runtime-win-x64.zip')]:
        entry = releases[0][component]
        candidates = [f for f in entry['files'] if f['rid'] == 'win-x64' and f['name'] == name]
        if entry['version'] != version or len(candidates) != 1 or not re.fullmatch('[a-fA-F0-9]{128}', candidates[0]['hash']):
            raise ValueError('Runtime component identity is unavailable: ' + version + '/' + component)
        result[component] = {'url': candidates[0]['url'], 'sha512': candidates[0]['hash'].lower()}
    return result


def extract_archive(archive, destination):
    seen = set(); total = 0
    with zipfile.ZipFile(archive) as package:
        if len(package.infolist()) > 30000: raise ValueError('Archive member count exceeds bound')
        for member in package.infolist():
            name = member.filename; path = PurePosixPath(name)
            if '\\' in name or ':' in name or path.is_absolute() or '..' in path.parts or not path.parts:
                raise ValueError('Unsafe archive path')
            if any(part.rstrip(' .') != part or any(c in part for c in '<>"|?*') or
                   part.split('.')[0].upper() in {'CON','PRN','AUX','NUL',*[f'COM{i}' for i in range(1,10)],*[f'LPT{i}' for i in range(1,10)]}
                   for part in path.parts):
                raise ValueError('Unsafe Windows archive member')
            if name.casefold() in seen or (member.external_attr >> 16) & 0o170000 == 0o120000:
                raise ValueError('Duplicate/symlink archive member')
            seen.add(name.casefold()); total += member.file_size
            if total > 2147483648 or member.file_size > 536870912: raise ValueError('Archive expansion bound exceeded')
            target = Path(destination).joinpath(*path.parts)
            if member.is_dir(): target.mkdir(parents=True, exist_ok=True); continue
            data = package.read(member)
            if target.exists():
                if target.read_bytes() != data: raise ValueError('Conflicting runtime archive overlap')
            else: write_new(target, data)


def verify_archive(archive, item):
    raw = Path(archive).read_bytes(); digest = hashlib.sha512(raw).hexdigest()
    if digest != item['sha512']: raise ValueError('Runtime archive SHA-512 mismatch')
    return dict(item, verifiedSha512=digest, sha256=sha(raw), bytes=len(raw))


def runtime_config(version):
    if version not in q.RUNTIMES: raise ValueError('Unsupported runtime')
    return {'runtimeOptions': {'tfm': 'net10.0', 'rollForward': 'Disable', 'frameworks': [
        {'name': 'Microsoft.NETCore.App', 'version': version}, {'name': 'Microsoft.WindowsDesktop.App', 'version': version}]}}


def product_worktree(path, revision):
    # The immutable archive builder reads Git blobs. Do not expand unrelated deep
    # historical evidence paths into a hosted Windows working directory.
    git('worktree', 'add', '--detach', '--no-checkout', str(path), revision)
    git('sparse-checkout', 'set', '--cone', 'src/native/LensDocsStudio.Core',
        'src/native/LensDocsStudio.Native', root=path)
    git('read-tree', '-mu', 'HEAD', root=path)
    if git('rev-parse', 'HEAD', root=path).decode().strip() != revision or git('status', '--porcelain', '--untracked-files=no', root=path).strip():
        raise ValueError('Sparse product checkout is not the clean pinned source')


def discovery(output):
    if os.name != 'nt' or any(os.environ.get(k) != v for k, v in {
        'GITHUB_ACTIONS': 'true', 'RUNNER_ENVIRONMENT': 'github-hosted', 'RUNNER_OS': 'Windows',
        'GITHUB_REPOSITORY': 'DouglasNLima/local-docs-studio', 'GITHUB_EVENT_NAME': 'workflow_dispatch',
        'GITHUB_REF': 'refs/heads/feature/native-ux-performance-contract', 'GITHUB_ACTOR': 'DouglasNLima',
        'GITHUB_TRIGGERING_ACTOR': 'DouglasNLima', 'GOAL77_DISCOVERY_ONLY': 'true'}.items()):
        raise ValueError('Only the reviewed hosted discovery job may collect this environment')
    head = git('rev-parse', 'HEAD').decode().strip()
    if not re.fullmatch('[a-f0-9]{40}', head) or head != os.environ.get('EXPECTED_TOOLING_SHA') or head != os.environ.get('GITHUB_SHA'):
        raise ValueError('Requested, checked-out and dispatched tooling SHA differ')
    git('merge-base', '--is-ancestor', START, head)
    if git('status', '--porcelain', '--untracked-files=all').strip(): raise ValueError('Discovery source is not clean')
    output = Path(output).resolve()
    if output.exists(): raise ValueError('Fresh discovery evidence directory required')
    output.mkdir(parents=True)
    try:
        collect(output, head)
    except Exception as error:
        json_new(output / 'blocked.json', {'status': 'BLOCKED', 'reason': str(error), 'campaignExecutionApproved': False,
                                          'R01Processes': 0, 'R02Processes': 0, 'calibrationProcesses': 0})
        raise


def collect(output, head):
    env = clean_environment(); host = lock.identity(); lock.check_identity(host, host)
    run_identity = {key: os.environ.get(key) for key in ('GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT', 'GITHUB_WORKFLOW', 'GITHUB_WORKFLOW_REF', 'GITHUB_SHA', 'RUNNER_NAME')}
    json_new(output / 'environment.json', {'environment': host, 'github': run_identity, 'toolingCommit': head,
        'toolingTree': git('rev-parse', 'HEAD^{tree}').decode().strip(), 'campaignExecutionApproved': False})
    dotnet = Path(shutil.which('dotnet.exe') or '').resolve(strict=True)
    sdk_version = run([str(dotnet), '--version'], env).decode().strip()
    if not re.fullmatch(r'10\.0\.\d+', sdk_version): raise ValueError('An actual .NET 10 SDK is required; observed ' + sdk_version)
    sdk = {'version': sdk_version, 'host': str(dotnet), 'identityFiles': []}
    for path in (dotnet, dotnet.parent/'sdk'/sdk_version/'MSBuild.dll', dotnet.parent/'sdk'/sdk_version/'Roslyn/bincore/csc.dll', dotnet.parent/'sdk'/sdk_version/'.version'):
        sdk['identityFiles'].append({'path': str(path), 'sha256': sha(path.read_bytes())})
    for switch, name in [('--info', 'dotnet-info.txt'), ('--list-sdks', 'available-sdks.txt'), ('--list-runtimes', 'available-runtimes.txt')]:
        write_new(output/name, run([str(dotnet), switch], env))
    available = {}
    for component, name in [('Microsoft.NETCore.App', 'System.Private.CoreLib.dll'), ('Microsoft.WindowsDesktop.App', 'PresentationFramework.dll')]:
        for path in sorted((dotnet.parent/'shared'/component).glob('*/'+name)):
            available[str(path)] = sha(path.read_bytes())
    json_new(output/'available-runtime-files.json', available)
    fetch(METADATA, output/'release-metadata.json', 8388608)
    metadata = q.strict_json(output/'release-metadata.json'); expected = q.strict_json(DOWNLOADS)
    runtimes = {}; pins = {}
    downloads = output/'downloads'; downloads.mkdir()
    for version in q.RUNTIMES:
        archives = select_archives(metadata, version)
        if archives != expected['versions'][version]: raise ValueError('Published runtime archive identity changed: '+version)
        root = output/'runtimes'/version; root.mkdir(parents=True)
        records = {}
        for component, item in archives.items():
            archive = downloads/(version+'-'+component+'.zip')
            fetch(item['url'], archive, 536870912)
            records[component] = verify_archive(archive, item)
            extract_archive(archive, root)
        pin = {}
        for key, relative in [('dotnet','dotnet.exe'), ('core',f'shared/Microsoft.NETCore.App/{version}/System.Private.CoreLib.dll'),
                              ('wpf',f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationFramework.dll')]:
            path = root/relative; pin[key] = str(path); pin[key+'Sha256'] = sha(path.read_bytes())
        q.validate_runtime(version, pin); pins[version] = pin
        runtimes[version] = {'root': str(root), 'archives': records, 'files': lock.closure(root)}
    probe = output/'runtime-probe'; probe.mkdir()
    write_new(probe/'Program.cs', (ROOT/'scripts/native/qualification/RuntimeIdentityProbe.cs').read_bytes())
    write_new(probe/'RuntimeIdentityProbe.csproj', '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>')
    write_new(output/'runtime-probe-build.txt', run([str(dotnet),'build',str(probe/'RuntimeIdentityProbe.csproj'),'-c','Release','--nologo'], env, 300))
    for version, item in runtimes.items():
        runtimeconfig = probe/(version+'.runtimeconfig.json'); json_new(runtimeconfig, runtime_config(version))
        probe_env = dict(env, DOTNET_ROLL_FORWARD='Disable', DOTNET_MULTILEVEL_LOOKUP='0')
        raw = run([pins[version]['dotnet'],'exec','--runtimeconfig',str(runtimeconfig),str(probe/'bin/Release/net10.0/RuntimeIdentityProbe.dll')], probe_env)
        write_new(output/(version+'-loaded-runtime.json'), raw)
        item['loaded'] = q.strict_data(raw)
        lock.validate_loaded(item['loaded'], version, item['root'], item['files'])
        lock.verify_closure(item['root'], item['files'])
    # Product snapshots and all metadata self-tests are the already reviewed offline path.
    roots = {}
    for label, revision in [('candidate', CANDIDATE), ('baseline', BASELINE)]:
        roots[label] = output/'worktrees'/label
        product_worktree(roots[label], revision)
    build = output/'build'
    source = q.build_sources(build, roots['candidate'], roots['baseline'])
    if source['toolingDirty']: raise ValueError('Build source receipt is dirty')
    write_new(output/'bundle-build.txt', run([sys.executable,str(ROOT/'scripts/native/qualification/qualification.py'),'build','--output',str(build)], env, 1200))
    if sdk_version != run([str(dotnet), '--version'], env).decode().strip(): raise ValueError('SDK moved during discovery')
    config = q.template(); config.update(toolingCommit=head, sourceReceipt=str(build/'source-receipt.json'),
        sourceReceiptSha256=sha((build/'source-receipt.json').read_bytes()), imageVersion=host['imageVersion'],
        osVersion=host['windows']['version'], runtimePins=pins)
    for name in config['bundles']:
        path = build/name/'bin/Release/net10.0-windows/bundle-receipt.json'
        config['bundles'][name] = {'receipt': str(path), 'receiptSha256': sha(path.read_bytes())}
    record = {'schemaVersion':1, 'status':'DISCOVERED_NOT_AUTHORISED', 'campaignExecutionApproved':False,
        'toolingCommit':head, 'environment':host, 'github':run_identity, 'sdk':sdk, 'runtimePins':pins, 'runtimes':runtimes,
        'sourceReceipt':{'path':config['sourceReceipt'],'sha256':config['sourceReceiptSha256']}, 'bundles':config['bundles'],
        'metadata':{'url':METADATA,'sha256':sha((output/'release-metadata.json').read_bytes()), 'selectedInputsSha256':sha(DOWNLOADS.read_bytes())},
        'processBounds':{'R01':4,'R02':18,'calibration':0}, 'discoveryOnly':{'runtimeConsoleProbes':2,'qualificationProcesses':0,'foregroundActions':0,'appearanceChanges':0}}
    json_new(output/'environment-lock.json',record)
    config.update(environmentLock=str(output/'environment-lock.json'),environmentLockSha256=sha((output/'environment-lock.json').read_bytes()))
    json_new(output/'candidate-config.json',config)
    for protocol, count in [('R01',4),('R02',18)]:
        rows, _, _ = q.validate_candidate_config(config,protocol)
        if len(rows) != count: raise ValueError('Finite process inventory changed')
    json_new(output/'validation.json',{'status':'CANDIDATE_VERIFIED_NOT_AUTHORISED','configurationSha256':sha((output/'candidate-config.json').read_bytes()),
        'toolingCommit':head,'R01ProcessesExecuted':0,'R02ProcessesExecuted':0,'calibrationProcessesExecuted':0,
        'configurationProtocols':{'R01':4,'R02':18},'campaignExecutionApproved':False})


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--output',required=True); args = parser.parse_args()
    try: discovery(args.output)
    except Exception as error:
        print('BLOCKED: '+str(error),file=sys.stderr);sys.exit(2)
