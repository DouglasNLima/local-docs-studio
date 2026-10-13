# Native performance and responsiveness

Recorded on 13 October 2026. Continuation of accepted `3dfda9fe6152b93bd9f6def2f68bd5e2d6d2635b`, on `feature/native-wpf-rebuild` and draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21). The reconstruction, earlier acceptance reports, fixtures and failures retain their original identities.

**Performance acceptance remains incomplete.** A small native optimisation improves the representative list, but its Continuous first-layout input still fails the unchanged **250 ms** budget. Mixed content and the 4.14 MB preview remain unresolved. The separate 587.9933 ms details-construction failure has no verified correction. Passing compatibility checks must not be read as performance certification.

## Findings and implementation

The renderer already constructs a private, complete `FlowDocument` cooperatively, checking cancellation and yielding at 32 units or eight milliseconds. Parsing and immutable preparation run on a worker; frozen raster/vector preparation uses the existing STA worker. WPF objects, attachment, layout, selection and navigation remain on their owning dispatcher. The previous preview is retained until a complete current result can commit.

The first cold `--layout` reproduction on this machine recorded list **3,071.29 ms**, table **970.21 ms**, quote **375.15 ms** and details **426.93 ms** first-layout input. Construction input was separately 0.98/117.93/1.81/59.42 ms. This confirms the accepted defect without substituting construction for layout. Historical 2,022.45/771.36/349.77/230.93 ms observations remain unchanged in the engineering report.

An EventPipe sampled-thread trace includes the UI thread in `FlowDocumentView.MeasureOverride`, `FlowDocumentPage.FormatBottomless`, recursive `ContainerParagraph.FormatParaBottomless`, glyph measurement, `ListParaClient.ValidateVisual` and `ListMarkerLine.FormatAndDrawVisual`. This supports a large native text/container formatting and visual-validation bottleneck. WPF's [bottomless formatter](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/Documents/FlowDocumentFormatter.cs) disables dispatcher processing while formatting, validates visuals before updating the viewport, and cannot be interrupted by adding yields around the application call. The [list visual implementation](https://github.com/dotnet/wpf/blob/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/MS/Internal/PtsHost/ListParaClient.cs) traverses its list-item subtrack and formats markers. These framework sources explain the observed stacks; they are not a claim that a sampled trace attributes every millisecond to one method.

The implemented renderer change uses one native `Run` for emphasis containing exactly one literal child, applying the existing formatting properties directly. It removes the redundant `Span` plus child `Run` pair. Complex emphasis, entities, links, code, breaks, semantic spans and nested formatting retain their native containers. The 4,000-item list retains one WPF `List`, all items, order, markers and source mappings. No source, reading preference, exports, undo history, asset or cancellation contract changes.

A regression also reproduced an active-tab close race. WPF's two-way selection binding cleared `ActiveDocument` synchronously when its selected collection item was removed; checking ownership after removal left surviving tabs without an active document. The close service now captures ownership before removal and selects the existing survivor. The actual close command regression verifies exact saved bytes and that an obsolete render cannot replace that survivor.

## Alternatives investigated

- **Live incremental construction:** a measured prototype attached the empty native document before construction, keeping the same native text model. Repeated invalidation of the growing 4,000-item list exceeded the 60-second construction deadline. It was rejected and removed; the complete-result commit remains intact. Prototype source/diff and original timeout are retained under ignored `acceptance/experiments` and the session's original output.
- **List subdivision:** the earlier 128-item subdivision experiment was reviewed. It offered no useful layout improvement and was removed in the accepted implementation. It has not been reintroduced or used to weaken the one-list regression.
- **Viewport/block virtualisation:** an arbitrary `FlowDocument` does not become virtualised through `VirtualizingStackPanel`. Detached content would invalidate the existing native text-pointer, contiguous selection/RTF, accessibility and distant-target contracts. A replacement presentation/selection model was not introduced speculatively.
- **Background WPF layout or renderer reuse:** moving dispatcher-owned document objects between workers and the visible viewer is unsafe; frozen media already crosses the existing STA boundary safely. There is no demonstrated safe reuse of old source spans/selections after document replacement. Those approaches were not adopted.
- **Deferring one full layout or switching modes:** neither bounds its uninterrupted work. Explicit Continuous remains Continuous; Automatic's existing Pages policy is unchanged.

## Comparable local measurements

