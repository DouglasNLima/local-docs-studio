"""Bounded R01/R02 launcher. Default commands are offline; execute is explicitly guarded."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid

from sources import (ROOT, CANDIDATE, BASELINE, PREPARATION, PROBE, VALIDATION, build_sources,
                     blob, git, json_new, sha, verify_sources, write_new)

HASH = re.compile(r'^[a-fA-F0-9]{64}$')
RUNTIMES = ('10.0.13', '10.0.14')
MAX_JSON_BYTES = 16 * 1024 * 1024


def strict_data(data):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ValueError('Duplicated JSON property: ' + key)
            result[key] = value
        return result
    if len(data) > MAX_JSON_BYTES:
        raise ValueError('JSON evidence exceeds its byte bound')
    return json.loads(data.decode('utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError('Non-finite JSON: ' + value)))


def strict_json(path):
    return strict_data(Path(path).read_bytes())


def cells(protocol):
    plan, _ = verify_sources()
    if protocol == 'R01':
        return [dict(c, kind='completion', runtime='10.0.14', bundle='r01') for c in plan['r01']['cells']]
    if protocol != 'R02':
        raise ValueError('Only R01/R02 are implemented; calibration is not executable')
    rows = []
    for cell in plan['r02']['cells']:
        # Preserve the original preflight pair before the independent app pair.
        for kind in ('preflight', 'application'):
            for index, product in enumerate(cell['products']):
                rows.append({'id': f"{cell['id']}-{index + 1}-{kind}", 'cell': cell['id'], 'kind': kind,
                             'runtime': cell['runtime'], 'product': product, 'order': cell['order'],
                             'appearance': 'high-contrast' if kind == 'application' else 'normal',
                             'bundle': 'r02-candidate' if product == CANDIDATE else 'r02-baseline'})
    for version in RUNTIMES:
        rows.append({'id': 'stock-' + version, 'kind': 'stock', 'runtime': version, 'product': CANDIDATE,
                     'appearance': 'normal', 'bundle': 'r02-candidate'})
    return rows


def template():
    return {'schemaVersion': 1, 'status': 'NOT_AUTHORISED', 'campaignExecutionApproved': False,
            'ownerAuthorisationUrl': None, 'toolingCommit': None, 'sourceReceipt': None,
            'sourceReceiptSha256': None, 'imageVersion': None, 'osVersion': None,
            'runtimePins': {v: {'dotnet': None, 'dotnetSha256': None, 'core': None, 'coreSha256': None,
                               'wpf': None, 'wpfSha256': None} for v in RUNTIMES},
            'bundles': {name: {'receipt': None, 'receiptSha256': None} for name in ('r01', 'r02-baseline', 'r02-candidate')},
            'limits': {'childSeconds': 240, 'privateBytes': 2147483648, 'logBytes': 16777216},
            'calibrationPilotEnabled': False}


def verify_file(path, expected):
    if not isinstance(expected, str) or not HASH.fullmatch(expected):
        raise ValueError('An exact SHA-256 is required')
    path = Path(path or '').resolve(strict=True)
    if not path.is_file() or path.is_symlink() or sha(path.read_bytes()).lower() != expected.lower():
        raise ValueError('File identity mismatch: ' + str(path))
    return path


def safe_relative(root, value):
    root = Path(root).resolve()
    if not isinstance(value, str) or not value or '\\' in value or Path(value).is_absolute() or '..' in Path(value).parts:
        raise ValueError('Unsafe evidence path')
    candidate = root / value
    if candidate.is_symlink() or not candidate.resolve().is_relative_to(root):
        raise ValueError('Evidence path escapes root')
    return candidate


def validate_config(config, protocol):
    expected = template()
    if set(config) != set(expected) or config['schemaVersion'] != 1 or config['limits'] != expected['limits'] or config['calibrationPilotEnabled'] is not False:
        raise ValueError('Configuration fields/limits differ from the finite contract')
    if config['campaignExecutionApproved'] is not True or config['status'] != 'OWNER_AUTHORISED':
        raise ValueError('Separate owner campaign execution authorisation is absent')
    if not re.fullmatch(r'https://github\.com/DouglasNLima/local-docs-studio/(issues/77|pull/78)#issuecomment-\d+', config['ownerAuthorisationUrl'] or ''):
        raise ValueError('An explicit Goal77/PR78 execution-authorisation record is required')
    # This is a recorded-authority guard, not an assertion that a URL authorises itself.
    if config['toolingCommit'] != git('rev-parse', 'HEAD').decode().strip() or git('status', '--porcelain', '--untracked-files=all').strip():
        raise ValueError('Exact clean tooling commit required')
    if not config['imageVersion'] or not config['osVersion']:
        raise ValueError('Exact OS build and image are unselected')
    if set(config['runtimePins']) != set(RUNTIMES) or set(config['bundles']) != set(expected['bundles']):
        raise ValueError('Unsupported or moving runtime/bundle identity')
    receipt_path = verify_file(config['sourceReceipt'], config['sourceReceiptSha256'])
    source = strict_json(receipt_path)
    if source['toolingDirty'] or source['toolingCommit'] != config['toolingCommit']:
        raise ValueError('Source preparation does not bind clean current tooling')
    _, identities = verify_sources()
    if source['identities'] != identities:
        raise ValueError('Pinned source trees differ')
    for name, digest in source['toolSources'].items():
        verify_file(safe_relative(ROOT, name), digest)
    for name, digest in source['generated'].items():
        verify_file(safe_relative(receipt_path.parent, name), digest)
    bundles = {}
    for name, pointer in config['bundles'].items():
        path = verify_file(pointer['receipt'], pointer['receiptSha256'])
        receipt = strict_json(path)
        product = BASELINE if name == 'r02-baseline' else CANDIDATE
        if receipt['bundle'] != name or receipt['productRevision'] != product or receipt['sourceReceiptSha256'] != sha(receipt_path.read_bytes()):
            raise ValueError('Product/harness receipt belongs to another process/source')
        root = path.parent
        for file, digest in receipt['files'].items():
            verify_file(safe_relative(root, file), digest)
        entry = 'LensDocsStudio.Native.UxProbe.dll' if name == 'r01' else 'LensDocsStudio.Native.Qualification.dll'
        for file in [entry, 'LensDocsStudio.Native.dll', 'LensDocsStudio.Core.dll', 'LensDocsStudio.Native.exe', entry.replace('.dll', '.deps.json')]:
            if file not in receipt['files']:
                raise ValueError('Incomplete binary closure: ' + file)
        bundles[name] = (root, receipt)
    for version, pin in config['runtimePins'].items(): validate_runtime(version, pin)
    return cells(protocol), bundles, receipt_path.parent / 'fixtures'


def validate_runtime(version, pin):
    if version not in RUNTIMES or set(pin) != set(template()['runtimePins'][version]):
        raise ValueError('Unsupported or moving runtime identity')
    dotnet = verify_file(pin['dotnet'], pin['dotnetSha256'])
    core = verify_file(pin['core'], pin['coreSha256'])
    wpf = verify_file(pin['wpf'], pin['wpfSha256'])
    if core.name != 'System.Private.CoreLib.dll' or wpf.name != 'PresentationFramework.dll' or core.parent.name != version or wpf.parent.name != version:
        raise ValueError('Runtime paths must identify exact core and WPF revisions')
    if core != dotnet.parent / 'shared/Microsoft.NETCore.App' / version / core.name or wpf != dotnet.parent / 'shared/Microsoft.WindowsDesktop.App' / version / wpf.name:
        raise ValueError('Runtime files do not belong to the pinned dotnet host')


def environment(base, row, execution, config_hash):
    # Remove ambient diagnostics, loader overrides and startup hooks, not merely known true flags.
    result = {k: v for k, v in base.items() if not k.upper().startswith(('LENS_', 'DOTNET_', 'COMPLUS_', 'CORECLR_', 'COR_', 'STARTUP_HOOKS'))}
    result.update({'DOTNET_ROLL_FORWARD': 'Disable', 'DOTNET_MULTILEVEL_LOOKUP': '0',
                   'DOTNET_EnableDiagnostics': '0', 'LENS_QUALIFICATION_EXECUTION_ID': execution,
                   'LENS_QUALIFICATION_CONFIG_SHA256': config_hash,
                   'LENS_NATIVE_COMPLETION_JOURNEY': '0', 'LENS_NATIVE_TRANSITION_TRACE': '0',
                   'LENS_NATIVE_STARTUP_TRACE': '0', 'LENS_NATIVE_COMPARISON_JOURNEY': '0',
                   'LENS_NATIVE_COMPLETION_TRACE': '0', 'LENS_NATIVE_COMPOSITION_TRACE': '0'})
    result.update(row.get('environment', {}))
    return result


def limit_failure(elapsed, private_bytes, log_bytes, limits):
    if elapsed > limits['childSeconds']: return 'CHILD_TIMEOUT'
    if private_bytes > limits['privateBytes']: return 'PRIVATE_MEMORY_BOUND'
    if log_bytes > limits['logBytes']: return 'LOG_BOUND'
    return None


def run_child(command, env, folder, limits, platform, clock=time.monotonic, popen=subprocess.Popen):
    """Injectable supervisor: unit tests exercise this same path without any Windows UI."""
    folder.mkdir(parents=True, exist_ok=False)
    started = clock(); violation = None
    with (folder / 'stdout.txt').open('xb') as out, (folder / 'stderr.txt').open('xb') as err:
        child = popen(command, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err,
                      creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        try:
            platform.attach(child)
            while True:
                exited = child.poll() is not None
                private = platform.private_bytes(child) if not exited else 0
                length = (folder / 'stdout.txt').stat().st_size + (folder / 'stderr.txt').stat().st_size
                violation = limit_failure(clock() - started, private, length, limits)
                if violation:
                    platform.kill(child); child.wait(timeout=10); break
                if exited: break
                time.sleep(0.25)
        except BaseException:
            platform.kill(child)
            child.wait(timeout=10)
            raise
        finally:
            tree_empty = platform.close()
    if tree_empty is not True:
        violation = violation or 'JOB_TREE_UNVERIFIED'
    return {'processId': child.pid, 'exitCode': child.returncode, 'supervisionFailure': violation,
            'elapsedSeconds': clock() - started, 'logBytes': length, 'command': command, 'jobTreeEmpty': tree_empty is True}


def restore_experiment(platform, initial):
    # Appearance restoration must still be attempted when input cleanup fails.
    result = {}
    try: result['releasedInput'] = platform.neutralise_input()
    except Exception as error: result['inputCleanupError'] = str(error)
    try:
        platform.set_appearance(initial)
        result['restoredAppearance'] = platform.appearance()
        result['restored'] = result['restoredAppearance'] == initial
    except Exception as error: result.update(restored=False, restoreError=str(error))
    result['cleanupVerified'] = result.get('restored') is True and 'inputCleanupError' not in result
    return result


def execute(config_path, protocol, output):
    # This check must precede loading any Windows API, starting a child or changing appearance.
    if os.name != 'nt' or any(os.environ.get(k) != v for k, v in {
            'GITHUB_ACTIONS': 'true', 'RUNNER_ENVIRONMENT': 'github-hosted', 'RUNNER_OS': 'Windows'}.items()):
        raise ValueError('BLOCKED: foreground/HC execution is forbidden on the developer desktop')
    config = strict_json(config_path)
    rows, bundles, fixtures = validate_config(config, protocol)
    if os.environ.get('ImageVersion') != config['imageVersion'] or sys.getwindowsversion().build != int(config['osVersion'].split('.')[-1]):
        raise ValueError('Exact hosted image/OS identity differs')
    from windows import Windows
    platform = Windows()
    output = Path(output).resolve()
    if output.exists(): raise ValueError('Fresh evidence root required; no cell retries or overwrite')
    output.mkdir(parents=True)
    execution = str(uuid.uuid4()); config_hash = sha(Path(config_path).read_bytes())
    json_new(output / 'execution.json', {'schemaVersion': 1, 'protocol': protocol, 'execution': execution, 'configSha256': config_hash,
        'config': config, 'cells': rows, 'status': 'COLLECTION_ONLY', 'limits': config['limits']})
    outcomes = []
    for row in rows:
        initial = platform.appearance()
        # Cold preflight order requires a normal initial state. Do not silently choose a new palette.
        if initial['flags'] & 1 or platform.held_input():
            json_new(output / 'stopped.json', {'reason': 'INITIAL_APPEARANCE_OR_INPUT_UNSUITABLE', 'nextCell': row['id']})
            break
        root, receipt = bundles[row['bundle']]
        pin = config['runtimePins'][row['runtime']]
        runtimeconfig = output / (row['id'] + '-runtimeconfig.json')
        json_new(runtimeconfig, {'runtimeOptions': {'tfm': 'net10.0', 'rollForward': 'Disable', 'frameworks': [
            {'name': 'Microsoft.NETCore.App', 'version': row['runtime']}, {'name': 'Microsoft.WindowsDesktop.App', 'version': row['runtime']}]}})
        entry = 'LensDocsStudio.Native.UxProbe' if protocol == 'R01' else 'LensDocsStudio.Native.Qualification'
        evidence = output / row['id'] / 'evidence'
        command = [pin['dotnet'], 'exec', '--runtimeconfig', str(runtimeconfig), '--depsfile', str(root / (entry + '.deps.json')), str(root / (entry + '.dll'))]
        if protocol == 'R01': command += [str(fixtures), str(evidence), 'mixed', '1', row['appearance'], CANDIDATE]
        else: command += [row['kind'], str(evidence), row['product'], row['runtime'], str(root / 'LensDocsStudio.Native.exe'), str(fixtures)]
        result = {'id': row['id'], 'kind': row['kind'], 'productRevision': row['product'], 'runtime': row['runtime'],
                  'bundle': row['bundle'], 'binaryHashes': receipt['files'], 'initialAppearance': initial}
        stop = False
        try:
            # R01 HC remains enabled by the original child constructor. Only R02's
            # separate application observation starts in an already-HC process.
            if row['kind'] == 'application': platform.set_appearance(dict(initial, flags=initial['flags'] | 1))
            result.update(run_child(command, environment(os.environ, row, execution, config_hash), output / row['id'], config['limits'], platform))
            stop = bool(result['supervisionFailure']) or not result['jobTreeEmpty']
        except Exception as error:
            result.update(exitCode=None, supervisionFailure='SUPERVISOR_ERROR', error=str(error)); stop = True
        finally:
            result.update(restore_experiment(platform, initial))
            if not result['cleanupVerified']: stop = True
            result['files'] = {p.relative_to(evidence).as_posix(): sha(p.read_bytes()) for p in sorted(evidence.rglob('*')) if p.is_file()}
            json_new(output / (row['id'] + '-process.json'), result)
            outcomes.append(result)
        if stop:
            json_new(output / 'stopped.json', {'reason': 'SAFETY_OR_CLEANUP_FAILURE', 'lastCell': row['id']}); break
    json_new(output / 'processes.json', outcomes)
    # Original assertion failures are retained; a successful supervisor cannot turn them green.
    return 0 if len(outcomes) == len(rows) and all(p.get('exitCode') == 0 and p.get('cleanupVerified') and p.get('jobTreeEmpty') is True and not p.get('supervisionFailure') for p in outcomes) else 1


def verify_build_inputs(destination, source):
    for file, digest in source['generated'].items(): verify_file(safe_relative(destination, file), digest)
    allowed_generated = {'source-receipt.json', 'r01/packages.lock.json', 'r02-candidate/packages.lock.json', 'r02-baseline/packages.lock.json'}
    actual = {p.relative_to(destination).as_posix() for p in destination.rglob('*')
              if p.is_file() and not any(x in ('bin', 'obj') for x in p.relative_to(destination).parts)
              and p.relative_to(destination).as_posix() not in allowed_generated}
    expected = set(source['generated']) - allowed_generated
    if actual != expected: raise ValueError('Untracked/extra build input in the isolated snapshot')


def validate_binary_metadata(metadata, product, tooling):
    if any(not metadata[name]['version'].endswith('+' + revision) for name, revision in [('product', product), ('core', product), ('harness', tooling)]):
        raise ValueError('Compiled product/Core/harness source-revision metadata differs')


def build(destination):
    destination = Path(destination).resolve(); source = strict_json(destination / 'source-receipt.json')
    verify_build_inputs(destination, source)
    import xml.etree.ElementTree as ET
    # Build each immutable product separately with its own revision. Referencing a
    # source project while passing the tooling revision would relabel the product.
    for label, revision in [('candidate', CANDIDATE), ('baseline', BASELINE)]:
        product_project = destination / source['productSnapshots'][label] / 'src/native/LensDocsStudio.Native/LensDocsStudio.Native.csproj'
        subprocess.run(['dotnet', 'build', str(product_project), '-c', 'Release', '--nologo', '-v', 'minimal',
                        '-p:SourceRevisionId=' + revision, '-p:ImportDirectoryBuildTargets=false', '-p:RestoreLockedMode=true'], check=True)
        verify_build_inputs(destination, source)
    for name in ('r01', 'r02-candidate', 'r02-baseline'):
        verify_build_inputs(destination, source)
        project = destination / name / 'Qualification.csproj'
        subprocess.run(['dotnet', 'build', str(project), '-c', 'Release', '--nologo', '-v', 'minimal',
                        '-p:SourceRevisionId=' + source['toolingCommit'], '-p:ImportDirectoryBuildTargets=false', '-p:BuildProjectReferences=false'], check=True)
        verify_build_inputs(destination, source)
        folder = project.parent / 'bin/Release/net10.0-windows'
        # ProjectReference copies DLLs but not the product apphost; retain it as identity-only input.
        import shutil
        product_project = Path(ET.parse(project).find('.//ProjectReference').attrib['Include'])
        shutil.copyfile(product_project.parent / 'bin/Release/net10.0-windows/LensDocsStudio.Native.exe', folder / 'LensDocsStudio.Native.exe')
        product_revision = BASELINE if name == 'r02-baseline' else CANDIDATE
        entry = 'LensDocsStudio.Native.UxProbe.dll' if name == 'r01' else 'LensDocsStudio.Native.Qualification.dll'
        mode = '--identity-offline' if name == 'r01' else '--self-test'
        metadata = strict_data(subprocess.check_output(['dotnet', str(folder / entry), mode]))
        validate_binary_metadata(metadata, product_revision, source['toolingCommit'])
        for key, file in [('product','LensDocsStudio.Native.dll'),('core','LensDocsStudio.Core.dll'),('harness',entry)]:
            verify_file(folder / file, metadata[key]['sha256'])
        json_new(folder / 'bundle-receipt.json', {'schemaVersion': 1, 'bundle': name,
            'sourceReceiptSha256': sha((destination / 'source-receipt.json').read_bytes()),
            'productRevision': product_revision, 'binaryMetadata': metadata,
            'sdk': subprocess.check_output(['dotnet', '--version']).decode().strip(),
            'files': {p.relative_to(folder).as_posix(): sha(p.read_bytes()) for p in sorted(folder.rglob('*')) if p.is_file()}})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['verify', 'template', 'plan', 'prepare', 'build', 'execute'])
    parser.add_argument('--protocol', choices=['R01', 'R02']); parser.add_argument('--output')
    parser.add_argument('--candidate-root'); parser.add_argument('--baseline-root'); parser.add_argument('--config')
    args = parser.parse_args()
    if args.command == 'verify':
        _, identities = verify_sources(); print(json.dumps({'status': 'SOURCE_VERIFIED_NO_EXECUTION', 'identities': identities})); return 0
    if args.command == 'template': json_new(args.output, template()); return 0
    if args.command == 'plan':
        rows = cells(args.protocol)
        json_new(args.output, {'status': 'NOT_EXECUTED', 'protocol': args.protocol, 'cells': rows, 'maximumProcesses': len(rows)})
        return 0
    if args.command == 'prepare': build_sources(args.output, Path(args.candidate_root).resolve(), Path(args.baseline_root).resolve()); return 0
    if args.command == 'build': build(args.output); return 0
    return execute(args.config, args.protocol, args.output)


if __name__ == '__main__':
    try: sys.exit(main())
    except Exception as error:
        print('BLOCKED: ' + str(error), file=sys.stderr); sys.exit(2)
