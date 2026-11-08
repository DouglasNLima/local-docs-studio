"""Fail-closed controls for simulated-buffer evidence, without any native calls."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

SPEC = importlib.util.spec_from_file_location('buffer_controls', Path(__file__).parent / 'native_buffer/run_controls.py')
controls = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(controls)


class BufferEvidenceControls(unittest.TestCase):
    def setUp(self):
        self.native = dict(nativeChecks=21, pointerBytes=8, structureBytes=16, offsets=[0, 4, 8], wcharBytes=2,
                           boolBytes=4, allocations=2, ownerReleases=2, borrowerReleaseDenials=1,
                           activeAllocations=0, actualHighContrastCalls=0)
        self.managed = dict(status='PASS_CONTROLLED_SIMULATION_ONLY', tests=20, passed=[str(n) for n in range(20)],
                            runtime='10.0.12', architecture='X64', coreSha256='a' * 64, allocations=50, ownerReleases=50,
                            borrowerReleaseDenials=2, activeAllocations=0, actualHighContrastCalls=0, uiCalls=0,
                            liveCertification=False, ownershipContract=controls.HC_OWNERSHIP_CONTRACT,
                            assemblies=['System.Private.CoreLib', 'NativeBufferTests'])
        self.imports = ['    KERNEL32.dll\n    api-ms-win-crt-runtime-l1-1-0.dll\n'] * 2

    def validate(self):
        controls.validate_reports(self.native, self.managed, self.imports)

    def test_complete_simulated_evidence_is_not_live_certification(self):
        self.validate()
        self.assertFalse(self.managed['liveCertification'])

    def test_every_missing_field_rejects(self):
        for record in (self.native, self.managed):
            for key in list(record):
                value = record.pop(key)
                with self.subTest(key=key), self.assertRaises((ValueError, KeyError, TypeError)): self.validate()
                record[key] = value

    def test_native_abi_release_and_execution_contradictions_reject(self):
        for key, value in [('nativeChecks', 20), ('pointerBytes', 4), ('structureBytes', 12), ('offsets', [0, 4, 4]),
                           ('wcharBytes', 1), ('boolBytes', 1), ('ownerReleases', 1), ('activeAllocations', 1),
                           ('actualHighContrastCalls', 1), ('actualHighContrastCalls', False)]:
            previous = self.native[key]; self.native[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError): self.validate()
            self.native[key] = previous

    def test_managed_truncated_duplicate_or_contradictory_evidence_rejects(self):
        for key, value in [('tests', 19), ('passed', ['repeated'] * 20), ('passed', self.managed['passed'][:-1]),
                           ('runtime', '10.0.14'), ('architecture', 'X86'), ('coreSha256', 'missing'),
                           ('ownerReleases', 49), ('activeAllocations', 1), ('uiCalls', 1), ('uiCalls', False),
                           ('liveCertification', True), ('ownershipContract', 'unknown'), ('status', 'FAIL'),
                           ('assemblies', ['PresentationFramework']), ('assemblies', []),
                           ('assemblies', ['System.Private.CoreLib']),
                           ('assemblies', self.managed['assemblies'] * 2)]:
            previous = self.managed[key]; self.managed[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError): self.validate()
            self.managed[key] = previous

    def test_missing_ui_or_dynamic_imports_reject_before_execution(self):
        for reports in ([], ['no imports'] * 2, self.imports + self.imports,
                        ['    USER32.dll\n'] * 2, ['    KERNEL32.dll\n    LoadLibraryW\n'] * 2,
                        ['    KERNEL32.dll\n    GetProcAddress\n'] * 2):
            with self.subTest(reports=reports), self.assertRaises(ValueError): controls.validate_imports(reports)

    def test_duplicate_truncated_and_nonfinite_raw_json_rejects(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'evidence.json'
            with self.assertRaises(FileNotFoundError): controls.read_json(path)
            for content in ('{"status":"FAIL","status":"PASS"}', '{"x":', '{"x":NaN}'):
                path.write_text(content)
                with self.assertRaises(ValueError): controls.read_json(path)


if __name__ == '__main__': unittest.main()
