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

Initial Core suite: **42 Sequence cases passed locally**. Native UI, accessibility, canonical drawing fidelity, full regressions, performance, screenshots and package certification remain pending. No native application has been executed or installed on the ordinary desktop.

## Integration considerations

Shared integration surfaces are `MainWindow.MermaidVisualEditor.cs`, native preview family routing, menu/command registration, theme propagation and owner-close handling. Sequence semantics and transformations live separately from Flowchart nodes/edges and do not depend on PR #31. Existing `MermaidBlockSource` and `MermaidDocumentEditSession` remain the document identity and exact-container authority.
