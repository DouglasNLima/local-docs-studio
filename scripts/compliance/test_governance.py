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
from licence_scope import (PREVIEW_LOADING_EVIDENCE, PREVIEW_LOADING_PAIRS, PREVIEW_LOADING_SOURCE_TREES,
                           apply_preview_loading_refresh, validate_preview_loading_approval,
                           check_preview_loading_source_identity, validate_preview_loading_evidence_digests,
                           validate_source_identity)
from licence_scope import (PREVIEW_REFLOW_EVIDENCE, PREVIEW_REFLOW_PAIRS, PREVIEW_REFLOW_SOURCE_TREES,
                           PREVIEW_REFLOW_MANIFEST_SHA256, PREVIEW_REFLOW_APPROVAL_SHA256,
                           PREVIEW_REFLOW_EARLIER_REFRESHES, apply_preview_reflow_refresh,
                           validate_preview_reflow_approval, check_preview_reflow_source_identity,
                           read_committed_evidence, read_working_evidence)
from licence_scope import (DOCUMENT_STATISTICS_EVIDENCE, DOCUMENT_STATISTICS_MANIFEST_SHA256,
                           DOCUMENT_STATISTICS_APPROVAL_SHA256, DOCUMENT_STATISTICS_REFRESH_SHA256,
                           DOCUMENT_STATISTICS_GOVERNANCE_PATHS,
                           apply_document_statistics_refresh, validate_document_statistics_approval,
                           check_document_statistics_source_identity, check_committed_file_identities,
                           document_statistics_predecessor_digests)


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
        check_document_statistics_source_identity(current_paths())

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


