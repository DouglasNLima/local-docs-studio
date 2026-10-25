# Native diagram compatibility

Current integrated status (24 October 2026): [Current Windows readiness](current-windows-readiness.md).
There are 24 bounded native editor families. Dated expansion milestones below
retain their original sources and counts; the [Goal VII audit](goal-vii-ux-audit.md)
separates implementation, actual Windows acceptance and manual prerequisites.


The [Class visual editor report](mermaid-class-visual-editor.md) tracks the fifth
native family: dedicated source occurrences, bounded class/member authoring and
endpoint-owned relationship markers/cardinalities. Method classifiers, unknown
source and ambiguous namespace/note operations remain protected. Hosted Class
certification is identified by the report and exact-revision PR delivery receipt;
implementation alone does not imply readiness.

| Native authoring family | Source model and authoring boundary | Certification record |
|---|---|---|
| Flowchart Advanced | Nodes, connections, supported chains, local styles and subgraphs; dependent unknown grammar protected | [Flowchart report](mermaid-advanced-flowchart-partial-editing.md) |
| Sequence | Participants and supported ordered interactions; exact occurrence patches | [Sequence report](mermaid-sequence-visual-editor.md) |
| State | States, transitions and supported scoped markers; exact source patches | [State report](mermaid-state-visual-editor.md) |
| Entity relationship | Entities, ordered attributes, keys and endpoint cardinalities | [ER report](mermaid-er-visual-editor.md) |
| Class | Classes, distinct attribute/method occurrences, six relationship types and endpoint-owned markers/cardinalities; classifiers and ambiguous syntax protected | [Class report](mermaid-class-visual-editor.md) |
| Gantt | Title, sections, labels, explicit ISO starts and whole-day durations; advanced scheduling protected | [Expansion report](gantt-mindmap-pie-editors.md) |
| Mind map | One root, labels, complete subtree operations, recognised shape/ID preservation | [Expansion report](gantt-mindmap-pie-editors.md) |
| Pie | Title, separate category occurrences, invariant decimal values and canonical wedge selection | [Expansion report](gantt-mindmap-pie-editors.md) |
| Timeline | Effective title, sections, periods and individual event occurrences | [Expansion II](timeline-journey-quadrant-editors.md) |
| User Journey | Effective title, sections, tasks, scores and ordered actor occurrences | [Expansion II](timeline-journey-quadrant-editors.md) |
| Quadrant | Title, axis/quadrant labels and distinct point occurrences | [Expansion II](timeline-journey-quadrant-editors.md) |
| XY Chart | Category-based bar/line series, values, titles, presentation, explicit/automatic Y range and atomic aligned categories | [Expansion III grammar and protections](xy-radar-sankey-editors.md); source-specific certification in the Expansion III receipt; current reruns in the Expansion IV receipt |
| Radar | Positional axes/curves, finite values, supported range directives and atomic aligned axis changes | [Expansion III grammar and protections](xy-radar-sankey-editors.md); source-specific certification in the Expansion III receipt; current reruns in the Expansion IV receipt |
| Sankey | Separate positive weighted link occurrences, local endpoints and explicit collision-protected node rename | [Expansion III grammar and protections](xy-radar-sankey-editors.md); source-specific certification in the Expansion III receipt; current reruns in the Expansion IV receipt |
| Kanban | Ordered columns/cards, effective title, coupled/bare and explicit IDs, simple metadata and proved membership moves | [Expansion IV grammar/protection guide](kanban-requirement-gitgraph-editors.md); final-source certification in the Expansion IV PR delivery receipt |
| Requirement | Six kinds, elements, property tokens, seven relation types, four directions and atomic symbolic rename | [Expansion IV grammar/protection guide](kanban-requirement-gitgraph-editors.md); hidden/malformed/unproved topology protected |
| GitGraph | Basic ordered command history, commit IDs/tags/types, branch/checkout/merge and independent tail edits | [Expansion IV grammar/protection guide](kanban-requirement-gitgraph-editors.md); stateful references proved against native links |
| Architecture | `architecture-beta`: simple service/group labels and IDs, bundled pictograms, explicit membership, junctions and actual native ports/arrowheads | [Expansion V grammar/protections](architecture-c4-block-editors.md); provider artwork and group endpoints remain protected; source-labelled gates in the [engineering receipt](expansion-v-engineering-receipt.md) |
| C4 | One family with separately tested C4Context, C4Container and C4Component; exact positional fields, complete simple boundaries and Rel/BiRel/Rel_Back | [Expansion V grammar/protections](architecture-c4-block-editors.md); Dynamic/Deployment and unproved arguments/topology remain source-only |
| Block | Flat `block`/`block-beta`: exact ordered token occurrences, coupled/bare and rectangle/rounded blocks, spacers, spans and directed links | [Expansion V grammar/protections](architecture-c4-block-editors.md); first-wins and partial-line protection; canonical grid geometry |
| Packet | `packet`/`packet-beta`: effective title, literal labels, explicit inclusive ranges, one-bit and relative-count forms; complete independent field groups | [Expansion VI grammar/protections](packet-treeview-treemap-venn-editors.md); changed downstream cursor interpretations, skipped forms and ambiguous overlaps protected; actual multirow native segments |
| TreeView | `treeView`/`treeView-beta`: mode-preserving label/directory/annotation fields and proved complete indentation subtrees | [Expansion VI grammar/protections](packet-treeview-treemap-venn-editors.md); box structural re-encoding and custom artwork source-only; default/hidden icons and native glyph/highlight targets |
| Treemap | `treemap-beta`: quoted section/leaf occurrences, explicit finite decimal tokens, derived totals and complete proved simple subtrees | [Expansion VI grammar/protections](packet-treeview-treemap-venn-editors.md); one-character tabs, hidden zero values, protected fallback/ambiguous indentation; native squarification |
| Venn | `venn-beta`: unique safe IDs, separate labels, exact union references, atomic rename/removal and stored size metadata | [Expansion VI grammar/protections](packet-treeview-treemap-venn-editors.md); fixed native circles, protected ambiguous references, keyboard-only unions and overlap ambiguity |