Windows 11 Home **10.0.26300**, Intel Core i7-1360P, 16 logical processors, approximately 32 GiB RAM; .NET SDK **10.0.401**, runtime **10.0.12**, Release/framework-dependent validation. Hidden native HWND, 640 × 300, 100% zoom. Four predeclared sequential baseline/optimised process pairs used identical sources, preparation and measurement boundaries. No test suites or tracing ran alongside these pairs. The list is first in each fresh process; later fixtures are warm within that same combined suite. Caches/power/other desktop activity were not globally controlled. This is one machine, not a universal guarantee.

The baseline is the accepted renderer source compiled into the same instrumented host. Native DLL SHA-256: baseline `A64C7B7D8CEDDB9555326090CBA304C4ACCE580CFF5985CEB69252A4C1E9F3FD`; optimised `71189107B970CE7D60C842C49F12681AFF539FA95EE44321A7583B4420A4D3B8`. Each sample records its actual assembly hash, runtime, viewport, individual operations and correctness outcomes. Restored-source builds initially reused a cached baseline DLL; those samples were relabelled and excluded. A separate contended batch and traced exploratory runs were also excluded. Explicit rebuilds and isolated binary copies produced the comparison below.

Input is queued **before attachment and first layout**. Construction input, first-layout input and the maximum serviced-input gap are distinct. Useful layout/raster time and completion-to-dispatcher-idle are also distinct. `UpdateLayout` includes descendant layout; root Measure/Arrange counters are inclusive diagnostics and cannot be summed to attribute all descendant work. A hidden raster is not a measured physical first frame.

| Fixture | Baseline median input | Optimised median input | Baseline / optimised worst | Median completion, before / after | 250 ms acceptance |
|---|---:|---:|---:|---:|---|
| 4,000-item list | 923.42 ms | 727.88 ms | 1,767.89 / 1,710.67 ms | 1,182.30 / 923.90 ms | **FAIL: all four optimised samples** |
| 1,000-row table | 318.06 ms | 227.74 ms | 464.75 / 429.45 ms | 939.64 / 853.66 ms | **FAIL: one optimised sample** |
| 1,200-paragraph quote | 114.43 ms | 96.45 ms | 126.41 / 152.99 ms | 180.65 / 159.77 ms | PASS in this warm context |
| Open details, 1,200 paragraphs | 150.90 ms | 158.31 ms | 199.56 / 173.88 ms | 302.86 / 304.50 ms | PASS in this warm context |
| 1,200 rich paragraphs | 360.32 ms | 354.44 ms | 407.91 / 424.38 ms | 590.38 / 621.90 ms | **FAIL: three optimised samples** |

List median first-layout input improves **21.18%**; median construction is 246.33 → 183.17 ms. Median allocation through construction/initial layout is **123,143,588 → 105,846,548 bytes**, a **14.05%** reduction. Median process peak working set is 243,421,184 → 239,536,128 bytes. Those peaks include the validation process; later-fixture peaks include preceding cases and cannot be called installed-app memory budgets.

Rich-paragraph allocation falls 48,808,688 → 44,677,028 bytes, but completion is approximately 5.3% slower in this sample set and input is not materially improved. Plain table/quote/details contain no eligible literal emphasis; their timing differences are not attributed to this change. Their variation and failing cold historical contexts remain visible.

A separate later built-host observation recorded list input **3,529.85 ms** (table 484.31, quote 98.94, details 202.31, paragraphs 323.62 ms). It is outside the controlled comparison and remains a retained **FAIL**; 1,710.67 ms is the worst comparable optimised sample, not the worst of every exploratory run. The diagnostic's temporary document-background mutation was subsequently removed; capture background is supplied by the empty viewer, before the measured document is attached.

Individual first-layout input samples, in pair order (milliseconds):

| Fixture | Baseline samples | Optimised samples |
|---|---|---|
| List | 1767.89, 860.51, 986.32, 816.51 | 1710.67, 692.13, 763.64, 674.61 |
| Table | 464.75, 445.99, 187.29, 190.13 | 429.45, 229.34, 210.91, 226.14 |
| Quote | 126.41, 117.51, 90.94, 111.35 | 152.99, 103.84, 85.82, 89.06 |
| Details | 199.56, 163.80, 138.00, 134.57 | 158.40, 173.88, 137.07, 158.22 |
| Paragraphs | 407.91, 358.77, 299.70, 361.86 | 306.06, 424.38, 243.97, 402.81 |

Exact JSON, operations, allocation/GC and raster evidence is retained in `acceptance/performance/paired-{baseline,optimised}-{1..4}`. The list and paragraph initial PNGs in pair 2 have identical before/after file hashes. These original captures have transparent surfaces; the diagnostic now supplies a white capture background without changing document content.

