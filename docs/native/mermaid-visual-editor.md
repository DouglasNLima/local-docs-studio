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
subgraphs and 4,096 characters per label. The established renderer can reject a
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
- The complete native solution builds locally with zero warnings. All 42 focused
  Mermaid tests pass, including extracted SVG output for escaped literal labels.
- Mermaider's first-occurrence semantics and numeric-escape rendering gap were
  found by tests. The shared rendering service now decodes numeric escapes through
  a layout-provider adapter **after** parsing and **before** existing layout/SVG
  generation, preserving portable source and matching text measurement to output.
- Indented tilde fences are targeted only where the established Markdown pipeline
  recognises them as code; surrounding paragraph contexts that parse as prose do
  not acquire invented diagram identities. Blockquote/list prefixes are protected.
- Draft PR: https://github.com/DouglasNLima/local-docs-studio/pull/26.
- The disposable Windows `mermaid-visual-editor` CI job exercises source/model
  outcomes through real controls and records screenshots, failures and timings.
  Its first run is pending; performance certification and package identity remain
  outstanding. Initial foundation CI passed existing browser and desktop UX checks;
  one branch native run failed starting the static export server, while the PR run
  passed that check and continued. Historical failure evidence is retained in CI.
- No new build has been installed or launched on the user's ordinary desktop.

CI links, screenshots, package hashes and readiness findings will be recorded here
after the corresponding checks run. Physical monitor/DPI and screen-reader
certification require separate evidence and must not be inferred from hidden tests.
