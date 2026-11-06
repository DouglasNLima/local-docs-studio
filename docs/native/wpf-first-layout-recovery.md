# WPF first-layout recovery — Goal #75 / Draft PR #76

Continuation from `715bdc58c8c53e5bb15c607d1a961949ebf57c09`, tree
`166fc4c166378be30d81c3ac1f46f0e56c30af52`. Exact develop remains
`99349d82eb06960eb1f9785cf384db2373aed4fa`, tree
`e4d137a12cbdd827583fcb841612890dd88736ec`.

Status: **no acceptable bounded renderer fix demonstrated; 250 ms remains FAIL**. The five
functional workstreams and stock renderer are unchanged. This document does not
supersede any earlier failed observation. The final commit/tree and terminal
workflow receipt belong to the [same Draft PR](https://github.com/DouglasNLima/local-docs-studio/pull/76).

## Measured question and boundaries

The previous candidate's worst hosted list observation had maximum input gap
1,599.4798 ms, final-layout interval 1,480.4658 ms, attachment 97.3108 ms,
construction input delay 36.0089 ms and source-map stage 1.219 ms. Both exact-develop
replications failed all eleven list samples on both products. Those results remain
in [run 42255213462](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42255213462).

Inspect the actual attachment/layout call tree before choosing a change. Retain
cold first runs, original six fixtures, correctness, source identity, existing
first-layout clock and 250 ms threshold. Separate diagnostic tracing from the
original 66-sample acceptance protocol. Do not add prewarming, forced collections,
new message pumping, hidden content or premature readiness. Do not change the
historical High Contrast assertion or its unresolved 2.542596:1 observation.

## Architecture inspection

`NativeMarkdownRenderer` already constructs the complete private FlowDocument
cooperatively, yielding between units. Parent-first attachment, plain-Run
coalescing, table initialisation batching, owned immutable source-map groups and
captured appearance are already present. The list fixture has one native List,
4,000 ListItems, 4,000 Paragraphs and 4,000 Runs. Removing another intermediate
formatting container cannot reduce that tree: there is none in this fixture.

`MainWindow` installs a completed, still-current result once, then restores
viewport/selection and updates navigation. `DocumentPreview` coalesces media-fit
requests and stops background pagination in explicit Continuous mode. Its
Automatic preference may choose Pages, but explicit Continuous remains Continuous.
The list contains no image/media-fit work. Changing those shell stages alone
cannot solve the original gate: its `StageViewer` directly subclasses
`FlowDocumentScrollViewer`, with neither MainWindow nor DocumentPreview involved.

The unchanged harness queues Input before assigning the complete document, then
calls Measure/Arrange/UpdateLayout and observes the callback. The private WPF
document view performs its own measure inside UpdateLayout: a tiny *root* Measure
counter does not mean document measurement was free. Root Measure/Arrange and
UpdateLayout timings overlap and must not be added. ApplicationIdle completion
is recorded separately. No physical first display frame is measured by this
hidden-HWND protocol.

The WPF reference source supports, but does not independently prove, the sampled
runtime attribution. [FlowDocumentView](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/documents/FlowDocumentView.cs)
formats a bottomless document during measurement and connects its visual during
arrangement. [ListParaClient](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListParaClient.cs)
validates the list and its markers.
[SimpleTextLine](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/TextFormatting/SimpleTextLine.cs)
declines its fast path for explicit line height and first-line marker properties.
These are versioned 10.0.0 reference sources, not a claim that their bytes match
every observed installed runtime. No upstream implementation is copied.

## Bounded opt-in attribution

`scripts/native/Measure-FirstLayoutAttribution.ps1` builds exact develop and the
candidate, then verifies the identical compiled harness DLL on both. It refuses
changes to the original harness, fixtures, event markers, comparison script or
acceptance workflow relative to the continuation start. Each disposable runner
uses one prescribed product order; the other order reverses it. For each product,
a fresh untraced process and a separate fresh sampled process each execute the six
unchanged fixtures twice. A second observation-order dimension profiles before
any untraced preview on that runner, retaining initial attachment costs without
clearing caches or prewarming. These are diagnostic repetitions, not substitutes for
the original cold/repeated/fresh-source acceptance protocol.

Pinned `dotnet-trace 10.0.750501` records existing EventSource markers and sampled
thread stacks, with a 32 MiB buffer, 120-second child limit and 256 MiB trace limit.
The analyser refuses lost events, incomplete/overlapping/wrong-thread intervals,
missing samples and cardinality overruns. It bounds traversal to 128 frames,
200,000 samples, 100,000 markers, 50,000 frame identities and ten million events.
Nine independent attribution controls cover boundary/thread accounting,
uncertainty and rejected incomplete evidence. Raw traces, Speedscope, process
outcomes, source/runtime/product hashes and all observations are uploaded even
after failure. The process bound is not an acceptance timeout extension.

The analyser consumes TraceEvent/FastSerialization already distributed with the
pinned diagnostic tool, without adding an application/package dependency or
changing any product lock file. The tool's NuGet metadata declares MIT and its
own `THIRD-PARTY-NOTICES.txt` remains in the isolated installation. Its upstream
repository identity is `dotnet/diagnostics` commit
`ad724c4b85e7ae775e42f393c6e95a7557a57954`. Diagnostic binaries are not packaged,
signed or shipped with Lens; first-party analysis code is independently written.

### Initial local observation, unmodified 715bdc5

The retained trace `local-715-profile/continuous.nettrace` was taken on the shared
development host, with a hidden HWND, no foreground input and no appearance
changes. It is diagnostic evidence with profiler and host-scheduling overhead.
All six source/content/navigation checks passed; all six traced latency checks
failed. It is not a hosted acceptance result or a before/after improvement.

| Fixture | Layout-interval samples | Document Measure | Document Arrange | Other |
| --- | ---: | ---: | ---: | ---: |
| list | 2,187 | 1,138 | 875 | 174 |
| table | 282 | 208 | 39 | 35 |
| quote | 138 | 119 | 9 | 10 |
| details | 129 | 112 | 7 | 10 |
| paragraphs | 294 | 262 | 13 | 19 |
| mixed | 372 | 247 | 85 | 40 |

Categories assign each sample once. List marker formatting occurs in 320 samples
inside the Arrange category, not an additional exclusive cost. Of the list's
2,187 stacks, 1,548 contain at least one unresolved/native frame; none was truncated.
Thus attribution to the managed WPF entry points is strong, while precise native
leaf CPU attribution remains **INCONCLUSIVE**. Sampling is not a CPU-time counter
and percentages are not exclusive CPU milliseconds. Renderer construction has
its existing actual thread-CPU counter; layout does not acquire a fictitious one.

The traced list reports construction 1,410.9523 ms, attachment 268.0361 ms,
final-layout interval 4,531.627 ms and first-layout Input 4,863.9483 ms. Its measured
construction allocation is 26,850,704 bytes; total through complete layout is
107,502,624 bytes. The traced marker interval differs slightly from the callback
interval because marker overhead has its own boundary. These values must not be
compared numerically with the earlier hosted, untraced 1,599.4798 ms observation.

## Alternatives and acceptance constraints

1. **Move work around attachment or change dispatcher priority.** This cannot
   interrupt the measured synchronous WPF call and can merely move the pause.
   Pre-layout before the clock would misrepresent completion and is rejected.
2. **Remove explicit line height.** An isolated source-copy experiment measures
   this one property with the same executable/fixtures and both orders. It is
   explicitly ineligible: line geometry/appearance changes, and native markers
   still require full formatting. The product source remains untouched. Retain
   its rasters and every result, regardless of whether any timing improves.
3. **Split native lists or cache native markers.** Earlier experiments remain
   relevant but are not relabelled as current-head evidence. Native 64-item
   segmentation changed logical grouping and still failed. The private-runtime
   marker experiment preserved checked semantics but failed 29/66 and 33/66
   samples. Its modified framework cannot be distributed as stock WPF. Details
   and raw links remain in the [historical investigation](continuous-preview-performance-refinement.md).
4. **Construct only the first viewport.** This changes the complete-content
   contract and cannot pass the unchanged original gate honestly. All source,
   distant selection and required layout must remain available and verified.
5. **Separate Continuous layout architecture or an upstream formatter change.**
   This needs a measured prototype and full parity; no bounded product fix has
   yet been demonstrated. Proposed design and risks follow the current-head
   hosted evidence rather than assuming this alternative will meet 250 ms.

## Hosted checkpoint 75b6a08

[Attribution run 42269577087](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42269577087)
completed all four original diagnostic jobs. The two profile jobs retained 96
observations: all independent source, complete text, native selection/navigation,
non-Run structure and styled-text checks pass; all 48 paired initial rasters match.
This is successful evidence collection, **not performance acceptance**. Image
`win25-vs2026` / `20261103.298.1`, .NET/WPF 10.0.14, four available logical
processors, 96 DPI, Light and 640 × 300 are recorded. Baseline-first used EPYC 7763;
candidate-first used EPYC 9V74. Comparisons stay within their own host.

The common harness SHA-256 is
`542968C741D688E2AC61A68AB783218C0ECD9CA8B82910E9FC6409070DB167ED`.
The [compact source-labelled observations](evidence/first-layout-recovery/checkpoint-75b6a08.json)
retain per-sample stages, allocations, memory, identity and attribution counts.
Full raw traces, samples and rasters are in that run's four artefacts; their local
retained copies are under `artifacts/first-layout-recovery/ci-75b6a08/`.

Untraced first / repeated first-layout Input in milliseconds, preserving order:

| Fixture | Baseline-first: develop | Baseline-first: candidate | Candidate-first: develop | Candidate-first: candidate |
| --- | ---: | ---: | ---: | ---: |
| list | 1489.14 / 1245.86 | 1578.49 / 934.46 | 850.20 / 656.84 | 2100.18 / 677.31 |
| table | 519.34 / 403.09 | 438.36 / 253.27 | 399.48 / 196.42 | 382.72 / 208.07 |
| quote | 239.69 / 142.66 | 183.59 / 115.22 | 148.02 / 82.68 | 93.30 / 110.36 |
| details | 213.63 / 164.06 | 161.07 / 131.85 | 123.56 / 99.10 | 153.12 / 137.73 |
| paragraphs | 354.82 / 361.96 | 303.66 / 268.55 | 268.23 / 234.93 | 191.33 / 285.12 |
| mixed | 796.60 / 532.32 | 485.74 / 373.68 | 470.35 / 295.27 | 594.96 / 344.37 |

The candidate's first list attachment on the candidate-first host was **1035.53
ms**, versus 87.27 ms on the baseline-first host. That distinct cold observation
is retained, not attributed to layout or dismissed as noise. Its final-layout
interval was 1049.83 ms. Subsequent profiled processes cannot causally explain an
earlier untraced stall: this motivates the additional sampled-first observation
order in the final diagnostic run. OS scheduling, initial native services and
framework initialisation remain possible contributors, not established causes.

In the four candidate list traces, Measure accounts for 51.2–53.2% and Arrange
for 40.1–41.6% of observed marker-thread samples. Baseline stacks show the same
managed entry points. Unresolved/native frames occur in 67.6–71.3% of those
candidate stacks; no trace events were lost or stacks truncated. This localises
the repeated dominant work to WPF bottomless formatting and visual validation,
without proving that all between-product or cold-attachment differences share
one cause. Similar totals do not establish non-regression.

For the untraced baseline-first candidate list, first/repeated construction was
393.44/240.03 ms (thread CPU 390.625/234.375 ms), attachment 87.27/20.18 ms and
final layout 1470.41/891.73 ms. The first content LayoutUpdated proxies were
1951.88/1151.85 ms from construction start; full ApplicationIdle completion was
1987.82/1176.02 ms. End-to-end readiness including parsing and harness preparation
was 2447.57/1227.76 ms. These are distinct clocks, not physical displayed frames.

Construction allocated 26,825,176/26,710,952 bytes on the UI thread; total managed
allocation through complete layout was 107,444,840/106,392,072 bytes. Natural
generation 0/1/2 collections were 8/4/1 and 7/4/0. Process private memory was
122,118,144/217,956,352 bytes, and working set 187,084,800/304,463,872 bytes.
Process/managed-memory observations are taken after correctness/navigation and
include previous fixtures in repeated processes; they are not exclusive layout
allocations or evidence of a leak. All other samples retain their own values.

### Rejected causal alternative

Removing only explicit 23-DIP line height in an isolated source copy preserves
the 48 checked source/content/navigation observations but changes **all 24 paired
rasters**. Visual inspection confirms changed row spacing and viewport density.
The four list results are stock → alternative:

| Order / construction | First-layout Input, ms |
| --- | ---: |
| stock-first / first | 1688.97 → 1392.16 |
| stock-first / repeated | 1424.62 → 1043.47 |
| alternative-first / first | 2146.76 → 1522.99 |
| alternative-first / repeated | 964.68 → 1345.79 |

The alternative still misses 250 ms and the final pair regresses. No appearance
or correctness criterion is relaxed to accept it. The patch and hashes remain
in the artefact, never in product source. This experiment establishes that this
property removal alone is neither a sufficient nor an eligible solution.

### Original protocol at this checkpoint

[Exact-develop run 42269577006](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42269577006)
retains the original 66 samples per product per replication. Replication 1 fails
28/66 on both products; replication 2 fails 31/66 develop and 29/66 candidate.
All 264 correctness outcomes and 132 paired rasters pass. The equal first count
does not establish non-regression. Local original-protocol evidence separately
fails 60/66 on both products with 132 correctness passes and 66 equal rasters;
its shared 144-DPI development host and runtime 10.0.12 are not pooled with CI.

## Proposed redesign and decision criteria

No stock application-level cache, yield placement or attachment adjustment has
demonstrated a safe reduction sufficient for this gate. This is a bounded
engineering finding, not a proof that every possible WPF optimisation is futile.
Do not ship the ineligible line-height variant or a private framework patch.

The least disruptive next investigation is **upstream WPF formatter work** while
retaining the actual FlowDocument/List/Table/Section, TextPointer and automation
contracts. A prototype would need to reduce full-line and marker processing
without changing baselines, explicit line height, numbering, bidi/complex-script
shaping, rich-text copy or list grouping. The earlier marker-only prototype is
insufficient. Require signed stock-runtime delivery upstream and independent
performance/correctness evidence before considering a dependency change. No
framework upgrade, fork distribution or upstream submission is made here.

A separate **Continuous view architecture** is substantially riskier. A concrete
prototype should use immutable semantic blocks and source spans, compute full
required line/cluster geometry on an owned STA worker, then publish immutable
layout snapshots to a WPF visual host. Only drawing residency may be virtualised;
all required geometry and content must be complete before a complete-ready marker.
First usable viewport and full completion need separate states and clocks. Keep
the old preview available while a new generation is incomplete; discard obsolete
source/version/theme/DPI/width snapshots. Mutable TextLines/DependencyObjects must
stay on their owner thread and be disposed there. Any frozen drawing transfer and
font-resource lifetime need actual thread-affinity tests, not an assumption.

This architecture needs bounded per-document caches with explicit eviction,
complete Unicode cluster/bidi hit testing, cross-block selection, keyboard
navigation, Find, source anchors, Details expansion, nested lists, table spans,
diagram/image placement, zoom/resize invalidation, text/copy fidelity and UIA
TextPattern parity. Pages and canonical export should continue using their
independent complete-document path. Risks include divergent layout, screen-reader
ordering, selection geometry, duplicated memory, resize stalls and disposal races.
No arbitrary memory budget or claimed 250 ms capability is inferred before a
measured prototype exists.

Crucially, the unchanged original harness consumes a FlowDocumentScrollViewer.
A replacement visual host cannot honestly claim to pass that binary contract.
Any future renderer qualification would require an explicitly reviewed additional
adapter/contract while retaining this original gate and its failures; silently
rewriting it is outside this continuation. Do not implement such a replacement
as an unreviewed small fix. The immediate next action is to explain the retained
cold attachment observation, then measure a parity-preserving upstream prototype
against the original gate before committing to a larger architecture.

## Validation and verdicts

Initial local Release build: PASS, zero warnings/errors. Analyser controls and
analysis of the real six-fixture trace: PASS. The first analyser compilation
required an explicit reference to the tool's FastSerialization assembly; that
pre-commit compilation failure did not involve product code.

Hosted checkpoint attribution and the rejected alternative are recorded above.
Final-head original performance, exact-develop comparisons, both observation
orders and required regressions have their own terminal identities in the
consolidated PR receipt. No earlier PASS certifies a later source head. No product
optimisation or new performance PASS is claimed. Functional readiness and current
regression outcomes remain separately scoped; candidate regression acceptance
cannot clear unexplained retained failures merely through passing repetitions.
The historical High Contrast FAIL remains unresolved. Overall: **NO-GO**.
