# Native workspace and Mermaid interaction stability

Status: implementation and certification in progress. Draft PR targets `develop`;
no merge, public release or installation on the ordinary desktop is authorised.

## Baseline and reproduced crash

Remote `develop` was fetched and verified at
`14b3d4fc605c965a35107cceb0cf20a60226f1ca`, the merge of PR #29.
The isolated branch is `feature/native-workspace-mermaid-interaction-stability`.

[Original hosted run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38945600830)
ran a new pointer harness against that immutable source and the unchanged native
application in the first harness commit. A synthetic 20-node vertical Flowchart
with decisions, multiline labels, labelled and unlabelled branches and backward
connections terminated during actual native node dragging. Windows recorded an
unhandled `System.ArgumentException: Cannot add null to the collection` from
`TransformCollection.Add`, through `UpdateSessionConnections()` line 338 and the
WPF mouse-move handler. The original source, pre-drag screenshot and complete
managed Windows event stack are retained in
[`evidence/workspace-mermaid-stability`](evidence/workspace-mermaid-stability/).
This reproduces a concrete crash mechanism on a representative synthetic fixture;
it does not establish that the unavailable private diagram had no other failure.

The old pointer path flattened and cloned every edge on every move. Its unlabelled
hit-target reconstruction added `geometry.Transform` to a transform collection;
a flattened path can have a null transform. This is a correctness failure, beyond
the additional cost of processing unrelated connections. The first harness also
failed while reading a disposed process wrapper in its error handler. That separate
diagnostic defect is retained in the original CI log and corrected in the harness.

The local unchanged Release build passed. The original full Core run produced
1,228 passes and one pre-existing sequence-render regex timeout, retained under
`artifacts/interactions/local-baseline/baseline.trx`. It is not counted as a pass.

## Interaction architecture

The canonical Mermaider drawing remains authoritative. A native drawing host fixes
its original coordinate origin rather than renormalising changing DrawingImage
bounds during movement. Node groups receive UI-owned translation transforms.
Pointer updates coalesce at WPF render priority; cached incident-edge indices
limit work to moved nodes and affected connections. Canonical paths are flattened
once when a render commits. Temporary dashed line guides use cached endpoints;
Auto layout restores the exact canonical drawing. No parsing, Mermaider rendering,
SharpVectors conversion or entire-canvas rebuild occurs per pointer move.

Pressed, threshold, dragging, completion and cancellation share capture cleanup.
Escape, lost capture, deactivation, rendering, theme change and closure cancel
temporary movement. Per-drag update timing and UI-thread allocation observations
are exposed as status HelpText for the hosted harness; elapsed round trips are
reported separately because they include mouse pacing and UIA transport.

Drop positions use `GetPosition(DiagramCanvas)` in device-independent coordinates.
WPF accounts for DPI, layout zoom, scrolling and centred canvas translation once.
After validated insertion the new semantic ID receives an absolute session centre
when its canonical render commits. Palette clicks use the visible viewport centre.
Only an explicitly selected, verified containing subgraph authorises membership.

Session centres survive supported edits and theme rerenders. Layout snapshots are
keyed by distinct private history identities rather than source text: identical
text at different Undo steps cannot transfer a position to a reused node ID.
Unreachable history branches are pruned alongside the bounded Core history. Explicit safe renames
transfer a centre. Removal prunes it; private history stores bounded layout snapshots
and restores them on Undo/Redo. Arbitrary source validation conservatively retains
unchanged node identities. Auto layout clears session layout history. Apply writes
only portable Mermaid; reopening restores canonical layout.

Pointer selection uses canonical routed paths with a 12-DIP target and the actual
label rectangle. Transparent path bounding rectangles do not intercept input.
Canvas selection no longer combines competing click and toggle mutations. Protected
inspection uses independently validated canonical metadata and ordered connection
ordinals, without creating source spans or transformation permission. Its inspector
is explicitly read-only. Parallel paths retain distinct list entries.

Selected editable connections expose named source/destination handles. Valid drops
use `UpdateMermaidConnection` with the exact private edge ID, original label and type;
invalid/ambiguous drops cancel before any source operation or Undo entry. Existing
endpoint inspector controls remain the keyboard alternative. Routing stays canonical.

## Workspace and toolbar

Viewport state belongs to the document. Navigation history records the preview
reading anchor independently of caret and editor scroll position. Restorations
retain immutable targets across rendering; explicit section navigation supersedes
queued tab restoration. Stale preview links reject activation. Binding captures outgoing state, cancels
obsolete work, detaches old render identity and restores the incoming editor state.
The preview restores that document's source anchor, including explicit source zero
for first open, behind document and generation guards. Synchronisation is suppressed
during that restoration. Back/Forward and explicit anchors retain their navigation
operations. Both native Continuous and Pages modes use the existing navigation API.

