"""Offline regression controls for the retained failed Job fixture observation."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/native/qualification_hosted'))
import live_process as supervisor
from job_evidence import PHASES, verify_supervision
from job_supervisor_support import Clock, FakeKernel, Timer, FIXTURE_BYTES, raw_job
import test_native_job_supervisor as baseline


class RecoveryControls(unittest.TestCase):
    rejected = baseline.JobSupervisorControls.rejected

    def run_fake(self, kernel=None):
        with patch.object(supervisor.c, 'WinDLL', create=True, side_effect=AssertionError('Native loading forbidden')), \
             patch.object(supervisor.subprocess, 'Popen', side_effect=AssertionError('Process execution forbidden')):
            return baseline.JobSupervisorControls.run_fake(self, kernel)

    def test_native_exit_identity_failure_retains_error_without_null_validation(self):
        kernel = FakeKernel()
        identity = kernel.identity
        def at_exit(*args):
            if kernel.exited:
                kernel.call('identity')
                raise supervisor.NativeFailure('QueryFullProcessImageNameW', dict(returnValue=0, winError=31))
            return identity(*args)
        kernel.identity = at_exit
        result, kernel = self.run_fake(kernel)
        native = next(o['error'] for o in result['operations'] if o['name'] == 'identity-at-exit' and o['phase'] == 'END')
        self.assertEqual(native['native'], dict(returnValue=0, winError=31))
        self.assertEqual(native['winError'], 31)
        self.assertIsNone(result['childIdentityAtExit'])
        self.assertEqual(result['errors'], ['NativeFailure: QueryFullProcessImageNameW failed'])
        self.assertEqual([o['phase'] for o in result['jobObservations']], list(PHASES))
        self.assertEqual(kernel.calls.count('close'), 3)
        self.assertFalse(result['cleanupVerified'])
        self.rejected(result)

    def test_silent_missing_exit_identity_is_explicit_failure(self):
        kernel = FakeKernel()
        identity = kernel.identity
        kernel.identity = lambda *args: None if kernel.exited else identity(*args)
        result, kernel = self.run_fake(kernel)
        self.assertIn('ValueError: Missing child exit identity', result['errors'])
        self.assertFalse(any('TypeError' in e for e in result['errors']))
        self.assertEqual(kernel.calls.count('close'), 3)
        self.rejected(result)

    def test_full_post_exit_identity_remains_required(self):
        for replacement in (None, {}, dict(processId=133), {'imagePath': 'same-path'}):
            with self.subTest(replacement=replacement):
                result, _ = self.run_fake()
                result['childIdentityAtExit'] = replacement
                self.rejected(result)

    def test_early_clock_wake_waits_for_fixed_target_without_an_extra_job_query(self):
        class EarlyClock(Clock):
            sleeps = 0
            def sleep(self, seconds):
                self.sleeps += 1
                if self.sleeps == 1:
                    self.value = 0.063  # Simulated direct exit, not a real wait.
                else:
                    self.value += 0.094 if self.sleeps == 2 else seconds
        clock = EarlyClock()
        result, kernel = self.run_fake(FakeKernel(clock=clock))
        self.assertEqual(verify_supervision(result), 'PASS_FIXED_JOB_OBSERVATIONS_ONLY')
        self.assertEqual([o['phase'] for o in result['jobObservations']], list(PHASES))
        self.assertEqual(kernel.observations, 10)
        self.assertEqual(kernel.calls.count('create'), 1)
        self.assertEqual(kernel.calls.count('assign'), 1)
        self.assertEqual(clock.sleeps, 5)  # One clock-only remainder wait; no evidence retry.
        entry = result['jobObservations'][7]
        self.assertGreaterEqual(entry['startedSeconds'], result['cleanupStartedSeconds'] + 0.1)
        self.assertGreaterEqual(entry['raw']['accounting']['startedSeconds'], result['cleanupStartedSeconds'] + 0.1)

    def test_stalled_or_tiny_progress_clock_has_a_finite_wait_bound(self):
        for step in (0, 0.000001):
            class SlowClock(Clock):
                sleeps = 0
                def sleep(self, seconds):
                    self.sleeps += 1
                    self.value += seconds if self.sleeps == 1 else step
            clock = SlowClock()
            result, kernel = self.run_fake(FakeKernel(clock=clock))
            self.assertLessEqual(clock.sleeps, 1 + 3 * supervisor.MAX_CLEANUP_WAITS)
            self.assertEqual(kernel.calls.count('close'), 3)
            self.assertTrue(any('cleanup clock' in e for e in result['errors']))
            self.assertNotIn('CLEANUP_1', [o['phase'] for o in result['jobObservations']])
            self.rejected(result)

    def test_stalled_clock_never_requests_eight_full_delays(self):
        requests = []
        waiting = dict(lastStamp=0, requestedSeconds=0)
        with self.assertRaisesRegex(supervisor.CleanupClockFailure, 'stalled'):
            supervisor.wait_cleanup_window(lambda: 0, requests.append, 0, 4, 5, waiting)
        self.assertEqual(requests, [4])

    def test_positive_but_tiny_clock_progress_cannot_extend_requested_sleep_budget(self):
        clock = Clock()
        requests = []
        def sleep(seconds):
            requests.append(seconds)
            clock.value += 0.000001
        waiting = dict(lastStamp=0, requestedSeconds=0)
        with self.assertRaisesRegex(supervisor.CleanupClockFailure, 'wait bound'):
            supervisor.wait_cleanup_window(clock, sleep, 0, 4, 5, waiting)
        self.assertEqual(requests, [4])
        self.assertLessEqual(waiting['requestedSeconds'], 5)

    def test_requested_sleep_budget_is_shared_by_all_original_cleanup_targets(self):
        clock = Clock()
        waiting = dict(lastStamp=0, requestedSeconds=0)
        requests = []
        def sleep(seconds):
            requests.append(seconds)
            clock.value += seconds / 2 if len(requests) % 2 else seconds
        failed_target = None
        for target, upper in ((0, 0.1), (0.1, 1), (1, 4), (4, 5)):
            try:
                supervisor.wait_cleanup_window(clock, sleep, 0, target, upper, waiting)
            except supervisor.CleanupClockFailure:
                failed_target = target
                break
        self.assertEqual(failed_target, 4)
        self.assertEqual(len(requests), 5)
        self.assertAlmostEqual(sum(requests), 4.5)
        self.assertLessEqual(sum(requests), 5)

    def test_regressing_clock_cannot_trigger_a_cleanup_query(self):
        class BadClock(Clock):
            sleeps = 0
            def sleep(self, seconds):
                self.sleeps += 1
                self.value = 0.1 if self.sleeps == 1 else 0.05
        result, kernel = self.run_fake(FakeKernel(clock=BadClock()))
        self.assertTrue(any('clock' in e for e in result['errors']))
        self.assertEqual(kernel.observations, 7)
        self.assertEqual(kernel.calls.count('close'), 3)
        self.rejected(result)

    def test_nonfinite_clock_cannot_query_or_certify_an_incomplete_journal(self):
        for value in (float('nan'), float('inf')):
            class BadClock(Clock):
                sleeps = 0
                def sleep(self, seconds):
                    self.sleeps += 1
                    self.value = 0.1 if self.sleeps == 1 else value
            kernel = FakeKernel(clock=BadClock())
            with tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                binary = root/'fixture.exe'; binary.write_bytes(FIXTURE_BYTES)
                def write(path, record): path.write_text(json.dumps(record), encoding='utf-8')
                with patch.object(supervisor, 'Kernel', side_effect=AssertionError('Native kernel forbidden')):
                    result = supervisor.supervise(binary, {}, root, write, kernel,
                        clock=kernel.clock, timer=Timer, sleep=kernel.clock.sleep)
                # Non-finite times cannot be written to the strict journal. Its
                # retained prefix is evidence of failure, not a complete record.
                lines = (root/'supervisor.jsonl').read_text(encoding='utf-8').splitlines()
                self.assertLess(len(lines), len(result['operations']))
            self.assertFalse(result['journalComplete'])
            self.assertTrue(result['loggingErrors'])
            self.assertEqual(kernel.observations, 7)
            self.assertEqual(kernel.calls.count('close'), 3)
            self.rejected(result)

    def test_missed_upper_window_is_not_sampled_or_replaced(self):
        for delay in (1.0, 5.0):
            class LateClock(Clock):
                sleeps = 0
                def sleep(self, seconds):
                    self.sleeps += 1
                    self.value += delay if self.sleeps == 2 else seconds
            result, kernel = self.run_fake(FakeKernel(clock=LateClock()))
            self.assertNotIn('CLEANUP_1', [o['phase'] for o in result['jobObservations']])
            self.assertLess(kernel.observations, 10)
            self.assertEqual(kernel.calls.count('close'), 3)
            self.assertFalse(result['cleanupVerified'])
            self.rejected(result)

    def test_window_is_rechecked_after_journal_io_before_backend_query(self):
        kernel = FakeKernel()
        original_open = Path.open
        class DelayedJournal:
            delay = False
            def __init__(self, stream): self.stream = stream
            def write(self, value):
                self.delay = '"name": "observe:CLEANUP_1"' in value and '"phase": "BEGIN"' in value
                return self.stream.write(value)
            def flush(self):
                self.stream.flush()
                if self.delay:
                    kernel.clock.sleep(1)
                    self.delay = False
            def close(self): self.stream.close()
        def open_file(path, *args, **kwargs):
            stream = original_open(path, *args, **kwargs)
            mode = args[0] if args else kwargs.get('mode', 'r')
            return DelayedJournal(stream) if path.name == 'supervisor.jsonl' and mode == 'x' else stream
        with patch.object(Path, 'open', open_file):
            result, kernel = self.run_fake(kernel)
        self.assertEqual(kernel.observations, 9)
        entry = next(o for o in result['jobObservations'] if o['phase'] == 'CLEANUP_1')
        self.assertNotIn('raw', entry)
        self.assertTrue(any('scheduling window' in e for e in result['errors']))
        self.assertEqual(kernel.calls.count('close'), 3)
        self.rejected(result)

    def test_raw_query_start_must_also_fit_the_fixed_window(self):
        result, _ = self.run_fake()
        entry = result['jobObservations'][6]
        late = result['cleanupStartedSeconds'] + 0.1
        for raw in entry['raw'].values():
            raw['startedSeconds'] = raw['finishedSeconds'] = late
        entry['finishedSeconds'] = late
        end = next(o for o in result['operations'] if o['name'] == 'observe:CLEANUP_0' and o['phase'] == 'END')
        end['seconds'] = late
        self.rejected(result)

    def test_backend_delay_cannot_hide_behind_an_earlier_entry_timestamp(self):
        kernel = FakeKernel()
        inspect = kernel.inspect
        def delayed(job):
            if kernel.observations == 7:
                kernel.clock.sleep(1)
            return inspect(job)
        kernel.inspect = delayed
        result, kernel = self.run_fake(kernel)
        self.assertFalse(result['cleanupVerified'])
        self.assertTrue(any('scheduling window' in e for e in result['errors']))
        self.assertEqual(kernel.calls.count('close'), 3)
        self.rejected(result)

    def test_unchanged_archived_run_remains_failed_and_bytes_are_preserved(self):
        archive = ROOT / 'docs/native/evidence/job-supervisor-authorised-control/raw-evidence.zip'
        before = archive.read_bytes()
        self.assertEqual(hashlib.sha256(before).hexdigest(), 'c0157c0a66e11baf8b10eb5ecf545456fa4278c30b2f8c5d7bc63d1c08d0f2f9')
        with zipfile.ZipFile(archive) as z:
            raw = z.read('attempt/process.json')
        self.assertEqual(hashlib.sha256(raw).hexdigest(), '35ab4f7eb63a943ee5f09376d7d9d346a04e4b4ef3cbf53a37df93c21a8c789d')
        result = json.loads(raw)
        self.assertEqual(result['jobObservations'][5]['decoded']['processIds'], [57332])
        self.assertEqual(result['totalProcesses'], 2)
        self.assertFalse(result['cleanupVerified'])
        self.rejected(result)
        self.assertEqual(archive.read_bytes(), before)

    def test_foreign_pid_cannot_be_accepted_by_supplementary_identity_claims(self):
        result, _ = self.run_fake()
        entry = result['jobObservations'][5]
        entry['raw'] = raw_job(2, 1, pids=[57332], now=entry['startedSeconds'])
        result['foreignIdentity'] = dict(processId=57332, imageSha256=result['binarySha256'], creationFileTime=20,
                                       inPrivateJob=True, claimedOwner='fixture')
        self.rejected(result)
        source = (ROOT / 'scripts/native/qualification_hosted/live_process.py').read_text(encoding='utf-8')
        self.assertNotIn('OpenProcess', source)


if __name__ == '__main__':
    unittest.main()
