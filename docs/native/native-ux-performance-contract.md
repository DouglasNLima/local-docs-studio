# Native UX performance contract — proposed calibration protocol

**PENDING owner approval; not an active replacement gate.**
[ADR](../adr/007-native-document-rendering-performance.md),
[Goal #77](https://github.com/DouglasNLima/local-docs-studio/issues/77),
[PR #76](https://github.com/DouglasNLima/local-docs-studio/pull/76).

## Three independent clocks

| Dimension | Start | Required end evidence | Candidate target and current authority |
|---|---|---|---|
| Interaction | Stopwatch/QPC immediately before SendInput; pointer placed beforehand | Exact corresponding buffer/caret/selection/active document/UI state, foreground ownership, normal WPF render queue and actual captured desktop response | P95 ≤100 ms, P99 ≤250 ms are hypotheses. No approved limit. |
| First usable preview | Real click selecting preview/split for the exact buffer | Current rendered document identity; visible unmasked content; valid geometry; exact first source-mapped text in viewport; TextPattern; captured foreground pixels; subsequent content/navigation audit must pass | Common-document ≤500 ms hypothesis, subject to baseline and hardware validation. |
| Complete readiness | Same preview action | Complete expected tokens and native structure, all mapped literal layout, diagrams/images, no warnings, exact distant source mapping and actual keyboard Find/visible selection | Finite complexity budgets still require calibration; no unlimited PASS. |

Complete readiness is a conservative **verified upper bound** including audit and
navigation time. Do not subtract estimated observer cost. First viewport is a separate
observation, never a synonym for complete layout. The probe uses the real compiled
MainWindow, model, AvalonEdit and DocumentPreview; it is a validation host, not the
installed application startup path. Show-to-buffer startup is recorded separately
and does not claim first presentation. Software desktop capture is not a photodiode
measurement or physical-machine certification.

The observer does not call UpdateLayout, replace event handlers with direct calls,
rewrite source to improve performance, force GC, prewarm WPF, reset OS caches, or
alter colours. A foreground ownership loss or failed SendInput is a collection FAIL.
`DwmFlush` plus desktop capture and semantic/geometry checks provide software-visible
evidence; normal render-queue observation adds conservative overhead. They do not
claim an exact physical refresh boundary. Screen captures and hashes remain inspectable.

## Action coverage

Typing, caret, selection, Find open/navigation, wheel scrolling, document switching,
Editor/Preview/Split, Outline open/close, slash popup/execution with Undo/Redo, and
comparison activation exercise real Windows keyboard or pointer input. Source setup
is outside action clocks; measured actions never use UIA Invoke or model setters.
Missing baseline-only features are NOT RUN, not latency zero or PASS. Documents that
fit the viewport explicitly mark scrolling NOT APPLICABLE.

Cancellation changes documents during real render construction. An existing renderer
EventSource marker wakes a background input sender, without first querying the busy
UI dispatcher. This avoids sampling only when the UI is idle. The measurement includes
any non-preemptible WPF work, verifies the outgoing buffer, and waits for the replacement
preview. This covers cancellation by document switch, not every export/import operation.
Export/import cancellation, resource exhaustion, physical accessibility and continuous
typing during every rendering phase still require independent coverage before approval.

Idle repeated interaction and interruption during rendering must remain separate
strata. Closed-loop input after an observed response is not an open-loop typing-storm
test. Do not use its latency distribution to claim responsiveness under all offered
loads. No new deferred renderer or cancellation behaviour is introduced here.

## Workload catalogue and proposed envelope

`tests/fixtures/native-ux/manifest.json` fixes exact UTF-8 byte hashes, LF/CRLF,
expected text markers and native list/table counts. `create-fixtures.mjs` is a
deterministic source generator, never a substitute for reviewed committed fixtures.

| Class | Initial representative workload | Budget calibration evidence needed |
|---|---|---|
| Small prose | 12 formatted paragraphs, Unicode | Common first-viewport hypothesis; repeat on minimum supported hardware |
| Typical technical | 80 sections, 160 list items | Reading/editing journeys and baseline distribution |
| Long | 1,200 formatted paragraphs | Complete readiness, distant navigation and memory slope |
| Large table | 1,000 data rows × 2 cells | Row/cell counts, layout, selection and resize |
| Nested lists | 4,000 list items across three levels | Nesting/marker cost, navigation and UIA hierarchy |
| Mermaid | 60 connected edges / 61 nodes | Graph complexity, parsing/layout, warnings and rendered media |
| Mixed | 150 list items, 100 table rows, local image and flowchart | Combined cold media, layout and memory effects |

These are additional workloads; the original six are unchanged. They are not yet a
representative user corpus. Add independently sourced, consented realistic examples
and explicit boundary/oversize cases before freezing a supported envelope. Byte size
alone is insufficient: record actual mapped literals, native elements, cells, nesting,
graph structure and decoded image sizes. Do not categorise a failing candidate into
a slower class after seeing its result.

Numeric completion budgets must be frozen **before** acceptance: proposed procedure
is (1) owner selects minimum hardware, (2) measure exact historical and develop
baselines independently of candidate, (3) identify a finite baseline upper confidence
bound per class, (4) test user tolerability and product cancellation/resource behaviour,
(5) owner approves the stricter tolerable limit or commissions a renderer change.
The current candidate cannot be the sole budget-setting input. A 30-second observer
timeout is a collection failure boundary, **not** a proposed UX SLO.

## Samples, confidence and comparisons

Run each fixture/product in independent fresh processes, both product orders on
separate hosted jobs, normal and actual OS High Contrast. Keep first render and first
interaction, then label subsequent operations warm. Fresh process means process-cold;
it does not imply disk/OS-cold. First-process order on each runner is retained.
Never discard samples, trim outliers, or rerun into green.

The PR smoke profile uses two processes × three cycles per cell to validate the
observer, **not tail compliance**. The manual calibration profile defaults to three
processes × 21 cycles. Report N, process count, P50 and maximum. P95 is withheld below
200 observations, P99 below 2,000, and any reported quantile is descriptive only.
Observations within a process share heaps/caches and are not independent. Do not pool
products, hardware images, runtime versions, appearances, temperatures or actions.

For formal acceptance use a predeclared independent-unit test. With zero failures,
the one-sided 95% binomial upper bound for exceedance is `1 - 0.05^(1/n)`:
59 independent trials are needed to bound a 5% exceedance rate; 299 for 1%.
Use at least **300 independently launched process trials per action/workload/appearance**
for a P99-threshold claim, selecting one predeclared ordinal per process, not the
best one. Correlated warm distributions additionally need at least 2,000 observations
and process-level resampling/confidence intervals before estimating tail effects.
Multiple comparisons need a predeclared family-wise error policy; these per-cell
numbers alone do not certify the entire product. Non-zero failures need exact
binomial bounds; do not use the zero-failure shortcut. No formal PASS is emitted by
the pilot report generator.

Performance non-regression needs paired controlled orders, a predeclared meaningful
absolute/relative margin, confidence intervals on process-level differences, and
correctness parity. Similar failure counts or lower medians are insufficient. Owner
must approve the margin before qualification; until then report INCONCLUSIVE.

## Resource and diagnostic bounds

Each action has a 30-second observation deadline. The supervisor bounds each child
to 240 seconds and 2 GiB private bytes. It retains partial JSONL and terminal exit,
then continues independent cells. These are test safety bounds, not product failure
handling or permission to discard user data. They are deliberately finite and must
be reported as FAIL if reached. Product memory/exhaustion/recovery behaviour is
not certified by killing a disposable probe.

Record process CPU, managed allocation deltas, natural GC counts, managed/private/
working-set memory, Stopwatch frequency, binary hashes, revision/tree, runtime/WPF,
OS/image/processors, fixture hashes, geometry and appearance. Counters include the
host and observer; do not label them exclusive renderer CPU/allocation or leak proof.
Dispatcher operation intervals ≥50 ms are bounded to 2,048; nested inclusive intervals
are not additive. A 16 ms off-thread queue probe records delivery gaps separately.
Missing/overflowed diagnostics invalidate collection rather than imply zero stalls.
Screen files are bounded; successful and failed child evidence is uploaded for 30 days.
Longer-term source-labelled compact evidence belongs in the repository receipt.

## Correctness and accessibility coexistence

This probe adds exact source/buffer preservation, expected content/structure, native
mapping/geometry, actual navigation/selection and screen evidence. It does not replace
the existing source/UTF-8, rich text, images, Mermaid, clipboard/export, Save/recovery/
conflict, keyboard, visual and semantic regression suites. Their tests remain intact.
Retain their exact-source CI receipts beside the new report. A layout difference,
omitted content, failed diagram or weakened accessibility disqualifies improvement.

Actual OS High Contrast uses the existing disposable-host helper. Initial OS/WPF/
pressed colours are recorded without prewarming; the separate 4.5:1 requirement
stays unchanged. Passing probe colours do not resolve the historical 2.542596:1 FAIL.
The original pre-assertion and installed/physical accessibility scopes remain distinct.

## Running and reviewing

Local safe checks:

```powershell
dotnet build src/native/LensDocsStudio.Native.UxProbe -c Release -p:RestoreLockedMode=true
node --test tests/unit/native-ux-report.test.mjs
npm run test:static
```

Foreground execution is restricted to disposable GitHub-hosted Windows. Use
`Native UX performance evidence (proposed policy)` on the proposed branch. The script
builds one probe DLL, overlays exact product binaries into separate directories and
verifies product/harness hashes. Actual source revision is checked again in-process.
Reports are JSON, accessible Markdown tables, PNG captures and raw JSONL; timestamps
and missing cells remain inspectable. A successful workflow is collection PASS only.

The original workflow name and 250 ms threshold remain unchanged. This branch changes
only the actual signing step's branch guard in the Native regression workflow, because
Goal #77 explicitly forbids signing even a disposable test certificate. Mock signing
policy checks remain allowed and do not sign anything. No required-check/ruleset,
deployment, release, tag or merge action is authorised.

Read-only settings audit on 6 November 2026 found active develop ruleset `26127883`
with 30 required status contexts, strict status checks and review-thread resolution.
The standalone Continuous workflow and the new UX workflow are not named required
contexts in that ruleset; the historical 250 ms requirement remains a mandatory
engineering acceptance policy and NO-GO condition. Do not confuse that policy with
GitHub enforcement. The legacy branch-protection endpoint returns 404 because this
repository uses rulesets; that does not mean develop lacks protection. No settings
were changed. Any future transition must explicitly reconcile policy and enforced
check names through owner review, not silently rely on a green collection job.
