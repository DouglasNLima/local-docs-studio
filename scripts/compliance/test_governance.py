"""Negative controls for approval deadlocks, bypass and inapplicable CI."""
import copy
import json
import unittest
import subprocess
import tempfile
import zipfile
from pathlib import Path

from check_governance import CONFIG, EVIDENCE, validate_ruleset, validate_admin_packet
from rights_ledger import boundary
from check_foundation import documentation_links
from licence_scope import (ROOT, SCOPE, validate_licence, validate_scope,
                           validate_metadata, validate_preserved)


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


if __name__ == '__main__':
    unittest.main()
