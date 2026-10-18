# Native Mermaid suite UX and performance refinement

Status: **in progress; not certified**. This report records evidence as it is
produced. An unexecuted gate is not a pass. Production readiness and universal
Mermaid compatibility are outside this internal preview.

## Consolidated baseline

Remote `develop` was fetched on 18 October 2026 and resolves to
`6a94dd3672f95a267402e2218935d064a6ee6401`. All five editor windows, dedicated
source adapters, presentation adapters and validation suites are present.

- [Consolidated native run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39103278298):
  initially still running the installed lifecycle; final result pending.
- [Consolidated browser/static run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39103278332):
  completed successfully, Chromium and Edge; deployment correctly skipped.
- [Fresh baseline UX capture](https://github.com/DouglasNLima/local-docs-studio/actions/runs/39104215598):
  dispatched against unchanged `develop` before any interface implementation.
  Includes the existing five-family canonical/UIA suites and actual Windows High
  Contrast. Results and screenshots await inspection.

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