class PreviewLoadingProvenanceTests(unittest.TestCase):
    def setUp(self):
        prior = FlowchartProvenanceTests()
        prior.setUp()
        self.preserved = prior.apply()
        self.granted = prior.granted
        self.actual = dict(self.preserved)
        self.actual.update({path: pair['current'] for path, pair in PREVIEW_LOADING_PAIRS.items()})
        self.refresh = flowchart_json((ROOT / (PREVIEW_LOADING_EVIDENCE + 'approved-refresh.json')).read_bytes())
        self.approval = (ROOT / (PREVIEW_LOADING_EVIDENCE + 'owner-approval.json')).read_bytes()
        self.candidate = (ROOT / (PREVIEW_LOADING_EVIDENCE + 'candidate-refresh.json')).read_bytes()

    def apply(self):
        return apply_preview_loading_refresh(self.preserved, self.refresh, self.approval,
                                             self.candidate, self.actual, self.granted)

    def test_exact_one_pair_overlay_preserves_prior_nine_and_inputs(self):
        before = copy.deepcopy((self.preserved, self.refresh, self.actual, self.granted))
        updated = self.apply()
        self.assertEqual({p for p in updated if updated[p] != self.preserved[p]}, set(PREVIEW_LOADING_PAIRS))
        self.assertEqual(len(updated), len(self.preserved))
        for path, pair in FLOWCHART_PAIRS.items():
            self.assertEqual(updated[path], pair['current'])
        self.assertEqual(before, (self.preserved, self.refresh, self.actual, self.granted))
        validate_preserved(updated, self.actual)

    def test_missing_extra_and_substituted_paths_are_rejected(self):
        original = copy.deepcopy(self.refresh)
        path = next(iter(PREVIEW_LOADING_PAIRS))
        variants = [{}, {**original['files'], 'SECURITY.md': original['files'][path]},
                    {'.github/workflows/native.yml': original['files'][path]}]
        for files in variants:
            with self.subTest(files=files), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                self.refresh['files'] = files
                self.apply()

    def test_previous_current_and_actual_digests_and_grant_are_required(self):
        path = next(iter(PREVIEW_LOADING_PAIRS))
        for field in ('previous', 'current'):
            with self.subTest(field=field), self.assertRaises(ValueError):
                original = self.refresh['files'][path][field]
                self.refresh['files'][path][field] = '0' * 64
                try:
                    self.apply()
                finally:
                    self.refresh['files'][path][field] = original
        for values in (self.preserved, self.actual):
            original = values.pop(path)
            with self.assertRaises(ValueError):
                self.apply()
            values[path] = '0' * 64
            with self.assertRaises(ValueError):
                self.apply()
            values[path] = original
        self.granted.remove(path)
        with self.assertRaises(ValueError):
            self.apply()

    def test_exact_identities_scope_and_successor_trees_cannot_drift(self):
        original = copy.deepcopy(self.refresh)
        changes = [('schemaVersion', True), ('status', 'PROPOSED_NOT_APPROVED'), ('proposalId', 'other'),
                   ('repository', 'other/repo'), ('ownerApprovalSha256', '0' * 64),
                   ('candidateManifestSha256', '0' * 64), ('excludedBoundaries', []),
                   ('previousRuntimeSourceTrees', {}), ('runtimeSourceTrees', {}), ('allowUnapproved', True)]
        changes += [(key, {'commit': '0' * 40, 'tree': '0' * 40})
                    for key in ('baseline', 'candidate', 'preparation')]
        for field, value in changes:
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                self.refresh[field] = value
                self.apply()

    def test_missing_modified_or_forged_approval_and_manifest_fail_closed(self):
        original = self.approval
        for value in (None, b'', original + b'\n', original.replace(b'"role": "user"', b'"role": "agent"')):
            with self.subTest(approval=value is None), self.assertRaises(ValueError):
                self.approval = value
                self.apply()
        self.approval = original
        self.candidate += b'\n'
        with self.assertRaises(ValueError):
            self.apply()

    def test_duplicate_fields_cannot_override_the_successor(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            flowchart_json(b'{"runtimeSourceTrees": {}, "runtimeSourceTrees": {}}')

    def test_successor_requires_exact_tree_and_complete_working_object_inventory(self):
        trees = validate_preview_loading_approval(self.refresh, self.approval, self.candidate)
        objects = {next(iter(PREVIEW_LOADING_PAIRS)): 'a' * 40}
        validate_source_identity(trees, PREVIEW_LOADING_SOURCE_TREES, objects, dict(objects))
        for actual in ({}, {**objects, 'src/native/additional.cs': 'b' * 40},
                       {next(iter(objects)): 'b' * 40}):
            with self.subTest(actual=actual), self.assertRaises(ValueError):
                validate_source_identity(trees, PREVIEW_LOADING_SOURCE_TREES, objects, actual)
        with self.assertRaises(ValueError):
            validate_source_identity(FLOWCHART_SOURCE_TREES, trees, objects, objects)
        with self.assertRaises(ValueError):
            validate_flowchart_source_identity(trees, objects, objects)
        with self.assertRaises(ValueError):
            validate_source_identity(trees, trees, {}, {})

    def test_other_protected_material_and_prior_flowchart_evidence_stay_protected(self):
        updated = self.apply()
        for path in ('SECURITY.md', '.github/workflows/native-continuous-performance.yml', *FLOWCHART_PAIRS):
            with self.subTest(path=path), self.assertRaises(ValueError):
                validate_preserved(updated, {**self.actual, path: '0' * 64})

    def test_git_canonical_evidence_digest_changes_are_rejected(self):
        names = {PREVIEW_LOADING_EVIDENCE + name for name in ('candidate-refresh.json', 'owner-approval.json')}
        actual = current_digests(names)
        validate_preview_loading_evidence_digests(actual)
        for path in names:
            with self.subTest(path=path), self.assertRaises(ValueError):
                validate_preview_loading_evidence_digests({**actual, path: '0' * 64})

    def test_checkout_line_endings_do_not_change_exact_approval(self):
        self.approval = self.approval.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        self.candidate = self.candidate.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        validate_preserved(self.apply(), self.actual)


class PreviewReflowProvenanceTests(unittest.TestCase):
    def setUp(self):
        prior = PreviewLoadingProvenanceTests()
        prior.setUp()
        self.preserved = prior.apply()
        self.granted = prior.granted
        self.actual = {**self.preserved, **{p: pair['current'] for p, pair in PREVIEW_REFLOW_PAIRS.items()}}
        self.refresh = flowchart_json((ROOT / (PREVIEW_REFLOW_EVIDENCE + 'approved-refresh.json')).read_bytes())
        self.approval, _ = read_working_evidence(
            PREVIEW_REFLOW_EVIDENCE + 'owner-approval.json', PREVIEW_REFLOW_APPROVAL_SHA256)
        self.candidate = read_committed_evidence(
            PREVIEW_REFLOW_EVIDENCE + 'candidate-refresh.json', PREVIEW_REFLOW_MANIFEST_SHA256)

    def apply(self):
        return apply_preview_reflow_refresh(self.preserved, self.refresh, self.approval,
                                           self.candidate, self.actual, self.granted)

    def test_three_pairs_preserve_all_2421_pins_and_previous_successors(self):
        before = copy.deepcopy((self.preserved, self.refresh, self.actual, self.granted))
        updated = self.apply()
        self.assertEqual(len(updated), 2421)
        self.assertEqual({p for p in updated if updated[p] != self.preserved[p]}, set(PREVIEW_REFLOW_PAIRS))
        for path, pair in {**FLOWCHART_PAIRS, **PREVIEW_LOADING_PAIRS}.items():
            self.assertEqual(updated[path], pair['current'])
        validate_preserved(updated, self.actual)
        self.assertEqual(before, (self.preserved, self.refresh, self.actual, self.granted))

    def test_rejects_missing_additional_or_substituted_protected_pairs(self):
        original = copy.deepcopy(self.refresh)
        for path in PREVIEW_REFLOW_PAIRS:
            for extra in (None, 'SECURITY.md', 'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs'):
                for remove in (False, True):
                    if extra is None and not remove:
                        continue
                    with self.subTest(path=path, extra=extra, remove=remove), self.assertRaises(ValueError):
                        self.refresh = copy.deepcopy(original)
                        if extra:
                            self.refresh['files'][extra] = self.refresh['files'][path]
                        if remove:
                            del self.refresh['files'][path]
                        self.apply()

    def test_rejects_every_altered_pair_effective_pin_actual_byte_and_missing_grant(self):
        for path in PREVIEW_REFLOW_PAIRS:
            for mapping, field in [(self.refresh['files'][path], 'previous'),
                                    (self.refresh['files'][path], 'current'),
                                    (self.preserved, path), (self.actual, path)]:
                for missing in (False, True):
                    with self.subTest(path=path, field=field, missing=missing):
                        original = mapping.pop(field)
                        if not missing:
                            mapping[field] = '0' * 64
                        try:
                            with self.assertRaises(ValueError):
                                self.apply()
                        finally:
                            mapping[field] = original
            self.granted.remove(path)
            with self.subTest(ungranted=path), self.assertRaises(ValueError):
                self.apply()
            self.granted.add(path)

    def test_rejects_identity_scope_added_file_and_evidence_overrides(self):
        original = copy.deepcopy(self.refresh)
        for field, value in [('schemaVersion', True), ('proposalId', 'preview-reflow-integration-three-v1'),
                ('status', 'PROPOSED_NOT_APPROVED'), ('ownerDecision', 'https://example.com/approval'),
                ('candidateManifestSha256', '1cecd8b708ea165e1bb73eb98382efb7799bd5dddd21cad3e5d46c586e3db9eb'),
                ('ownerApprovalSha256', '0' * 64), ('repository', 'another/repo'),
                ('excludedBoundaries', []), ('preparation', {}), ('baseline', {}), ('candidate', {}),
                ('historicalSource', {}), ('runtimeSourceTrees', {}), ('controlTrees', {}),
                ('previousRuntimeSourceTrees', {}), ('addedFiles', {}), ('preservedEvidence', {}),
                ('historicalEvidenceTrees', {}), ('activationPathsOnly', []), ('allowUnapproved', True)]:
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                self.refresh[field] = value
                self.apply()

    def test_rejects_modified_missing_or_forged_owner_decision_and_manifest(self):
        for field in ('approval', 'candidate'):
            original = getattr(self, field)
            values = [None, b'', original + b'\n', original.replace(b'\n', b'\r\n'),
                      original.replace(b'preview-reflow-integration-three-v2', b'preview-reflow-integration-three-v1')]
            if field == 'approval':
                values.append(original.replace(b'6765036155', b'6765036156'))
                values.append(original.replace(b'DouglasNLima', b'AnotherOwner'))
            for value in values:
                with self.subTest(field=field, missing=value is None), self.assertRaises(ValueError):
                    setattr(self, field, value)
                    self.apply()
            setattr(self, field, original)

    def test_rejects_protected_changes_outside_the_three_and_lost_inventory(self):
        updated = self.apply()
        for path in ('SECURITY.md', '.github/workflows/native-continuous-performance.yml',
                     *FLOWCHART_PAIRS, *PREVIEW_LOADING_PAIRS):
            for missing in (False, True):
                with self.subTest(path=path, missing=missing), self.assertRaises(ValueError):
                    actual = dict(self.actual)
                    actual.pop(path)
                    if not missing:
                        actual[path] = '0' * 64
                    validate_preserved(updated, actual)
        del self.preserved['SECURITY.md']
        with self.assertRaises(ValueError):
            self.apply()

    def test_added_paths_have_exact_digests_without_an_additional_licence_grant(self):
        added = json.loads(self.candidate)['addedFiles']
        self.assertEqual(len(added), 9)
        self.assertFalse(set(added) & self.granted)
        validate_preserved(added, current_digests(set(added)))
        for path in added:
            for missing in (False, True):
                with self.subTest(path=path, missing=missing), self.assertRaises(ValueError):
                    actual = dict(added)
                    actual.pop(path)
                    if not missing:
                        actual[path] = '0' * 64
                    validate_preserved(added, actual)

    def test_rejects_native_control_fixture_and_historical_tree_or_inventory_changes(self):
        trees = {**PREVIEW_REFLOW_SOURCE_TREES, **json.loads(self.candidate)['historicalEvidenceTrees']}
        objects = {p: 'a' * 40 for p in json.loads(self.candidate)['addedFiles']}
        for path in trees:
            with self.subTest(tree=path), self.assertRaises(ValueError):
                validate_source_identity({**trees, path: '0' * 40}, trees, objects, objects)
        for actual in ({}, {**objects, 'src/native/unapproved.cs': 'b' * 40},
                       {**objects, next(iter(objects)): 'b' * 40}):
            with self.subTest(inventory=actual), self.assertRaises(ValueError):
                validate_source_identity(trees, trees, objects, actual)

    def test_all_prior_approvals_and_scope_are_exact_committed_and_working_bytes(self):
        for path, digest in {**json.loads(self.candidate)['preservedEvidence'],
                             **PREVIEW_REFLOW_EARLIER_REFRESHES}.items():
            with self.subTest(path=path):
                read_committed_evidence(path, digest)


class PreviewReflowCheckoutTests(unittest.TestCase):
    evidence_prefix = PREVIEW_REFLOW_EVIDENCE
    manifest_sha256 = PREVIEW_REFLOW_MANIFEST_SHA256

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='lens-reflow-provenance-')
        self.addCleanup(self.temporary.cleanup)
        self.repository = Path(self.temporary.name)
        self.path = self.evidence_prefix + 'candidate-refresh.json'
        self.raw = subprocess.check_output(['git', 'cat-file', 'blob', 'HEAD:' + self.path], cwd=ROOT)
        self.git('init', '-q')
        self.git('config', 'core.autocrlf', 'false')
        self.git('config', 'user.name', 'Local control')
        self.git('config', 'user.email', 'control@example.invalid')
        self.file = self.repository / self.path
        self.file.parent.mkdir(parents=True)
        self.file.write_bytes(self.raw)
        self.git('add', '--', self.path)
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Local fixture [skip ci]')

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.repository, stderr=subprocess.PIPE)

    def verify(self, path=None, digest=None):
        with patch('licence_scope.ROOT', self.repository):
            return read_committed_evidence(path or self.path, digest or self.manifest_sha256)

    def test_lf_and_crlf_checkout_conversion_retain_exact_committed_identity(self):
        for autocrlf in ('false', 'true'):
            self.git('config', 'core.autocrlf', autocrlf)
            self.file.unlink()
            self.git('checkout-index', '--force', '--', self.path)
            self.assertEqual(b'\r\n' in self.file.read_bytes(), autocrlf == 'true')
            self.assertEqual(self.verify(), self.raw)

    def test_rejects_semantic_and_formatting_edits_in_working_or_committed_manifest(self):
        for raw in (self.raw.replace(b'PROPOSED_NOT_APPROVED', b'APPROVED'), self.raw + b'\n'):
            with self.subTest(committed=False), self.assertRaises(ValueError):
                self.file.write_bytes(raw)
                self.verify()
            self.git('add', '--', self.path)
            self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Negative fixture [skip ci]')
            with self.subTest(committed=True), self.assertRaises(ValueError):
                self.verify()

    def test_rejects_missing_working_or_committed_manifest(self):
        self.file.unlink()
        with self.assertRaises(ValueError):
            self.verify()
        self.git('add', '-u')
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Missing fixture [skip ci]')
        with self.assertRaises(ValueError):
            self.verify()

    def test_rejects_substituted_working_manifest(self):
        self.file.unlink()
        self.file.mkdir()
        with self.assertRaises(ValueError):
            self.verify()

    def test_rejects_modified_index_even_with_restored_working_bytes(self):
        self.file.write_bytes(self.raw + b'\n')
        self.git('add', '--', self.path)
        self.file.write_bytes(self.raw)
        with self.assertRaisesRegex(ValueError, 'index differs'):
            self.verify()

    def test_rejects_clean_filter_that_changes_canonical_identity(self):
        self.file.write_bytes(self.raw.replace(b'\n', b'\r\n'))
        (self.repository / '.gitattributes').write_text('*.json -text\n')
        with self.assertRaises(ValueError):
            self.verify()

    def test_rejects_committed_crlf_blob_as_a_different_approval_identity(self):
        self.file.write_bytes(self.raw.replace(b'\n', b'\r\n'))
        self.git('add', '--', self.path)
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'CRLF blob [skip ci]')
        with self.assertRaises(ValueError):
            self.verify()

    def predecessor_evidence(self):
        manifest = json.loads(self.raw)
        return {**manifest['preservedEvidence'], **PREVIEW_REFLOW_EARLIER_REFRESHES}

    def test_rejects_each_altered_predecessor_approval(self):
        for path, digest in self.predecessor_evidence().items():
            original = subprocess.check_output(['git', 'cat-file', 'blob', 'HEAD:' + path], cwd=ROOT)
            file = self.repository / path
            file.parent.mkdir(parents=True, exist_ok=True)
            file.write_bytes(original)
            self.git('add', '--', path)
            self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Prior approval [skip ci]')
            self.verify(path, digest)
            for committed in (False, True):
                file.write_bytes(original + b'\n')
                if committed:
                    self.git('add', '--', path)
                    self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Altered approval [skip ci]')
                with self.subTest(path=path, committed=committed), self.assertRaises(ValueError):
                    self.verify(path, digest)


class DocumentStatisticsProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.prior = PreviewReflowProvenanceTests()
        self.prior.setUp()
        self.preserved = self.prior.apply()
        self.granted = set(self.prior.granted)
        self.candidate = read_committed_evidence(
            DOCUMENT_STATISTICS_EVIDENCE + 'candidate-refresh.json', DOCUMENT_STATISTICS_MANIFEST_SHA256)
        self.approval, _ = read_working_evidence(
            DOCUMENT_STATISTICS_EVIDENCE + 'owner-approval.json', DOCUMENT_STATISTICS_APPROVAL_SHA256)
        self.refresh = flowchart_json((ROOT / (DOCUMENT_STATISTICS_EVIDENCE + 'approved-refresh.json')).read_bytes())
        self.contract = json.loads(self.candidate)
        self.pairs = self.contract['files']
        self.actual = {**self.preserved, **{p: pair['current'] for p, pair in self.pairs.items()}}

    def apply(self):
        return apply_document_statistics_refresh(self.preserved, self.refresh, self.approval,
                                                 self.candidate, self.actual, self.granted)

    def test_exact_ten_pair_overlay_retains_2421_paths_and_does_not_mutate_its_inputs(self):
        original = copy.deepcopy((self.preserved, self.refresh, self.actual, self.granted))
        result = self.apply()
        self.assertEqual(len(result), 2421)
        self.assertEqual(set(result), set(self.preserved))
        self.assertEqual({p for p in result if result[p] != self.preserved[p]}, set(self.pairs))
        self.assertEqual(len(self.pairs), 10)
        self.assertEqual(sum(boundary(p, self.granted)[0] == 'R2' for p in self.pairs), 5)
        validate_preserved(result, self.actual)
        self.assertEqual(original, (self.preserved, self.refresh, self.actual, self.granted))

    def test_missing_additional_or_substituted_pairs_never_expand_the_amendment(self):
        original = copy.deepcopy(self.refresh)
        for path in self.pairs:
            for extra in (None, 'SECURITY.md', 'src/native/LensDocsStudio.Core/Rendering/BoundedErLayout.cs'):
                for remove in (False, True):
                    if extra is None and not remove:
                        continue
                    with self.subTest(path=path, extra=extra, remove=remove), self.assertRaises(ValueError):
                        self.refresh = copy.deepcopy(original)
                        if extra:
                            self.refresh['files'][extra] = self.refresh['files'][path]
                        if remove:
                            del self.refresh['files'][path]
                        self.apply()

    def test_every_pair_requires_exact_previous_current_actual_bytes_and_existing_grant(self):
        for path in self.pairs:
            for mapping, field in [(self.refresh['files'][path], 'previous'),
                                    (self.refresh['files'][path], 'current'),
                                    (self.preserved, path), (self.actual, path)]:
                for missing in (False, True):
                    with self.subTest(path=path, field=field, missing=missing):
                        original = mapping.pop(field)
                        if not missing:
                            mapping[field] = '0' * 64
                        try:
                            with self.assertRaises(ValueError):
                                self.apply()
                        finally:
                            mapping[field] = original
            self.granted.remove(path)
            with self.subTest(ungranted=path), self.assertRaises(ValueError):
                self.apply()
            self.granted.add(path)

    def test_rejects_all_identity_scope_evidence_and_override_changes(self):
        original = copy.deepcopy(self.refresh)
        for field in original:
            value = original[field]
            replacement = {} if isinstance(value, dict) else [] if isinstance(value, list) else 'unapproved'
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                self.refresh[field] = replacement
                self.apply()
        for field, value in [('schemaVersion', True), ('allowUnapproved', True), ('licenceGrant', True)]:
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.refresh = copy.deepcopy(original)
                self.refresh[field] = value
                self.apply()

    def test_canonical_owner_and_manifest_bytes_cannot_be_replaced_or_reserialised(self):
        for field in ('approval', 'candidate'):
            original = getattr(self, field)
            for value in (None, b'', original + b'\n', original.replace(b'\n', b'\r\n'),
                          original.replace(b'DouglasNLima', b'AnotherOwner'),
                          original.replace(b'document-statistics-ten-v1', b'document-statistics-ten-v2')):
                with self.subTest(field=field, missing=value is None), self.assertRaises(ValueError):
                    setattr(self, field, value)
                    self.apply()
            setattr(self, field, original)
        with self.assertRaises(ValueError):
            flowchart_json(b'{"files": {}, "files": {}}')

    def test_predecessor_replay_is_bounded_and_requires_verified_successor_bytes_first(self):
        previous = document_statistics_predecessor_digests(
            self.refresh, self.approval, self.candidate, self.actual)
        self.assertEqual(previous, self.preserved)
        self.assertEqual(apply_preview_reflow_refresh(self.prior.preserved, self.prior.refresh,
            self.prior.approval, self.prior.candidate, previous, self.granted), self.preserved)
        with self.assertRaises(ValueError):
            apply_preview_reflow_refresh(self.prior.preserved, self.prior.refresh,
                self.prior.approval, self.prior.candidate, self.actual, self.granted)
        for path in self.pairs:
            with self.subTest(path=path), self.assertRaises(ValueError):
                document_statistics_predecessor_digests(self.refresh, self.approval, self.candidate,
                    {**self.actual, path: self.pairs[path]['previous']})

    def test_all_28_added_files_are_pinned_without_a_new_licence_grant(self):
        added = self.contract['addedFiles']
        self.assertEqual(len(added), 28)
        self.assertFalse(set(added) & self.granted)
        check_committed_file_identities(added)
        for path in added:
            for missing in (False, True):
                with self.subTest(path=path, missing=missing), self.assertRaises(ValueError):
                    actual = dict(added)
                    actual.pop(path)
                    if not missing:
                        actual[path] = '0' * 64
                    validate_preserved(added, actual)

    def test_unrelated_protected_failures_and_previous_amendments_remain_protected(self):
        result = self.apply()
        for path in ('SECURITY.md', '.github/workflows/native-continuous-performance.yml',
                     *FLOWCHART_PAIRS, *PREVIEW_LOADING_PAIRS,
                     *(set(PREVIEW_REFLOW_PAIRS) - set(self.pairs))):
            with self.subTest(path=path), self.assertRaises(ValueError):
                validate_preserved(result, {**self.actual, path: '0' * 64})
        del self.preserved['SECURITY.md']
        with self.assertRaises(ValueError):
            self.apply()

    def test_native_control_unchanged_tree_and_complete_inventory_changes_fail_closed(self):
        trees = {**self.contract['runtimeSourceTrees'], **self.contract['controlTrees'],
                 **self.contract['unchangedTrees']}
        objects = {p: 'a' * 40 for p in self.contract['addedFiles']}
        for path in trees:
            with self.subTest(tree=path), self.assertRaises(ValueError):
                validate_source_identity({**trees, path: '0' * 40}, trees, objects, objects)
        for actual in ({}, {**objects, 'src/native/unapproved.cs': 'b' * 40},
                       {**objects, next(iter(objects)): 'b' * 40}):
            with self.subTest(inventory=actual), self.assertRaises(ValueError):
                validate_source_identity(trees, trees, objects, actual)

    def test_prior_approval_evidence_allowlist_and_original_inventory_stay_exact(self):
        for path, digest in {**self.contract['preservedEvidence'], **PREVIEW_REFLOW_EARLIER_REFRESHES}.items():
            if path not in DOCUMENT_STATISTICS_GOVERNANCE_PATHS:
                with self.subTest(path=path):
                    read_committed_evidence(path, digest)


