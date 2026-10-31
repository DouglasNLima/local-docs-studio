# Goal #56: native Continuous architecture feasibility

Assessment baseline: `86d60322f6f1f1dec06974bf5f9e1738a3bb80c7`, tree
`552158f43ebf2501c684d210dd9ebae80a3e6f8f`. This is the latest fetched
`origin/develop` at the start of this investigation on 31 October 2026.
The engineering receipt records the eventual prototype identities and results.

This is a bounded feasibility investigation. **The existing 250 ms gate remains
FAIL until the complete protected acceptance contract is satisfied.** No product
engine is enabled by this document. Pages, Automatic, exports, web/PWA, trust,
privacy, Save/UIA and the separate preview-loading harness remain unchanged.

## Source identities and historical evidence

- PR #69 remains Draft, unmerged, at
  `c85a51118ac0f175cb21c79c3f7503ee1bc8ecd0`.
- PR #70 remains Draft, unmerged, at
  `460be40d42c1c7248249e4b10a677078db474baa`.
- Paused PRs #38 and #50 are not implementation dependencies and must not move.
- `0237b08390d5d1d946c0b19b81d671ccd54fb951` remains the protected script's
  historical performance baseline. It is distinct from the current develop
  baseline used to assess the architecture. Neither identity is substituted.
