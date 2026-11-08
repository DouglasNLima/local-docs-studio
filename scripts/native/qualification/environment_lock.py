"""Read-only hosted identity lock. No input, appearance or WPF APIs are used."""
import os
from pathlib import Path
import sys

import qualification as q
from sources import sha


def identity():
    if os.name != 'nt':
        raise ValueError('Windows identity is unavailable')
    import winreg
    with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r'SOFTWARE\Microsoft\Windows NT\CurrentVersion') as key:
        windows = {name: winreg.QueryValueEx(key, name)[0] for name in ('CurrentBuildNumber', 'UBR', 'EditionID', 'BuildLabEx', 'ProductName')}
    version = sys.getwindowsversion()
    windows['version'] = f'{version.major}.{version.minor}.{version.build}'
    if windows['CurrentBuildNumber'] != str(version.build):
        raise ValueError('Registry and loaded OS build disagree')
    return {'imageOS': os.environ.get('ImageOS'), 'imageVersion': os.environ.get('ImageVersion'),
            'runnerOS': os.environ.get('RUNNER_OS'), 'runnerArch': os.environ.get('RUNNER_ARCH'),
            'runnerEnvironment': os.environ.get('RUNNER_ENVIRONMENT'), 'windows': windows}


def check_identity(expected, actual):
    if expected != actual or not expected.get('imageOS') or not expected.get('imageVersion'):
        raise ValueError('Hosted image/full Windows identity changed; fresh discovery is required')
    if any(expected[k] != value for k, value in [('runnerOS', 'Windows'), ('runnerArch', 'X64'), ('runnerEnvironment', 'github-hosted')]):
        raise ValueError('Unsupported hosted OS/architecture/environment')


def closure(root):
    root = Path(root)
    result = {}
    for path in sorted(root.rglob('*')):
        if path.is_symlink():
            raise ValueError('Symlink in sealed closure')
        if path.is_file():
            result[path.relative_to(root).as_posix()] = sha(path.read_bytes())
    return result


def verify_closure(root, expected):
    if not expected or closure(root) != expected:
        raise ValueError('Runtime closure missing, extra or changed files')


def validate_loaded(record, version, root, files):
    root = Path(root).resolve()
    if record['runtimeVersion'] != version or record['architecture'] != 'X64' or record['processId'] <= 0:
        raise ValueError('Loaded runtime version/architecture/PID mismatch')
    required = {'core': f'shared/Microsoft.NETCore.App/{version}/System.Private.CoreLib.dll',
                'framework': f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationFramework.dll',
                'presentationCore': f'shared/Microsoft.WindowsDesktop.App/{version}/PresentationCore.dll',
                'windowsBase': f'shared/Microsoft.WindowsDesktop.App/{version}/WindowsBase.dll'}
    if set(record['assemblies']) != set(required):
        raise ValueError('Loaded assembly observation is incomplete')
    for key, relative in required.items():
        item = record['assemblies'][key]
        if Path(item['path']).resolve() != root / relative or item['sha256'].lower() != files.get(relative):
            raise ValueError('Loaded assembly escaped the exact runtime closure')
    modules = record['modules']
    if set(modules) != {'dotnet.exe', 'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll'}:
        raise ValueError('Loaded host/runtime module observation is incomplete')
    for name, item in modules.items():
        path = Path(item['path']).resolve()
        if not path.is_relative_to(root) or path.name.lower() != name or files.get(path.relative_to(root).as_posix()) != item['sha256'].lower():
            raise ValueError('Loaded host module escaped the exact runtime closure')
    if record['windowOrAppearanceApisUsed'] is not False:
        raise ValueError('Runtime discovery scope is inconsistent')


def validate_lock(config, actual=None):
    path = q.verify_file(config['environmentLock'], config['environmentLockSha256'])
    lock = q.strict_json(path)
    if lock['schemaVersion'] != 1 or lock['status'] != 'DISCOVERED_NOT_AUTHORISED' or lock['campaignExecutionApproved'] is not False:
        raise ValueError('Environment lock cannot grant execution authority')
    if lock['toolingCommit'] != config['toolingCommit'] or lock['runtimePins'] != config['runtimePins']:
        raise ValueError('Configuration and environment lock differ')
    if lock['sourceReceipt'] != {'path': config['sourceReceipt'], 'sha256': config['sourceReceiptSha256']} or lock['bundles'] != config['bundles']:
        raise ValueError('Configuration source/binary receipt binding differs')
    check_identity(lock['environment'], identity() if actual is None else actual)
    if config['imageVersion'] != lock['environment']['imageVersion'] or config['osVersion'] != lock['environment']['windows']['version']:
        raise ValueError('Configuration image/OS fields disagree')
    if lock['processBounds'] != {'R01': 4, 'R02': 18, 'calibration': 0}:
        raise ValueError('Protocol process boundary differs')
    if set(lock['runtimes']) != set(q.RUNTIMES):
        raise ValueError('Runtime lock has unsupported or missing revisions')
    pids = []
    for version, item in lock['runtimes'].items():
        root = Path(item['root']).resolve()
        if root != Path(config['runtimePins'][version]['dotnet']).resolve().parent:
            raise ValueError('Runtime root differs from the pinned host')
        verify_closure(root, item['files'])
        validate_loaded(item['loaded'], version, root, item['files'])
        pids.append(item['loaded']['processId'])
    if len(set(pids)) != len(pids):
        raise ValueError('Discovery runtime probes have mismatched process identities')
    for item in lock['sdk']['identityFiles']:
        q.verify_file(item['path'], item['sha256'])
    return lock
