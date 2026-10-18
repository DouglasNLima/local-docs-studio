# Native Mermaid suite UX and performance refinement

Status: **in progress; not certified**. This report records evidence as it is
produced. An unexecuted gate is not a pass. Production readiness and universal
Mermaid compatibility are outside this internal preview.

## Consolidated baseline

Remote `develop` was fetched on 18 October 2026 and resolves to
`6a94dd3672f95a267402e2218935d064a6ee6401`. All five editor windows, dedicated
source adapters, presentation adapters and validation suites are present.

- [Consolidated native run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39103278298):
  completed successfully, including the main native/package lifecycle and all
  three separate installed authoring jobs. The integration-only historical-upgrade
  job was skipped by its existing branch condition; it is not counted as passed.
- [Consolidated browser/static run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39103278332):
  completed successfully, Chromium and Edge; deployment correctly skipped.
- [Fresh baseline UX capture](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39104215598):
  dispatched against unchanged `develop` before any interface implementation.
  Includes the existing five-family canonical/UIA suites and actual Windows High
  Contrast. completed successfully. The five 880 � 600 family captures were manually
  inspected; full baseline state/size coverage remains to be catalogued.

No historical feature-PR result substitutes for consolidated post-merge checks.
The ordinary desktop installation and primary `main` checkout remain untouched.

## Five-editor audit before interface changes

This matrix is the source-level audit of the consolidated revision. Hosted
interaction/screenshot acceptance is recorded separately when available.

| Area | Flowchart Advanced | Sequence | State | ER | Class |
|---|---|---|---|---|---|
| Navigation initial/minimum width | 180/156 | 180/152 | 180/150 | 180/150 | 180/150 |
| Navigation organisation | Nodes/connections/subgraphs with icons | Separate participants and ordered interactions | Flat occurrence list with scoped descriptions | Entity followed by attributes; relationships | Class followed by members; namespaces/notes/relationships |
| Long names | Ellipsis and tooltip on content | Default display member | Default display member | Default display member | Default display member including long signatures |
| Inspector identity | Fixed above property scroll | Inside property scroll | Inside property scroll | Inside named property scroll | Inside named property scroll |
| Update/reset | Fixed inspector heading | Within scroll | Within scroll | Within scroll | Within scroll |
| Toolbar | Predominantly existing vector icons | Mixed icon/text | Mixed icon/text | Mixed icon/text | Mixed icon/text |
| Canonical placement command | Clears session placement | No placement command | Restores temporary positions | Fits canonical positions | Fits canonical positions |
| Source initial state | Expanded; resizable row | Collapsed; fixed 180 body | Collapsed; fixed 170 body | Collapsed; fixed 170 body | Collapsed; fixed 170 body |
| Source operations | Validate/copy | Validate/original | Validate/original | Validate/original | Validate/original |
| Viewport | Pointer zoom, pan, session placement | Pointer zoom, middle pan | Pointer zoom, pan, session placement | Pointer zoom, pan; canonical positions | Pointer zoom, pan; canonical positions |
| Pending protection | Existing source/private property guards | Existing source/private property guards | Existing source/private property guards | Existing source/private property guards | Existing source/private property guards |

Intentional differences: Sequence chronology and activations; Flowchart multi-node
selection and partial editing; State scoped markers/composites; ER attributes and
cardinalities; Class attributes/methods/namespaces. Their semantic models and exact
occurrence identities must remain separate.

Accidental inconsistencies to address: disappearing inspector context, unnamed
splitters, incomplete common toolbar names, narrow initial navigation, missing
full-description navigation tooltips, fixed source heights, and misleading
canonical placement wording.

Hosted state audit must cover initial/selected/long-name/long-form/protected/
pending/error/source expanded/source collapsed/Fit/Actual Size/zoomed/inserted/
small-window/High Contrast for every family. Required sizes are 880 × 600,
1024 × 768, 1280 × 800, 1440 × 900 and 1920 × 1080. No visual acceptance is
claimed from XAML inspection alone.

## Performance investigation

