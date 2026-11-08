"""Fail-closed offline evidence audit. No capture, appearance changes or pixel-dependent retries."""
import argparse
import math
from pathlib import Path
import re
import sys

from qualification import cells, safe_relative, strict_data, strict_json, verify_file
from png import inspect as inspect_png
from sources import CANDIDATE, PREPARATION, json_new, sha

BOUNDARIES = ['primary-find', 'primary-find-all-readers', 'secondary-find', 'secondary-find-all-readers',
              'returned-primary', 'rapid-switch', 'closed-comparison']


def rgb(value):
    if not isinstance(value, str) or not re.fullmatch(r'#[0-9A-Fa-f]{6}|#FF[0-9A-Fa-f]{6}', value):
        raise ValueError('An opaque #RRGGBB or #FFRRGGBB colour is required')
    return tuple(int(value[-6:][i:i + 2], 16) for i in (0, 2, 4))


def contrast(first, second):
    def luminance(value):
        channels = [v / 255 for v in rgb(value)]
        linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in channels]
        return sum(a * b for a, b in zip(linear, (0.2126, 0.7152, 0.0722)))
    a, b = luminance(first), luminance(second)
    return (max(a, b) + 0.05) / (min(a, b) + 0.05)


def presentation(state, required, allow_hidden_secondary=False):
    """Preserve #76 checkPresentation assertions, adding finite/positive geometry checks."""
    errors = []
    if not state.get('previewStatus') or state.get('previewStatus') != state.get('statusText') or state.get('statusText') != state.get('statusUia'):
        errors.append('STATUS_MODEL_WPF_UIA_MISMATCH')
    for name in ('primary', 'secondary'):
        reader = state[name]
        if not reader['isVisible']:
            if name == 'primary' or not allow_hidden_secondary: errors.append('REQUIRED_READER_HIDDEN')
            continue  # Secondary is hidden at the original closed-comparison boundary.
        if name not in required and reader['itemStatus'] != 'Ready': continue
        if not all(reader.get(key) is True for key in ('currentOwner', 'attachedMatches', 'isMeasureValid', 'isArrangeValid')) or reader['itemStatus'] != 'Ready':
            errors.append(name + ':READY_OWNERSHIP_GEOMETRY')
        if not reader.get('owner') or reader['owner'] != reader.get('expectedOwner'):
            errors.append(name + ':WRONG_OWNER')
        if reader.get('source') is None or not any(r.get('meaningful') and positive_rectangles(r.get('rectangles')) for r in reader.get('textPattern') or []):
            errors.append(name + ':VISIBLE_MAPPED_TEXT_MISSING')
        if reader['mode'] not in ('Page', 'TwoPage'): errors.append(name + ':NOT_PAGES')
        if reader['pageNumber'] != reader['nativePage'] or reader['pageCount'] != reader['nativeCount'] or reader['pageText'] != f"Page {reader['pageNumber']} of {reader['pageCount']}" or reader['pageUia'] != reader['pageText']:
            errors.append(name + ':PAGE_COUNTER_MISMATCH')
    return errors


def positive_rectangles(values):
    return isinstance(values, list) and len(values) >= 4 and len(values) % 4 == 0 and all(isinstance(v, (int, float)) and math.isfinite(v) for v in values) and all(values[i] > 0 and values[i + 1] > 0 for i in range(2, len(values), 4))


