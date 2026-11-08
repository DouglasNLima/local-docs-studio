"""One local harmless-fixture control, only after a new exact owner comment.

No build, collector, hosted job, UI or appearance operation. No automatic retry.
Importing this module is safe for offline proposal/authority tests.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import urllib.request
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parent))
from live_verification import ROOT, IMPLEMENTATION, _unique, require, verify_authority
from discover_desktop import read_json, write, digest
from job_evidence import CLEANUP_TARGETS, PHASES, verify_supervision

PLAN = 'docs/native/qualification/job-supervisor-control.json'
SOURCES = (*IMPLEMENTATION, 'scripts/native/qualification_hosted/local_job_control.py')
BOUNDS = dict(fixtureProcesses=1, assignmentAttempts=1, supervisionSeconds=30, cleanupSeconds=5,
              jobObservations=10, pidListCapacity=8, retries=0, collectorProcesses=0, hostedRuns=0,
              qualificationProcesses=0, calibrationProcesses=0)
EXPECTED_REPORT = dict(nativeChecks=21,pointerBytes=8,structureBytes=16,offsets=[0,4,8],wcharBytes=2,boolBytes=4,
                       allocations=2,ownerReleases=2,borrowerReleaseDenials=1,activeAllocations=0,actualHighContrastCalls=0)


def validate_plan(plan):
    require(plan['status']=='NOT_AUTHORISED' and plan['approved'] is False
            and plan['campaignExecutionApproved'] is False, 'Plan cannot grant authority')
    require(plan['bounds']==BOUNDS and all(type(n) is int for n in plan['bounds'].values())
            and plan['cleanupTargets']==list(CLEANUP_TARGETS) and plan['phases']==list(PHASES), 'Changed local control bounds')
    require(json.dumps(plan['expectedFixtureReport'],sort_keys=True)==json.dumps(EXPECTED_REPORT,sort_keys=True),
            'Changed native fixture expectations')
    require(set(plan['pythonFiles'])=={plan['pythonExecutable'],str(Path(plan['pythonExecutable']).parent/'python312.dll')}
            and all(re.fullmatch('[0-9a-f]{64}',h) for h in plan['pythonFiles'].values()), 'Incomplete Python identity')
    require(set(plan['sourceFiles'])==set(plan['executionFiles'])==set(SOURCES)
            and all(re.fullmatch('[0-9a-f]{64}',h) for field in ('sourceFiles','executionFiles') for h in plan[field].values()),
            'Incomplete reviewed source closure')
    require(plan['fixture']['sha256']=='311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549'
            and plan['fixture']['archive']=='docs/native/evidence/buffer-safety-recovery/offline-original-bytes.zip'
            and plan['fixture']['archiveSha256']=='d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27'
            and plan['fixture']['member']=='attempt-6/NativeBufferSelfTest.exe' and plan['fixture']['bytes']==20480,
            'Different fixture or historical archive')
    for key in ('sourceCommit','sourceTree'):
        require(re.fullmatch('[0-9a-f]{40}',plan[key]), 'Moving source identity')


def owner_proposal(plan, plan_sha, commit, tree, approved=False):
    return dict(schemaVersion=1,repository='DouglasNLima/local-docs-studio',issue=77,
                decision='AUTHORISE_ONE_LOCAL_JOB_FIXTURE_CONTROL' if approved else 'PROPOSED_NOT_AUTHORISED',
                approved=approved,executionCommit=commit,executionTree=tree,sourceCommit=plan['sourceCommit'],
                sourceTree=plan['sourceTree'],planSha256=plan_sha,fixtureSha256=plan['fixture']['sha256'],
                pythonFiles=plan['pythonFiles'],bounds=BOUNDS,cleanupTargets=list(CLEANUP_TARGETS),
                exclusion='No HIGHCONTRAST collector, hosted workflow, UI/input/display/appearance change, retry, R01/R02, calibration or policy activation')


def fetch_owner(comment_id):
    require(re.fullmatch('[1-9][0-9]*',comment_id), 'Invalid owner comment identifier')
    url='https://api.github.com/repos/DouglasNLima/local-docs-studio/issues/comments/'+comment_id
    with urllib.request.urlopen(urllib.request.Request(url,headers={'Accept':'application/vnd.github+json'}),timeout=20) as response:
        raw=response.read(131073)
    require(len(raw)<=131072,'Oversized authority record')
    return json.loads(raw,object_pairs_hook=_unique,parse_constant=lambda _:require(False,'Non-finite authority'))


def verify_files(plan):
    for path,sha in plan['executionFiles'].items():
        require(digest(ROOT/path)==sha, 'Observed local source bytes changed, including line endings')
    for path,sha in plan['pythonFiles'].items():
        require(digest(path)==sha,'Python identity changed')


def execute(comment_id, body_sha):
    require(sys.flags.isolated == 1 and sys.flags.no_site == 1, 'Use the exact isolated Python -I -S command')
    require(os.name=='nt' and not os.environ.get('GITHUB_ACTIONS'), 'Local Windows only')
    plan=read_json(ROOT/PLAN);validate_plan(plan);plan_sha=digest(ROOT/PLAN)
    require(str(ROOT).casefold()==plan['workspace'].casefold() and sys.version==plan['pythonVersion'], 'Local workspace/interpreter differs')
    git=lambda *args:subprocess.check_output(['git','-C',str(ROOT),*args]).decode().strip()
    require(not git('status','--porcelain'), 'Clean reviewed checkout required')
    commit,tree=git('rev-parse','HEAD'),git('rev-parse','HEAD^{tree}')
    require(git('rev-parse',plan['sourceCommit']+'^{tree}')==plan['sourceTree'], 'Source tree differs')
    require(not git('diff','--name-only',plan['sourceCommit'],'HEAD','--',*SOURCES), 'Execution source differs from reviewed source')
    verify_files(plan)
    for path in plan['sourceFiles']:
        blob=subprocess.check_output(['git','-C',str(ROOT),'show',plan['sourceCommit']+':'+path])
        require(hashlib.sha256(blob).hexdigest()==plan['sourceFiles'][path], 'Canonical source binding differs')
    require(str(Path(sys.executable)).casefold()==plan['pythonExecutable'].casefold(), 'Wrong supervisor executable')
    expected=owner_proposal(plan,plan_sha,commit,tree,approved=True)
    authority_env=dict(AUTHORISATION_COMMENT_ID=comment_id,AUTHORISATION_BODY_SHA256=body_sha)
    authority=fetch_owner(comment_id);verify_authority(authority,expected,authority_env)
    # A fixed, exclusive directory is the local spent-attempt record. Never remove
    # or rename it to retry. A failed prerequisite after reservation spends the run.
    output=ROOT/'artifacts'/('goal77-job-control-'+commit);output.mkdir(exist_ok=False)
    try:
        write(output/'owner-before.json',authority);write(output/'plan.json',plan)
        archive=ROOT/plan['fixture']['archive'];require(digest(archive)==plan['fixture']['archiveSha256'],'Fixture archive changed')
        with zipfile.ZipFile(archive) as z:
            require(z.namelist().count(plan['fixture']['member'])==1,'Missing or duplicate fixture member')
            binary_bytes=z.read(plan['fixture']['member'])
        require(len(binary_bytes)==20480 and hashlib.sha256(binary_bytes).hexdigest()==plan['fixture']['sha256'],'Fixture bytes changed')
        binary=output/'NativeBufferSelfTest.exe';binary.write_bytes(binary_bytes)
        child={k:os.environ[k] for k in ('SystemRoot','WINDIR','TEMP','TMP','USERPROFILE')}
        child['PATH']=str(Path(child['SystemRoot'])/'System32')
        write(output/'child-environment.json',child)
        authority=fetch_owner(comment_id);write(output/'owner-before-launch.json',authority)
        verify_authority(authority,expected,authority_env)
        require(digest(ROOT/PLAN)==plan_sha and git('rev-parse','HEAD')==commit and not git('status','--porcelain'),
                'Plan or checkout changed before launch')
        verify_files(plan)
        from live_process import supervise
        record=supervise(binary,child,output,write)  # Exactly one native fixture; no retry.
        require(digest(ROOT/PLAN)==plan_sha and git('rev-parse','HEAD')==commit and not git('status','--porcelain'),
                'Plan or checkout changed during collection')
        verify_files(plan)
        verify_supervision(record)
        require(record['binarySha256']==plan['fixture']['sha256'] and record['parentIdentity']['imageSha256']==plan['pythonFiles'][plan['pythonExecutable']],
                'Observed child/supervisor executable differs')
        require(digest(output/'supervisor.jsonl')==record['supervisorJournalSha256']
                and [json.loads(line,object_pairs_hook=_unique) for line in (output/'supervisor.jsonl').read_bytes().splitlines()]==record['operations'],
                'Supervisor journal differs')
        native=read_json(output/'stdout.json')
        require(native==plan['expectedFixtureReport'] and all(type(v) is int for k,v in native.items() if k!='offsets')
                and all(type(v) is int for v in native['offsets']) and (output/'stderr.jsonl').read_bytes()==b'', 'Fixture observation differs')
        require(digest(binary)==plan['fixture']['sha256'],'Fixture changed during control')
        for path,sha in plan['pythonFiles'].items():require(digest(path)==sha,'Python identity changed during control')
        final_owner=fetch_owner(comment_id);write(output/'owner-after.json',final_owner)
        verify_authority(final_owner,expected,authority_env)
        write(output/'terminal.json',dict(status='COLLECTED_PENDING_INDEPENDENT_REVIEW',nativeControl='PASS_FIXED_JOB_OBSERVATIONS_ONLY',
              originalFailure='RETAINED_INCONCLUSIVE_CAUSE',collectorReadiness='BLOCKED',campaignExecutionApproved=False))
    except BaseException as error:
        write(output/'failure.json',dict(status='FAIL_OR_INCOMPLETE',error=type(error).__name__+': '+str(error),collectorReadiness='BLOCKED'))
        raise
    finally:
        write(output/'files.json',{p.name:digest(p) for p in output.iterdir() if p.is_file()})


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--authorisation-comment-id',required=True)
    parser.add_argument('--authorisation-body-sha256',required=True)
    args=parser.parse_args()
    execute(args.authorisation_comment_id,args.authorisation_body_sha256)
