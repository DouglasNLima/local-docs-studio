# Native workspace and Mermaid interaction stability

Status: implementation and scoped unsigned internal-preview certification complete.
Draft PR 30 targets `develop`. Exact final-head CI and any newer package pins are
recorded at delivery; no merge, public release or ordinary-desktop installation occurs.

The continuation addresses seven reported problems: the main toolbar ICO, shared
document scroll state, difficult element selection, missing endpoint manipulation,
canonical repositioning after palette drops, lost session positions and termination
while dragging an existing node. The separate native .NET 10/WPF application keeps
AvalonEdit, Markdig, Mermaider 0.15.1, SharpVectors and FlowDocument. The static web
application and the excluded PR #22 viewer remain unchanged.

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

Cancelled movement restores both the visible position and whether that position
was explicitly manual. It cannot manufacture an offset for an originally canonical
node. A manually inserted subgraph carries unpositioned descendants with its
translation; individually positioned nodes retain their own absolute centres.
Canonical drawings are flat, so this inheritance is applied explicitly without
introducing a second layout engine. Undo cancels active pointer capture before
changing private history, and a render cache also checks the private history identity.

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

Paged reading positions use the paginator's actual page position, mapped with
forward affinity through literal text. A page can start midway through a paragraph
or at an emphasis boundary; restoring the paragraph's start selects the preceding
page. Tab restoration and Back/Forward therefore use the exact mapped text pointer
before falling back to a semantic block. The bounded opt-in viewport diagnostic
records identities and offsets, never document text.

The hosted Pages test also isolated a separate layout cause: the operation's
visible Cancel button reduced the reader height from 559.45 to 546.45 DIPs. A
section selected during that operation reflowed when the button collapsed,
retaining the page's first position rather than the requested section. Back had
the same problem. The cancellation control now retains its measured layout space
while hidden, keeping the reader height stable, and section navigation commits
after the operation's layout settles. Acceptance compares the actual native
height during and after the operation as well as checking the visible section.
Earlier native text-range experiments were rejected after regression failures;
they are absent from the final implementation and retained in Git/CI history.

