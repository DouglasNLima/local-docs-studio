# Continuous Markdown preview: first layout and responsiveness

## Scope and dependency

This continuation is stacked on open Draft PR [#36](https://github.com/DouglasNLima/local-docs-studio/pull/36),
verified at `9a64dfa5adf79dc447db1f671ea39c94ec42de40`. Remote `develop` was
`6a94dd3672f95a267402e2218935d064a6ee6401` at inspection. Neither branch is
merged by this work. Branch: `feature/native-continuous-preview-performance`;
base: `feature/native-mermaid-suite-ux-performance`. Rebase/retarget after the
dependency is integrated, then repeat integration checks at that revision.

**Implemented with bounded limitations; the 250 ms goal is incomplete.** Lists,
mixed content and some paragraph/table samples still fail. This Draft PR is [#37](https://github.com/DouglasNLima/local-docs-studio/pull/37).
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

## Subsequent paired verification at `411ebf5`

This independent later comparison uses the same protocol and original fixture
bytes, including the appearance-guard refinement. It is not pooled with the
earlier run. [Replication 1](evidence/continuous-preview-performance/application-411ebf5/replication-1.json)
and [replication 2](evidence/continuous-preview-performance/application-411ebf5/replication-2.json)
retain every sample, its original raw hash, allocation/GC/stages and independent
correctness. Both use harness `4F174FEA2B9153BB30007682E04FAA05086C5FEB42E9254F4F1E5CB9FA5FC0F8`,
baseline `9a64dfa5adf79dc447db1f671ea39c94ec42de40` and candidate
`411ebf563c0ffebfd6ab111bc64a4d7830464b26`. The [source run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39135650685)
retains full bounded dispatcher arrays and rasters. Both hosts have the same
recorded Windows image/runtime/DPI/viewport policy as above; replication 1 uses
EPYC 9V45 and replication 2 EPYC 9V74. Host CPU availability is four logical
processors. Final-head outcomes remain separately identified in the PR receipt.

**12/66 and 28/66 candidate samples fail acceptance; correctness passes for all
264 product samples.** A paragraph cold sample reaches 288.46 ms, so the earlier
paragraph pass is scoped to that earlier measured workload/runner. The target is
still incomplete. No slow samples are discarded. Values below are first layout
in milliseconds, baseline → candidate; cold/repeated/edited cells are observed
median / maximum of three, never p95/p99. The first combined execution is one
retained sample per fixture. Repeated rows exclude the separately retained
first construction in that process (repetition zero), which remains in raw JSON.

### Replication 1

| Fixture | First combined execution | Fresh process cold | Repeated construction | Fresh distant-source revision |
| --- | ---: | ---: | ---: | ---: |
| details | 78.96 → 56.97 | 102.73/106.82 → 103.95/104.05 | 42.97/64.40 → 33.81/38.02 | 70.97/80.97 → 64.51/77.84 |
| list | 429.67 → 417.17 | 403.55/406.13 → 399.44/410.62 | 386.41/436.50 → 333.73/444.39 | 377.83/384.70 → 398.72/405.68 |
| paragraphs | 160.24 → 107.54 | 171.78/175.89 → 139.10/139.92 | 166.24/199.59 → 94.52/134.56 | 157.15/201.34 → 115.06/169.95 |
| quote | 72.40 → 47.65 | 101.79/105.37 → 99.27/99.88 | 51.26/68.12 → 40.51/49.86 | 55.39/63.27 → 50.18/53.34 |
| table | 248.31 → 152.99 | 236.67/241.60 → 172.94/220.10 | 150.71/167.50 → 102.76/104.90 | 126.22/209.34 → 136.69/160.07 |
| mixed | 379.34 → 238.72 | 239.22/243.03 → 233.23/246.94 | 203.15/245.80 → 162.46/172.57 | 261.16/327.20 → 219.98/283.44 |

### Replication 2

| Fixture | First combined execution | Fresh process cold | Repeated construction | Fresh distant-source revision |
| --- | ---: | ---: | ---: | ---: |
| details | 97.46 → 130.75 | 166.96/170.50 → 169.66/173.65 | 50.53/67.24 → 60.44/70.69 | 84.84/118.46 → 101.01/140.79 |
| list | 919.82 → 971.78 | 865.75/985.51 → 834.69/900.27 | 506.43/715.23 → 696.09/726.06 | 686.98/838.07 → 754.17/837.70 |
| paragraphs | 349.01 → 282.24 | 310.46/344.85 → 240.76/288.46 | 131.59/198.30 → 149.04/206.07 | 212.86/306.37 → 192.85/214.25 |
| quote | 82.66 → 119.48 | 165.84/174.87 → 162.96/174.33 | 49.13/123.88 → 71.71/117.73 | 76.39/131.92 → 82.42/111.28 |
| table | 281.97 → 398.52 | 357.65/375.99 → 276.83/289.65 | 134.50/251.81 → 159.45/342.67 | 232.80/250.21 → 163.24/285.86 |
| mixed | 296.85 → 348.14 | 361.05/365.37 → 368.39/375.37 | 240.67/301.52 → 219.62/324.59 | 217.03/261.89 → 325.27/337.25 |

Regressions include replication 2 mixed cold/repeated/fresh-source maximums,
list repeated median/maximum and table repeated maximum; details/quote timings
also vary. These are reported observations, not hidden by the paragraph/table
construction benefits or an average across hosts. No universal responsiveness
or memory guarantee is inferred. Full readiness and allocation observations for
every temperature remain in the linked paired records.

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

The [`411ebf5` hosted trace](evidence/continuous-preview-performance/contrast-transition-completed-411ebf5.json) subsequently completes the actual OS transition and
restoration in 11.10 seconds: the original document remains while WPF's flag is
stale, then exactly one observed installation uses the current system colours.
The [phase log](evidence/continuous-preview-performance/contrast-transition-phases-411ebf5.txt)
records restoration. The overall normal interaction job was cancelled after stalling later and is
not a pass. Inspection found a separate harness ownership defect: the queued-work
case synchronously waited for the preview semaphore on WPF's dispatcher, while
a preference-triggered render could own it and await that same dispatcher.
The subsequent [external stack capture at `b4526ab`](evidence/continuous-preview-performance/queued-gate-blocked-b4526ab.txt)
confirms the validation UI thread blocked in `SemaphoreSlim.Wait()`; its controlled
normal interaction job correctly fails, while fixed High Contrast passes.
The acquisition now pumps `WaitAsync` within the existing 15-second deadline;
the held-gate cancellation and latest-source assertions are unchanged. An
independent external process bound retains diagnostics if an uninterruptible
test stage prevents an in-process deadline from executing. Neither bound changes
the product's performance acceptance or represents a dispatcher latency promise.

Physical first frame, higher DPI, physical screen reader/clean Windows and
production signing remain outside this scoped certification. No ordinary desktop
installation, merge, release or tag movement is performed. Any produced preview
must identify this exact clean source and pass its own isolated verification;
#36's package cannot certify this continuation.


## Continuation after 0237b08 (in progress)

PR #37 remains open and stacked on unmerged #36. The previous bounded delivery does not complete the goal. The continuation baseline is `0237b08390d5d1d946c0b19b81d671ccd54fb951`; previous evidence and failures above remain applicable to that revision only.

The original `.749` first-launch failure is unresolved. Its combined exact-editor/four-heading assertion is retained. New bounded metadata probes capture the independent predicate values, focus, clipboard ownership and preview state before cleanup; probe failures cannot mask that assertion. Component failure files are now included in the installed evidence upload. A dedicated diagnostic workflow prescribes five independent launches on each of two disposable hosts using the original installer SHA-256 `89494c6a963fd283773ce23af979aa732796920e943c3a607db413d67720cc07`. All samples, including failures, are retained. Success would not by itself explain the historical failure.

Opt-in shell markers distinguish parse, preparation/media/construction awaits, synchronous attachment, source-map/viewport work, selection restoration and outline refresh. Await intervals are inclusive elapsed spans; background thread samples and exclusive synchronous UI spans must be analysed separately. Diagnostic stack collection is separate from the controlled performance acceptance path. The shown-shell harness now loads `App.xaml` resources. Implementation and acceptance remain incomplete pending causal evidence and a further structural reduction.

### Shown-shell attribution and viewport index

The diagnostic [hosted run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39155870393) used `29b728f` with full `App.xaml` resources. Exclusive synchronous `viewport-save` spans reached 1,779.76 ms and 1,517.54 ms; the maximum Input heartbeat gap was 3,073.12 ms. Sampled thread-time stacks include 26,640.5 ms inclusive in `PreviewSourceMap.VisibleSource` across the entire shown scenario and 26,745.7 ms inclusive in `TextPointerBase.GetCharacterRect`. These overlap and are **not added**. Native list visual validation and bottomless formatting also remain visible. The tracer affects timing; these are diagnostic attribution, not acceptance samples. Raw bounded markers and source-labelled scalar observations are retained under `evidence/continuous-preview-performance/continuation-0237/`; original trace hashes identify local retained files.

The index contains only references already owned by one immutable preview. It groups literals by paragraph, or by complete outer table row where cell source order is not vertical order. Rendering order is kept independently from source order for relocated footnotes. Unknown geometry falls back to the full scan. A fresh validity filter handles details opening/closing; a binary search chooses the paragraph/row spanning the viewport top, then the existing exact character-boundary algorithm scans visible groups. There is no cross-document or exact-source cache, reuse of mutable WPF objects, changed formatting or deferred document construction. A source/theme regeneration builds a new map; closing releases the map with its document. Ordinary scrolling reuses its structural index without caching geometry.

Local diagnostic anchor checks retain the six original fixtures plus a wrapped multi-cell table, relocated footnotes and a nested ordered/unordered list with initially closed details. All nine cases pass independent correctness, and every initial/top/middle/bottom result matches the old exhaustive scan in the latest local check (`indexed-anchors-3`). A first adversarial source placed details directly in a list container unsupported by the existing passive-details parser and correctly failed the unchanged zero-warning assertion; a heading now ends that list before the independently tested details section. The failure remains in local `indexed-anchors/`; the corrected source and successful results are `indexed-anchors-2/`. A very large wrapped row remains a bounded limitation: its own literals are still scanned.

### Original-package first-launch reproduction

Both predetermined five-launch replications passed using the unchanged `.749` installer. These observations do not establish a fix or explain the original timeout. In this first diagnostic revision, read-only state probes before input could alter scheduling. The next protocol removes those probes from the original route and retains pre-cleanup probes on failure only. It also adds a separate controlled foreground-loss schedule using an independently owned native helper window, with original and explicit foreground restoration routes distinguished. Those scheduled outcomes must not be represented as reproduction of the historical failure without supporting state.

### Native segmentation experiment

An explicit validation-only prototype moves complete native `ListItem` objects into consecutive 64-item native lists; text, bold formatting and numbering remain. It changes logical list grouping and is never installed by the product. Local diagnostic first/repeated list layouts were 1,944.74/825.02 ms; mixed was 554.32/298.31 ms. The target remains 250 ms. These exploratory samples are not paired acceptance evidence; hosted independent prototype repetitions are queued with the next controlled comparison. They test a bounded structural alternative without accepting a first viewport or changed accessibility grouping as goal completion.

### Retained intermediate comparison: 0237b08 → 2134fe8

[Controlled run 39157579889](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39157579889) used one compiled harness (`EAA891395058FC7295251DF74C5498963CEB0787F11E5DB5E00BC74A5D40EB26`) on both products per host. All 264 independent content/structure/navigation checks passed; all 132 paired initial rasters were byte-identical. Acceptance failed: 37/66 candidate samples in replication 1 and 26/66 in replication 2 exceeded first layout. Every sample and regression remains in `2134fe8-replication-*.json`, with original raw-file hashes and separate source, harness, environment and fixture identity.

Values below are observed median / maximum in milliseconds, over the same eleven prescribed samples per fixture. These combined-temperature summaries are descriptive only; temperature and individual samples remain separate in the raw records, and no percentile claim is made.

| Fixture | Replication 1 baseline | Replication 1 candidate | Replication 2 baseline | Replication 2 candidate |
|---|---:|---:|---:|---:|
| details | 91.82 / 271.47 | 129.10 / 333.41 | 56.15 / 168.42 | 102.96 / 173.09 |
| list | 1298.54 / 1548.16 | 1302.69 / 1444.73 | 737.91 / 897.63 | 755.24 / 1053.41 |
| paragraphs | 166.34 / 397.03 | 237.22 / 384.87 | 118.86 / 254.23 | 219.42 / 236.01 |
| quote | 87.59 / 257.12 | 131.62 / 277.71 | 50.97 / 167.01 | 106.99 / 166.57 |
| table | 287.95 / 433.49 | 362.97 / 454.18 | 189.33 / 282.24 | 273.95 / 383.17 |
| mixed | 308.74 / 623.82 | 480.44 / 625.55 | 207.17 / 396.51 | 367.01 / 487.99 |

The new outside-clock anchor probe made this intermediate protocol less comparable to the established protocol: the baseline spent seconds scanning geometry between ordinary samples, while the candidate did much less work. That can alter subsequent heap/cache state. It does not explain every regression or justify discarding any sample. The correction confines anchor probing to its explicit correctness command. Both products must rerun the corrected compiled harness with the original first-layout clock and 250 ms threshold unchanged.

The first index implementation also walked WPF parents and allocated LINQ groups before layout. The current refinement records immutable paragraph/row ranges during the renderer's existing traversal, avoids additional WPF tree access, and reuses the literal array when source order already matches document order. Tables containing row spans use a complete-table range because rows can overlap. Ten local cases (the original six plus spanning/wrapped tables, relocated footnotes and nested lists/details) pass exact initial/top/middle/bottom anchor comparison against the retained exhaustive algorithm. No layout work is deferred by this change.

### Shown responsiveness and appearance follow-up

The `2134fe8` sampled shown run reduced the largest recorded viewport-save span to 112.49 ms, but its maximum Input heartbeat gap was **4,470.87 ms**. It ran on another host with tracing overhead, so these observations do not quantify a paired responsiveness improvement. The new controlled path runs the identical shown harness against baseline and candidate on each host, reverses initial order in replication 2, and retains both outcomes under independent 180-second process bounds. A normal-appearance run rejects a desktop left in High Contrast by a failed earlier process.

The trace includes 5,562.0 ms inclusive in `ThemeService.Apply`; samples within that call include logical-child/resource-change tree walking. These spans overlap native/framework work and must not be added to other inclusive totals. A resource `BeginInit`/`EndInit` batching experiment encountered a local Pages-mode WPF fail-fast; one independent fresh-process reproduction passed. Its cause is not isolated and the experiment is rejected. The failure is retained as `rejected-resource-batch-experiment.json`.

The replacement keeps ten colour slots and their bound brushes inside one resource-dictionary owner. A theme refresh updates their colours; unchanged resource keys and aliases retain their objects. Actual Fluent variant changes replace one merged dictionary rather than remove then add. Each owner has its own palette under a weak-key lifetime; there is no document text, layout, path or cross-owner WPF object in the palette. The brushes stay on their owning dispatcher; immutable preview drawings retain their separate frozen-brush path. All ten role values are recomputed from current Light/Dark/System/High Contrast colours on application, including scheme colours. No theme is prewarmed. The [WPF Freezable contract](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/freezable-objects-overview) explains bound-brush mutability and thread ownership.

The targeted dynamic-resource consumer test checks exact Light/Dark colours, retained owned brushes and a separate owner's unchanged colours. It runs without showing a window and is **not GUI certification**. Actual shell, all five editors, OS High Contrast and final-source package checks remain required.

The traced `2134fe8` process observed automatic enable → current preview → disable → current preview with no manual ApplyTheme and no obsolete installation. The separate normal interaction job failed before disable verification because its optional diagnostic watch rejected the newly added `disabling-os` marker. That bounded whitelist is corrected; the assertion, 15-second dispatcher deadlines and independent hard bounds are unchanged. The original failed job and stderr are retained. Neither a green traced job nor cleanup substitutes for the required corrected interaction job.

Ten original `.749` launches without pre-input probes also passed on two hosts. Both original and explicit-foreground-restoration routes passed the separately injected foreground-loss schedule on both hosts. Thus that experiment **does not support a focus diagnosis**. No foreground workaround has been added to the ordinary input route. The historical first-launch failure remains unresolved.

The replacement palette passed the owned dynamic-consumer check and three predetermined fresh-process local scheduling runs, all retained under `local-palette-*.json`. Those local runs exercise Automatic/Pages transitions but do not toggle the developer's OS appearance or certify visible desktop interaction. The next hosted candidate must repeat shown normal/High Contrast, both-direction automatic OS transitions, native/browser/export regressions and its own package lifecycle. First-layout acceptance is still unachieved.

### Corrected stock-runtime comparison at 7e80d1a

[Run 39160646840](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39160646840) compares `0237b08` with `7e80d1a` using the corrected identical compiled harness per host. All 264 independent correctness outcomes and all 132 paired initial raster comparisons pass. Acceptance remains **FAIL**, with 13/66 and 19/66 failing candidate samples. The table again reports observed median / maximum over the eleven prescribed samples; each temperature and raw record remains separate in `7e80d1a-replication-*.json`.

| Fixture | Replication 1 baseline | Replication 1 candidate | Replication 2 baseline | Replication 2 candidate |
|---|---:|---:|---:|---:|
| details | 64.32 / 105.27 | 60.50 / 106.91 | 76.02 / 120.27 | 75.24 / 118.93 |
| list | 386.01 / 502.55 | 383.68 / 459.75 | 429.12 / 587.59 | 499.18 / 608.83 |
| paragraphs | 126.39 / 151.87 | 112.43 / 158.43 | 151.67 / 247.67 | 156.21 / 232.16 |
| quote | 52.72 / 99.87 | 54.02 / 97.96 | 74.85 / 110.37 | 60.49 / 134.35 |
| table | 153.25 / 193.75 | 141.67 / 167.57 | 179.68 / 224.90 | 189.06 / 267.67 |
| mixed | 230.71 / 346.69 | 230.88 / 269.57 | 260.70 / 353.83 | 264.39 / 390.47 |

This includes regressions: list, mixed and table are slower in replication 2, and several maxima rise. It does not establish a six-fixture first-layout improvement. The unchanged acceptance remains independently red.

The same compiled full-resource shown harness passes against both products on each host. Maximum observed Input gaps were baseline/candidate **1,835.33 / 1,075.38 ms** in replication 1 and **2,397.40 / 1,182.04 ms** in replication 2. These are observed maxima for this scenario/order, not frame latency, a percentile or a hardware-independent guarantee. Both directions of actual OS High Contrast transition pass automatically on both products, with every observed installation current. Disable notification/current-preview elapsed time was 2,823.04 / 2,122.35 ms and 3,506.54 / 2,368.54 ms respectively; OS broadcasting, WPF notification, construction and layout are inclusive here. The stock candidate still has substantial non-interruptible formatting.

The 64-item native segmentation experiment also misses: list first/repeated 359.20 / 311.44 ms on replication 1 and 413.00 / 429.23 ms on replication 2. Its logical grouping changes make it ineligible as a product solution even apart from timing. The production list fixture already has the minimal native shape of one List, 4,000 ListItems, 4,000 Paragraphs and 4,000 Runs; single-literal bold formatting does not add a separate formatting container.

### Framework-level diagnostic prototype (pending)

WPF's [list visual validator](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListParaClient.cs) iterates all markers. Its [marker formatter](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListMarkerLine.cs) creates and disposes a formatted TextLine for each item. Explicit line height and first-line markers also fall outside the framework's [simple-line fast path](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/TextFormatting/SimpleTextLine.cs). Removing the established line height or native markers would change required appearance/semantics. These internals have no application-level cancellation/yield hook.

A diagnostic-only tool under `scripts/native/experiments/MarkerReuse/` now tests a bounded framework alternative: reuse the native Disc marker TextLine within one existing ValidateVisual scope and release it before the existing marker handle. Numbered markers, actual native List/ListItem objects and complete content remain. This is an isolated modified runtime, **not a product dependency or package**; its framework signature is no longer Microsoft-valid/trusted. The harness labels it EXPERIMENT_ONLY and records the actual loaded framework hash and scalar reuse count.

A local hidden-HWND sanity run loaded the private copy, exercised 3,999 reuses and passed native content/navigation correctness. It is not performance acceptance or GUI evidence. The first tool attempt failed a private-field access check; the flag is now assembly-internal within the framework. Neither attempt changed the installed runtime. The hosted workflow independently compares original/modified private runtime folders with the same application/harness/fixture bytes and the fixed 66-sample policy. ReadyToRun is disabled equally because rewriting the framework emits IL; those samples are a separate experimental temperature and will not be pooled with stock-runtime acceptance. No runtime fork is bundled or certified by this work. Results must inform an explicit architectural decision if the stock control cannot meet 250 ms.

### Stock and framework replication at 1f0463e

Stock [run 39163671147](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39163671147) retains two new paired comparisons. All 264 correctness outcomes and 132 paired rasters pass. Candidate first-layout/input acceptance fails 46/66 and 16/66 samples. This is not target completion. The product is unchanged from `7e80d1a`; the harness adds opt-in runtime-experiment identity only, with no framework hashing in ordinary measurements.

| Fixture | Replication 1 baseline median / max | Replication 1 candidate median / max | Replication 2 baseline median / max | Replication 2 candidate median / max |
|---|---:|---:|---:|---:|
| details | 135.89 / 271.57 | 176.65 / 263.00 | 59.09 / 111.00 | 59.68 / 105.65 |
| list | 1310.53 / 1563.90 | 1323.69 / 1710.32 | 388.45 / 469.58 | 401.34 / 465.70 |
| paragraphs | 243.51 / 393.37 | 291.74 / 408.10 | 109.06 / 186.98 | 115.75 / 186.62 |
| quote | 131.30 / 264.54 | 156.73 / 256.09 | 47.67 / 124.44 | 53.67 / 110.75 |
| table | 338.06 / 520.80 | 417.71 / 483.71 | 148.89 / 187.14 | 152.84 / 269.68 |
| mixed | 468.84 / 698.68 | 551.02 / 685.66 | 223.72 / 270.03 | 224.83 / 448.73 |

Times are milliseconds and describe the prescribed eleven mixed-temperature samples per fixture. They include every regression and slow sample. Separate first-combined, genuine fresh-process cold, first/repeated construction, first/repeated fresh-source edit records are retained in `1f0463e-temperature-samples.json`, alongside full per-sample end-to-end readiness, allocations, natural GC counts and process memory in `1f0463e-comparison-*.json`. No sample was discarded or retried to obtain a pass. No p95/p99 claim is supported by these sample counts. Readiness medians across the six fixtures are 1,075.2 / 1,096.7 ms baseline/candidate for fresh-process cold on host 1 and 492.3 / 497.2 ms on host 2; that context does not establish a cold-readiness gain. Repeated-construction medians are 407.5 / 381.2 ms and 231.0 / 206.5 ms respectively. Original per-fixture temperatures remain the authoritative comparison.

Same-host shown correctness passes both products in both replications. Maximum observed Input gaps are baseline/candidate **4,164.82 / 2,386.14 ms** and **1,793.09 / 1,284.58 ms**. These are measured workload maxima, not universal bounds. Both actual High Contrast directions pass automatically with no stale installation. The earlier `7e80d1a` diagnostic trace has 37 theme-update spans with maximum 84.50 ms; viewport saving max 118.58 ms, selection restoration max 1.45 ms and outline refresh max 1.80 ms. Inclusive preparation/OS notification spans and exclusive synchronous phase markers remain separate, and UIA transport is not used as dispatcher latency.

[Framework experiment 39163671085](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39163671085) completed both fixed 66-pair replications. Each preserves native structure, styled text, actual distant selection/navigation and byte-identical initial rasters. Loaded framework hashes and positive reuse counts prove the private copy executed. Nevertheless it fails 29/66 and 33/66 timing samples. List median / maximum is original/modified **822.99 / 1113.26 → 721.10 / 850.08 ms** on host 1 and **1101.25 / 1278.55 → 1016.29 / 1288.06 ms** on host 2. Mixed is **304.21 / 518.47 → 264.30 / 512.94 ms** and **413.24 / 801.46 → 417.75 / 778.49 ms**. The second host's mixed median regresses. ReadyToRun is disabled equally, making these a separate experimental temperature; they do not certify stock-runtime performance or a distributable runtime. All six cases and original/modified raw samples remain in `1f0463e-framework-marker-*.json`.

Actual hosted Dark and High Contrast screenshots at `7e80d1a` are retained beside the scalar evidence and were visually reviewed for readable document, controls, menus and outline. These are source-labelled real GUI captures. They do not certify a later source head or physical clean-Windows/screen-reader/high-DPI behaviour.

### Explicit acceptance and next architectural decision

**The original goal remains incomplete.** Bounded application changes are implemented and improve the demonstrated shown dispatcher workload, but stock first-layout acceptance fails. The `.749` historical first-launch cause remains unresolved despite finite independent original-package reproductions. No cleanup or successful later package is described as its causal fix. No production-readiness claim is made.

## Architecture decision draft — stock Continuous layout remains incomplete

The implemented application changes preserve the current stock .NET 10 WPF FlowDocument viewer, actual List/ListItem controls, all content and the established first-layout clock. They reduce demonstrated dispatcher work but do not meet 250 ms. First-launch .749 causality remains unresolved.

The next renderer decision cannot be justified as a small cache or scheduling adjustment. The list fixture already uses the minimal native List/ListItem/Paragraph/Run representation. Native list visual validation traverses every marker; explicit 23-DIP line height and marker properties select the framework's full formatting path. The synchronous PTS formatting and visual pass has no supported application cancellation or dispatcher yield hook. Changing it by hiding content, changing markers, removing line height or deferring required layout would violate this task.

Two native prototypes were tested, not merely proposed. Splitting the existing native list into 64-item groups still missed 250 ms and changed logical grouping, so it was not accepted. A private-framework prototype reused Disc TextLines inside the existing validation scope while preserving actual native objects, styled text, selection/navigation and rasters. Both 66-sample replications passed independent correctness but failed 29/66 and 33/66 timing samples. It uses a different, equally disabled ReadyToRun policy and cannot be relabelled as stock-runtime acceptance. Its modified framework is not Microsoft-signed/trusted and is not distributed.

This evidence demonstrates the remaining constraint in the tested stock viewer and bounded alternatives; it is not a mathematical impossibility claim for every future WPF design or hardware configuration. No tested implementation satisfies the original goal. Keep the performance gate red and the goal incomplete.

Proposed decision: retain the verified stock-viewer improvements in Draft PR #37, and separately approve a native Continuous layout architecture prototype with explicit text-selection, search, source-anchor and UI Automation parity. That would replace the FlowDocument/PTS layout dependency for Continuous mode, retaining .NET 10/WPF, source/recovery/workspace ownership, current Pages/export paths and the unchanged full-layout 250 ms target. It needs a new design and independent correctness/measurement contract before implementation; it must not claim a usable first viewport as complete layout. An upstream WPF fix is an alternative, but the marker experiment alone does not demonstrate the target and no runtime fork is approved for packaging.

The concrete approval question is whether to authorise that change to the existing Continuous layout/selection/automation architecture. The current bounded implementation and packages cannot be declared accepted instead. No merge, release, tag, production signature or ordinary desktop installation is authorised.

Final head, source/base/dependency, final CI job outcomes and exact final-source unsigned package identities are maintained in the PR's final receipt. Evidence above keeps its own source/harness/runtime identity. After a source or harness change, the applicable exact-head checks and package provenance must rerun; no previous package is substituted. Historical-upgrade skips remain skips.
