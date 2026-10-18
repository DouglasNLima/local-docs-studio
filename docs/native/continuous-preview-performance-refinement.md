# Continuous Markdown preview: first layout and responsiveness

## Scope and dependency

This continuation is stacked on open Draft PR [#36](https://github.com/DouglasNLima/local-docs-studio/pull/36),
verified at `9a64dfa5adf79dc447db1f671ea39c94ec42de40`. Remote `develop` was
`6a94dd3672f95a267402e2218935d064a6ee6401` at inspection. Neither branch is
merged by this work. Branch: `feature/native-continuous-preview-performance`;
base: `feature/native-mermaid-suite-ux-performance`. Rebase/retarget after the
dependency is integrated, then repeat integration checks at that revision.

**Implemented with bounded limitations; the 250 ms goal is incomplete.** Lists,
mixed content and some table samples still fail. This Draft PR is [#37](https://github.com/DouglasNLima/local-docs-studio/pull/37).
The final delivery receipt on that PR identifies the exact clean delivered head,
its required CI results and its own package identities. Results below retain
their measured application/harness revision and are never relabelled as a later
revision's package certification.

## Demonstrated costs and bounded changes

The existing renderer parses/prepares on workers and constructs a complete private
WPF document on its owning dispatcher, yielding at its existing 32-unit/eight-ms
checkpoints. It does not publish partial documents. Continuous layout itself is
non-interruptible WPF work. The unchanged six fixtures remain fully populated.

Local baseline reproduction on Windows 10.0.26300/.NET 10.0.12/16 available logical
processors fails all six in the first exploratory process batch. The separate
sampled-thread trace records list 1,258.90 ms, table 356.14 ms, paragraphs 357.76 ms,
quote 158.30 ms and details 181.55 ms. These exploratory, traced and occasionally
build-contended observations are retained under ignored `artifacts/`; they are
not pooled with the controlled hosted comparisons.

The trace includes WPF bottomless container/paragraph formatting, glyph analysis
and native font finalisation. These are sampled stacks, not an exclusive-time
accounting of every millisecond. The framework's [list visual validation](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListParaClient.cs)
formats native markers for its item subtrack. The [table implementation](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Documents/Table.cs)
revalidates preceding structure after insertions unless initialisation is batched.

Changes under validation:

- Coalesce adjacent plain literals, decoded entities and soft breaks into one
  native Run, retaining hard breaks and every formatting/link boundary. Literal
  source-map entries carry their offset within the shared Run, so character
  selection, Unicode, source navigation and copying keep their exact boundaries.
  The rich-paragraph fixture retains all text/styles with 3,600 rather than 6,000
  runs. No mutable WPF element crosses documents or dispatcher ownership.
- Use WPF `Table.BeginInit/EndInit` around private cooperative table construction.
  Rows/cells still attach parent-first. Final structural validation occurs once
  on the owner dispatcher, including the cancellation cleanup path; it is a
  synchronous, non-interruptible stage with opt-in phase markers.
- Recheck source version, document owner, appearance, resolved diagram palette,
  source/workspace identity and window lifetime before installing a result.
  Stale failures cannot replace current status. Closing prevents new preview work.
  A read-only native Windows High Contrast flag check also rejects a result while
  WPF's cached appearance property lags the actual OS. Renderer foreground,
  surfaces and code colouring share one captured High Contrast value.
  While OS/WPF state disagrees, the existing debounce timer retains the previous
  document and waits to prepare the latest source; it does not spin or build
  repeatedly with an obsolete palette. The coalesced request reapplies the current
  application/editor appearance once when notification state becomes current.
- Map a retained preview selection through source versions into the replacement
  document without moving keyboard focus. Existing source-based viewport and
  scroll-sync ownership remain in force; current editor selection takes priority
  when selection synchronisation is active.

No document cache, partial viewport, deferred required content, forced collection,
fixture reduction, grammar/limit change or export shortcut is introduced. WPF
objects remain dispatcher-owned. Parsing/native media conversion and synchronous
layout can finish after cancellation; their stale results cannot install.

A display-mode text-formatting experiment did not show consistent benefit and was
removed. A scrollbar experiment was disproved by the framework default (Visible)
and removed; its timing variation is not a product benefit. Uniform paragraph
font promotion also lacked useful evidence and was removed. An eager-template
experiment exposed a WPF finite-pagination fail-fast in the unshown harness; all
template changes were removed.
Original diagnostic logs remain in the worktree's ignored evidence directory.

## Measurement and acceptance protocol

`NativeContinuousPerformanceChecks` retains the original first-layout boundary:
queue Input immediately before assigning the complete document, synchronously
Measure/Arrange/UpdateLayout, then observe callback delivery. Parsing, media and
document construction are outside that span and reported separately. Inclusive
root Measure/Arrange/UpdateLayout diagnostics overlap; do not add them together.
End-to-end readiness begins before parsing and ends after complete construction,
attachment, layout and ApplicationIdle. Raster/correctness/navigation follow.
It includes the harness's empty-host preparation and is labelled separately from
actual application interaction. This hidden-HWND protocol does not measure a
physical first display frame.

The dedicated `Native Continuous 250 ms acceptance` workflow uses one compiled
harness DLL on both product revisions and identical UTF-8 fixture bytes, on the
same hosted Windows runner per comparison. Two independent jobs repeat the
protocol. Each retains the first combined execution, three fresh processes per
fixture/side, four repeated complete constructions and three fresh distant-source
revisions. Pair order alternates; replication 2 reverses the initial order. The
six-fixture combined order stays list/table/quote/details/paragraphs/mixed. Later
combined fixtures share process/runtime/media warming; cold individual processes
are reported separately. No samples are discarded or retried to reach a result.

Every sample records fixture/source/harness/product hashes, source revision, host
image, available processors, runtime, DPI, 640 × 300 viewport, 100% zoom and Light
appearance. Content/structure hashes and native final-target selection/navigation
are compared independently after timing. Initial native PNG equality is reported
and any visual difference requires explicit review. Allocations, natural GC,
managed/private/working-set observations and bounded Input/dispatcher samples
retain their actual scopes. Natural weak-reference observations do not establish
zero retention or a leak. No p95/p99 or universal hardware guarantee is inferred.

Plain-Run count reduction is intentional. Independent verification compares the
complete logical text, every non-Run element count and canonical actual styled
text with equivalent adjacent runs normalised. Raw Run counts remain in each
sample. A white raster backing fixes transparent diagnostic PNG readability,
outside every timed boundary; both sides use the same capture implementation.

`--continuous-acceptance` and the controlled comparison emit explicit PASS/FAIL
receipts. Any candidate first layout above 250 ms or existing construction/input
gap failure makes dedicated acceptance fail. All samples and correctness results
remain available through the always-uploaded artefact. Ordinary deterministic
tests do not acquire environment-specific wall-clock assertions.

## Retained historical observations

The original [#36 run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39122741124)
remains unchanged; these values are historical, not the new comparable baseline.

| Fixture | #36 first layout | 250 ms |
|---|---:|---|
| details | 118.25 ms | PASS |
| list | 1,310.05 ms | FAIL |
| paragraphs | 273.31 ms | FAIL |
| quote | 123.93 ms | PASS |
| table | 309.58 ms | FAIL |
| mixed | 656.42 ms | FAIL |

## Comparable application measurements

Application source `4e55d15a07464d55aaf20cd470526d6c8c5410b0`, immutable baseline
`9a64dfa5adf79dc447db1f671ea39c94ec42de40`, [controlled run 39129930380](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39129930380).
Both independent comparisons use harness SHA-256
`6872D03E0DE6CB20AC741DA76233E7F9C1F46B194AB329AFCE4495A4DB1A043D`.
Both use image `win25-vs2026` `20261007.264.1`, Windows 10.0.26100,
.NET 10.0.13, four available logical processors, 96 DPI, Light, 640 × 300,
100% zoom. Replication 1 reports AMD EPYC 9V74; replication 2 reports EPYC 9V45.
Each before/after comparison stays on its own runner. Results are not pooled.

The [replication 1 receipt](evidence/continuous-preview-performance/application-4e55d15/replication-1.json)
and [independent replication 2](evidence/continuous-preview-performance/application-4e55d15/replication-2.json)
retain all 132 pairs / 264 product samples, fixture/source/product hashes, all
scalar timings, stages, GC/allocation/memory, correctness and PNG outcomes. Each
original JSON is hashed; full bounded dispatcher-operation arrays remain in the
run's `continuous-performance-{1,2}` artefacts. No slow samples were discarded.

Fresh-process cold first-layout input, **median / maximum of three** per cell:

| Fixture | Replication 1 baseline | Replication 1 candidate | Replication 2 baseline | Replication 2 candidate |
|---|---:|---:|---:|---:|
| list | 826.23 / 889.95 | 769.12 / 885.98 **FAIL** | 426.89 / 429.18 | 421.15 / 424.26 **FAIL** |
| table | 380.15 / 390.70 | 295.18 / 300.44 **FAIL** | 243.49 / 275.58 | 179.02 / 210.12 PASS |
| paragraphs | 292.56 / 302.10 | 239.18 / 243.01 PASS | 187.67 / 219.25 | 157.61 / 158.63 PASS |
| quote | 167.91 / 170.71 | 167.07 / 169.65 PASS | 114.54 / 141.39 | 107.29 / 113.33 PASS |
| details | 167.29 / 173.39 | 166.74 / 174.67 PASS | 107.32 / 111.84 | 111.50 / 116.03 PASS |
| mixed | 401.86 / 401.87 | 407.19 / 435.44 **FAIL** | 254.82 / 258.72 | 264.19 / 269.63 **FAIL** |

All values are milliseconds. Cold paragraph medians improve 18.2% and 16.0% in
these two workloads; every candidate paragraph sample, including fresh edits,
passes 250 ms (worst 243.72 / 183.57 ms across all temperatures). This does not
establish a universal latency guarantee. Cold table construction medians fall
525.47 → 139.48 and 363.98 → 97.58 ms. Its complete harness readiness falls
1,231.44 → 757.86 and 874.89 → 533.81 ms. Table first layout still fails in
replication 1, and a replication-2 fresh-source sample reaches 315.37 ms.

Repeated-process first layout, repetitions 1–3 after the retained repetition 0,
**median / maximum**, with every case including observed regressions:

| Fixture | Replication 1 baseline → candidate | Replication 2 baseline → candidate |
|---|---:|---:|
| list | 473.16 / 537.25 → 580.62 / 743.76 | 473.35 / 500.48 → 487.79 / 491.48 |
| table | 149.66 / 178.86 → 222.07 / 233.99 | 147.60 / 209.82 → 188.46 / 201.58 |
| paragraphs | 147.74 / 183.09 → 190.46 / 197.81 | 165.47 / 215.45 → 153.57 / 171.91 |
| quote | 60.78 / 71.72 → 89.45 / 109.43 | 67.44 / 68.61 → 70.05 / 88.62 |
| details | 47.43 / 65.34 → 82.35 / 87.97 | 55.34 / 62.96 → 66.87 / 77.86 |
| mixed | 230.41 / 276.94 → 314.71 / 325.86 | 237.43 / 323.49 → 294.97 / 297.76 |

These warm regressions remain part of the result. In replication 1, paragraph
complete readiness also regresses 297.16 → 334.34 ms; replication 2 improves
332.03 → 245.07 ms. Allocation reduction does not imply all temperatures improve.
No causal improvement is attributed to unchanged quote/details/list element trees.
The simple-line/font and scrollbar experiments did not resolve the list bottleneck.

First combined execution is retained separately (baseline → candidate):

| Fixture | Replication 1 | Replication 2 |
|---|---:|---:|
| list | 1,177.48 → 897.37 | 433.41 → 480.32 |
| table | 391.92 → 368.99 | 208.33 → 195.93 |
| paragraphs | 268.87 → 238.39 | 139.18 → 117.78 |
| quote | 113.99 → 146.20 | 64.97 → 55.08 |
| details | 122.36 → 132.38 | 96.51 → 75.74 |
| mixed | 548.16 → 378.71 | 300.76 → 423.42 |

Only list is first in that combined process. Later cases share framework/font/media
warming. Fresh-source cases modify each distant final marker and append a new
revision; 8/18 and 7/18 candidate samples fail the unchanged input acceptance.
Overall, **29/66 and 22/66 candidate samples fail**. Both controlled jobs are
correctly red. Every sample independently passes source/text/style/structure and
actual native final-target navigation/selection checks outside timing.

Paragraph construction/layout allocation falls about 45.65 → 32.25 MB and
45.73 → 32.35 MB in the two cold comparisons; end-to-end allocation falls about
51.04 → 37.65 MB in each. Gen-0 collections fall from three to two; Gen-1 remains
one and Gen-2 zero for these cold paragraph samples. List allocation remains about
106 MB through construction/layout and 115 MB end to end. Cold table private bytes
increase about 88 → 91–92 MB despite lower construction cost. Memory benefits are
scoped, not an application-wide memory budget.

After 24 repeated documents, natural collection leaves baseline/candidate weak
document references 6/2 and 3/2 alive, with observed managed bytes about 150/111
and 145/110 MB. Every reader is detached. These observations use no forced GC and
do not prove zero retention or a leak; JIT liveness/collection timing remain limits.

Only paragraph raster bytes differ in both comparisons. Text/style hashes and
all non-Run element counts match. The [baseline](images/continuous-preview-performance/application-4e55d15/paragraphs-baseline.png)
and [candidate](images/continuous-preview-performance/application-4e55d15/paragraphs-candidate.png)
were inspected: joining adjacent same-style runs changes WPF glyph positioning
after the decoded entity; text, emphasis, line positions and wrapping remain
readable and equivalent in this fixture. No typography setting is disabled.

## Validation and delivery receipt

At application `4e55d15`, local Release compilation has zero warnings/errors,
1,647 Core tests pass with no failures/skips, and all 134 native corpus/export
cases pass. Native logical/source/Unicode/LF/CRLF/RTF, nested formatting and
expanded scheduling checks pass. The final PR receipt records complete final-head
Core/native/browser/static, five-editor, actual UIA and package/lifecycle results
separately; these earlier checks do not certify an untested later head.

Both visible interaction jobs in run 39129930380 pass on actual Windows, including
actual OS High Contrast. Each opens all six unchanged files, selects a distant
target, preserves mapped selection/focus/caret across a leading edit and full
replacement, undoes without saving, closes and reopens. Further cases exercise
rapid/distant edits, tab switching, obsolete appearance, rapid themes, tab closure
and active window closure. Final coverage also holds the preview gate, cancels
obsolete queued work and verifies only the latest source can subsequently install.
Its local queued cancellation observation is 3.11 ms; hosted final values retain
their own source/runner identity. Parsing and WPF layout remain non-interruptible.

[Normal](evidence/continuous-preview-performance/application-4e55d15/interactions-normal.json)
and [actual High Contrast](evidence/continuous-preview-performance/application-4e55d15/interactions-high-contrast.json)
retain Input-priority heartbeat observations. Whole-scenario maximum gaps are
**2,111.60 / 3,986.64 ms**, so passing interaction correctness is emphatically not
full dispatcher-responsiveness acceptance. No UIA transport timing is presented
as dispatcher/frame latency. The remaining full-list/compound-layout bottleneck
is the framework's atomic bottomless formatting/visual validation; yields around
that call cannot interrupt it.

Hosted visible-window rasters are retained for every fixture in
[normal appearance](images/continuous-preview-performance/application-4e55d15/normal/list-visible-window.png)
and [actual High Contrast](images/continuous-preview-performance/application-4e55d15/high-contrast/list-visible-window.png),
alongside the other five fixtures in those directories. These are captures of the
shown WPF window, not a measured physical frame. Existing native UIA screenshot,
keyboard, selection/search, save/recovery and installed-context captures remain
separate evidence in the final native and UX workflow artefacts.

The original `0c2d2de` controlled iteration remains failed (32/66 and 36/66 samples).
Its first visible outline check exposed a harness defect: earlier fixture opening
could already select index 1, so assigning index 1 again raised no navigation
event. The correction explicitly changes selection before retaining the exact
original caret assertion. Its original logs and both failed acceptance artefacts
remain [available](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39129124545).

The subsequent actual OS-transition case at `f8a74af` found a product race, retained
in [its original installation trace](evidence/continuous-preview-performance/contrast-transition-stale-appearance-f8a74af.json)
and [run 39131744105](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39131744105).
Windows and WPF reported High Contrast enabled, yet a newly installed document
retained normal `#FF202124` instead of system `#FFFFFFFF`. The production commit
boundary now checks the native OS flag in addition to WPF state and cancellation;
document construction captures High Contrast once. Final transition verification
observes every document installation, including valid newer preference-triggered
renders, and retains initial/final traces. It never treats UIA transport as a
dispatcher measurement. Final source-specific outcomes belong to the PR receipt.
The following [2742b2b trace](evidence/continuous-preview-performance/contrast-transition-awaiting-wpf-2742b2b.json)
shows the native guard rejecting the obsolete document while WPF still reports
the previous state. The harness then waits for the actual asynchronous appearance
notification within its unchanged 15-second deadline before requesting/verifying
the current preview, keeping the no-stale-install assertion active throughout.
It verifies the editor and application system colours without manually applying
the theme after that notification.
An [adfb3be readiness trace](evidence/continuous-preview-performance/contrast-transition-current-render-pending-adfb3be.json)
also records no stale installation while the winning preference-triggered render
is still preparing. Completion of a superseded request plus dispatcher idle is
not worker/preview readiness. The final verifier waits for the winning current
source/version/appearance within the existing deadline, while continuing to
reject every obsolete installation; it does not repeatedly force rendering.
The validation-only OS toggle/restore runs on a worker, modelling an external
Windows Settings change while WPF keeps processing messages. A synchronous
settings broadcast from the render dispatcher blocked the harness itself before
its dispatcher deadline could run; that stalled run remains unpassed. OS-control
duration is not presented as application dispatcher latency. The trace verifies
construction was in flight and records distinct control/UI thread identities.

Physical first frame, higher DPI, physical screen reader/clean Windows and
production signing remain outside this scoped certification. No ordinary desktop
installation, merge, release or tag movement is performed. Any produced preview
must identify this exact clean source and pass its own isolated verification;
#36's package cannot certify this continuation.