Inspection found duplicate SVG sanitisation in the ER and Class cold presentation
paths: `Create` calls `Prepare` for correspondence, then `Render` calls `Prepare`
again on a cache miss. Flowchart/State already convert their prepared SVG with
runtime identities; Sequence validates correspondence before ordinary conversion.
Measurements must distinguish those different paths before selecting changes.

Existing limits remain 100,000 source characters, 500 renderer elements and a
three-second renderer deadline. The ER 100-entity/150-relationship case remains a
resource diagnostic until a complete render and interaction actually succeeds.
Class 100/150 is measured independently. Non-interruptible SharpVectors calls
must finish safely and obsolete results must never be installed.

## Delivery evidence still required

Shared resources/toolbars; navigation; persistent inspector and scroll behaviour;
source and viewport consistency; normal/dark/actual High Contrast accessibility;
manually inspected before/after hosted screenshots; phase-separated comparable
performance samples; memory/open-close/cancellation checks; full Core/source/
fidelity/UIA/pointer/workspace/native export/browser/static regression; installed
five-family lifecycle and negative controls; independently verified exact-source
unsigned installer/portable ZIP identities and SHA-256 hashes; final-HEAD CI URLs.

Current verdict: **NOT CERTIFIED**. No optimisation or speed-up is claimed yet.

## First implementation and local checks

Shared neutral navigation styles now retain full accessible names/tooltips and
exact SelectionItem identities. Flowchart has node/connection/subgraph sections;
Class and ER retain parent/member order within family sections; State retains
scoped composite hierarchy; Sequence retains separate participants and ordered
interactions. Navigation starts at 220 pixels with a 180-pixel minimum, with
keyboard-accessible splitters. Domain adapters and source transformations are
unchanged.

The four newer editors now have fixed inspector identity/status and Update/Reset
actions outside the property scroll. Different identities reset the property
scroll; the same identity preserves it. Common commands use existing vector
icons with stable IDs and explicit accessible names. Source rows resize, expand
and collapse without recreating drafts. Off-screen navigation targets and inserted
elements are revealed without changing zoom. Ctrl+wheel zoom is pointer-centred.
Class/ER now cancel obsolete render waits as well as rejecting stale generations.

The first local Release build passes without warnings; all 1,645 Core tests pass,
none skipped. Static integrity passes: 50 modules, 57 shell assets, 150 vendor
assets and 56 dependency scans. Eleven focused canonical cache checks pass.
Hosted candidate UIA/fidelity, screenshots and installed acceptance are pending.

## Initial same-host performance evidence

The original baseline assemblies were preserved before interface changes. The
same compiled validation binary ran against baseline and candidate assemblies on
Windows 10.0.26300, .NET 10.0.12, x64, 16 logical processors. Each fixture has
one first execution and six repeated executions with a new editor SVG service.
All 70 per-revision presentation drawings were compared with independently
converted canonical PNG bytes outside the timed stage.

The optimisation caches only frozen canonical drawings, bounded to eight entries
and two million source/prepared-SVG characters. Keys contain exact source, theme,
complete prepared SVG and runtime-identity options. Renderer/converter identity is
pinned by the process. Semantic correspondence still executes for every model.
Mutable session drawings, source identities and windows are never cached.
SharpVectors remains non-interruptible; queued work is cancellable and cancelled
results are rejected. Renderer limits are unchanged.

The table shows median complete presentation time over the six repeated samples.
It measures warm exact-source conversion reuse, not cold first-window opening or
UIA input transport. These preliminary local measurements do not certify final
HEAD or hardware-independent latency. Raw receipts include first samples,
allocations, working sets, source hashes and exact assembly/harness hashes.

| Family | Scenario | Before (ms) | After (ms) | Repeated samples |
|---|---|---:|---:|---:|
| Flowchart | Smaller | 332.97 | 13.81 | 6 |
| Flowchart | Larger | 919.54 | 74.77 | 6 |
| Sequence | Smaller | 276.46 | 7.64 | 6 |
| Sequence | Larger | 1210.59 | 62.18 | 6 |
| State | Smaller | 161.37 | 8.10 | 6 |
| State | Larger | 580.75 | 70.85 | 6 |
| ER | Smaller | 231.33 | 9.67 | 6 |
| ER | Larger | 825.24 | 34.28 | 6 |
| Class | Smaller | 165.57 | 10.64 | 6 |
| Class | Larger | 2491.44 | 130.14 | 6 |

