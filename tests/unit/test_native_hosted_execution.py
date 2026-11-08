"""Offline negative controls for the one-stage hosted coordinator; no Windows UI."""
import copy
import base64
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import types
import unittest
from unittest.mock import Mock, patch
import zipfile

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location('hosted_execution', ROOT / 'scripts/native/qualification_hosted/execute_locked.py')
coordinator = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(coordinator)


class HostedExecutionControls(unittest.TestCase):
    def env(self):
        return dict(GITHUB_ACTIONS='true', RUNNER_ENVIRONMENT='github-hosted', RUNNER_OS='Windows',
                    RUNNER_ARCH='X64', GITHUB_REPOSITORY=coordinator.REPOSITORY, GITHUB_EVENT_NAME='workflow_dispatch',
                    GITHUB_REF='refs/heads/' + coordinator.BRANCH, GITHUB_ACTOR='DouglasNLima',
                    GITHUB_TRIGGERING_ACTOR='DouglasNLima', GITHUB_RUN_ATTEMPT='1',
                    EXPECTED_WRAPPER_SHA='a' * 40, GITHUB_SHA='a' * 40)

    def test_only_first_attempt_on_owner_hosted_branch_is_eligible(self):
        coordinator.guard(self.env(), 'R01')
        for key in self.env():
            with self.subTest(key=key), self.assertRaises(ValueError):
                coordinator.guard(dict(self.env(), **{key: 'wrong'}), 'R01')

    def test_r02_and_calibration_are_unavailable_before_independent_review(self):
        for protocol in ('R02', 'calibration', 'r01', '', '../R01'):
            with self.subTest(protocol=protocol), self.assertRaises(ValueError):
                coordinator.guard(self.env(), protocol)

    def test_local_cli_rejects_without_gui_or_api_import(self):
        env = {k: v for k, v in os.environ.items() if not k.startswith(('GITHUB_', 'RUNNER_'))}
        result = subprocess.run([sys.executable, str(ROOT / coordinator.SCRIPT), 'execute', '--protocol', 'R01'],
                                env=env, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Hosted owner/branch/attempt restriction failed', result.stderr)
        self.assertNotIn('windows', sys.modules)

    def authority(self):
        record = json.loads((ROOT / 'docs/native/evidence/bounded-qualification/owner-record.json').read_text(encoding='utf-8-sig'))
        record['user'] = {'login': record['user']}
        return record

    def test_exact_owner_record_and_attachment_are_bound(self):
        coordinator.verify_authority(self.authority())
        attachment = json.loads((ROOT / 'docs/native/evidence/bounded-qualification/owner-attachment.json').read_text())
        self.assertEqual(hashlib.sha256(base64.b64decode(attachment['base64'], validate=True)).hexdigest(), attachment['sha256'])
        self.assertEqual(attachment['sha256'], '0a4d82c72a81ba40a38952105502d71e9d680f86751289dc92cddbedbfe7cd4b')

    def test_changed_authority_identity_body_or_url_is_rejected(self):
        for key, value in [('id', 1), ('user', {'login': 'other'}), ('html_url', 'https://example.com'), ('body', 'revoked')]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                coordinator.verify_authority(dict(self.authority(), **{key: value}))

    def metadata(self):
        return ({'id': coordinator.ARTEFACT, 'expired': False, 'digest': 'sha256:' + coordinator.ARCHIVE_SHA,
                 'workflow_run': {'id': coordinator.DISCOVERY, 'head_sha': coordinator.TOOLING}},
                {'id': coordinator.DISCOVERY, 'run_attempt': 1, 'head_sha': coordinator.TOOLING,
                 'status': 'completed', 'conclusion': 'success', 'repository': {'full_name': coordinator.REPOSITORY}})

    def test_original_successful_discovery_is_required(self):
        artefact, run = self.metadata()
        coordinator.verify_metadata(artefact, run)
        for key, value in [('id', 1), ('expired', True), ('digest', 'sha256:' + '0' * 64), ('workflow_run', {'id': 1, 'head_sha': coordinator.TOOLING})]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                coordinator.verify_metadata(dict(artefact, **{key: value}), run)
        for key, value in [('id', 1), ('run_attempt', 2), ('head_sha', '0' * 40), ('status', 'in_progress'),
                           ('conclusion', 'failure'), ('repository', {'full_name': 'other/repo'})]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                coordinator.verify_metadata(artefact, dict(run, **{key: value}))

    def test_only_three_authority_fields_change(self):
        raw = self.candidate_bytes()
        self.assertEqual(hashlib.sha256(raw).hexdigest(), coordinator.CONFIG_SHA)
        candidate = json.loads(raw)
        original = copy.deepcopy(candidate)
        authorised = coordinator.authorised_copy(candidate)
        self.assertEqual(candidate, original)
        self.assertEqual({k for k in original if original[k] != authorised[k]},
                         {'status', 'campaignExecutionApproved', 'ownerAuthorisationUrl'})
        self.assertFalse(authorised['calibrationPilotEnabled'])
        for key, value in [('status', 'PASS'), ('campaignExecutionApproved', True), ('ownerAuthorisationUrl', 'x'), ('toolingCommit', '0' * 40)]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                coordinator.authorised_copy(dict(candidate, **{key: value}))

    def candidate_bytes(self):
        # Use the immutable Git receipt, independent of local checkout EOL rules.
        return subprocess.check_output(['git', 'show', '06057ecf1a070c5275ff39a8427314287e8a0490:'
            'docs/native/evidence/hosted-environment-lock/candidate-config.json'], cwd=ROOT)

    def test_safe_zip_restores_exact_bytes_and_cannot_overwrite(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); archive = root / 'input.zip'; target = root / 'restored'
            with zipfile.ZipFile(archive, 'w') as zipped:
                zipped.writestr('a/file.txt', b'exact bytes')
            manifest = coordinator.extract_archive(archive, target, expected_count=1)
            self.assertEqual(manifest, {'a/file.txt': hashlib.sha256(b'exact bytes').hexdigest()})
            with self.assertRaises(ValueError): coordinator.extract_archive(archive, target, expected_count=1)

    def test_escaping_ambiguous_duplicate_or_unexpected_zip_is_rejected(self):
        cases = [['../escape'], ['/absolute'], ['C:/escape'], ['a\\b'], ['a//b'], ['a/./b'], ['a', 'A'], ['only-one']]
        for names in cases:
            with self.subTest(names=names), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary); archive = root / 'input.zip'; target = root / 'restored'
                with zipfile.ZipFile(archive, 'w') as zipped:
                    for name in names:
                        # Windows ZipInfo construction normalises backslashes;
                        # set the raw member name afterwards to retain the attack.
                        member = zipfile.ZipInfo('placeholder')
                        member.filename = name
                        zipped.writestr(member, b'x')
                with self.assertRaises(ValueError):
                    coordinator.extract_archive(archive, target, expected_count=2 if names == ['only-one'] else len(names))
                self.assertFalse(target.exists())

    def test_truncated_zip_is_not_repaired_or_retried(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); archive = root / 'input.zip'
            archive.write_bytes(b'PK\x03\x04partial')
            with self.assertRaises(zipfile.BadZipFile): coordinator.extract_archive(archive, root / 'out')

    def records(self):
        return [dict(id=f'V{i}', processId=100 + i, cleanupVerified=True, jobTreeEmpty=True, restored=True,
                     initialAppearance={'flags': 0, 'scheme': ''}, restoredAppearance={'flags': 0, 'scheme': ''},
                     supervisionFailure=None, exitCode=0) for i in range(1, 5)]

    def test_retained_product_failure_is_distinct_from_cleanup_safety(self):
        records = self.records(); records[0]['exitCode'] = 1
        self.assertTrue(coordinator.safe_processes(records, ['V1', 'V2', 'V3', 'V4']))
        self.assertEqual(records[0]['exitCode'], 1)

    def test_unknown_cleanup_missing_duplicate_wrong_pid_and_timeout_stop(self):
        ids = ['V1', 'V2', 'V3', 'V4']
        self.assertTrue(coordinator.safe_processes(self.records(), ids))
        for key, value in [('cleanupVerified', False), ('jobTreeEmpty', None), ('restored', False),
                           ('restoredAppearance', {'flags': 1}), ('supervisionFailure', 'CHILD_TIMEOUT'),
                           ('exitCode', None), ('processId', 102), ('processId', 0), ('id', 'V2')]:
            records = self.records(); records[0][key] = value
            with self.subTest(key=key, value=value): self.assertFalse(coordinator.safe_processes(records, ids))
        self.assertFalse(coordinator.safe_processes(self.records()[:-1], ids))

    def test_duplicate_and_nonfinite_json_fail(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'record.json'
            for value in ('{"a":1,"a":2}', '{"a":NaN}', '{"a":'):
                path.write_text(value)
                with self.assertRaises(ValueError): coordinator.read_json(path)

    def test_bounded_dispatch_excludes_every_original_job_and_discovery(self):
        workflow = (ROOT / '.github/workflows/native-ux-performance.yml').read_text()
        for name, following in [('observer-smoke', 'contract-controls'), ('contract-controls', 'calibration'),
                                ('calibration', 'environment-lock'), ('environment-lock', 'bounded-qualification')]:
            section = workflow.split('  ' + name + ':\n')[1].split('  ' + following + ':\n')[0]
            self.assertIn("inputs.bounded_protocol == '' || inputs.bounded_protocol == 'none'", section)
        bounded = workflow.split('  bounded-qualification:\n')[1].split('  highcontrast-verification:\n')[0]
        for required in ('persist-credentials: false', 'github.run_attempt == 1', 'github.triggering_actor',
                         'actions: read', 'contents: read', 'timeout-minutes: 30', 'if: always()',
                         'git checkout --detach ' + coordinator.TOOLING, 'exit $LASTEXITCODE'):
            self.assertIn(required, bounded)
        self.assertNotIn('secrets.', bounded)
        self.assertNotIn('Measure-NativeUx', bounded)
        self.assertNotIn('GH_TOKEN', bounded.split('- name: Revalidate every identity')[1])
        self.assertEqual(bounded.count(' execute --protocol '), 1)

    def test_environment_mismatch_precedes_windows_api_and_child_access(self):
        if os.name != 'nt': self.skipTest('Wrapper execute has an additional Windows-only guard')
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); output = root / 'evidence'; output.mkdir()
            restored = {'wrapperCommit': 'a' * 40, 'wrapperSha256': coordinator.sha(coordinator.__file__),
                        'archiveSha256': coordinator.ARCHIVE_SHA, 'fileCount': 2019}
            coordinator.write_json(output / 'restore.json', restored)
            coordinator.write_json(output / 'owner-authorisation.json', self.authority())
            original = root / 'artifacts/qualification-environment/candidate-config.json'
            original.parent.mkdir(parents=True)
            original.write_bytes(self.candidate_bytes())
            validate = Mock(side_effect=ValueError('Hosted image/full Windows identity changed'))
            windows = Mock()
            modules = {'qualification': types.SimpleNamespace(strict_json=coordinator.read_json, validate_candidate_config=validate),
                       'environment_lock': types.SimpleNamespace(identity=lambda: {'imageVersion': 'different'}),
                       'audit': types.SimpleNamespace(), 'windows': types.SimpleNamespace(Windows=windows)}
            original_path = sys.path[:]
            try:
                with patch.dict(sys.modules, modules), patch.dict(os.environ, {'GITHUB_SHA': 'a' * 40}, clear=True), \
                        patch.object(coordinator, 'git', side_effect=[coordinator.TOOLING, '']), \
                        patch.object(coordinator, 'verify_restored'), patch.object(coordinator, 'recovery_gate'):
                    with self.assertRaisesRegex(ValueError, 'identity changed'): coordinator.execute(root, output, 'R01')
                windows.assert_not_called()
                self.assertFalse((output / 'execution-config.json').exists())
                self.assertFalse((output / 'collection').exists())
            finally:
                sys.path[:] = original_path

    def test_audit_failure_or_invalid_collection_never_yields_zero_exit(self):
        terminal = dict(originalLauncherExit=0, restorationVerified=True, environmentUnchanged=True,
                        initialAudit={'collection': 'VALID', 'existingCorrectness': 'PASS'})
        self.assertEqual(coordinator.terminal_exit(terminal), 0)
        terminal['initialAudit']['existingCorrectness'] = 'INCONCLUSIVE'
        self.assertEqual(coordinator.terminal_exit(terminal), 0)
        terminal['initialAudit']['existingCorrectness'] = 'FAIL'
        self.assertEqual(coordinator.terminal_exit(terminal), 1)
        terminal['initialAudit'] = {'collection': 'INVALID', 'existingCorrectness': 'PASS'}
        self.assertEqual(coordinator.terminal_exit(terminal), 1)
        terminal['initialAudit'] = {'collection': 'VALID', 'existingCorrectness': 'PASS'}
        for key, value in [('originalLauncherExit', 1), ('restorationVerified', None), ('environmentUnchanged', False),
                           ('auditError', 'missing'), ('error', 'exception'), ('initialAudit', {})]:
            with self.subTest(key=key): self.assertEqual(coordinator.terminal_exit(dict(terminal, **{key: value})), 1)

    def test_complete_restored_closure_detects_changed_missing_extra_files_and_manifest(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); output = root / 'evidence'; output.mkdir()
            restored = root / 'artifacts/qualification-environment'; restored.mkdir(parents=True)
            files = {}
            for i in range(2019):
                path = restored / str(i); path.write_bytes(b'fixture'); files[path.name] = coordinator.sha(path)
            coordinator.write_json(output / 'restored-files.json', files)
            receipt = {'filesManifestSha256': coordinator.sha(output / 'restored-files.json')}
            coordinator.verify_restored(root, output, receipt)
            (restored / '0').write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError, 'changed files'): coordinator.verify_restored(root, output, receipt)
            (restored / '0').unlink()
            with self.assertRaisesRegex(ValueError, 'missing'): coordinator.verify_restored(root, output, receipt)
            (restored / '0').write_bytes(b'fixture'); (restored / 'extra').write_bytes(b'fixture')
            with self.assertRaisesRegex(ValueError, 'extra'): coordinator.verify_restored(root, output, receipt)
            with self.assertRaisesRegex(ValueError, 'manifest identity'):
                coordinator.verify_restored(root, output, {'filesManifestSha256': '0' * 64})


if __name__ == '__main__':
    unittest.main()
