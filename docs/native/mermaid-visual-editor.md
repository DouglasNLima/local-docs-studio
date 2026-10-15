# Native Mermaid visual editor

Implementation branch: `feature/native-mermaid-visual-editor`, based on verified
`42c3fc614feb6db6f69a242312604e46ff76c238`. This is work in progress and is not yet an
internal-preview readiness claim.

## Source model and transformations

`Core/Mermaid` contains a bounded lexical source adapter, semantic model, exact text
patches and private draft history. Mermaider 0.15.1 independently parses the graph;
the adapter must agree on node identities, labels, shapes and connection endpoints
before it enables transformations. Every candidate is reparsed and rendered by the
existing `DiagramService` before it becomes a committed draft. Its existing limits
are unchanged. WPF owns interactive presentation; source owns the saved diagram.

Labels change through their source spans. Other edits use disjoint, expected-text
patches, applied from the end of the source. Comments, line endings, existing IDs,
unaffected styles and surrounding source are retained. Deleting a connection or
changing endpoints retains its inline node declarations, including implicit nodes.
Mermaider honours the first node occurrence; newly moved declarations therefore
precede references, and changed connections follow existing declarations.

The verified adapter accepts `flowchart`/`graph`, TD/TB/BT/LR/RL, process, decision,
rounded, terminal, circle, double circle, subroutine, cylinder and hexagon nodes;
separate directed, undirected, dotted, thick and bidirectional connections with
pipe labels; nested named subgraphs; class definitions/assignments and node styles.
Class definitions remain authored in source. New explicit styles accept hexadecimal
fill, stroke and text colours. Labels are escaped as literal text.

Bounds: 100,000 source characters, 300 nodes, 500 total elements, eight nested
subgraphs, 500 styling statements and 4,096 characters per label. Graphs with more
than 100 connections **and** more than four connections per node open protected.
The established renderer can reject a
candidate within these bounds. This is a supported grammar, not universal Mermaid
Flowchart compatibility.

Unknown shapes, edge IDs, interactions, link styling, configuration directives,
repeated declarations, chained connections and parser disagreements protect the
entire draft from visual transformation. Source stays verbatim and inspectable.
Other Mermaid families retain the existing renderer and source editing.

Moving a node between subgraphs currently requires its connections to be declared
outside subgraphs. This prevents a remaining reference from silently restoring
its old membership. Deleting a group either flattens it, preserving its members,
or explicitly deletes descendant members and their incident connections.

Undo/redo stores exact source and selection, with a bounded history. Rejected,
cancelled and obsolete operations do not enter history. Drafts are independent of
documents and recovery; discarded operations do not modify documents.

## Native interaction

Use **Edit visually** beside a supported preview, or **Insert → Flowchart visually**
to start a new diagram. Existing `.mmd` and `.mermaid` files use their raw source.
The editor is modeless: Apply always targets its owning tab, even after switching
documents. Closing/renaming that tab or changing the tracked diagram rejects
Apply. Safe edits outside its container relocate through AvalonEdit history.
Apply changes the in-memory document in one Undo group; ordinary Save retains the
existing disk-conflict checks. Cancel discards only this editor's draft.

Choose a palette element, select native canvas elements or use the element list.
The list exposes SelectionItem/Selection patterns; each canvas node/edge/group has
an accessible name and Toggle state. Ports expose named native buttons. Selection
uses border thickness as well as colour. Physical screen-reader certification is
not claimed. Properties have explicit Update actions; unapplied field values block
Apply and selection changes until updated or explicitly reset. Inline labels use
Enter to confirm and Escape to cancel. Source changes require explicit validation;
invalid or unsupported text stays in the source panel and disables visual edits.

Copy/duplicate/paste retains supported node shapes, internal connections and basic
classes/styles. Each insertion is one visual Undo action. Conflicting class names
are remapped; copied active/unknown style values reject the entire insertion rather
than partially inserting nodes or silently dropping styling. Existing style text
outside the operation remains unchanged.