The opt-in diagnostic also exposed its own side effect during investigation:
querying `FlowDocument.DocumentPaginator` in Continuous mode switches the document
away from its bottomless formatter. That produced blank synthetic previews in the
instrumented tests. The query is now Pages-only, and the original failures remain
retained. The actual Continuous/Pages workspace scenarios run with diagnostics
enabled so this side effect remains covered. The relevant behaviour is explicit in
the [WPF FlowDocument implementation](https://raw.githubusercontent.com/dotnet/wpf/v10.0.0/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Documents/FlowDocument.cs).

Only the 24-pixel toolbar ICO image is removed. The window, executable, installer,
taskbar and shortcut resources retain their existing branding.

## Measured candidate evidence

The immutable baseline exits with CLR exception code `-532462766` (`0xE0434352`).
This is retained diagnostic evidence, rather than an inferred performance failure.
The original private diagram was unavailable; the concrete synthetic null-transform
crash was reproduced and eliminated in the tested replacement path.

[Completed interaction run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38971676231),
application source `1700b203a612e05158697bf95410f1f48772a3bc`, passed 30 actual drags
on each 20-, 100-, 300- and 24-node nested/long-label fixture in normal and actual
Windows High Contrast: 240 drags and 4,080 delivered pointer moves. Both N0 and N1
were exercised. Reversals, repeated Escape, subsequent selection, process response,
exact private/disk source and ordinary Cancel passed. Separate scenario checks cover
multi-node movement, pending labels, deactivation, active-drag Undo and reconnection.

| Fixture | Worst native press to first update, normal / HC | Worst update method, normal / HC | Update allocation per frame, normal / HC |
|---|---:|---:|---:|
| 20 nodes | 33.909 / 38.417 ms | 3.217 / 3.024 ms | 1,275 / 1,274 bytes |
| 100 nodes | 34.642 / 41.882 ms | 2.885 / 3.417 ms | 1,329 / 1,367 bytes |
| 300 nodes | 31.959 / 41.558 ms | 2.246 / 3.037 ms | 1,718 / 1,813 bytes |
| Nested / long labels | 41.004 / 39.099 ms | 2.882 / 3.067 ms | 1,852 / 1,830 bytes |

[Derived observations](evidence/workspace-mermaid-stability/drag-metrics.json) retain
sample/frame counts and incident connections (at most three in the main fixtures,
six in the nested fixture). First-update timing starts at the native press handler;
it does not measure device delivery or physical display latency. Update-method timing
excludes WPF painting and UIA transport. The allocations in the table are weighted
by observed update frames, including cancellation samples.

Total UI-thread allocations between updates were much larger and grew with diagram
size: approximately 0.53 MB/frame at 20 nodes, 3.17–3.37 MB/frame at 100 and
13.82–15.23 MB/frame at 300. These include other dispatcher/WPF/UIA work and have
not been stack-attributed. They are not represented by the small method-allocation
figure. End-of-case working sets were 228.8–394.2 MiB in separate fixture processes;
this is bounded stress evidence, not a long-duration heap-leak or graphics-paint
certification. There was no crash or unresponsive process in these repeated cases.
The baseline crashes before comparable sustained measurement, so a before/after
speed-up percentage is unsupported. The earlier complete 240-drag measurement at
`6319a37` remains in [its original run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38949043030),
whose later preview-reopening harness failure is retained rather than counted green.

Placement acceptance uses six physical pixels at measured 96 DPI, equivalent to
six DIPs on that runner. The completed application run measured 1.414, 0, 1.414
and 0 pixels at 50%, 100%, 150% and 200% zoom in each appearance. Those measurements
alone did not certify non-zero scrolling. The stronger repeat below explicitly
measures those offsets. Source existence alone never
counts as placement acceptance. Zoom percentages are not OS DPI certification.

## Source-preserving reconnection

For example, reconnecting the destination of `A -->|Next| B` to existing node C uses
`UpdateMermaidConnection` against the selected private edge ID. The original endpoint
declarations remain present; the service may append `A -->|Next| C` after those
existing declarations to retain Mermaid's first-occurrence label/shape behaviour.
Comments, Unicode, line endings, classes and unrelated statements are retained.
The operation validates before one private Undo entry; Apply still performs one
owning-document transaction. Invalid drops have no source operation or history entry.

For an existing dotted parallel connection, the Core tests reconnect only the second
private identity while leaving the first `A -.->|Next| B` unchanged. The changed edge
retains `Next` and `Dotted`; the original `A[Start]`, `B[Review]`, `C[Alternative]`,
Unicode comment, `classDef`, class assignment and LF/CRLF convention survive. Private
Undo restores the exact input bytes and Redo restores the same selected edge identity.
The native harness separately performs an actual drag on the second identical edge.
Protected chained source stays exact and receives no private Undo entry on rejection.

## Inspected native evidence

All images below come from synthetic documents on disposable hosted Windows at 96
DPI. The baseline is immutable `14b3d4f`; initial application images are from source
`1700b203a612e05158697bf95410f1f48772a3bc`. Scrolled drop/marker and five-size
images are from harness revision `df71d00f9dd83532106fd43a1004722d84b27238`;
its Native/Core product sources are unchanged from that implementation revision. Image inspection found and corrected
detached subgraph titles, capture destroyed by recreated endpoint buttons, invisible
High Contrast handles and a viewport shift after zoomed insertion. Rejected captures
and original failures remain in the downloaded CI evidence.

| View | Actual Windows evidence |
|---|---|
| Toolbar before / after; title-bar icon retained | [Before](evidence/workspace-mermaid-stability/toolbar-before.png), [after](evidence/workspace-mermaid-stability/toolbar-after.png) |
| First-open Pages / returning reading position | [New document](evidence/workspace-mermaid-stability/pages-new-document.png), [restored document](evidence/workspace-mermaid-stability/pages-restored.png) |
| Existing node in active drag / multiple session positions | [Active drag](evidence/workspace-mermaid-stability/drag-active-20.png), [manual positions](evidence/workspace-mermaid-stability/multiple-session-positions.png) |
| Selected connection / endpoint drag / canonical result | [Handles](evidence/workspace-mermaid-stability/connection-endpoints.png), [in progress](evidence/workspace-mermaid-stability/reconnecting-destination.png), [result](evidence/workspace-mermaid-stability/reconnection-result.png) |
| Protected inspector / actual Windows High Contrast | [Read-only inspection](evidence/workspace-mermaid-stability/protected-inspection.png), [High Contrast handles](evidence/workspace-mermaid-stability/high-contrast-endpoints.png) |
| Translated subgraph / Auto layout / Dark | [Group and title](evidence/workspace-mermaid-stability/positioned-subgraph-membership.png), [canonical reset](evidence/workspace-mermaid-stability/auto-layout-reset.png), [Dark session](evidence/workspace-mermaid-stability/dark-session-layout.png) |

The first 1920×1080 stress-harness capture was rejected during visual inspection:
resizing a centred window left its right/bottom outside the desktop. The harness now
moves it to the desktop origin and requires complete desktop, Apply and taskbar
containment before capture. Earlier percentage measurements also exposed PAN inputs
clamped at zero; final coordinate checks explicitly require non-zero vertical offsets,
an effective native PAN and a non-zero horizontal offset in the narrow 200% case.

The [stronger repeat](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38975164864)
at `df71d00f9dd83532106fd43a1004722d84b27238` passes another 240 actual drags,
4,080 moves, 57 normal and 52 actual HC scenario assertions and all four workspace
contexts per appearance. The [recorded placement results](evidence/workspace-mermaid-stability/scrolled-placement-results.json)
show effective PAN and approximately 35.4–37.0% vertical scroll at every zoom; the
narrow 200% case also starts at 44.72% horizontal scroll. Both appearances measure
1.414 / 0 / 1.414 / 0 physical pixels of error at 50 / 100 / 150 / 200% zoom.
The changing horizontal percentage after insertion reflects a changed content extent;
the physical target centre and observed camera offset remain correct. This repeat's
worst update method observation is 5.046 ms, separately from the earlier table.

The complete 880×600, 1024×768, 1280×800, 1440×900 and 1920×1080 captures were
inspected. Inspector scrolling and native toolbar overflow retain access to controls
at the smallest size; Apply remains visible. The complete 1920×1080 light and actual
HC captures include their right edge and footer. Native drop markers are visible.

| Stronger visual acceptance | Evidence |
|---|---|
| Insertion marker / scrolled drop at 200% | [Marker](evidence/workspace-mermaid-stability/palette-marker.png), [placed node](evidence/workspace-mermaid-stability/scrolled-drop.png) |
| Actual HC marker / complete desktop | [HC marker](evidence/workspace-mermaid-stability/high-contrast-palette-marker.png), [HC 1920×1080](evidence/workspace-mermaid-stability/high-contrast-full-desktop.png) |
| Complete requested window sizes | [880×600](evidence/workspace-mermaid-stability/editor-880x600.png), [1024×768](evidence/workspace-mermaid-stability/editor-1024x768.png), [1280×800](evidence/workspace-mermaid-stability/editor-1280x800.png), [1440×900](evidence/workspace-mermaid-stability/editor-1440x900.png), [1920×1080](evidence/workspace-mermaid-stability/editor-1920x1080.png) |

## Regression and source provenance

Implementation source is `1700b203a612e05158697bf95410f1f48772a3bc`.
The PR merge checkout is `ab0f2dc43d0db49a3868d9cb30d1301901b58bbc`;
both resolve to tree `8e31ddd5eee6454f79259eab15970be491592289`. Subsequent changes
extend only the synthetic validation harness and this documentation/evidence. They
require their own final-HEAD workflow results; this report does not transfer an older
run's success to newer application changes. The branch remains
`feature/native-workspace-mermaid-interaction-stability` and
[Draft PR 30](https://github.com/DouglasNLima/local-docs-studio/pull/30) targets `develop`.

| Mandatory validation | Application-revision evidence |
|---|---|
| Release build / full Core / transformation cases | [Native pipeline](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38971676302): 1,234 Core passes, including exact parallel reconnection, LF/CRLF, Unicode, protected rejection, private history and independent preview anchors |
| Native rendering/export corpus | Same pipeline: 70 native diagram/export cases, zero failures, and 12 Chromium/Edge static Docs Site export checks |
| Canonical fidelity | [Native UX](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38971676214): 291 assertions; exact normalised preview/canvas pixels, including translated-title identities |
| Normal / actual High Contrast authoring | Same UX run: 122 / 118 assertions, including protected inspection and actual second-identical-edge endpoint dragging |
| Repeated dragging / selection / reconnection | [Interactions](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38971676231): 240 drags, 4,080 moves and 52 normal / 47 HC scenario assertions |
| Scroll / navigation | Same interaction run: four Continuous/Pages × sync contexts in each normal/HC process; independent Pages-first job also passes all four |
| Desktop UIA / persisted workspace and export | Native pipeline: 75 UIA groups in each of four contexts (build output, initial install, upgrade, reinstall); 13 persisted workspace/export checks, plus separate UX controls |
| Chromium / Edge | [Web regression](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38971676251): 134 + 134 passes |
| Static integrity | Same web run: 50 modules, 57 shell assets, 150 vendor assets and 56 runtime external-dependency scans |
| Installer lifecycle / package rejection controls | Native pipeline: five wizard checks, seven rejection controls and 20 lifecycle checks; downloaded packages independently verified below |

The original unchanged Core timeout is retained as a baseline failure; subsequent
full suites pass without increasing its timeout. Existing UIA and source-preservation
tests remain in place. New native assertions also cover the fresh Pages anchor,
forward affinity at a formatted text boundary and zero-height-change anchor navigation.
The help guide retains its original 14-topic contract.

Hosted discoveries were retained while their causes were corrected: stale preview
actions after document Undo; assumptions about source-order position after reconnection;
endpoint capture destroyed by recreated buttons; Follow caret overriding history;
page starts mapped to the preceding paragraph; operation-footer reflow; and an active
paginator query in the Continuous diagnostic. The export browser harness encountered
Python first-use initialisation inside its unchanged 15-second readiness window.
CI now imports that test-only runtime before the deadline. No timeout or application
architecture was changed to conceal a failure.

## Packages and lifecycle

Unsigned self-contained x64 preview `0.1.1-preview.581` is built and independently
verified. The lifecycle upgrades `0.1.0-preview.581` to that version, then uninstalls,
reinstalls and restores actual saved/unsaved session state. The verified source is
PR merge `ab0f2dc43d0db49a3868d9cb30d1301901b58bbc`, whose complete tree matches
application commit `1700b203a612e05158697bf95410f1f48772a3bc`. Metadata declares
`sourceDirty: false`, `pull-request-merge-checkout`, .NET 10 WPF and `NotSigned`.

| Package | SHA-256 |
|---|---|
| `LensDocsStudio.Native-0.1.1-preview.581-win-x64-Setup.exe` | `ccfe7365cab6efff8747e92d6dc3e5a427b784a6e375b2000890635659393f76` |
| `LensDocsStudio.Native-0.1.1-preview.581-win-x64.zip` | `09ea10a591b7322aa5a6e046a90cdce75c010bef2097a25d0cff9796db980808` |
| Lifecycle initial `0.1.0-preview.581` installer | `1a26f032fce908e956d5cd6314e1d8dda90eed8c2797e9e3670e2aa0af4f8dad` |
| Lifecycle initial `0.1.0-preview.581` ZIP | `ccc272e997d644050a86f6017d3b1e1054b99cd957b0e85d39155785b66a2e22` |

[Hosted package artefact](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38971676302/artifacts/12078955537)
contains installers, ZIPs, version metadata, SHA256SUMS and the original verification
and lifecycle evidence (14-day CI retention). Independent local read-only verification
was repeated on both downloaded versions with explicit source and both hash pins.
The [retained verification report](evidence/workspace-mermaid-stability/package-verification-581.json)
records matching app/Core identities, included runtime, original executable/installer
icon frames and native Mermaid resources, no browser runtime, excluded experiment or
private/development payload, and `executionPerformed: false`. No native UI or installer
was launched on the ordinary desktop.

The [20-check lifecycle report](evidence/workspace-mermaid-stability/installer-lifecycle-581.json)
confirms all 528 installed payload files match portable bytes. Twenty-two existing
native state files and workspace source survive upgrade, uninstall, reinstall and
final uninstall; actual tabs, settings, dirty saved and untitled drafts are restored.
Interrupted-save recovery, shortcuts, file branding, existing shell registration and
Markdown defaults pass. This is a same-source, distinct-version upgrade.
The [seven negative controls](evidence/workspace-mermaid-stability/package-rejection-controls.json)
reject wrong source, version, independent hash, dirty metadata, ZIP tampering,
excluded-viewer payload even with recomputed checksum, and a missing editor assembly
even with recomputed checksum. The original package remains unchanged.
The [persisted workspace/export result](evidence/workspace-mermaid-stability/persisted-workspace-export-results.json)
retains the 13-check service/artefact workflow separately from interactive UIA.

Later harness/documentation revisions generate their own CI package identities.
Their final source/hash pins belong to the delivery verification report; this immutable
implementation certificate continues to identify exactly the package bytes above.
The [PR checks](https://github.com/DouglasNLima/local-docs-studio/pull/30/checks)
expose the final revision's independent workflows. No older run is counted as a pass
for a later application change.

## Readiness and limits

**Verdict: ready for a scoped unsigned internal preview, with the explicit limits below.**
The concrete synthetic P0 crash is reproduced, diagnosed and corrected; repeated
pointer input, the stronger non-zero coordinate checks, source integrity, workspace
navigation, visual inspection, full regression and independent package/lifecycle
verification pass at the pinned revisions. Final documentation-only HEAD workflows
are inspected separately before hand-off and their exact URLs accompany delivery.

| Classification | Finding |
|---|---|
| Implemented and verified | Toolbar correction; editable/protected selection; exact endpoint reconnection and rejection; document-owned positions, anchors and Back/Forward; canonical fidelity; source/Undo/Cancel protections; package/lifecycle checks |
| Implemented with bounded limitations | Efficient drag/drop and session layout at measured 96 DPI, all requested sizes and diagram zooms; temporary connection guides; source-authoritative routing and supported editing budgets |
| Not reproduced but stress-tested | The precise unavailable private diagram/failure. A concrete equivalent synthetic null-transform crash is reproduced and its replacement path survives repeated input |
| Blocked | Unsigned in-app updater execution remains blocked by the existing trust policy; production signing is outside scope |
| Not completed | The separate pre-existing Continuous 250 ms first-layout work and unrestricted dense rendering remain unresolved; neither is represented as a new pointer-stability pass |
| Not certified | Higher OS DPI, physical-device/display latency, long-duration heap/paint profiling, assistive-technology user study and a separate SDK-free clean machine |

All achievable scope of this interaction continuation is implemented and evidenced.
The limits below remain part of the internal-preview verdict.

Measured 96 DPI is the only OS scaling certified by the hosted environment. No suitable
authorised disposable 125%, 150% or 200% OS-DPI environment was available. Actual
Windows High Contrast was used; choosing a dark appearance is not counted as HC.
Native accessible names, selection patterns, keyboard alternatives and focus checks
are exercised, but an assistive-technology user study is not certified.

The 300-node editable fixture uses 181 connections to remain within the 500-element
budget; it is not an unrestricted dense-graph editing claim. Protected dense cases
retain their existing source-only/protected rejection and source-integrity coverage.
In the separate performance harness, the 20-node/190-edge dense case rejected editing
and its render was cancelled after approximately 7.13 seconds, with about 2.39 GB of
process allocation recorded during the attempt. That case is not a successful dense
render or editable pointer test. Ordinary protected chained/multiline diagrams have
separate successful native inspection coverage. Synchronous renderer cancellation
limits remain. Session centres and dashed connection
guides are intentionally private to the editing session; saved Mermaid has canonical
layout on reopening and no proprietary coordinates.

The small drag-update timings do not certify end-to-end paint latency, total dispatcher
allocation or long-duration heap behaviour. The separate historical Continuous 250 ms
first-layout failures remain unresolved in
[performance and responsiveness](performance-and-responsiveness.md), including the
mixed/large-document and details-construction limitations. Green compatibility checks
do not convert those observations into performance passes. This completed run's
[retained observations](evidence/workspace-mermaid-stability/continuous-observations.json)
record list 1,297.07 ms, table 257.98 ms and mixed 666.16 ms against 250 ms: all
three remain **FAIL**. Source, complete text and target visibility pass separately.

Production signing, trusted in-app update execution and a separate SDK-free clean-machine
image remain outside this internal-preview certification. Existing historical migration
fixtures/workflows are retained; the integration-only twelve cross-revision cases are
historical reference evidence, not rerun by this feature branch's conditional job.
No merge, public release or ordinary-desktop installation has been performed.
