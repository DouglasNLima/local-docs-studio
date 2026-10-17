# Native Mermaid ER visual editor v1

Implementation and certification are separate. This work starts at consolidated
`develop` `3229246af356829771a200134a5df38d3cc8a04c`, containing the normal merges
of PRs #30, #31, #32 and #33. Main remains untouched. Post-merge browser/static
[run 39061522521](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39061522521)
passed Chromium and Edge; Pages deployment was skipped. The native
[post-merge run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39061522483)
was still executing installer lifecycle checks when this implementation milestone
was recorded. Baseline acceptance remains pending its final result.

The local Release solution builds with zero warnings/errors. All 1,510 Core cases
pass, including 86 ER cases. Actual Windows UIA, High Contrast, canonical
fidelity, performance observations, installer lifecycle and independent package
verification are mandatory hosted gates and are **not yet certified** at this
milestone. Earlier State/Sequence/Flowchart results are retained in their own reports.

## Architecture and source authority

`LensDocsStudio.Core.ErDiagrams` owns complete bounded source recognition, exact
statement/token spans, semantic entities, implicit entities, ordered attribute and
relationship occurrences, protected statements, operation permissions, expected-text
patch plans, independent expected semantics and a bounded private draft. The source
revision is a SHA-256 of the complete source. Whitespace, comments and mixed LF/CRLF
remain in the original string; they are never reconstructed from a graph.

`DiagramService` uses the existing per-render `MermaidEntityLayoutProvider`. Its
`LayoutEr` delegates exactly once to the original provider and captures immutable
parser semantics and positioned geometry for that revision. Mermaider 0.15.1,
SharpVectors and the existing native FlowDocument preview remain authoritative.
No database, external rendering service, browser runtime or replacement layout is
used. The browser product and GitHub Pages publication remain static and separate.

## Pinned grammar investigation

