# ADR — Native Document Rendering Performance & Responsiveness Contract

Status: **Proposed**. Owner: repository maintainer DouglasNLima. Decision authority:
explicit written owner approval on [Goal #77](https://github.com/DouglasNLima/local-docs-studio/issues/77)
and its Draft PR. No approval has been given. Date: 6 November 2026.

Related: [Goal #75](https://github.com/DouglasNLima/local-docs-studio/issues/75),
[Draft PR #76](https://github.com/DouglasNLima/local-docs-studio/pull/76),
[exact investigation](https://github.com/DouglasNLima/local-docs-studio/blob/75de42474514af0e7a6e521235a6517f4acad14b/docs/native/wpf-first-layout-recovery.md).

## Decision proposed

Adopt **B + C**: independently qualify foreground interaction, first usable preview
and complete readiness, with empirically calibrated complexity and hardware strata.
Keep **A** as the unchanged required gate until an owner approves the policy and
its independently validated implementation. Do not implement **D**, ship a WPF fork,
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

Verified before work: `develop` is `99349d82eb06960eb1f9785cf384db2373aed4fa`, tree
`e4d137a12cbdd827583fcb841612890dd88736ec`; PR #76 remains Draft at
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

Retained original-protocol failures from the final PR #76 receipt:

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

## Owner decisions pending

- Approve or reject B+C and the proposed observation boundaries; identify reviewer.
- Select minimum supported hardware, display/DPI and runtime support policy.
- Approve numeric limits and confidence/variance tolerances after calibration and
  human evaluation; determine supported large-document envelope and failure UX.
- Decide whether evidence is sufficient to retire the old gate's blocking role.
- Resolve independent HC ownership and release/physical-certification requirements.

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
