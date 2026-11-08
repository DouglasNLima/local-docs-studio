"""Pure offline eligibility/continuation guards. These functions never call Windows."""
import copy
import math


def require(value, message):
    if not value:
        raise ValueError(message)


def rectangle(value):
    require(set(value) == {'left', 'top', 'right', 'bottom'}, 'Incomplete rectangle')
    require(all(type(n) is int for n in value.values()), 'Non-integer physical rectangle')
    width, height = value['right'] - value['left'], value['bottom'] - value['top']
    require(0 < width <= 32768 and 0 < height <= 32768, 'Invalid physical rectangle')
    return width, height


def appearance_state(value):
    require(type(value['flags']) is int and 0 <= value['flags'] <= 0xffffffff, 'Invalid raw HC flags')
    require(type(value['enabled']) is bool and value['enabled'] == bool(value['flags'] & 1), 'Inconsistent HC-enabled state')
    scheme = value['scheme']
    if value['schemeKind'] == 'NULL':
        require(value['schemePointerWasNull'] is True and scheme is None, 'NULL scheme was normalised')
    elif value['schemeKind'] == 'EMPTY':
        require(value['schemePointerWasNull'] is False and scheme == '', 'Empty scheme was normalised')
    elif value['schemeKind'] == 'NAMED':
        require(value['schemePointerWasNull'] is False and isinstance(scheme, str) and 0 < len(scheme) < 32768, 'Named scheme is incomplete')
    else:
        raise ValueError('Unknown scheme kind')
    require(value['getBufferReleased'] is True, 'GET buffer ownership is unverified')
    require(type(value['theme']['hresult']) is int and value['theme']['hresult'] == 0
            and all(isinstance(value['theme'][k], str) for k in ('file', 'colour', 'size')), 'Active theme observation unavailable')
    require(set(value['colours']) == set(map(str, (2, 3, 5, 6, 7, 8, 9, 13, 14, 15, 16, 17, 18, 20, 26))), 'Incomplete Win32 palette')
    require(all(type(c) is int and 0 <= c <= 0xffffff for c in value['colours'].values()), 'Invalid system colour')
    return copy.deepcopy(value)


def exact_restoration(before, after):
    """No NULL/empty/named equivalence, flag masking or theme/palette substitution."""
    try:
        return appearance_state(before) == appearance_state(after)
    except (ValueError, KeyError, TypeError):
        return False


def restoration_proposal(original):
    state = appearance_state(original)
    return {'status': 'PROPOSED_NOT_LIVE_VALIDATED', 'original': state,
            'request': {'api': 'SPI_SETHIGHCONTRAST', 'flags': state['flags'],
                        'schemeKind': state['schemeKind'], 'scheme': state['scheme'],
                        'notificationFlags': 2, 'registryPersistence': False},
            'unresolved': ['NULL/empty restoration has no documented round-trip guarantee'] if state['schemeKind'] != 'NAMED' else ['Named-scheme restoration still requires an authorised live round trip'],
            'executePermitted': False}


