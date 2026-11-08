"""Offline controls: no app windows, input, OS appearance changes or fake hosted flags."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import unittest
import uuid
from unittest.mock import patch
import zlib

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/native/qualification'))
import audit
import png
import qualification as q
import sources
import windows

R01_CELLS = q.cells('R01')
R02_CELLS = q.cells('R02')


def state():
    reader = dict(isVisible=True, currentOwner=True, attachedMatches=True, itemStatus='Ready', isMeasureValid=True, isArrangeValid=True,
                  source=40, textPattern=[dict(meaningful=True, rectangles=[1, 2, 30, 40])], mode='Page', pageNumber=4, pageCount=5,
                  nativePage=4, nativeCount=5, pageText='Page 4 of 5', pageUia='Page 4 of 5', owner='owner', expectedOwner='owner')
    return dict(tick=100, finished=110, previewStatus='Preview updated', statusText='Preview updated', statusUia='Preview updated',
                primary=copy.deepcopy(reader), secondary=copy.deepcopy(reader))


def r01():
    row = copy.deepcopy(R01_CELLS[0])
    identity = dict(processId=7, iterations=1, fixture='mixed', highContrast=False, genericObserverHooks=False, stopwatchFrequency=1000)
    summary = dict(functional='PASS', sourceIntegrity='PASS', scenario='pixels', target='primary', mode='Pages', instrumented=False)
    frames, reviews, files = [], [], {}
    for action in audit.BOUNDARIES:
        for index in range(3):
            file = f'{action}-{index}.png'; files[file] = 'a' * 64
            before, after = state(), state(); after['tick'] = 140; after['finished'] = 150
            frames.append(dict(action=action, frameIndex=index, file=file, observationStarted=100, observationDeadlineSeconds=30,
                before=before, after=after, started=111, dwmReturned=112, pixelsStarted=120, pixelsFinished=130, committed=None,
                compositionBefore=None, compositionAfter=None,
                completedReaders=['primary'] if action in ('primary-find', 'secondary-find') else ['primary', 'secondary'],
                regions=[dict(name='status', x=0, y=0, width=20, height=20)]))
            reviews.append(dict(file=file, pngSha256='a'*64, processId=7, reviewer='Offline fixture control', requiredReaders=frames[-1]['completedReaders'],
                                currentSourceVisible=True, requiredLabelsCorrect=True, selectionCorrect=True))
    return row, dict(files=files), identity, summary, frames, None, None, reviews


def preflight():
    row = copy.deepcopy(R02_CELLS[0]); identity = dict(processId=10)
    result = dict(processId=10, mode='preflight', acceptance='PASS')
    diagnostic = dict(processId=10, revisionArgument=row['product'], scope='VALIDATION_PROCESS_PRE_ASSERTION; capture application not yet launched',
        pressedText={'effective': {'argb': '#FFFFFFFF'}}, pressedBackground={'effective': {'argb': '#FF000000'}},
        wpfHighlightText={'argb': '#FFFFFFFF'}, pressedContrast=21, requiredContrast=4.5)
    return row, identity, result, diagnostic


def application():
    row = copy.deepcopy(next(r for r in R02_CELLS if r['kind'] == 'application')); identity = dict(processId=11)
    observations = []
    for i, phase in enumerate(('initial-hc', 'normal', 'returned-hc')):
        observations.append(dict(phase=phase, processId=11, expectedHc=phase != 'normal', transitionStarted=10+i*100,
            pair=dict(processId=11, name='New document (Ctrl+N)', uiaName='New document (Ctrl+N)', kind='New', pressed=True,
                foreground='#FFFFFFFF', iconForeground='#FFFFFFFF', background='#FF000000', highlightText='#FFFFFFFF', tick=50+i*100),
            before=dict(actualWidth=1280, actualHeight=800, actualHc=phase != 'normal', wpfHc=phase != 'normal', focus=True, source='x', selectionStart=0, selectionLength=8),
            after=dict(focus=True, source='x', selectionStart=0, selectionLength=8), stillPressed=True, preserved=True, ratio=21, iconRatio=21,
            requirement=4.5, acceptance='PASS'))
    return row, identity, dict(mode='application', processId=11, acceptance='PASS', observations=observations, notificationOverflow=False,
        notifications=[dict(processId=11, tick=120, message=0x1a), dict(processId=11, tick=220, message=0x1a)])


def image_bytes(width=2, height=1):
    def chunk(kind, data): return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind+data) & 0xffffffff)
    pixels = bytes([255, 0, 0, 255, 0, 0, 255, 255])
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(b'\0'+pixels)) + chunk(b'IEND', b'')


class SourceAndScope(unittest.TestCase):
    def test_exact_sources_fixture_bytes_and_all_seven_anchors(self):
        plan, identities = sources.verify_sources()
        self.assertEqual(len(plan['corpus']['syntheticAnchors']), 7)
        self.assertEqual(identities['candidate']['tree'], '887856d293114442e97e0d06bef2928b340b639a')

    def test_product_snapshot_rejects_later_edits_or_untracked_compiler_inputs(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); file=root/'product/Example.cs'; file.parent.mkdir(); file.write_bytes(b'original')
            receipt={'generated':{'product/Example.cs':sources.sha(b'original')}}
            q.verify_build_inputs(root,receipt)
            (root/'product/Injected.cs').write_bytes(b'injected')
            with self.assertRaisesRegex(ValueError,'extra build input'): q.verify_build_inputs(root,receipt)
            file.write_bytes(b'changed')
            with self.assertRaisesRegex(ValueError,'identity mismatch'): q.verify_build_inputs(root,receipt)

    def test_compiled_metadata_cannot_relabel_products_with_the_tooling_commit(self):
        good={name:{'version':'0.1+'+revision} for name,revision in [('product',sources.CANDIDATE),('core',sources.CANDIDATE),('harness',sources.START)]}
        q.validate_binary_metadata(good,sources.CANDIDATE,sources.START)
        for name in ('product','core'):
            bad=copy.deepcopy(good); bad[name]['version']='0.1+'+sources.START
            with self.assertRaises(ValueError): q.validate_binary_metadata(bad,sources.CANDIDATE,sources.START)

    def test_finite_cells_and_no_calibration_execution(self):
        r01 = q.cells('R01'); r02 = q.cells('R02')
        self.assertEqual([(r['appearance'], r['trace']) for r in r01], [('normal', False), ('normal', True), ('high-contrast', False), ('high-contrast', True)])
        self.assertEqual(len(r01)*7*3, 84); self.assertEqual(len(r01)*7*3*3, 252)
        self.assertEqual([sum(r['kind'] == k for r in r02) for k in ('preflight', 'application', 'stock')], [8, 8, 2])
        self.assertEqual(len({r['id'] for r in r02}), 18)
        self.assertEqual({r['runtime'] for r in r02}, {'10.0.13', '10.0.14'})
        with self.assertRaises(ValueError): q.cells('calibration')

    def test_all_ambient_hooks_and_loader_overrides_are_removed(self):
        env = q.environment({'PATH':'safe', 'LENS_FAKE':'1', 'LENS_NATIVE_TRANSITION_TRACE':'1', 'DOTNET_STARTUP_HOOKS':'bad', 'COMPLUS_ReadyToRun':'0', 'CORECLR_ENABLE_PROFILING':'1'}, q.cells('R01')[0], 'id', 'hash')
        self.assertNotIn('DOTNET_STARTUP_HOOKS', env); self.assertNotIn('LENS_FAKE', env); self.assertNotIn('COMPLUS_ReadyToRun', env)
        self.assertEqual(env['LENS_NATIVE_COMPLETION_JOURNEY'], '1'); self.assertEqual(env['LENS_NATIVE_COMPARISON_MODE'], 'Pages')
        for key in ('LENS_NATIVE_TRANSITION_TRACE', 'LENS_NATIVE_STARTUP_TRACE', 'LENS_NATIVE_COMPARISON_JOURNEY', 'LENS_NATIVE_COMPLETION_TRACE', 'LENS_NATIVE_COMPOSITION_TRACE'):
            self.assertEqual(env[key], '0')

    def test_overlay_is_exact_and_rejects_drift(self):
        original = sources.blob(sources.CANDIDATE, sources.PROBE+'Program.cs').decode()
        overlay = sources.completion_overlay(original)
        self.assertIn('if (stages is not null)', overlay); self.assertIn('stages?.Dispose();', overlay)
        self.assertIn('== "1" ? null : new();', overlay); self.assertIn('extraCapture = false', overlay)
        self.assertEqual(overlay.count('new StageListener'), 0)
        self.assertLess(overlay.index('LENS_QUALIFICATION_EXECUTION_ID'), overlay.index('if (args.FirstOrDefault()'))
        with self.assertRaises(ValueError): sources.completion_overlay(original.replace('private readonly StageListener stages = new();', 'changed'))

    def test_preflight_sequence_and_diagnostic_are_verbatim(self):
        original = sources.blob(sources.CANDIDATE, sources.VALIDATION+'NativeUxScreenshots.cs').decode().replace('\r\n', '\n')
        sequence = '            using var contrast = '+original.split('            using var contrast = ', 1)[1].split('            using var display = ',1)[0]
        generated = sources.preflight_source(); self.assertIn(sequence, generated)
        self.assertIn(original[original.index('    // Read only, after the original resource evaluation:'):original.rindex('\n}')], generated)
        self.assertLess(sequence.index('new NativeTestHighContrast'), sequence.index('System.Windows.SystemParameters.HighContrast'))
        self.assertLess(sequence.index('ThemeService.Apply'), sequence.index('WriteUxContrastDiagnostic'))
        self.assertLess(sequence.index('WriteUxContrastDiagnostic'), sequence.index('NativeSyntaxChecks.Contrast'))
        win32 = (ROOT/'scripts/native/qualification/observer/Appearance.cs').read_text()
        self.assertNotIn('using System.Windows', win32)
        monitor=(ROOT/'scripts/native/qualification/observer/NativeNotifications.cs').read_text()
        self.assertNotIn('using System.Windows',monitor); self.assertNotIn('SystemColors.',monitor)
        self.assertNotIn('ShowWindow(',monitor); self.assertNotIn('SendMessage(',monitor)

    def test_no_authorisation_and_developer_desktop_are_rejected_before_process_or_api(self):
        with self.assertRaisesRegex(ValueError, 'authorisation'): q.validate_config(q.template(), 'R01')
        with patch.dict(os.environ, {'GITHUB_ACTIONS':'false'}), patch('qualification.subprocess.Popen') as child:
            with self.assertRaisesRegex(ValueError, 'developer desktop'): q.execute('missing.json', 'R01', 'not-created')
            child.assert_not_called()

    def test_safety_limits_cannot_be_relaxed(self):
        limits = q.template()['limits']
        self.assertEqual(q.limit_failure(241, 0, 0, limits), 'CHILD_TIMEOUT')
        self.assertEqual(q.limit_failure(1, 2147483649, 0, limits), 'PRIVATE_MEMORY_BOUND')
        self.assertEqual(q.limit_failure(1, 0, 16777217, limits), 'LOG_BOUND')
        config = q.template(); config['limits']['childSeconds'] = 241
        with self.assertRaisesRegex(ValueError, 'finite contract'): q.validate_config(config, 'R01')

    def test_exact_runtime_paths_and_bytes_reject_moving_or_mismatched_versions(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); pin={}
            for key, relative in [('dotnet','dotnet.exe'),('core','shared/Microsoft.NETCore.App/10.0.13/System.Private.CoreLib.dll'),('wpf','shared/Microsoft.WindowsDesktop.App/10.0.13/PresentationFramework.dll')]:
                file=root/relative; file.parent.mkdir(parents=True,exist_ok=True); file.write_bytes(key.encode())
                pin[key]=str(file); pin[key+'Sha256']=sources.sha(key.encode())
            q.validate_runtime('10.0.13',pin)
            for version in ('10.0.x','latest','10.0.12','10.0.14'):
                with self.assertRaises(ValueError): q.validate_runtime(version,pin)
            damaged=dict(pin,wpfSha256='0'*64)
            with self.assertRaises(ValueError): q.validate_runtime('10.0.13',damaged)


class VisibleCompletion(unittest.TestCase):
    def test_original_presentation_assertions_still_fail_on_the_same_negative_controls(self):
        cases=[state()]
        for mutation in (dict(currentOwner=False),dict(attachedMatches=False),dict(textPattern=[]),dict(pageUia='stale'),dict(nativePage=5),dict(isMeasureValid=False)):
            item=state(); item['primary'].update(mutation); cases.append(item)
        item=state(); item['statusText']='stale'; cases.append(item)
        with tempfile.TemporaryDirectory() as tmp:
            module=Path(tmp)/'original.mjs'; module.write_bytes(sources.blob(sources.CANDIDATE,'scripts/native/completion-audit.mjs'))
            code='import {readFileSync} from "node:fs"; const m=await import('+json.dumps(module.as_uri())+'); console.log(JSON.stringify(JSON.parse(readFileSync(0,"utf8")).map(x=>m.checkPresentation(x))));'
            original=json.loads(subprocess.check_output(['node','--input-type=module','-e',code],input=json.dumps(cases).encode()))
            self.assertEqual([bool(x) for x in original],[bool(audit.presentation(x,['primary','secondary'])) for x in cases])
    def test_good_fixed_evidence_separates_correctness_from_unqualified_order(self):
        result = audit.r01_record(*r01())
        self.assertEqual((result['collection'], result['existingCorrectness'], result['presentationOrder']), ('VALID','PASS','INCONCLUSIVE'))

    def test_wrong_owner_stale_source_missing_text_invalid_geometry_and_false_ready_fail(self):
        for mutation in [dict(currentOwner=False), dict(owner='wrong'), dict(source=None), dict(textPattern=[]),
                         dict(textPattern=[dict(meaningful=True, rectangles=[0,0,-2,4])]), dict(pageText='Page 1 of 5'), dict(isArrangeValid=False), dict(itemStatus='Busy')]:
            data = list(r01()); data[4][0]['before']['primary'].update(mutation)
            self.assertEqual(audit.r01_record(*data)['existingCorrectness'], 'FAIL', mutation)

    def test_busy_unrequired_pane_never_exempts_a_false_ready_or_all_readers_boundary(self):
        value = state(); value['secondary'].update(itemStatus='Busy', currentOwner=False, textPattern=[])
        self.assertEqual(audit.presentation(value, ['primary']), [])
        self.assertTrue(audit.presentation(value, ['primary','secondary']))
        value['secondary']['itemStatus']='Ready'; self.assertTrue(audit.presentation(value, ['primary']))

    def test_stale_pixels_and_selection_never_cleared_by_later_frames(self):
        for field in ('currentSourceVisible', 'requiredLabelsCorrect', 'selectionCorrect'):
            data = list(r01()); data[7][0][field] = False
            self.assertEqual(audit.r01_record(*data)['existingCorrectness'], 'FAIL')

    def test_ocr_or_missing_review_is_inconclusive(self):
        data = list(r01()); data[7] = None
        self.assertEqual(audit.r01_record(*data)['existingCorrectness'], 'INCONCLUSIVE')
        data = list(r01()); data[7][0]['currentSourceVisible'] = 'OCR MATCH'
        self.assertEqual(audit.r01_record(*data)['existingCorrectness'], 'INCONCLUSIVE')

    def test_wrong_review_pid_hash_duplicate_and_hidden_scope_invalid(self):
        for change in (dict(processId=8), dict(pngSha256='b'*64), dict(requiredReaders=['secondary'])):
            data = list(r01()); data[7][0].update(change)
            self.assertEqual(audit.r01_record(*data)['collection'], 'INVALID')
        data = list(r01()); data[7].append(data[7][0]); self.assertEqual(audit.r01_record(*data)['collection'], 'INVALID')

    def test_missing_duplicate_extra_frame_and_deadline_are_invalid(self):
        for mutate in (lambda f: f.pop(), lambda f: f.append(f[0]), lambda f: f[0].update(frameIndex=1),
                       lambda f: f[0].update(observationDeadlineSeconds=31), lambda f: f[0].update(pixelsFinished=30101)):
            data = list(r01()); mutate(data[4]); self.assertEqual(audit.r01_record(*data)['collection'], 'INVALID')

    def test_hidden_secondary_is_only_allowed_after_closed_comparison(self):
        data=list(r01()); data[4][3]['before']['secondary']['isVisible']=False
        self.assertEqual(audit.r01_record(*data)['existingCorrectness'],'FAIL')
        value=state(); value['secondary']['isVisible']=False
        self.assertTrue(audit.presentation(value,['primary','secondary']))
        self.assertEqual(audit.presentation(value,['primary','secondary'],True),[])

    def test_trace_off_negative_or_reversed_capture_clock_is_invalid(self):
        for mutation in (dict(pixelsFinished=99),dict(pixelsStarted=140),dict(started=-1),dict(dwmReturned=float('nan'))):
            data=list(r01()); data[4][0].update(mutation)
            self.assertEqual(audit.r01_record(*data)['collection'],'INVALID')

    def test_disabled_trace_detects_hooks_or_media_counters(self):
        data = list(r01()); data[4][0]['compositionBefore']={}; self.assertEqual(audit.r01_record(*data)['collection'], 'INVALID')
        data = list(r01()); data[2]['genericObserverHooks']=True; self.assertEqual(audit.r01_record(*data)['collection'], 'INVALID')
        data = list(r01()); data[5]=[]; self.assertEqual(audit.r01_record(*data)['collection'], 'INVALID')

    def test_missing_duplicate_truncated_trace_and_overflow_are_invalid(self):
        lines=[dict(tick=1,boundary='subscribed'),dict(tick=2,boundary='wpf/status-text')]
        meta=dict(recordOverflow=False,byteOverflow=False,written=2)
        audit.validate_trace(lines, meta)
        for value, metadata in [(lines[:-1],meta),(lines+[lines[-1]],dict(meta,written=3)),(lines,dict(meta,byteOverflow=True)),([],dict(meta,written=0))]:
            with self.assertRaises(ValueError): audit.validate_trace(value, metadata)

    def test_render_callback_dwmflush_and_ready_alone_do_not_support_order(self):
        frame = r01()[4][0]
        trace=[dict(tick=100,boundary='wpf/status-text'), dict(tick=115,boundary='media/RenderComplete')]
        self.assertEqual(audit.capture_order(frame, trace, True), 'INCONCLUSIVE')
        commit=dict(tick=115,boundary='media/CommittingBatch',primary=dict(owner='owner',current=True))
        self.assertEqual(audit.capture_order(frame, trace+[commit], True), 'SUPPORTED_SOFTWARE_ORDER_ONLY')
        for event in [dict(tick=125,boundary='wpf/status-text'), dict(tick=119,boundary='model/ActiveDocument')]:
            self.assertEqual(audit.capture_order(frame, trace+[commit,event], True), 'INCONCLUSIVE')
        commit['primary']['owner']='wrong'; self.assertEqual(audit.capture_order(frame, trace+[commit], True), 'INCONCLUSIVE')


class HighContrast(unittest.TestCase):
    def test_stock_and_preflight_native_notifications_require_correct_process_and_chronology(self):
        record=dict(processId=9,overflow=False,error=None,started=10,finished=30,events=[dict(processId=9,tick=20,message=0x1a)])
        audit.validate_notifications(record,9)
        for patching in (dict(processId=10),dict(events=[]),dict(overflow=True),dict(finished=15)):
            with self.assertRaises(ValueError): audit.validate_notifications(dict(record,**patching),9)
    def test_rgb_ratios_historical_failure_and_unchanged_requirement(self):
        self.assertEqual(audit.contrast('#000000','#FFFFFF'),21)
        self.assertAlmostEqual(audit.contrast('#FF263B50','#FF0078D4'),2.542596386,places=8)
        for value in ('#00263B50','#GG0000','#fff','red',None):
            with self.assertRaises(ValueError): audit.rgb(value)
        data=list(preflight()); data[3]['pressedText']['effective']['argb']='#FF263B50'; data[3]['pressedBackground']['effective']['argb']='#FF0078D4'
        data[3]['pressedContrast']=audit.contrast('#FF263B50','#FF0078D4')
        self.assertEqual(audit.r02_record(*data)['existingCorrectness'],'FAIL')

    def test_preflight_and_application_are_independent(self):
        data=list(preflight()); data[2]['acceptance']='FAIL'
        self.assertEqual(audit.r02_record(*data)['existingCorrectness'],'FAIL')
        self.assertEqual(audit.r02_record(*application())['existingCorrectness'],'PASS')

    def test_wrong_pid_process_kind_and_requirement_are_invalid(self):
        for mutation in (dict(processId=99),dict(mode='application')):
            data=list(preflight()); data[2].update(mutation); self.assertEqual(audit.r02_record(*data)['collection'],'INVALID')
        data=list(preflight()); data[3]['processId']=99; self.assertEqual(audit.r02_record(*data)['collection'],'INVALID')
        data=list(preflight()); data[3]['requiredContrast']=2.5; self.assertEqual(audit.r02_record(*data)['collection'],'INVALID')

    def test_app_requires_actual_control_icon_and_template_surface(self):
        for mutation in (dict(name='Save'),dict(kind='Document'),dict(processId=10)):
            data=list(application()); data[2]['observations'][0]['pair'].update(mutation)
            self.assertEqual(audit.r02_record(*data)['collection'],'INVALID')
        data=list(application()); data[2]['observations'][0]['pair']['background']='#00000000'
        with self.assertRaises(ValueError): audit.r02_record(*data)

    def test_app_selection_focus_pressed_state_and_bad_icon_contrast_fail(self):
        for mutation in (dict(stillPressed=False),dict(preserved=False)):
            data=list(application()); data[2]['observations'][0].update(mutation)
            self.assertEqual(audit.r02_record(*data)['existingCorrectness'],'FAIL')
        data=list(application()); data[2]['observations'][0]['after']['selectionLength']=0
        self.assertEqual(audit.r02_record(*data)['existingCorrectness'],'FAIL')
        data=list(application()); data[2]['observations'][0]['pair']['iconForeground']='#FF000000'; data[2]['observations'][0]['iconRatio']=1
        self.assertEqual(audit.r02_record(*data)['existingCorrectness'],'FAIL')

    def test_transition_missing_duplicate_wrong_pid_and_overflow_invalid(self):
        for mutate in (lambda r:r['observations'].pop(), lambda r:r['observations'].append(r['observations'][0]),
                       lambda r:r.update(notifications=[]), lambda r:r.update(notificationOverflow=True),
                       lambda r:r['notifications'][0].update(processId=99)):
            data=list(application()); mutate(data[2]); self.assertEqual(audit.r02_record(*data)['collection'],'INVALID')


class EvidenceAndSupervisor(unittest.TestCase):
    @staticmethod
    def campaign(root):
        def put(path,value):
            path.parent.mkdir(parents=True,exist_ok=True); path.write_text(json.dumps(value),encoding='utf-8')
        rows=copy.deepcopy(R02_CELLS); processes=[]
        pins={version:{'coreSha256':'d'*64,'wpfSha256':'e'*64} for version in q.RUNTIMES}
        put(root/'execution.json',dict(protocol='R02',cells=rows,config={'runtimePins':pins}))
        for index,row in enumerate(rows):
            pid=1000+index; folder=root/row['id']/'evidence'
            identity=dict(processId=pid,productRevision=row['product'],runtimeVersion=row['runtime'],product={'sha256':'a'*64},core={'sha256':'b'*64},
                          harness={'sha256':'c'*64},runtimeCore={'sha256':'d'*64},wpf={'sha256':'e'*64})
            put(folder/'identity.json',identity)
            if row['kind']=='preflight':
                _,_,result,diagnostic=preflight(); result['processId']=pid; diagnostic['processId']=pid; diagnostic['revisionArgument']=row['product']
                put(folder/'result.json',result); put(folder/'high-contrast-pre-assertion.json',diagnostic)
            elif row['kind']=='stock':
                put(folder/'result.json',dict(mode='stock',processId=pid,acceptance='PASS',foreground='#FFFFFFFF',background='#FF000000',ratio=21,requirement=4.5))
            else:
                _,_,result=application(); result['processId']=pid
                for event in result['notifications']: event['processId']=pid
                for observation in result['observations']:
                    observation['processId']=pid; observation['pair']['processId']=pid
                    file=observation['phase']+'.png'; data=image_bytes(); (folder/file).write_bytes(data)
                    observation.update(file=file,pngSha256=sources.sha(data),sha256=png.inspect(data)['bgraSha256'])
                put(folder/'result.json',result)
            if row['kind']!='application':
                put(folder/'native-notifications.json',dict(processId=pid,overflow=False,error=None,started=10,finished=30,events=[dict(processId=pid,tick=20,message=0x1a)]))
            processes.append(dict(id=row['id'],kind=row['kind'],productRevision=row['product'],runtime=row['runtime'],bundle=row['bundle'],processId=pid,exitCode=0,
                restored=True,cleanupVerified=True,jobTreeEmpty=True,supervisionFailure=None,binaryHashes={'LensDocsStudio.Native.dll':'a'*64,'LensDocsStudio.Core.dll':'b'*64,'LensDocsStudio.Native.Qualification.dll':'c'*64},
                files={p.relative_to(folder).as_posix():sources.sha(p.read_bytes()) for p in folder.rglob('*') if p.is_file()}))
        put(root/'processes.json',processes)
        return processes

    def test_full_offline_r02_envelope_retains_independent_process_results(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); self.campaign(root); result=audit.audit(root)
            self.assertEqual((result['collection'],result['existingCorrectness']),('VALID','PASS'))
            self.assertEqual(len(result['outcomes']),18); self.assertEqual(result['pr76Integration'],'NO-GO')

    def test_full_envelope_rejects_wrong_binary_runtime_pid_missing_and_unsealed_evidence(self):
        for mutation in ('binary','runtime','pid','missing','missing-result','unsealed','cleanup','failure'):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as tmp:
                root=Path(tmp); processes=self.campaign(root); folder=root/processes[0]['id']/'evidence'; path=folder/'identity.json'
                identity=q.strict_json(path)
                if mutation=='binary': identity['product']['sha256']='f'*64
                elif mutation=='runtime': identity['runtimeVersion']='10.0.x'
                elif mutation=='pid': identity['processId']=9000
                if mutation in ('binary','runtime','pid'):
                    path.write_text(json.dumps(identity)); processes[0]['files']['identity.json']=sources.sha(path.read_bytes())
                elif mutation=='missing': path.unlink()
                elif mutation=='missing-result':
                    (folder/'result.json').unlink(); del processes[0]['files']['result.json']
                elif mutation=='unsealed': (folder/'extra.json').write_text('{}')
                elif mutation=='cleanup': processes[0]['cleanupVerified']=False
                else:
                    (folder/'failure.json').write_text('{"acceptance":"FAIL"}')
                    processes[0]['files']['failure.json']=sources.sha((folder/'failure.json').read_bytes())
                    path.unlink()  # Incomplete collection must not erase a separately recorded FAIL.
                (root/'processes.json').write_text(json.dumps(processes))
                result=audit.audit(root)
                self.assertEqual(result['collection'],'INVALID')
                self.assertNotEqual(result['existingCorrectness'],'PASS')
                if mutation=='failure': self.assertEqual(result['existingCorrectness'],'FAIL')
                if mutation=='missing-result': self.assertEqual(result['existingCorrectness'],'INCONCLUSIVE')

    def test_full_envelope_rejects_extra_duplicate_or_mismatched_processes(self):
        for mutation in ('extra','duplicate','wrong-pid'):
            with tempfile.TemporaryDirectory() as tmp:
                root=Path(tmp); processes=self.campaign(root)
                if mutation=='extra': processes.append(copy.deepcopy(processes[0]))
                elif mutation=='duplicate': processes[1]['id']=processes[0]['id']
                else: processes[1]['processId']=processes[0]['processId']
                (root/'processes.json').write_text(json.dumps(processes))
                with self.assertRaises(ValueError): audit.audit(root)

    def test_cleanup_restores_os_even_after_input_failure_and_never_passes_unknown_state(self):
        class Host:
            restored=False
            def neutralise_input(self): raise RuntimeError('input unknown')
            def set_appearance(self,state): self.restored=True
            def appearance(self): return {'flags':0,'scheme':'original'}
        host=Host(); result=q.restore_experiment(host,{'flags':0,'scheme':'original'})
        self.assertTrue(host.restored); self.assertTrue(result['restored']); self.assertFalse(result['cleanupVerified'])
        host.neutralise_input=lambda: []
        self.assertTrue(q.restore_experiment(host,{'flags':0,'scheme':'original'})['cleanupVerified'])
        self.assertFalse(q.restore_experiment(host,{'flags':1,'scheme':'different'})['cleanupVerified'])
    def test_job_assignment_failure_terminates_the_unassigned_child(self):
        class Child:
            killed=False
            def poll(self): return None
            def kill(self): self.killed=True
        class Kernel:
            terminated=False
            def TerminateJobObject(self,*args): self.terminated=True; return 0
        host=object.__new__(windows.Windows); host.job=123; host.assigned=False; host.kernel=Kernel()
        child=Child(); host.kill(child)
        self.assertTrue(child.killed); self.assertFalse(host.kernel.terminated)
        host.assigned=True; child=Child(); host.kill(child)
        self.assertTrue(child.killed); self.assertTrue(host.kernel.terminated)
    def test_json_duplicate_truncated_nonfinite_and_missing_evidence_rejected(self):
        for data in (b'{"pid":1,"pid":2}',b'{"pid":',b'{"x":NaN}',b'[]trailing'):
            with self.assertRaises(ValueError): q.strict_data(data)
        with self.assertRaises(FileNotFoundError): q.strict_json(ROOT/'no-such-evidence.json')

    def test_png_decoded_hash_crc_truncation_and_dimensions(self):
        data=image_bytes(); result=png.inspect(data)
        self.assertEqual(result['width'],2)
        self.assertEqual(result['bgraSha256'],hashlib.sha256(bytes([0,0,255,255,255,0,0,255])).hexdigest())
        for damaged in (data[:-1],data+b'junk',data[:40]+bytes([data[40]^1])+data[41:],image_bytes(5000)):
            with self.assertRaises(ValueError): png.inspect(damaged)

    def test_path_escape_and_hash_mismatch_rejected(self):
        for path in ('../outside','C:/outside','a/../../b','a\\b'):
            with self.assertRaises(ValueError): q.safe_relative(ROOT,path)
        with tempfile.TemporaryDirectory() as tmp:
            file=Path(tmp)/'data'; file.write_bytes(b'original')
            with self.assertRaises(ValueError): q.verify_file(file,'0'*64)
            self.assertEqual(q.verify_file(file,sources.sha(b'original')),file)

    def test_supervisor_kills_bound_breach_and_closes_job(self):
        class Child:
            pid=77; returncode=None
            def poll(self): return None
            def wait(self,timeout): self.returncode=1
        class Host:
            killed=False; closed=False
            def attach(self,p): pass
            def private_bytes(self,p): return 2147483649
            def kill(self,p): self.killed=True
            def close(self): self.closed=True
        with tempfile.TemporaryDirectory() as tmp:
            host=Host(); result=q.run_child(['never-started'],{},Path(tmp)/'cell',q.template()['limits'],host,popen=lambda *a,**k:Child())
            self.assertTrue(host.killed and host.closed); self.assertEqual(result['supervisionFailure'],'PRIVATE_MEMORY_BOUND')
            self.assertEqual(result['exitCode'],1)

    def test_unknown_job_cleanup_is_a_supervision_failure_even_after_zero_exit(self):
        class Child:
            pid=79; returncode=0
            def poll(self): return 0
        class Host:
            def attach(self,p): pass
            def close(self): return False
        with tempfile.TemporaryDirectory() as tmp:
            result=q.run_child(['never-started'],{},Path(tmp)/'cell',q.template()['limits'],Host(),popen=lambda *a,**k:Child())
            self.assertFalse(result['jobTreeEmpty']); self.assertEqual(result['supervisionFailure'],'JOB_TREE_UNVERIFIED')

    @unittest.skipUnless(os.name=='nt','Windows job-object supervision control')
    def test_real_windows_job_supervises_only_a_harmless_console_child(self):
        # No WPF, window, foreground input or appearance API is called. This checks
        # real job/memory ABI and termination with a disposable Python sleeper.
        folder=ROOT/'artifacts/native-qualification-unit'/str(uuid.uuid4())
        limits=dict(q.template()['limits'],childSeconds=0.1)
        result=q.run_child([sys.executable,'-c','import time; time.sleep(60)'],dict(os.environ),folder,limits,windows.Windows())
        sources.json_new(folder/'receipt.json',result)
        self.assertEqual(result['supervisionFailure'],'CHILD_TIMEOUT'); self.assertNotEqual(result['exitCode'],0)
        self.assertTrue(result['jobTreeEmpty'])

    @unittest.skipUnless(os.name=='nt','Windows job-object supervision control')
    def test_real_windows_job_retains_a_harmless_console_failure(self):
        folder=ROOT/'artifacts/native-qualification-unit'/str(uuid.uuid4())
        result=q.run_child([sys.executable,'-c','raise SystemExit(7)'],dict(os.environ),folder,q.template()['limits'],windows.Windows())
        sources.json_new(folder/'receipt.json',result)
        self.assertEqual(result['exitCode'],7); self.assertIsNone(result['supervisionFailure']); self.assertTrue(result['jobTreeEmpty'])

    def test_supervisor_catches_immediate_exit_log_overflow_and_never_retries(self):
        class Child:
            pid=78; returncode=0
            def poll(self): return 0
            def wait(self,timeout): return 0
        class Host:
            killed=False
            def attach(self,p): pass
            def kill(self,p): self.killed=True
            def close(self): pass
        def spawn(*a,**kwargs): kwargs['stdout'].write(b'x'*101); kwargs['stdout'].flush(); return Child()
        with tempfile.TemporaryDirectory() as tmp:
            host=Host(); limits=dict(q.template()['limits'],logBytes=100)
            result=q.run_child(['never-started'],{},Path(tmp)/'cell',limits,host,popen=spawn)
            self.assertTrue(host.killed); self.assertEqual(result['supervisionFailure'],'LOG_BOUND')
            with self.assertRaises(FileExistsError): q.run_child([],{},Path(tmp)/'cell',limits,host,popen=spawn)


if __name__ == '__main__': unittest.main()
