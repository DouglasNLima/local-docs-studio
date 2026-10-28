"""Negative controls for approval deadlocks, bypass and inapplicable CI."""
import copy
import json
import unittest

from check_governance import CONFIG, EVIDENCE, validate_ruleset
from rights_ledger import boundary
from check_foundation import documentation_links


class GovernanceBoundaryTests(unittest.TestCase):
    def setUp(self):
        self.payload = json.loads((CONFIG / 'develop-ruleset.json').read_text())
        evidence = json.loads((EVIDENCE / 'check-contexts.json').read_text())
        self.observed = {(r['name'], r['integrationId']) for r in evidence['contexts']}

    def test_valid_solo_proposal(self):
        validate_ruleset(self.payload, 'develop', self.observed)

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
            ('tests/fixtures/sample.md', 'R3'),
            ('docs/native/evidence/receipt.json', 'R5'),
            ('docs/open-source/evidence/upstream-notices/grant.txt', 'R5'),
        ]:
            with self.subTest(path=path):
                self.assertEqual(boundary(path)[0], category)


if __name__ == '__main__':
    unittest.main()