The matrix lists twenty-four bounded implementations. Expansion VI final-source hosted appearance, installed lifecycle and package acceptance are tracked separately in its [source-labelled receipt](expansion-vi-engineering-receipt.md); local implementation/preview proof does not turn pending gates into passes or certify every grammar form.



The [ER visual editor report](mermaid-er-visual-editor.md) records the fourth native
authoring family: exact source entities, ordered attribute occurrences, PK/FK/UK,
comments, endpoint cardinalities and identifying/non-identifying relationships.
The pinned parser's ignored suffixes remain protected. Self-loop layout reordering
is reconciled against actual per-render semantics and metadata. Implementation and
hosted certification are reported separately; rendering success never grants editing
permission or universal ER grammar support.

The [State visual editor report](mermaid-state-visual-editor.md) records the pinned
0.15.1 syntax matrix, exact source identity/patch architecture, scoped markers,
safe operations and protected constructs. State is the third dedicated native
visual authoring family. Its canonical geometry uses the pinned renderer's graph
pipeline, with State-specific semantic verification before shared native hit
geometry. Current certification status is reported separately from implementation.

The [native Sequence visual editor report](mermaid-sequence-visual-editor.md) records
the exact Mermaider 0.15.1 grammar and bounded source-preserving authoring subset.
Sequence and Flowchart preview actions route to separate native semantic editors.
Sequence layout and rendering remain authoritative in Mermaider; its verified SVG
metadata supplies interaction geometry only after correspondence with source tokens.
The existing standalone activation adapter continues to affect rendering only.

Sequence certification exposed a native interpolation failure for functional RGB
colours in protected participant boxes. The adapter now resolves bounded numeric,
percentage and alpha RGB/RGBA channels before native conversion. It retains the
original Mermaid and existing SVG sanitisation; this does not certify every CSS
colour space accepted lexically by the pinned renderer. Conversion failures retain
source and a visible diagnostic.

