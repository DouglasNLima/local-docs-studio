# WPF first-layout recovery — Goal #75 / Draft PR #76

Continuation from `715bdc58c8c53e5bb15c607d1a961949ebf57c09`, tree
`166fc4c166378be30d81c3ac1f46f0e56c30af52`. Exact develop remains
`99349d82eb06960eb1f9785cf384db2373aed4fa`, tree
`e4d137a12cbdd827583fcb841612890dd88736ec`.

Status: **investigation; original 250 ms acceptance remains FAIL**. The five
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
uses one prescribed order; the second job reverses it. For each product, a fresh
untraced process and a separate fresh sampled process each execute the six
unchanged fixtures twice. These are diagnostic repetitions, not substitutes for
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

## Validation and verdicts

Initial local Release build: PASS, zero warnings/errors. Analyser controls and
analysis of the real six-fixture trace: PASS. The first analyser compilation
required an explicit reference to the tool's FastSerialization assembly; that
pre-commit compilation failure did not involve product code.

Hosted attribution, the isolated line-height alternative, final-head original
performance protocol, exact-develop replication and required regressions are
pending. No product optimisation or new performance PASS is claimed. Functional
readiness retains its separately scoped 715bdc5 evidence; candidate regression
acceptance remains INCONCLUSIVE until current-head results are recorded. The
historical High Contrast FAIL remains unresolved. Overall integration: **NO-GO**.
