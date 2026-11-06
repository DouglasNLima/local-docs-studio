import test from 'node:test';
import assert from 'node:assert/strict';
import { distribution, validateSample } from '../../scripts/native/ux/report.mjs';

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
  for (const patch of [{foregroundVerified:false}, {screenSha256:null}, {elapsedMs:-2}, {endTick:0}, {status:'PASS'}])
    assert.throws(() => validateSample({...valid, ...patch}));
});
