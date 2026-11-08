"""Separately authorised, one-process HIGHCONTRAST verification. No automatic authority.

Preparation and audit functions are offline. Only execute() can import the
kernel-only supervisor, after exact owner/run/source/environment/file checks.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import sys
import urllib.request

sys.path.insert(0, str(Path(__file__).resolve().parent))
from discover_desktop import digest, read_json, write
from eligibility import appearance_state
from execute_locked import extract_archive

ROOT = Path(__file__).resolve().parents[3]
CONFIG = 'docs/native/qualification/highcontrast-live/config.json'
REPOSITORY = 'DouglasNLima/local-docs-studio'
WORKFLOW = '.github/workflows/native-ux-performance.yml'
WORKFLOW_ID = 409767103
BRANCH = 'refs/heads/feature/native-ux-performance-contract'
BOUNDS = dict(collectorProcesses=1, highContrastGets=2, supervisionSeconds=30, cleanupSeconds=5,
              retries=0, qualificationProcesses=0, calibrationProcesses=0)
COLLECTOR_SOURCES = tuple('scripts/native/qualification_hosted/desktop_probe/' + name for name in
                         ('Program.cs','HighContrastReader.cs','RuntimeIdentity.cs','DesktopProbe.csproj','desktop.manifest'))
IMPLEMENTATION = (WORKFLOW, *COLLECTOR_SOURCES, *tuple('scripts/native/qualification_hosted/'+name for name in
                  ('live_verification.py','live_process.py','discover_desktop.py','eligibility.py','execute_locked.py')),
                  *tuple('scripts/native/qualification/'+name for name in ('environment_lock.py','qualification.py','sources.py')))
DECISION = 'AUTHORISE_ONE_CORRECTED_HIGHCONTRAST_READ_ONLY_PROCESS'


def require(value, message):
    if not value: raise ValueError(message)


def exact_keys(value, keys):
    require(isinstance(value, dict) and set(value) == set(keys), 'Missing or unexpected fields')


def validate_config(config):
    require(config['schemaVersion'] == 1 and type(config['schemaVersion']) is int
            and config['kind'] == 'CORRECTED_HIGHCONTRAST_SINGLE_PROCESS', 'Wrong protocol')
    require(config['status'] == 'NOT_AUTHORISED' and config['campaignExecutionApproved'] is False
            and config['readOnlyVerificationApproved'] is False, 'Configuration cannot grant authority')
    require(config['bounds'] == BOUNDS and all(type(n) is int for n in config['bounds'].values()), 'Changed process/deadline boundary')
    require(config['supervisorQualification'] in ('BLOCKED_LOCAL_JOB_ACCOUNTING_UNRESOLVED','PASS_INDEPENDENTLY_REVIEWED'), 'Unknown supervisor qualification')
    require(set(config['implementationFiles']) == set(IMPLEMENTATION)
            and set(config['collector']['sourceFiles']) == set(COLLECTOR_SOURCES), 'Incomplete implementation/source dependency closure')
    for key in ('sourceCommit', 'sourceTree'):
        require(re.fullmatch('[0-9a-f]{40}', config['collector'][key]) is not None, 'Unpinned collector source')
    require(config['runtime']['version'] == '10.0.14' and config['runtime']['archive']['url'] ==
            'https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-win-x64.zip', 'Unsupported runtime')
    for closure in (config['collector']['files'], config['collector']['sourceFiles'], config['runtime']['files'], config['implementationFiles']):
        require(closure and all(isinstance(k, str) and re.fullmatch('[0-9a-f]{64}', v) for k, v in closure.items()), 'Missing file identities')
    require(all(config['implementationFiles'][p]==value for p,value in config['collector']['sourceFiles'].items()), 'Collector source hashes contradict implementation binding')
    require(set(config['collector']['files']) == {'DesktopProbe.exe', 'DesktopProbe.dll', 'DesktopProbe.pdb',
            'DesktopProbe.deps.json', 'DesktopProbe.runtimeconfig.json'}, 'Collector dependency closure differs')
    required = {'host/fxr/10.0.14/hostfxr.dll', 'shared/Microsoft.NETCore.App/10.0.14/hostpolicy.dll',
                'shared/Microsoft.NETCore.App/10.0.14/coreclr.dll', 'shared/Microsoft.NETCore.App/10.0.14/System.Private.CoreLib.dll'}
    require(required <= set(config['runtime']['files']) and not any('WindowsDesktop' in p for p in config['runtime']['files']),
            'Core-only runtime closure is incomplete or contains WPF')


def guard(env):
    expected = dict(GITHUB_ACTIONS='true', RUNNER_ENVIRONMENT='github-hosted', RUNNER_OS='Windows', RUNNER_ARCH='X64',
                    GITHUB_REPOSITORY=REPOSITORY, GITHUB_EVENT_NAME='workflow_dispatch', GITHUB_REF=BRANCH,
                    GITHUB_ACTOR='DouglasNLima', GITHUB_TRIGGERING_ACTOR='DouglasNLima', GITHUB_RUN_ATTEMPT='1',
                    GOAL77_HIGHCONTRAST_VERIFICATION='true')
    require(all(env.get(k) == v for k, v in expected.items()), 'Hosted owner/branch/attempt restriction failed')
    revision = env.get('EXPECTED_VERIFICATION_SHA', '')
    require(re.fullmatch('[0-9a-f]{40}', revision) and revision == env.get('GITHUB_SHA') == env.get('GITHUB_WORKFLOW_SHA'),
            'Exact execution/workflow revision required')
    require(env.get('GITHUB_WORKFLOW_REF') == REPOSITORY + '/' + WORKFLOW + '@' + BRANCH, 'Wrong registered workflow')
    require(re.fullmatch('[1-9][0-9]*', env.get('AUTHORISATION_COMMENT_ID', ''))
            and re.fullmatch('[0-9a-f]{64}', env.get('AUTHORISATION_BODY_SHA256', '')), 'New explicit owner authority required')
    return revision


def owner_payload(config, config_sha, execution_commit, execution_tree, approved=False):
    return dict(schemaVersion=1, repository=REPOSITORY, issue=77,
                decision=DECISION if approved else 'PROPOSED_NOT_AUTHORISED', approved=approved,
                executionCommit=execution_commit, executionTree=execution_tree, configurationSha256=config_sha,
                collectorCommit=config['collector']['sourceCommit'], collectorTree=config['collector']['sourceTree'],
                collectorArchiveSha256=config['collector']['archiveSha256'], runtimeArchiveSha256=config['runtime']['archive']['sha256'],
                imageVersion=config['environment']['imageVersion'], bounds=BOUNDS,
                exclusion='No R01/R02, WPF prewarming, GUI/input, display/appearance changes, calibration, retry or policy activation')


def verify_authority(record, expected, env):
    require(type(record['id']) is int and str(record['id']) == env['AUTHORISATION_COMMENT_ID']
            and record['user']['login'] == 'DouglasNLima' and record['user']['id'] == 33501911
            and record['html_url'] == f'https://github.com/{REPOSITORY}/issues/77#issuecomment-{record["id"]}'
            and record['issue_url'] == f'https://api.github.com/repos/{REPOSITORY}/issues/77'
            and record['created_at'] == record['updated_at'], 'Owner identity or unedited authority differs')
    require(hashlib.sha256(record['body'].encode()).hexdigest() == env['AUTHORISATION_BODY_SHA256'], 'Changed owner record bytes')
    # Reuse strict parsing without accepting a Markdown instruction or executable content.
    pairs = lambda items: _unique(items)
    actual = json.loads(record['body'], object_pairs_hook=pairs, parse_constant=lambda _: require(False, 'Non-finite authority'))
    require(actual == expected and actual['approved'] is True
            and type(actual['schemaVersion']) is int and type(actual['issue']) is int
            and all(type(n) is int for n in actual['bounds'].values()), 'Owner has not authorised this exact single process')


def _unique(items):
    result = {}
    for key, value in items:
        require(key not in result, 'Duplicate evidence field'); result[key] = value
    return result


def verify_run_history(history, current, env, authority_created):
    require(type(history['total_count']) is int and history['total_count'] == len(history['workflow_runs']) == 1,
            'Missing, prior, parallel or truncated dispatch history; no reuse')
    run = history['workflow_runs'][0]
    for item in (run,current):
        require(item['head_branch'] == BRANCH.removeprefix('refs/heads/') and item['status'] == 'in_progress'
                and item['actor']['login'] == item['triggering_actor']['login'] == 'DouglasNLima'
                and item['actor']['id'] == item['triggering_actor']['id'] == 33501911,
                'Unexpected dispatch actor, branch or state')
    require(run['id'] == current['id'] == int(env['GITHUB_RUN_ID']) and run['run_attempt'] == current['run_attempt'] == 1
            and run['head_sha'] == current['head_sha'] == env['GITHUB_SHA']
            and run['event'] == current['event'] == 'workflow_dispatch'
            and run['path'] == current['path'] == WORKFLOW
            and run['repository']['full_name'] == current['repository']['full_name'] == REPOSITORY
            and run['workflow_id'] == current['workflow_id'] == WORKFLOW_ID and type(run['workflow_id']) is int
            and run['created_at'] > authority_created, 'Wrong run identity or authority chronology')


def closure(root):
    result = {}
    for path in sorted(Path(root).rglob('*')):
        require(not path.is_symlink() and not (hasattr(path, 'is_junction') and path.is_junction()), 'Reparse-point dependency')
        if path.is_file(): result[path.relative_to(root).as_posix()] = digest(path)
    return result


def expected_identity(config, binary_root, runtime_root):
    path = lambda root, relative: str((root / relative).resolve())
    core = 'shared/Microsoft.NETCore.App/10.0.14/System.Private.CoreLib.dll'
    return dict(sourceRevision='1.0.0+' + config['collector']['sourceCommit'], runtimeRoot=str(runtime_root.resolve()),
                runtimeFiles=config['runtime']['files'],
                process=dict(path=path(binary_root, 'DesktopProbe.exe'), sha256=config['collector']['files']['DesktopProbe.exe']),
                assembly=dict(path=path(binary_root, 'DesktopProbe.dll'), sha256=config['collector']['files']['DesktopProbe.dll']),
                core=dict(path=path(runtime_root, core), sha256=config['runtime']['files'][core]))


def child_environment(environment, runtime_root, identity_path):
    child={k:environment[k] for k in ('SystemRoot','WINDIR','TEMP','TMP','USERPROFILE')}
    child.update(PATH=str(Path(child['SystemRoot'])/'System32'),GITHUB_ACTIONS='true',RUNNER_ENVIRONMENT='github-hosted',
                 RUNNER_OS='Windows',GOAL77_READ_ONLY_DISCOVERY='true',DOTNET_ROOT=str(runtime_root),DOTNET_ROOT_X64=str(runtime_root),
                 DOTNET_ROLL_FORWARD='Disable',DOTNET_MULTILEVEL_LOOKUP='0',DOTNET_EnableDiagnostics='0',DOTNET_DbgEnableMiniDump='0',
                 GOAL77_IDENTITY_PATH=str(identity_path),GOAL77_IDENTITY_SHA256=digest(identity_path))
    return child


def verify_identity(observed, expected, pid, phase):
    require(type(observed['processId']) is int and observed['processId'] == pid and observed['architecture'] == 'X64'
            and observed['runtimeVersion'] == '10.0.14' and observed['phase'] == phase
            and observed['sourceRevision'] == expected['sourceRevision'], 'Loaded process/source identity differs')
    require(observed['files'] == {k: expected[k] for k in ('process', 'assembly', 'core')}, 'Loaded file identity differs')
    modules = observed['modules']
    require({'hostfxr.dll', 'hostpolicy.dll', 'coreclr.dll'} <= set(modules), 'Loaded runtime-native identity missing')
    for name, item in modules.items():
        relative = item['relative']
        require(relative in expected['runtimeFiles'] and Path(relative).name.lower() == name
                and str((Path(expected['runtimeRoot']) / relative).resolve()).casefold() == item['path'].casefold()
                and item['sha256'] == expected['runtimeFiles'][relative], 'Native runtime path/hash escaped its closure')


SNAPSHOT = ['dpiReadBegin', 'workAreaReadBegin', 'monitorEnumerationBegin', 'monitorInfoBegin', 'monitorScaleBegin',
            'monitorDeviceBegin', 'displayModesBegin', 'sessionReadBegin', 'geometryComplete', 'highContrastGetBegin',
            'highContrastCopied', 'highContrastBorrowComplete', 'themeReadBegin', 'appearanceReadComplete']


def verify_job(record, pid, before):
    numeric=('totalProcesses','activeProcesses','terminatedProcesses','pageFaults','returnedBytes','limitFlags','activeLimit','assignedProcesses')
    require(all(type(record[k]) is int for k in numeric) and record['returnedBytes']==48
            and record['limitFlags']==0x2008 and record['activeLimit']==1
            and re.fullmatch('[0-9a-f]{96}',record['accountingBytes']), 'Incomplete Job/limit evidence')
    raw=bytes.fromhex(record['accountingBytes'])
    require([int.from_bytes(raw[n:n+4],'little') for n in (32,36,40,44)] ==
            [record[k] for k in ('pageFaults','totalProcesses','activeProcesses','terminatedProcesses')], 'Contradictory native accounting bytes')
    require(record['totalProcesses']==1 and record['activeProcesses']==record['assignedProcesses']==int(before)
            and record['terminatedProcesses']==0
            and record['processIds']==([pid] if before else []) and all(type(p) is int for p in record['processIds']),
            'Unexpected Job member or cumulative process count')


def audit(output, expected):
    process = read_json(output / 'process.json')
    require(type(process['processId']) is int and process['processId'] > 0 and type(process['exitCode']) is int
            and process['exitCode'] == 0 and process['exitCodeHex'] == '00000000' and process['error'] is None,
            'Native process failed or identity is missing')
    require(all(process[k] is True for k in ('created', 'assigned', 'resumed', 'cleanupVerified'))
            and process['timedOut'] is False and process['totalProcesses'] == 1 and type(process['totalProcesses']) is int
            and process['activeProcesses'] == 0 and type(process['activeProcesses']) is int
            and process['retryCount'] == 0 and process['timeoutSeconds'] == 30 and process['cleanupSeconds'] == 5
            and not any(k in process for k in ('watchdogError', 'cleanupError', 'closeError')), 'Supervision or cleanup is incomplete')
    require(all(type(process[k]) is int for k in ('retryCount','timeoutSeconds','cleanupSeconds')), 'Invalid supervision types')
    times = [process[k] for k in ('createdSeconds', 'assignedSeconds', 'resumedSeconds', 'observedExitSeconds')]
    require(all(type(t) in (int, float) and 0 <= t <= 30 for t in times) and times == sorted(times), 'Invalid supervision chronology')
    cleanup = [process[k] for k in ('cleanupStartedSeconds','cleanupFinishedSeconds','finishedSeconds')]
    require(all(type(t) in (int,float) and 0 <= t <= 35 for t in cleanup)
            and times[-1] <= cleanup[0] <= cleanup[1] == cleanup[2] and cleanup[1]-cleanup[0] <= 5, 'Invalid or late cleanup chronology')
    observation = read_json(output / 'stdout.json'); pid = process['processId']
    verify_job(process['assignmentObservation'],pid,True);verify_job(process['cleanupObservation'],pid,False)
    require(observation['processId'] == pid and type(observation['processId']) is int, 'Stdout PID mismatch')
    verify_identity(observation['identity'], expected, pid, 'BEFORE_GETS')
    verify_identity(observation['identityAfter'], expected, pid, 'AFTER_GETS')
    require(observation['sourceRevision'] == expected['sourceRevision'] and observation['runtimeVersion'] == '10.0.14'
            and observation['coreSha256'] == expected['core']['sha256'] and observation['corePath'] == expected['core']['path']
            and observation['processSha256'] == expected['process']['sha256'] and observation['processPath'] == expected['process']['path']
            and observation['assemblySha256'] == expected['assembly']['sha256'], 'Contradictory final self-identity')
    require(observation['schemaVersion'] == 1 and type(observation['schemaVersion']) is int and observation['kind'] == 'READ_ONLY_WIN32_DESKTOP'
            and observation['qualificationProcesses'] == 0 and type(observation['qualificationProcesses']) is int
            and all(observation[k] is False for k in ('windowCreation', 'wpfResourceAccess', 'inputChanges', 'displayChanges', 'appearanceChanges')),
            'Wrong observation scope')
    assemblies = observation['assemblies']
    require(isinstance(assemblies, list) and len(assemblies) == len(set(assemblies))
            and all(isinstance(name,str) and name for name in assemblies)
            and {'System.Private.CoreLib', 'DesktopProbe'} <= set(assemblies)
            and not set(assemblies) & {'PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Windows.Forms'}, 'UI or missing assembly identity')
    raw = (output / 'stderr.jsonl').read_bytes()
    require(raw.endswith(b'\n'), 'Truncated native journal')
    records = [json.loads(line, object_pairs_hook=_unique, parse_constant=lambda _: require(False, 'Non-finite journal')) for line in raw.splitlines()]
    for record in records: exact_keys(record, ('boundary','timestamp','processId','data'))
    require([r['boundary'] for r in records] == ['guardedStart', 'runtimeIdentity'] + SNAPSHOT * 2 + ['runtimeIdentityAfter'], 'Missing, duplicate or reordered native records')
    ticks = [r['timestamp'] for r in records]
    require(all(type(t) is int and t > 0 for t in ticks) and ticks == sorted(ticks)
            and all(type(r['processId']) is int and r['processId'] == pid for r in records), 'Journal PID/chronology mismatch')
    require(records[0]['data'] == dict(processId=pid, runtime='10.0.14') and records[1]['data'] == observation['identity'], 'Pre-GET identity differs')
    require(records[-1]['data'] == observation['identityAfter'], 'Post-GET identity differs')
    before, after = observation['before'], observation['after']
    require({k:v for k,v in before.items() if k not in ('started','finished')} ==
            {k:v for k,v in after.items() if k not in ('started','finished')}, 'Fixed observations disagree')
    require(all(type(s[k]) is int for s in (before, after) for k in ('started','finished'))
            and records[1]['timestamp'] <= before['started'] <= before['finished'] <= after['started'] <= after['finished'] <= records[-1]['timestamp'], 'Snapshot order invalid')
    for index, snapshot in enumerate((before, after)):
        appearance = appearance_state(snapshot['appearance'])
        part = records[2 + index * 14:2 + (index + 1) * 14]
        require(snapshot['started'] <= part[0]['timestamp'] <= part[-1]['timestamp'] <= snapshot['finished'], 'Capture boundary escaped snapshot')
        require(part[10]['data'] == dict(flags=appearance['flags'], pointerWasNull=appearance['schemePointerWasNull'], scheme=appearance['scheme']),
                'Native copy contradicts final snapshot')
        geometry = part[8]['data']
        geometry_keys = ('primaryWorkPixels','dpi','monitors','sessionId','activeConsoleSession','remoteSession',
                         'desktop','inputDesktop','windowStation','windowConstraintsPixels')
        exact_keys(geometry,geometry_keys); exact_keys(snapshot,(*geometry_keys,'started','finished','appearance'))
        require(all(snapshot[k] == v for k,v in geometry.items()) and len(snapshot['monitors']) == 1, 'Native geometry contradicts final snapshot')
        require(part[1]['data'] == dict(awareness=snapshot['dpi']['processAwareness'],threadAwareness=snapshot['dpi']['threadAwareness'])
                and part[2]['data'] == snapshot['primaryWorkPixels'], 'Early geometry journal differs')
        monitor = snapshot['monitors'][0]
        require(part[4]['data'] == dict(device=monitor['device'],monitor=monitor['monitorPixels'],work=monitor['workPixels']), 'Monitor journal differs')
        require(all(part[n]['data'] is None for n in (0,3,5,6,7,9,11,12,13)), 'Contradictory native boundary data')
    return dict(collectionValidity='VALID', nativeVerification='PASS_TWO_FIXED_READS_ONLY', independentReview='PENDING',
                originalCorrectness='NOT_RUN', presentationOrder='INCONCLUSIVE', infrastructureEligibility='BLOCKED',
                campaignExecutionApproved=False, status='NOT_AUTHORISED')


def api(endpoint):
    request = urllib.request.Request(f'https://api.github.com/repos/{REPOSITORY}/' + endpoint,
        headers={'Authorization': 'Bearer ' + os.environ['GH_TOKEN'], 'Accept':'application/vnd.github+json', 'X-GitHub-Api-Version':'2022-11-28'})
    with urllib.request.urlopen(request, timeout=20) as response:
        raw = response.read(2 * 1024 * 1024 + 1)
    require(len(raw) <= 2 * 1024 * 1024, 'Oversized GitHub evidence')
    return json.loads(raw, object_pairs_hook=_unique, parse_constant=lambda _: require(False, 'Non-finite API record'))


def audit_package(output, config, expected_owner, authority_env):
    """Offline final-evidence review entry; cannot launch or authorise any process."""
    validate_config(config)
    require(config['supervisorQualification']=='PASS_INDEPENDENTLY_REVIEWED', 'Supervisor prerequisite remains blocked')
    require(not (output/'failure.json').exists(), 'Retained failure cannot become PASS')
    actual=closure(output);recorded=read_json(output/'files.json');actual.pop('files.json')
    require(actual==recorded, 'Missing, extra or changed evidence files')
    require(digest(output/'configuration.json')==expected_owner['configurationSha256']
            and read_json(output/'configuration.json')==config, 'Configuration evidence differs')
    require(read_json(output/'environment.json')==config['environment'], 'Hosted image drift')
    commit=read_json(output/'execution-commit.json')
    require(commit['sha']==expected_owner['executionCommit'] and commit['tree']['sha']==expected_owner['executionTree'], 'Execution source evidence differs')
    for suffix in ('','-before-launch','-after'):
        authority=read_json(output/('owner'+suffix+'.json'));verify_authority(authority,expected_owner,authority_env)
        run_suffix='-before' if not suffix else suffix
        verify_run_history(read_json(output/('history'+run_suffix+'.json')),read_json(output/('run'+run_suffix+'.json')),authority_env,authority['created_at'])
    require(closure(output/'collector')==config['collector']['files'] and closure(output/'runtime')==config['runtime']['files'], 'Evidence binary/runtime closure differs')
    hosted=Path(config['workspace'])/'artifacts/goal77-highcontrast-live'
    expected=expected_identity(config,hosted/'collector',hosted/'runtime')
    require(read_json(output/'expected-identity.json')==expected, 'Self-supplied identity record differs from sealed configuration')
    verdict=audit(output,expected)
    require(read_json(output/'terminal.json')==verdict, 'Terminal result contradicts original observations')
    return verdict  # Independent human/agent review must still inspect GitHub terminal status/artifact provenance.


def execute():
    revision = guard(os.environ)  # Before paths, output, Windows imports or any child.
    output = ROOT / 'artifacts/goal77-highcontrast-live'; output.mkdir(parents=True, exist_ok=False)
    try:
        config = read_json(ROOT / CONFIG); validate_config(config); config_sha = digest(ROOT / CONFIG)
        write(output/'configuration.json',config)
        require(config['supervisorQualification'] == 'PASS_INDEPENDENTLY_REVIEWED',
                'BLOCKED: local supervisor Job accounting 2 total / 1 active remains unresolved; no hosted execution')
        require((ROOT / '.git/HEAD').read_text().strip() == revision, 'Exact detached checkout required')
        for relative, expected in config['implementationFiles'].items(): require(digest(ROOT / relative) == expected, 'Reviewed implementation changed')
        commit = api('git/commits/' + revision); write(output/'execution-commit.json',commit); tree = commit['tree']['sha']
        require(commit['sha'] == revision, 'Execution commit identity differs')
        expected_owner = owner_payload(config, config_sha, revision, tree, approved=True)
        authority = api('issues/comments/' + os.environ['AUTHORISATION_COMMENT_ID']); write(output/'owner.json',authority)
        verify_authority(authority, expected_owner, os.environ)
        current = api('actions/runs/' + os.environ['GITHUB_RUN_ID']); write(output/'run-before.json',current)
        history_path = f'actions/workflows/{Path(WORKFLOW).name}/runs?event=workflow_dispatch&head_sha={revision}&per_page=100'
        history = api(history_path); write(output/'history-before.json',history)
        verify_run_history(history, current, os.environ, authority['created_at'])
        require(os.name == 'nt' and str(ROOT).casefold() == config['workspace'].casefold(), 'Wrong Windows workspace')
        sys.path.insert(0, str(ROOT / 'scripts/native/qualification'))
        import environment_lock
        environment = environment_lock.identity(); write(output/'environment.json',environment)
        environment_lock.check_identity(config['environment'], environment)
        binary_archive = ROOT / config['collector']['archivePath']
        require(digest(binary_archive) == config['collector']['archiveSha256'], 'Wrong sealed binary archive')
        binary_root = output/'collector'; runtime_root = output/'runtime'
        require(extract_archive(binary_archive,binary_root,len(config['collector']['files'])) == config['collector']['files'], 'Wrong collector closure')
        runtime_archive = output/'runtime.zip'; declaration = config['runtime']['archive']
        with urllib.request.urlopen(declaration['url'],timeout=30) as response, runtime_archive.open('xb') as stream:
            remaining = declaration['bytes']
            while remaining:
                block=response.read(min(1024*1024,remaining)); require(block,'Truncated runtime download'); stream.write(block); remaining-=len(block)
            require(not response.read(1),'Oversized runtime download')
        require(digest(runtime_archive) == declaration['sha256'] and hashlib.sha512(runtime_archive.read_bytes()).hexdigest() == declaration['sha512'], 'Runtime archive changed')
        require(extract_archive(runtime_archive,runtime_root,len(config['runtime']['files'])) == config['runtime']['files'], 'Wrong runtime closure')
        runtime_config = read_json(binary_root/'DesktopProbe.runtimeconfig.json')['runtimeOptions']
        require(runtime_config['rollForward']=='Disable' and runtime_config['framework']==dict(name='Microsoft.NETCore.App',version='10.0.14'), 'Runtime roll-forward/configuration differs')
        identity = expected_identity(config,binary_root,runtime_root); write(output/'expected-identity.json',identity)
        # Revalidate sole dispatch immediately before the one permitted process.
        launch_authority=api('issues/comments/'+os.environ['AUTHORISATION_COMMENT_ID'])
        write(output/'owner-before-launch.json',launch_authority); verify_authority(launch_authority,expected_owner,os.environ)
        launch_history=api(history_path);launch_run=api('actions/runs/'+os.environ['GITHUB_RUN_ID'])
        write(output/'history-before-launch.json',launch_history);write(output/'run-before-launch.json',launch_run)
        verify_run_history(launch_history,launch_run,os.environ,authority['created_at'])
        child = child_environment(os.environ,runtime_root,output/'expected-identity.json')
        from live_process import supervise
        supervise(binary_root/'DesktopProbe.exe', child, output, write)
        require(closure(binary_root)==config['collector']['files'] and closure(runtime_root)==config['runtime']['files'], 'Files changed during observation')
        final_authority=api('issues/comments/'+os.environ['AUTHORISATION_COMMENT_ID']); verify_authority(final_authority,expected_owner,os.environ)
        final_history=api(history_path); final_run=api('actions/runs/'+os.environ['GITHUB_RUN_ID'])
        write(output/'owner-after.json',final_authority); write(output/'history-after.json',final_history); write(output/'run-after.json',final_run)
        verify_run_history(final_history,final_run,os.environ,authority['created_at'])
        write(output/'terminal.json',audit(output,identity))
    except BaseException as error:
        launched = (output/'process.json').exists() and read_json(output/'process.json')['created'] is True
        write(output/'failure.json',dict(collectionValidity='INVALID' if launched else 'NOT_RUN',
              infrastructureEligibility='BLOCKED',error=type(error).__name__+': '+str(error),
              originalCorrectness='NOT_RUN',campaignExecutionApproved=False,status='NOT_AUTHORISED'))
        raise
    finally: write(output/'files.json',closure(output))


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('action',choices=['execute']); parser.parse_args()
    try: execute()
    except Exception as error: print('BLOCKED / INVALID: '+str(error),file=sys.stderr); sys.exit(2)