ER 100/150 again exceeded the unchanged renderer deadline; editor interaction
was not completed and this remains a resource diagnostic. The Class 100/150
case completed independently. Main-thread window presentation, interaction,
retained-memory and higher-DPI certification remain outstanding.

## First hosted candidate: `b2a79d9`

[UX run 39105334937](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39105334937)
passes the five-family canonical comparisons, Sequence/State normal and actual
High Contrast authoring, and the identical-binary comparative performance job.
This revision is **not certified**: Class/ER UIA and the new workspace suite fail.
Their original receipts are retained in this report's evidence directory.

Class/ER stop at the old assertion that Update belongs inside the property
ScrollViewer. The actual hosted screenshot shows Update visibly fixed below it.
The check is being adapted to require containment in the fixed inspector panel
for Update, retain scroll-body containment for every actual field, and additionally
require the selection heading to remain visible. No fields/assertions are removed.

The new Flowchart workspace fixture incorrectly waited for Apply on an unchanged
draft. Its existing contract requires a real validated private change. The check
now waits for enabled visual authoring and requires Reset to restore the original
Apply state. The original failure screenshot shows a ready editable canvas with
an appropriately disabled Apply. This is a harness correction, not a claimed
product performance fix.

The hosted performance job uses Windows 10.0.26100 / .NET 10.0.13, four logical
processors. Repeated larger-scenario presentation medians before/after are
Flowchart 455.81/50.93 ms, Sequence 500.59/27.82 ms, State 280.84/18.15 ms,
ER 284.23/15.00 ms and Class 973.12/52.15 ms. Every sample retains the canonical
pixel check. Raw paired receipts share the same harness SHA-256. These results
belong to this revision and runner, not the final package or arbitrary hardware.

Follow-up work adds real WPF retained-window/selection/heartbeat diagnostics,
explicit cleanup after render completion, cancelled queued conversion tests,
fixed full-selection descriptions, and Flowchart/Sequence installed authoring
using the existing package provenance and complete payload-parity assertions.

## Expanded lifecycle and keyboard checks at `f0bb2c5`

[Run 39106077251](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39106077251)
retains further failures. Normal managed WPF lifecycle checks complete six real
open/close cycles in each family, with zero retained windows after diagnostic
collection and zero canonical conversions during 20 selections per cycle. Actual
High Contrast releases Flowchart, Sequence, State and ER windows but retains one
Class window at the immediate collection point. This is unresolved evidence, not
a certified leak fix. The strict zero-retained-window assertion remains.

The new keyboard test identifies Flowchart's window-level Home shortcut consuming
Home before the focused navigation list can use it. The correction preserves Home
reset on the canvas and native Home/End/arrow navigation within lists/selectors.
The fixed-inspector containment check also exposes that a plain DockPanel does
not create a UIA control peer. A shared native DockPanel subclass now supplies an
explicit Group peer and the existing inspector automation identifiers.

Follow-up diagnostics wait for actual asynchronous window cleanup completion
before collection. Deferred inspector scroll callbacks are bounded to the latest
selection and aborted on unload. No production garbage collection or relaxed
retention assertion is introduced. These changes still require fresh hosted
normal and actual High Contrast execution.

Local browser regression completes all 270 Chromium/Edge cases; static validation
remains unchanged and passing. Sequence insertion now selects the newly inserted
exact statement occurrence, with a focused native regression. All five toolbars
include source visibility and original-source inspection is available in Flowchart
as well as the four newer editors.

## Verified intermediate revision `4e9fa58` and phase instrumentation

[UX run 39106690084](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39106690084)
passes every job: five-family canonical fidelity and authoring in normal/actual
High Contrast, shared workspace, retained-window lifecycle, comparative performance
and historical diagnostic controls. This is an intermediate revision; subsequent
trace/long-form refinements require fresh certification.

Both appearances release all 30 tested windows after their real asynchronous
cleanup completes and controlled diagnostic collection. The earlier immediate
High Contrast collection failure remains retained. Cleanup completion is now an
explicit measurement boundary; deferred inspector/navigation callbacks are
bounded and cancelled on unload. No production GC is used.