The installed NuGet metadata pins Mermaider 0.15.1 to
[`71b64845570d633a94d29e85579c0a02786fece8`](https://github.com/nullean/mermaider/tree/71b64845570d633a94d29e85579c0a02786fece8).
The exact source was fetched and inspected: `ErParser`, `ErDiagram`, `ErEntity`,
`ErAttributeInfo`, `ErRelationship`, `ErCardinality`, `PositionedErDiagram`,
`PositionedErEntity`, `PositionedErRelationship`, `LightweightErLayoutEngine` and
`ErSvgRenderer`. The adapter is deliberately narrower than permissive parser regexes.

In the following table, correspondence means every captured parser entity,
attribute, ordered key marker, comment, direction and relationship matches the
source model. Every accepted edit must also match its independent expected complete
semantics. SVG success alone never grants permission.

| Construct | Pinned parser / renderer | Correspondence and inspection | Source transformation / protection |
|---|---|---|---|
| Exact `erDiagram` header | Recognised; normal source renders | Exact header required | Header editing uses source |
| Comment before header | Renderer family detection fails | Original source retained | No private edit/history commit |
| Implicit entity | Created by recognised relationship | Semantic ID and first occurrence order checked | Label/attribute edit adds explicit block; last-reference removal requires explicit block first |
| Explicit block | Separate opening/closing lines; attributes accumulated | Full block and ordered row spans | Ordinary single block supported; repeated declarations protect structure |
| `ENTITY["Label"]` alias | Recognised on block opening; latest explicit alias wins | Alias value/span retained | Existing alias token edited in place; repeated authority protected |
| Unquoted block alias | Recognised | Exact token retained | Bounded alias edit preserves its syntax |
| Attribute type/name | Pinned parser accepts broad non-space tokens | Every row counted; safe single-token grammar independently checked | Safe type/name spans edited; unsupported tokens protected |
| PK, FK, UK | Case-insensitive, space-separated markers retained in order | Complete ordered marker list checked | Toggle supported unique markers; unchanged ordering retained |
| `PK, FK` | Comma token silently ignored, FK retained | Entire original statement protected | Never repair or rewrite the recognised prefix |
| Quoted attribute comment | First double-quoted comment parsed; HTML breaks normalised | Complete comment and exact original source retained | Ordinary single-line quoted comment supported; quote/braces/newline injection rejected |
| Relationship endpoints | Both semantic IDs recognised | Exact endpoint token spans | Reconnect existing entities; unknown global references protect reconnection |
| Cardinalities | One, zero/one, many, zero/many; token characters normalised | Both endpoint values checked independently | All 16 endpoint pairs, solid/dotted: 32 cases |
| Optional relationship label | Unlabelled, plain, single/double-quoted supported | Complete semantic label checked | Existing quote style retained on label change; unsupported quoting protected |
| Identifying `--` | Solid relationship | Identifying semantic flag checked | Explicit identifying choice |
| Non-identifying `..` | Dotted relationship | Non-identifying semantic flag checked | Explicit non-identifying choice |
| Self-relationship | Supported; laid out after ordinary relationships | Reconciled to original occurrence, not global source ordinal | Independent editing, reversing and duplication supported |
| Repeated/parallel relationship | Distinct ordered source occurrences retained | Verified tuple and actual positioned order | Exact private identity; overlapping routes offer occurrence menu/list |
| TD, TB, LR, BT, RL | Direction parser accepts all five | Direction matches captured pinned semantics | Inspected and preserved; change using source |
| Comments and blank lines | Trimmed/comment lines skipped by renderer | Exact original bytes retained | Outside patches unchanged; row moves leave intervening comments in place |
| Unknown inside a block | Ignored line or partially parsed attribute | Owner and statement protected; known semantics still fully checked | Entity structural and attribute rewrites blocked; unrelated safe label may remain editable |
| Unknown outside a block | Often silently ignored | Global unknown dependencies retained | ID/add/delete/reconnect protected; proven independent properties may remain editable |
| Unsupported relationship/direction | Can be silently ignored | Retained as protected, never claimed as supported meaning | Source editing required |

Canonical cardinality notation is endpoint oriented:

| Meaning | At first entity | At second entity |
|---|---|---|
| Exactly one | `\|\|` | `\|\|` |
| Zero or one | `\|o` | `o\|` |
| One or more | `}\|` | `\|{` |
| Zero or more | `}o` | `o{` |

The parser also recognises character permutations of these pairs; an unchanged
cardinality keeps its original bytes. Reverse explicitly swaps endpoint IDs and
cardinalities together, retaining each entity's original cardinality.

## Operation capabilities

| Target | Supported | Protected limitations |
|---|---|---|
| Entity | Add; display label; ID and all recognised references; implicit-to-explicit; duplicate with unique ID; independent delete; inspect incident relationships | Duplicate declarations, unknown references/contents; connected delete requires explicitly removing/reconnecting each relationship |
| Attribute | Add; type/name; PK/FK/UK; comment; duplicate; delete; move up/down | Unknown suffix/statement in owning entity; unsupported quoting/type/name; invalid move; no inferred FK relationship |
| Relationship | Add; label; endpoints; both cardinalities; identifying/non-identifying; delete; duplicate; explicit semantic reverse | Unknown dependencies for structural changes; last-reference removal of implicit entities requires explicit materialisation |
| Source | View immutable original; edit and validate candidate; locate exact occurrence | Invalid or semantically unreconciled candidate cannot commit |

The inspector table shows type, name, keys and comment. Separate native fields edit
the selected occurrence. Entity IDs are semantic IDs; attributes and relationships
have distinct private GUID identities even when every visible field repeats.
Patch planning carries exact statement identity order through insertion, deletion,
reorder and Undo/Redo. No IDs or coordinates are written into Mermaid.

## Complete validation and history

Each operation plans the smallest supported expected-text changes against a captured
revision. Existing patch validation rejects stale text, invalid boundaries and
overlaps. It then reparses the complete candidate, restores planned private
identities, compares the full independent expected semantics, verifies protected
statements byte-exact, renders canonically and verifies native conversion before
committing history. Cancellation, stale presentation and conversion failures cannot
commit. No whole-diagram serialisation is used.

Private history retains up to 100 entries and an 8-million-character aggregate
budget; selection and exact source are restored. Rejected/no-op edits create no
entry. A new successful edit invalidates redo. Source editing requires explicit
validation; pending inspector properties require Update or Discard before selection,
other operations or Apply.

## Actual layout and visual identity

The pinned engine filters self-loops from ordinary layout, emits ordinary routes
in source order, then appends self-loops in source order. `RelationshipOrder`
reconciles that order and verifies each complete tuple. Native conversion verifies
entity IDs/labels, path endpoint IDs, both cardinalities, identifying flags, labels
and endpoint geometry against the same snapshot. Attribute hit rows come from the
verified entity ID, ordered source rows and canonical header/row dimensions;
SVG text never supplies attribute identity.

The displayed drawing uses the same native conversion as preview/export. Neutral
overlays are transparent until selected. Relationship paths/labels, entity headers
and ordered attribute rows are selectable; overlapping routes offer exact occurrences.
The element list and inspector provide keyboard alternatives and native SelectionItem
patterns. Endpoint drag only updates one guide geometry during movement; parsing,
SVG conversion and layout occur after a valid drop. Invalid drop/cancellation releases
capture and retains source/history. Zoom, middle-button PAN, Fit and actual size are
available. Entity manual positioning/palette drop positioning is currently absent;
canonical Mermaider layout always determines positions.

## Document integration

`MermaidDocumentEditSession` retains owning document identity, exact container spans,
AvalonEdit versions and final revision checks. ER routes separately from Flowchart,
Sequence and State. Insert → ER diagram visually… supplies a portable starter.
Standard/tilde fences, recognised DevOps containers, `.mmd`, `.mermaid`, repeated
identical fences, unsaved source and concurrent edits use existing ownership guards.
Apply is one document Undo action. Cancel retains owner source; Save persists portable
Mermaid. The mandatory actual Windows harness must verify Save/restart/reopen and
the complete native workflow before certification can be reported.

## Evidence and remaining work

Initial local evidence: Release compilation 0 warnings/errors; full Core 1,489/1,489;
ER 65/65. A first ER test exposed the pinned leading-comment/header limitation;
the original failing TRX is retained under ignored local evidence and the corrected
test now verifies safe rejection as a separate case.

Hosted ER jobs run normal and actual Windows High Contrast, strict canonical pixel
comparisons, grammar observations, resource cases (10/15, 30/60, 100/150, many rows,
convergence/clusters and long labels), document conflicts and real application UIA.
No rendering resource limit is raised. Native SVG conversion remains non-interruptible;
stale results are discarded. Hosted timings are observations, not hardware-independent
guarantees. Existing Continuous 250 ms and dense-render limitations remain separate.

Final evidence must add the inspected screenshot gallery (Light/Dark/High Contrast
and five viewport sizes), actual assertion counts/performance results, CI URLs,
exact commit, package versions/hashes and independent receipts. The mandatory native
workflow produces self-contained Windows x64 unsigned installer/portable previews,
verifies ER compiled resources beside all three previous editors, checks runtime,
branding, payload identity and rejection controls. A separate installed ER job
reverifies downloaded packages and runs install/upgrade/reinstall authoring.
Historical cross-revision migration remains distinct from same-source two-version
testing. No ordinary-desktop native execution/installation, merge, release or tag
movement is authorised. Readiness is **implementation in progress; not certified**.

## Retained first-run failure

At `684291f`, both ER fidelity jobs in
[run 39063103201](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39063103201)
failed before UIA: WPF `Geometry.Parse` returned a read-only geometry and assigning
the coordinate transform raised `InvalidOperationException`. The correction clones
that geometry before assigning its transform and freezes it afterwards. Original
normal/High Contrast logs are retained; this does not count as a passing acceptance.
The extended source-integrity suite now passes 82 ER cases locally, including alias
forms, entity duplicate/delete, self-to-normal reconnection, native-conversion
rejection, invalid property atomicity and quoted/unlabelled relationship labels.

At `d0d4604`, High Contrast fidelity, grammar/resource observations and document
ownership checks pass. Actual UIA reaches entity/attribute editing, keys, relationship
cardinalities/type, creation, private Undo/Redo and inspector reconnection, then
fails its invalid-drop check. Inspection of the retained native screenshot shows
an unintended discard prompt: the parent canvas intercepted endpoint-handle clicks,
rebuilt the selected relationship controls and prevented capture before Escape.
The correction excludes endpoint-control descendants from parent path selection.
The cancellation check now also requires measured native pointer updates, preventing
a source-only assertion from falsely establishing that a drag actually occurred.
Original [run 39063313555](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39063313555)
logs and captures are retained. Complete UIA remains pending correction validation.
