"""Negative controls for approval deadlocks, bypass and inapplicable CI."""
import copy
import ast
import json
import unittest
import subprocess
import tempfile
import zipfile
from pathlib import Path
from unittest.mock import patch

from check_governance import CONFIG, EVIDENCE, validate_ruleset, validate_admin_packet
from rights_ledger import boundary, current_paths, current_digests
from check_foundation import documentation_links
from licence_scope import (ROOT, SCOPE, validate_licence, validate_scope,
                           validate_metadata, validate_preserved, apply_find_caret_refresh, apply_selection_sync_refresh)
from licence_scope import (FLOWCHART_EVIDENCE, FLOWCHART_PAIRS, FLOWCHART_SOURCE_TREES,
                           flowchart_json, apply_flowchart_styling_refresh,
                           check_flowchart_source_identity, validate_flowchart_source_identity,
                           validate_flowchart_evidence_digests)


class GovernanceBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.payload = json.loads((CONFIG / 'develop-ruleset.json').read_text())
        evidence = json.loads((EVIDENCE / 'check-contexts.json').read_text())
        self.observed = {(r['name'], r['integrationId']) for r in evidence['contexts']}

    def test_valid_solo_proposal(self):
        validate_ruleset(self.payload, 'develop', self.observed)

    def test_admin_packet_requires_exact_target_payload_and_rollback(self):
        original = json.loads((CONFIG / 'admin-activation-plan.json').read_text())
        validate_admin_packet(original)
        for field, value in [('target', '/repos/another/project'),
                             ('bodySha256', '0' * 64)]:
            with self.subTest(field=field):
                changed = copy.deepcopy(original)
                changed['actions'][0][field] = value
                with self.assertRaises(ValueError):
                    validate_admin_packet(changed)
        changed = copy.deepcopy(original)
        changed['actions'][2]['rollback']['target'] = '/repos/another/project'
        with self.assertRaises(ValueError):
            validate_admin_packet(changed)

    def test_documentation_examples_are_not_live_links(self):
        markdown = '[Guide](guide.md)\n```md\n![Example](absent.png)\n```\n`[sample](absent.md)`'
        self.assertEqual(documentation_links(markdown), ['guide.md'])
        self.assertEqual(documentation_links('   [One](one.md)\n   [Two](two.md)'),
                         ['one.md', 'two.md'])

    def test_rejects_human_approval_deadlocks(self):
        for key, value in [('required_approving_review_count', 1),
                           ('require_code_owner_review', True),
                           ('require_last_push_approval', True)]:
            with self.subTest(setting=key):
                payload = copy.deepcopy(self.payload)
                payload['rules'][2]['parameters'][key] = value
                with self.assertRaises(ValueError):
                    validate_ruleset(payload, 'develop', self.observed)

    def test_rejects_administrator_bypass(self):
        self.payload['bypass_actors'] = [
            {'actor_type': 'RepositoryRole', 'actor_id': 5, 'bypass_mode': 'always'}]
        with self.assertRaises(ValueError):
            validate_ruleset(self.payload, 'develop', self.observed)

    def test_rejects_native_check_on_main(self):
        self.payload['conditions']['ref_name']['include'] = ['refs/heads/main']
        with self.assertRaises(ValueError):
            validate_ruleset(self.payload, 'main', self.observed)

    def test_rejects_unobserved_or_wrong_app_check(self):
        with self.assertRaises(ValueError):
            validate_ruleset(self.payload, 'develop', set())
        self.payload['rules'][3]['parameters']['required_status_checks'][0]['integration_id'] = 1
        with self.assertRaises(ValueError):
            validate_ruleset(self.payload, 'develop', self.observed)

    def test_retains_upstream_visual_fixture_and_evidence_exclusions(self):
        for path, category in [
            ('src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs', 'R6'),
            ('src/native/LensDocsStudio.Core/Rendering/PinnedMermaider/CharWidths.cs', 'R6'),
            ('assets/vendor/mermaid.js', 'R6'),
            ('icon.svg', 'R4'),
            ('src/native/LensDocsStudio.Native/Resources/CommandIcons.xaml', 'R4'),
            ('tests/fixtures/sample.md', 'R3'),
            ('src/native/LensDocsStudio.Core/Exports/DocsSiteTemplates/site.js', 'R3'),
            ('scripts/native/Generate-ComplexErFixtures.mjs', 'R3'),
            ('src/native/LensDocsStudio.Core/Content/built-in-catalogue.json', 'R3'),
            ('.github/ISSUE_TEMPLATE/lens-docs-studio-prerelease-feedback.md', 'R3'),
            ('docs/native/native-853-original-evidence.json', 'R5'),
            ('docs/native/evidence/receipt.json', 'R5'),
            ('docs/open-source/evidence/upstream-notices/grant.txt', 'R5'),
        ]:
            with self.subTest(path=path):
                self.assertEqual(boundary(path)[0], category)