The five hosted 880 � 600 High Contrast source-expanded captures were manually
inspected. Fixed headings/actions and footers remain visible. Further review moves
selected Class/ER member fields above the parent member list, wraps long Class
parameters, uses pixel-based virtualised navigation scrolling and verifies full
long-member tooltips. Final screenshots still need all-size inspection.

Opt-in `LensDocsStudio.Mermaid` ActivitySource scopes distinguish source scanning,
semantic validation, actual graph parser/layout/SVG boundaries, sanitisation,
conversion queue waits, SharpVectors Read, WPF freeze/image creation, geometry
correspondence, overlay creation, first native presentation/interaction, selection,
private source validation and cleanup. Every span identifies start/end threads.
Allocated bytes are per-thread and deliberately unavailable when a span changes
threads. Nested inclusive spans must not be added twice. Document text and paths
are never recorded by the phase listener. Normal execution creates no timing
objects or logs without a listener.

Two pinned-library boundaries remain explicit: Mermaider's Sequence pipeline has
no graph layout callback, so its parser/layout/SVG phases remain combined in the
labelled renderer pipeline; SharpVectors Read internally creates DrawingGroups,
while final freeze/image wrapping is measured separately. No parser-only or
converter-internal timing is invented, and neither library is forked.

A new Core regression confirms active tracing preserves exact CRLF/Unicode source,
canonical SVG bytes and ambient Activity ownership. All 1,646 Core tests pass.
The local windowless phase scenario completes all five families and unchanged
private-source/history validation. Comparative performance remains measured with
tracing disabled and the exact same harness binary for both revisions.

## Complete intermediate UX gate at `1aeb654`

[UX run 39107688436](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39107688436)
passes every job, including phase instrumentation, normal/actual High Contrast
workspace and lifecycle tests, long member selection/tooltips, all five authoring
suites, canonical fidelity and paired performance. Later changes retain those
checks and strengthen tab order, simultaneous windows, larger real-window cases,
pointer-centred Ctrl+wheel and cancellation exactly at the converter boundary.

All five inspectors now declare the scrollable body before fixed actions in
keyboard traversal, while keeping selected context outside the scroll. Flowchart
connection/subgraph Update actions join its fixed actions without changing their
handlers, IDs, visibility or permission bindings. Deferred navigation reveal is
bounded and cancelled on unload.

The final lifecycle matrix is six smaller cycles and one larger cycle per family
plus all five modeless families open together: 40 windows per appearance. It
records actual DPI, native selection allocation, first useful presentation,
Input-priority heartbeat observations, completed cleanup, exact source, queued
cancellation and latest-theme canonical pixels. Actual Ctrl+wheel verifies its
canonical anchor on scrollable axes, with no SVG conversion. These strengthened
checks still require hosted execution at the next revision.

The performance job now also checks identical harness SHA-256, runner metadata,
fixture hashes, family/size order, exactly seven samples and canonical verification
on every measured sample. Core/Native hashes and pinned renderer identities are
recorded. Fresh-process UIA opening observations are explicitly labelled as
including document preview and UIA transport; they are separate from native stage
measurements.

## Uniform consolidated before-capture completion

The original pre-implementation audit and unchanged-develop run remain the
baseline. An additional capture-only mode now runs the immutable `6a94dd3`
binaries with the exact same generated long-name fixtures as the candidate,
covering all five sizes in Light, Dark and actual High Contrast. These additional
before-state captures are produced after implementation to complete the paired
visual evidence; they are not mislabelled as the date of the initial audit.

The capture mode records the old inspector, long names, pending/reset, source
expanded/collapsed, Fit, Actual Size, zoom/scroll, invalid-source diagnostics and
new-element insertion/Undo. It checks unchanged owning source and does not apply
new-layout assertions to the old UI. The earlier five-family baseline authoring
suites retain the protected-source cases. Candidate acceptance continues through
the full shared/family suites; no existing assertions are skipped.

All four native/browser workflow definitions now pin PR test checkout to the exact
head. Pages deployment keeps its original default-branch-only condition; native
feature pushes and this Draft PR cannot deploy the site.