def capture_order(frame, trace, traced):
    """Only a software observation ordering claim; never a presentation fence."""
    if not traced: return 'INCONCLUSIVE'
    start, end = frame['pixelsStarted'], frame['pixelsFinished']
    if not (frame['before']['finished'] <= frame['started'] <= frame['dwmReturned'] <= start <= end <= frame['after']['tick']): return 'INCONCLUSIVE'
    mutations = [e for e in trace if e.get('boundary', '').startswith(('wpf/', 'model/', 'anchor/')) and e['tick'] <= end]
    if not mutations or any(start <= e['tick'] <= end for e in mutations): return 'INCONCLUSIVE'
    last = max(e['tick'] for e in mutations)
    commits = [e for e in trace if e.get('boundary') == 'media/CommittingBatch' and last < e['tick'] <= start]
    if not commits: return 'INCONCLUSIVE'
    commit = commits[-1]
    for name in frame['completedReaders']:
        before, after = frame['before'][name], frame['after'][name]
        if not before['isVisible'] and not after['isVisible']: continue
        observed = commit.get(name, {})
        if observed.get('owner') != before['owner'] or before['owner'] != after['owner'] or observed.get('current') is not True:
            return 'INCONCLUSIVE'
    return 'SUPPORTED_SOFTWARE_ORDER_ONLY'


def validate_trace(lines, metadata):
    if not isinstance(lines, list) or not lines or len(lines) > 4000 or metadata.get('recordOverflow') is not False or metadata.get('byteOverflow') is not False or metadata.get('written') != len(lines):
        raise ValueError('Incomplete, empty or overflowed trace')
    ticks = [x['tick'] for x in lines]
    if not all(isinstance(t, int) and t > 0 for t in ticks) or ticks != sorted(ticks) or len({repr(x) for x in lines}) != len(lines):
        raise ValueError('Duplicated or inconsistent trace chronology')
    if lines[0].get('boundary') != 'subscribed': raise ValueError('Missing trace subscription boundary')


def validate_notifications(record, pid):
    if record.get('processId') != pid or record.get('overflow') is not False or record.get('error') is not None:
        raise ValueError('Wrong-PID/overflowed native notification record')
    events = record.get('events')
    if not isinstance(events, list) or not events or len(events) > 1024 or record['started'] <= 0 or record['finished'] < record['started']:
        raise ValueError('Missing/incomplete real notification chronology')
    ticks = [event['tick'] for event in events]
    if ticks != sorted(ticks) or any(e.get('processId') != pid or e.get('message') not in (0x1a, 0x31a, 0x320) or
        not record['started'] <= e['tick'] <= record['finished'] for e in events):
        raise ValueError('Mismatched native notification identity/order')


