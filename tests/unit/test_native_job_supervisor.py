"""Fixed-observation supervisor controls: fake clock/kernel only, no native run."""
import copy
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'scripts/native/qualification_hosted'))
import live_process as supervisor
import local_job_control as local
from live_verification import verify_authority
from job_evidence import CLEANUP_TARGETS, PHASES, decode_snapshot, verify_supervision
from job_supervisor_support import Clock, Timer, FakeKernel, FIXTURE_BYTES, raw_job


def replace_raw(value, total=1, active=0, pids=None, **kwargs):
    stamp=value['accounting']['startedSeconds']
    value.clear();value.update(raw_job(total,active,pids=pids,now=stamp,**kwargs))


class JobSupervisorControls(unittest.TestCase):
    def run_fake(self, kernel=None, broken_journal=False):
        kernel=kernel or FakeKernel()
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);binary=root/'fixture.exe';binary.write_bytes(FIXTURE_BYTES)
            def write(path,value):path.write_text(json.dumps(value),encoding='utf-8')
            original_open=Path.open
            class JournalFailure:
                def __init__(self,stream):self.stream=stream
                def write(self,value):
                    if '"name": "create"' in value and '"phase": "END"' in value:
                        raise OSError('Injected journal storage failure')
                    return self.stream.write(value)
                def flush(self):return self.stream.flush()
                def close(self):return self.stream.close()
            def open_file(path,*args,**kwargs):
                stream=original_open(path,*args,**kwargs)
                mode=args[0] if args else kwargs.get('mode','r')
                return JournalFailure(stream) if broken_journal and path.name=='supervisor.jsonl' and mode=='x' else stream
            with patch.object(supervisor,'Kernel',side_effect=AssertionError('Native kernel forbidden')),patch.object(Path,'open',open_file):
                result=supervisor.supervise(binary,{},root,write,kernel,clock=kernel.clock,timer=Timer,sleep=kernel.clock.sleep)
            journal=[json.loads(line) for line in (root/'supervisor.jsonl').read_text(encoding='utf-8').splitlines()]
            if not broken_journal:self.assertEqual(journal,json.loads(json.dumps(result['operations'])))
            return result,kernel

    def rejected(self, result):
        with self.assertRaises((ValueError,KeyError,TypeError)):verify_supervision(result)

    def test_empty_creation_single_suspended_assignment_and_all_fixed_samples(self):
        result,kernel=self.run_fake()
        self.assertEqual(verify_supervision(result),'PASS_FIXED_JOB_OBSERVATIONS_ONLY')
        self.assertEqual([e['phase'] for e in result['jobObservations']],list(PHASES))
        self.assertEqual(kernel.calls.count('create'),1);self.assertEqual(kernel.calls.count('assign'),1)
        self.assertLess(kernel.calls.index('assign'),kernel.calls.index('resume'))
        self.assertEqual([e['targetSeconds'] for e in result['jobObservations'][-4:]],list(CLEANUP_TARGETS))
        self.assertAlmostEqual(result['finishedSeconds']-result['cleanupStartedSeconds'],4)
        self.assertTrue(result['emptyMembershipObserved']);self.assertTrue(result['jobHandleClosed'])

    def test_nonempty_new_job_stops_before_child_creation(self):
        kernel=FakeKernel(mutations={0:lambda x:replace_raw(x,active=1,flags=0,limit=0)})
        result,kernel=self.run_fake(kernel)
        self.assertNotIn('create',kernel.calls);self.rejected(result)

    def test_wrong_limits_stop_before_creation_or_resume(self):
        for index in (1,3):
            kernel=FakeKernel(mutations={index:lambda x:replace_raw(x,total=0 if index==1 else 1,active=0 if index==1 else 1,limit=2)})
            result,kernel=self.run_fake(kernel)
            self.assertNotIn('resume',kernel.calls);self.rejected(result)

    def test_failed_assignment_retains_error_and_post_attempt_snapshot(self):
        result,kernel=self.run_fake(FakeKernel('assign'))
        self.assertEqual(result['assignmentResult'],dict(returnValue=0,winError=5))
        self.assertIn('AFTER_ASSIGNMENT',[e['phase'] for e in result['jobObservations']])
        self.assertNotIn('resume',kernel.calls);self.assertIn('stop-direct',kernel.calls)
        self.rejected(result)

    def test_repeated_assignment_is_rejected_before_native_assign(self):
        kernel=FakeKernel();kernel.association=lambda *args:True
        result,kernel=self.run_fake(kernel)
        self.assertNotIn('assign',kernel.calls);self.assertNotIn('resume',kernel.calls);self.rejected(result)

    def test_wrong_pid_at_suspended_assignment_never_resumes(self):
        result,kernel=self.run_fake(FakeKernel(mutations={3:lambda x:replace_raw(x,active=1,pids=[999])}))
        self.assertNotIn('resume',kernel.calls);self.rejected(result)

    def test_extra_member_or_total_two_is_sticky_even_after_empty(self):
        for mutation in (lambda x:replace_raw(x,2,1),lambda x:replace_raw(x,1,1,[133,999]),
                         lambda x:replace_raw(x,1,0,terminated=1)):
            result,_=self.run_fake(FakeKernel(mutations={5:mutation}))
            self.assertEqual(result['jobObservations'][-1]['decoded']['processIds'],[])
            self.assertFalse(result['cleanupVerified']);self.rejected(result)

    def test_known_child_non_atomic_accounting_transition_keeps_every_sample(self):
        result,_=self.run_fake(FakeKernel(mutations={
            5:lambda x:replace_raw(x,active=1,pids=[]),6:lambda x:replace_raw(x,active=1,pids=[])}))
        self.assertEqual(verify_supervision(result),'PASS_FIXED_JOB_OBSERVATIONS_ONLY')
        self.assertEqual(len(result['jobObservations']),10)

    def test_child_exit_with_final_active_job_is_not_cleanup(self):
        result,_=self.run_fake(FakeKernel(counts=(1,1)))
        self.assertTrue(result['childExitObserved']);self.assertEqual(result['exitCode'],0)
        self.assertFalse(result['cleanupVerified']);self.rejected(result)

    def test_membership_reappearance_is_sticky(self):
        result,_=self.run_fake(FakeKernel(mutations={7:lambda x:replace_raw(x,active=1)}))
        self.assertTrue(result['jobHandleClosed']);self.rejected(result)

    def test_independent_counter_or_pid_stream_reappearance_is_rejected(self):
        for first,second in ((dict(active=1,pids=[]),dict(active=1,pids=[133])),
                             (dict(active=0,pids=[133]),dict(active=1,pids=[133]))):
            result,_=self.run_fake(FakeKernel(mutations={5:lambda x:replace_raw(x,**first),6:lambda x:replace_raw(x,**second)}))
            self.rejected(result)

    def test_journal_failure_after_creation_cannot_suppress_cleanup(self):
        result,kernel=self.run_fake(broken_journal=True)
        self.assertIn('stop-direct',kernel.calls);self.assertIn('wait',kernel.calls)
        self.assertEqual(kernel.calls.count('close'),3);self.assertTrue(result['childExitObserved'])
        self.assertFalse(result['journalComplete']);self.rejected(result)

    def test_failed_close_does_not_skip_other_handles_or_retry(self):
        result,kernel=self.run_fake(FakeKernel('close'))
        self.assertEqual(kernel.calls.count('close'),3);self.assertFalse(result['jobHandleClosed']);self.rejected(result)

    def test_pid_capacity_truncation_and_native_failure_never_retry(self):
        changes=[lambda x:x['pids'].update(returnValue=0,winError=234),
                 lambda x:x['pids'].update(returnedBytes=7),
                 lambda x:x['pids'].update(returnedBytes=8),
                 lambda x:x['pids'].update(capacityBytes=144),
                 lambda x:x['pids'].update(returnedBytes=73),
                 lambda x:x['pids'].update(rawBytes='00'*71)]
        for mutation in changes:
            with self.subTest(mutation=mutation):
                result,kernel=self.run_fake(FakeKernel(mutations={3:mutation}))
                self.assertNotIn('resume',kernel.calls);self.assertEqual(kernel.calls.count('assign'),1);self.rejected(result)

    def test_returned_pid_prefix_may_be_shorter_than_allocated_buffer(self):
        for count in (0,1):
            value=raw_job(count,count)
            self.assertEqual(value['pids']['returnedBytes'],8+count*8)
            self.assertEqual(decode_snapshot(value)['activeProcesses'],count)

    def test_wrong_fixed_structure_return_lengths_are_rejected(self):
        for part,size in (('accounting',47),('limits',143)):
            value=raw_job(1,1);value[part]['returnedBytes']=size
            with self.assertRaises(ValueError):decode_snapshot(value)

    def test_failed_termination_cannot_skip_wait_exit_or_job_observations(self):
        kernel=FakeKernel('stop-job')
        original=kernel.resume
        def fail_resume(*args):original(*args);raise RuntimeError('Injected post-resume failure')
        kernel.resume=fail_resume
        result,kernel=self.run_fake(kernel)
        self.assertIn('wait',kernel.calls);self.assertIn('exit',kernel.calls)
        self.assertEqual([e['phase'] for e in result['jobObservations'][-4:]],list(PHASES[-4:]))
        error=next(o['error'] for o in result['operations'] if o['name']=='terminate' and o['phase']=='END')
        self.assertEqual(error['winError'],5);self.rejected(result)

    def test_failed_exit_query_does_not_skip_identity_or_job_observation(self):
        result,kernel=self.run_fake(FakeKernel('exit'))
        self.assertEqual(kernel.calls.count('identity'),2)
        self.assertIn('PROCESS_EXIT',[e['phase'] for e in result['jobObservations']]);self.rejected(result)

    def test_native_wait_error_is_not_relabelled_timeout(self):
        result,kernel=self.run_fake(FakeKernel('wait'))
        self.assertFalse(result['timedOut'])
        error=next(o['error'] for o in result['operations'] if o['name']=='wait' and o['phase']=='END')
        self.assertEqual(error['winError'],5);self.rejected(result)

    def test_timeout_and_late_cleanup_cannot_pass(self):
        result,_=self.run_fake(FakeKernel('timeout'))
        self.assertTrue(result['timedOut']);self.rejected(result)
        kernel=FakeKernel()
        def slow(value):kernel.clock.sleep(6)
        kernel.mutations[6]=slow
        result,kernel=self.run_fake(kernel)
        self.assertFalse(result['cleanupVerified']);self.rejected(result)

    def test_wrong_handle_pid_thread_image_or_creation_identity_rejects(self):
        changes=[('processId',999),('threadId',999),('threadProcessId',999),('imageSha256','b'*64),
                 ('imagePath','C:\\other.exe'),('creationFileTime',0),('processHandleFlags',1)]
        for key,value in changes:
            kernel=FakeKernel();original=kernel.identity
            kernel.identity=lambda *args,k=key,v=value,f=original:dict(f(*args),**{k:v})
            result,kernel=self.run_fake(kernel)
            self.assertNotIn('assign',kernel.calls);self.assertNotIn('resume',kernel.calls)
            self.assertIn('stop-direct',kernel.calls);self.assertTrue(result['childExitObserved']);self.rejected(result)

    def test_aliased_native_handles_never_assign_or_resume(self):
        kernel=FakeKernel();original=kernel.create
        def aliased(*args):original(*args);return 11,11,133,134
        kernel.create=aliased
        result,kernel=self.run_fake(kernel)
        self.assertNotIn('assign',kernel.calls);self.assertNotIn('resume',kernel.calls);self.rejected(result)

    def test_missing_duplicate_reordered_and_contradictory_evidence_fails(self):
        original,_=self.run_fake()
        changes=[lambda r:r['jobObservations'].pop(),lambda r:r['jobObservations'].append(copy.deepcopy(r['jobObservations'][-1])),
                 lambda r:r['jobObservations'][3]['decoded'].update(totalProcesses=2),
                 lambda r:r['jobObservations'][-1].update(targetSeconds=3),
                 lambda r:r['operations'].pop(),lambda r:r.update(jobHandleClosed=False),
                 lambda r:r.update(childIdentityAtExit={}),lambda r:r.update(assignmentAttempts=2)]
        for change in changes:
            value=copy.deepcopy(original);change(value);self.rejected(value)

    def test_operation_results_and_handles_cannot_contradict_passing_summary(self):
        original,_=self.run_fake()
        changes=[('assign',dict(returnValue=0,winError=5)),('create',[111,12,133,134]),
                 ('association-before',True),('association-after',False),('wait',dict(waitResult=258,requestedMilliseconds=30000)),
                 ('close:job',dict(returnValue=0,winError=5)),('exit',1),('identity-before',{})]
        for name,value in changes:
            bad=copy.deepcopy(original)
            next(o for o in bad['operations'] if o['name']==name and o['phase']=='END')['result']=value
            self.rejected(bad)
        bad=copy.deepcopy(original)
        operation=next(o for o in bad['operations'] if o['name']=='observe:CREATED_EMPTY' and o['phase']=='END')
        operation['result']=copy.deepcopy(operation['result']);operation['result']['accounting']['returnValue']=0
        self.rejected(bad)
        bad=copy.deepcopy(original);bad['operations']=[o for o in bad['operations'] if o['name'] in ('create','assign')]
        self.rejected(bad)

    def test_bunched_or_boolean_evidence_is_not_accepted(self):
        original,_=self.run_fake()
        for field in ('processId','assignmentAttempts','exitCode','totalProcesses','activeProcesses'):
            bad=copy.deepcopy(original);bad[field]=bool(bad[field]);self.rejected(bad)
        bad=copy.deepcopy(original)
        for entry in bad['jobObservations'][-4:]:entry['startedSeconds']=bad['cleanupStartedSeconds']+4
        self.rejected(bad)

    def test_pure_abi_offsets_and_wrong_platform_reject_before_loading_kernel(self):
        if supervisor.os.name=='nt':
            self.assertEqual(supervisor.c.sizeof(supervisor.Basic),64)
            self.assertEqual(supervisor.c.sizeof(supervisor.Limits),144)
            self.assertEqual(supervisor.c.sizeof(supervisor.Accounting),48)
            self.assertEqual([getattr(supervisor.Accounting,k).offset for k in ('faults','total','active','terminated')],[32,36,40,44])
        with patch.object(supervisor.os,'name','unsupported'),patch.object(supervisor.c,'WinDLL',create=True) as loader:
            with self.assertRaises(ValueError):supervisor.Kernel()
            loader.assert_not_called()

    def test_setup_deadline_stops_before_any_child(self):
        kernel=FakeKernel();old=kernel.parent
        def slow_parent(*args):kernel.clock.sleep(31);return old(*args)
        kernel.parent=slow_parent
        result,kernel=self.run_fake(kernel)
        self.assertNotIn('create',kernel.calls);self.assertTrue(result['timedOut']);self.rejected(result)

    def test_one_local_control_proposal_cannot_authorise_execution(self):
        plan=dict(status='NOT_AUTHORISED',approved=False,campaignExecutionApproved=False,bounds=copy.deepcopy(local.BOUNDS),
                  cleanupTargets=list(CLEANUP_TARGETS),phases=list(PHASES),expectedFixtureReport=copy.deepcopy(local.EXPECTED_REPORT),
                  sourceCommit='a'*40,sourceTree='b'*40,sourceFiles={p:'c'*64 for p in local.SOURCES},
                  executionFiles={p:'d'*64 for p in local.SOURCES},
                  pythonExecutable=sys.executable,pythonFiles={sys.executable:'c'*64,str(Path(sys.executable).parent/'python312.dll'):'d'*64},
                  fixture=dict(sha256='311655cd165d4a82631efce4b827decea6a5199d2e723da67175b94a4539e549',
                   archive='docs/native/evidence/buffer-safety-recovery/offline-original-bytes.zip',
                   archiveSha256='d28cc4f433b02f93bdfac1604b270123a16419da0229387104ccc990f5a29c27',
                   member='attempt-6/NativeBufferSelfTest.exe',bytes=20480))
        local.validate_plan(plan)
        proposal=local.owner_proposal(plan,'d'*64,'e'*40,'f'*40)
        self.assertIs(proposal['approved'],False);self.assertEqual(proposal['decision'],'PROPOSED_NOT_AUTHORISED')
        for key in local.BOUNDS:
            bad=copy.deepcopy(plan);bad['bounds'][key]+=1
            with self.assertRaises(ValueError):local.validate_plan(bad)
        for key in ('approved','campaignExecutionApproved'):
            bad=copy.deepcopy(plan);bad[key]=True
            with self.assertRaises(ValueError):local.validate_plan(bad)
        expected=local.owner_proposal(plan,'d'*64,'e'*40,'f'*40,approved=True)
        import hashlib
        body=json.dumps(proposal)
        authority=dict(id=11,user=dict(login='DouglasNLima',id=33501911),
           html_url='https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-11',
           issue_url='https://api.github.com/repos/DouglasNLima/local-docs-studio/issues/77',
           created_at='2026-11-08T00:00:00Z',updated_at='2026-11-08T00:00:00Z',body=body)
        with self.assertRaises(ValueError):verify_authority(authority,expected,
            dict(AUTHORISATION_COMMENT_ID='11',AUTHORISATION_BODY_SHA256=hashlib.sha256(body.encode()).hexdigest()))

    def test_local_driver_rejects_nonisolated_interpreter_before_network_or_native(self):
        from types import SimpleNamespace
        with patch.object(local.sys,'flags',SimpleNamespace(isolated=0,no_site=0)),patch.object(local,'fetch_owner') as network,\
             patch.object(local.subprocess,'check_output') as command,patch.object(supervisor,'Kernel') as native:
            with self.assertRaisesRegex(ValueError,'isolated Python'):local.execute('11','a'*64)
            network.assert_not_called();command.assert_not_called();native.assert_not_called()


if __name__=='__main__':unittest.main()
