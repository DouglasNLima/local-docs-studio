# Native Continuous viewer feasibility

Recorded on 13 October 2026. **Decision: NO-GO for integrating the current recycling RichTextBox hybrid as the production Continuous viewer.** The executable spike substantially improves representative first layout and proves viable document-level selection, copy, offscreen text accessibility and distant navigation. It does not establish bounded native layout for every valid logical text unit, the unchanged **250 ms** input objective is not met in all observed scenarios, and scroll/history restoration has visible defects.

The complete logical-document/selection model is useful engineering evidence for a different native surface. The existing FlowDocument renderer remains the reference and default. Full integration is not recommended by this spike.

## Adopted implementation and limitations

The isolated `spike/native-continuous-viewer` worktree starts at verified native head **`9c7454eeb892614af93881b762a1b6aecc3259d2`**, whose runtime implementation is `58e535178fdba26b02635f711a9269990cfb3fcb`. Draft [PR 22](https://github.com/DouglasNLima/local-docs-studio/pull/22) targets `feature/native-wpf-rebuild`; [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) and its branch remain unchanged. Prototype code is separate from the production solution and publish inputs.

The adopted [performance report](performance-and-responsiveness.md), [engineering delivery](engineering-completion-report.md), [continuation](continuation-handoff.md), [parity matrix](functional-parity-matrix.md), [experience improvements](native-experience-improvements.md) and [architecture decisions](architecture-decisions.md) retain their historical records. Original traces and rejected experiments were inspected in the native-responsiveness worktree; they have not been overwritten or reclassified.

The reference already parses/prepares on workers where safe and constructs native content cooperatively. Its principal remaining limitation is synchronous bottomless formatting and visual validation on the owning dispatcher. Adopted sampled stacks include `FlowDocumentView.MeasureOverride`, `FlowDocumentPage.FormatBottomless`, `ContainerParagraph.FormatParaBottomless`, `ListParaClient.ValidateVisual` and `ListMarkerLine.FormatAndDrawVisual`. Application yields cannot interrupt these calls.

Historical comparable median list/table/rich-paragraph input remains **727.88/227.74/354.44 ms**, with **1,710.67 ms** worst comparable list input. Mixed **6,097.98 ms**, large-document **815.91 ms**, its 60-second background-layout timeout/unverified distant navigation, and unresolved **587.9933 ms** details construction retain their original failures and qualifications. New measurements do not replace these records or pool different environments.

The rejected 128-item global-list subdivision and attached incremental global FlowDocument construction are not reinstated. The new hypothesis changes ownership, layout units and selection.

## Alternatives and chosen experiment

| Approach | Responsiveness and scalability | Selection, clipboard and accessibility | Complexity and maintenance |
| --- | --- | --- | --- |
| A: virtualising native item host | Restricts actual item creation/measure to viewport/cache. Estimated extent/item lookup can depend on document size. A giant child remains a giant synchronous formatting operation. | Needs logical ranges above controls; native shaping/hits remain useful. Recycling cannot own selection/source identity. | Moderate/high. Validate actual containers, nested structures, cancellation and resource release. |
| B: DrawingVisual/TextFormatter surface | Explicit line/glyph budgets and height indexing offer stronger control. Native shaping calls still require a proven bound. | Own glyph hits, bidi geometry, caret/keyboard policy, highlighting, rich transfer and complete TextPattern. Drawing speed alone proves none of these. | High/very high. Substantial interaction engine and Unicode maintenance obligation. |
| C: hybrid A with bounded native text/media | Recycling host, small per-piece FlowDocuments, native row grids and frozen raster/vector services. Avoids a global FlowDocument. | Logical selection/clipboard/TextPattern spans all pieces; controls provide shaping and point geometry. | Selected prototype. Unicode and restoration defects invalidate adoption. |

[WPF control performance guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls) distinguishes presentation/data virtualisation and accidental ways to disable recycling. [Advanced text formatting](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/advanced-text-formatting) supplies Candidate B's credible native API. No complete second renderer's performance is claimed.

The hybrid improves large list/table/mixed first layout, with higher overhead for the small rich catalogue. Memory improves for the large source but not uniformly for small documents. Selection/source cancellation work is independently viable. Correct layout, complete clipboard semantics and physical accessibility remain insufficient for replacement.

## Logical-document and viewport boundaries

`src/native/spikes/ContinuousViewer.Model` uses established `MarkdownService`/Markdig parsing and passive typed nodes. It retains original source, complete syntax/headings/targets, logical text, styled runs, source spans, nested details ancestry, table slices and per-document piece IDs. Original source is authoritative and unmodified. Complete syntax remains available when visuals are absent.

Logical text has a declared LF projection with list markers, table tabs and media descriptions. Tests compare it exactly, including Unicode/line breaks. This does not certify identical separators/formats to old FlowDocument `TextRange.Text`. Character-exact mapping is claimed only for verified literals inside parser-owned spans; decoded/generated text keeps a semantic span. LF/CRLF source bytes remain unchanged.

Presentation is a pixel-scrolling recycling WPF ListBox with native text controls, table grids and frozen image/Mermaid/maths drawings. Large structures/text are flattened/split. There is **no global complete-preview FlowDocument**. Only frozen media crosses the existing STA boundary; controls/layout stay on their dispatcher.

Selection uses grapheme-aware document anchor/focus offsets. Highlights are regenerated from that state when controls recycle. Applying partial formatting can split native Runs, so retained pointers are not used as selection authority. Item selection decoration is suppressed. Identity/revision checks reject obsolete loads, navigation and automation ranges.

Nominal pieces contain **512 UTF-16 characters**. Preserving an oversized indivisible grapheme bypasses that limit. This is an actual failed bound, not an excluded input.

## Executable location, launch and isolation

- `src/native/spikes/ContinuousViewer.Model`: logical model and selection/transfer preparation.
- `src/native/spikes/ContinuousViewer`: real WPF viewport/media/TextPattern and executable harness.
- `src/native/spikes/ContinuousViewer.Tests`: focused logical/source/clipboard regressions.

From a fresh spike checkout:

```powershell
dotnet restore src/native/spikes/ContinuousViewer --locked-mode
dotnet restore src/native/spikes/ContinuousViewer.Tests --locked-mode
dotnet build src/native/spikes/ContinuousViewer -c Release --no-restore
dotnet test src/native/spikes/ContinuousViewer.Tests -c Release --no-restore
dotnet src/native/spikes/ContinuousViewer/bin/Release/net10.0-windows/ContinuousViewer.dll --validate acceptance/continuous/validation-new
dotnet src/native/spikes/ContinuousViewer/bin/Release/net10.0-windows/ContinuousViewer.dll --stress acceptance/continuous/stress-new
dotnet src/native/spikes/ContinuousViewer/bin/Release/net10.0-windows/ContinuousViewer.dll --exports acceptance/continuous/exports-new
pwsh -NoProfile -File scripts/native/Measure-ContinuousViewer.ps1 -OutputDirectory acceptance/continuous/comparison-new
```

Use fresh output directories. Measurement rejects overwriting evidence. The separate `--cluster <output>` diagnostic reproduces native termination; it is a failure probe. Disposable Windows records actual exit/partial results. No local repeat after the first termination was used to seek a pass.

`--interactive <fixture> <synthetic-root>` launches a real native window; `--desktop <output>` drives actual keyboard/mouse/clipboard/UIA. Both require genuine disposable GitHub-hosted Windows and run through `.github/workflows/continuous-viewer.yml`. Newly built visible apps were not launched on the user's normal desktop. No installation, associations, certificates, personal documents or system settings are involved.

Production Native project/solution/publish script, browser assets, manifest, service worker and legacy entry have no diff from the reference. The spike is excluded from default production packages.

## Selection and clipboard evidence

Hidden-WPF checks cover selection within/across paragraphs, headings, lists, table rows and formerly virtualised pieces; exact logical text; Unicode/surrogate/combining boundaries; scroll/appearance/replacement; and stale ranges. Actual native RTF loading verifies Unicode/line-break round-trips. Unicode text, RTF, CF_HTML and Markdown-compatible formats derive from full logical selection.

Hosted Windows executes **24 checks across list, rich and 4.14 MB sources**: cross-process TextPattern reads/search, native keyboard cross-block/full-document extension, all four Windows clipboard formats, physical scrolling, distant geometry and mouse dragging. Offscreen text remains selectable/copyable before most controls ever exist. Local checks create DataObjects without changing the user's clipboard.

Payload preparation runs on a worker. Commit checks document/selection identity and clipboard sequence, protecting newer clipboard content. These are prototype contracts, not integration with production copy commands.

**Full clipboard parity is not established.** Plain logical selection and supported decorations are verified. RTF hyperlink/image/table structures are incomplete; CF_HTML is styled logical text rather than a complete selected table/image structure. Markdown is a rendered-text projection rather than exact source. Actual Word/Excel fidelity remains unverified. Format presence alone is insufficient.

## Navigation, source mapping and scrolling

Item 4000 (`word4000` in unchanged baseline source) is reached without preceding visuals. Comparable prototype samples reveal/select final targets. The exact large source has **one initial heading**: final-paragraph navigation uses unchanged 4,140,025-byte source, while a separately labelled **4,140,080-byte variant** appends a distant heading. Original comparison input is unchanged. The variant retains two headings, reaches UTF-16 offset **4,120,026**, exposes actual bounds and realises eight native containers locally.

Rich probes cover fragment activation, named targets, source-follow requests, literal preview-to-source mapping, delegated wiki activation, nested-details reveal, cancelled navigation and recycled source identity. Actual editor/view-model and wiki workspace opening are outside the standalone harness; callback/source contracts are exercised.

An oversized cell initially mapped every slice to one broad span because Markdig included edge whitespace while content was trimmed. Verified literal matching **inside the semantic span**, plus oversized-source regressions, now resolves distant paragraph/cell/code/quote/list targets. Failed `stress-{1,2}` remain retained.

WPF estimated/measured pixel extent and piece/relative-top anchors support Home/End, line/page scrolling, expansion and font/theme changes. **Restoration is defective:** a later sequence expects piece 10 but restores piece 12; font changes alter the logical anchor with **67.99 px** correction. Earlier 7 px/retained-anchor evidence remains distinct. Decoded-character precision, every oversized UIA scroll fragment and complete history/height reconciliation remain prerequisites.

## Accessibility scope

`ITextProvider`/`ITextRangeProvider` expose full logical order, offscreen searches/ranges, contiguous selection and change events. Heading peers retain order/level descriptions. Visible line rectangles derive from native character geometry. Replaced-document ranges are rejected.

Cross-process UIA proves more than names on graphics: offscreen text is discoverable and native selection readable. All format attributes, caret/line semantics, table/image patterns and movement edge cases are not certified. **Physical screen-reader, actual DPI/high-contrast/multi-monitor certification remains outstanding.** Theme/font tests do not certify all media contrast.

## Comparative methodology and individual observations

Final local comparison: `acceptance/continuous/comparison-2`, source **`82240572d056bb5f9bacf6f5b4df7e25d7e16004`**. Three predeclared sequential independent process pairs per fixture, each with separate cold/warm observations. Cold means a fresh process, not a controlled cold OS/font/disk cache. No tests/traces ran alongside pairs. Intel Core i7-1360P, 16 logical processors, **34,027,192,320 bytes RAM**, Windows 10.0.26300, SDK 10.0.401/runtime 10.0.12, Release/framework-dependent, **640 × 300**, 100% zoom. Global power/cache/background activity was uncontrolled.

Both engines use identical source hashes and **unchanged renderer code verified against 9c7454e**, compiled in the same instrumented checkout. Native DLL SHA-256: **`5E90A3DF08EC248E070F719C34302358BF9407145E2DB76BA97825681AE79DB3`**; prototype: **`E4E3338BD90BB24A930EE40A64EFB663ED31D055085E470CA5A43C7D52B26589`**. JSON records actual hashes. SDK source-version metadata/build context differs from historical package DLLs; those bytes are not represented as these instrumented binaries.

Input is queued **before attachment and native measure/arrange/layout**, separately from parsing/preparation. A 16 ms Input-priority timer records serviced gaps during layout/navigation/scroll. Maximum gaps also include diagnostic raster/PNG work in the observation window; causes are not fully attributed. Layout readiness, full logical readiness and end-to-end readiness are separate. Hidden raster is not a physical first frame. Successful observation recording does not mean passed acceptance.

Times are milliseconds. Individual first-layout samples precede medians. Failure counts consider first-layout and maximum input/scroll gaps.

### Cold process samples

| Fixture | Reference samples / median | Prototype samples / median | Prototype worst serviced gap | Prototype failures |
| --- | --- | --- | ---: | ---: |
| 4,000-item list | 624.85, 701.14, 802.85 / **701.14** | 125.58, 148.38, 177.35 / **148.38** | 177.40 | 0/3 |
| 1,000-row table | 260.85, 234.30, 340.10 / **260.85** | 134.00, 155.32, 175.82 / **155.32** | 175.81 | 0/3 |
| 1,200 rich paragraphs | 222.23, 252.12, 286.92 / **252.12** | 156.89, 144.95, 207.92 / **156.89** | 207.44 | 0/3 |
| Quote | 135.13, 144.26, 273.81 / **144.26** | 136.31, 145.11, 185.99 / **145.11** | 186.05 | 0/3 |
| Open details | 159.06, 140.92, 175.21 / **159.06** | 138.10, 132.40, 176.70 / **138.10** | 176.71 | 0/3 |
| Mixed | 302.50, 310.28, 400.74 / **310.28** | 130.79, 118.95, 145.51 / **130.79** | 145.42 | 0/3 |
| 4.14 MB | 408.15, 426.83, 558.98 / **426.83** | 159.97, 195.41, 180.16 / **180.16** | 220.48 | 0/3 |
| Rich catalogue | 70.12, 79.07, 78.00 / **78.00** | 128.23, 182.02, 152.97 / **152.97** | 209.73 | 0/3 |
| Oversized paragraph | 253.58, 268.74, 265.19 / **265.19** | 113.98, 160.30, 131.59 / **131.59** | 160.34 | 0/3 |

### Warm samples within each process

| Fixture | Reference samples / median | Prototype samples / median | Prototype worst serviced gap | Prototype failures |
| --- | --- | --- | ---: | ---: |
| List | 723.68, 737.25, 805.11 / **737.25** | 37.85, 40.81, 62.40 / **40.81** | 62.39 | 0/3 |
| Table | 223.31, 245.71, 274.99 / **245.71** | 79.91, 38.29, 48.21 / **48.21** | 119.13 | 0/3 |
| Paragraphs | 191.92, 232.57, 241.46 / **232.57** | 58.68, 58.52, 86.87 / **58.68** | 89.85 | 0/3 |
| Quote | 146.95, 81.03, 96.09 / **96.09** | 28.43, 29.28, 38.92 / **29.28** | 68.75 | 0/3 |
| Details | 77.11, 75.74, 87.30 / **77.11** | 28.27, 29.58, 38.61 / **29.58** | 93.55 | 0/3 |
| Mixed | 248.11, 265.42, 409.17 / **265.42** | 51.46, 48.30, 68.92 / **51.46** | 129.78 | 0/3 |
| 4.14 MB | Unmeasured: all cold reference runs time out at idle | 60.07, 80.54, 97.64 / **80.54** | **341.89** | **3/3** |
| Rich catalogue | 9.16, 10.49, 16.34 / **10.49** | 74.68, 76.24, 81.53 / **76.24** | **253.02** | **1/3** |
| Oversized paragraph | 203.66, 204.26, 204.51 / **204.26** | 9.40, 13.09, 13.35 / **13.09** | 71.85 | 0/3 |

Cold list median improves about **78.84%** locally. The small rich catalogue is slower and more resource-intensive than the reference. Quote/details cold differences are modest. All **27 cold prototype samples** pass their observed budget, but **four of 27 warm samples fail**; overall acceptance remains incomplete.

Large warm gaps: **274.05/341.89/316.07 ms**; navigation **250.46/309.64/281.69 ms**; worst scroll input **158.12/203.11/269.82 ms**. Rich warm sample 2: **253.02 ms** gap and **252.88 ms** scroll input. Earlier `comparison-1` large warm **442.12/315.88 ms** gaps retain their original binary/source identity. A later pass does not prove correction.

Every completion/timeout, source hash, actual layout/materialisation/container/visual measurement, allocation and memory observation is retained in JSON/CSV and CI artefacts. No average replaces failures.

## Large document, layout units and memory

Exact large source retains **20,001 logical pieces**. Cold samples demonstrate bounded initial/distant work and final-paragraph selection. Median logical readiness **955.30 ms**, useful viewport after attachment **180.81 ms**, end-to-end **1,513.65 ms**. Cold navigation median/worst **177.46/184.85 ms**; peak realised pieces **seven**. Deferred pieces are not all physically laid out. A visible viewport is not full visual completion.

All reference large runs retain first-layout evidence then exceed **60-second** idle deadlines. Warm/full-navigation results are unmeasured. Prototype warm navigation stays reachable but its observed gaps fail.

| Cold fixture | Reference / prototype first-viewport allocation (MiB, median) | Prototype total allocations (MiB, median) | Prototype peak working set (MiB, worst) | Peak realised pieces | Maximum actual piece layout (ms, worst) |
| --- | ---: | ---: | ---: | ---: | ---: |
| List | 109.14 / 19.62 | 33.12 | 258.97 | 13 | 65.60 |
| Table | 47.52 / 11.07 | 26.43 | 256.98 | 10 | 71.00 |
| Paragraphs | 48.16 / 14.60 | 31.93 | 263.77 | 13 | 79.58 |
| Quote | 21.26 / 8.81 | 20.57 | 250.38 | 13 | 77.82 |
| Details | 23.14 / 8.65 | 20.04 | 249.41 | 12 | 75.32 |
| Mixed | 57.30 / 15.78 | 30.19 | 270.76 | 13 | 36.33 |
| 4.14 MB | 525.15 / 235.39 | 245.62 | 343.51 | 7 | 78.20 |
| Rich catalogue | 8.85 / 11.32 | 34.21 | 286.17 | 13 | 44.88 |
| Oversized paragraph | 17.60 / 15.42 | 21.27 | 230.73 | 5 | 78.57 |

Construction/materialisation and actual descendant measure are separate; construction is not layout acceptance. Actual native container/visual counts corroborate recycling. Allocations include complete parsing/media and the observation process. Working sets include CLR/WPF/fonts/test dependencies; warm peaks are cumulative. These are not installed-app budgets. Reference large worst working set is **628.47 MiB after timeout**; completion scopes differ materially.

Disposal clears source/selection/media/controls; WeakReference verifies collection after explicit disposal/idle/GC. One later process reports **27,341,176 managed bytes** after collection. Forced collection occurs only in this test. Logical data is O(document size), visuals bounded in observed cases. Arbitrary images/long sessions/installed memory are uncertified.

Rapid replacement: new readiness **8.715 ms**, obsolete cancellation completion **468.494 ms**. Active Markdig parse is not interruptible but remains on its worker and cannot commit obsolete output. Navigation checks cancellation at deferred dispatch. All future expansion/media/resize sequences are not universally certified.

Excluded `trace-1` samples include native shaping, `VirtualizingStackPanel.GetContainerSizeForItem` and item/ICU hashing. They support investigating nonvisual extent/index work, not full attribution or a proven correction.

## Critical failed bound: indivisible Unicode

Valid UTF-16 fixture: **`a` followed by 65,536 combining acute marks**, then a heading/paragraph. **65,586 source characters**, not malformed surrogates. Grapheme-preserving subdivision creates a **65,537-character** piece rather than the nominal 512. Source/logical text are retained before native layout.

The local diagnostic wrote only pre-layout evidence and terminated; its original exit was not captured and is not invented. Two independent disposable Windows runs capture **`-2147483645` / `0x80000003`**, `completed: false`, `stage: BEFORE native layout`. No symbolicated dump attributes it to one WPF/DirectWrite method.

A whole grapheme in one RichTextBox abandons the unit bound. Dropping marks, changing source or excluding input weakens semantics. Worker-owned WPF documents cannot simply move to the UI; yields cannot make this call safe. **Outer virtualisation is insufficient when a piece can terminate the viewer or remain unbounded.** This fundamental premise failure prevents CONDITIONAL GO.

## Complex content and complete exports

Normal suites exercise 4,000 list items, nested/numbered/task lists, formatting, 1,000 table rows, columns/alignment/breaks, nested details, headings, quotes, code, images, native Mermaid, frozen display-maths drawings and caption/badge text. Six 352 Ki-character paragraph/cell/quote/list/code/details variants retain source/text and reach the final target with bounded pieces/native geometry. Their pass does not override indivisible Unicode failure.

`--exports` verifies nine **complete-source DOCX/PDF/HTML outputs** for list/table/mixed while only a viewport is realised. DOCX text comes from actual package bodies, PDF from extracted glyph words, HTML from the established full-source pipeline. Final markers/source preservation pass. Earlier raw PDF concatenated text omitted layout spaces; that harness failure remains in `exports-2`. Corrected word extraction passes in `exports-3`, without changing production export code or original bytes.

Media dimensions freeze before commit; late lazy dimension arrival is not simulated. Multiple inline images/formulae, all passive blocks, full syntax colouring/diagram actions, all contrast, every table continuation/overflow and universal destination fidelity remain outside demonstrated scope. Complete source availability is not full visual/clipboard parity.

## Validation, environments and retained failures

Final local results: **zero build warnings/errors**, **11 model tests**, **158 hidden-WPF checks**, normal oversized/scroll/source probes, nine export checks, unchanged **1,057 Core tests**, reference native **40 diagram cases/export checks with zero failures**. Static integrity passes. Initial reference invocation through `dotnet <dll>` correctly failed two apphost/diagnostic guards. The supported validation `.exe` passes; both logs remain, with no guard bypass.

At `b00cf05`, both native runs pass 24 actual interaction checks, model/hidden/stress/export steps and comparative observations, then remain **FAILED because of Unicode native termination**. [Push 38458654638](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38458654638) is branch source; [PR 38458661773](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38458661773) is merge `280c3a4ce5aa6f85e02445c79ef37c28990a80f6`. Actual exit evidence is independently downloaded. Branch web [38458654719](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38458654719) verifies **268 browser cases**, Pages deployment skipped. Final code is reconciled below.

Hosted data is separate: Windows 10.0.26100/.NET 10.0.12, AMD EPYC 9V45, about 16 GiB/four logical processors. b00cf05 push cold list reference/prototype medians **1,315.71/199.85 ms**, mixed **636.89/148.23 ms**, large **539.29/196.31 ms**. Two large warm failures remain. These are not pooled with Intel or PR-merge results.

Evidence, relative to the spike worktree:

- `acceptance/continuous/comparison-{1,2}`: local JSON/CSV/PNGs, hashes/environments, partial reference timeouts and report calculations.
- `acceptance/continuous/validation-*`, `model/`, `stress-*`, `exports-*`, `cluster-1`: original/corrected source/selection/transfer/resource/failure probes.
- `acceptance/continuous/trace-1`: excluded native trace and top-function report.
- `acceptance/continuous/reference-{core,native,native-apphost}` and `acceptance-reference-*.log`: original reference checks/invocation errors.
- `acceptance/continuous/ci-{40fc578,f974650,c4873fa,b00cf05}-*`: hosted failures/downloads; cancelled revisions keep partial identity, not completed-pass labels.
- Actions `native-continuous-feasibility` artefacts: synthetic screenshots, selection/clipboard records, exports, actual failed process exits/partial JSON and measurements.

Early zero-height presenters, idle-starving pump, narrow columns, Run-splitting pointers and disposed-token race were corrected. An observer initially accepted old whole selection because it contained the target; exact selection/bounds now establishes completed navigation. Keyboard observation waits for exact cleared/extended states rather than any earlier nonempty selection. Original failures are preserved.

## Engineering impact and final decision

Meaning, selection and TextPattern can be separated from realised controls. This is demonstrated. The current hybrid does **not** establish a fully equivalent scalable Continuous renderer.

Failed assumptions: grapheme-safe character caps do not bound every native call; estimated extents do not reliably preserve history/anchors; warm navigation/scroll sometimes exceeds 250 ms. Full rich clipboard and assistive semantics need substantial work. These are correctness/boundedness obligations, not minor polish.

**NO-GO:** retain FlowDocument. Do not integrate/enable this host as replacement. A credible future direction is a width-aware native line/glyph surface with explicit height indexing, controlled shaping batches and the proven logical selection/source/TextPattern model. Unicode-safe fragmentation and faithful hit/selection/transfer geometry must be demonstrated first. TextFormatter shares shaping dependencies and cannot be assumed immune. No complete second speculative renderer is launched here.

Integration cost is high: complete projection/format parity, precise selection/source semantics, line/bidi geometry, structural clipboard serialisation, height reconciliation, media cancellation/contrast and accessible patterns require coordinated regressions. Failure evidence justifies stopping before replacement. [ADR](adr-continuous-viewer-spike.md) preserves the decision and useful model/prototype.

## Final-code CI reconciliation

Code **`82240572d056bb5f9bacf6f5b4df7e25d7e16004`** includes native maths, item highlights and distant-heading/history probes used by local comparison-2. Both final native pipelines are **FAILED**, exactly as the NO-GO requires: [push 38462199340](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38462199340), [PR 38462205537](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38462205537). Their model/hidden-WPF/24 desktop/export/measurement steps complete; the indivisible Unicode step records **`-2147483645`** in each. Source/text are retained in the partial pre-layout JSON. Native failure was not masked, retried unchanged until green, or converted into performance acceptance.

Both final web pipelines pass **268 Chromium/Edge cases**: [push 38462199162](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38462199162), [PR 38462205441](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38462205441). Pages deployment is skipped. Original PR 21 remains draft/open at 9c7454e.

Downloaded `ci-8224057-{push,pr}/evidence` verifies real distant screenshots, complete transfers/exports, source hashes, exits and separately scoped measurements. Branch-source hosted data records **54/54 passing representative observations** on AMD EPYC 9V45. The PR merge **`94e33f8b44af90fbc9c03527d6abffbc1849dac8`** runs on AMD EPYC 7763 and records **five failures among 54 prototype observations**, all large-document gaps: cold **258.63/348.62 ms**, warm **337.97/309.99/403.38 ms**. Its corresponding first-layout inputs remain below 250 ms; passing first layout cannot erase those later failures. These different environments are not pooled with each other or the four local warm failures.

Hosted stress reproduces **restored piece 12 versus expected 10**, **67.99 px** correction and real derived distant-heading visibility with eight containers. Both final Unicode process exits remain failed. Individual observations and checkout identities stay in their own manifests. Documentation-only reconciliation changes no measured runtime, default viewer or failed gate.
