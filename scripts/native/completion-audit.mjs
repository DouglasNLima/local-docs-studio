import {readFileSync, writeFileSync, readdirSync, existsSync} from 'node:fs';
import {join, resolve, basename, dirname} from 'node:path';
import {pathToFileURL} from 'node:url';

export function qualifyInterruption(verdict, trace) {
  if (verdict.staleReady > 0) return 'FAIL';
  const qualified = verdict.deliveries.filter(d => {
    const o = verdict.operations.find(o => o.id === d.operation && o.reader === d.reader);
    return o && d.pendingTask && d.sameCancellation && !d.cancellationRequested && o.start <= d.tick && o.end > d.tick
      && (d.action !== 'close' || d.inputTarget === 'CloseComparison')
      && trace.some(e => e.boundary === 'native-input' && e.offered === d.operation && e.tick <= d.tick
        && (d.action === 'close' ? e.message === 0x202 : [0x100,0x104].includes(e.message) && e.key === (d.action === 'switch' ? 9 : 27)));
  });
  if (!qualified.length) return 'INCONCLUSIVE';
  return qualified.every(d => {
    const o = verdict.operations.find(o => o.id === d.operation);
    return o.cancelled >= d.tick && o.cancelled <= o.end && o.outcome === 'rejected';
  }) ? 'PASS' : 'FAIL';
}

export function checkPresentation(state) {
  const failures = [];
  if (state.previewStatus !== state.statusText || state.statusText !== state.statusUia) failures.push('status model/WPF/UIA mismatch');
  for (const name of ['primary','secondary']) {
    const reader = state[name];
    if (!reader.isVisible) continue;
    if (!reader.currentOwner || !reader.attachedMatches || reader.itemStatus !== 'Ready' || !reader.isMeasureValid || !reader.isArrangeValid)
      failures.push(name + ': current Ready geometry/ownership missing');
    if (reader.source === null || !reader.textPattern?.some(r => r.meaningful && r.rectangles.length >= 4)) failures.push(name + ': visible mapped UIA text missing');
    if (reader.mode !== 'Scroll' && (reader.pageNumber !== reader.nativePage || reader.pageCount !== reader.nativeCount
      || reader.pageText !== `Page ${reader.pageNumber} of ${reader.pageCount}` || reader.pageUia !== reader.pageText)) failures.push(name + ': page-counter mismatch');
  }
  return failures;
}

function files(root, name) {
  return readdirSync(root, {withFileTypes:true}).flatMap(e => e.isDirectory() ? files(join(root,e.name),name) : e.name===name ? [join(root,e.name)] : []);
}
const read = path => JSON.parse(readFileSync(path,'utf8').replace(/^\uFEFF/,''));
export function audit(root) {
  const processes=read(join(root,'processes.json'));
  const pending=[], semantic=[], pixels=[];
  for (const file of files(root,'presentation-completion.json')) {
    const result=read(file);
    if (result.instrumented && result.scenario!=='pixels') {
      const trace=readFileSync(join(dirname(file),'completion-trace.jsonl'),'utf8').trim().split(/\r?\n/).map(JSON.parse);
      pending.push({file,scenario:result.scenario,reader:result.target,verdict:qualifyInterruption(result.verdict,trace)});
    }
  }
  for (const file of files(root,'completion-captures.json')) {
    const captures=read(file);
    const actions=[...new Set(captures.map(c=>c.action))];
    for (const action of actions) if (captures.filter(c=>c.action===action).map(c=>c.frameIndex).join(',')!=='0,1,2') semantic.push({file,action,failures:['not exactly three fixed frames']});
    for (const capture of captures) for (const boundary of ['before','after']) {
      const failures=checkPresentation(capture[boundary]);
      if (failures.length) semantic.push({file,action:capture.action,frame:capture.frameIndex,boundary,failures});
    }
  }
  const ocr=existsSync(join(root,'pixel-readings.json'))?read(join(root,'pixel-readings.json')):null;
  for (const row of ocr?.readings??[]) if (!row.match) pixels.push(row);
  const collection=processes.length===15 && processes.every(p=>p.exitCode===0 && !p.supervisionFailure) ? 'PASS':'FAIL';
  const pendingResult=pending.length===6 && pending.every(p=>p.verdict==='PASS')?'PASS':pending.some(p=>p.verdict==='FAIL')?'FAIL':'INCONCLUSIVE';
  const presentationResult=semantic.length?'FAIL':!ocr?.readings?.length||pixels.length?'INCONCLUSIVE':'PASS';
  return {collection,pendingInterruption:pendingResult,presentation:presentationResult,pending,semantic,pixelReview:pixels,
    pixelReadings:ocr?.readings?.length??0,verdict:collection==='PASS'&&pendingResult==='PASS'&&presentationResult==='PASS'?'PASS':'NO-GO',
    scope:'Finite observation, not universal intermittent-failure elimination; OCR mismatches require independent review and are never silently accepted.'};
}
if (process.argv[1] && import.meta.url===pathToFileURL(resolve(process.argv[1])).href) {
  const root=resolve(process.argv[2]); const result=audit(root);
  writeFileSync(join(root,'completion-audit.json'),JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({collection:result.collection,pending:result.pendingInterruption,presentation:result.presentation,verdict:result.verdict}));
  process.exitCode=result.verdict==='PASS'?0:1;
}