def r01_record(row, process, identity, summary, captures, trace=None, metadata=None, reviews=None):
    collection, correctness, attribution = [], [], []
    if summary.get('functional') != 'PASS' or summary.get('sourceIntegrity') != 'PASS': correctness.append('ORIGINAL_FUNCTIONAL_OR_SOURCE_FAIL')
    if summary.get('scenario') != 'pixels' or summary.get('mode') != 'Pages' or summary.get('target') != 'primary' or summary.get('instrumented') is not row['trace']:
        collection.append('WRONG_JOURNEY')
    if identity.get('iterations') != 1 or identity.get('fixture') != 'mixed' or identity.get('highContrast') != (row['appearance'] == 'high-contrast') or identity.get('genericObserverHooks') is not False:
        collection.append('WRONG_SCOPE_OR_HOOKS')
    expected = [(name, frame) for name in BOUNDARIES for frame in range(3)]
    if [(c['action'], c['frameIndex']) for c in captures] != expected: collection.append('MISSING_DUPLICATED_OR_EXTRA_FRAME')
    if len({c['file'] for c in captures}) != len(captures): collection.append('DUPLICATED_CAPTURE_FILE')
    if row['trace']:
        try: validate_trace(trace, metadata)
        except (ValueError, KeyError, TypeError): collection.append('INVALID_TRACE'); trace = []
        if metadata and metadata.get('verdict', {}).get('staleReady', 0): correctness.append('STALE_READY_TRACE')
    elif trace is not None or metadata is not None or any(c.get('compositionBefore') is not None or c.get('compositionAfter') is not None or c.get('committed') is not None for c in captures):
        collection.append('TRACE_HOOKS_IN_DISABLED_CELL')
    frequency = identity.get('stopwatchFrequency', 0)
    if not isinstance(frequency, int) or frequency <= 0: collection.append('INVALID_QPC_FREQUENCY')
    review_map = {}
    for item in reviews or []:
        if item['file'] in review_map: collection.append('DUPLICATED_PIXEL_REVIEW')
        review_map[item['file']] = item
    missing_review = False
    for frame in captures:
        required = ['primary'] if frame['action'] in ('primary-find', 'secondary-find') else ['primary', 'secondary']
        if frame.get('completedReaders') != required: collection.append('WRONG_REQUIRED_READERS')
        times = [frame.get('observationStarted'), frame['before'].get('tick'), frame['before'].get('finished'), frame.get('started'), frame.get('dwmReturned'),
                 frame.get('pixelsStarted'), frame.get('pixelsFinished'), frame['after'].get('tick'), frame['after'].get('finished')]
        if not all(isinstance(t, int) and t >= 0 for t in times) or times != sorted(times): collection.append('INVALID_CAPTURE_CHRONOLOGY')
        if frame.get('observationDeadlineSeconds') != 30 or frequency <= 0 or not 0 <= (frame['pixelsFinished'] - frame['observationStarted']) / max(frequency, 1) <= 30:
            collection.append('OBSERVATION_BOUND')
        for name in ('before', 'after'): correctness.extend(presentation(frame[name], required, frame['action'] == 'closed-comparison'))
        for name in required:
            if frame['before'][name]['owner'] != frame['after'][name]['owner'] or frame['before'][name]['source'] != frame['after'][name]['source']:
                correctness.append('OWNERSHIP_OR_SOURCE_CHANGED_DURING_CAPTURE')
        regions = frame['regions']
        if len(regions) > 3 or len({r['name'] for r in regions}) != len(regions) or not any(r['name'] == 'status' for r in regions): collection.append('INVALID_CROP_SET')
        for r in regions:
            if not positive_rectangles([r['x'], r['y'], r['width'], r['height']]) or r['x'] < 0 or r['y'] < 0: collection.append('INVALID_CROP_GEOMETRY')
        review = review_map.get(frame['file'])
        if not review: missing_review = True
        elif review.get('pngSha256', '').lower() != process.get('files', {}).get(frame['file'], '').lower() or review.get('processId') != identity['processId'] or not review.get('reviewer'):
            collection.append('PIXEL_REVIEW_IDENTITY_MISMATCH')
        else:
            visible = [name for name in required if frame['before'][name]['isVisible']]
            if review.get('requiredReaders') != visible: collection.append('PIXEL_REVIEW_SCOPE_MISMATCH')
            # OCR equality alone is insufficient for current source, clipped text or ambiguous pixels.
            for field in ('currentSourceVisible', 'requiredLabelsCorrect', 'selectionCorrect'):
                if review.get(field) is False: correctness.append('VISIBLE_' + field.upper() + '_FAIL')
                elif review.get(field) is not True: missing_review = True
        attribution.append(capture_order(frame, trace or [], row['trace']))
    if len(review_map) > len(captures): collection.append('EXTRA_PIXEL_REVIEW')
    return {'collection': 'INVALID' if collection else 'VALID', 'existingCorrectness': 'FAIL' if correctness else 'INCONCLUSIVE' if missing_review or collection else 'PASS',
            'presentationOrder': 'SUPPORTED_SOFTWARE_ORDER_ONLY' if attribution and all(a == 'SUPPORTED_SOFTWARE_ORDER_ONLY' for a in attribution) and not collection else 'INCONCLUSIVE',
            'collectionErrors': collection, 'correctnessErrors': correctness, 'frameOrder': attribution,
            'scope': 'Fixed captured software frames. WPF/DWM/model signals alone never certify displayed pixels or physical scan-out.'}