def assess(observation):
    reasons = []
    geometry = 'INCONCLUSIVE'
    details = {}
    try:
        require(type(observation['schemaVersion']) is int and observation['schemaVersion'] == 1
                and observation['kind'] == 'READ_ONLY_WIN32_DESKTOP', 'Wrong discovery schema')
        require(type(observation['processId']) is int and observation['processId'] > 0, 'Invalid discovery PID')
        require(type(observation['qualificationProcesses']) is int and observation['qualificationProcesses'] == 0 and all(observation[k] is False for k in
                ('windowCreation', 'wpfResourceAccess', 'inputChanges', 'displayChanges', 'appearanceChanges')), 'Discovery exceeded read-only scope')
        require(not set(observation['assemblies']) & {'PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Windows.Forms'}, 'UI assembly loaded')
        before, after = observation['before'], observation['after']
        # Exclude only observation clocks; every measured desktop/appearance field must agree.
        require({k: v for k, v in before.items() if k not in ('started', 'finished')} ==
                {k: v for k, v in after.items() if k not in ('started', 'finished')}, 'Desktop changed between fixed reads')
        require(all(type(s[k]) is int for s in (before, after) for k in ('started', 'finished'))
                and 0 < before['started'] <= before['finished'] <= after['started'] <= after['finished'], 'Invalid discovery chronology')
        require(before['dpi']['processAwareness'] == before['dpi']['threadAwareness'] == 1, 'System-aware declaration was not observed')
        require(before['desktop'] == before['inputDesktop'] == 'Default' and before['windowStation'] == 'WinSta0'
                and before['sessionId'] > 0, 'Interactive desktop resource unavailable')
        monitors = before['monitors']
        require(isinstance(monitors, list) and len(monitors) == 1 and monitors[0]['primary'] is True, 'Single primary monitor mapping is unqualified')
        monitor = monitors[0]
        require(monitor['modesComplete'] is True and 0 < len(monitor['enumeratedModes']) <= 256, 'Incomplete mode enumeration')
        require(before['dpi']['system'] == 96 and monitor['scalePercent'] == 100, 'DPI-to-WPF-unit mapping outside the proposed 96-DPI diagnostic envelope')
        width, height = rectangle(before['primaryWorkPixels'])
        require(before['primaryWorkPixels'] == monitor['workPixels'], 'SPI/monitor work areas disagree')
        monitor_width, monitor_height = rectangle(monitor['monitorPixels'])
        require((monitor_width, monitor_height) == (monitor['currentMode']['width'], monitor['currentMode']['height']), 'Physical mode and monitor bounds disagree')
        require(monitor['monitorPixels']['left'] <= monitor['workPixels']['left'] < monitor['workPixels']['right'] <= monitor['monitorPixels']['right']
                and monitor['monitorPixels']['top'] <= monitor['workPixels']['top'] < monitor['workPixels']['bottom'] <= monitor['monitorPixels']['bottom'], 'Work area escapes monitor')
        constraints = before['windowConstraintsPixels']
        require(all(type(constraints[k]) is int and constraints[k] > 0 for k in ('maximumTrackWidth', 'maximumTrackHeight')), 'Invalid window tracking limits')
        details = {'workAreaPixels': [width, height], 'requiredWindowDips': [1280, 800], 'applicationZoomPercent': 100,
                   'predictedHarnessWidth': min(1280, width), 'predictedHarnessHeight': min(800, height),
                   'mapping': '96 DPI only; no WPF window created or measured'}
        geometry = 'ELIGIBLE' if width >= 1280 and height >= 800 and constraints['maximumTrackWidth'] >= 1280 and constraints['maximumTrackHeight'] >= 800 else 'INELIGIBLE'
        if geometry != 'ELIGIBLE': reasons.append('REQUIRED_GEOMETRY_UNAVAILABLE')
        state = appearance_state(before['appearance'])
        require(not state['enabled'], 'Normal initial appearance required')
        if state['schemeKind'] != 'NAMED': reasons.append('UNNAMED_SCHEME_RESTORATION_UNPROVEN')
    except (ValueError, KeyError, TypeError) as error:
        reasons.append('READ_ONLY_DISCOVERY_INCOMPLETE_OR_UNQUALIFIED: ' + str(error))
    reasons.append('EXACT_APPEARANCE_RESTORATION_NOT_LIVE_VALIDATED')
    return {'schemaVersion': 1, 'infrastructureEligibility': 'BLOCKED', 'geometryEligibility': geometry,
            'geometry': details, 'appearanceRestoration': 'UNVERIFIED', 'reasons': reasons,
            'collectionValidity': 'NOT_RUN', 'originalCorrectness': 'NOT_RUN',
            'presentationOrder': 'INCONCLUSIVE', 'status': 'NOT_AUTHORISED',
            'campaignExecutionApproved': False, 'qualificationProcesses': 0,
            'processCeilings': {'R01': 4, 'R02': 18, 'calibration': 0},
            'pr76Integration': 'NO-GO', 'policyAcceptance': 'NOT_ACTIVATED'}


def require_campaign_eligibility(observation):
    verdict = assess(observation)
    # Read-only evidence cannot acquire the missing live-restoration validation or authority.
    require(verdict['infrastructureEligibility'] == 'ELIGIBLE' and verdict['campaignExecutionApproved'] is True,
            'BLOCKED before V1: ' + '; '.join(verdict['reasons']))
    return verdict


def continuation(process, scope, expected_pid):
    """Supplemental fail-fast rule: an exit code alone cannot classify product failure.

    No real campaign uses an amended launcher in this continuation. This pure gate
    is ready for integration only with fresh reviewed source/authority.
    """
    if (not isinstance(process, dict) or type(process.get('processId')) is not int
            or process.get('processId') != expected_pid or type(expected_pid) is not int or expected_pid <= 0
            or process.get('jobTreeEmpty') is not True or process.get('cleanupVerified') is not True or process.get('restored') is not True
            or 'supervisionFailure' not in process or process['supervisionFailure'] is not None
            or type(process.get('exitCode')) is not int):
        return 'STOP_INFRASTRUCTURE'
    if not isinstance(scope, dict) or type(scope.get('processId')) is not int or scope.get('processId') != expected_pid or any(
            type(scope.get(k)) not in (int, float) or not math.isfinite(scope[k]) or scope[k] != v
            for k, v in [('width', 1280), ('height', 800), ('zoom', 100)]) or scope.get('genericObserverHooks') is not False:
        return 'STOP_SHARED_SCOPE_UNVERIFIED'
    return 'REVIEW_PRODUCT_RESULT_INDEPENDENTLY'