Keyboard: Tab reaches the palette, element list and properties; arrows navigate
the list. Insert adds a process; F2 edits a node label; F4 focuses properties;
Delete removes the selection with incident-connection information; Ctrl+D
duplicates; Ctrl+C/Ctrl+V copy/paste nodes; Ctrl+A selects all; Escape deselects or
cancels connection creation. Ctrl+Z/Ctrl+Y use visual history outside text fields.
F fits, Ctrl+0 restores actual size, Home resets, Shift+arrows pan, F11 toggles
Full Screen and Ctrl+Enter applies a validated draft. Text fields keep their own
ordinary editing shortcuts. Middle-drag/Space+drag pan and Ctrl+wheel zooms around
the pointer. Splitters resize the element list, properties and collapsible source
panel. Auto Layout discards temporary positions; structural group edits relayout
the compound graph. No coordinates or proprietary layout metadata are saved.

## Delivery evidence

- Source foundation: 32 focused Core tests passed locally on Windows/.NET 10.
- Initial failures retained under ignored `artifacts/native/mermaid-tests/`; repairs
  cover first-reference semantics and literal entity decoding.
- Native WPF canvas and properties are implemented: node and connection editing,
  palette insertion/drop, connection ports, inline labels, group operations,
  source inspection/validation, draft history, temporary dragging, deterministic
  compound layout, zoom/PAN and fit/reset. These remain subject to real UIA and
  visual acceptance; a successful build is not interaction certification.
- Markdown Apply tracks the existing AvalonEdit version chain and owning tab,
  rejecting overlapping edits or changed file identities. Unrelated surrounding
  edits relocate the original block through actual changes, never text searches.
  Standalone files use raw source; fenced/DevOps containers keep their wrappers.
- The complete native solution builds locally with zero warnings. All 1,211 Core
  tests pass locally, including 65 focused Mermaid cases and extracted SVG output
  for escaped literal labels. A Windows CRLF-only multiline-label disagreement
  found in CI now has explicit LF/CRLF regressions; physical source is unchanged.
- Mermaider's first-occurrence semantics and numeric-escape rendering gap were
  found by tests. The shared rendering service now decodes numeric escapes through
  a layout-provider adapter **after** parsing and **before** existing layout/SVG
  generation, preserving portable source and matching text measurement to output.
- Indented tilde fences are targeted only where the established Markdown pipeline
  recognises them as code; surrounding paragraph contexts that parse as prose do
  not acquire invented diagram identities. Blockquote/list prefixes are protected.
- Draft PR: https://github.com/DouglasNLima/local-docs-studio/pull/26.
- Disposable Windows authoring and actual high contrast passed at `0edb5d2` in
  [PR visual CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38766341642)
  and [branch visual CI](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38766331665).
  Each records 21 source/model/UI outcomes, before/after Markdown and screenshots.
  The next run expands standalone/new-diagram, inline keyboard, duplicate, dark
  theme and resize evidence. Final-revision certification remains outstanding.
- The initial dense 20-node/190-edge probe rejected rendering after 5.66 seconds
  and allocated about 4.79 GB cumulatively across rendering/edit validation. This
  exposed a practical limitation of the existing cooperative renderer deadline.
  The stricter visual density guard now prevents repeated rendering of that case;
  ordinary source/rendering workflows and the renderer's own limits are unchanged.
  At that revision, 100-node/99-edge editing took 20.8 ms, 300-node/199-edge editing
  52.5 ms and nested 100-node/20-subgraph editing 26.5 ms in the single-process
  hosted probe. These are observations, not physical input or universal guarantees.
- Existing browser pipelines passed at that revision. Broader native CI then found
  the CRLF fixture disagreement; historical failures, including the earlier static
  export-server start failure and the native selection correction, remain in CI.
- Final package identity, final full regression totals and final screenshots remain
  outstanding. No readiness claim is made from predecessor packages.
- No new build has been installed or launched on the user's ordinary desktop.

CI links, screenshots, package hashes and readiness findings will be recorded here
after the corresponding checks run. Physical monitor/DPI and screen-reader
certification require separate evidence and must not be inferred from hidden tests.
