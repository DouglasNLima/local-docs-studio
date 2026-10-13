# Native Continuous viewer feasibility

This is an isolated engineering spike based on verified native head `9c7454eeb892614af93881b762a1b6aecc3259d2`. The production renderer, preferences, packages and PR 21 remain the reference. The **250 ms dispatcher input objective is unchanged**. The architecture decision is pending comparative and hosted interaction evidence; a successful build is not a GO.

## Adopted evidence

Read [performance and responsiveness](performance-and-responsiveness.md), [engineering delivery](engineering-completion-report.md), [continuation](continuation-handoff.md), [parity](functional-parity-matrix.md), [experience improvements](native-experience-improvements.md) and [architecture decisions](architecture-decisions.md). Existing failures retain their original classifications and evidence directories.

The reference already parses/prepares off the UI thread and yields during native construction. FlowDocument bottomless formatting and list visual validation remain synchronous. Adding application yields around those calls does not bound the calls themselves. The original 128-item subdivision and attached incremental-construction experiments are not reinstated.

Historical comparable list/table/rich-paragraph median input is 727.88/227.74/354.44 ms; worst comparable list is 1,710.67 ms. Mixed 6,097.98 ms, large-document 815.91 ms, large background-layout timeout and unresolved 587.9933 ms details construction remain failures or unverified work, exactly as recorded. These historical numbers are not pooled with new measurements.

## Alternatives and selection

| Approach | Layout behaviour | Selection and accessibility cost | Maintenance and compatibility |
| --- | --- | --- | --- |
| A: recycling native block host | Actual item creation/measure is restricted to viewport/cache. Large structures must be flattened into addressable units; one giant child would recreate the defect. | Requires a document selection and logical TextPattern above independent native text controls. Native text shaping and hit testing can be reused. | Moderate/high; existing Markdown semantics/media/export services remain useful. |
| B: DrawingVisual/TextFormatter surface | Explicit line/page budgets, independent height index and visual retention can bound work more precisely. TextFormatter calls still need bounded input. | Must reconstruct character hits, bidi/glyph geometry, highlighting, caret/keyboard policy, rich transfer and complete TextPattern. Fast drawing alone proves none of these. | High; offers the most layout control but a much larger native interaction engine to maintain. |
| C: hybrid A with bounded native text and existing media services | Recycling host plus bounded per-piece native FlowDocuments, native row cell grids and frozen raster/vector media. No global FlowDocument. | Shared logical selection and UIA ranges provide document order independently of recycled controls. Individual controls are presentation objects. | Selected bounded prototype. Native WPF text and existing image/Mermaid/maths services are reused. |

[WPF control performance guidance](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls) distinguishes presentation virtualisation from data virtualisation and documents ways to disable recycling accidentally. Actual container count and stale associations are therefore measured. [Native advanced text formatting](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/advanced-text-formatting) supplies Candidate B's credible text engine, rather than a browser or JavaScript runtime. These API capabilities are architectural evidence, not measured performance claims for an unbuilt complete alternative.

## Document and presentation boundaries

`src/native/spikes/ContinuousViewer.Model` uses the established Markdig service and typed passive semantic nodes. It retains authoritative original source, full syntax/headings/targets, complete logical text, styled runs, source spans, nested details ancestry and stable piece IDs. Presentation pieces are at most 512 UTF-16 characters except an indivisible pathological combining sequence, which remains an explicit stress risk. Grapheme boundaries govern logical selection. Source spans for decoded/generated text identify the original semantic span instead of inventing character precision.

`src/native/spikes/ContinuousViewer` is a separate executable, outside the production solution and package publish inputs. A pixel-scrolling recycling ListBox hosts bounded native text controls, table cell grids and prepared native media. The entire logical document remains available to navigation, copy and export when controls are absent. Selection lives in offsets; its visual runs are derived afresh, avoiding stale TextPointers when native formatting splits a Run. Expansion belongs to logical groups. Request revisions/cancellation prevent superseded loads or navigation from committing.

## Executable prototype and evidence

From the spike worktree:

```powershell
dotnet build src/native/spikes/ContinuousViewer -c Release
dotnet test src/native/spikes/ContinuousViewer.Tests -c Release
dotnet src/native/spikes/ContinuousViewer/bin/Release/net10.0-windows/ContinuousViewer.dll --validate acceptance/continuous/validation-new
pwsh -NoProfile -File scripts/native/Measure-ContinuousViewer.ps1 -OutputDirectory acceptance/continuous/comparison-new
```

Output directories must be fresh. The measurement script rejects overwriting original observations, runs sequential independent processes for both engines, and separates cold/warm samples. It uses the **unchanged compiled production renderer from this checkout** for its baseline, identical source hashes and 640 × 300 hidden native HWNDs. Every sample records actual native/prototype assembly hashes, runtime, OS, input budget and failures. A completed evidence-recording command does not imply timing acceptance.

Interactive `--interactive <fixture> <synthetic-root>` and `--desktop <output>` are guarded to genuine disposable GitHub-hosted Windows, following the existing boundary. No newly built visible app is executed on the user's normal desktop. The dedicated `continuous-viewer.yml` workflow never installs a product or produces production preview packages.

## Current validation and known limits

Model tests validate LF/CRLF source preservation, Unicode/combining/surrogate selection, decoded styled text, CF_HTML, RTF encoding, nested details, table contents, complete 4,140,025-byte source and oversized paragraph subdivision. Hidden-WPF validation exercises real initial/distant controls, native RTF loading, selection spanning previously virtualised blocks, offscreen logical TextPattern, replacement/cancellation, expansion, theme/font changes, disposal and complete established HTML exports. The latest evidence directory and hosted results will be recorded when final validation completes.

Original exploration logs are retained under ignored `acceptance/` and root `acceptance-*.log`. Early pump starvation was a harness scheduling error, not a renderer result. A narrow-column screenshot led to an explicit viewport-width regression. A rapid-switch disposed-token failure led to captured cancellation tokens. These failed revisions are excluded from the final performance comparison and remain inspectable.

The prototype does not certify physical screen-reader behaviour, actual DPI/multi-monitor changes or a physical first frame. Full formatting/semantic parity, visual line geometry in accessibility, long-table continuation, scroll-estimate reconciliation, selection gesture details, media error presentation and pathological oversized graphemes require explicit evidence or declared follow-up gates. These cannot be silently labelled production-complete.

## Decision and follow-up

**PENDING evidence.** No integration is authorised by this spike. The final report will make a GO, CONDITIONAL GO or NO-GO decision with individual samples, medians/worst results, memory, actual distant navigation, hosted interaction scope, known defects and a concrete implementation plan only if justified.
