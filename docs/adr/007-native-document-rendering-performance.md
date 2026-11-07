# ADR — Native Document Rendering Performance & Responsiveness Contract

Status: **Proposed — formal approval pending**. Owner: repository maintainer
DouglasNLima. Created 6 November 2026; decision-readiness continuation 7 November 2026.
The owner [supports B+C in principle](https://github.com/DouglasNLima/local-docs-studio/issues/77#issuecomment-6713735024).
That agreement does not accept this ADR, numeric limits, support envelope,
statistical rules, gate migration, integration or release. Formal approval must
name the exact ADR commit and individual decisions below on Goal #77 / Draft PR #78.

Current entry point: [Native Acceptance Decision Package](../native/native-acceptance-decision-package.md),
with independent integration/release contracts, [risk register](../native/native-acceptance-risk-register.md)
and [PR #76 review index](../native/pr76-review-index.md). Earlier measurements below
retain their original identities; they are not measurements of current PR #76.

Related: [Goal #75](https://github.com/DouglasNLima/local-docs-studio/issues/75),
[Draft PR #76](https://github.com/DouglasNLima/local-docs-studio/pull/76),
[exact investigation](https://github.com/DouglasNLima/local-docs-studio/blob/75de42474514af0e7a6e521235a6517f4acad14b/docs/native/wpf-first-layout-recovery.md).

## Decision proposed

Adopt **B + C**: independently qualify foreground interaction, first usable preview
and complete readiness, with empirically calibrated complexity and hardware strata.
Keep **A** unchanged, including its present release and campaign acceptance authority,
until an owner approves a separately validated transition. Repository governance
already excludes Continuous 250 ms from ordinary integration protection; this ADR
must not extend that gate to every feature PR or remove the current #75–#77 NO-GO.
The decision package reconciles those scopes explicitly. Do not implement **D**, ship a WPF fork,
or make release planning depend on **E**. This decision is not a waiver for PR #76.

Maintaining the 250 ms benchmark is inexpensive and useful for engineering. Requiring
every complete rich document to meet it is not presently justified as the sole UX
release measure: no supported-hardware envelope, latency distribution or user study
supporting that universal deadline was located. Retiring its blocking authority
*before* better evidence exists would also be unjustified. Feasibility is unresolved,
not mathematically impossible; no economic case for an open-ended formatter rewrite
has been demonstrated.

## Requirement and history

The reconstruction brief asks for responsiveness, large-document handling and measured
claims. It does not specify 250 ms. Later performance protocols explicitly require
250 ms construction/input/cancellation and complete first-layout input, all content,
native semantics and distant navigation. Those later requirements are authoritative
acceptance policy today. The original six-fixture protocol retains first execution,
three fresh processes per fixture, four repeated constructions and three fresh-source
changes: 66 observations per product per replication. All candidate observations
must pass; a favourable median cannot clear that maximum-based gate.

The benchmark queues Dispatcher Input, assigns a complete FlowDocument to a private
`StageViewer : FlowDocumentScrollViewer`, calls Measure/Arrange/UpdateLayout, and
observes the callback. It bypasses MainWindow and DocumentPreview and uses a hidden
HWND. Thus it is primarily a **complete-layout throughput and dispatcher-stall
benchmark**. Its input-gap component is relevant to responsiveness, but it is not
a measurement of typing or usable foreground presentation. It cannot alone certify
all of those experiences. It also embodies the stock FlowDocument presentation
architecture; an alternative renderer cannot honestly pass it under another name.

The combination of complete stress-fixture content, stock bottomless WPF layout, identical
visual/selection/UIA semantics, cold runtimes, unspecified minimum hardware and a single 250 ms
maximum has not been established as feasible. Individual responsiveness and fidelity
requirements remain valuable. No requirement is removed because it is difficult.

## Architecture and exact evidence

Verified for the original 6 November investigation: `develop` was `99349d82eb06960eb1f9785cf384db2373aed4fa`, tree
`e4d137a12cbdd827583fcb841612890dd88736ec`; PR #76 was Draft at
`75de42474514af0e7a6e521235a6517f4acad14b`, tree
`6cb88161e24573649321bcb1d9f3418ba128194b`. Original comparison baseline:
`0237b08390d5d1d946c0b19b81d671ccd54fb951`. These are fixed inputs, not moving refs.

Native uses .NET 10/WPF, AvalonEdit, Markdig and Mermaider 0.15.1. Parsing and media
preparation can run away from the UI dispatcher. Native document construction is
cooperative; complete objects are attached on the UI thread. Continuous WPF layout
is synchronous and bottomless. Existing optimisation already includes parent-first
attachment, Run coalescing and batched table construction. The list fixture contains
4,000 ListItems, 4,000 Paragraphs and 4,000 Runs in one native List. Automatic mode
may choose Pages, but explicit Continuous stays Continuous.

MainWindow also queues preview updates through a 300 ms dispatcher timer; the PR #76
secondary comparison preview has a 350 ms timer. The direct-viewer benchmark bypasses
this scheduling. End-to-end foreground latency therefore includes work that the
historical benchmark never measured. Investigating explicit-navigation scheduling is
a bounded follow-up option, distinct from a formatter rewrite; no product timer is
changed here and no measured gain is claimed without a controlled experiment.

The versioned WPF formatter also has viewport-specific visual updates and background
formatting state. Therefore synchronous bottomless formatting does not mean every
off-screen pixel must be drawn, or prove that first-viewport latency cannot improve.
Its blocking format section disables dispatcher processing. The actual complete
layout and navigation obligations, rather than the control's name, determine what
work a measurement forces. This distinction is another reason to retain three clocks.

Retained original-protocol failures from the `75de4247` PR #76 receipt:

| Exact comparison | Baseline failures / 66 | Candidate failures / 66 |
|---|---:|---:|
| Historical, replication 1 | 29 | 28 |
| Historical, replication 2 | 26 | 27 |
| Develop, replication 1 | 14 | 12 |
| Develop, replication 2 | 37 | 33 |

All eleven list samples fail in both exact-develop replications. Develop/candidate
list median and maximum: replication 1, 427.39/543.18 vs 391.41/477.37 ms;
replication 2, 1134.79/1211.05 vs 1136.06/1591.97 ms. Candidate-only threshold
crossings remain in paragraphs, tables and mixed content. These small strata do
not support P95/P99 estimates. Across four comparisons, 528 correctness outcomes
and 264 paired rasters pass independently. Source: original
[Continuous run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42274159149)
and [develop/HC run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42274158971).

Final diagnostic candidate list samples attribute 47.99–56.24% to WPF document
Measure, 30.20–41.72% to Arrange and 3.97–18.55% to attachment. These are mutually
exclusive sampled-stack categories, **not CPU milliseconds**. Native/unresolved
frames and a retained 1035.53 ms cold attachment are not causally explained.
Overlapping root Measure, Arrange and UpdateLayout counters must not be added.
The first candidate list in one final untraced run records 224.30 ms construction,
92.38 ms attachment, 757.88 ms final layout, 871.81 ms queued Input and 1501.94 ms
end-to-end readiness. Managed allocation through layout is 107,424,448 bytes;
private memory 176,676,864 bytes is process-wide, not an exclusive layout cost.

Removing explicit line height changed all 24 paired rasters and still failed
250 ms; it was rejected. List segmentation changed semantics. The private WPF
marker experiment still failed. None is an eligible product optimisation.
Local .NET 10.0.12/144-DPI and hosted .NET 10.0.14/96-DPI observations must not be
pooled. The original 4.14 MB timeout and unresolved local class/dense-ER deadlines
remain historical failures; passing hosted runs do not explain them.

## Independent foreground evidence from this Goal

The reviewed campaign at `fb19e6fcd71f8d47771eb8ed0d5dc77085b718c2`, tree
`a427e5378471a31c0ab0abf4b376fd01dca7539e`, has four passing observer jobs and
219/224 complete child processes. Five mixed-document Preview transitions show a
blank viewport despite an updated status; exact PR #76, develop and historical
products are all affected. All 112 initial logical-content/structure pairs passed
before the later interaction failures. This is additional evidence for independent
journey correctness, not justification to loosen a deadline. The [engineering receipt](../native/native-ux-performance-report.md)
preserves cohort identities, raw numeric observations, screenshots and terminal
results. Numeric SLO approval and non-regression remain INCONCLUSIVE. Mandatory
usable-preview correctness is FAIL on the observed journeys. No policy transition
can waive that finding, the original 250 ms FAIL, or the separate HC failure.

## User consequences

A synchronous layout blocks keyboard and pointer dispatch even when parsing is
asynchronous. A fast handler can still have a late visible response. Conversely,
a long background render can be acceptable if editing remains responsive, status
is truthful, cancellation works, and completed content remains correct. A spinner
or old/hidden preview is not usable content. A rendered first paragraph does not
prove distant Find, selection, source mapping, diagrams or accessibility readiness.

## Alternatives

| Option | Feasibility and evidence | Cost, maintenance and regression risk | Decision |
|---|---|---|---|
| A. Absolute 250 ms complete layout | Both baselines fail; stock layout dominates. No proof of impossibility or eligible bounded fix. | Low benchmark upkeep; potentially months of formatter/renderer work, unknown return. Changing line geometry or list grouping breaks parity. | Retain required policy for now and benchmark permanently; do not claim compliance. |
| B. User-centred responsiveness SLOs | Directly observes typing, navigation, interaction and visible completion separately. Requires foreground observation and independent correctness. | Estimated 2–4 engineer-weeks for robust automation/calibration plus physical acceptance; ongoing workload/runtime maintenance. Estimates are planning ranges, not promises. | Recommended, pending validation and approval. |
| C. Complexity-dependent budgets | Native element counts, text size, nesting, cells, diagrams and decoded pixels explain different work better than filename or byte count alone. | Additional fixture governance and hardware calibration; avoid categories selected after seeing candidate results. | Combine with B; freeze categories and limits before acceptance reruns. |
| D. Custom/virtualised Continuous renderer | Potentially bounds viewport work; no parity-preserving implementation or target evidence in this Goal. | Multi-month design, Unicode/bidi shaping, source mapping, selection, UIA, printing/export, caching and cancellation risk; sustained renderer ownership. | Architectural option only. Separate approval and parity prototype required. |
| E. Wait for upstream WPF | Managed attribution can inform a minimal upstream reproduction. No promised fix/version/performance result was found. | Uncontrolled schedule and rollout, runtime/version maintenance, complete requalification after upgrades. | Track upstream as optional research; never presume it resolves this gate. |

## Proposed acceptance contract

The [measurement specification](../native/native-ux-performance-contract.md) defines
clocks, correctness, strata, sampling, failure bounds and the calibration experiment.
100 ms P95 / 250 ms P99 interaction and 500 ms common first viewport remain **testable
design hypotheses**, not approved thresholds. Completion must have finite per-class
budgets, but this ADR does not derive a release limit solely from PR #76. Required
evidence is baseline and candidate distributions on an owner-selected minimum
hardware profile, independent fresh-process repetitions, human reading/interaction
validation, resource behaviour, and a predeclared confidence/error policy.

Pilot data can reject a target or reveal missing coverage; it cannot approve itself.
CI collection PASS is distinct from UX acceptance. Missing or invalid observations
are never zero latency. Source/visual/accessibility failures cannot be offset by
better speed. Comparisons remain within hardware/runtime/appearance/workload strata.

Human-factors guidance supports separate interaction classes, not a universal layout
deadline. Microsoft's Windows responsiveness guidance distinguishes fast menu feedback
(100–200 ms), interactive results (300–500 ms), navigation and longer work. Nielsen's
direct-manipulation guidance uses approximately 100 ms for an immediate feeling and
one second for uninterrupted navigation. These are design guidance, not percentile
acceptance studies for this editor, minimum-hardware evidence, or justification for
allowing every action 250 ms. The proposed targets still require representative user
tasks, physical display validation and independent baseline calibration.

## High Contrast remains independent

The final packaged .NET 10.0.13 baseline-first comparison reproduces **2.542596386:1**
on exact develop, before the screenshot application launches. WPF pressed/text
are `#FF0078D4 / #FF263B50`; Win32 highlight is `#FF8EE3F0`. Candidate-first can
produce 4.528515:1, later observations 7.893273:1. Order/runtime dependence and
Win32/WPF disagreement make propagation/cache/runtime/environment interaction
plausible; they do not identify ownership or absolve the application. No prewarm,
colour substitution, artificial pumping or lowered 4.5:1 assertion is acceptable.
Retain original failure and observe initial colours before any further interaction.
An independent minimal WPF reproduction and transition message chronology across
stock runtimes are still needed for causality. Accessibility qualification remains
FAIL/INCONCLUSIVE as scoped in the receipt, and blocks integration.

## Migration, acceptance and rollback

1. Add independent proposed-policy collection; leave original workflow/scripts and
   fixture hashes byte-identical. Record both statuses in every report.
2. Freeze a qualified hardware envelope, per-class finite budgets, action semantics,
   sample plan and confidence criteria **before** a new acceptance campaign.
3. Independently review observer controls, actual screen evidence, all correctness
   suites and HC evidence. Obtain explicit owner approval on the exact ADR commit.
4. Only then prepare a separate, approved required-check transition. Desired labels:
   `Historical Full-Layout Benchmark — FAIL / LEGACY QUALIFICATION` and
   `Native UX Performance Acceptance — PENDING / PASS / FAIL`. The first label
   currently describes reporting context, not retirement of its required status.
5. Reassess the exact PR #76 source under the approved policy with fresh evidence;
   do not grant retrospective PASS. Resolve HC and any demonstrated regressions,
   then obtain integration approval. Native production certification remains separate.

Rollback or withhold transition if the probe accepts blank/stale/hidden content,
misses a real response, cannot verify source/artefact identity, lacks required
coverage, or performance improves by changing semantics. Revert only new policy
authority; retain all raw observations and failed histories. Restore the previous
required policy through an explicit owner action, never an agent ruleset edit.

## Decision matrix — 7 November continuation

Every row is **PENDING formal owner decision**. B+C alone has directional agreement.
“Ready” means the recommendation can be approved as a design/protocol choice now;
it is not an acceptance PASS. “Evidence needed” identifies a separate later approval.
The [package](../native/native-acceptance-decision-package.md) gives contracts A/B,
statistical details, migration steps and an owner response form.

| ID / item | Recommended option | Evidence and trade-off | Explicit owner decision / readiness |
|---|---|---|---|
| D01 Architecture | B+C; retain native semantics and existing renderer. Separate three clocks, finite complexity budgets. No D/E dependency. | Original hidden-viewer gate and foreground journeys observe different work; WPF layout remains synchronous. More measurement governance, no evidence for a parity-preserving rewrite. | Accept/reject the exact ADR architecture and nominate technical reviewer. **Ready**; in-principle agreement is already recorded. |
| D02 Interaction responsiveness | Measure offered real input to correct visible response, idle and render-load separately; cancellation includes acknowledgement and rejection of late ownership. | Probe has actual input/capture controls; delayed Continuous/Automatic interruption is not a pending cancellation PASS. Proposed 100 ms P95 / 250 ms P99 remain hypotheses. | Approve boundaries now; approve per-action limits only after independent hardware/task calibration. **Evidence needed for numbers and load coverage**. |
| D03 First usable preview | Current source/version, readable mapped viewport, valid geometry, usable UIA and captured foreground pixels; never a spinner or stale content. | Original mixed-preview defect escaped logical-content tests. Hidden HWND first layout is only a proxy. Captures add overhead. | Approve observable boundary; retain 500 ms common-preview only as a hypothesis. **Ready for definition; evidence needed for budget**. |
| D04 Complete readiness | Finite end-to-end budget for each approved complexity class; all required readers/media, distant navigation/selection and truthful status. Honour separate software-visible and physical scopes. | 128 foreground / 388 controls pass functionally, but required-label frames still fail. Full audit timing is a conservative upper bound. | Approve boundary and honest failure/retry/source-preservation semantics; reject unlimited completion. **Evidence needed for finite budgets and resource handling**. |
| D05 Document complexity | Freeze the seven exact-byte workloads as calibration anchors, add a small consented real corpus plus boundary/oversize cases; classify by structural counts, graph and decoded media size. | Existing catalogue spans prose, technical, long, table, nested lists, Mermaid and mixed content; it is not a representative user corpus. Byte-only classes conceal costly structures. | Approve catalogue method; explicitly select maximum counts/nesting/media and supported modes before acceptance. **Evidence needed for support caps**. |
| D06 Minimum hardware | Nominate a physical entry-level Windows x64 reference: proposed 4 physical CPU cores, 8 GiB RAM, SSD, integrated GPU, 1920×1080/60 Hz; one 16 GiB typical control. Record exact models, drivers, power/thermal state and storage. | Hosted CPUs and the development workstation are not a measured minimum. This is a proposed test procurement profile, not a proven minimum or a budget derived from PR #76. A higher floor reduces reach. | Accept/replace the proposed reference machines and appoint an operator. **Ready to choose; physical performance evidence needed before claiming support**. |
| D07 OS / .NET | Preserve the repository's Windows 10/11 x64 qualification obligations; choose explicit serviced OS editions/builds and exact self-contained .NET 10/WPF package revision. Validate the shipped runtime; keep hosted runtime controls separate. | Existing release checklist still has Windows 10/11 clean-machine gaps. Packaged 10.0.13 and hosted 10.0.14 evidence cannot be pooled. Narrowing to Windows 11 or changing runtime needs its own explicit support decision. | Select actual OS builds/runtime and any deliberately reduced support scope, with rationale. **Evidence needed for certification; no automatic version upgrade**. |
| D08 Display / DPI | Integration retains hosted real-input/actual-HC evidence with exact geometry; release adds physical 100/125/150/200% and mixed-DPI monitor movement, resize and focus checks. Keep application zoom 50–200% distinct from OS scaling. | Hosted 96-DPI and local 144-DPI evidence are different strata; UIA geometry cannot certify physical display behaviour. Physical work is smaller but manual. | Approve integration/release split and physical display matrix/operator. **Ready for plan; physical evidence needed**. |
| D09 Statistical confidence | Separate bounded integration correctness evidence from release tail claims. Predeclare independent units, strata, trial counts and family-wise 95% confidence; use exact exceedance bounds and process/block-aware comparisons. | Pilot cells with two processes cannot establish P99 or equivalence. Within-process repetitions and shared runners are correlated. Cost is explicit; no campaign-wide claim from per-cell minima. | Approve method; freeze claim family, sampling/power and resource plan before qualification. **Ready for method; implementation/evidence needed**. |
| D10 Correctness / accessibility | Non-compensable gates under both contracts: source, ownership, selection, navigation, truthful completion, keyboard/UIA and unchanged 4.5:1 HC assertion. Physical reader certification additionally belongs to release. | Current pixel FAIL / attribution gaps and historical 2.542596:1 FAIL remain open. Baseline defects cannot prove candidate safety. | Approve proposed risk closure criteria and assign owners; do not waive observed failures. **Ready for criteria; closure evidence needed**. |
| D11 Performance regression tolerance | Require both absolute and relative upper-confidence bounds on matched candidate–baseline degradation, plus absolute SLO compliance where activated. Approve action/class margins from human tolerability and baseline variability before candidate qualification. | Similar counts/lower medians do not prove non-regression. Zero margin may be statistically impractical; arbitrary percentages would excuse harm. No defensible numeric margin is established here. | Approve the dual-bound method; supply calibrated absolute/relative margins and precision plan later. **Evidence needed for numeric approval**. |
| D12 Historical benchmark | Keep original scripts, six fixtures, 250 ms assertions, raw FAILs and reproducible benchmark permanently. Propose replacing its release/campaign blocking role only after independently qualified B+C checks. | Existing ordinary integration governance already excludes it; current campaign expressly retains it. Two scopes must be recorded, not silently conflated. | Approve migration design now; separately approve activation, exact check contexts and fresh PR #76 decision later. **Ready for design; activation not ready**. |
| D13 Acceptance separation | Adopt Contract A for safe feature integration and Contract B for distribution. Retain all currently required integration jobs, installed preview checks and unresolved campaign blockers. | Governance already separates release/trust work. Full production signing/physical certification would otherwise stall unrelated development; installer changes still need affected safety tests at integration. | Approve/reject contracts and explicitly reconcile #76 campaign-specific requirements. **Ready for policy decision; #76 remains NO-GO**. |

## Primary references consulted

- [Microsoft Windows responsiveness classes](https://learn.microsoft.com/en-us/windows/apps/develop/performance/responsive): scenario-specific goals and visible interaction boundaries.
- [Jakob Nielsen, Response Times: The 3 Important Limits](https://www.nngroup.com/articles/response-times-3-important-limits/): direct-manipulation and navigation guidance; not a Lens Docs Studio percentile study.
- [Microsoft WPF threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model): dispatcher operations run to completion; queued input cannot interrupt one.
- [Microsoft layout guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-layout-and-design) and [text guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-text): tree/layout work matters; lighter controls have different functionality.
- [Versioned WPF FlowDocumentView](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/documents/FlowDocumentView.cs): supports the architectural interpretation; not a byte match to every serviced runtime.
- [Versioned WPF FlowDocumentFormatter](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/documents/FlowDocumentFormatter.cs): blocking bottomless formatting and viewport visual updates are distinct.
- [Microsoft DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush): waits for the caller's queued graphics work, not the entire session; not physical scan-out proof.
- [Microsoft High Contrast guidance](https://learn.microsoft.com/en-us/windows/compatibility/high-contrast-mode): system-colour pairs and responding to theme changes matter. This guidance does not establish the cause of this failure.
- [Microsoft WM_SYSCOLORCHANGE](https://learn.microsoft.com/en-us/windows/win32/gdi/wm-syscolorchange) and [WM_THEMECHANGED](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-themechanged): colour/theme notification and invalidation paths warrant separate chronology; do not simulate messages to make acceptance pass.
