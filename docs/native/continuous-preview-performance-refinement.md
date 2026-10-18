# Continuous Markdown preview: first layout and responsiveness

## Scope and dependency

This continuation is stacked on open Draft PR [#36](https://github.com/DouglasNLima/local-docs-studio/pull/36),
verified at `9a64dfa5adf79dc447db1f671ea39c94ec42de40`. Remote `develop` was
`6a94dd3672f95a267402e2218935d064a6ee6401` at inspection. Neither branch is
merged by this work. Branch: `feature/native-continuous-preview-performance`;
base: `feature/native-mermaid-suite-ux-performance`. Rebase/retarget after the
dependency is integrated, then repeat integration checks at that revision.

**The 250 ms goal is incomplete.** Implementation and compatibility checks must
not be represented as first-layout acceptance. Exact-source hosted comparison,
interaction and package receipts will be reconciled here after execution.

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

- Reserve the native Continuous vertical scrollbar gutter before content layout,
  avoiding width invalidation when a long document needs its scrollbar. Short
  Continuous previews also retain the gutter. Pages and Automatic's mode choice
  remain unchanged. Every native list/item/paragraph remains present and styled.
- Use WPF `Table.BeginInit/EndInit` around private cooperative table construction.
  Rows/cells still attach parent-first. Final structural validation occurs once
  on the owner dispatcher, including the cancellation cleanup path; it is a
  synchronous, non-interruptible stage with opt-in phase markers.
- Recheck source version, document owner, appearance, resolved diagram palette,
  source/workspace identity and window lifetime before installing a result.
  Stale failures cannot replace current status. Closing prevents new preview work.
- Map a retained preview selection through source versions into the replacement
  document without moving keyboard focus. Existing source-based viewport and
  scroll-sync ownership remain in force; current editor selection takes priority
  when selection synchronisation is active.

No document cache, partial viewport, deferred required content, forced collection,
fixture reduction, grammar/limit change or export shortcut is introduced. WPF
objects remain dispatcher-owned. Parsing/native media conversion and synchronous
layout can finish after cancellation; their stale results cannot install.

A display-mode text-formatting experiment did not show consistent benefit and was
removed. An eager-template experiment exposed a WPF finite-pagination fail-fast
in the unshown harness; forced template creation for unloaded readers was removed.
Original diagnostic logs remain in the worktree's ignored evidence directory.

## Measurement and acceptance protocol

`NativeContinuousPerformanceChecks` retains the original first-layout boundary:
queue Input immediately before assigning the complete document, synchronously
Measure/Arrange/UpdateLayout, then observe callback delivery. Parsing, media and
document construction are outside that span and reported separately. Inclusive
root Measure/Arrange/UpdateLayout diagnostics overlap; do not add them together.
End-to-end readiness begins before parsing and ends after complete construction,
attachment, layout and ApplicationIdle. Raster/correctness/navigation follow.
This hidden-HWND protocol does not measure a physical first display frame.

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

## Validation and delivery receipt

Pending exact-head hosted execution: six-fixture comparison and independent
replication; visible native opening/edit/replacement/reopen, rapid/distant edits,
theme/actual High Contrast, tab/window closure; existing source/safety/export,
five-editor regression, browser/static and isolated package/lifecycle jobs.

Physical first frame, higher DPI, physical screen reader/clean Windows and
production signing remain outside this scoped certification. No ordinary desktop
installation, merge, release or tag movement is performed. Any produced preview
must identify this exact clean source and pass its own isolated verification;
#36's package cannot certify this continuation.