def r02_record(row, identity, result, diagnostic=None):
    errors, failures = [], []
    if result.get('processId') != identity['processId'] or result.get('mode') != row['kind']: errors.append('WRONG_PID_OR_PROCESS_KIND')
    if result.get('acceptance') not in ('PASS', 'FAIL'): errors.append('MISSING_ACCEPTANCE')
    if result.get('acceptance') == 'FAIL': failures.append('ORIGINAL_ASSERTION_FAIL')
    if row['kind'] == 'preflight':
        if not diagnostic or diagnostic.get('processId') != identity['processId'] or diagnostic.get('revisionArgument') != row['product'] or diagnostic.get('scope') != 'VALIDATION_PROCESS_PRE_ASSERTION; capture application not yet launched':
            errors.append('PREFLIGHT_IDENTITY_MISSING')
        else:
            foreground = diagnostic['pressedText']['effective']['argb']; background = diagnostic['pressedBackground']['effective']['argb']
            ratio = contrast(foreground, background)
            if diagnostic.get('requiredContrast') != 4.5 or abs(ratio - diagnostic['pressedContrast']) > 1e-9: errors.append('CONTRAST_CALCULATION_MISMATCH')
            if ratio < 4.5 or foreground != diagnostic['wpfHighlightText']['argb']: failures.append('PRESSED_CONTRAST_FAIL')
    elif row['kind'] == 'stock':
        ratio = contrast(result['foreground'], result['background'])
        if result.get('requirement') != 4.5 or abs(ratio - result['ratio']) > 1e-9: errors.append('CONTRAST_CALCULATION_MISMATCH')
        if ratio < 4.5: failures.append('STOCK_PAIR_FAIL')
    else:
        observations = result.get('observations', [])
        if [x['phase'] for x in observations] != ['initial-hc', 'normal', 'returned-hc']: errors.append('INCOMPLETE_OR_DUPLICATED_TRANSITION')
        if result.get('notificationOverflow') is not False: errors.append('NOTIFICATION_OVERFLOW')
        notifications = result.get('notifications', [])
        if any(n.get('processId') != identity['processId'] for n in notifications): errors.append('WRONG_NOTIFICATION_PID')
        for observation in observations:
            pair = observation['pair']; before = observation['before']; after = observation['after']
            if observation.get('processId') != identity['processId'] or pair.get('processId') != identity['processId']: errors.append('WRONG_APP_PID')
            if pair.get('name') != 'New document (Ctrl+N)' or pair.get('uiaName') != pair.get('name') or pair.get('kind') != 'New': errors.append('WRONG_CONTROL')
            if before['actualWidth'] != 1280 or before['actualHeight'] != 800: errors.append('WRONG_VIEWPORT')
            if pair.get('pressed') is not True or observation.get('stillPressed') is not True: failures.append('PRESSED_STATE_LOST')
            if observation.get('preserved') is not True or not before['focus'] or not after['focus'] or before['source'] != after['source'] or before['selectionStart'] != after['selectionStart'] or before['selectionLength'] != after['selectionLength']:
                failures.append('SOURCE_SELECTION_FOCUS_FAIL')
            if observation['expectedHc'] != (observation['phase'] != 'normal') or before['actualHc'] != observation['expectedHc'] or before['wpfHc'] != observation['expectedHc']:
                failures.append('APPEARANCE_MISMATCH')
            if observation['phase'] != 'initial-hc' and not any(n['tick'] >= observation['transitionStarted'] and n['tick'] <= pair['tick'] and n['message'] in (0x1a, 0x31a, 0x320) for n in notifications):
                errors.append('MISSING_REAL_TRANSITION_NOTIFICATION')
            for field, measured in [('foreground', 'ratio'), ('iconForeground', 'iconRatio')]:
                ratio = contrast(pair[field], pair['background'])
                if observation.get('requirement') != 4.5 or abs(ratio - observation[measured]) > 1e-9: errors.append('CONTRAST_CALCULATION_MISMATCH')
                if ratio < 4.5 or observation['expectedHc'] and pair[field] != pair['highlightText']: failures.append('APP_CONTRAST_FAIL')
            if observation.get('acceptance') != 'PASS': failures.append('ORIGINAL_APP_OBSERVATION_FAIL')
    return {'collection': 'INVALID' if errors else 'VALID', 'existingCorrectness': 'FAIL' if failures else 'INCONCLUSIVE' if errors else 'PASS',
            'presentationOrder': 'NOT_CLAIMED', 'collectionErrors': errors, 'correctnessErrors': failures}