class ScopedLicenceTests(unittest.TestCase):
    def setUp(self):
        self.scope = json.loads(SCOPE.read_text(encoding='utf-8'))
        self.paths = set(self.scope['paths'])

    def test_approved_notice_and_unmodified_terms(self):
        validate_licence((ROOT / 'LICENSE').read_text(encoding='utf-8'))

    def test_rejects_notice_or_terms_changes(self):
        licence = (ROOT / 'LICENSE').read_text(encoding='utf-8')
        for old, new in [('2026 DouglasNLima', '2025 Contributors'),
                         ('without restriction', 'for non-commercial use only')]:
            with self.subTest(change=old), self.assertRaises(ValueError):
                validate_licence(licence.replace(old, new))

    def test_rejects_missing_operative_licence(self):
        with self.assertRaises(ValueError):
            validate_licence('')

    def test_rejects_blanket_grant(self):
        self.scope['scope'] = 'Entire repository and all binaries'
        with self.assertRaises(ValueError):
            validate_scope(self.scope, self.paths)

    def test_rejects_removed_content_exclusions(self):
        self.scope['excludedBoundaries'].remove('R7')
        with self.assertRaises(ValueError):
            validate_scope(self.scope, self.paths)

    def test_rejects_vendor_adaptation_artwork_fixture_and_evidence_grants(self):
        for path in ['assets/vendor/new.js',
                     'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs',
                     'src/native/LensDocsStudio.Native/Resources/CommandIcons.xaml',
                     'tests/fixtures/new.md', 'docs/open-source/evidence/new.json']:
            with self.subTest(path=path):
                scope = copy.deepcopy(self.scope)
                scope['paths'] = sorted(self.paths | {path})
                with self.assertRaises(ValueError):
                    validate_scope(scope, self.paths | {path})

    def test_new_path_never_gets_an_automatic_grant(self):
        self.assertEqual(boundary('assets/scripts/new-unreviewed-source.js', self.paths)[1],
                         'BLOCKED')

    def test_rejects_blanket_package_metadata(self):
        package = json.loads((ROOT / 'package.json').read_text(encoding='utf-8'))
        lock = json.loads((ROOT / 'package-lock.json').read_text(encoding='utf-8'))
        validate_metadata(package, lock)
        package['license'] = lock['packages']['']['license'] = 'MIT'
        with self.assertRaises(ValueError):
            validate_metadata(package, lock)

    def test_rejects_missing_or_modified_third_party_attribution(self):
        expected = {'upstream-notice': 'original-digest'}
        for actual in [{}, {'upstream-notice': 'changed-digest'}]:
            with self.subTest(actual=actual), self.assertRaises(ValueError):
                validate_preserved(expected, actual)

    def test_find_amendment_preserves_historical_hashes_and_rejects_unreviewed_paths(self):
        preserved = json.loads((EVIDENCE / 'preserved-files.json').read_text())['sha256']
        refresh = json.loads((ROOT / 'docs/open-source/evidence/find-caret/preserved-refresh.json').read_text())
        updated = apply_find_caret_refresh(preserved, refresh)
        for path in set(preserved) - set(refresh['files']):
            self.assertEqual(updated[path], preserved[path])
        for mutation in ('baseline', 'missing', 'notice', 'digest'):
            changed = copy.deepcopy(refresh)
            path = 'assets/scripts/editor/find-replace-service.js'
            if mutation == 'baseline':
                changed['files'][path]['previous'] = '0' * 64
            elif mutation == 'missing':
                del changed['files'][path]
            elif mutation == 'notice':
                changed['files']['LICENSE'] = {'previous': '0' * 64, 'current': '1' * 64}
            else:
                changed['files'][path]['current'] = 'not-a-digest'
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                apply_find_caret_refresh(preserved, changed)
        actual = dict(updated)
        actual['assets/scripts/editor/find-replace-service.js'] = '0' * 64
        with self.assertRaises(ValueError):
            validate_preserved(updated, actual)

    def test_selection_amendment_preserves_unrelated_trust_and_performance_pins(self):
        preserved = json.loads((EVIDENCE / 'preserved-files.json').read_text())['sha256']
        find = json.loads((ROOT / 'docs/open-source/evidence/find-caret/preserved-refresh.json').read_text())
        preserved = apply_find_caret_refresh(preserved, find)
        refresh = json.loads((ROOT / 'docs/open-source/evidence/selection-sync/preserved-refresh.json').read_text())
        updated = apply_selection_sync_refresh(preserved, refresh)
        for path in set(preserved) - set(refresh['files']):
            self.assertEqual(updated[path], preserved[path])
        for mutation in ('baseline', 'previous', 'missing', 'notice', 'digest'):
            changed = copy.deepcopy(refresh)
            path = 'src/native/LensDocsStudio.Native/MainWindow.Synchronisation.cs'
            if mutation == 'baseline':
                changed['baseCommit'] = '0' * 40
            elif mutation == 'previous':
                changed['files'][path]['previous'] = '0' * 64
            elif mutation == 'missing':
                del changed['files'][path]
            elif mutation == 'notice':
                changed['files']['LICENSE'] = changed['files'][path]
            else:
                changed['files'][path]['current'] = 'not-a-digest'
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                apply_selection_sync_refresh(preserved, changed)

    def test_rejects_continuous_check_in_ordinary_integration(self):
        payload = json.loads((CONFIG / 'develop-ruleset.json').read_text())
        observed = {(r['name'], r['integrationId']) for r in
                    json.loads((EVIDENCE / 'check-contexts.json').read_text())['contexts']}
        payload['rules'][3]['parameters']['required_status_checks'].append(
            {'context': 'Continuous 250 ms', 'integration_id': 15368})
        observed.add(('Continuous 250 ms', 15368))
        with self.assertRaises(ValueError):
            validate_ruleset(payload, 'develop', observed)

    def test_native_payload_contains_exact_licence_and_scoped_disclosure(self):
        with tempfile.TemporaryDirectory(prefix='lens-licence-') as directory:
            revision = 'a' * 40
            result = subprocess.run([
                'pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/native/Copy-NativeLicence.ps1'),
                '-RepositoryRoot', str(ROOT), '-PayloadDirectory', directory,
                '-SourceRevision', revision], capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(result.returncode, 0, result.stderr)
            output = Path(directory)
            self.assertEqual((output / 'LICENSE').read_bytes(), (ROOT / 'LICENSE').read_bytes())
            self.assertEqual(json.loads((output / 'FIRST-PARTY-SCOPE.json').read_text()),
                             self.scope)
            disclosure = (output / 'LICENSING.md').read_text(encoding='utf-8')
            self.assertIn('/blob/' + revision + '/LICENSING.md', disclosure)
            self.assertNotIn('SOURCE_REVISION', disclosure)
            self.assertIn('not license this complete installer', disclosure)
            self.assertEqual({p.name for p in output.iterdir()},
                             {'LICENSE', 'LICENSING.md', 'FIRST-PARTY-SCOPE.json'})

    def test_native_payload_refuses_missing_scope_before_copying(self):
        with tempfile.TemporaryDirectory(prefix='lens-missing-scope-') as directory:
            repository = Path(directory) / 'source'
            repository.mkdir()
            (repository / 'LICENSE').write_bytes((ROOT / 'LICENSE').read_bytes())
            output = Path(directory) / 'payload'
            output.mkdir()
            result = subprocess.run([
                'pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/native/Copy-NativeLicence.ps1'),
                '-RepositoryRoot', str(repository), '-PayloadDirectory', str(output),
                '-SourceRevision', 'a' * 40], capture_output=True, text=True, encoding='utf-8')
            self.assertNotEqual(result.returncode, 0)
            self.assertIn('Missing required scoped licence material', result.stderr)
            self.assertEqual(list(output.iterdir()), [])

    def test_package_licence_contract_rejects_removed_or_tampered_notices(self):
        revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT,
                                           text=True).strip()
        source_paths = {'LICENSE': 'LICENSE',
                        'FIRST-PARTY-SCOPE.json': 'docs/open-source/configuration/mit-scope.json',
                        'LICENSING.md': 'docs/open-source/native-distribution-licensing.md'}
        entries = {name: subprocess.check_output(['git', 'show', revision + ':' + path],
                                                 cwd=ROOT).decode('utf-8')
                   .replace('SOURCE_REVISION', revision) for name, path in source_paths.items()}
        variants = [('valid', entries, True), ('missing-all', {}, False)]
        for name in entries:
            changed = dict(entries)
            changed[name] += 'Unauthorised change.\n'
            variants.append((name, changed, False))
        with tempfile.TemporaryDirectory(prefix='lens-zip-licence-') as directory:
            for label, files, accepted in variants:
                with self.subTest(case=label):
                    archive = Path(directory) / 'payload.zip'
                    with zipfile.ZipFile(archive, 'w') as output:
                        for name, content in files.items():
                            output.writestr(name, content)
                    result = subprocess.run([
                        'pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/native/Test-NativeLicence.ps1'),
                        '-PortablePath', str(archive), '-ExpectedSourceRevision', revision,
                        '-ExpectedVersion', '0.1.0-preview.1', '-InstallerSha256', '0' * 64],
                        capture_output=True, text=True, encoding='utf-8')
                    self.assertEqual(result.returncode == 0, accepted, result.stderr)
                    if not accepted:
                        self.assertRegex(result.stderr, 'scoped licence entry|Scoped licence entry')

    def test_missing_source_cannot_masquerade_as_pre_adoption(self):
        with tempfile.TemporaryDirectory(prefix='lens-unknown-source-') as directory:
            archive = Path(directory) / 'payload.zip'
            with zipfile.ZipFile(archive, 'w'):
                pass
            for revision, version in [('f' * 40, '0.1.0-preview.1'),
                                      ('358147cd6da2265efb49e4922698796f9399fcf6', '0.1.0-preview.853')]:
                result = subprocess.run([
                    'pwsh', '-NoProfile', '-File', str(ROOT / 'scripts/native/Test-NativeLicence.ps1'),
                    '-PortablePath', str(archive), '-ExpectedSourceRevision', revision,
                    '-ExpectedVersion', version, '-InstallerSha256', '0' * 64],
                    capture_output=True, text=True, encoding='utf-8')
                self.assertNotEqual(result.returncode, 0, result.stdout)


class FlowchartProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.refresh = flowchart_json((ROOT / (FLOWCHART_EVIDENCE + 'approved-refresh.json')).read_bytes())
        self.approval = (ROOT / (FLOWCHART_EVIDENCE + 'owner-approval.json')).read_bytes()
        self.candidate = (ROOT / (FLOWCHART_EVIDENCE + 'candidate-refresh.json')).read_bytes()
        self.granted = set(json.loads(SCOPE.read_text(encoding='utf-8'))['paths'])
        self.preserved = json.loads((EVIDENCE / 'preserved-files.json').read_text())['sha256']
        source = ast.parse((ROOT / 'scripts/compliance/licence_scope.py').read_text(encoding='utf-8'))
        policy = next(ast.literal_eval(node.value) for node in ast.walk(source)
                      if isinstance(node, ast.Assign)
                      and any(isinstance(t, ast.Name) and t.id == 'policy_refresh' for t in node.targets))
        for path, (previous, current) in policy.items():
            self.assertEqual(self.preserved[path], previous)
            self.preserved[path] = current
        for name, apply in [('find-caret', apply_find_caret_refresh),
                            ('selection-sync', apply_selection_sync_refresh)]:
            self.preserved = apply(self.preserved, json.loads(
                (ROOT / ('docs/open-source/evidence/' + name + '/preserved-refresh.json')).read_text()))
        self.actual = dict(self.preserved)
        self.actual.update({path: pair['current'] for path, pair in FLOWCHART_PAIRS.items()})

    def apply(self):
        return apply_flowchart_styling_refresh(self.preserved, self.refresh, self.approval,
                                              self.candidate, self.actual, self.granted)

    def test_owner_approved_overlay_changes_exactly_nine_effective_pins_without_mutating_evidence(self):
        before = copy.deepcopy((self.preserved, self.refresh, self.actual, self.granted))
        result = self.apply()
        self.assertEqual({p for p in result if result[p] != self.preserved[p]}, set(FLOWCHART_PAIRS))
        self.assertEqual(len(result), len(self.preserved))
        self.assertEqual(before, (self.preserved, self.refresh, self.actual, self.granted))
        validate_preserved(result, self.actual)

    def test_every_missing_or_unapproved_path_is_rejected(self):
        original = copy.deepcopy(self.refresh)
        for path in FLOWCHART_PAIRS:
            with self.subTest(missing=path), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                del self.refresh['files'][path]
                self.apply()
            self.refresh = copy.deepcopy(original)
            with self.subTest(unapproved=path), self.assertRaises(ValueError):
                self.granted.remove(path)
                try:
                    self.apply()
                finally:
                    self.granted.add(path)

    def test_extra_and_substituted_excluded_or_trust_paths_are_rejected(self):
        original = copy.deepcopy(self.refresh)
        first = next(iter(FLOWCHART_PAIRS))
        excluded = [
            'tests/fixtures/native-flowchart/spc-application-architecture.md',
            'src/native/LensDocsStudio.Native/Resources/CommandIcons.xaml',
            'docs/open-source/evidence/goal62/preserved-files.json',
            'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs',
            'user-content/private.md', 'SECURITY.md',
            '.github/workflows/native-continuous-performance.yml',
        ]
        for path in excluded:
            for substituted in (False, True):
                with self.subTest(path=path, substituted=substituted), self.assertRaises(ValueError):
                    self.refresh = copy.deepcopy(original)
                    self.refresh['files'][path] = self.refresh['files'][first]
                    if substituted:
                        del self.refresh['files'][first]
                    self.apply()

    def test_every_previous_current_and_actual_digest_is_bound_to_exact_bytes(self):
        for path in FLOWCHART_PAIRS:
            for field in ('previous', 'current'):
                for bad in ('0' * 64, 'not-a-digest'):
                    with self.subTest(path=path, field=field, bad=bad), self.assertRaises(ValueError):
                        original = self.refresh['files'][path][field]
                        self.refresh['files'][path][field] = bad
                        try:
                            self.apply()
                        finally:
                            self.refresh['files'][path][field] = original
            with self.subTest(actual=path), self.assertRaises(ValueError):
                actual = self.actual.pop(path)
                try:
                    self.apply()
                finally:
                    self.actual[path] = actual
            with self.subTest(previous=path), self.assertRaises(ValueError):
                previous = self.preserved[path]
                self.preserved[path] = '0' * 64
                try:
                    self.apply()
                finally:
                    self.preserved[path] = previous

    def test_program_chains_from_selection_sync_rather_than_the_frozen_goal62_pin(self):
        path = 'src/native/LensDocsStudio.Native.Validation/Program.cs'
        self.preserved[path] = json.loads((EVIDENCE / 'preserved-files.json').read_text())['sha256'][path]
        with self.assertRaisesRegex(ValueError, 'previous effective pin'):
            self.apply()

    def test_exact_commit_and_tree_identities_cannot_be_substituted(self):
        original = copy.deepcopy(self.refresh)
        for identity in ('preparation', 'baseline', 'candidate', 'evaluation', 'originalEvidence'):
            for field in ('commit', 'tree'):
                with self.subTest(identity=identity, field=field), self.assertRaises(ValueError):
                    self.refresh = copy.deepcopy(original)
                    self.refresh[identity][field] = '0' * 40
                    self.apply()

    def test_proposal_approval_scope_and_override_fields_cannot_change(self):
        original = copy.deepcopy(self.refresh)
        mutations = [
            ('proposalId', 'another-proposal'), ('status', 'PROPOSED_NOT_APPROVED'),
            ('repository', 'another/repository'), ('candidateManifestSha256', '0' * 64),
            ('ownerApprovalSha256', '0' * 64), ('excludedBoundaries', ['R3']),
            ('runtimeSourceTrees', {}), ('allowUnapproved', True), ('schemaVersion', True),
        ]
        for field, value in mutations:
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                self.refresh[field] = value
                self.apply()

    def test_missing_forged_or_modified_owner_receipt_is_rejected(self):
        original = self.approval
        for altered in (None, b'', original + b'\n',
                        original.replace(b'"role": "user"', b'"role": "agent"'),
                        original.replace(b'only the nine', b'all the')):
            with self.subTest(approval=altered is None), self.assertRaises(ValueError):
                self.approval = altered
                self.apply()

    def test_candidate_manifest_is_the_exact_approved_preparation_document(self):
        self.candidate += b'\n'
        with self.assertRaises(ValueError):
            self.apply()

    def test_duplicate_fields_cannot_override_approval_or_path_decisions(self):
        for raw in (b'{"files": {}, "files": {}}',
                    b'{"files": {"path": 1, "path": 2}}',
                    b'{"status": "PROPOSED", "status": "APPROVED"}'):
            with self.subTest(raw=raw), self.assertRaisesRegex(ValueError, 'Duplicate'):
                flowchart_json(raw)

    def test_canonical_receipt_and_manifest_accept_git_checkout_line_endings(self):
        self.approval = self.approval.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        self.candidate = self.candidate.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        validate_preserved(self.apply(), self.actual)

    def test_actual_git_filters_must_preserve_the_approved_evidence_digests(self):
        with tempfile.TemporaryDirectory(prefix='lens-flowchart-evidence-') as directory:
            repository = Path(directory)
            subprocess.run(['git', 'init', '-q', str(repository)], check=True, capture_output=True)
            subprocess.run(['git', '-C', str(repository), 'config', 'core.autocrlf', 'false'], check=True)
            folder = repository / FLOWCHART_EVIDENCE
            folder.mkdir(parents=True)
            for name, raw in [('candidate-refresh.json', self.candidate), ('owner-approval.json', self.approval)]:
                (folder / name).write_bytes(raw.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n'))
            names = {FLOWCHART_EVIDENCE + name for name in ('candidate-refresh.json', 'owner-approval.json')}
            attributes = repository / '.gitattributes'
            attributes.write_text('*.json -text\n', encoding='utf-8')
            with patch('rights_ledger.ROOT', repository):
                with self.assertRaisesRegex(ValueError, 'canonical evidence bytes'):
                    validate_flowchart_evidence_digests(current_digests(names))
                attributes.write_text('*.json text eol=lf\n', encoding='utf-8')
                validate_flowchart_evidence_digests(current_digests(names))

    def test_protected_change_outside_the_nine_still_fails_full_validation(self):
        updated = self.apply()
        for path in ('SECURITY.md', '.github/workflows/native-continuous-performance.yml'):
            with self.subTest(path=path), self.assertRaises(ValueError):
                actual = dict(self.actual)
                actual[path] = '0' * 64
                validate_preserved(updated, actual)

    def test_current_source_subtrees_and_working_bytes_match_the_approved_identity(self):
        check_flowchart_source_identity(current_paths())

    def test_changed_tree_missing_extra_or_modified_helper_bytes_fail_closed(self):
        helper = 'src/native/LensDocsStudio.Core/Rendering/FlowchartCompatibility.cs'
        expected = {helper: 'a' * 40}
        validate_flowchart_source_identity(FLOWCHART_SOURCE_TREES, expected, dict(expected))
        for actual in ({}, {**expected, helper + '.new': 'b' * 40}, {helper: 'b' * 40}):
            with self.subTest(actual=actual), self.assertRaises(ValueError):
                validate_flowchart_source_identity(FLOWCHART_SOURCE_TREES, expected, actual)
        changed = dict(FLOWCHART_SOURCE_TREES)
        changed['src/native'] = '0' * 40
        with self.assertRaises(ValueError):
            validate_flowchart_source_identity(changed, expected, expected)
        with self.assertRaises(ValueError):
            validate_flowchart_source_identity(FLOWCHART_SOURCE_TREES, {}, {})


if __name__ == '__main__':
    unittest.main()
