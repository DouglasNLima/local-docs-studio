"""Focused byte-identity controls in isolated Windows Git repositories."""
import hashlib
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('proposal', Path(__file__).with_name('validate-proposal.py'))
proposal = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(proposal)
RAW = b'{\n  "status": "PROPOSED_NOT_APPROVED",\n  "ownerApproval": null\n}\n'
PIN = hashlib.sha256(RAW).hexdigest()


class CanonicalManifestTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='lens-canonical-manifest-')
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.path = self.root / 'candidate.json'
        self.git('init', '--quiet')
        self.git('config', 'core.autocrlf', 'false')
        self.git('config', 'core.safecrlf', 'false')
        self.path.write_bytes(RAW)
        self.git('add', 'candidate.json')
        self.commit()

    def git(self, *args):
        return subprocess.check_output(['git', *args], cwd=self.root, stderr=subprocess.PIPE)

    def commit(self):
        self.git('-c', 'user.name=Lens controls', '-c', 'user.email=controls@example.invalid',
                 '-c', 'commit.gpgsign=false', 'commit', '--quiet', '-m', 'Isolated manifest control')

    def read(self):
        return proposal.read_verified_manifest(self.root, 'HEAD', 'candidate.json', PIN)

    def test_lf_and_crlf_checkouts_share_exact_committed_identity(self):
        for autocrlf, working in [('false', RAW), ('true', RAW.replace(b'\n', b'\r\n'))]:
            with self.subTest(autocrlf=autocrlf):
                self.git('config', 'core.autocrlf', autocrlf)
                self.path.unlink()  # Materialise a fresh checkout, not Git's unchanged-file fast path.
                self.git('checkout-index', '--force', '--', 'candidate.json')
                self.assertEqual(self.path.read_bytes(), working)
                self.assertEqual(self.read(), RAW)
                self.assertEqual(hashlib.sha256(self.read()).hexdigest(), PIN)

    def test_semantically_modified_working_json_is_rejected(self):
        self.path.write_bytes(RAW.replace(b'null', b'true'))
        with self.assertRaisesRegex(ValueError, 'Working manifest differs'):
            self.read()

    def test_same_json_with_different_spacing_is_not_normalised(self):
        self.path.write_bytes(RAW.replace(b'  ', b'    '))
        with self.assertRaisesRegex(ValueError, 'Working manifest differs'):
            self.read()

    def test_missing_working_manifest_is_rejected(self):
        self.path.unlink()
        with self.assertRaisesRegex(ValueError, 'missing or substituted'):
            self.read()

    def test_substituted_working_manifest_is_rejected(self):
        self.path.write_bytes(b'x' * len(RAW))
        with self.assertRaisesRegex(ValueError, 'Working manifest differs'):
            self.read()

    def test_staged_substitution_is_rejected_even_with_original_working_bytes(self):
        self.path.write_bytes(RAW.replace(b'null', b'true'))
        self.git('add', 'candidate.json')
        self.path.write_bytes(RAW)
        with self.assertRaisesRegex(ValueError, 'Manifest index differs'):
            self.read()

    def test_changed_committed_manifest_is_rejected_by_original_pin(self):
        self.path.write_bytes(RAW.replace(b'null', b'true'))
        self.git('add', 'candidate.json'); self.commit()
        with self.assertRaisesRegex(ValueError, 'Canonical committed manifest bytes changed'):
            self.read()

    def test_missing_committed_manifest_is_rejected(self):
        self.git('rm', '--quiet', 'candidate.json'); self.commit()
        with self.assertRaisesRegex(ValueError, 'Committed manifest is missing'):
            self.read()


if __name__ == '__main__':
    unittest.main(verbosity=2)
