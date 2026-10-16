# Mermaid preview fidelity and visual editing access

Implementation and certification work for draft [PR 28](https://github.com/DouglasNLima/local-docs-studio/pull/28),
branch `feature/native-mermaid-preview-fidelity`, based on
`47e78d6bff490a9b0c0b5cb2022dab262fe14e5e`. PR 27 and its ancestors remain unchanged.
This report is provisional until the final Windows evidence and package identities below are populated.

## Causes and entry point

The document renderer required the lexical adapter's `CanEdit` before creating its
visual authoring button. A valid chained Flowchart could render successfully while
losing that button. Recognition now uses the diagram family independently of the
transformation classification. **Visual Edit** is the first authoring action;
**Edit source** remains separate. Existing `DiagramEditVisually{sourceOffset}` IDs
are retained. Document size and source-container mapping checks remain unchanged.

Opening still checks document identity, revision, lifetime, save state, format and
source span. Apply still passes through `MermaidDocumentEditSession`; rendering
metadata never supplies editing offsets. Identical fences are distinguished by
their original container and AvalonEdit version history.

## Shared presentation decision

The former canvas called `MermaidGraphLayout.Arrange` and drew its own shapes,
straight connections and guessed label positions. It no longer calls that layout.
The old utility remains solely for the established Core regression assertions.

The chosen presentation is native SVG plus WPF interaction overlays. Mermaider
0.15.1, package source commit `71b64845570d633a94d29e85579c0a02786fece8`, exposes
`RenderOptions.LayoutProvider` and `IGraphLayoutProvider`. A provider created for
each render delegates to the existing provider, preserves numeric-escape decoding
and captures the positioned graph returned to that very rendering call. No global
provider is replaced and no dependency version or library binary is modified.

`DiagramResult` carries that snapshot beside its SVG. Preview/export continue
using the established SVG sanitisation and SharpVectors conversion. The canvas
uses the same sanitised SVG, font settings and native converter, with inert
temporary IDs for native drawing groups. Text remains converted to geometry.
No browser, JavaScript editor, web server or SVG link activation is introduced.

Repainting independently from positioned nodes was rejected: it would reproduce
positions but still duplicate Mermaid's shape, rounded-path, arrowhead, label and
style rendering. Parsing arbitrary SVG as an authoring model was also rejected.
Only the output of this Mermaid render and its associated positioned graph enter
the presentation adapter. Original Mermaid and verified lexical spans remain the
authority for transformations.

## Geometry and source identity

Node and nested-group IDs and labels must agree with generated metadata. Their
bounding rectangles come from the captured positioned graph. Shape hit geometry
comes from the actual generated SVG. Drawing bounds supply a single translation
for the image, paths, ports and outlines. XML processing prohibits DTDs and external
entities, limits document size and element count, and rejects unsupported
transforms, non-finite coordinates and conflicting identities. Existing active
content and external-reference rejection remains ahead of conversion.

Connections use the exact rounded SVG `d` path and generated label rectangle.
Metadata order is checked against the positioned edges, then against the source
adapter's ordered edges. Stable private draft IDs distinguish parallel connections,
including equal endpoints and labels; those IDs are never saved in Mermaid.
At physically overlapping broad path targets, pointer selection asks users to
choose the intended connection in the element list. It never guesses an edge from
overlapping bounding rectangles. Unmapped elements cannot enable transformations.

Selection, multi-selection, keyboard list navigation, properties, ports, inline
labels, node identity/shape, colours/classes, endpoints, reversal, group membership,
deletion, duplication and private history retain the existing source-patching flow.
Pending inspector values continue blocking selection changes, source validation
and Apply until explicitly updated or reset.

## Protected inspection and editing refresh

A renderable protected Flowchart displays its full original drawing. Partial lexical
information may supply safe inspection targets, but never a reconstructed substitute
for the complete diagram. Apply, palette operations and transformations remain
disabled. Zoom, PAN, source editing and Rendered preview remain available. The
compact status explains the protection; diagnostic details are available by tooltip.
Actual rendering errors display diagnostics and retain source. If only geometry
mapping fails, the original sanitised drawing remains available for inspection.

Every accepted semantic edit still validates portable Mermaid, commits only the
private draft, rerenders through the shared pipeline and restores logical selection.
The editor keeps the user's zoom and scroll position; only the initial view fits
automatically; very wide diagrams open at 100% around the first safely mapped node
for readable labels, with Fit available for an overview. Auto layout restores the canonical drawing without another parser
or layout engine. Cancellation reaches Mermaider's supported asynchronous render
pipeline; generation, source and theme checks reject obsolete results. Frozen native
geometry crosses worker boundaries; UI mutations belong to the WPF dispatcher.

Session dragging translates the actual native node drawing groups. Affected
connections become explicitly dashed temporary guides. This deliberate editing
affordance is not claimed as canonical routing. Source edits and Auto layout restore
the exact original rendering, and no proprietary coordinates are saved. Hover and
pointer motion do not parse SVG or invoke Mermaider. Long-lived render and drawing
caches retain their existing bounds, and closing cancels pending work.

The adapter's editing grammar is deliberately unchanged. Chained statements and
other unproven transformations stay protected. Dotted paths, multiline labels,
implicit nodes and repeated references are displayed by Mermaider whether or not
their lexical representation can be transformed safely.

## Viewport

Canvas extent follows actual drawing bounds rather than a 600 by 400 minimum with
extra margins. Smaller content is centred. Initial fit avoids enlarging a small
diagram beyond 100%; explicit Fit and Actual Size remain available. The standalone
Rendered preview adds Fit width and centres smaller drawings. When a whole-diagram
fit would be below 60%, both surfaces initially use readable Actual Size; explicit
Fit still reaches any required scale down to 1%, and PAN explores the wide drawing. Both surfaces use
WPF coordinate transforms to preserve the pointer's diagram anchor while zooming.
Space+drag, middle-button PAN and keyboard alternatives remain. Very wide diagrams
necessarily shrink when fitted into narrow viewports; Actual Size and PAN allow
inspection without changing aspect ratio.

## Certification and limitations

Synthetic fixtures under `tests/fixtures/native-mermaid-fidelity` cover a small
Flowchart, a representative flow error log lab, 20 styled nodes with nested groups
and parallel connections. No private documentation is included.

The repeatable `--mermaid-fidelity` harness compares the actual FlowDocument
diagram against the interaction drawing in Light and Dark, checks every canonical
node rectangle with a 0.001 native-unit tolerance, verifies group and edge identities,
and asserts normalised raster equality with zero pixel tolerance. UIA extends the
existing authoring suite to open protected diagrams through the actual document
preview button. The first complete comparison passed all eight fixture/theme pairs on hosted Windows,
including the protected lab. Captures have been inspected; final revision certification
is still pending.

Local verification so far: solution build with zero warnings; nine new Core cases;
full Core suite 1,229 passed, zero skipped. First hosted comparison caught a frozen
WPF path mutation before certification; its original log is retained and the path
is now cloned before transformation. No assertion was weakened.

The engineering checkpoint `03a0666` passes normal and actual Windows High Contrast
visual authoring on [run 38879424059](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38879424059).
Earlier failures are preserved: frozen path mutation, XML attribute line-break
normalisation and palette drops outside the small centred drawing. The baseline
comparison explicitly uses the old Insert entry point for protected diagrams;
requiring the new button from the unchanged baseline would hide the original defect.

Checkpoint performance observations (four-core hosted .NET 10.0.13) include about
245/151/605 ms for canonical presentation preparation at 20/100/300 nodes, and
20/14/46 ms for source edit plus render. These include cold/warm process effects.
The dense 190-edge fixture reaches the existing render deadline and remains rejected;
its original source stays exact. Long labels remain materially more expensive to
materialise through native text geometry. These are observations, not timing guarantees.

Pending: final native rendering, normal and actual High Contrast UIA, browser and
desktop regression results; screenshot inspection; measured canonical presentation
performance; complete installer lifecycle; independently verified unsigned package
version, source revision and SHA-256 hashes. Internal-preview readiness is not yet
established. This document must not be used as a completion certificate in this state.
