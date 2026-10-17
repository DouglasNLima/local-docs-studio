# Native Mermaid Sequence visual editor

This work extends the native editor independently from Draft PR #31. The baseline is `7bd2207b9c18e451b17678f4dd417134070a5722` on `develop`. Saved diagrams remain ordinary Mermaid. This report is updated as implementation and certification proceed; unrecorded checks are not certified.

## Verified renderer grammar

Mermaider **0.15.1**, NuGet repository commit `71b64845570d633a94d29e85579c0a02786fece8`, is the authority. Inspection covered `SequenceParser`, `SequenceDiagram`, `PositionedSequenceDiagram`, `SequenceLayout`, `SequenceSvgRenderer` and `MermaidRenderPipeline`. Sequence layout bypasses `IGraphLayoutProvider`; no change to that renderer path is proposed.

| Construct | Pinned renderer | Source model / operation policy |
|---|---|---|
| `sequenceDiagram` | Supported | Required exact header |
| `participant`, `actor`, `as` aliases | Supported | Explicit declarations editable; safe known-reference rename |
| Implicit participants | Supported | Inspect; bounded message label/arrow edits; endpoint/order changes protected |
| `->>`, `-->>`, `->`, `-->` | Supported | Editable solid/dashed, filled/open arrows |
| `-)`, `--)` | Supported | Open head; renderer does not distinguish an asynchronous head |
| `-x`, `--x` | Parsed as filled head | Preserved/editable as pinned semantics; no distinct cross head |
| `<<->>`, `<<-->>` | Supported | Editable bidirectional messages; reverse head omitted on self messages |
| Self messages | Supported | Editable; canonical polyline geometry |
| Message `+` / `-` | Supported | Simple non-nested activation lifecycles editable |
| Standalone `activate` / `deactivate` | Lens adapter only | Exact adjacent-message subset; original representation retained |
| Notes left/right/over | Supported | One participant; over may span two; source position retained |
| `autonumber [start [step]]` | Supported | Preserved; rendered labels verified including prefix |
| `loop`, `alt`, `opt`, `par`, `critical`, `break`, `rect` | Supported | Boundaries retained; inspect; message labels editable; structural changes protected |
| `else`, `and`, `end` | Supported | Retained; source-only structural changes |
| `box` | Supported | Preserved, protected dependency analysis |
| `create participant/actor`, `destroy` | Supported | Preserved, protected dependency analysis |
| Unknown statements | May be silently ignored | Preserved; all visual transformations fail closed |

The editable identifier subset is letters, digits and underscores, beginning with a letter or underscore. Plain Unicode labels and verified `<br/>`/escaped-newline representation are supported. Rich markup is retained for source editing.

## Source and history architecture

Dedicated `Core.Sequence` records describe participants, messages, notes, activations and statements. Every recognised occurrence has exact body/line/token spans and a private identity. Transformations return bounded expected-text patches and a planned identity order; the whole diagram is never serialised. Unchanged comments, whitespace and mixed line endings remain intact. A separate private draft validates the complete candidate through the existing `DiagramService` and compares renderer metadata with recognised semantics before adding one bounded history entry. Rejected, cancelled and superseded operations do not commit source/history. Undo/Redo restores model, source, selection and private history identity.

For example, editing the final repeated `A->>B: Request` patches only that occurrence's label token. Renaming `B` patches its declaration and known endpoint/note/activation reference tokens, leaving comments and unrelated labels untouched. Unknown dependencies prevent this rename.

## Verification status

The current local suite passes **1,285 Core cases, including 51 Sequence cases**, with no skips. Release builds have zero warnings/errors; static integrity passes. No native application has been executed or installed on the ordinary desktop.

At source `ca331b92c66b8b4868eca1af0145db18684a922c`, [desktop UX CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033004555) passed the Sequence normal and actual High Contrast jobs, with 18 UIA assertions each and 73 Sequence fidelity/geometry assertions per appearance job. The complete existing Flowchart UIA/fidelity and baseline jobs also passed. [Workspace and interaction CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033004553) passed. [Both browser projects](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033004612) passed. These results belong to that source revision; subsequent enhancements require fresh certification.

Initial native captures were inspected at all five requested window sizes, plus light, dark, selected message/note/participant, activation inspector, message creation and source collapse/expansion. Apply remained visible at the minimum size. The initial source-specific captures and performance observations are retained in the CI artefact `native-sequence-normal` / `native-sequence-high-contrast`. Package and final-HEAD results are delivered with their exact source identity in the PR and verification receipts; no skipped mandatory check counts as a pass.

## Safe operations and protected boundaries

