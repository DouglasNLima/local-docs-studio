# Native Mermaid State Visual Editor

Status: implementation in progress; not certified.

The feature branch starts at consolidated `develop` commit
`77dff109d84c0730c5b6b3574c71b02b112b082f`, including advanced Flowchart
editing (PR #31, merge `91f541ce`) and the Sequence editor (PR #32).
The integrated web checks passed in [run 39045995936](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39045995936).
Native post-merge [run 39045995957](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39045995957)
was running when work began and subsequently completed successfully, including
desktop, package verification and installer lifecycle. The consolidated baseline
is certified within those workflow scopes, rather than inferred from earlier PR checks.

## Engineering scope

Add a dedicated State source model, expected-text transformations, private
history and native WPF editor. Preserve canonical Mermaider rendering and
existing document ownership/version guards. Verify the actual Mermaider
0.15.1 source at package repository commit
`71b64845570d633a94d29e85579c0a02786fece8` before exposing operations.

## Pinned syntax matrix

Inspection used the actual NuGet package repository revision, including
`Parsing/StateParser.cs`, `MermaidRenderer.cs`, `MermaidRenderPipeline.cs`, the
positioned graph records and emitted SVG. Executable fixtures use the application
`DiagramService` with its existing 100,000-character, 500-element and three-second
rendering limits. Rendering success alone does not establish intended semantics:
this parser silently ignores several unsupported statements.

| Construct | Recognised by pinned parser | Renders | Source model / inspection | Visual editing v1 |
| --- | --- | --- | --- | --- |
| `stateDiagram-v2`, `stateDiagram` | Yes | Yes | Header retained | Supported |
| Named states through transitions | Yes, first reference creates state | Yes | Exact ID/reference spans | Label materialised before first reference when simple |
| `state "Label" as ID` | Yes, word IDs | Yes | First declaration and all references | Label; safe simple ID rename |
| `ID : description` | Yes, first occurrence wins | Yes | Exact description span | Label; description is display text, not a state action |
| Bare `state ID` | Ignored | Empty/partial output possible | Unknown, retained | Protected |
| `[*] --> ID`, `ID --> [*]` | Yes | Yes | Distinct initial/final identities per source scope | Create/reconnect/remove through simple transitions |
| Directed, labelled, self and cyclic transitions | Yes | Yes | Private occurrence identity and exact endpoint/label spans | Supported subject to dependencies |
| Repeated identical transitions | Yes, distinct ordered edges | Yes | Distinct private identities | Exact occurrence; overlapping pointer paths offer a choice |
| Composite and nested state blocks | Yes | Yes | Opening/closing spans, parent and references | Selection, inspection and unambiguous heading labels |
| Internal actions/transitions | No separate internal-action grammar | May be mistaken for a description | Original description/unknown statement retained | No action authoring |
| `<<choice>>`, `<<fork>>`, `<<join>>` | Yes | Yes | Special-state identity and inspection | Protected; no special-state palette insertion |
| Inline and block left/right notes | Yes | Yes | Protected full statement and target | Preserved; independent declared labels may be edited |
| Direction commands | Yes, scoped | Yes | Exact command retained | Source editing only |
| Full-line comments | Skipped by renderer | Yes | Original source bytes retained | Never rewritten |
| Concurrent regions `--` | Separator ignored | Partial drawing possible | Unknown dependency | Protected |
| `classDef`, `class`, `style`, class shorthand | Yes | Yes for bounded fixtures | Protected styling/dependency statements | Labels may remain independently editable; structural edits protected |
| Configuration directives | Preprocessed by renderer | May render | Protected dependency; never treated as an inert comment | Source only |
| Unknown directives, malformed boundaries | Ignored or rejected | Partial output possible | Retained with protection/diagnostics | No guessed editable identities |

The hosted `state-fidelity.json` report contains the exact probe sources,
recognition/mapping results and render outcomes. No online newer Mermaid grammar
is used to infer support. The source adapter currently recognises the standard
case-sensitive header spellings above; other casing remains source-only.

## Model, identity and source integrity

`Core/StateDiagrams/` is independent of both Flowchart and Sequence authoring
models and the existing persistence namespace. It records the original source,
header, lexical statements, token and line spans, states, declarations/references,
ordered transitions, parent scopes, composite boundaries and scoped pseudo-nodes.
The original source retains comments, whitespace, unsupported statements, Unicode
and line endings verbatim. No graph is serialised back into Mermaid.

Every transition statement receives a private occurrence identity. An edit plan
carries the retained identity sequence through complete reparsing; Undo restores
the original model object, identities and selection. Initial and final markers
have separate identities for each enclosing composite, verified against the
pinned parser's generated `_start`/`_end` nodes. These generated IDs never become
user-renamable source IDs.

Each transformation checks capabilities, builds expected-text patches, rejects
stale/overlapping spans, applies privately, reparses the full source, compares the
complete pinned parser graph against only the authorised semantic delta, renders
the candidate and verifies renderer/source correspondence before committing.
The comparison includes notes, styles, directions, node order, hierarchy and
composite endpoint redirections. Implicit label materialisation may move only
that selected state's first occurrence; unrelated state order is checked.

Private history is capped at 100 snapshots and eight million retained source
characters. Failed, cancelled and no-op operations add no entry. Generation guards
reject stale validation. A history branch gets a fresh identity even when its
source coincidentally matches an earlier snapshot. Text source editing is validated
as a separate private revision; it never borrows old occurrence identities.

## Operation capabilities

| Operation | Implemented | Protection |
| --- | --- | --- |
| Add ordinary state | Explicit portable alias declaration | Complex hierarchy/notes/styles or unknown dependencies |
| State label | Exact declaration token, or earlier declaration for simple implicit state | Duplicate declarations, special states, shared scopes, unknown dependencies |
| Rename ID | All known ID references in simple diagrams | Incident unknown/hierarchical/styling/note dependencies; duplicate/reserved/invalid ID |
| Duplicate state | New independent explicit state | Complex/special states |
| Delete state | Independent explicit declaration | Incident edges or unresolved references |
| Add transition | Existing states or correctly oriented root pseudo-markers | Unknown endpoints, group membership or complex dependencies |
| Transition label | Exact occurrence token | Unknown dependency or failed complete semantic/render check |
| Reconnect / reverse | Minimal endpoint tokens, complete graph comparison | Scope/role conflict, disappearing implicit states, changed unrelated order or complex dependencies |
| Delete transition | Exact full line | Lost implicit states or complex dependencies |
| Composite label | Exact heading token / portable labelled heading | Ambiguous source boundaries or failed semantic validation |
| Add/move composite membership, notes, choice/fork/join | Protected | No unproven authoring command exposed |

The inspector displays property/operation capabilities, with disabled-action
explanations available through tooltips and UI Automation help text. A valid
complex drawing can permit independent declared-state and transition-label edits
without permitting structural operations. Unknown dependencies fail closed.

## Native presentation and interaction

State uses the actual sanitised `DiagramService` result and native SharpVectors
conversion used by FlowDocument preview. Pinned State rendering demonstrably uses
the same graph layout/SVG metadata as Flowchart; the existing geometry adapter is
shared only after State-specific semantic verification. Neither editor's semantic
model is reused or changed. SVG supplies geometry, never source spans.

State/pseudo/group hit geometry and transition ordinals are verified against that
same positioned graph. Transition labels and precise native path hit tests select
private occurrences. Overlapping paths offer explicit source-order choices. The
element list and canvas expose keyboard/SelectionItem alternatives.

Simple ordinary states can be dragged by translating the existing native drawing
groups, using a fixed drawing origin and null-safe transforms. Flattening occurs
once per canonical render. Pointer movement is coalesced at dispatcher render
priority; incident lines use cached session guides. Positions remain private,
survive compatible edits and private history, and are discarded by Auto layout.
No hidden coordinates or proprietary format enter saved Mermaid. Canonical
preview is always available independently of temporary positions.

Rendering/conversion is serialised and stale generations rejected; SharpVectors
conversion cannot be interrupted mid-call. Existing rendering resource limits
and the separate Continuous 250 ms/dense-render cancellation limitations remain.

## Document integration and keys

Preview Visual Edit routes State, Sequence and Flowchart to their dedicated
editors. Insert → State diagram visually… creates a valid supported starter.
Markdown fences, Azure DevOps containers and standalone `.mmd`/`.mermaid` use
the existing `MermaidDocumentEditSession`, document identity/version chain and
final wrapper validation. Identical diagrams are mapped by their owning source
container/version, never by text equality. Apply commits one AvalonEdit document
update; Cancel preserves the owning source. Ordinary Save retains disk-conflict
checks.

Outside text fields: Ctrl+Z/Ctrl+Y private Undo/Redo; Ctrl+Shift+S adds a state;
Ctrl+Shift+T adds a transition; F fits; Ctrl+0 shows actual size; F2 focuses the
label; Delete removes an eligible selection; Ctrl+Enter applies. Middle mouse
pans; Ctrl+wheel zooms. Escape cancels a pointer interaction before closing.
Inspector endpoint selectors provide an alternative to canvas picking.

## Validation and certification status

Implemented: dedicated source model, expected-text transformations, complete
semantic validation, bounded private history, canonical WPF canvas/inspector,
source validation, family routing, insertion and document Apply integration.

Local Release build: zero warnings/errors. Initial complete Core suite: 1,408
passed without skips; expanded complete suite: 1,413 passed; State suite: 31 passed.
At `ff7dfb0`, both normal and actual High Contrast State native UIA acceptance,
Sequence regressions, Flowchart interaction regressions and the complete native
desktop UX workflow passed. The first `0afabbc` State UIA attempt reached the
authoring operations but then requested a source control after collapsing its
panel; the corrected harness retains the source panel during pointer checks.
Both failure JSON reports are retained alongside the first passing fidelity
report under `evidence/state-visual-editor/`. No assertion or timeout was relaxed.

The `ff7dfb0` PR Edge run failed one existing browser-fallback test because the
top bar had browser-default `display: block` instead of the required stylesheet
value; Chromium and the same-revision push browser run passed. The failed trace
is retained for investigation; the earlier successful run is not a substitute
for final-head browser certification.

Hosted State grammar,
fidelity/performance, full native UIA, High Contrast and existing editor/browser
regressions are being extended. The new State jobs are mandatory on PRs to `develop`.
Pages deployment remains restricted to default-branch non-PR events. Native
package builds check out the exact PR head for source provenance.

Not yet certified: final-head UIA, screenshots, performance observations,
Flowchart/Sequence hosted regression conclusions, installer lifecycle and
independent unsigned package verification/hashes. Evidence and screenshot gallery
will be added after actual hosted acceptance. No production readiness, signing,
merge, tag, release or installation on the ordinary desktop is authorised.