- [PR #70's performance run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41180694388)
  retains 41/66 and 17/66 failing candidate samples on separate hosts. Individual
  list observations around 1,689 and 513 ms are host-specific, not medians or a
  universal guarantee. Passing visible normal/High Contrast jobs do not replace
  either failed comparison. The historically failed `complex-er-preview` check
  has an inconclusive root cause; this investigation does not resolve it.

## Existing dependency map (before implementation)

Paths below are relative to `src/native/LensDocsStudio.Native/` unless specified.
These are public C# contracts used within the native solution, not a promise of
an externally versioned renderer SDK.

| Owner / contract | Actual dependency | Consequence for an alternative |
|---|---|---|
| `Rendering/NativeMarkdownRenderer.cs`, public `PreviewResult` | `FlowDocument`, dictionary of source offsets to `TextElement`, `PreviewSourceMap`, details `Expander`, `InlineUIContainer` formulae, media `Image` / `BlockUIContainer` | A new semantic result cannot be passed as an existing preview without an adapter. Preserve the original export and Pages result. |
| `Rendering/DocumentPreview.cs`, public `SetPreview`, `ApplyReadingMode` | Inherits `FlowDocumentReader`; Continuous resolves to Scroll; Automatic chooses Pages at 256 KiB source or 1,000 source elements | Alternative only for explicitly requested Continuous after parity. Do not change Automatic selection. |
| `Rendering/DocumentPreview.xaml` | Reader's `PART_ContentHost`, selection appearance, page commands/count, guidance | An alternative host needs its own scroll, focus and text behaviour. Replacing the reader template alone does not replace PTS. |
| `Rendering/PreviewSourceMap.cs`, public `PreviewLiteral` and range methods | `Run`, `TextPointer`, `TextRange`, exact literal offsets including offsets within coalesced runs | Use revision-bound display/source intervals with affinity; do not search text to resolve duplicate occurrences. |
| `Rendering/PreviewSemanticAnchors.cs` | Source `TextElement` dictionary, pointer parent/tag, native character rectangles, page paginator | Replace anchor, viewport and geometry queries together for a new engine. |
| `Rendering/PreviewNavigationService.cs` | `FlowDocumentReader`, `FlowDocumentScrollViewer`, `TextPointer`, selection, native page paginator; cancellation tied to reader/document identity | Need equivalent generation-owned navigation and selection. Never use an old pointer with a new document. |
| `Rendering/PreviewFindIndex.cs`, `MainWindow.Find.cs` | Traverses `FlowDocument` pointer contexts; embeds formula source; maps matches to pointer ranges | Alternative search must preserve structural separators, exact occurrence, next/previous and focus. |
| `MainWindow.Synchronisation.cs` | `TextSelection`, source-version mapping, retained selection, source anchors, `PreviewSourceHit`, scroll events, highlight adorner | Cannot retain this code by drawing an unrelated visual over a hidden FlowDocument. |
| `MainWindow.Navigation.cs`, `MainWindow.xaml.cs` | Heading/link source element lookup, `ContentStart`, viewport restoration, tab identity, selected source revision | Adapter must expose source navigation and viewport restoration without making layout lazy. |
| `Rendering/DocumentPreview.cs` automation peer | `FlowDocumentReaderAutomationPeer` delegates TextPattern to the real document's `ContentElementAutomationPeer` | Drawn glyphs have no automatic document TextPattern. A real new text provider and range provider are necessary. |
| Renderer native controls | Hyperlinks, details expanders, media actions, code/table context menus, formulae and diagrams | Retain keyboard, native control and accessibility semantics, not only text labels. |
| `MainWindow.xaml.cs` preview ownership | Worker parsing/media; dispatcher-owned private document; request, tab, source version, path, workspace, appearance and cancellation checks before installation | Immutable preparation is transferable; mutable WPF objects are not. Old results/failures must never replace current state. |
| `MainWindow.PreviewLoading.cs` / Core `PreviewLoadingState` | Request/generation-owned progress through parsing, media, building, attachment, completion; cancellation preserves previous complete preview | Do not equate a first viewport with completion. Keep the independent 25-second loading test unchanged. |
| Validation `NativeContinuousPerformanceChecks.cs` | Complete text, source hashes, native element structure/styles, pointer selection/navigation, PNGs; original queued Input boundary | Preserve the existing oracle. Add independent semantic and geometry checks for the experiment. |

`PreviewSourceHit` is declared in `MainWindow.Synchronisation.cs`; it is not a
separate source file in this baseline. Source and preview operations also depend
on AvalonEdit's document revision identity. Selection is focus-sensitive and
deferred navigation is cancelled by real input or a newer document.

The renderer already uses one compact native list, one item and paragraph per
source item, with one bold span and literal run for the retained list. It yields
during construction; synchronous bottomless PTS layout still blocks the UI.
The upstream [WPF 10.0.12 list visual implementation](https://github.com/dotnet/wpf/blob/v10.0.12/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListParaClient.cs)
enumerates item paragraphs and formats markers during visual validation. This
supports the cost hypothesis; it is not an exclusive-time profile.

Previously completed literal coalescing, table initialisation, viewport indexing,
ownership, palette and dispatcher corrections remain. The 64-item segmentation,
simple formatting/font/scrollbar changes and resource batching experiments did
not establish acceptance; resource batching encountered a WPF failure. Private
runtime marker reuse still failed 29/66 and 33/66 samples and required an unsigned
modified framework. No framework fork or private framework API is proposed.

## Alternatives

| Concern | A: dedicated TextFormatter surface | B: structured native blocks | C: existing FlowDocument |
|---|---|---|---|
| First-layout hypothesis | Avoid PTS and per-item document-container/marker validation; measure every formatted line/marker. No timing guarantee. | Independent rich block hosts may reduce one PTS tree; thousands of controls/formatters and table sizing may dominate. | Current compact list is already expensive; no new supported API evidence bypasses its work. |
| Full processing | Format all lines and markers at actual width before completion; retain line objects/bounds. | Every block and wrapped cell must have exact geometry, including offscreen blocks. Virtualisation alone is insufficient. | Established full document path retained. |
| Fidelity/source | Own immutable hierarchy, spans, generated markers, display/source mapping and Unicode caret semantics. | Keep Markdown blocks, but global positions across independent text containers need a new index. | Existing mappings/styles are established. |
| Accessibility | New document peer, `ITextProvider` / `ITextRangeProvider`, embedded child/control semantics, real UIA validation. | Native children help, but do not supply one document-wide TextPattern or cross-block selection. | Existing document peer and text pattern remain. |
| Memory | Retain all TextLines and interval/geometry data; measure natural allocation/GC. | All logical geometry plus per-control overhead; visual recycling must not discard semantics. | Retain current WPF tree and PTS data. |
| Scroll/navigation | Binary-search line geometry; custom hit tests, selection, viewport and anchors. | Global block index plus local range queries; cross-block operations complex. | Existing native scroll/page navigation. |
| Complexity/maintenance | High: text store, layout, bidi/clusters, selection, accessibility, media, tables and lifecycle. | High: independent containers, global selection/accessibility and consistent wrapping. | Low incremental cost, but no demonstrated materially new performance hypothesis. |
| Pages interaction | Separate engine; leave Pages document and Automatic policy unchanged. | Separate engine with original Pages renderer. | Existing implementation. |
| Expected failures | Font cold start; full-line formatting above budget; complex script/caret errors; incomplete providers/media. | Cross-block selection, focus, nested controls, table column constraints and accessibility fragmentation. | Existing list/table latency and non-interruptible layout. |

Supported [TextFormatter documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/advanced-text-formatting)
assigns text-store, span, paragraph and embedded-object management to the client.
[TextLine](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.textformatting.textline?view=windowsdesktop-10.0)
provides retained line metrics, drawing, caret/hit and bounds operations. These
APIs make A a measurable hypothesis, not a ready-made document control.
Microsoft's [TextPattern contract](https://learn.microsoft.com/en-us/dotnet/framework/ui-automation/ui-automation-textpattern-overview)
requires real text/range providers, selection support when offered, and character
and document units. A visual or automation name cannot stand in for that contract.

## Decision and bounded implementation order

Select A for an isolated diagnostic experiment, outside the product and solution
package. First test the full-list layout cost before implementing a production
adapter or a broad Markdown framework. It must accept parsed source, not fixture
names, counts or known results. Unsupported constructs fail explicitly. No hidden
FlowDocument may be used to lend accessibility to unrelated drawing geometry.

The first spike is a **necessary-cost feasibility test**, not milestone-one
acceptance: all list lines and markers, real wrapping, style/source intervals,
retained native geometry and distant hit tests. If that cost cannot approach the
target, stop before implementing a selection/UIA surface whose cost can only be
additional. Missing selection or actual UIA prevents technical acceptance even if
this lower-bound experiment happens to run below 250 ms. A promising lower bound
requires further implementation and independent review before milestone two.

No unfinished surface is installed by `DocumentPreview`. Its disabled-by-default
selection mechanism is explicit invocation of the standalone experiment only.
This is a smaller isolation boundary than an application setting. There is no
user preference, environment switch or production fallback to an incomplete view.

## Minimum semantic and layout contract

Before layout, own an immutable snapshot with source SHA-256 and generation,
source text, ordered paragraph/list hierarchy and stable indices. Each paragraph
has half-open source bounds, its parent item/list/depth/number and a read-only
sequence of styled display spans. Each literal carries exact source start/length;
generated marker/separator positions are explicitly distinguished. Unsupported
decoded/nonliteral mappings must be rejected rather than guessed in this spike.
No WPF visual or parsed mutable node crosses dispatchers.

On its owning STA, layout consumes width, DPI, font, zoom and appearance. It
retains every TextLine, line start/length/bounds, paragraph relationship and marker
placement. Lines are disposed with their owning layout, including failed or
cancelled construction. Hit tests use retained WPF line geometry and exact
interval mapping. Completion identity must include generation/source identity,
width/DPI/zoom/appearance and all line/marker counts. A layout cannot be attached
after replacement or cancellation. No offscreen text formatting is deferred.

For an eventual accepted engine, logical selection ranges refer to one display
revision, with source endpoint affinity and grapheme-aware caret boundaries.
Drawing, mouse/keyboard input, find, copy and UIA must use those same ranges and
geometry. Source anchors and details/media/table relationships must be explicit.
The spike does not silently claim to supply unimplemented portions of this
contract. Exact original logical text and styled ranges must be compared with the
existing renderer independently, retaining all deviations and screenshots.

## Completion and measurement contract

The existing protected workflow/script/harness/fixtures and failure rules are
immutable for this task. Six original fixtures, 66 candidate samples per
replication, two independently scheduled replications, 250 ms, original
first/repeated/cold/fresh-source policy and historical baseline stay unchanged.
The original FlowDocument structural oracle is never removed.

The separate diagnostic must retain original UTF-8 fixture bytes/hashes and use
640 × 300, 100% zoom, no prewarm and no forced collection. Record parsing/semantic
construction separately. Queue Input immediately before attachment; synchronous
Measure/Arrange/UpdateLayout must establish the **entire** retained native layout
before that callback runs. Record full readiness through ApplicationIdle,
allocation, natural GC, managed/private memory and actual loaded runtime/engine.
Report overlapping inclusive phases separately, never add them.

Layout completion for the spike means all supported paragraphs, markers, wrapped
lines and native line geometry exist. It does **not** mean complete document
interaction/accessibility. Mark both meanings explicitly. Geometry queries may
read retained TextLines after timing; they must not invoke new line formatting.
Initial and distant rasters, text/style/source comparisons and every unsuccessful
sample remain evidence. No p95/p99 or cross-host pooled latency claim is allowed.

## Finite execution budget (before hosted tests)

1. One local build/correctness pass, targeted local list feasibility measurements
   and any defect-specific correction. Keep every original failure.
2. One planned hosted diagnostic comparison, with two independent Windows jobs
   reversing baseline/prototype initial order. The PR also runs the existing
   unchanged protected acceptance and applicable native CI.
3. At most one corrective hosted comparison, only for an identified source defect
   or invalid diagnostic boundary documented before execution. No rerun for a
   better timing. Evidence-only documentation pushes must avoid launching another
   hosted performance comparison.

Stop for full-layout cost without a credible path, unsafe/incomplete source,
selection or accessibility semantics, unjustified correctness loss, or an owner
architectural trade-off. Report `CONTINUOUS_NATIVE_PROTOTYPE_PARTIAL` for an
unaccepted but useful spike, not a global impossibility theorem. No full PASS or
production integration recommendation follows from a fast subset.

## Release and validation boundary

Visible application interaction, real UIA ranges, Normal/Dark/actual High Contrast,
selection/copy, resize/zoom, cancellation, fresh-source/tab/viewport restoration,
installed lifecycle and loading ownership remain required for an integrated
engine. Hidden-HWND measurements do not certify those behaviours or a physical
screen reader. No local desktop input or appearance change is authorised by the
diagnostic; visible tests use the existing disposable hosted environments.

`PR69_INTEGRATION_NO_GO`, `PR70_INTEGRATION_NO_GO`,
`SIGNPATH_ELIGIBILITY_UNCONFIRMED`, `NATIVE_STABLE_NO_GO` and
`NATIVE_CONTINUOUS_250MS_FAIL` remain. No merge, rebase, cherry-pick, tag, signing,
certificate installation, RC/beta/Stable or native package publication is part of
this investigation. New diagnostic paths do not extend the frozen licence grant;
licensing checks must retain their explicit non-granted classification.
