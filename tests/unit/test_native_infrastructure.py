"""Offline controls only: do not invoke the desktop probe or any Windows API."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[2]
MODULES = ROOT / 'scripts/native/qualification_hosted'
sys.path.insert(0, str(MODULES))
import eligibility as gate
import discover_desktop as discovery
import execute_locked as coordinator


def appearance(kind='NAMED'):
    return dict(flags=126, enabled=False, schemePointerWasNull=kind == 'NULL', schemeKind=kind,
                scheme={'NULL': None, 'EMPTY': '', 'NAMED': 'High Contrast Black'}[kind], schemeOwnership='BORROWED_OS',
                schemeCopyComplete=True, schemeReleaseAttempted=False, ownershipContract=gate.HC_OWNERSHIP_CONTRACT,
                colours={str(n): 0 for n in (2, 3, 5, 6, 7, 8, 9, 13, 14, 15, 16, 17, 18, 20, 26)},
                theme={'hresult': 0, 'file': 'normal.theme', 'colour': 'normal', 'size': 'normal'})


def observation(width=1920, height=1032, kind='NAMED'):
    rectangle = {'left': 0, 'top': 0, 'right': width, 'bottom': height}
    monitor = dict(device='DISPLAY1', primary=True, monitorPixels=dict(rectangle, bottom=height + 48), workPixels=rectangle,
                   scalePercent=100, currentMode={'width': width, 'height': height + 48}, enumeratedModes=[{'width': width, 'height': height + 48}], modesComplete=True)
    snapshot = dict(started=1, finished=2, primaryWorkPixels=rectangle,
                    dpi={'processAwareness': 1, 'threadAwareness': 1, 'system': 96}, monitors=[monitor],
                    desktop='Default', inputDesktop='Default', windowStation='WinSta0', sessionId=1,
                    activeConsoleSession=1, remoteSession=False, appearance=appearance(kind),
                    windowConstraintsPixels={'maximumTrackWidth': width + 16, 'maximumTrackHeight': height + 64})
    end = copy.deepcopy(snapshot); end.update(started=3, finished=4)
    return dict(schemaVersion=1, kind='READ_ONLY_WIN32_DESKTOP', processId=101, qualificationProcesses=0,
                windowCreation=False, wpfResourceAccess=False, inputChanges=False, displayChanges=False, appearanceChanges=False,
                assemblies=['System.Private.CoreLib', 'DesktopProbe'], before=snapshot, after=end)


class InfrastructureControls(unittest.TestCase):
    def both(self, data, mutate):
        mutate(data['before']); mutate(data['after']); return data

    def test_supported_geometry_still_cannot_authorise_appearance_or_campaign(self):
        result = gate.assess(observation())
        self.assertEqual(result['geometryEligibility'], 'ELIGIBLE')
        self.assertEqual(result['infrastructureEligibility'], 'BLOCKED')
        self.assertEqual(result['appearanceRestoration'], 'UNVERIFIED')
        self.assertFalse(result['campaignExecutionApproved'])
        self.assertEqual(result['collectionValidity'], 'NOT_RUN')
        self.assertEqual(result['originalCorrectness'], 'NOT_RUN')

    def test_observed_historical_geometry_is_rejected_before_v1(self):
        result = gate.assess(observation(1024, 720, 'NULL'))
        self.assertEqual(result['geometryEligibility'], 'INELIGIBLE')
        self.assertEqual(result['geometry']['predictedHarnessWidth'], 1024)
        self.assertEqual(result['geometry']['predictedHarnessHeight'], 720)
        launch = Mock()
        with self.assertRaisesRegex(ValueError, 'before V1'):
            gate.require_campaign_eligibility(observation(1024, 720))
            launch()
        launch.assert_not_called()

    def test_declared_large_screen_cannot_mask_small_work_area(self):
        data = observation(1024, 720)
        self.both(data, lambda s: s['monitors'][0].update(monitorPixels={'left': 0, 'top': 0, 'right': 1920, 'bottom': 1080}, currentMode={'width': 1920, 'height': 1080}))
        self.assertEqual(gate.assess(data)['geometryEligibility'], 'INELIGIBLE')

    def test_unaware_virtualised_or_scaled_dpi_is_inconclusive(self):
        for key, value in [('processAwareness', 0), ('threadAwareness', 0), ('system', 120)]:
            data = self.both(observation(), lambda s: s['dpi'].__setitem__(key, value))
            self.assertEqual(gate.assess(data)['geometryEligibility'], 'INCONCLUSIVE')
        data = self.both(observation(), lambda s: s['monitors'][0].__setitem__('scalePercent', 125))
        self.assertEqual(gate.assess(data)['geometryEligibility'], 'INCONCLUSIVE')

    def test_multiple_missing_or_nonprimary_monitors_are_not_summed(self):
        for monitors in ([], [dict(observation()['before']['monitors'][0], primary=False)], observation()['before']['monitors'] * 2):
            data = self.both(observation(), lambda s: s.__setitem__('monitors', monitors))
            self.assertEqual(gate.assess(data)['geometryEligibility'], 'INCONCLUSIVE')

    def test_rectangles_units_mode_and_tracking_limits_are_not_spoofed(self):
        changes = [lambda s: s['primaryWorkPixels'].__setitem__('right', float('nan')),
                   lambda s: s['primaryWorkPixels'].__setitem__('right', -1),
                   lambda s: s['monitors'][0]['currentMode'].__setitem__('width', 1),
                   lambda s: s['monitors'][0].__setitem__('workPixels', {'left': 0, 'top': 0, 'right': 1300, 'bottom': 800}),
                   lambda s: s['monitors'][0].__setitem__('modesComplete', False)]
        for change in changes:
            self.assertEqual(gate.assess(self.both(observation(), change))['geometryEligibility'], 'INCONCLUSIVE')
        data = self.both(observation(), lambda s: s['windowConstraintsPixels'].__setitem__('maximumTrackWidth', 1024))
        self.assertEqual(gate.assess(data)['geometryEligibility'], 'INELIGIBLE')

    def test_session_desktop_or_between_read_drift_stops(self):
        for key, value in [('sessionId', 0), ('inputDesktop', 'Winlogon'), ('windowStation', 'ServiceStation')]:
            data = self.both(observation(), lambda s: s.__setitem__(key, value))
            self.assertEqual(gate.assess(data)['geometryEligibility'], 'INCONCLUSIVE')
        data = observation(); data['after']['appearance']['scheme'] = 'changed'
        self.assertEqual(gate.assess(data)['geometryEligibility'], 'INCONCLUSIVE')

    def test_incomplete_wrong_pid_or_ui_mutating_discovery_is_never_accepted(self):
        for key, value in [('processId', 0), ('processId', True), ('schemaVersion', 2), ('schemaVersion', True), ('qualificationProcesses', 1), ('qualificationProcesses', False),
                           ('windowCreation', True), ('wpfResourceAccess', True), ('displayChanges', True),
                           ('appearanceChanges', True), ('inputChanges', True), ('assemblies', ['PresentationFramework'])]:
            self.assertEqual(gate.assess(dict(observation(), **{key: value}))['geometryEligibility'], 'INCONCLUSIVE')
        for key in observation():
            data = observation(); del data[key]
            self.assertEqual(gate.assess(data)['infrastructureEligibility'], 'BLOCKED')

    def test_null_empty_and_named_schemes_remain_distinct(self):
        for kind in ('NULL', 'EMPTY', 'NAMED'):
            original = appearance(kind)
            self.assertTrue(gate.exact_restoration(original, copy.deepcopy(original)))
            for other in ('NULL', 'EMPTY', 'NAMED'):
                if other != kind: self.assertFalse(gate.exact_restoration(original, appearance(other)))
            proposal = gate.restoration_proposal(original)
            self.assertFalse(proposal['executePermitted'])
            self.assertEqual(proposal['request']['schemeKind'], kind)
            self.assertEqual(proposal['request']['flags'], 126)
            self.assertEqual(proposal['request']['notificationFlags'], 2)

    def test_enabled_bit_alone_never_proves_restoration(self):
        initial = appearance('EMPTY'); final = appearance('NAMED')
        self.assertFalse(initial['enabled']); self.assertFalse(final['enabled'])
        self.assertFalse(gate.exact_restoration(initial, final))
        for field, value in [('flags', 62), ('theme', {'hresult': 0, 'file': 'other.theme'}), ('colours', {'13': 1})]:
            self.assertFalse(gate.exact_restoration(appearance(), dict(appearance(), **{field: value})))

    def test_failed_copy_or_wrong_ownership_or_inconsistent_pointer_is_not_safe(self):
        for key, value in [('getBufferReleased', True), ('schemeOwnership', 'UNKNOWN'), ('schemeReleaseAttempted', True),
                           ('schemeCopyComplete', False), ('ownershipContract', 'moving'), ('schemePointerWasNull', True),
                           ('enabled', True), ('scheme', None), ('flags', True)]:
            self.assertFalse(gate.exact_restoration(appearance(), dict(appearance(), **{key: value})))
        for key in ('schemeOwnership', 'schemeCopyComplete', 'schemeReleaseAttempted', 'ownershipContract'):
            incomplete = appearance(); del incomplete[key]
            self.assertFalse(gate.exact_restoration(appearance(), incomplete))
        for scheme in ('\ud800', 'A\0B', '🎨' * 16384):
            malformed = dict(appearance(), schemeKind='NAMED', scheme=scheme)
            self.assertFalse(gate.exact_restoration(malformed, malformed))

    def process(self):
        return dict(processId=17, jobTreeEmpty=True, cleanupVerified=True, restored=True, supervisionFailure=None, exitCode=1)

    def scope(self):
        return dict(processId=17, width=1280, height=800, zoom=100, genericObserverHooks=False)

    def test_shared_failure_stops_next_cell_instead_of_becoming_product_failure(self):
        self.assertEqual(gate.continuation(self.process(), None, 17), 'STOP_SHARED_SCOPE_UNVERIFIED')
        for key, value in [('processId', 18), ('width', 1024), ('height', 720), ('zoom', 99), ('genericObserverHooks', True), ('width', float('nan'))]:
            self.assertEqual(gate.continuation(self.process(), dict(self.scope(), **{key: value}), 17), 'STOP_SHARED_SCOPE_UNVERIFIED')
        for key, value in [('processId', 18), ('processId', True), ('jobTreeEmpty', None), ('cleanupVerified', False), ('restored', False), ('supervisionFailure', 'TIMEOUT'), ('exitCode', None), ('exitCode', True), ('exitCode', '0')]:
            self.assertEqual(gate.continuation(dict(self.process(), **{key: value}), self.scope(), 17), 'STOP_INFRASTRUCTURE')
        self.assertEqual(gate.continuation(self.process(), self.scope(), 17), 'REVIEW_PRODUCT_RESULT_INDEPENDENTLY')
        self.assertEqual(self.process()['exitCode'], 1)

    def test_missing_duplicate_truncated_or_nonfinite_json_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / 'observation.json'
            with self.assertRaises(FileNotFoundError): discovery.read_json(path)
            for content in ('{"status": "FAIL", "status": "PASS"}', '{"value": NaN}', '{"value": Infinity}', '{"before":'):
                path.write_text(content)
                with self.assertRaises(ValueError): discovery.read_json(path)
        for key in self.process():
            process = self.process(); del process[key]
            self.assertEqual(gate.continuation(process, self.scope(), 17), 'STOP_INFRASTRUCTURE')

    def test_spent_coordinator_cannot_reach_any_side_effect(self):
        with patch.object(coordinator, 'read_json') as read, patch.object(coordinator, 'verify_restored') as verify:
            with self.assertRaisesRegex(ValueError, 'NOT_AUTHORISED'): coordinator.execute(Path('unused'), Path('unused'), 'R01')
            read.assert_not_called(); verify.assert_not_called()

    def test_local_discovery_cli_stops_before_build_or_observation(self):
        with tempfile.TemporaryDirectory() as temporary:
            env = {k: v for k, v in os.environ.items() if not k.startswith(('GITHUB_', 'RUNNER_'))}
            output = Path(temporary) / 'never-created'
            result = subprocess.run([sys.executable, str(MODULES / 'discover_desktop.py'), '--output', str(output)], env=env, capture_output=True, text=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse(output.exists())
            self.assertIn('discovery guard failed', result.stderr)

    def test_known_native_buffer_failure_blocks_even_guarded_discovery_before_side_effects(self):
        with tempfile.TemporaryDirectory() as temporary, patch.object(discovery, 'guard', return_value='a' * 40), \
                patch.object(discovery.subprocess, 'Popen') as launch, patch.object(discovery.subprocess, 'check_output') as read:
            output = Path(temporary) / 'never-created'
            with patch.object(sys, 'argv', ['discover_desktop.py', '--output', str(output)]):
                with self.assertRaisesRegex(ValueError, 'buffer ownership is unqualified'): discovery.main()
            self.assertFalse(output.exists()); launch.assert_not_called(); read.assert_not_called()

    def test_read_only_native_failure_retains_exact_exit_pid_and_no_retry(self):
        for code in (0, 2, 0xc0000374):
            with tempfile.TemporaryDirectory() as temporary, patch.object(discovery.subprocess, 'Popen') as launch:
                child = launch.return_value; child.pid = 501; child.returncode = code
                if code:
                    with self.assertRaisesRegex(ValueError, 'exact child result'):
                        discovery.collect_probe(Path('never-launched.exe'), {}, Path(temporary))
                else:
                    self.assertEqual(discovery.collect_probe(Path('never-launched.exe'), {}, Path(temporary)), 501)
                record = discovery.read_json(Path(temporary) / 'child-process.json')
                self.assertEqual(record['exitCode'], code)
                self.assertEqual(record['exitCodeHex'], format(code, '08x'))
                self.assertEqual(record['processId'], 501)
                self.assertFalse(record['qualificationProcess'])
                self.assertEqual(record['retryCount'], 0)
                launch.assert_called_once(); child.wait.assert_called_once_with(timeout=30)
                child.kill.assert_not_called()

    def test_read_only_timeout_retains_failure_after_killing_only_the_mock_child(self):
        with tempfile.TemporaryDirectory() as temporary, patch.object(discovery.subprocess, 'Popen') as launch:
            child = launch.return_value; child.pid = 502; child.returncode = -9
            child.wait.side_effect = [subprocess.TimeoutExpired('mock', 30), -9]
            with self.assertRaisesRegex(ValueError, 'exact child result'):
                discovery.collect_probe(Path('never-launched.exe'), {}, Path(temporary))
            record = discovery.read_json(Path(temporary) / 'child-process.json')
            self.assertTrue(record['timedOut']); self.assertEqual(record['exitCodeHex'], 'fffffff7')
            child.kill.assert_called_once(); launch.assert_called_once()

    def test_read_only_self_report_must_match_supervised_child_and_exact_binary(self):
        source = 'a' * 40
        binaries = {'DesktopProbe.exe': 'host', 'DesktopProbe.dll': 'assembly'}
        record = dict(processId=503, runtimeVersion='10.0.14', coreSha256=discovery.CORE_SHA256,
                      processSha256='host', assemblySha256='assembly', sourceRevision='1.0.0+' + source)
        discovery.verify_probe(record, binaries, source, 503)
        for key, value in [('processId', 504), ('processId', True), ('runtimeVersion', '10.0.15'),
                           ('coreSha256', 'wrong'), ('processSha256', 'wrong'), ('assemblySha256', 'wrong'), ('sourceRevision', 'moving')]:
            with self.assertRaises(ValueError): discovery.verify_probe(dict(record, **{key: value}), binaries, source, 503)

    def test_win32_imports_are_getters_only_and_no_ui_framework_is_referenced(self):
        source = (MODULES / 'desktop_probe/Program.cs').read_text()
        externs = set(re.findall(r'extern \w+ (\w+)\(', source))
        self.assertEqual(externs, {'SystemParametersInfoRect', 'SystemParametersInfoContrast', 'EnumDisplayMonitors',
            'GetMonitorInfoW', 'EnumDisplayDevicesW', 'EnumDisplaySettingsW', 'GetProcessDpiAwareness', 'GetScaleFactorForMonitor',
            'GetThreadDpiAwarenessContext', 'GetAwarenessFromDpiAwarenessContext', 'GetDpiForSystem', 'GetSystemMetrics',
            'GetSysColor', 'GetThreadDesktop', 'GetProcessWindowStation', 'OpenInputDesktop', 'CloseDesktop',
            'GetUserObjectInformationW', 'GetCurrentThreadId', 'ProcessIdToSessionId', 'WTSGetActiveConsoleSessionId', 'GetCurrentThemeName'})
        self.assertEqual(source.count('SystemParametersInfoContrast(GetHighContrast,'), 1)
        self.assertIn('GetHighContrast = 0x42', source)
        for name in ('SendInput(', 'ChangeDisplaySettings', 'SetProcessDpi', 'SetThreadDpi', 'CreateWindow', 'SystemParametersInfoContrast(0x43', 'HCF_OPTION_NOTHEMECHANGE'):
            self.assertNotIn(name, source)
        reader = (MODULES / 'desktop_probe/HighContrastReader.cs').read_text()
        for name in ('LocalFree(', 'FreeHGlobal(', 'FreeCoTaskMem(', 'SafeHandle'):
            self.assertNotIn(name, source + reader)
        self.assertIn('HighContrastReader.cs', (MODULES / 'discover_desktop.py').read_text())
        project = (MODULES / 'desktop_probe/DesktopProbe.csproj').read_text()
        for name in ('UseWPF', 'UseWindowsForms', 'PackageReference', 'ProjectReference'):
            self.assertNotIn(name, project)
        self.assertIn('<RollForward>Disable</RollForward>', project)

    def test_read_only_dispatch_cannot_schedule_an_original_or_qualification_job(self):
        workflow = (ROOT / '.github/workflows/native-ux-performance.yml').read_text()
        original = workflow.split('  infrastructure-discovery:\n')[0]
        self.assertEqual(original.count('inputs.infrastructure_discovery_only != true'), 5)
        job = workflow.split('  infrastructure-discovery:\n')[1]
        for text in ('inputs.infrastructure_discovery_only == true', "inputs.bounded_protocol == 'none'",
                     'github.run_attempt == 1', 'persist-credentials: false', 'contents: read', 'if: always()'):
            self.assertIn(text, job)
        for text in ('GH_TOKEN', 'secrets.', 'execute_locked', 'Measure-NativeUx', 'setup-dotnet', 'strategy:'):
            self.assertNotIn(text, job)


if __name__ == '__main__': unittest.main()