| Area | Implemented operations | Restrictions |
|---|---|---|
| Participants | Add participant/actor; edit alias/type; rename all known ID references; explicit declaration reorder; dependency-free delete | Implicit declarations inspect only; duplicate/late declarations and unknown dependencies protect the source; remove known dependencies before deletion |
| Messages | Add, update text/endpoints/arrows, duplicate, insert before/after, delete, earlier/later | New endpoints and order changes require explicit participants; activation-triggering endpoints stay fixed; only adjacent top-level messages without activation dependencies may move |
| Notes | Add, update text/placement/participants, remove; defined insertion position | Left/right notes use one participant; over uses one or two; block-bound removal/insertion protected |
| Activations | Inspect triggering participant/message; change/remove marker or adjacent standalone command | Simple non-nested lifecycles only; block/bidirectional changes protected; unmatched deactivation rejected; existing standalone representation retained |
| Structural blocks | Canonical display; heading inspection; bounded message-label edits | Source-only block creation/rewrite, boundary-crossing moves, deletion inside blocks, boxes and create/destroy |
| History | Atomic batches; one private step per successful visual operation; exact Undo/Redo and selection; 100-step/8-million-character bound | Rejected/cancelled/superseded candidates retain source/history; new edits discard obsolete Redo branches |

## Canonical mapping and rendering

The exact pinned layout pipeline calls internal `SequenceLayout` directly and has no public Sequence layout hook. The editor uses the documented fallback: verify actors, message ordinals/endpoints/styles/labels, notes, blocks and simple activation participant counts against generated SVG metadata. Only then derive hit bounds from that render's canonical rectangles, message lines/polylines, label boxes and note primitives. Source spans/identities come exclusively from the lexical model. Repeated identical messages map by verified source/render order, never text search. A participant with several activation lifecycles retains list/message inspection when a bar cannot be mapped uniquely.

The drawing is the unchanged existing `NativeSvgService.Render` result, including its SVG sanitisation. Selection overlays do not regenerate SVG. Parsing/candidate validation run off the UI thread; native conversion uses the existing serial STA worker with cancellation and stale-generation checks. Conversion itself is non-interruptible in SharpVectors; obsolete work finishes before another conversion starts, bounding threads/drawings. Hover and pointer movement perform no parsing or rendering.

## Document lifecycle and accessibility

`MermaidBlockSource` and `MermaidDocumentEditSession` own exact body/container tracking, version relocation, stale/overlap rejection and the one-change AvalonEdit commit. Sequence validation and final rendering finish before the session prepares/commits. Owner closure, saving/renaming and competing source revisions reject Apply without source loss. Cancel affects only the private draft. The native UIA fixture exercises a second identical Markdown diagram through selection, message/note edits, activation changes, private history, validation, Apply, document Undo/Redo, Save, close and reopen.

WPF lists expose Selection/SelectionItem; named controls and canonical hit targets expose stable automation names and private occurrence IDs. Text fields retain their normal shortcuts. Creation, ordering, deletion, validation, source navigation, zoom, Fit, scrolling/PAN, Apply and Cancel have keyboard command alternatives. Pending properties prevent silent selection/Apply loss and remain available after rejection. Disabled structural/dependency actions include diagnostic help text. Actual High Contrast is enabled/restored only on disposable GitHub-hosted Windows. Physical higher-DPI certification remains outstanding.

## Initial performance observations

Hosted measurements at `ca331b9`, in milliseconds; these are observations rather than responsiveness guarantees:

| Participants / messages | Parse | Semantic validation | Render | Native conversion | Allocations |
|---|---:|---:|---:|---:|---:|
| 2 / 5 | 0.19 | 0.49 | 1.48 | 35.51 | 5.2 MB |
| 10 / 50 | 0.44 | 1.85 | 3.35 | 253.42 | 40.7 MB |
| 20 / 150 | 1.22 | 4.34 | 7.80 | 876.96 | 116.2 MB |

Allocation figures combine the measured stages. The 150-message conversion motivated moving conversion onto the existing STA worker in the subsequent candidate. A thousand private selection changes took under 0.7 ms in these hosted measurements; this excludes real UIA transport/native focus and is not a UI latency guarantee. The historical Continuous preview 250 ms limitation is unchanged.

## Integration considerations

Shared integration surfaces are `MainWindow.MermaidVisualEditor.cs`, native preview family routing, menu/command registration, theme propagation and owner-close handling. Sequence semantics and transformations live separately from Flowchart nodes/edges and do not depend on PR #31. Existing `MermaidBlockSource` and `MermaidDocumentEditSession` remain the document identity and exact-container authority.
