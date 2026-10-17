# Native Mermaid Sequence visual editor

This work extends the native editor independently from Draft PR #31. The baseline is `7bd2207b9c18e451b17678f4dd417134070a5722` on `develop`. Saved diagrams remain ordinary Mermaid. This report is updated as implementation and certification proceed; unrecorded checks are not certified.

## Verified renderer grammar

Mermaider **0.15.1**, NuGet repository commit `71b64845570d633a94d29e85579c0a02786fece8`, is the authority. Inspection covered [SequenceParser](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Parsing/SequenceParser.cs), `SequenceDiagram`, `PositionedSequenceDiagram`, [SequenceLayout](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Layout/SequenceLayout.cs), [SequenceSvgRenderer](https://github.com/nullean/mermaider/blob/71b64845570d633a94d29e85579c0a02786fece8/src/Mermaider/Rendering/SequenceSvgRenderer.cs) and `MermaidRenderPipeline`. Sequence layout bypasses `IGraphLayoutProvider`; the renderer path is unchanged.

| Construct | Pinned renderer | Source recognised | Selectable inspection | Safe transformation | Protected scope |
|---|---|---|---|---|---|
| `sequenceDiagram` | Yes | Exact header | Source | New diagram creation | Header rewriting |
| `participant`, `actor`, `as` | Yes | Explicit ID/type/label spans | Headers/lifelines/list | Add, alias/type, known-reference rename, bounded order/delete | Duplicate/late declarations; unknown references |
| Implicit participants | Yes | First-use identity/order | Headers/lifelines/list | Existing message text/arrow | Declaration, endpoint/order changes |
| `->>`, `-->>`, `->`, `-->` | Yes | Exact arrow/endpoints/text | Path/label/list | Add/update/delete/duplicate/bounded order | Dependency/block constraints |
| `-)`, `--)` | Open head | Yes | Path/label/list | Same as other messages | No distinct asynchronous glyph in pinned renderer |
| `-x`, `--x` | Filled head | Yes | Path/label/list | Same as other messages | No distinct cross head in pinned renderer |
| `<<->>`, `<<-->>` | Yes | Bidirectional token | Path/label/list | Bounded message edits | Activation shorthand; reverse head omitted on self messages |
| Self messages | Yes | Sender equals recipient | Canonical polyline/label/list | Bounded message edits | Same dependency constraints |
| Message `+` / `-` | Yes | Marker and trigger identity | Message inspector; unique simple bar | Simple lifecycle marker changes | Nested/unmatched lifecycles; blocks |
| Standalone `activate` / `deactivate` | Adjacent subset through Lens adapter | Command spans and exact trigger | Command/message inspector; unique simple bar | Change/remove original command | Non-adjacent/conflicting/ambiguous lifecycles |
| Notes left/right | Yes | Placement, text, participant | Note/list | Add/update/delete | One participant; block-bound deletion/insertion |
| Notes over | Yes | Placement, text, participant pair | Note/list | Add/update/delete | One or two participants; block-bound deletion/insertion |
| `autonumber [start [step]]` | Yes | Numbering state | Source; numbered messages | Preserve through message changes | Numbering-control authoring; overflow/mismatched semantics |
| `loop`, `alt`, `opt`, `par`, `critical`, `break`, `rect` | Yes | Boundaries/type/label/depth | Block/list headings | Bounded message labels inside blocks | Structural rewrites; insertion/deletion/reorder inside blocks |
| `else`, `and`, `end` | Yes | Boundary statements | Source | Preserve | Structural changes |
| `box` | Yes | Protected raw span | Canonical drawing/source | Preserve only | Entire dependency-sensitive draft |
| `create participant/actor`, `destroy` | Yes | Protected raw span | Canonical drawing/source | Preserve only | Entire dependency-sensitive draft |
| Unknown statements/directives | May be ignored by renderer | Protected raw span | Original drawing/source | Preserve only | All visual transformations fail closed |

The editable identifier subset is letters, digits and underscores, beginning with a letter or underscore. Plain Unicode labels and verified `<br/>`/escaped-newline representation are supported. Rich markup is retained for source editing.

## Source and history architecture

Dedicated `Core.Sequence` records describe participants, messages, notes, activations and statements. Every recognised occurrence has exact body/line/token spans and a private identity. Transformations return bounded expected-text patches and a planned identity order; the whole diagram is never serialised. Unchanged comments, whitespace and mixed line endings remain intact. A separate private draft validates the complete candidate through the existing `DiagramService` and compares renderer metadata with recognised semantics before adding one bounded history entry. Rejected, cancelled and superseded operations do not commit source/history. Undo/Redo restores model, source, selection and private history identity.

For example, editing the final repeated `A->>B: Request` patches only that occurrence's label token. Renaming `B` patches its declaration and known endpoint/note/activation reference tokens, leaving comments and unrelated labels untouched. Unknown dependencies prevent this rename.

## Verification status

The expanded local suite passes **1,292 Core cases, including 58 Sequence cases**, with no skips. Release builds have zero warnings/errors; static integrity and all **268 Chromium/Edge browser cases** pass locally. No native application has been executed or installed on the ordinary desktop.

At source `ca331b92c66b8b4868eca1af0145db18684a922c`, [desktop UX CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033004555) passed the Sequence normal and actual High Contrast jobs, with 18 UIA assertions each and 73 Sequence fidelity/geometry assertions per appearance job. The complete existing Flowchart UIA/fidelity and baseline jobs also passed. [Workspace and interaction CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033004553) passed. [Both browser projects](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033004612) passed. These results belong to that source revision; subsequent enhancements require fresh certification.

Initial native captures were inspected at all five requested window sizes, plus light, dark, selected message/note/participant, activation inspector, message creation and source collapse/expansion. Apply remained visible at the minimum size. The initial source-specific captures and performance observations are retained in the CI artefact `native-sequence-normal` / `native-sequence-high-contrast`. Package and final-HEAD results are delivered with their exact source identity in the PR and verification receipts; no skipped mandatory check counts as a pass.

The expanded run at `e1cad9205c6c04cf0474265d055fcc40b3f2c8e9` passed **101 fidelity/ownership assertions, 27 normal UIA assertions and 28 actual High Contrast assertions** in [desktop UX CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39033667815). The additional cases cover participant rename/actor creation/reorder/deletion, real pointer message selection, message ordering/duplication, empty diagrams, protected blocks, repeated occurrences and standalone `.mermaid` Apply/Undo/Save. Source-specific receipts are retained under [evidence/sequence-visual-editor](evidence/sequence-visual-editor/).

Screenshot review of that run found that an inherited Flowchart hover fill obscured a canonical message label in dark mode. The [retained before image](evidence/sequence-visual-editor/before-hover-dark.png) records the defect. The subsequent Sequence-only hit-target style uses transparent interiors with focus/selection outlines; it has no apparent drag handles. A focused regression also retains shorthand activation selection through message edits and private history.

At `269b288ae89714add99e3f74937ce6382b3b6665`, both Sequence appearances and existing Flowchart authoring passed in [desktop UX CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39034075723). The corrected dark capture was inspected: message labels remain visible through hovered hit targets. Receipts prefixed `269b288` retain that source-specific result.

The expanded protected-grouping performance case exposed a native colour-adapter failure at `13d0457`: WPF's `ColorConverter` rejected functional `rgb(...)` inside Mermaider colour interpolation. [The failure receipt](evidence/sequence-visual-editor/13d0457-colour-failure.json) retains its source/job/artefact identity. The correction parses bounded RGB/RGBA channels, retains the original scenario and adds real native PNG regressions for numeric, percentage and alpha forms. Sequence conversion failures also retain source and a visible diagnostic instead of escaping the asynchronous window handler. The pinned layout, sanitisation and protected grouping policy are unchanged.

Style materialisation now leaves inert `data-*` source metadata literal. Labels mentioning `var(...)`, `color-mix(...)` or `rem` units cannot be mistaken for style declarations or corrupt correspondence checks. Sequence and Flowchart native regressions exercise those labels through canonical mapping and PNG output. Active/external SVG checks still run before attribute materialisation.

## Screenshot gallery

These native captures come from disposable Windows at `269b288`; they are not mock-ups. The hover correction is visible in the dark capture.

![Native Sequence message inspector](images/sequence-visual-editor/selected-message.png)

| Area | Captures |
|---|---|
| Starting states | [Empty](images/sequence-visual-editor/empty.png), [starter/multiple participants](images/sequence-visual-editor/starter.png), [new diagram](images/sequence-visual-editor/new-sequence.png) |
| Selection and authoring | [Participant](images/sequence-visual-editor/selected-participant.png), [message](images/sequence-visual-editor/selected-message.png), [note](images/sequence-visual-editor/selected-note.png), [activation](images/sequence-visual-editor/activation-inspector.png), [message creation](images/sequence-visual-editor/message-creation.png), [reordering](images/sequence-visual-editor/message-reorder.png) |
| Protected and repeated source | [Block inspection](images/sequence-visual-editor/protected-block.png), [repeated messages](images/sequence-visual-editor/repeated.png), [reopened document](images/sequence-visual-editor/reopened.png) |
| Source and appearances | [Collapsed](images/sequence-visual-editor/source-collapsed.png), [expanded](images/sequence-visual-editor/source-expanded.png), [dark](images/sequence-visual-editor/dark.png), [actual High Contrast](images/sequence-visual-editor/high-contrast-note.png) |
| Window sizes | [880 × 600](images/sequence-visual-editor/viewport-880x600.png), [1024 × 768](images/sequence-visual-editor/viewport-1024x768.png), [1280 × 800](images/sequence-visual-editor/viewport-1280x800.png), [1440 × 900](images/sequence-visual-editor/viewport-1440x900.png), [1920 × 1080](images/sequence-visual-editor/viewport-1920x1080.png), [High Contrast minimum](images/sequence-visual-editor/high-contrast-880x600.png) |

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

The exact pinned layout pipeline calls internal `SequenceLayout` directly and has no public Sequence layout hook. The editor uses the documented fallback: verify actors, message ordinals/endpoints/styles/labels, notes, blocks and simple activation participant counts against generated SVG metadata. Only then derive hit bounds from that render's canonical rectangles, message lines/polylines, label boxes and note primitives. Source spans/identities come exclusively from the lexical model. Repeated identical messages map by verified source/render order, never text search. Repeated block headings and a participant with several activation lifecycles retain exact-list/message inspection when their canvas regions cannot be mapped uniquely; the status explains that restriction. Failed interaction correspondence retains the current canonical drawing for read-only inspection and disables visual changes.

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
