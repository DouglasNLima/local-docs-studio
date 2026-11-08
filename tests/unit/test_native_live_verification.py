"""Offline controls only: fake kernel/processes, synthetic records, no Win32 calls."""
import copy
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/native/qualification_hosted'))
import live_verification as v
import live_process as process
from eligibility import HC_OWNERSHIP_CONTRACT


def config():
    return dict(schemaVersion=1,kind='CORRECTED_HIGHCONTRAST_SINGLE_PROCESS',status='NOT_AUTHORISED',
                campaignExecutionApproved=False,readOnlyVerificationApproved=False,bounds=copy.deepcopy(v.BOUNDS),
                supervisorQualification='BLOCKED_LOCAL_JOB_ACCOUNTING_UNRESOLVED',
                environment=dict(imageVersion='20261103.298.1'),implementationFiles={p:('b' if p in v.COLLECTOR_SOURCES else 'a')*64 for p in v.IMPLEMENTATION},
                collector=dict(sourceCommit='a'*40,sourceTree='b'*40,archiveSha256='c'*64,
                  sourceFiles={p:'b'*64 for p in v.COLLECTOR_SOURCES},
                  files={f'DesktopProbe.{name}':'d'*64 for name in ('exe','dll','pdb','deps.json','runtimeconfig.json')}),
                runtime=dict(version='10.0.14',archive=dict(url='https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-win-x64.zip',sha256='e'*64),
                  files={name:'f'*64 for name in ('host/fxr/10.0.14/hostfxr.dll',
                      'shared/Microsoft.NETCore.App/10.0.14/hostpolicy.dll','shared/Microsoft.NETCore.App/10.0.14/coreclr.dll',
                      'shared/Microsoft.NETCore.App/10.0.14/System.Private.CoreLib.dll')}))


def env():
    return dict(GITHUB_ACTIONS='true',RUNNER_ENVIRONMENT='github-hosted',RUNNER_OS='Windows',RUNNER_ARCH='X64',
                GITHUB_REPOSITORY=v.REPOSITORY,GITHUB_EVENT_NAME='workflow_dispatch',GITHUB_REF=v.BRANCH,
                GITHUB_ACTOR='DouglasNLima',GITHUB_TRIGGERING_ACTOR='DouglasNLima',GITHUB_RUN_ATTEMPT='1',
                GOAL77_HIGHCONTRAST_VERIFICATION='true',EXPECTED_VERIFICATION_SHA='c'*40,GITHUB_SHA='c'*40,GITHUB_WORKFLOW_SHA='c'*40,
                GITHUB_WORKFLOW_REF=v.REPOSITORY+'/'+v.WORKFLOW+'@'+v.BRANCH,AUTHORISATION_COMMENT_ID='77',
                AUTHORISATION_BODY_SHA256='d'*64,GITHUB_RUN_ID='900')


class Timer:
    def __init__(self,seconds,callback): assert seconds==30
    def start(self): pass
    def cancel(self): pass


def jobdata(total,active):
    raw=bytes(32)+b''.join(n.to_bytes(4,'little') for n in (0,total,active,0))
    return dict(totalProcesses=total,activeProcesses=active,terminatedProcesses=0,pageFaults=0,
                processIds=[133] if active else [],limitFlags=0x2008,activeLimit=1,
                assignedProcesses=active,returnedBytes=48,accountingBytes=raw.hex())


class FakeKernel:
    def __init__(self,fail=None,code=0,counts=(1,0)):
        self.calls=[];self.fail=fail;self.code=code;self.final_counts=counts;self.waits=0
    def call(self,name):
        self.calls.append(name)
        if self.fail==name: raise RuntimeError('Injected '+name+' failure')
    def job(self): self.call('job');return 10
    def create(self,*args): self.call('create');return 11,12,133,134
    def assign(self,*args): self.call('assign')
    def resume(self,*args): self.call('resume')
    def wait(self,handle,seconds):
        self.call('wait');self.waits+=1
        return self.fail!='timeout' or self.waits>1
    def exit(self,*args): self.call('exit');return self.code
    def stop(self,job,handle,assigned): self.call('stop-job' if assigned else 'stop-direct')
    def counts(self,*args): self.call('counts');return self.final_counts
    def inspect(self,*args):
        before='resume' not in self.calls and not any(name.startswith('stop-') for name in self.calls)
        total,active=(1,1) if before else self.final_counts
        return jobdata(total,active)
    def close(self,*args): self.call('close')