Only the 24-pixel toolbar ICO image is removed. The window, executable, installer,
taskbar and shortcut resources retain their existing branding.

## Measured candidate evidence

The immutable baseline exits with CLR exception code `-532462766` (`0xE0434352`).
This is retained diagnostic evidence, rather than an inferred performance failure.

[Candidate pointer run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38949043030),
source `6319a37848833a74dd05244d5eb2d16f68549a70`, completed 30 drags each on
20-, 100-, 300- and 24-node nested/long-label diagrams in normal and actual Windows
High Contrast: 240 drags, exact private/disk source and process liveness. The whole
run remained red because of a later harness preview-reopening observation; it is
not presented as completed end-to-end acceptance.

| Fixture | Normal worst update | High Contrast worst update | Normal / HC update allocation per frame |
|---|---:|---:|---:|
| 20 nodes | 5.086 ms | 4.209 ms | 1,711 / 1,708 bytes |
| 100 nodes | 4.582 ms | 4.876 ms | 1,843 / 1,825 bytes |
| 300 nodes | 3.867 ms | 3.938 ms | 2,042 / 2,042 bytes |
| Nested / long labels | 4.782 ms | 12.037 ms | 2,417 / 2,411 bytes |

These timings cover the native update method, not input-to-display latency. Allocations
inside that method are distinguished from all dispatcher allocations between frames.
End-of-case working set spans approximately 229–399 MiB across these cold/warm cases;
this is not a long-duration heap-leak certification. The baseline crashes before a
comparable sustained measurement, so a before/after speed-up percentage is unsupported.
Subsequent instrumentation records native press-to-first-update and incident-edge
counts separately and is enabled only for the synthetic interaction harness.

Measured placement at 96 DPI uses a six-physical-pixel tolerance (six DIPs at this
DPI). In that candidate the 50%, 100%, 150% and 200% cases measured errors of 0,
0, approximately 1.414 and 1 pixel in High Contrast. Normal placement also passed
all four zooms. Tests exercise native PAN and asynchronous canonical rendering;
source existence alone does not count as placement acceptance.

Multi-node pointer movement, three separate insertions, label rerenders, safe rename,
private Undo/Redo, themes, editable endpoint reconnection, invalid drops, deactivation,
Escape, all five requested window sizes, Apply and one document Undo have scoped
Windows evidence. Source-order changes made by the existing reconnection service
are checked by exact semantic/private edge identity, never by assuming the edited
edge remains the first statement. Separate native authoring coverage exercises an
identical parallel edge and protected read-only inspection.

The native regression run for source `6bf4ceb` passed 122 normal and 118 actual
High Contrast authoring assertions, plus 281 canonical-fidelity assertions. Its
package job was cancelled when a newer source superseded it, so no package readiness
is inferred. Original CI runs and downloaded evidence remain under ignored
`artifacts/interactions/`; baseline crash evidence is also committed beside this report.

## Source-preserving reconnection

For example, reconnecting the destination of `A -->|Next| B` to existing node C uses
`UpdateMermaidConnection` against the selected private edge ID. The original endpoint
declarations remain present; the service may append `A -->|Next| C` after those
existing declarations to retain Mermaid's first-occurrence label/shape behaviour.
Comments, Unicode, line endings, classes and unrelated statements are retained.
The operation validates before one private Undo entry; Apply still performs one
owning-document transaction. Invalid drops have no source operation or history entry.

## Certification ledger

Implemented, pending verification: bounded dragging; drop positions; session layout;
editable/protected selection; endpoint reconnection; document viewport ownership;
toolbar correction. No overall readiness claim is made until candidate Windows
pointer tests, screenshots, regression jobs and independent package verification pass.

The current local Release build and all 1,234 Core tests pass, including distinct
identical-source history steps, source-preserving parallel reconnection and separate
preview-history anchors. The original regex timeout remains retained as a baseline
failure. The help guide retains its original 14-topic contract.

Pending: final revision and CI links, performance comparison, placement tolerance
results, actual High Contrast and window-size evidence, Core/render/browser/UIA
totals, installer/ZIP versions and SHA-256, lifecycle and rejection controls.

Higher-DPI certification requires a suitable disposable authorised environment.
Renderer cancellation limitations and the separate historical Continuous 250 ms
first-layout issue remain documented in `performance-and-responsiveness.md`.
