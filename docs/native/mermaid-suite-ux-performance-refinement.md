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