def appearance():
    return dict(flags=126,enabled=False,scheme='',schemeKind='EMPTY',schemePointerWasNull=False,schemeOwnership='BORROWED_OS',
                schemeCopyComplete=True,schemeReleaseAttempted=False,ownershipContract=HC_OWNERSHIP_CONTRACT,
                theme=dict(hresult=0,file='normal',colour='normal',size='normal'),
                colours={str(n):0 for n in (2,3,5,6,7,8,9,13,14,15,16,17,18,20,26)})


def evidence(root):
    cfg=config();expected=v.expected_identity(cfg,root/'collector',root/'runtime')
    identity=dict(processId=133,architecture='X64',runtimeVersion='10.0.14',sourceRevision=expected['sourceRevision'],
                  phase='BEFORE_GETS',files={k:expected[k] for k in ('process','assembly','core')},modules={})
    for name in ('hostfxr.dll','hostpolicy.dll','coreclr.dll'):
        relative=next(p for p in expected['runtimeFiles'] if p.endswith('/'+name))
        identity['modules'][name]=dict(relative=relative,path=str((Path(expected['runtimeRoot'])/relative).resolve()),sha256=expected['runtimeFiles'][relative])
    rect=dict(left=0,top=0,right=1024,bottom=720)
    geometry=dict(primaryWorkPixels=rect,dpi=dict(processAwareness=1,threadAwareness=1),
                  monitors=[dict(device='DISPLAY1',monitorPixels=rect,workPixels=rect)],sessionId=2,activeConsoleSession=2,
                  remoteSession=False,desktop='Default',inputDesktop='Default',windowStation='WinSta0',windowConstraintsPixels={})
    before=dict(geometry,started=3,finished=17,appearance=appearance());after=dict(geometry,started=18,finished=32,appearance=appearance())
    observation=dict(schemaVersion=1,kind='READ_ONLY_WIN32_DESKTOP',processId=133,identity=identity,identityAfter=copy.deepcopy(identity),
                     sourceRevision=expected['sourceRevision'],runtimeVersion='10.0.14',coreSha256=expected['core']['sha256'],corePath=expected['core']['path'],
                     processSha256=expected['process']['sha256'],processPath=expected['process']['path'],assemblySha256=expected['assembly']['sha256'],
                     qualificationProcesses=0,windowCreation=False,wpfResourceAccess=False,inputChanges=False,displayChanges=False,appearanceChanges=False,
                     assemblies=['System.Private.CoreLib','DesktopProbe'],before=before,after=after)
    observation['identityAfter']['phase']='AFTER_GETS'
    records=[dict(boundary='guardedStart',timestamp=1,processId=133,data=dict(processId=133,runtime='10.0.14')),
             dict(boundary='runtimeIdentity',timestamp=2,processId=133,data=identity)]
    for start in (3,18):
        for n,name in enumerate(v.SNAPSHOT):
            data=None
            if n==1:data=dict(awareness=1,threadAwareness=1)
            if n==2:data=rect
            if n==4:data=dict(device='DISPLAY1',monitor=rect,work=rect)
            if n==8:data=geometry
            if n==10:data=dict(flags=126,pointerWasNull=False,scheme='')
            records.append(dict(boundary=name,timestamp=start+n,processId=133,data=data))
    records.append(dict(boundary='runtimeIdentityAfter',timestamp=33,processId=133,data=observation['identityAfter']))
    child=dict(processId=133,exitCode=0,exitCodeHex='00000000',error=None,created=True,assigned=True,resumed=True,cleanupVerified=True,
               timedOut=False,totalProcesses=1,activeProcesses=0,retryCount=0,timeoutSeconds=30,cleanupSeconds=5,
               createdSeconds=0.1,assignedSeconds=0.2,resumedSeconds=0.3,observedExitSeconds=1,
               cleanupStartedSeconds=1.1,cleanupFinishedSeconds=1.2,finishedSeconds=1.2)
    child.update(assignmentObservation=jobdata(1,1),cleanupObservation=jobdata(1,0))
    return expected,observation,records,child


