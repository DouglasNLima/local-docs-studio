# Native performance contract — engineering assessment

Status: **Proposed policy; integration remains NO-GO**. This is the engineering
receipt for [Goal #77](https://github.com/DouglasNLima/local-docs-studio/issues/77)
and [Draft PR #78](https://github.com/DouglasNLima/local-docs-studio/pull/78), targeting
`develop`. [Goal #75](https://github.com/DouglasNLima/local-docs-studio/issues/75)
and [Draft PR #76](https://github.com/DouglasNLima/local-docs-studio/pull/76) retain
their original acceptance consequences.

## Recommendation and authority

The [ADR](../adr/007-native-document-rendering-performance.md) recommends **B+C**:
separate action-to-visible-response, first usable viewport and complete readiness,
with workload/hardware-dependent finite completion budgets. Keep the original
250 ms engineering requirement blocking until explicit owner approval of an exact
policy and independently validated acceptance implementation. It remains a useful
layout-throughput and dispatcher-stall benchmark; it does not directly measure the
foreground editing experience. No proof that 250 ms is impossible is claimed.

The historical protocol has fixed stress fixtures. Extending its deadline to every
document or hardware configuration would be an additional, unvalidated interpretation.
Baseline failure alone does not invalidate an acceptance policy. Equally, no evidence
was found that a universal full-layout deadline was derived from users or a supported
minimum-hardware envelope. Maintaining the engineering benchmark is inexpensive;
commissioning a parity-preserving renderer rewrite has unbounded cost at this stage.

The [SLO specification](native-ux-performance-contract.md) retains 100 ms P95,
250 ms P99 and 500 ms common-preview hypotheses. They are **not approved thresholds**.
No completion budget was reverse-engineered from PR #76. Freeze finite limits only
after independent baseline, supported-hardware and user-task calibration; then run
a predeclared confidence/non-regression campaign. The present observer safety
timeouts and memory limits are not product SLOs.

## Exact sources and scope

| Role | Exact commit | Tree |
|---|---|---|
| Historical product | `0237b08390d5d1d946c0b19b81d671ccd54fb951` | Recorded in each protocol.json |
| Develop product | `99349d82eb06960eb1f9785cf384db2373aed4fa` | `e4d137a12cbdd827583fcb841612890dd88736ec` |
| PR #76 product | `75de42474514af0e7a6e521235a6517f4acad14b` | `6cb88161e24573649321bcb1d9f3418ba128194b` |
| First full comparison harness | `8e7edf073e5ed81f142132deb3237e15f953158d` | `bc1defbc6e33d76e6d692de4c69951df8364eaa6` |

Final documentation/provenance commit, tree and terminal CI receipts are recorded
on PR #78 and Goal #77. A commit cannot contain its own hash; no pre-final hash is
presented as the final documentation commit. Product and harness identities in
machine evidence are independent of the eventual documentation commit.

The product/Core sources, existing validation project, original Continuous scripts,
workflows and fixture bytes are unchanged from exact develop. PR #76 is untouched.
The sole existing Native workflow change excludes actual test-certificate signing
on this branch, as explicitly required by this Goal; mock signing policy checks
remain. No merge, deployment, release, tag, signing or ruleset change was performed.

## Measurement implementation and review

`LensDocsStudio.Native.UxProbe` hosts the actual compiled MainWindow, AvalonEdit,
DocumentPreview, real buffers and local assets. A worker offers Windows SendInput;
Stopwatch/QPC covers the offered input through corresponding state, normal WPF
render-queue processing and a captured foreground desktop frame. UIA visible text,
source-map geometry and current document/version identity reject blank or stale
previews. This is a conservative software-observed upper bound, not physical scan-out.

Typing, caret, selection, Find, scrolling, document switching, all three layouts,
Outline, slash commands/Undo/Redo and comparison are separate action cells. Render-load
typing is offered at construction/attachment markers without first asking the UI
thread whether it is idle. Cancellation switches documents during real construction
and verifies the preserved buffer and replacement preview. Missing historical
features/telemetry are NOT RUN, never zero latency or PASS.

One harness binary is reused across exact products in each comparison job. Fixtures
fix UTF-8/LF/CRLF bytes, structure and media. Processes are independent, orders are
controlled, and normal/actual OS High Contrast are separate. JSONL, JSON, Markdown,
PNG, CPU/allocation/GC/memory, dispatcher intervals, queue gaps and provenance remain
inspectable. Failed children retain partial evidence. Observer-control delays are
excluded from product dispatcher/heartbeat diagnostics.

Review found and corrected fixture checkout conversion, clipped screen bounds, CRLF
caret restoration, premature menu coordinates, ambiguous access-key activation,
comparison activation before readable content, and observer-control contamination.
Earlier FAIL/CANCELLED/PASS collection outcomes remain in GitHub history. Superseded
PASS controls are not retroactively treated as qualified product comparisons.

The ten auditor controls reject absent processes/actions, wrong identities, invalid
or inconsistent clocks, unsupported tail claims and content disagreements. A real
deliberately delayed input control runs after product measurements; collection fails
if the observer omits that blocked interval. Existing fidelity, export, recovery,
conflict, keyboard, visual and accessibility suites remain independent requirements.

## Calibration evidence

The first full campaign is [run 42295050128](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42295050128).
Both exact-develop normal jobs pass collection: 56 fresh child processes and 28
complete logical-content/structure pairs across two distinct host profiles. Each
individual cold/render-load cell has only two samples. The historical runs expose
an observer assumption about the absent live Find counter/F3 route. HC screenshots
show real wheel scrolling while the outer TextEditor offset stays zero; the observer
was subsequently changed to read the rendered TextView and retain both offsets.
Those failed observations remain FAIL, and later runs use a separately identified
harness. Find routes changed from F3 to Enter and then the common visible Next control;
these separately identified protocols are never pooled. HC palette acceptance is independent of this scroll observation.

Illustrative **observed maxima in milliseconds**, all with N=2 per entry, on the
Intel Xeon Platinum 8573C host (2 cores/4 logical processors, image 20261103.298.1,
.NET/WPF 10.0.14), initial baseline-first order:

| Workload | First usable develop / PR #76 | Complete readiness develop / PR #76 | Typing at attachment develop / PR #76 |
|---|---:|---:|---:|
| Small | 621.90 / 538.17 | 1128.35 / 1020.39 | 101.92 / 109.68 |
| Technical | 661.87 / 581.53 | 1175.81 / 1073.39 | 162.74 / 206.88 |
| Long | 912.85 / 907.81 | 1652.92 / 1760.66 | 246.33 / 257.01 |
| Table | 1089.18 / 916.60 | 1972.57 / 1738.95 | 372.71 / 388.31 |
| Nested lists | 1814.10 / 1839.23 | 3296.84 / 3390.64 | 554.50 / 671.11 |
| Mermaid | 1152.19 / 1070.57 | 1644.50 / 1526.20 | 108.24 / 140.51 |
| Mixed | 1054.82 / 1056.79 | 1567.28 / 1627.20 | 129.19 / 153.82 |

The separate initial-candidate-first job ran on AMD EPYC 7763 (2 cores/4 logical
processors), not the same CPU. Its nested-list maxima were 2662.32 / 2646.13 ms
first usable, 4557.27 / 4651.92 ms complete, and 924.35 / 885.14 ms attachment typing.
Do not attribute these differences solely to order or pool them into an SLO. Idle
warm typing maxima were below 100 ms in each normal cohort, while rendering-load
typing was substantially slower. The isolated candidate table-cancellation maximum
of 382.83 ms on the Intel cohort also remains visible; other passing cells cannot
erase it. These observations demonstrate useful distinctions, not non-regression.

The Intel normal cohort recorded maximum queue-delivery gaps of 873.11 / 920.07 ms
and inclusive dispatcher operations of 750.72 / 801.22 ms (develop / PR #76).
Maximum sampled private memory was 393,244,672 / 444,030,976 bytes. The separate AMD
cohort recorded queue gaps of 1538.98 / 1591.60 ms and inclusive operations of
1315.64 / 1382.80 ms. These are observer-inclusive diagnostics, not exclusive
renderer CPU or a certified memory ceiling. Per-action CPU, allocation, GC and
memory samples are retained in the compact receipt and raw reports.

MainWindow's 300 ms preview scheduling and the secondary comparison's 350 ms timer
are additional end-to-end costs omitted by the historical direct-viewer benchmark.
Explicit navigation scheduling is a bounded future experiment; no product timer or
formatter was changed. Complete-readiness numbers include the full audit and actual
distant Find/selection, as defined in the protocol.

Final terminal matrix and source-labelled compact evidence follow the completed
corrected campaign. Reports never turn a pilot collection PASS into UX acceptance.
P95 below 200 observations and P99 below 2,000 are withheld. Repeated input within a
process is clustered; even published descriptive quantiles do not establish an
independent P99-threshold claim. No pooling across products, workload, appearance,
runtime, hardware, action or temperature is authorised.

## High Contrast qualification

The historical **2.542596386:1 FAIL remains unresolved** against the unchanged 4.5:1
requirement. Exact develop's packaged .NET 10.0.13 baseline-first run observed WPF
pressed/text `#FF0078D4 / #FF263B50` while Win32 highlight was `#FF8EE3F0`, before the
screenshot application launched. Other executions yielded 4.528515:1 or 7.893273:1.
All 112 initial-campaign HC process palettes observed 7.893273:1 on .NET/WPF
10.0.14. These have their own process/runtime scope and cannot clear the original
packaged-path failure. They are evidence of variation, not proof of resolution.

ThemeService uses WPF system-colour pairs, and the existing disposable-host helper
broadcasts the actual OS change without persistent registry updates. The evidence
supports investigating WPF colour invalidation/propagation, process ordering and
runtime/environment interaction. It does **not** establish which component owns the
defect. Application correctness is not absolved by a suspected upstream dependency.
No colour substitution, synthetic notification, artificial prewarm or threshold
change was used. Root-cause attribution is INCONCLUSIVE; mandatory accessibility
qualification remains FAIL. An isolated reproduction and message chronology across
the affected runtime/packaged path remain required.

## Governance and integration assessment

The read-only audit found active develop ruleset `26127883` with 30 required contexts.
The standalone Continuous workflow is a mandatory engineering acceptance condition,
but is not itself a named context in that ruleset. The new collection workflow is
also not a required replacement check. No settings were changed.

| Dimension | Verdict | Basis |
|---|---|---|
| PR #76 functional readiness | PASS, scoped | Five completed feature workstreams and their retained exact-source functional receipts; not production certification |
| Proposed UX responsiveness acceptance | INCONCLUSIVE / PENDING | No approved thresholds/hardware/confidence policy; pilot observations cannot certify tails |
| Historical 250 ms benchmark | FAIL / LEGACY QUALIFICATION | Original evidence unchanged; still blocking until explicit approved transition |
| Performance non-regression | INCONCLUSIVE | No predeclared margin or adequately powered equivalence campaign |
| High Contrast/accessibility acceptance | FAIL | Historical 2.542596:1 unresolved; causality INCONCLUSIVE |
| Native production certification | BLOCKED | Trust, installation, physical-machine and screen-reader scopes remain separate; new physical certification NOT RUN |
| Overall PR #76 integration readiness | BLOCKED / NO-GO | Mandatory performance/accessibility requirements remain unmet |

Owner decisions: approve/reject B+C and exact observation boundaries; nominate an
independent reviewer; select minimum hardware/runtime/display support; approve finite
budgets and statistical/non-regression margins after calibration; allocate HC root-cause
work; only then decide whether to retire the original gate's blocking authority.
Any approved transition needs fresh exact-PR #76 qualification and independent
correctness/accessibility PASS. It cannot grant retrospective merge eligibility.

Current collection targets explicit Continuous. Pages/Automatic UX performance,
sustained offered typing loads, export/import cancellation, controlled exhaustion/
recovery, representative user-owned documents, physical display timing and screen-reader
certification are NOT RUN by this probe. These are explicit limits on policy approval,
not removed correctness requirements. The harness's finite supervision bounds do not
certify the product's resource-exhaustion behaviour.

### Retained initial campaign and observer corrections

The [compact first-campaign receipt](evidence/native-ux-contract/initial-campaign-summary.json)
and [uploaded artefact identities](evidence/native-ux-contract/initial-campaign-artifacts.json)
retain all eight cohorts from run 42295050128. Original report hashes were verified
against their integrity sidecars. Of 224 processes, 84 completed and 140 failed:
56 historical counter null references, 83 scroll observer timeouts, and one
`layout-preview` timeout. That last timeout remains an unresolved observation;
it is not silently attributed to scrolling or discarded.

The subsequent four observer checks at `5fa9782` passed develop/normal but failed
historical/normal, historical/HC and develop/HC. The TextView scrolling endpoint
now succeeds in HC. Captured failures show input arriving without editor focus
after a layout transition, plus historical Enter-to-next not establishing the
required selection. The revised probe waits for normal arranged input geometry
and verifies real-click focus before a typing trial. Find navigation uses the
shared visible Next control for every product. The failed historical Enter case
remains retained, separate keyboard evidence; a pointer result cannot certify it.
No product code, correctness endpoint, timeout or acceptance threshold changes.


A subsequent independent observer campaign at `79c983c` retained one HC candidate
focus-setup failure after Preview-to-Editor transition (the other three observer
jobs passed). It is not a timing sample and is not discarded. Pointer setup now
requires actual WPF mouse-over after the native move, targets the editor TextView,
and records hover/capture/geometry on failure. Independent matrix jobs continue
when the auditor controls pass even if an observer job fails; that observer failure
still fails the workflow and prevents qualification. This is evidence collection,
not a retry or acceptance waiver.


Review also tightened cancellation's replacement check to the same current-document,
visible UIA and source-mapped readable geometry used by usable-preview observations.
A switch offered at construction start does not prove every short-document render
was still unfinished when Windows delivered input. Abort acknowledgement/chronology
remains INCONCLUSIVE; the observed switch, preserved buffer and current replacement
are the measured scope. No broader cancellation PASS is claimed.

An unchanged existing selection workflow at `79c983c` also failed independently:
[run 42301217980](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42301217980),
normal PASS / High Contrast FAIL. Pages/natural-preview-wheel could not find visible
selection geometry, with diagnostics enabled and disabled. This workflow tested the
PR #78 product (unchanged develop source), not PR #76. The [retained receipt](evidence/native-ux-contract/selection-observation.json)
preserves source, scenario, report hashes and artefact identities. It is an additional
unresolved correctness observation, separate from the 2.542596:1 colour failure;
no product or existing test was changed to clear it.
