import test from 'node:test';
import assert from 'node:assert/strict';
import { distribution, validateSample, buildReport } from '../../scripts/native/ux/report.mjs';
import { readFileSync, writeFileSync, mkdtempSync, mkdirSync, rmSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

test('committed UX fixtures preserve exact UTF-8, line endings and local assets', () => {
  const root = new URL('../fixtures/native-ux/', import.meta.url);
  const manifest = JSON.parse(readFileSync(new URL('manifest.json', root), 'utf8'));
  const hash = b => createHash('sha256').update(b).digest('hex');
  for (const fixture of manifest.fixtures) {
    const bytes = readFileSync(new URL(fixture.file, root));
    assert.equal(hash(bytes), fixture.sha256);
    const text = new TextDecoder('utf-8', { fatal: true }).decode(bytes);
    assert.equal(text.includes('\r\n'), fixture.lineEnding === 'CRLF');
  }
  assert.equal(hash(readFileSync(new URL('pixel.png', root))), manifest.assetSha256);
});

test('native UX withholds unsupported tails and retains the maximum', () => {
  const result = distribution([10, 20, 900], 3);
  assert.equal(result.p50, 20); assert.equal(result.maximum, 900);
  assert.equal(result.p95, null); assert.equal(result.p99, null);
  assert.ok(result.bestPossibleIndependentZeroFailureUpper95 > .6);
});
test('repeated input samples do not become independent tail evidence', () => {
  const result = distribution(Array.from({length: 2000}, (_, i) => i + 1), 2);
  assert.equal(result.p95, 1900); assert.equal(result.p99, 1980);
  assert.equal(result.independentProcesses, 2);
  assert.ok(result.bestPossibleIndependentZeroFailureUpper95 > .7);
});
test('missing measurements are unknown and malformed timings are rejected', () => {
  assert.equal(distribution([], 0).maximum, null);
  for (const value of [-1, NaN, Infinity]) assert.throws(() => distribution([value], 1));
});
test('a semantic handler response without foreground screen evidence is rejected', () => {
  const valid = { kind: 'sample', status: 'OBSERVED', foregroundVerified: true, screenSha256: 'A'.repeat(64), elapsedMs: 25, startTick: 1, endTick: 2 };
  validateSample(valid);
  for (const patch of [{foregroundVerified:false}, {screenSha256:null}, {elapsedMs:-2}, {endTick:0}, {status:'PASS'}, {semanticTick:3}])
    assert.throws(() => validateSample({...valid, ...patch}));
});

function reportFixture(t) {
  // Synthetic evidence tests the auditor only; it never represents product timing.
  const root = mkdtempSync(join(tmpdir(), 'native-ux-auditor-'));
  t.after(() => rmSync(root, {recursive:true, force:true}));
  const write = (file, value) => writeFileSync(join(root, file), JSON.stringify(value));
  const hash = 'A'.repeat(64), revision = 'b'.repeat(40);
  const product = { revision, productSha256:hash, coreSha256:hash };
  write('protocol.json', {processes:1, iterations:1, fixtures:['small'], fixtureIdentities:[{id:'small',sha256:hash.toLowerCase()}], products:{baseline:product,candidate:product}, harnessSha256:hash, appearance:'normal'});
  write('processes.json', ['baseline','candidate'].map(side => ({process:0,fixture:'small',side,exitCode:0})));
  for (const side of ['baseline','candidate']) {
    const prefix = `samples/0/small/${side}`; mkdirSync(join(root,prefix), {recursive:true});
    write(`${prefix}/identity.json`, {fixture:'small',sha256:hash,productRevision:revision,product:{sha256:hash},core:{sha256:hash},harness:{sha256:hash},highContrast:false,runtime:'test',wpf:{sha256:hash},stopwatchFrequency:1000,capabilities:{slash:false,comparison:false}});
    write(`${prefix}/completion.json`, {collection:'PASS',exactSource:'PASS',completeContent:'PASS',intervalOverflow:false,heartbeatMaximumMs:0,dispatcherIntervals:[]});
    const actions = ['typing','caret','selection','find-open','find-navigation','document-switch','outline-open','outline-close','layout-editor','layout-preview','layout-split','first-usable-preview','complete-readiness','cancel-render-by-switch','observer-delay-control','scroll'];
    const records = actions.map(action => ({kind:'sample',action,status:'OBSERVED',foregroundVerified:true,screenSha256:hash,elapsedMs:25,startTick:1,endTick:26,temperature:'test',facts:action === 'complete-readiness' ? {logicalTextSha256:hash,counts:{Paragraph:12},mappedLiterals:12,media:0}:undefined}));
    records.push({kind:'coverage',action:'slash',status:'NOT RUN'}, {kind:'coverage',action:'comparison-switch',status:'NOT RUN'},
      {kind:'observer-control',status:'PASS',delayMs:200}, {kind:'cancellation',status:'PASS',originalBufferPreserved:true,stalePreviewRejected:true});
    writeFileSync(join(root,prefix,'samples.jsonl'),records.map(r=>JSON.stringify(r)).join('\n')+'\n');
  }
  return {root,write,read:file=>JSON.parse(readFileSync(join(root,file),'utf8'))};
}
test('complete collection remains pending policy approval and cannot grant non-regression', t => {
  const {root}=reportFixture(t), report=buildReport(root);
  assert.equal(report.collection,'PASS'); assert.equal(report.nativeUxAcceptance,'PENDING');
  assert.equal(report.correctnessPairs[0].verdict,'PASS'); assert.match(report.nonRegression,/INCONCLUSIVE/);
});
test('missing independent processes cannot be silently dropped', t => {
  const {root,write,read}=reportFixture(t);
  write('processes.json',read('processes.json').slice(1));
  assert.equal(buildReport(root).collection,'FAIL');
});
test('wrong binary identity and absent candidate features invalidate collection', t => {
  const {root,write,read}=reportFixture(t), path='samples/0/small/candidate/identity.json', id=read(path);
  id.product.sha256='C'.repeat(64); id.capabilities={slash:true,comparison:true}; write(path,id);
  const report=buildReport(root);
  assert.equal(report.collection,'FAIL'); assert.ok(report.failures.some(f=>f.includes('artefact identity')));
  assert.ok(report.failures.some(f=>f.includes('missing slash'))); assert.ok(report.failures.some(f=>f.includes('comparison activation')));
});
test('exact content disagreement and false monotonic duration are rejected', t => {
  const {root}=reportFixture(t), file=join(root,'samples/0/small/candidate/samples.jsonl');
  const records=readFileSync(file,'utf8').trim().split('\n').map(JSON.parse);
  records.find(r=>r.action==='complete-readiness').facts.counts.Paragraph=11;
  records.find(r=>r.action==='typing').elapsedMs=0;
  writeFileSync(file,records.map(r=>JSON.stringify(r)).join('\n')+'\n');
  const report=buildReport(root);
  assert.equal(report.collection,'FAIL'); assert.equal(report.correctnessPairs[0].verdict,'FAIL');
  assert.ok(report.failures.some(f=>f.includes('monotonic timestamps')));
});