class LiveVerificationControls(unittest.TestCase):
    def test_configuration_never_grants_authority_and_rejects_changed_bounds(self):
        v.validate_config(config())
        for key,value in [('status','AUTHORISED'),('readOnlyVerificationApproved',True),('campaignExecutionApproved',True)]:
            bad=config();bad[key]=value
            with self.subTest(key=key),self.assertRaises(ValueError):v.validate_config(bad)
        for key in v.BOUNDS:
            bad=config();bad['bounds'][key]+=1
            with self.subTest(key=key),self.assertRaises(ValueError):v.validate_config(bad)

    def test_host_owner_attempt_source_and_authority_guards_fail_closed(self):
        v.guard(env())
        for key in env():
            if key=='GITHUB_RUN_ID':continue
            bad=env();bad[key]=''
            with self.subTest(key=key),self.assertRaises(ValueError):v.guard(bad)

    def test_known_supervisor_failure_blocks_before_authority_network_or_native_access(self):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);path=root/v.CONFIG;path.parent.mkdir(parents=True);v.write(path,config())
            with patch.object(v,'ROOT',root),patch.object(v,'guard',return_value='c'*40),patch.object(v,'api') as api,patch.object(process,'supervise') as launch:
                with self.assertRaisesRegex(ValueError,'local supervisor Job accounting'):v.execute()
                api.assert_not_called();launch.assert_not_called()
            failure=v.read_json(root/'artifacts/goal77-highcontrast-live/failure.json')
            self.assertEqual(failure['collectionValidity'],'NOT_RUN');self.assertEqual(failure['infrastructureEligibility'],'BLOCKED')

    def test_missing_implementation_and_source_dependencies_fail(self):
        for key in ('implementationFiles','sourceFiles'):
            bad=config();mapping=bad[key] if key=='implementationFiles' else bad['collector'][key];mapping.pop(next(iter(mapping)))
            with self.assertRaises(ValueError):v.validate_config(bad)
        bad=config();bad['collector']['sourceFiles'][v.COLLECTOR_SOURCES[0]]='d'*64
        with self.assertRaises(ValueError):v.validate_config(bad)

    def test_child_environment_excludes_tokens_hooks_and_runtime_overrides(self):
        with tempfile.TemporaryDirectory() as temporary:
            identity=Path(temporary)/'identity.json';v.write(identity,{})
            environment={k:temporary for k in ('SystemRoot','WINDIR','TEMP','TMP','USERPROFILE')}
            banned=('GH_TOKEN','GITHUB_TOKEN','DOTNET_STARTUP_HOOKS','DOTNET_ADDITIONAL_DEPS','DOTNET_SHARED_STORE',
                    'COR_ENABLE_PROFILING','CORECLR_PROFILER','COMPLUS_Version','PYTHONPATH')
            environment.update({key:'must-not-forward' for key in banned});environment['DOTNET_ROOT']='moving'
            child=v.child_environment(environment,Path(temporary)/'pinned',identity)
            self.assertFalse(set(banned)&set(child));self.assertNotEqual(child['DOTNET_ROOT'],'moving')
            self.assertEqual(child['DOTNET_ROLL_FORWARD'],'Disable');self.assertEqual(child['DOTNET_EnableDiagnostics'],'0')

    def test_local_cli_cannot_reach_windows_or_output(self):
        environment={k:value for k,value in os.environ.items() if not k.startswith(('GITHUB_','RUNNER_','GOAL77_'))}
        result=subprocess.run([sys.executable,str(ROOT/'scripts/native/qualification_hosted/live_verification.py'),'execute'],
                              env=environment,capture_output=True,text=True)
        self.assertNotEqual(result.returncode,0);self.assertIn('Hosted owner/branch/attempt restriction failed',result.stderr)
        self.assertFalse((ROOT/'artifacts/goal77-highcontrast-live').exists())

    def test_owner_proposal_is_not_approval_and_exact_payload_is_required(self):
        expected=v.owner_payload(config(),'1'*64,'c'*40,'2'*40,True)
        record=dict(id=77,user=dict(login='DouglasNLima',id=33501911),html_url=f'https://github.com/{v.REPOSITORY}/issues/77#issuecomment-77',
                    issue_url=f'https://api.github.com/repos/{v.REPOSITORY}/issues/77',created_at='2026-11-08T01:00:00Z',updated_at='2026-11-08T01:00:00Z',body=json.dumps(expected))
        environment=env();environment['AUTHORISATION_BODY_SHA256']=hashlib.sha256(record['body'].encode()).hexdigest()
        v.verify_authority(record,expected,environment)
        for key,value in [('id',78),('user',dict(login='DouglasNLima',id=1)),('updated_at','later'),('issue_url','wrong'),('body',record['body']+' ')]:
            bad=copy.deepcopy(record);bad[key]=value
            with self.subTest(key=key),self.assertRaises(ValueError):v.verify_authority(bad,expected,environment)
        for payload in (v.owner_payload(config(),'1'*64,'c'*40,'2'*40),dict(expected,configurationSha256='3'*64),dict(expected,bounds=dict(v.BOUNDS,collectorProcesses=2))):
            bad=dict(record,body=json.dumps(payload));other=dict(environment,AUTHORISATION_BODY_SHA256=hashlib.sha256(bad['body'].encode()).hexdigest())
            with self.assertRaises(ValueError):v.verify_authority(bad,expected,other)

    def test_prior_parallel_retried_and_truncated_dispatches_are_rejected(self):
        run=dict(id=900,run_attempt=1,head_sha='c'*40,event='workflow_dispatch',path=v.WORKFLOW,
                 head_branch=v.BRANCH.removeprefix('refs/heads/'),status='in_progress',actor=dict(login='DouglasNLima',id=33501911),
                 triggering_actor=dict(login='DouglasNLima',id=33501911),
                 repository=dict(full_name=v.REPOSITORY),workflow_id=v.WORKFLOW_ID,created_at='2026-11-08T02:00:00Z')
        history=dict(total_count=1,workflow_runs=[run]);v.verify_run_history(history,run,env(),'2026-11-08T01:00:00Z')
        for bad in (dict(total_count=0,workflow_runs=[]),dict(total_count=2,workflow_runs=[run,run]),dict(total_count=2,workflow_runs=[run])):
            with self.assertRaises(ValueError):v.verify_run_history(bad,run,env(),'2026-11-08T01:00:00Z')
        for key,value in [('run_attempt',2),('id',901),('path','other'),('head_sha','d'*40),('workflow_id',1),('event','push')]:
            bad=dict(run,**{key:value})
            with self.assertRaises(ValueError):v.verify_run_history(dict(total_count=1,workflow_runs=[bad]),bad,env(),'2026-11-08T01:00:00Z')

    def supervise(self,kernel):
        with tempfile.TemporaryDirectory() as temporary:
            return process.supervise(Path('never-run.exe'),{},Path(temporary),v.write,kernel,clock=lambda:1,timer=Timer)

    def test_suspended_assignment_precedes_resume_and_exactly_one_child_is_created(self):
        kernel=FakeKernel();result=self.supervise(kernel)
        self.assertEqual(kernel.calls[:4],['job','create','assign','resume'])
        self.assertEqual(kernel.calls.count('create'),1);self.assertEqual(result['processId'],133)
        self.assertTrue(result['cleanupVerified']);self.assertEqual(result['exitCodeHex'],'00000000')

    def test_assignment_failure_never_resumes_and_terminates_suspended_direct_child(self):
        kernel=FakeKernel('assign');result=self.supervise(kernel)
        self.assertNotIn('resume',kernel.calls);self.assertIn('stop-direct',kernel.calls)
        self.assertFalse(result['cleanupVerified']);self.assertIn('Injected',result['error'])

    def test_post_create_handle_restoration_failure_cannot_lose_the_suspended_child(self):
        kernel=FakeKernel()
        def broken(*args):
            kernel.calls.append('create');raise process.CreatedProcessError((11,12,133,134),'handle restoration failed')
        kernel.create=broken;result=self.supervise(kernel)
        self.assertTrue(result['created']);self.assertEqual(result['processId'],133)
        self.assertNotIn('resume',kernel.calls);self.assertIn('stop-direct',kernel.calls)
        self.assertEqual(kernel.calls.count('close'),3)

    def test_crash_timeout_and_extra_processes_remain_distinct_failures(self):
        crash=self.supervise(FakeKernel(code=0xc0000374));self.assertEqual(crash['exitCodeHex'],'c0000374')
        kernel=FakeKernel('timeout');timeout=self.supervise(kernel)
        self.assertTrue(timeout['timedOut']);self.assertIn('stop-job',kernel.calls);self.assertEqual(kernel.calls.count('create'),1)
        self.assertFalse(self.supervise(FakeKernel(counts=(2,0)))['cleanupVerified'])
        self.assertFalse(self.supervise(FakeKernel(counts=(1,1)))['cleanupVerified'])

    def check_evidence(self,change=None):
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);expected,observation,records,child=evidence(root)
            if change:change(expected,observation,records,child)
            v.write(root/'stdout.json',observation);v.write(root/'process.json',child)
            (root/'stderr.jsonl').write_text(''.join(json.dumps(r)+'\n' for r in records),encoding='utf-8')
            return v.audit(root,expected)

    def test_complete_synthetic_collection_cannot_qualify_product_or_campaign(self):
        verdict=self.check_evidence();self.assertEqual(verdict['collectionValidity'],'VALID')
        self.assertEqual(verdict['independentReview'],'PENDING');self.assertFalse(verdict['campaignExecutionApproved'])
        self.assertEqual(verdict['originalCorrectness'],'NOT_RUN')

    def test_missing_duplicate_wrong_pid_and_reordered_journals_fail(self):
        changes=[lambda e,o,r,p:r.pop(),lambda e,o,r,p:r.insert(4,copy.deepcopy(r[4])),
                 lambda e,o,r,p:r[10].update(processId=134),lambda e,o,r,p:r[10].update(timestamp=1),
                 lambda e,o,r,p:r[10].update(data={}),lambda e,o,r,p:r[12]['data'].update(scheme='stale')]
        for change in changes:
            with self.subTest(change=change),self.assertRaises((ValueError,KeyError)):self.check_evidence(change)

    def test_runtime_source_file_flags_and_final_identity_contradictions_fail(self):
        changes=[lambda e,o,r,p:o.update(sourceRevision='moving'),lambda e,o,r,p:o.update(processId=134),
                 lambda e,o,r,p:o.update(assemblies=[]),lambda e,o,r,p:o.update(wpfResourceAccess=True),
                 lambda e,o,r,p:o['identity']['modules'].pop('hostfxr.dll'),
                 lambda e,o,r,p:o['identity']['modules']['coreclr.dll'].update(path='C:/wrong/coreclr.dll'),
                 lambda e,o,r,p:o['identity']['modules']['coreclr.dll'].update(sha256='0'*64),
                 lambda e,o,r,p:o['identityAfter'].update(runtimeVersion='10.0.15'),
                 lambda e,o,r,p:o['after']['appearance'].update(scheme='High Contrast Black',schemeKind='NAMED'),
                 lambda e,o,r,p:o['before']['appearance'].update(schemeReleaseAttempted=True)]
        for change in changes:
            with self.subTest(change=change),self.assertRaises((ValueError,KeyError)):self.check_evidence(change)

    def test_process_crash_cleanup_deadline_and_boolean_inconsistencies_fail(self):
        for key,value in [('exitCode',0xc0000374),('exitCodeHex','c0000374'),('cleanupVerified',False),('totalProcesses',2),
                           ('activeProcesses',1),('retryCount',False),('timedOut',True),('resumed',False),('observedExitSeconds',31),
                           ('cleanupFinishedSeconds',9),('cleanupStartedSeconds',float('nan')),('finishedSeconds',36)]:
            with self.subTest(key=key),self.assertRaises(ValueError):self.check_evidence(lambda e,o,r,p:p.update(**{key:value}))
        for key,value in [('accountingBytes','00'*48),('returnedBytes',47),('processIds',[134]),('activeLimit',2),('assignedProcesses',2),('terminatedProcesses',1)]:
            with self.subTest(key=key),self.assertRaises(ValueError):self.check_evidence(lambda e,o,r,p:p['assignmentObservation'].update(**{key:value}))

    def test_workflow_has_one_opt_in_job_and_excludes_every_other_job(self):
        workflow=(ROOT/v.WORKFLOW).read_text();jobs=workflow.split('jobs:\n',1)[1]
        import re
        sections=re.split(r'(?m)^  ([a-z-]+):\n',jobs)
        names=[]
        for i in range(1,len(sections),2):
            name,body=sections[i:i+2];names.append(name)
            if name=='highcontrast-verification':
                for required in ('inputs.highcontrast_verification_only == true','cancel-in-progress: false','persist-credentials: false','github.run_attempt == 1','if: always()'):
                    self.assertIn(required,body)
                for banned in ('setup-dotnet','dotnet build','Measure-NativeUx','strategy:','execute_locked'):
                    self.assertNotIn(banned,body)
            else:self.assertIn('inputs.highcontrast_verification_only != true',body)
        self.assertEqual(len(names),7)


if __name__=='__main__':unittest.main()
