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

Session centres survive supported edits and theme rerenders. Explicit safe renames
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

Viewport state belongs to the document. Binding captures outgoing state, cancels
obsolete work, detaches old render identity and restores the incoming editor state.
The preview restores that document's source anchor, including explicit source zero
for first open, behind document and generation guards. Synchronisation is suppressed
during that restoration. Back/Forward and explicit anchors retain their navigation
operations. Both native Continuous and Pages modes use the existing navigation API.

Only the 24-pixel toolbar ICO image is removed. The window, executable, installer,
taskbar and shortcut resources retain their existing branding.

## Certification ledger

Implemented, pending verification: bounded dragging; drop positions; session layout;
editable/protected selection; endpoint reconnection; document viewport ownership;
toolbar correction. No overall readiness claim is made until candidate Windows
pointer tests, screenshots, regression jobs and independent package verification pass.

Pending: final revision and CI links, performance comparison, placement tolerance
results, actual High Contrast and window-size evidence, Core/render/browser/UIA
totals, installer/ZIP versions and SHA-256, lifecycle and rejection controls.

Higher-DPI certification requires a suitable disposable authorised environment.
Renderer cancellation limitations and the separate historical Continuous 250 ms
first-layout issue remain documented in `performance-and-responsiveness.md`.
