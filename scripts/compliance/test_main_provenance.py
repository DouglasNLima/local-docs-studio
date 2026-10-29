"""Mutation checks for failures that would silently broaden or break the boundary."""
import copy
from pathlib import Path
import tempfile
import unittest
import main_provenance as check


class ProvenanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scope = check.read_json(check.SCOPE)
        cls.rows = check.read_json(check.LEDGER)
        cls.names = {r['path'] for r in cls.rows}
        cls.hashes = {r['path']:r['sha256'] for r in cls.rows if r['path'] != check.LEDGER}
        cls.provenance = check.read_json(check.PROVENANCE)
        cls.pack = (check.ROOT / 'assets/vendor/THIRD-PARTY-NOTICES.txt').read_bytes()

    def test_exclusions_cannot_be_silently_removed(self):
        for boundary in ['R3', 'R4', 'R5', 'R6', 'R7']:
            with self.subTest(boundary=boundary):
                scope = copy.deepcopy(self.scope)
                scope['excludedBoundaries'].remove(boundary)
                with self.assertRaises(ValueError): check.validate_scope(scope, self.names)

    def test_vendor_artwork_and_examples_cannot_enter_grant(self):
        for path in ['assets/vendor/highlight-11.11.1.esm.js', 'icon.svg', 'assets/scripts/registries/content.js']:
            scope = copy.deepcopy(self.scope)
            scope['paths'] = sorted(scope['paths'] + [path])
            with self.subTest(path=path), self.assertRaises(ValueError): check.validate_scope(scope, self.names)

    def test_new_original_path_is_blocked_until_reviewed(self):
        with self.assertRaises(ValueError): check.validate_scope(self.scope, self.names | {'unreviewed.js'})

    def test_missing_path_or_stale_source_fails(self):
        with self.assertRaises(ValueError): check.validate_ledger(self.rows[:-1], self.names, self.hashes, set(self.scope['paths']))
        hashes = dict(self.hashes); hashes['README.md'] = '0' * 64
        with self.assertRaises(ValueError): check.validate_ledger(self.rows, self.names, hashes, set(self.scope['paths']))

    def test_vendor_or_original_notice_tampering_fails(self):
        hashes = dict(self.hashes); hashes['assets/vendor/highlight-11.11.1.esm.js'] = '0' * 64
        with self.assertRaises(ValueError): check.validate_notices(self.provenance, hashes, self.pack)
        with self.assertRaises(ValueError): check.validate_notices(self.provenance, self.hashes, self.pack + b'changed')
        provenance = copy.deepcopy(self.provenance); provenance['notices'][0]['offset'] += 1
        with self.assertRaises(ValueError): check.validate_notices(provenance, self.hashes, self.pack)

    def test_incomplete_provenance_cannot_be_called_complete(self):
        provenance = copy.deepcopy(self.provenance); provenance['completeEmbeddedClosure'] = True
        with self.assertRaises(ValueError): check.validate_notices(provenance, self.hashes, self.pack)

    def test_missing_or_escaping_relative_links_fail(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for text in ['[missing](missing.md)', '[escape](../outside.md)']:
                with self.subTest(text=text), self.assertRaises(ValueError): check.validate_links('README.md', text, root)


if __name__ == '__main__': unittest.main()
