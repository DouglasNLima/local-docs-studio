"""Historical hosted coordinator; spent authority is now blocked before side effects.

R02 is deliberately unavailable until an independent disposition of actual R01
evidence. This coordinator does not alter the reviewed launcher or its binaries.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import zipfile

REPOSITORY = 'DouglasNLima/local-docs-studio'
BRANCH = 'feature/native-ux-performance-contract'
TOOLING = '77664fbeed39162e17c6d0d2ff24a65f8a7f9d13'
DISCOVERY = 42484885426
ARTEFACT = 13618494876
ARCHIVE_SHA = '8b3d50ff7b9b394867fdc9598fe14e47d980e865c7d74f389d7802be90c7f812'
CONFIG_SHA = '7d0d520d2045c9b547c968882e93e2c48c408effce32d602fde30f7546af1a2f'
AUTHORITY_ID = 6723189720
AUTHORITY_URL = f'https://github.com/{REPOSITORY}/issues/77#issuecomment-{AUTHORITY_ID}'
AUTHORITY_BODY_SHA = 'fe514835d3b73cb946bde7f82700936a0422d727dfbdca6e5ea10406847be080'
SCRIPT = 'scripts/native/qualification_hosted/execute_locked.py'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def recovery_gate():
    # The sole authority above was consumed by run42495945111. The owner's
    # recovery continuation permits read-only discovery only. Fail before V1,
    # before Windows imports and before any restore/launch side effect.
    raise ValueError('NOT_AUTHORISED: spent R01 authority; geometry and exact appearance recovery remain unqualified')


def sha(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def read_json(path):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'Duplicate JSON field: ' + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding='utf-8-sig'), object_pairs_hook=pairs,
                      parse_constant=lambda value: require(False, 'Non-finite JSON'))


def write_json(path, value):
    with Path(path).open('x', encoding='utf-8', newline='\n') as stream:
        json.dump(value, stream, indent=2, ensure_ascii=False)
        stream.write('\n')


def guard(env, protocol):
    require(protocol == 'R01', 'R02 requires independent R01 disposition; calibration is forbidden')
    required = {'GITHUB_ACTIONS': 'true', 'RUNNER_ENVIRONMENT': 'github-hosted',
                'RUNNER_OS': 'Windows', 'RUNNER_ARCH': 'X64', 'GITHUB_REPOSITORY': REPOSITORY,
                'GITHUB_EVENT_NAME': 'workflow_dispatch', 'GITHUB_REF': 'refs/heads/' + BRANCH,
                'GITHUB_ACTOR': 'DouglasNLima', 'GITHUB_TRIGGERING_ACTOR': 'DouglasNLima',
                'GITHUB_RUN_ATTEMPT': '1'}
    require(all(env.get(k) == v for k, v in required.items()), 'Hosted owner/branch/attempt restriction failed')
    expected = env.get('EXPECTED_WRAPPER_SHA', '')
    require(re.fullmatch('[0-9a-f]{40}', expected) and env.get('GITHUB_SHA') == expected,
            'Exact reviewed coordinator commit is required')


def git(workspace, *args):
    return subprocess.check_output(['git', '-C', str(workspace), *args]).decode().strip()


def verify_authority(record):
    require(record['id'] == AUTHORITY_ID and record['user']['login'] == 'DouglasNLima'
            and record['html_url'] == AUTHORITY_URL
            and hashlib.sha256(record['body'].encode()).hexdigest() == AUTHORITY_BODY_SHA,
            'Owner authorisation is absent or changed; stop without GUI activity')


def verify_metadata(artefact, run):
    require(artefact['id'] == ARTEFACT and artefact['expired'] is False
            and artefact['digest'] == 'sha256:' + ARCHIVE_SHA
            and artefact['workflow_run']['id'] == DISCOVERY
            and artefact['workflow_run']['head_sha'] == TOOLING,
            'Reusable artefact identity differs')
    require(run['id'] == DISCOVERY and run['run_attempt'] == 1 and run['head_sha'] == TOOLING
            and run['status'] == 'completed' and run['conclusion'] == 'success'
            and run['repository']['full_name'] == REPOSITORY,
            'Discovery run identity/result differs')


def extract_archive(archive, destination, expected_count=2019):
    """Reject ambiguous/escaping ZIP names before writing any member."""
    require(not destination.exists(), 'A fresh restore root is required')
    with zipfile.ZipFile(archive) as zipped:
        members = zipped.infolist()
        require(len(members) == expected_count and sum(m.file_size for m in members) <= 2 * 1024**3,
                'Archive count/size exceeds the sealed collection')
        names = set()
        for member in members:
            name = member.orig_filename
            parts = PurePosixPath(name).parts
            require(name and name == member.filename and '\\' not in name and ':' not in name and not name.startswith('/')
                    and all(p not in ('.', '..') for p in parts)
                    and '/'.join(parts) == name and name.casefold() not in names
                    and not member.is_dir() and not stat.S_ISLNK(member.external_attr >> 16),
                    'Ambiguous or unsafe archive member')
            names.add(name.casefold())
        destination.mkdir(parents=True)
        for member in members:
            target = destination.joinpath(*PurePosixPath(member.filename).parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with zipped.open(member) as source, target.open('xb') as output:
                shutil.copyfileobj(source, output)
    return {p.relative_to(destination).as_posix(): sha(p) for p in sorted(destination.rglob('*')) if p.is_file()}


def api(endpoint, target):
    # gh handles GitHub's signed-storage redirect. No hand-built bearer forwarding.
    with target.open('xb') as stream:
        subprocess.run(['gh', 'api', 'repos/' + REPOSITORY + '/' + endpoint], stdout=stream, check=True)


def prepare(workspace, output):
    require(git(workspace, 'rev-parse', 'HEAD') == os.environ['EXPECTED_WRAPPER_SHA']
            and not git(workspace, 'status', '--porcelain', '--untracked-files=all'),
            'Exact clean coordinator checkout required')
    require(sha(__file__) == sha(workspace / SCRIPT), 'Copied coordinator differs from reviewed source')
    output.mkdir(parents=True, exist_ok=False)
    shutil.copyfile(__file__, output / 'coordinator.py')
    api(f'issues/comments/{AUTHORITY_ID}', output / 'owner-authorisation.json')
    verify_authority(read_json(output / 'owner-authorisation.json'))
    api(f'actions/artifacts/{ARTEFACT}', output / 'discovery-artefact.json')
    api(f'actions/runs/{DISCOVERY}', output / 'discovery-run.json')
    verify_metadata(read_json(output / 'discovery-artefact.json'), read_json(output / 'discovery-run.json'))
    archive = Path(os.environ['RUNNER_TEMP']) / 'goal77-sealed-environment.zip'
    api(f'actions/artifacts/{ARTEFACT}/zip', archive)
    require(sha(archive) == ARCHIVE_SHA, 'Downloaded ZIP differs from owner-approved artefact')
    files = extract_archive(archive, workspace / 'artifacts/qualification-environment')
    write_json(output / 'restored-files.json', files)
    write_json(output / 'restore.json', {'status': 'SEALED_ARTEFACT_RESTORED', 'wrapperCommit': os.environ['GITHUB_SHA'],
               'wrapperSha256': sha(__file__), 'runId': os.environ['GITHUB_RUN_ID'], 'attempt': 1,
               'discoveryRun': DISCOVERY, 'artefact': ARTEFACT, 'archiveSha256': sha(archive),
               'fileCount': len(files), 'filesManifestSha256': sha(output / 'restored-files.json'),
               'ownerAuthorisationUrl': AUTHORITY_URL})


def authorised_copy(candidate):
    require(candidate['status'] == 'NOT_AUTHORISED' and candidate['campaignExecutionApproved'] is False
            and candidate['ownerAuthorisationUrl'] is None and candidate['toolingCommit'] == TOOLING,
            'Original candidate authority/source differs')
    result = dict(candidate, status='OWNER_AUTHORISED', campaignExecutionApproved=True,
                  ownerAuthorisationUrl=AUTHORITY_URL)
    require({k for k in candidate if candidate[k] != result[k]}
            == {'status', 'campaignExecutionApproved', 'ownerAuthorisationUrl'}, 'Unexpected configuration change')
    return result


def safe_processes(records, expected_ids):
    return (len(records) == len(expected_ids) and [r.get('id') for r in records] == expected_ids
            and len({r.get('processId') for r in records}) == len(records)
            and all(type(r.get('processId')) is int and r['processId'] > 0
                    and r.get('cleanupVerified') is True and r.get('jobTreeEmpty') is True
                    and r.get('restored') is True and isinstance(r.get('initialAppearance'), dict)
                    and set(r['initialAppearance']) == {'flags', 'scheme'}
                    and r['initialAppearance'] == r.get('restoredAppearance')
                    and r.get('supervisionFailure') is None and r.get('exitCode') is not None for r in records))


def verify_restored(workspace, output, restored):
    manifest = output / 'restored-files.json'
    require(sha(manifest) == restored['filesManifestSha256'], 'Restored-file manifest identity differs')
    expected = read_json(manifest)
    require(len(expected) == 2019, 'Complete sealed artefact file count differs')
    root = workspace / 'artifacts/qualification-environment'
    actual = {}
    for path in sorted(root.rglob('*')):
        require(not path.is_symlink() and not path.is_junction(), 'Link in restored artefact')
        if path.is_file():
            actual[path.relative_to(root).as_posix()] = sha(path)
    require(actual == expected, 'Complete restored artefact has missing, extra or changed files')


def terminal_exit(terminal):
    audit = terminal.get('initialAudit', {})
    return 0 if (terminal.get('originalLauncherExit') == 0
                 and terminal.get('restorationVerified') is True and terminal.get('environmentUnchanged') is True
                 and audit.get('collection') == 'VALID' and audit.get('existingCorrectness') in ('PASS', 'INCONCLUSIVE')
                 and 'auditError' not in terminal and 'error' not in terminal) else 1


def execute(workspace, output, protocol):
    recovery_gate()
    require(os.name == 'nt' and not any(os.environ.get(k) for k in ('GH_TOKEN', 'GITHUB_TOKEN', 'GH_ENTERPRISE_TOKEN')),
            'Execution requires Windows and no inherited API credential')
    restored = read_json(output / 'restore.json')
    require(restored['wrapperCommit'] == os.environ['GITHUB_SHA'] and restored['wrapperSha256'] == sha(__file__)
            and restored['archiveSha256'] == ARCHIVE_SHA and restored['fileCount'] == 2019,
            'Restore/coordinator receipt differs')
    verify_authority(read_json(output / 'owner-authorisation.json'))
    require(git(workspace, 'rev-parse', 'HEAD') == TOOLING
            and not git(workspace, 'status', '--porcelain', '--untracked-files=all'),
            'Exact clean pinned implementation required before importing its modules')
    verify_restored(workspace, output, restored)
    sys.path.insert(0, str(workspace / 'scripts/native/qualification'))
    import qualification as q
    import environment_lock
    import audit
    original = workspace / 'artifacts/qualification-environment/candidate-config.json'
    require(sha(original) == CONFIG_SHA, 'Original candidate configuration hash differs')
    candidate = q.strict_json(original)
    # Capture actual passive identity even when the exact image/UBR comparison fails.
    write_json(output / 'observed-environment.json', environment_lock.identity())
    rows, _, _ = q.validate_candidate_config(candidate, protocol)
    require([r['id'] for r in rows] == ['V1', 'V2', 'V3', 'V4'], 'Fixed R01 process boundary differs')
    require(subprocess.check_output(['dotnet', '--version']).decode().strip() == '10.0.403', 'SDK identity differs')
    config = authorised_copy(candidate)
    config_path = output / 'execution-config.json'
    write_json(config_path, config)
    q.validate_config(config, protocol)
    write_json(output / 'preflight.json', {'status': 'VERIFIED_BEFORE_GUI', 'toolingCommit': TOOLING,
               'candidateConfigSha256': sha(original), 'executionConfigSha256': sha(config_path),
               'sourceReceiptSha256': candidate['sourceReceiptSha256'], 'environmentLockSha256': candidate['environmentLockSha256'],
               'runtimeAndBinaryClosures': 'ALL_VERIFIED', 'sdk': '10.0.403', 'cells': rows,
               'ownerAuthorisationUrl': AUTHORITY_URL})
    # No Windows input/appearance API has been loaded before every identity check.
    from windows import Windows
    platform = Windows()
    initial = {'appearance': platform.appearance(), 'heldInput': platform.held_input()}
    write_json(output / 'initial-state.json', initial)
    require(not initial['appearance']['flags'] & 1 and not initial['heldInput'], 'Initial appearance/input unsuitable')
    terminal = {'protocol': protocol, 'status': 'INCOMPLETE', 'originalLauncherExit': None,
                'environmentUnchanged': False, 'restorationVerified': False, 'r02Disposition': 'PENDING_INDEPENDENT_REVIEW',
                'policyAcceptance': 'NOT_ACTIVATED', 'pr76Integration': 'NO-GO'}
    try:
        terminal['originalLauncherExit'] = q.execute(str(config_path), protocol, str(output / 'collection'))
        q.validate_candidate_config(candidate, protocol)
        terminal['environmentUnchanged'] = sha(original) == CONFIG_SHA
        final = {'appearance': platform.appearance(), 'heldInput': platform.held_input()}
        write_json(output / 'final-state.json', final)
        records = q.strict_json(output / 'collection/processes.json')
        terminal['processCount'] = len(records)
        terminal['restorationVerified'] = final == initial and safe_processes(records, [r['id'] for r in rows])
        try:
            result = audit.audit(output / 'collection')
            write_json(output / 'initial-audit.json', result)
            terminal['initialAudit'] = {'collection': result['collection'], 'existingCorrectness': result['existingCorrectness']}
            if result['collection'] != 'VALID' or result['existingCorrectness'] == 'FAIL':
                terminal['auditFailure'] = 'KNOWN_AUDIT_FAILURE_RETAINS_NONZERO_EXIT'
        except Exception as error:
            terminal['auditError'] = str(error)
        terminal['status'] = 'COLLECTED_PENDING_INDEPENDENT_REVIEW' if terminal['restorationVerified'] and terminal['environmentUnchanged'] else 'STOP_UNSAFE_OR_INCOMPLETE'
    except Exception as error:
        terminal['error'] = str(error)
        terminal['status'] = 'STOP_EXCEPTION'
    finally:
        write_json(output / 'terminal.json', terminal)
    return terminal_exit(terminal)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=['prepare', 'execute'])
    parser.add_argument('--protocol', required=True)
    args = parser.parse_args()
    guard(os.environ, args.protocol)
    recovery_gate()
    require(os.name == 'nt', 'Hosted Windows only')
    workspace = Path(os.environ['GITHUB_WORKSPACE']).resolve()
    output = workspace / ('artifacts/goal77-bounded-' + args.protocol)
    try:
        if args.stage == 'prepare':
            prepare(workspace, output)
            return 0
        return execute(workspace, output, args.protocol)
    except Exception as error:
        if output.exists():
            write_json(output / (args.stage + '-failure.json'), {'status': 'BLOCKED', 'error': str(error),
                       'stage': args.stage, 'noRetryPermitted': True, 'r02Permitted': False})
        print('BLOCKED: ' + str(error), file=sys.stderr)
        return 2
    finally:
        if output.exists():
            write_json(output / (args.stage + '-files.json'), {p.relative_to(output).as_posix(): sha(p)
                       for p in sorted(output.rglob('*')) if p.is_file()})


if __name__ == '__main__':
    sys.exit(main())
