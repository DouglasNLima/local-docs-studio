import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { createHash } from 'node:crypto';

export function distribution(values, independentProcesses) {
  if (!values.length) return { count: 0, p50: null, p95: null, p99: null, maximum: null, independentProcesses };
  if (values.some(v => !Number.isFinite(v) || v < 0)) throw new Error('Non-finite or negative latency');
  const sorted = [...values].sort((a, b) => a - b);
  const q = p => sorted[Math.ceil(p * sorted.length) - 1];
  return { count: sorted.length, p50: q(.5), p95: values.length >= 200 ? q(.95) : null,
    p99: values.length >= 2000 ? q(.99) : null, maximum: sorted.at(-1), independentProcesses,
    tails: 'Empirical descriptive quantiles only; repeated samples are clustered by process. Null means insufficient observations.',
    bestPossibleIndependentZeroFailureUpper95: independentProcesses ? 1 - Math.pow(.05, 1 / independentProcesses) : null };
}
export function validateSample(sample) {
  if (sample.kind !== 'sample' || sample.status !== 'OBSERVED' || sample.foregroundVerified !== true ||
      typeof sample.screenSha256 !== 'string' || !/^[0-9A-F]{64}$/.test(sample.screenSha256) ||
      !Number.isFinite(sample.elapsedMs) || sample.elapsedMs < 0 || !Number.isFinite(sample.startTick) ||
      !Number.isFinite(sample.endTick) || sample.endTick < sample.startTick)
    throw new Error('Invalid action-to-screen evidence');
  if (sample.semanticTick !== undefined && (!Number.isFinite(sample.semanticTick) || sample.semanticTick < sample.startTick || sample.semanticTick > sample.endTick))
    throw new Error('Invalid semantic observation clock');
}
export function buildReport(root) {
  const read = p => JSON.parse(readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
  const protocol = read(join(root, 'protocol.json'));
  const processRuns = read(join(root, 'processes.json'));
  const runs = Array.isArray(processRuns) ? processRuns : [processRuns];
  const groups = new Map(), failures = [], coverage = [], appearances = [], identities = [], diagnostics = [], facts = new Map(), correctnessPairs = [];
  const expected = protocol.processes * protocol.fixtures.length * 2;
  if (runs.length !== expected) failures.push(`Expected ${expected} independent process outcomes; received ${runs.length}`);
  const seen = new Set();
  for (const run of runs) {
    const key = `${run.process}/${run.fixture}/${run.side}`;
    if (!['baseline','candidate'].includes(run.side) || !protocol.fixtures.includes(run.fixture) || !Number.isInteger(run.process) || run.process < 0 || run.process >= protocol.processes)
      { failures.push(`Unknown process cell: ${key}`); continue; }
    if (seen.has(key)) { failures.push(`Duplicate process cell: ${key}`); continue; } seen.add(key);
    const dir = join(root, 'samples', key);
    if (run.exitCode !== 0 || run.supervisionFailure || !existsSync(join(dir, 'completion.json'))) failures.push(`${key}: failed or incomplete child`);
    if (!existsSync(join(dir, 'identity.json'))) { failures.push(`${key}: missing identity`); continue; }
    const id = read(join(dir, 'identity.json')); identities.push({ key, ...id });
    if (id.fixture !== run.fixture || id.sha256.toLowerCase() !== protocol.fixtureIdentities.find(f => f.id === run.fixture).sha256)
      failures.push(`${key}: fixture identity mismatch`);
    if (id.productRevision !== protocol.products[run.side].revision || id.product.sha256 !== protocol.products[run.side].productSha256 ||
        id.core.sha256 !== protocol.products[run.side].coreSha256 || id.harness.sha256 !== protocol.harnessSha256 ||
        id.highContrast !== (protocol.appearance === 'high-contrast')) failures.push(`${key}: artefact identity mismatch`);
    if (existsSync(join(dir, 'appearance.json'))) appearances.push({ key, ...read(join(dir, 'appearance.json')) });
    const counts = new Map(), records = [];
    if (existsSync(join(dir, 'samples.jsonl'))) {
      for (const line of readFileSync(join(dir, 'samples.jsonl'), 'utf8').trim().split(/\r?\n/).filter(Boolean)) {
        const sample = JSON.parse(line);
        records.push(sample);
        if (sample.kind !== 'sample') { coverage.push({ key, ...sample }); continue; }
        try { validateSample(sample); } catch (error) { failures.push(`${key}: ${error.message}`); continue; }
        if (!Number.isFinite(id.stopwatchFrequency) || Math.abs(sample.elapsedMs - 1000 * (sample.endTick - sample.startTick) / id.stopwatchFrequency) > 1)
          { failures.push(`${key}: elapsed time differs from monotonic timestamps`); continue; }
        if (sample.action === 'complete-readiness') facts.set(key, sample.facts);
        counts.set(sample.action, (counts.get(sample.action) ?? 0) + 1);
        const group = `${run.side}/${run.fixture}/${sample.action}/${sample.temperature}`;
        if (!groups.has(group)) groups.set(group, { samples: [], processes: new Set() });
        groups.get(group).samples.push(sample.elapsedMs); groups.get(group).processes.add(run.process);
      }
    }
    for (const action of ['typing','caret','selection','find-open','find-navigation','document-switch','outline-open','outline-close','layout-editor','layout-preview','layout-split'])
      if (counts.get(action) !== protocol.iterations) failures.push(`${key}: missing or extra ${action} observations`);
    for (const action of ['first-usable-preview','complete-readiness','cancel-render-by-switch','observer-delay-control'])
      if (counts.get(action) !== 1) failures.push(`${key}: missing or extra ${action} observation`);
    const scrollOmissions = records.filter(r => r.kind === 'coverage' && r.action === 'scroll' && r.status === 'NOT APPLICABLE').length;
    if ((counts.get('scroll') ?? 0) + scrollOmissions !== protocol.iterations) failures.push(`${key}: incomplete scrolling coverage`);
    if (id.capabilities?.slash === true) for (const action of ['slash-open','slash-execute']) {
      if (counts.get(action) !== 1) failures.push(`${key}: missing ${action}`);
    }
    else if (id.capabilities?.slash !== false || !records.some(r => r.kind === 'coverage' && r.action === 'slash' && r.status === 'NOT RUN')) failures.push(`${key}: unaccounted slash coverage`);
    if (id.capabilities?.comparison === true) {
      if (counts.get('comparison-switch') !== 1) failures.push(`${key}: missing comparison activation`);
    }
    else if (id.capabilities?.comparison !== false || !records.some(r => r.kind === 'coverage' && r.action === 'comparison-switch' && r.status === 'NOT RUN')) failures.push(`${key}: unaccounted comparison coverage`);
    if (!records.some(r => r.kind === 'observer-control' && r.status === 'PASS' && r.delayMs >= 180)) failures.push(`${key}: delayed-response observer control absent or failed`);
    if (!records.some(r => r.kind === 'cancellation' && r.status === 'PASS' && r.originalBufferPreserved === true && r.stalePreviewRejected === true)) failures.push(`${key}: cancellation correctness absent or failed`);
    if (existsSync(join(dir, 'completion.json'))) {
      const completion = read(join(dir, 'completion.json'));
      if (completion.collection !== 'PASS' || completion.exactSource !== 'PASS' || completion.completeContent !== 'PASS' || completion.intervalOverflow) failures.push(`${key}: incomplete correctness or bounded diagnostics`);
      diagnostics.push({ key, heartbeatMaximumMs: completion.heartbeatMaximumMs, dispatcherIntervals: completion.dispatcherIntervals,
        samples: records.filter(r => r.kind === 'sample').map(r => ({ action: r.action, temperature: r.temperature,
          cpuMs: r.cpuMs, allocations: r.allocations, privateBytes: r.privateBytes, workingSet: r.workingSet, managedBytes: r.managedBytes, gc: r.gc })) });
    }
  }
  for (const fixture of protocol.fixtures) for (let process = 0; process < protocol.processes; process++) {
    const pair = identities.filter(id => id.key.startsWith(`${process}/${fixture}/`));
    if (pair.length === 2 && (pair[0].runtime !== pair[1].runtime || pair[0].wpf.sha256 !== pair[1].wpf.sha256))
      failures.push(`${process}/${fixture}: runtime/WPF identity mismatch between products`);
    const baseline = facts.get(`${process}/${fixture}/baseline`), candidate = facts.get(`${process}/${fixture}/candidate`);
    const equal = baseline && candidate && JSON.stringify(baseline) === JSON.stringify(candidate);
    correctnessPairs.push({ process, fixture, verdict: !baseline || !candidate ? 'INCONCLUSIVE' : equal ? 'PASS' : 'FAIL', baseline, candidate });
    if (baseline && candidate && !equal) failures.push(`${process}/${fixture}: complete logical content/structure differs between products`);
  }
  const summaries = [...groups].map(([cell, group]) => ({ cell, ...distribution(group.samples, group.processes.size) }));
  const report = { schema: 1, collection: failures.length ? 'FAIL' : 'PASS', nativeUxAcceptance: 'PENDING',
    proposedContractVerdict: 'INCONCLUSIVE', historicalFullLayout: 'FAIL / LEGACY QUALIFICATION', historicalGateRemainsBlocking: true,
    highContrast: appearances.some(a => a.verdict === 'FAIL') ? 'FAIL' : 'INCONCLUSIVE; historical failure unresolved',
    nonRegression: 'INCONCLUSIVE; pilot observations are not an equivalence test', protocol, failures, summaries, coverage, appearances, identities, diagnostics, correctnessPairs,
    limits: ['No approved policy or supported-hardware envelope', 'No physical scan-out or screen-reader certification',
      'Probe allocations and CPU include observers; no exclusive renderer attribution', 'No pooling across host, runtime, appearance, fixture, action or temperature',
      'No absent feature is scored as PASS; no percentile from a failed/missing child clears acceptance'] };
  writeFileSync(join(root, 'report.json'), JSON.stringify(report, null, 2) + '\n');
  const fmt = v => v === null ? 'INCONCLUSIVE' : v.toFixed(2);
  const lines = ['# Native UX calibration evidence', '', `Collection: **${report.collection}**. Native UX Performance Acceptance: **PENDING**.`, '',
    'Historical Full-Layout Benchmark: **FAIL / LEGACY QUALIFICATION**. It remains required and blocking.', '',
    `Harness source: \`${protocol.harnessRevision}\`; tree: \`${protocol.harnessTree}\`.`, '',
    `Baseline: \`${protocol.products.baseline.revision}\`; candidate: \`${protocol.products.candidate.revision}\`.`, '',
    `Appearance: ${protocol.appearance}; order: ${protocol.order}. Software-observed upper bounds include observation overhead.`, '',
    '| Cell (product / workload / action / temperature) | N | Processes | P50 ms | P95 ms | P99 ms | Max ms |',
    '|---|---:|---:|---:|---:|---:|---:|',
    ...summaries.map(s => `| ${s.cell} | ${s.count} | ${s.independentProcesses} | ${fmt(s.p50)} | ${fmt(s.p95)} | ${fmt(s.p99)} | ${fmt(s.maximum)} |`), '',
    'P95 requires at least 200 observations and P99 at least 2,000. These are descriptive only; clustered samples do not establish independent tail compliance.', '',
    '## Dispatcher delivery diagnostics', '', '| Process cell | Maximum queue delay ms | Inclusive operations ≥50 ms |', '|---|---:|---:|',
    ...diagnostics.map(d => `| ${d.key} | ${fmt(d.heartbeatMaximumMs ?? null)} | ${d.dispatcherIntervals?.length ?? 'UNKNOWN'} |`), '',
    'CPU, allocations, private/working-set/managed memory and GC deltas are retained by action in report.json and the raw journal. They include observation work. Inclusive dispatcher intervals must not be summed.', '',
    '## Complete content comparison', '', ...correctnessPairs.map(p => `- Process ${p.process}, ${p.fixture}: ${p.verdict} (logical text, native structure, mapping count and media; not a substitute for raster/accessibility suites).`), '',
    '## Collection failures', '', ...(failures.length ? failures.map(f => `- ${f}`) : ['None. This is not acceptance PASS.']), '',
    '## Limits', '', ...report.limits.map(l => `- ${l}`), ''];
  writeFileSync(join(root, 'report.md'), lines.join('\n'));
  const reportHash = createHash('sha256').update(readFileSync(join(root, 'report.json'))).digest('hex');
  writeFileSync(join(root, 'report-integrity.json'), JSON.stringify({ reportSha256: reportHash, source: protocol.harnessRevision, tree: protocol.harnessTree }) + '\n');
  return report;
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const report = buildReport(resolve(process.argv[2]));
  console.log(`Collection ${report.collection}; UX acceptance PENDING; historical gate unchanged; ${report.failures.length} collection failures.`);
  if (report.failures.length) process.exitCode = 1;
}
