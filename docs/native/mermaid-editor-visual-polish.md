# Mermaid editor visual polish

Draft [PR 27](https://github.com/DouglasNLima/local-docs-studio/pull/27), branch
`feature/native-mermaid-editor-visual-polish`, targets the verified PR 26 head
`eb81939835a1bb2a92cf76570de77d8d69e571ff` on `feature/native-mermaid-visual-editor`.
PR 26 and its ancestors, the installed preview and personal workspaces are untouched.
This is a presentation change to the native editor; the buildless web app is unchanged.

## Baseline and visual decisions

Retrieved actual Windows evidence from verified run 38784102160, then rebuilt the
same revision in an isolated CI worktree for a repeatable comparison. Inspected
main-window and editor captures in equivalent themes and representative documents.
The final comparison harness uses 1280 x 840 owner/editor windows, the same two-node
source, selected B, expanded source and 100% zoom. Earlier screenshots used Fit,
whose percentage differs with the new inspector width; those are not pixel-equivalent
zoom comparisons.

The old editor had generic local Button/TextBox/ComboBox/Label styles, wrapping text
commands, irregular insertion buttons, a flat list, square permanent ports, crowded
properties, an isolated Validate button and two footer rows. The main window uses
Segoe UI, outline CommandIcons, neutral panels, compact commands, themed Fluent
inputs and an accent selection marker. Those existing resources are the reference.

| Area | Shared resource / resulting presentation |
|---|---|
| Commands | `Desktop.DiagramToolbar`, `Desktop.DiagramToolbarButton`, native overflow, existing command icon control |
| Forms | `Form.Label`, `Form.TextInput`, `Form.ComboBox`, `Form.Field`, `Form.Help`, `Form.Action`, `Form.PrimaryAction`, `Form.CheckBox` |
| List | `Desktop.SelectionItem`, icon plus ellipsised text, full tooltip, retained Selection/SelectionItem |
| Panels | `Desktop.PanelHeading`; existing Window/Panel/Border/Text/MutedText/Hover/Pressed/Accent brushes |
| Theme | Existing application ThemeService and Fluent dictionaries; Windows system colours in actual High Contrast |
| Local styles | `Resources/MermaidEditorStyles.xaml` derives palette, actions and toolbar controls from shared styles; owns canvas/source/splitter presentation |

No global theme change, new dependency, UI framework or colour-token set is added.
The Lens icon is retained. Existing icon geometries are reused for history, zoom,
fit, reset and preview; small original vectors cover delete/duplicate/layout,
source actions and every supported node shape. No raster or emoji palette artwork.

## Workspace and interactions

The compact identity header displays Ready, Draft modified, Pending properties,
Source unvalidated, Validation failed or Protected source. A single overflow toolbar
groups editing, direction and view commands. Icon-only controls have names and
action/shortcut tooltips. Existing command handlers and automation IDs remain.

The two-column palette has consistent native shape tiles and native drag/drop.
The diagram list distinguishes nodes, connections and subgraphs by vector icon;
long labels truncate with tooltips. Multi-selection and keyboard navigation retain
the original list contract. Compact tiles leave more space for the list at 880 x 600.

The canvas has a bounded neutral surface and compact Session layout help. Nodes
retain authored colours and supported geometry, with balanced label padding.
Selection adds an outline and small handle; focus adds a separate dashed ring.
Ports retain named buttons and larger 32-pixel targets, revealed by selection,
hover or keyboard focus. Connections have rounded joins, centred labels, a broad
transparent pointer target and a lightweight dashed connection guide. Subgraphs
retain subtle surfaces and labels. Hover never rebuilds the graph. Pointer motion
updates native geometry without parsing or invoking the Mermaid renderer.

Properties have a stable summary and independently scrolling contextual body.
Identity, Appearance, Colours and Structure group the node fields; connections
and subgraphs show only their own sections. Labels target their controls. The
pending Update node action stays above the scrolling body; Reset changes appears
only while needed. Absent source-defined classes do not leave an empty selector.
Existing membership/class/colour actions remain available. Their atomic pending-node
commit behaviour is unchanged. No automatic property application is introduced.

Fill, Stroke and Text have labelled hexadecimal inputs and safe native swatches.
Swatches accept only the existing six-digit syntax and never interpret CSS or URLs;
the existing transformation validator remains authoritative. Invalid values retain
both source and pending-property protection. High Contrast uses system swatch surfaces
and keeps the authoritative values readable.

The source header now contains validation and copy controls, a state marker and
collapse control. AvalonEdit, syntax highlighting, text shortcuts and invalid-source
retention remain. Source is populated before first layout, equal-text refreshes no
longer reset it, and a padded bounded source body keeps the first line clear. Actual
first meaningful and settled captures are inspected separately; no timer or arbitrary
delay is introduced. Collapsing source reclaims canvas space and disables its splitter
until expanded; it does not reset source or selection.

The fixed footer contains a single ellipsised live status with full tooltip, supporting
Edit source, secondary Cancel and accent Apply. Long diagnostics cannot grow the
footer or push its actions away. Copy belongs to the source header. The former
permanent session-position paragraph is now discoverable canvas help.

## Safety, accessibility and responsive acceptance

Core Mermaid parsing, source patching, bounds, private history and document-conflict
contracts are untouched. Existing exact-source authoring checks still exercise
Apply to the second identical block, one document Undo/Redo, safe relocation,
conflicting Apply, invalid/unsupported source, standalone Mermaid, Cancel and reopen.
Presentation changes do not add persistent coordinates or proprietary metadata.

The extended real UIA sequence also verifies pending-property selection/theme
protection, collapse/expand draft preservation, actual zoom values, reversed connection
endpoints, invalid-colour rejection/reset and native 20/100/300-node canvases plus the
nested/styled complex fixture. It checks action/panel containment at 880 x 600,
1024 x 768, 1280 x 800, 1440 x 900 and 1920 x 1080 in normal and actual High Contrast.
Minimum size intentionally uses toolbar overflow, scrollable list/properties/source
and PAN/fit; it does not promise every graph label fits without zooming.

All existing IDs, named ports, Toggle states and list patterns are retained. New
icon controls have accessible names/tooltips; new form labels use target associations.
Keyboard commands remain in the original handler; text entry retains its own keys.
These are tested control paths, not physical screen-reader certification.

## Evidence and certification status

The first integrated candidate `2316415` passed 44 normal / 43 High Contrast authoring
checks; inspected screenshots prompted smaller palette tiles and a stable contextual
inspector action. The second candidate `5f94d11` passed 83 normal / 81 High Contrast
checks in branch and PR runs. First-frame source and compact/complex/pending-theme
screenshots were actually reviewed, then splitter, hit-target and comparison refinements
were compiled for final validation.

Retained failure: baseline run 38800356300 recorded the unchanged main window's
active-tab visibility assertion during a 1440-wide screenshot. Its raw screenshot
and tab bounds are preserved; the parallel PR run 38800362715 passed the identical
assertion. No assertion/deadline was weakened, and no retry loop was added. Final
validation retains that original desktop regression.

Final regression totals, immutable screenshots, CI references, package identities,
checksums and readiness verdict will be recorded after final hosted validation.
All native GUI, High Contrast/display changes and installer lifecycle execution use
disposable GitHub-hosted Windows with restoration guards. Local work compiles source
and inspects pixels/PE metadata; it never launches or installs the new app.

Physical multi-monitor/DPI at 125/150/200%, screen readers, input latency on real
hardware and a genuinely SDK-free Windows machine remain unexecuted certification.
Unsigned trusted in-app update execution stays blocked by the existing policy.
No merge, public release, tag movement or production signing is performed.
