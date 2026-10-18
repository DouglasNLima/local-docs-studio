# Native Mermaid suite UX and performance refinement

This report records the five-family native editor refinement in Draft PR
[#36](https://github.com/DouglasNLima/local-docs-studio/pull/36), targeting
`develop` from `feature/native-mermaid-suite-ux-performance`.

Status: **implemented with bounded limitations; controlled internal preview**.
The primary UX/performance implementation and complete application preview were
verified at `11063a9c60447a2777bc53906375890b01f6315f`. A final feedback refinement
distinguishes renderer resource/deadline failures from caller cancellation;
budgets, successful rendering and source semantics remain unchanged. Subsequent
evidence/harness revisions and final package source are identified in the PR's
delivery receipt. Earlier engineering
and family certification reports remain intact. The
[implementation work log](mermaid-suite-ux-performance-refinement-work-log.md)
retains intermediate measurements, rejected approaches and original failures.

## 1. Consolidated baseline

Remote `develop` was fetched before implementation and checked again before
delivery. It resolves to `6a94dd3672f95a267402e2218935d064a6ee6401`; all five
windows, dedicated semantic models, adapters and authoring harnesses are present.

| Baseline gate | Result |
|---|---|
| [Actual consolidated native post-merge run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39103278298) | Successful; main package lifecycle and installed State/ER/Class jobs completed |
| [Actual consolidated browser/static post-merge run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39103278332) | Successful; Chromium/Edge/static, deployment skipped |
| [Unchanged baseline UX run before implementation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39104215598) | Successful; five-family UIA/canonical checks and actual High Contrast |

The integration-only historical-upgrade job was skipped by its existing branch
condition. It is not counted as a pass. Historical feature-PR checks do not
substitute for these post-merge checks. Baseline run metadata is retained in
[the evidence directory](evidence/mermaid-suite-ux-performance/).

## 2. Five-editor UX audit

This source-level matrix was recorded before changing the interface; the first
hosted 880 × 600 captures were also inspected then. Supplemental, same-fixture
before captures subsequently ran the current capture harness against immutable
baseline binaries. Their later capture date does not make them pre-change runs.

| Area | Flowchart Advanced | Sequence | State | ER | Class |
|---|---|---|---|---|---|
| Initial/minimum navigation width, DIP | 180/156 | 180/152 | 180/150 | 180/150 | 180/150 |
| Navigation | Nodes/connections/subgraphs | Separate participants/ordered interactions | Flat scoped occurrences | Entity then attributes/relationships | Class then members/namespaces/relationships |
| Full descriptions | Content tooltip | Default display member | Default display member | Default display member | Default display member |
| Selected identity | Fixed above scroll | Inside scroll | Inside scroll | Inside scroll | Inside scroll |
| Property actions | Fixed; node pending rules | Inside scroll | Inside scroll | Inside scroll | Inside scroll |
| Common toolbar | Mostly vectors | Mixed text/icons | Mixed text/icons | Mixed text/icons | Mixed text/icons |
| Source size | Expanded, adjustable | Collapsed, fixed | Collapsed, fixed | Collapsed, fixed | Collapsed, fixed |
| Placement command | Clear session positions | None | Restore session positions | Fit canonical positions | Fit canonical positions |

Sequence chronology/activations, Flowchart partial editing and multi-selection,
State composite scopes, ER cardinalities/attributes and Class member ordering are
intentional family differences. Disappearing identity, misleading placement
wording, narrow unnamed splitters, incomplete full-name access and inconsistent
common commands were accidental inconsistencies.

## 3. Shared design changes

`MermaidEditorStyles.xaml` now provides common vector command presentation,
navigation entries/groups, inspector headings, source frames and keyboard
splitters. Existing DesktopStyles, FormStyles and CommandIcons remain in use.
Neutral surfaces and restrained `#FF883E` actions/selection follow Lens branding;
actual High Contrast uses system resources and visible non-colour state cues.

`MermaidWorkspace` contains presentation behaviours only: source sizing,
identity-aware inspector scrolling, native list focus/reveal, canvas reveal and
pointer-centred zoom. It imports no family model. `MermaidInspectorPanel` supplies
a native UIA Group peer for the persistent context/actions container. Plain
DockPanel was insufficiently observable and was rejected after hosted evidence.

.NET 10, WPF, AvalonEdit, Markdig, Mermaider 0.15.1, SharpVectors, FlowDocument,
DiagramService and all five source adapters remain. No grammar, family, layout
engine, browser runtime or backend was added.

| Family | Resulting refinement |
|---|---|
| Flowchart Advanced | Shared full-name navigation/focus/splitters, compact common commands, original-source view, retained partial editing and session placement |
| Sequence | Consistent participant/interaction presentation, fixed selected context/actions, exact newly inserted statement selection, adjustable source/copy and readable initial viewport |
| State | Scoped navigation groups, fixed state/transition context, shared zoom/source/commands and retained temporary positions |
| ER | Full entity/attribute names, fields before parent attribute list, fixed context/actions, accurate canonical-centre command and immutable conversion reuse |
| Class | Full occurrence-qualified method descriptions, wrapped parameters, fields before member list, fixed context/actions and measured large-diagram conversion reuse |

## 4. Navigation improvements

All five initialise at 220 DIP with a 180 DIP minimum and remain resizable.
Named splitters have visible grips/focus and 16 DIP arrow-key resizing. The
880 × 600 layout retains canvas space and its footer. The default is deliberately
modest; larger widths in the size gallery include explicit keyboard resizing.

Flowchart groups nodes/connections/subgraphs. State groups scoped states/composite
entries and transitions. ER preserves entity/attribute order and groups
relationships. Class preserves class/member order and separates namespaces,
notes and relationships. Sequence retains distinct participant and chronological
interaction lists. A single inappropriate tree model was not imposed.

Every navigation occurrence exposes the exact full description as UIA Name and
HelpText, plus a wrapped tooltip bounded to 560 DIP. Ellipsis is presentation only.
Parent identifiers and occurrence ordinals distinguish repeated members and
parallel relationships; their private semantic identities remain authoritative.

Native Selection/SelectionItem semantics remain. Entering a list delegates focus
to its exact single selected row after the focus provider completes. Blocked
pending-property moves restore selection, visible row and focus after WPF's
selection transaction. Deferred callbacks are bounded and cancelled on unload.
Multiple-selection behaviour remains native. Both row-focused and list-focused
keyboard paths are asserted without replaying input.

## 5. Persistent inspector context

Selected type, occurrence/display description, parent/scope and editability or
pending status stay outside the independent property scroll. Update/Reset actions
remain reachable in a fixed action area. Flowchart retains its existing
pending-only node actions and separate connection/subgraph actions.

An identity change starts the new property context at its beginning. The same
identity retains its scroll and field; refreshes do not unconditionally scroll
to the top. Class method and ER attribute fields precede the parent's member
list. Class parameters wrap in a bounded multiline field. Keyboard focus uses
native BringIntoView behaviour. Pending values, blocked-operation explanations,
explicit Update and Reset remain protected.

## 6. Toolbar and keyboard consistency

Common Undo/Redo, Delete/Duplicate, zoom controls, Fit, Actual Size, source
visibility/copy and rendered preview use existing vector iconography, accessible
names, tooltips, stable IDs and correct disabled states. Existing IDs were
preserved; new inspector groups/splitters/source commands received new IDs.
Native ToolBar overflow is tested at 880 × 600, including invoking its preview.
The zoom caption uses one decimal only when needed; slider precision is unchanged.

Class/ER accurately say **Centre canonical layout** and fit/centre without
rebuilding overlays. State restores temporary positions; Flowchart retains Auto
layout. Sequence retains Earlier/Later; relationship reversal remains specific to
its owning families. No command promises portable manual coordinates.

Ctrl+0 provides Actual Size consistently, including Class/ER. Ctrl+wheel remains
pointer-centred. Existing middle-button pan and applicable Space+drag remain.
Flowchart Home/End/arrows operate its focused list; viewport Home still works
outside navigation. Built-in help documents the shared workspace and shortcuts.

## 7. Source-panel behaviour

The four newer editors now have an adjustable source row with a 190 DIP expanded
default and compact collapsed heading, matching Flowchart's adjustable workflow.
All expose validation, immutable original source, copy and readable line numbers
with theme-aware syntax colours. Flowchart gained the original-source view.

Expanding/collapsing does not reassign source, reset selection/history or change
zoom. Refresh assigns editor text only when it differs, preserving caret
ownership. Validation failures retain the invalid draft, diagnostics and safe
copy/history operations; no generated correction overwrites it. The existing
family authoring checks cover correction, rejection, private Undo/Redo and Apply.

## 8. Viewport behaviour

Initial Fit is accepted when it is at least 60%; otherwise Actual Size shows a
useful canonical region. Explicit Fit remains available below 60%. Dense diagrams
are not automatically reduced to illegible labels just to show everything.

Navigation and insertion reveal the exact canonical target only when necessary,
preserving zoom. Oversized targets retain a meaningful visible region. New
Sequence statements use the exact newly added private identity. Session-only
Flowchart/State positions remain; canvas selection during dragging does not
introduce viewport jumps. Pointer zoom performs no SVG conversion or full render.
Viewport transforms do not alter canonical geometry or portable source.

## 9. Feedback and accessibility

Fixed context and existing status/footer distinguish Ready, Draft modified,
Pending properties, Partially editable, protected operations, validation/render
failure and resource rejection. Disabled controls retain explanations through
tooltips/UIA HelpText. Protected syntax is not reclassified as invalid syntax.

Actual Windows UIA verifies full names, exact selected occurrence, visible focus,
field labels/tab order, enabled states, pending-value rollback, keyboard
splitters, toolbar overflow and retained inspector context. Light, Dark and actual
OS High Contrast passed at 96 DPI. This is UIA/visual verification, not physical
screen-reader certification. OS scaling at 125%, 150% and 200% was unavailable on
the disposable runner and is **not certified**.

Mermaider's documented resource exception receives a specific limit message.
Its internal deadline cancellation receives **Rendering resource deadline reached
(3 seconds)** with a size-reduction/validation action. Caller cancellation still
propagates, and source is retained exactly. The existing input/element/deadline
values are unchanged. A real 501-node rejection test verifies specific feedback,
CRLF/Unicode source preservation and independent caller cancellation. The ER
100/150 diagnostic remains unsuccessful, with its deadline now explained clearly.

## 10. Screenshot acceptance

The [unedited hosted gallery](images/mermaid-suite-ux-performance/verified-11063/README.md)
contains all five editors at 880 × 600, 1024 × 768, 1280 × 800, 1440 × 900 and
1920 × 1080, in Light and actual High Contrast, plus initial, pending, protected,
Dark and baseline comparisons. All 50 selected size/appearance images were
manually inspected. Footers, common commands, identity headings and action areas
remain contained; Class/ER long-name tooltips were inspected in Dark appearance.

Full expanded/collapsed sets and additional Fit/Actual Size, validation error,
insertion, zoom/scroll, property and protected states are in the linked hosted
artifacts. The current harness also captures minimum-size lower inspector fields
before asserting that a new selection starts at its own property context.
The [SHA-256 manifest](evidence/mermaid-suite-ux-performance/verified-11063/provenance.json)
identifies copied files, source artifact paths and their runs. No mock-ups or
edited screenshot pixels are used.

The stronger [source-state gallery](images/mermaid-suite-ux-performance/verified-589dad/README.md)
uses harness revision `589dad3` against unchanged production code. Light/Dark
**54 groups** and actual High Contrast **27 groups** passed, including invalid
source retention, explicit recovery, all five selected/revealed insertions at
175% zoom, exact private Undo and minimum-size lower-inspector captures. The
Class/ER lower-field and all five insertion images were manually inspected.

## 11. Performance instrumentation

Opt-in ActivitySource `LensDocsStudio.Mermaid` records scanner, semantic/private
validation, renderer, sanitisation, conversion queue, SharpVectors Read, WPF
freeze/image, geometry correspondence, overlay, presentation/interaction,
selection and cleanup boundaries. No listener creates no measurement objects or
logs. Tags contain thread IDs and same-thread allocations, never authored text,
paths or document identities. Inclusive nested spans must not be added twice.

The real editors dispatch DiagramService work to background tasks and native
conversion/correspondence to STA workers. Overlay controls, inspector updates,
window layout and Apply remain on the WPF dispatcher. Recorded start/end thread
tags distinguish these paths; the windowless benchmark deliberately executes its
measured stages on one STA and is not presented as UI-thread timing.

Existing Mermaider graph callbacks distinguish parsing before layout, graph
layout and SVG production for Flowchart/State/ER/Class. Mermaider 0.15.1 exposes
no equivalent Sequence layout callback: its combined pipeline is labelled
honestly. SharpVectors Read creates DrawingGroup geometry internally; Read and
final freeze/wrapping are separate, without inventing an inaccessible boundary.

## 12. Measurement method and host

Baseline and candidate execute the **same compiled validation harness** on the
same hosted Windows runner. CI asserts matching harness hash, host metadata,
family/source SHA-256, workload and sample counts. Seven samples per fixture
include first fixture execution and six same-source/new-service repeats.
Every sample independently compares canonical PNG pixels outside the timed block.

The timed presentation includes correspondence and native conversion, not UIA.
Standalone scanning, renderer, sanitisation and conversion observations overlap
the complete presentation and are not summed into a fictitious total. Allocations
and working set are recorded. First fixture execution includes process/JIT/cache
history and is not a clean-process latency guarantee for every workload. The
separate native lifecycle harness measures real WPF presentation and selection;
UIA cold-open observations explicitly include transport/document preview.

Host: `win25-vs2026` image `20261007.264.1`, Windows 10.0.26100, .NET 10.0.13,
x64, four logical processors, 96 DPI. Renderer/converter: Mermaider 0.15.1 and
SharpVectors.Wpf 1.8.6. Local Release/Core/static/browser checks used Windows
10.0.26300, .NET SDK 10.0.401/runtime 10.0.12; local timings are not mixed into
the hosted comparison.

## 13. Before/after performance

The primary paired run is [39117436826](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117436826).
Times are milliseconds; warm median and maximum use six repeats per revision.
The maximum is an observed tail sample, not a defensible p95 estimate from six
samples. Raw [before](evidence/mermaid-suite-ux-performance/verified-11063/primary-before-suite-performance.json)
and [after](evidence/mermaid-suite-ux-performance/verified-11063/primary-after-suite-performance.json)
retain stage timings, allocation/working-set observations and exact fixture text.

| Family/workload | First before → after | Warm median before → after | Warm maximum before → after |
|---|---:|---:|---:|
| Flowchart 20 nodes/24 branches and cycles | 105.75 → 107.22 | 121.96 → 9.21 | 137.04 → 20.62 |
| Flowchart 100 nodes/110 connections | 589.42 → 558.48 | 493.46 → 45.19 | 551.11 → 65.32 |
| Sequence 5 participants/20 messages | 74.38 → 135.42 | 78.81 → 8.38 | 118.30 → 22.60 |
| Sequence 20 participants/150 messages | 614.81 → 823.77 | 558.50 → 39.72 | 603.78 → 77.60 |
| State 10 states/15 transitions | 155.72 → 154.16 | 65.31 → 4.66 | 125.05 → 13.43 |
| State 60 states/70 transitions | 306.93 → 366.45 | 283.51 → 28.90 | 360.07 → 35.51 |
| ER 10 entities/15 relationships | 106.76 → 137.92 | 92.31 → 7.22 | 116.92 → 22.08 |
| ER 30 entities/60 relationships | 281.11 → 330.28 | 308.51 → 29.18 | 381.43 → 37.94 |
| Class 10 classes/15 relationships | 142.35 → 157.26 | 102.96 → 6.96 | 115.77 → 22.64 |
| Class 100 classes/150 relationships | 1020.56 → 1250.94 | 1018.15 → 63.60 | 1090.58 → 75.67 |

Warm repeated-presentation reductions are approximately 89.4–93.8% across these
fixtures. They reflect reused immutable conversion, not omitted validation,
smaller diagrams or disabled pixel assertions. First-execution results are mixed;
no general cold-open speed-up is claimed. The independent duplicate run also
shows warm gains in every family. Its first Sequence/ER cases warrant separate
uncached investigation rather than averaging them away. A new seven-sample
unique-comment comparison forces exact-source misses, preserves diagram content
and records per-presentation collection counts/allocations.

That investigation completed with harness `589dad3` in two independent hosted
runs: [primary](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39120214799)
and [replication](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39120219662).
Each cell below is a seven-sample **uncached presentation median**, before →
after in milliseconds. Distinct trailing comments force source-key misses while
retaining the entire diagram and pixel verification. Each pair uses one host and
identical harness; the two runs are reported separately, not averaged together.

| Family/workload | Primary uncached before → after | Independent replication before → after |
|---|---:|---:|
| Flowchart smaller | 103.94 → 99.06 | 81.07 → 97.24 |
| Flowchart larger | 354.01 → 346.30 | 468.72 → 324.80 |
| Sequence smaller | 74.73 → 79.45 | 67.66 → 63.57 |
| Sequence larger | 504.62 → 466.12 | 516.71 → 486.81 |
| State smaller | 50.23 → 51.18 | 35.51 → 36.87 |
| State larger | 257.26 → 297.41 | 261.15 → 253.54 |
| ER smaller | 91.35 → 103.14 | 59.93 → 59.03 |
| ER larger | 266.58 → 267.54 | 247.19 → 245.29 |
| Class smaller | 89.46 → 88.11 | 97.88 → 66.61 |
| Class larger | 873.74 → 920.22 | 864.60 → 870.69 |

The primary larger State median regressed 15.6%; the independent run improved
2.9%. Its standalone converter median was 233.73 → 228.28 ms in the first run,
while presentation generation-two collections rose from two to five. This is a
memory/collection observation, not proof of a single cause. The replication does
not reproduce that slowdown. Conversely, smaller Flowchart regressed 20.0% in
the second run while the first improved 4.7%. Larger Class is slightly slower in
both uncached medians (5.3% and 0.7%); its cold speed is not claimed improved.

These regressions remain visible. No general cold-render gain or memory reduction
is certified. Retaining bounded immutable drawings has a working-set trade-off;
the investigation did not justify unsafe mutable reuse, forced collection,
deadline changes or a replacement renderer. Larger ER/Class per-presentation
median allocations fall from 105.31 → 101.10 MiB and 337.97 → 329.35 MiB in
the primary uncached run, consistent with removing duplicate XML preparation.
Raw [uncached before](evidence/mermaid-suite-ux-performance/verified-589dad/uncached-before-suite-performance.json),
[after](evidence/mermaid-suite-ux-performance/verified-589dad/uncached-after-suite-performance.json)
and replication receipts retain all samples, maxima, collection counts and
working-set snapshots. No slow sample is omitted.

The ER 100-entity/150-relationship diagnostic still hits the unchanged renderer
deadline: no successful rendering or interaction benchmark is claimed. Class
100/150 does complete and its native conversion is measured separately. Existing
fidelity/UIA fixtures additionally exercise long labels, nested scopes, many
members, parallel relationships, cycles and protected syntax.

The following large-workload observations use the same primary run. Allocated
bytes cover the measured scanning/rendering/standalone conversion/presentation
work before pixel verification; they are not retained heap size. Working set is
the process snapshot after the last sample, influenced by prior fixtures and
collection timing. The bounded cache trades retained drawings for avoided work;
there is no claim that working set decreases.

| Family, larger fixture | Warm median allocated MiB before → after | Last-sample working set MiB before → after |
|---|---:|---:|
| Flowchart | 152.04 → 80.39 | 179.5 → 179.8 |
| Sequence | 305.02 → 164.39 | 294.0 → 268.9 |
| State | 166.50 → 88.63 | 234.5 → 322.8 |
| ER | 206.21 → 108.42 | 265.5 → 325.9 |
| Class | 670.38 → 356.26 | 411.3 → 454.9 |

## 14. Safe optimisation and rejected approaches

Profiling established repeated SharpVectors conversion as a shared cost. The
new process-local canonical drawing cache holds at most eight frozen drawings
and two million exact-source/prepared-SVG characters. Keys include exact source,
resolved theme, complete prepared SVG and runtime-identity option; pinned
renderer/converter versions are part of process lifetime. Mutable models,
occurrence maps, overlays, session positions and windows are never cached.

ER/Class also reuse their sanitised XDocument instead of sanitising and parsing
the same SVG repeatedly. Correspondence/semantic checks still run. Ordinary
preview/export's existing cache remains. Selection and canonical centring no
longer request needless reconstruction. Cache tests assert source/theme/options
isolation, frozen cross-STA ownership, eviction, cancellation and active-SVG
rejection.

Rejected approaches include sharing mutable WPF drawings, skipping semantic or
pixel checks, relaxing deadlines, physical scrolling as a substitute for native
selection repair, immediate focus delegation inside UIA SetFocus, unsafe thread
aborts and another graph layout engine. No production GC was introduced.

## 15. Memory and interaction lifecycle

Each normal/High Contrast native lifecycle run opens six smaller and one larger
window per family, then all five simultaneously: **40 real windows per run**.
It asserts exact source, completed cleanup, independent modeless ownership,
queued-close cancellation and 20 selection updates per sequential window with
**zero conversions**. Controlled diagnostic collection retained **zero editor
windows** in every family. Bounded drawing/font/theme caches remain in process.

| Family | Maximum 20-selection batch, normal / HC ms | Maximum Input heartbeat gap, normal / HC ms | Retained windows, normal / HC |
|---|---:|---:|---:|
| Flowchart | 67.85 / 29.96 | 715.71 / 450.97 | 0 / 0 |
| Sequence | 32.74 / 15.54 | 273.23 / 214.92 | 0 / 0 |
| State | 32.32 / 15.19 | 364.59 / 342.25 | 0 / 0 |
| ER | 45.72 / 28.42 | 199.40 / 104.52 | 0 / 0 |
| Class | 66.92 / 47.88 | 281.12 / 259.59 | 0 / 0 |

These native timings exclude UIA transport. Heartbeat gaps cover initial
render/conversion, not selection alone, and show that cold conversion can still
delay dispatch. Post-collection working set ranged from about 210–327 MiB as
families/caches/fonts accumulated; five simultaneous windows reached roughly
350 MiB. No working-set reduction or unlimited-duration leak guarantee is
claimed. Strict window-retention assertions are separate from these observations.

## 16. Cancellation and stale results

Canonical conversions share one semaphore. Obsolete waiters cancel before
SharpVectors starts, and cancelled results are checked after its non-interruptible
Read before cache or UI installation. Editor generation/source/document-version
guards remain. Class/ER cancel obsolete work; close waits for actual render
cleanup before disposing the gate. Exceptions are not silently suppressed.

Tests hold conversion capacity, queue a unique source, close the editor and
require completed cancellation/cleanup. Rapid theme/source changes must install
the latest canonical pixels. Cache tests also cancel at converter entry. The
existing source 100,000-character, renderer 500-element/three-second, SVG
eight-million-character and interaction-geometry 30,000-element limits remain.
An active SharpVectors call still cannot be interrupted safely.

## 17. Regression and source integrity

| Gate at the frozen application revision | Evidence/result |
|---|---|
| Release solution | Zero warnings/errors |
| Core | 1,646 passed, zero skipped; family authoring/capability/partial-source contracts included |
| Native rendering/export corpus | 134 cases/export checks, zero failures; DOCX/PDF/SVG/PNG and static Docs Site included |
| Chromium/Edge | 270 passed (135 each); static integrity passed |
| Five-family UIA normal / actual HC groups | Flowchart 142/138; Sequence 30/31; State 47/47; ER 40/41; Class 45/46 |
| Shared suite | 44 Light/Dark groups and 22 actual HC groups; both focus-entry paths |
| Canonical fidelity | Flowchart 371 checks; Sequence 183 per run; State 110/111; ER 152/153; Class 282/283, including 40 Class contexts per appearance |
| Immutable cache | 13 isolation/ownership/cancellation checks |
| Native lifecycle | 40 windows normal plus 40 actual HC; zero retained editor windows |
| Pointer/workspace | PR run successful; duplicate HC capture failure retained and capture path corrected for final execution |

The Core total includes Class 117, ER 103, State 41, Sequence 60 plus 17
activation cases, Flowchart visual 44, visual corpus 19 and partial-editing 86.
These are overlapping scope descriptions within the total, not additional test
totals. The [complete class-level summary](evidence/mermaid-suite-ux-performance/verified-11063/core-test-summary.json)
retains every executed class and the original zero-failure/zero-skip counters.
The final resource-feedback safety test brings the branch Core total to **1,647**;
the complete local suite passes without skips. Its windowless run also completes
70 canonical pixel checks and explains the unchanged ER deadline explicitly.
Final hosted totals are identified in the PR delivery receipt.

All five retain Open → Select → Edit → Validate → Apply → document Undo/Redo →
Save → Reopen. Cancel leaves the owner unchanged. Existing exact expected-text
patches, comments, unsupported regions, Unicode, LF/CRLF, repeated occurrences,
parallel relationships, membership, class order, ER keys/cardinalities and
Sequence chronology remain protected. Rejected edits create no private Undo
entry. Owner close/change, tab switching and revision conflicts remain guarded.
No coordinates or private metadata are saved into Mermaid.

An earlier independent SQL syntax-colouring guard failed at `dc74795`; its
original log remains. That code and its 25 ms regex/250 ms document budgets are
unchanged. Later passes are not described as a fix. Continuous Markdown preview
250 ms first-layout observations are rerun and reported independently below;
Mermaid cache improvements do not resolve them by implication. At `11063a9`,
the unchanged Continuous first-layout checks report list **1288.52 ms FAIL**,
table **258.22 ms FAIL**, mixed **582.94 ms FAIL**, quote 127.95 ms PASS,
details 100.07 ms PASS and paragraphs 247.94 ms PASS. The workflow intentionally
records these known 250 ms observations separately; its green conclusion is not
a 250 ms performance certification. Raw observations are retained with the
suite evidence.

## 18. CI and installed acceptance

All four necessary workflows run the exact feature/PR head. Pages deployment
remains isolated from feature PRs. No mandatory skipped job counts as passing.

| Application-revision workflow | URL |
|---|---|
| Native PR / push | [39117436785](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117436785) / [39117433665](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117433665) |
| UX PR / push | [39117436826](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117436826) / [39117433645](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117433645) |
| Interactions PR / push | [39117436776](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117436776) / [39117433658](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117433658) |
| Browser/static PR / push | [39117436767](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117436767) / [39117433702](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117433702) |

The duplicate interaction push run failed querying UIA BoundingRectangle for a
screenshot during active drag after all 120 drag cycles passed. The PR run
passed unchanged. The failure is retained, not relabelled as a fix by retry.
The corrected harness queries bounds before MouseDown and captures the same
real rectangle during the gesture, keeping all input/assertions and deadlines.

Final-head workflow URLs/results and current package receipts are maintained in
the PR delivery summary. General installation, controlled upgrade, uninstall,
reinstall and all five separate installed authoring suites execute only on
disposable hosted Windows. The ordinary desktop installation is untouched.

Both native workflows at the frozen application revision completed successfully.
All five installed suites passed install, upgrade and reinstall with exact
portable-byte parity, private/document Undo, Apply, Save/reopen and Cancel.

| Installed family | UIA groups per install / upgrade / reinstall | Receipt |
|---|---:|---|
| Flowchart | 142 / 142 / 142 | [Verified](evidence/mermaid-suite-ux-performance/verified-11063/installed-flowchart.json) |
| Sequence | 30 / 30 / 30 | [Verified](evidence/mermaid-suite-ux-performance/verified-11063/installed-sequence.json) |
| State | 47 / 47 / 47 | [Verified](evidence/mermaid-suite-ux-performance/verified-11063/installed-state.json) |
| ER | 40 / 40 / 40 | [Verified](evidence/mermaid-suite-ux-performance/verified-11063/installed-er.json) |
| Class | 45 / 45 / 45 | [Verified](evidence/mermaid-suite-ux-performance/verified-11063/installed-class.json) |

The interaction workflow separately reproduces the old null-transform drag crash
on immutable `14b3d4f`. Its baseline failures are diagnostic evidence, not passing
candidate checks and not the consolidated `6a94dd3` baseline. Candidate normal/HC
each complete 120 drags and 2,040 pointer moves, plus reconnection, insertion,
selection, source/Apply and workspace ownership checks. Original event logs are
retained with the suite evidence.

## 19. Package provenance

The independently verified application preview is **0.1.1-preview.713**, Windows
x64, unsigned and self-contained, from exact clean source
`11063a9c60447a2777bc53906375890b01f6315f`. The installer and portable ZIP are in
the [native-validation artifact](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39117433665).

| File | SHA-256 |
|---|---|
| `LensDocsStudio.Native-0.1.1-preview.713-win-x64-Setup.exe` | `ad7a0b6e3554cb795433a668b2d3c084cfa9819150c1be27def7672046e1dfea` |
| `LensDocsStudio.Native-0.1.1-preview.713-win-x64.zip` | `27723af8ddb361e9e055e9a4fb417aad9287c7821a5288969067607c2dddce9a` |

The [independent, non-executing receipt](evidence/mermaid-suite-ux-performance/verified-11063/independent-package-verification.json)
verifies the pinned hashes, exact versions/source, all five editor types/BAML,
matching Native/Core/apphost, complete bundled .NET/WPF, Lens branding and absence
of browser/experimental/development/private payload. Hosted installed verification
compares **all 528 payload files** against portable bytes; only Inno uninstall
files are additional. Upgrade/reinstall preserve **22 state files**, actual tabs,
dirty drafts/settings and workspace bytes. The
[nine negative controls](evidence/mermaid-suite-ux-performance/verified-11063/package-negative-controls.json)
all reject their packages, including missing editor/ER/Class resources and
tampering with recomputed checksums; the original package remains unchanged.

This is a controlled same-revision two-version upgrade. A physical SDK-free clean
Windows image, unrepresented historical state and production-signed upgrades are
not certified. The hosted installed process loads its bundled coreclr/WPF modules;
it does not rely on the runner's installed runtime. Final evidence/harness-head
packages and their exact identities are listed separately in the PR receipt.

No public release, tag movement, merge, production signing or unsigned updater
execution is authorised by this preview.

## 20. Readiness and bounded limitations

| Deliverable | Classification |
|---|---|
| Shared toolbar/navigation/inspector/source/viewport UX | Implemented and verified at the frozen application revision |
| Source and canonical contracts | Implemented and verified by five-family/Core/UIA regression |
| Warm conversion bottleneck | Implemented and verified; bounded immutable reuse with comparable fixtures |
| Cold conversion / first presentation | Implemented with bounded limitations; mixed timings, uncached investigation retained |
| Lifecycle/cancellation | Implemented and verified in the finite normal/HC scenarios |
| Higher OS DPI / physical screen reader | Not certified; suitable execution unavailable |
| ER 100/150 rendering | Unchanged resource-limit diagnostic; not a successful benchmark |
| Continuous 250 ms preview | Unchanged separate acceptance issue |
| Full installed/package acceptance | Implemented and verified for the identified application preview; controlled same-revision upgrade limitations recorded |
| Resource/deadline feedback | Implemented and verified locally; final hosted revision identified in the PR receipt |
| Production readiness / universal Mermaid compatibility | Not certified and outside scope |

The identified application preview is ready for controlled internal evaluation
within these limits. Final review-head CI/package identities are recorded in the
PR receipt and must be checked for that exact revision. An unexecuted or skipped
gate is not a pass. No production or universal Mermaid compatibility claim is
made.