class DocumentStatisticsCheckoutTests(PreviewReflowCheckoutTests):
    evidence_prefix = DOCUMENT_STATISTICS_EVIDENCE
    manifest_sha256 = DOCUMENT_STATISTICS_MANIFEST_SHA256

    def predecessor_evidence(self):
        return {p: digest for p, digest in super().predecessor_evidence().items()
                if p not in DOCUMENT_STATISTICS_GOVERNANCE_PATHS}

    def test_owner_receipt_is_pinned_in_committed_index_and_working_identities(self):
        path = DOCUMENT_STATISTICS_EVIDENCE + 'owner-approval.json'
        raw, _ = read_working_evidence(path, DOCUMENT_STATISTICS_APPROVAL_SHA256)
        file = self.repository / path
        file.write_bytes(raw)
        self.git('add', '--', path)
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Owner receipt fixture [skip ci]')
        self.assertEqual(self.verify(path, DOCUMENT_STATISTICS_APPROVAL_SHA256), raw)
        file.write_bytes(raw + b'\n')
        with self.assertRaises(ValueError):
            self.verify(path, DOCUMENT_STATISTICS_APPROVAL_SHA256)
        self.git('add', '--', path)
        file.write_bytes(raw)
        with self.assertRaisesRegex(ValueError, 'index differs'):
            self.verify(path, DOCUMENT_STATISTICS_APPROVAL_SHA256)
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Altered owner fixture [skip ci]')
        with self.assertRaises(ValueError):
            self.verify(path, DOCUMENT_STATISTICS_APPROVAL_SHA256)

    def test_active_receipt_requires_exact_committed_bytes_and_index_identity(self):
        path = DOCUMENT_STATISTICS_EVIDENCE + 'approved-refresh.json'
        raw, _ = read_working_evidence(path, DOCUMENT_STATISTICS_REFRESH_SHA256)
        file = self.repository / path
        file.write_bytes(raw)
        with self.assertRaisesRegex(ValueError, 'Committed provenance evidence is missing'):
            self.verify(path, DOCUMENT_STATISTICS_REFRESH_SHA256)
        self.git('add', '--', path)
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Active receipt fixture [skip ci]')
        self.assertEqual(self.verify(path, DOCUMENT_STATISTICS_REFRESH_SHA256), raw)
        file.write_bytes(raw + b'\n')
        self.git('add', '--', path)
        file.write_bytes(raw)
        with self.assertRaisesRegex(ValueError, 'index differs'):
            self.verify(path, DOCUMENT_STATISTICS_REFRESH_SHA256)

    def check_source(self, expected):
        with patch('licence_scope.ROOT', self.repository), patch('rights_ledger.ROOT', self.repository):
            check_committed_file_identities(expected)

    def test_protected_source_lf_and_crlf_checkouts_match_committed_blob(self):
        expected = {self.path: self.manifest_sha256}
        for autocrlf in ('false', 'true'):
            self.git('config', 'core.autocrlf', autocrlf)
            self.file.unlink()
            self.git('checkout-index', '--force', '--', self.path)
            self.check_source(expected)

    def test_protected_source_rejects_dirty_working_index_missing_and_substituted_objects(self):
        expected = {self.path: self.manifest_sha256}
        self.file.write_bytes(self.raw + b'\n')
        with self.assertRaisesRegex(ValueError, 'Git-clean working'):
            self.check_source(expected)
        self.git('add', '--', self.path)
        self.file.write_bytes(self.raw)
        with self.assertRaisesRegex(ValueError, 'index differs'):
            self.check_source(expected)
        self.git('add', '--', self.path)
        self.git('update-index', '--chmod=+x', '--', self.path)
        with self.assertRaisesRegex(ValueError, 'index differs'):
            self.check_source(expected)
        self.git('update-index', '--chmod=-x', '--', self.path)
        self.file.unlink()
        with self.assertRaisesRegex(ValueError, 'missing or substituted'):
            self.check_source(expected)
        self.file.mkdir()
        with self.assertRaisesRegex(ValueError, 'missing or substituted'):
            self.check_source(expected)
        with self.assertRaisesRegex(ValueError, 'committed files are missing'):
            self.check_source({'missing.txt': '0' * 64})

    def test_unapproved_committed_blob_digest_is_rejected_even_when_index_and_working_match(self):
        self.file.write_bytes(self.raw + b'\n')
        self.git('add', '--', self.path)
        self.git('-c', 'commit.gpgsign=false', 'commit', '-qm', 'Unapproved source fixture [skip ci]')
        with self.assertRaisesRegex(ValueError, 'bytes changed'):
            self.check_source({self.path: self.manifest_sha256})


if __name__ == '__main__':
    unittest.main()
