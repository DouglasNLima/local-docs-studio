# Mermaid creation hub â€” Goal IX

## Source audit and acceptance plan

Work starts from live `develop` commit `9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`, tree `1fb777317ad6e5c8531eebcb565fefae4227e354`, verified on 25 October 2026. The new `feature/native-mermaid-creation-hub` worktree starts clean at that commit. No source is imported from another feature branch.

Accepted-base push runs: [native 40108034236](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40108034236), 25 successful jobs and two intentional historical skips; [web 40108034406](https://github.com/DouglasNLima/local-docs-studio/actions/runs/40108034406), three successful jobs and Pages skipped. These establish the starting source only, not acceptance of this feature.

PR #38 remains open, Draft and paused at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. PR #48 remains open and Draft at `2d86fe3eb9ed95c832f7b3cbfba29005343bb7bd`; its failed High Contrast baseline and incomplete original f05 shell/stale-owner acceptance remain failed/blocked. Goal IX cannot certify either independently scoped outcome. Personal-host GUI tests are not planned; no existing native process, modal or private draft is touched. Hosted disposable Windows acceptance is mandatory.

| Boundary | Existing implementation | Goal IX approach and proof required |
| --- | --- | --- |
| Family selection | 24 menu handlers; boolean selector; existing starter constants | Typed 24-family catalogue, exact variant samples, independent draft acceptance and editor dispatch checks |
| Source ownership | `MermaidDocumentEditSession`, AvalonEdit versions, exact occurrence spans | Capture before toolbar focus; revalidate owner/version/identity; continue using the session's prepare/commit and parser post-check |
| Existing Markdown | `MermaidBlockSource.Find` over actual `MarkdownService` syntax | Unique body-only containment; protect fence boundaries, overlaps, ordinary code and unmappable containers |
| New Markdown | Existing insertion formatter replaces selection with a fixed fence | Preserve selection; insert after it; safe delimiter length; exact surrounding bytes; parser-verified container; one Undo/Redo |
| Standalone Mermaid | Raw source, nonempty source routes to existing editor | Preserve that behaviour; no family conversion or fences |
| Picker | Existing native catalogue, command search and DesktopStyles patterns | Search/categories, variants, keyboard/UIA, selected-only canonical preview, cancellation and bounded work |
| Editor dispatch | `OpenVisualDraft` creates all 24 existing windows | Reuse specialised windows and private history; no new engine, parser grammar or renderer changes |

| Validation group | Required coverage | Initial status |
| --- | --- | --- |
| Catalogue | 24 unique families; all offered variants; aliases/categories/filter/clear; actual editable starter and window type | NOT RUN |
| Context | Each family, duplicate bodies, protected/unsupported code, boundaries/overlaps, standalone, missing/stale/read-only/saving/closed/renamed owner | NOT RUN |
| Insertion | Beginning/middle/end; LF/CRLF/BOM/Unicode; adjacency; nested Markdown; selection; safe fences; exact outer source; cancel; one Undo/Redo; Save independent | NOT RUN |
| Native UX | 880Ã—600 and 1280Ã—800; light/dark/actual OS High Contrast; focus/keyboard/selection/overflow; screenshots reviewed | NOT RUN |
| Existing regressions | Core, 176 corpus/export baseline, 38 expanded appearances, five original specialist suites, workspace, stock Continuous correctness | NOT RUN |
| Packages | 24 families Ã— install/same-source synthetic upgrade/reinstall; package negatives; unsigned internal pair, full payload inventory/delta and runtime hashes | NOT RUN |
| Web | Exact-head push/PR static and Chromium/Edge gates; Pages remains skipped | NOT RUN |

Implementation and acceptance are pending. No merge, release, tag, signing, association change, Pages deployment or performance experiment is authorised. Physical-host, internal beta and public production decisions remain independent and blocked by their existing requirements.


## Catalogue and dispatch

The source catalogue is `MermaidDiagramCatalogue`; native dispatch remains in
`MainWindow.OpenVisualDraft`. All 24 historical Insert handlers now select a typed
family and retain their original default starter. The picker additionally offers
the existing simple Flowchart fixture. The 28 offered starting points do not
increase the family count.

| Family ID | Category | Starting points | Existing native editor |
| --- | --- | --- | --- |
| `flowchart` — Flowchart | Flows and systems | Empty canvas; Simple process | `MermaidVisualEditorWindow` |
| `sequence` — Sequence | Flows and systems | Standard | `SequenceVisualEditorWindow` |
| `state` — State | Flows and systems | Standard | `StateVisualEditorWindow` |
| `er` — Entity relationship | Flows and systems | Standard | `ErVisualEditorWindow` |
| `class` — Class | Flows and systems | Standard | `ClassVisualEditorWindow` |
| `architecture` — Architecture | Flows and systems | Standard | `ArchitectureVisualEditorWindow` |
| `c4` — C4 | Flows and systems | Context; Container; Component | `C4VisualEditorWindow` |
| `block` — Block | Flows and systems | Standard | `BlockVisualEditorWindow` |
| `pie` — Pie | Data and visualisation | Standard | `PieVisualEditorWindow` |
| `xy` — XY Chart | Data and visualisation | Standard | `XyVisualEditorWindow` |
| `radar` — Radar | Data and visualisation | Standard | `RadarVisualEditorWindow` |
| `sankey` — Sankey | Data and visualisation | Standard | `SankeyVisualEditorWindow` |
| `quadrant` — Quadrant | Data and visualisation | Standard | `QuadrantVisualEditorWindow` |
| `treemap` — Treemap | Data and visualisation | Standard | `TreemapVisualEditorWindow` |
| `venn` — Venn | Data and visualisation | Standard | `VennVisualEditorWindow` |
| `gantt` — Gantt | Planning and work | Standard | `GanttVisualEditorWindow` |
| `timeline` — Timeline | Planning and work | Standard | `TimelineVisualEditorWindow` |
| `journey` — Journey | Planning and work | Standard | `JourneyVisualEditorWindow` |
| `kanban` — Kanban | Planning and work | Standard | `KanbanVisualEditorWindow` |
| `requirement` — Requirement | Planning and work | Standard | `RequirementVisualEditorWindow` |
| `gitgraph` — GitGraph | Planning and work | Standard | `GitGraphVisualEditorWindow` |
| `mindmap` — Mindmap | Structures and technical | Standard | `MindmapVisualEditorWindow` |
| `treeview` — TreeView | Structures and technical | Indentation; Box drawing | `TreeViewVisualEditorWindow` |
| `packet` — Packet | Structures and technical | Standard | `PacketVisualEditorWindow` |


## Manual acceptance checklist

Run these only on an authorised disposable desktop. The automated UIA suite does
not establish physical screen-reader, alternate-DPI or personal-host acceptance.

- Open Markdown and invoke Mermaid from mouse and keyboard. Search, clear, filter
  categories and select each family. Confirm the correct preview, full name,
  variant note and existing editor; inspect light, dark and actual High Contrast.
- At 880×600 and 1280×800, check readable disabled/selected/focused states, list
  scrolling, source expansion and the visible footer. Tab/Shift+Tab must remain
  usable. Enter in search must not create; Enter in the family list must create
  only once. Escape and Cancel must restore source selection and scroll.
- Create at the beginning, middle and end; preserve selected text, Unicode,
  CRLF/LF, BOM and adjacent fences. Apply once, Undo once, Redo once, then Save and
  reopen. Inspect exact source, not merely rendered similarity.
- Edit each family at an exact body occurrence, including the second duplicate.
  Fence edges, overlapping blocks, ordinary code, protected grammar and nested
  Markdown must not receive a replacement starter.
- Exercise empty and nonempty `.mmd`/`.mermaid`, then stale source, switching,
  closing, saving, renaming and read-only owner conditions. Cancel must retain
  the document and private edits must never auto-save.
- Verify the `Formatmermaid` source action in the adjacent toolbar menu, all 24
  historical Insert commands, preview Visual Edit, Palette, Quick Open and Find.
  This does not resolve or reinterpret PR #48's independent Find/host failures.

## Development observations (not acceptance receipts)

The first targeted negative run exposed an unterminated-fence parser span ending
at the opening marker; the new coordinator now refuses that unresolved region.
The initial session assertion incorrectly counted an identical existing Flowchart
outside the insertion span; it now asserts the exact inserted occurrence, while
retaining the duplicate fixture. The first full Core run had 2,200 passes and one
failure because adding a Help topic changed the exact topic count from 21 to 22;
the expected count is updated and the new topic has its own content assertions.
These original failures are retained in local development outputs/conversation;
they are not claimed as passed runs. Source was an uncommitted development tree,
not the accepted base or a final CI source.

At this stage, hosted UIA, screenshots, installed phases, exact-head CI and package
inspection remain pending. Final receipt must supersede these pending statuses
with source-labelled evidence, without rewriting historical failed outcomes.