The advanced native Flowchart editing continuation is tracked in
[Draft PR 31](https://github.com/DouglasNLima/local-docs-studio/pull/31), stacked on
the exact unmerged PR #30 implementation. Its [partial-editing report](mermaid-advanced-flowchart-partial-editing.md)
owns current source-transformation evidence; the rendering reports below retain
their original revision scope. Full native regression, actual High Contrast,
source-preserving Apply and independently verified unsigned preview `0.1.1-preview.599`
pass within the report's explicit compatibility and certification limits.

| Flowchart source | Transformation behaviour |
|---|---|
| Fully supported separate statements | Existing operations, complete semantic comparison and rendering |
| Known chains | Partial; independent edits and exact chain property spans; no automatic splitting |
| Repeated declarations | Partial; first occurrence remains authoritative; repeated declaration/identity edits protected |
| Pipe, inline text and quoted connection labels | Exact label spans; type changes use a bounded arrow replacement when necessary |
| `classDef`, multi-target `class`, local `style` | Preserve shared definitions/custom properties; targeted local overrides |
| Numeric/default `linkStyle` | Safe properties; connection insertion, deletion and reconnection protected |
| Nested named subgraphs | Preserve hierarchy, direction and membership; safe labels/properties |
| Unknown directives, interactions, edge IDs or configuration | Protected inspection; unknown dependencies fail closed |

Rendering support does not itself grant editing permission. No universal Mermaid
compatibility or production certification is claimed by these boundaries.

Hyperlinked images now retain both their supported external destination and original image bytes through Word import. The focused core test uses an actual embedded PNG package; the full local native corpus still passes in `artifacts/native/word-links-final-validation`. This does not claim that Word images recover their original Mermaid/LaTeX source.

The Word reimport probe now confirms exact native Mermaid/formula image bytes after DOCX export and import, retaining two list items and their continuations. The full local harness passes in `artifacts/native/word-import-heading-validation`, alongside 868 core tests. This does not recover Mermaid/LaTeX source from the rendered images or certify complete Word formatting.

Invalid, oversized or unsupported diagrams, including native SVG conversion failures, retain a readable explanation with Copy source and Edit source. Editing reveals the original fence (or offset zero for standalone Mermaid); a changed document invalidates the old preview's navigation action. Fourteen local and CI native recovery cases pass across both themes, including DevOps containers and successful-render controls. Actual clipboard, editing, selection collapse and Undo pass in all four desktop contexts of both df281c0 native runs.

Diagram themes now have native Auto, Lens, Default, Neutral, Forest and Dark choices. Auto preserves the existing light/dark Lens appearance; fixed choices ignore application theme changes. Default uses a lavender palette, Neutral greys and Forest greens. The same resolved palette is carried into preview, maps, SVG/PNG and document/clipboard/site images. Twelve light/dark application combinations retain exact exported image pixels across HTML, DOCX, PDF, clipboard representations and Docs Sites; source stays unchanged. These are native palette equivalents, not a claim of browser Mermaid pixel identity. Actual desktop/menu checks pass in all four contexts of the 56b5161 native CI runs.

The desktop uses Mermaider 0.15.1 and SharpVectors through the native SVG adapter. It has no browser rendering fallback. The [fixture corpus](../../tests/fixtures/native-mermaid/compatibility.md) is checked in light and dark themes; the complete repository harness currently executes 40 diagram cases and retains SVG/PNG output under `artifacts/native/validation`. The catalogue contributes another 56 diagram/theme cases. Passing counts establish this corpus, not all Mermaid syntax.

| Family | Inspected fixture outcome |
|---|---|
| Flowchart | Nested Workspace/Authoring subgraphs, multiline labels, decision shape, labelled edge, class colour and explicit node fill retain visible geometry and text |
| Sequence | Participants, request/response arrows, note and activation bar retain their content; standalone activation adaptation is described below |
| Class | Inheritance, property type/name and method labels are present |
| Entity relationship | Workspace/document/asset labels, relationship labels and cardinality markers are present |
| State | Initial state, forward transitions, revise loop and approval label are present |
| Gantt | Draft/review bars, title, section and October date axis are present |
| Pie | Verified/pending labels, 70/30 proportions and legend are present |
| Mind map | Root, author/export branches and four leaf labels are present |

Visual inspection exposed three defects beyond the earlier valid-SVG checks. Dark inherited text could disappear on an authored pale node fill; the adapter now selects contrasting neutral text only when the text colour is inherited and the node has one opaque solid shape. Explicit text colours, gradients and translucent shapes retain their authored/rendered colours. The target is 4.5:1 for this narrow inherited-text case, using the [W3C contrast calculation](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html); this is not a whole-application accessibility certification.

SVG theme variables did not paint a background in WPF. The adapter now materialises an opaque declared background across the SVG viewBox, retaining margins and readable exported labels when pasted onto a different surface. SVGs without an opaque declared background retain transparency. Native checks inspect exact background pixels and verify that repeated preparation does not duplicate the background. Corrected nested-flowchart, sequence, class, entity, state and pie output was inspected, alongside the focused light/dark/mid-grey label PNGs.

The pinned [sequence parser](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Parsing/SequenceParser.cs) recognises `+`/`-` message markers but ignores standalone `activate`/`deactivate` commands. The native adapter translates a standalone command immediately after a matching message into the supported marker, without changing saved Markdown or the displayed source. Blank lines/comments may intervene. Activation must match the message recipient; deactivation must match its sender. Existing shorthand remains supported. Seventeen core cases check geometry, labels, original CRLF source, six arrow forms and visible rejection of commands that cannot be represented.

Separate activation commands before any matching message, after notes/blocks, with conflicting existing markers, or stacked on the same message cannot be represented by this adapter. They produce an explicit diagram error and preserve source. One command/actor per line is required. These are compatibility limits, not successful partial rendering. Broader sequence lifecycles, additional diagram families, dense layouts and destination-specific zoom/clipboard behaviour still require further acceptance work.

Six focused native cases cover inherited/explicit text colours, class fills, mid-grey fallback and declared-background pixels. Evidence is `native-diagram-contrast.json` and `native-contrast-*.png` under `artifacts/native/diagram-contrast` or the complete validation directory. The complete local suite passes 766 core tests and 40 diagram/export cases after these corrections. Both native CI runs at a827668 pass 766 core tests and all 55 desktop/installed/reinstalled workflows; both browser runs pass all 262 cases. The preceding allocation correction at 54b1392 also passes both native and both browser CI runs.


## Mermaid creation hub

The native creation catalogue routes exactly 24 existing bounded editor families;
it adds no Mermaid grammar or renderer support. Its selected samples pass the same
family draft validators, canonical Mermaider renderer and sanitised native SVG
pipeline. Flowchart retains its legacy empty starter and offers the existing simple
process fixture. C4 offers Context, Container and Component as one family;
TreeView offers indentation and box drawing with existing structural protection.
Rendering arbitrary syntax does not grant visual editing permission. See the
[creation hub evidence and limits](mermaid-creation-hub.md).
