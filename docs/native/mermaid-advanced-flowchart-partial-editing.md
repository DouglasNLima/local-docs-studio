# Advanced Flowchart compatibility and safe partial editing

Status: implementation in progress; hosted native certification and package verification pending.

## Verified baseline

PR #30 remains open and Draft at `833e9db1f8643f7023143dd94349a2709f9ae9ce`.
This branch starts at that exact head and targets
`feature/native-workspace-mermaid-interaction-stability`. Its implementation is
inherited through Git ancestry. The rejected Continuous viewer is excluded.

The source adapter already records individual chain edges, but marks the complete
model protected. Transformation planning, private draft validation and canonical
identity mapping each require global `CanEdit`. An independent node therefore
cannot be changed beside a chain.

## Delivery requirements

- Core owns operation capabilities and bounded source dependency analysis.
- Expected-text patches preserve unrelated original source exactly.
- Every accepted operation requires complete pinned-parser semantic comparison and
  bounded canonical rendering. Unknown dependencies fail closed.
- The native inspector presents individual permissions and diagnostics.
- Private history and document identity/conflict guards remain transactional.
- Synthetic fixtures cover independent branches, chains, repeated declarations,
  styles, nested groups, Unicode, parallel edges and identical document blocks.
- Disposable hosted Windows supplies native execution, actual High Contrast,
  screenshots, regression evidence and unsigned package/lifecycle verification.

Grammar matrices, measured results, immutable CI links and package hashes will be
recorded here as verification completes. Earlier reports retain their original
revision scope. This Draft PR must not be merged or publicly released automatically.

## Capability and validation architecture

`MermaidCapabilityAnalysis` caches a bounded dependency inventory against each
immutable source model. `Query` returns target, operation kind, permission, source
spans, protected dependencies, diagnostic and validation requirements. The legacy
`CanEdit` still means fully editable; it does not grant partial transformation
permission. `PartiallyEditable` and `HasCompleteSourceMap` are distinct properties.
Unknown syntax, configuration, ambiguous membership and resource limits fail closed.

The adapter checks all recognised nodes, ordered connections, labels, shapes and
nested membership against Mermaider 0.15.1. It honours the first occurrence even
when that occurrence is bare. Chains expose exact occurrence/token spans without
being split. Every planned operation must pass its individual capabilities.

The private draft applies expected-text patches off the UI thread, checks protected
overlap, reparses the entire candidate and compares the complete pinned-parser
graph against an operation-specific expected semantic delta. The comparison includes
node order, labels, shapes, markdown flags, every ordered edge property, nested
groups/directions, classes, node styles, ordinal/default edge styles, redirections
and notes. It is an internal comparison representation; it never generates Mermaid.
The original and final candidate also pass bounded canonical rendering and source
identity checks against the actual positioned graph. Rejection adds no history.

`CanApply` requires a real committed private change. Final rendering precedes the
unchanged `MermaidDocumentEditSession` transaction and its AvalonEdit revision,
container and owning-document guards. Direct manual source validation still requires
the fully supported subset; it cannot masquerade as an authorised partial operation.

## Current operation boundaries

| Operation | Known partial Flowchart | Protection |
|---|---|---|
| Inspect | Canonical nodes, ordered connections and groups | Independent of transformation permission |
| Node label / shape | Unique authoritative occurrence, including inside a chain | Repeated explicit declarations remain protected |
| Node ID / delete / move | Complete independent references | Any chain dependency or repeated declaration blocks these operations |
| Connection label / type | Exact token, including chain edges | Entire graph and original edge identity are verified |
| Reconnect / delete connection | Separate root statement | Chains, internal membership and `linkStyle` order remain protected |
| Add node | Unique ID, known scope, within budgets | No insertion into unknown source |
| Add connection | Existing known nodes | All insertion blocked when ordinal/default `linkStyle` is present |
| Local style / class | Targeted override or defined class assignment | Shared class definitions and unrelated custom properties retained |
| Group label | Known heading span | Group hierarchy retained |
| Group structure / direction | Existing fully editable workflow | Protected in partial diagrams |

Supported links retain `-->`, `---`, `-.->`, `==>`, `<-->`, pipe labels and pinned
inline text forms (`-- text -->`, `-. text .->`, `== text ==>`, `<-- text -->`).
Quoted label content uses the pinned parser's quote semantics. Label-only edits
retain original delimiters and quotes. An inline type change can replace only its
arrow span with the equivalent pipe form. Other arrow combinations, edge IDs,
interactions, unknown directives and unknown dependencies remain protected.

## Source preservation example

```mermaid
flowchart LR
    %% Preserve this exact comment
    A["Capture"] --> B["Review"] --> C["Complete"]
    D["Independent step"]
```

Changing D to `Reviewed step` changes only `"Independent step"` to
`"Reviewed step"`. The comment, chain, whitespace and line endings remain exact.
Renaming or deleting B is rejected because its chain structure is protected.
Adding or deleting a connection with ordinal `linkStyle` is rejected before
candidate rendering. Arbitrary source text has a separate explicit validation path.

## Native inspector and synthetic acceptance

The existing inspector now exposes a compact text capability state and individual
read-only fields, disabled actions, tooltips and UIA HelpText. Effective colours
are distinguished from a new local override. Update/Reset still protects pending
values when selection changes. Protected chains have no endpoint handles; safe
separate root connections retain them. Canonical drawing and pointer paths remain
the inherited PR #30 architecture.

The synthetic `advanced-flow-error-log-lab.mmd` includes multiple entries, shared
capture, storage, scheduling, retries, cycles, chains, dotted links, labels, implicit
references, nested independent branches and shared/local styles. New native UIA
checks exercise mixed permissions, pending values, node/edge edits, private history,
Apply, one document Undo, Redo, Save, process restart without rewriting the file,
reopen/render and Cancel. These checks run in normal and actual Windows High
Contrast alongside all existing authoring and interaction tests.

Local Release builds and the full Core suite passed 1,301 tests before the final
renderer-correspondence tightening; the subsequent run and hosted evidence are
pending. The initial retained run had four old global-read-only expectations fail;
those cases now also verify safe insertion and explicit unsafe-operation rejection.
No existing scenario has been removed. Forty-eight deterministic seeds exercise
whitespace, LF/CRLF/mixed endings, Unicode, declaration order, class sharing and
parallel edge labels, checking exact source and history restoration.