## Mixed and 4.14 MB limitations

The mixed fixture combines 1,000 list items, 300 table rows, nested open details/quotes, Unicode, 12 repeated images, Mermaid, an internal link and a distant heading. Both versions retain complete text/source and reveal/select the distant target. Single-process observations are **2,410.65 ms baseline** and **6,097.98 ms optimised** first-layout input; completion 3,775.24 → 12,609.62 ms, allocation 56,587,624 → 51,542,152 bytes. Cold media preparation is 5,178.42 → 12,235.84 ms. These highly variable single observations demonstrate an unresolved failure and an observed worse time, not a defensible percentage improvement or a universal regression estimate. No unchanged retry-until-green was performed.

The **4,140,025-byte / 20,000-paragraph** source retains all 20,001 mapped elements and complete logical text. A corrected stage probe records construction **6,654.39 / 9,251.85 ms** and first-layout input **738.53 / 815.91 ms** (baseline / optimised), both **FAIL**. Waiting for background layout/dispatcher idle exceeds the unchanged 60-second diagnostic deadline; total allocation reaches **1,832,944,280 / 1,871,562,624 bytes**. Maximum input gaps during observation are **1,377.67 / 815.23 ms**. Full layout completion and distant-preview navigation are **NOT VERIFIED** for this source. Existing editor navigation/source/Undo checks for it remain separate.

An earlier probe incorrectly left no partial result when its idle wait timed out. The stack places that wait after construction, not inside it. New `large-4mb-first-layout.json` and `large-4mb-failure.json` retain both boundaries and the failure. No timer/budget was increased to obtain a pass.

## Intermittent details construction

Original [run 38344048333](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38344048333), revision `77be5707d5a67dfcb5525d699eeb2dee691985a2`: input **587.9933 ms**, initial call **587.7282 ms**, construction **671.4819 ms**. Its original failure log and JSON remain retained; the engineering report's original evidence directory is unchanged. CPU/GC did not advance across the long toggle/header phases; scheduling, waiting or contention is plausible, not proven. Existing bounded content-free phase/CPU/allocation/input diagnostics and validation-owned stall-stack capture remain enabled only for investigation. Later non-reproduction, including the current details observations, does not establish a fix. **Status: investigated, unresolved.**

## Regression and CI evidence

Existing 250 ms construction/input/cancellation assertions remain. Formatting checks now inspect styled native text rather than requiring the redundant `Span` representation; exact text, decoration, count, link, cancellation and source assertions are retained. New regressions cover literal formatting in both themes, Unicode/combining/surrogate selection, native RTF text transfer, nested formatting, exact LF/CRLF source bytes and distant heading offsets. At the first native construction yield, the scheduling suite edits, rapidly switches tabs and invokes the actual saved-tab close command, verifying current preview/source/Undo and preserved files.

Hosted desktop validation additionally explicitly selects Continuous for the 4,000-item document, finds and visibly highlights the last target while query focus is retained, switches to Split without a silent mode override, and verifies complete Undo and unchanged source/preferences. Built/installed/upgraded/reinstalled contexts retain these screenshots and selection records. These checks have not been executed on the user's desktop: new-build manual GUI remains **BLOCKED by authorisation**, with execution **NOT RUN**.

The native pipeline records standard and mixed first-layout observations separately from compatibility validation. The observation command's successful completion means evidence/correctness checks completed; its JSON/console **FAIL** against 250 ms remains a performance failure. It is not a timing test pass. The 4.14 MB timeout is retained locally rather than hidden in a green observation.

Local Release build passes with zero warnings/errors; the full Core suite passes **1,057**, zero failed/skipped. Native validation passes **40 corpus cases**, zero failures, plus the existing persisted-workflow checks, 16 literal/nested formatting observations and seven scheduling checks. Static integrity passes; 268 Chromium/Edge cases and final push/PR pipelines are being validated. Their final results and exact code/evidence revisions will be recorded here and on PR 21 before handoff. Original failures remain unchanged. No merge, release, tag movement, production identity or desktop installation is authorised by this work.

## Readiness

Retain this small allocation/layout improvement and the reproduced close correction if compatibility CI passes. Continue internal native evaluation, but **do not certify the native performance objective as achieved**. A safe presentation strategy that bounds composite-block layout, background invalidation and distant-target work remains necessary; it must preserve the native text/selection/navigation contracts before adoption. Installed hardware-specific latency/memory, physical first frame, screen-reader/DPI/multi-monitor and authorised new-build manual acceptance are not certified by the hidden-HWND measurements.