def audit(root):
    root = Path(root).resolve(); execution = strict_json(root / 'execution.json')
    expected = cells(execution['protocol'])
    if execution['cells'] != expected: raise ValueError('Execution cells differ from the fixed plan')
    processes = strict_json(root / 'processes.json')
    if len(processes) > len(expected) or len({p['id'] for p in processes}) != len(processes): raise ValueError('Extra or duplicated process')
    pids = [p['processId'] for p in processes if p.get('processId')]
    if len(pids) != len(set(pids)): raise ValueError('Process identity reused; preserve distinct observations')
    outcomes = []
    for row in expected:
        process = next((p for p in processes if p['id'] == row['id']), None)
        if not process:
            outcomes.append({'id': row['id'], 'collection': 'INVALID', 'existingCorrectness': 'INCONCLUSIVE', 'reason': 'MISSING_PROCESS'}); continue
        folder = root / row['id'] / 'evidence'
        try:
            if process['kind'] != row['kind'] or process['productRevision'] != row['product'] or process['runtime'] != row['runtime'] or process['bundle'] != row['bundle']:
                raise ValueError('Mismatched process/cell record')
            for name, digest in process['files'].items(): verify_file(safe_relative(folder, name), digest)
            actual = {p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file()}
            if actual != set(process['files']): raise ValueError('Missing or unsealed extra evidence')
            identity = strict_json(folder / 'identity.json'); pin = execution['config']['runtimePins'][row['runtime']]
            if identity['processId'] != process['processId'] or identity['productRevision'] != row['product'] or identity['runtimeVersion'] != row['runtime']:
                raise ValueError('Loaded process/product/runtime identity differs')
            for key, file in [('product', 'LensDocsStudio.Native.dll'), ('core', 'LensDocsStudio.Core.dll'), ('harness', 'LensDocsStudio.Native.UxProbe.dll' if row['kind'] == 'completion' else 'LensDocsStudio.Native.Qualification.dll')]:
                if identity[key]['sha256'].lower() != process['binaryHashes'][file].lower(): raise ValueError('Loaded binary mismatch: ' + key)
            if identity['runtimeCore']['sha256'].lower() != pin['coreSha256'].lower() or identity['wpf']['sha256'].lower() != pin['wpfSha256'].lower(): raise ValueError('Loaded core/WPF hash differs')
            if row['kind'] == 'completion':
                summary = strict_json(folder / 'presentation-completion.json'); captures = strict_json(folder / 'completion-captures.json')
                trace_path = folder / 'completion-trace.jsonl'; meta_path = folder / 'completion-trace-metadata.json'
                trace = None; metadata = strict_json(meta_path) if meta_path.exists() else None
                if trace_path.exists():
                    raw = trace_path.read_bytes()
                    if len(raw) > 16777216 or not raw.endswith(b'\n'): raise ValueError('Truncated/unbounded trace')
                    trace = [strict_data(line) for line in raw.splitlines()]
                    if not metadata or metadata['bytes'] != len(raw): raise ValueError('Trace byte count differs')
                pngs = [p for p in actual if p.endswith('.png') and p != 'pixel.png']
                declared = {c['file'] for c in captures} | {c['file'].replace('.png', '-' + r['name'] + '.png') for c in captures for r in c['regions']}
                if set(pngs) != declared or len(captures) > 21 or len(declared) > 84: raise ValueError('Missing/extra fixed PNG or crop')
                scope = strict_json(folder / 'qualification-scope.json')
                if scope != {'processId': identity['processId'], 'width': 1280, 'height': 800, 'zoom': 100, 'genericObserverHooks': False}:
                    raise ValueError('Fixed viewport/zoom/hook scope unavailable')
                for frame in captures:
                    decoded = inspect_png(safe_relative(folder, frame['file']).read_bytes())
                    if decoded['bgraSha256'].lower() != frame['sha256'].lower(): raise ValueError('Decoded capture differs from original BGRA hash')
                    for region in frame['regions']:
                        if region['x'] + region['width'] > decoded['width'] + 1 or region['y'] + region['height'] > decoded['height'] + 1:
                            raise ValueError('Clipped label crop')
                        inspect_png(safe_relative(folder, frame['file'].replace('.png', '-' + region['name'] + '.png')).read_bytes())
                reviews_path = root / 'pixel-reviews' / (row['id'] + '.json')
                reviews = strict_json(reviews_path) if reviews_path.exists() else None
                result = r01_record(row, process, identity, summary, captures, trace, metadata, reviews)
            else:
                result_path = folder / 'result.json'
                if result_path.exists(): result_data = strict_json(result_path)
                elif (folder / 'failure.json').exists():
                    # Original preflight throws on its unchanged assertion. Use its
                    # retained failure record; absence alone is never a failed assertion.
                    result_data = strict_json(folder / 'failure.json')
                else: raise ValueError('Missing process result and original failure record')
                diagnostic_path = folder / 'high-contrast-pre-assertion.json'
                result = r02_record(row, identity, result_data, strict_json(diagnostic_path) if diagnostic_path.exists() else None)
                if row['kind'] != 'application': validate_notifications(strict_json(folder / 'native-notifications.json'), identity['processId'])
                if row['kind'] == 'application':
                    for observation in result_data.get('observations', []):
                        image = verify_file(safe_relative(folder, observation['file']), observation['pngSha256'])
                        if inspect_png(image.read_bytes())['bgraSha256'].lower() != observation['sha256'].lower(): raise ValueError('Decoded app capture differs')
            if process.get('restored') is not True or process.get('cleanupVerified') is not True or process.get('jobTreeEmpty') is not True or process.get('supervisionFailure') or process.get('exitCode') is None:
                result['collection'] = 'INVALID'; result['collectionErrors'].append('SUPERVISION_OR_RESTORATION_FAIL')
            if process.get('exitCode') != 0 or any('failure' in name and name.endswith('.json') for name in actual):
                result['existingCorrectness'] = 'FAIL'; result['correctnessErrors'].append('RETAINED_PROCESS_FAILURE')
            if result['collection'] == 'INVALID' and result['existingCorrectness'] == 'PASS':
                result['existingCorrectness'] = 'INCONCLUSIVE'
            outcomes.append(dict(id=row['id'], **result))
        except (ValueError, KeyError, TypeError, FileNotFoundError) as error:
            retained = any('failure' in name and name.endswith('.json') for name in process.get('files', {}))
            outcomes.append({'id': row['id'], 'collection': 'INVALID', 'existingCorrectness': 'FAIL' if retained or process.get('exitCode') not in (None, 0) else 'INCONCLUSIVE',
                             'presentationOrder': 'INCONCLUSIVE', 'reason': str(error)})
    return {'schemaVersion': 1, 'protocol': execution['protocol'], 'outcomes': outcomes,
            'collection': 'VALID' if all(o['collection'] == 'VALID' for o in outcomes) else 'INVALID',
            'existingCorrectness': 'FAIL' if any(o['existingCorrectness'] == 'FAIL' for o in outcomes) else 'PASS' if all(o['existingCorrectness'] == 'PASS' for o in outcomes) else 'INCONCLUSIVE',
            'policyAcceptance': 'NOT_ACTIVATED', 'pr76Integration': 'NO-GO', 'historicalFailuresReclassified': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('root'); parser.add_argument('--output', required=True); args = parser.parse_args()
    try:
        result = audit(args.root); json_new(args.output, result)
        print(result['collection'], result['existingCorrectness'], 'PR76 NO-GO')
        sys.exit(0 if result['collection'] == 'VALID' and result['existingCorrectness'] == 'PASS' else 1)
    except Exception as error:
        print('INVALID: ' + str(error), file=sys.stderr); sys.exit(2)
