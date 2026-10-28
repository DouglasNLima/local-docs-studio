# Independent UI and export review — Draft PR 55 / Issue 54

Review date: 28 October 2026. Verdict for the requested head: **NO-GO**.

Base: `81bcb02f8a0ee618e70260a49a4aab8c0896c12d`.
Head: `74213763c9deff8554e0dc7f4ba1723995c91520`.

Scope: `DiagramArrangementWindow`, source/editor ownership, detached preview integration, inspection/selection, theme/accessibility, arranged SVG/PNG export and output failure behaviour. Root and native `AGENTS.md`, the reconstruction brief and smart-arrangement design/evidence were read. No product source, PR, branch, private SPC content, commits or remote state was changed. All diagnostic files are in this ignored directory.

## Evidence and method

An independent diagnostic executable (`Program.cs`, `ReviewUi.csproj`) references the existing Release assemblies without rebuilding shared projects. It creates shown native WPF windows, invokes the actual product handlers, pumps the Dispatcher and records state/events. This is actual WPF lifecycle evidence, not desktop mouse/keyboard evidence.

`run4/receipt.json` records four matching portable-PDB source checks for the arrangement window, both editors and MainWindow ownership integration. Thus the inspected compiled code matches the requested clean working tree for the reproduced paths. Native assembly SHA-256: `313F8DF00873313E9AD5BCC731CEA498CD234F7E657D859DC67CA6E0AFBD311C`. Core assembly SHA-256: `D6AD8452AD5E8893138E0CB0F7C1C936D7DBA4D5713271B0EC1184F986EE7230`.

The existing `--arrangement-windows` validation was independently rerun against the existing validation executable. It passed all 80 checks; receipt and window captures are in `existing-window-validation/`. This confirms the documented happy paths while the independent repros cover gaps in that harness. No actual High Contrast switch, large-DPI input, screen reader or private topology test was performed in this review. Terminal hosted CI is owned by the coordinating review, not inferred from local source documentation.

Reproduce the diagnostic from the repository root:

```powershell
dotnet build artifacts/native/final-acceptance-review-ui/ReviewUi.csproj -c Release --nologo
dotnet artifacts/native/final-acceptance-review-ui/bin/Release/net10.0-windows/ReviewUi.dll artifacts/native/final-acceptance-review-ui/new-run
```

The project deliberately references the current native Release output. Rebuild that product output before re-reviewing fixes, then use a fresh result directory. Earlier run receipts are retained.

## Findings

### UI-1 — P1: closing the owning editor silently discards a dirty arrangement

**Location:** `src/native/LensDocsStudio.Native/DiagramArrangementWindow.xaml.cs:60-64`; ownership is established at `MainWindow.MermaidVisualEditor.cs:167`.

The unsaved-layout confirmation exists only in the arrangement window's `Closing` event. WPF automatically closes owned windows when the owner closes, without raising the owned window's `Closing` event. Both editor close paths and application close currently close the editor without explicitly confirming/closing its arrangement children. The clean source editor has nothing of its own to prompt about.

**Repro:** open a clean ER or Flowchart visual editor; open Arrange; move a node without saving the layout; close the visual editor. The arrangement disappears without its discard prompt. The independent harness performed exactly this lifecycle with both concrete editor types. For each: `DirtyBefore=true`, `ClosingEvents=0`, `ClosedEvents=1`, `WindowClosed=true` (`run4/receipt.json`). This also applies when application shutdown closes the editor, and when a successful source Apply closes it.

**Impact:** loss of accepted, unsaved positions and pins through an ordinary parent-window action; the arrangement's documented discard protection is bypassed.

**Correction:** establish an explicit close/confirmation contract for owned arrangement windows, invoke it before editor close, Apply and application shutdown, and propagate cancellation so the entire owner hierarchy remains open. Test Yes/No, clean source with dirty layout, both editors, source Apply and app shutdown.

### UI-2 — P2: accepting a stale candidate removes pins added after the search

**Location:** `DiagramArrangementWindow.xaml.cs:168-176`, together with `143-147`.

Candidate choices contain only snapshots, with no generation from the search. An accepted manual/pin change does not clear the previously populated candidate panel. `AcceptCandidate` passes `session.Generation` at acceptance time, so the generation check cannot detect that the candidate was generated before the user's intervening changes.

**Repro:** generate Smart layouts; before selecting a candidate, select a node and Pin it; then choose and accept a candidate from the still-visible list. Using the same deterministic public dense Flowchart fixture as the repository (14 nodes, 36 edges, seed 2), the independent shown-window harness records two candidates, `PinBefore=true`, `PanelStillVisible=true`, `PinAfter=false` (`run4/receipt.json`). The accepted old candidate can also replace positions changed since its generation.

**Impact:** the explicit pin contract is broken and a candidate can silently overwrite newer accepted layout state. Serialising asynchronous work with `busy` does not protect a retained list across subsequent user actions.

**Correction:** bind each candidate set to the generation/state from which it was computed and validate that captured generation at preview/accept, or clear and invalidate every candidate on an intervening accepted mutation. Cover Generate → Pin/Move/Apply saved → Preview/Accept, including undoability and unchanged pins.

### UI-3 — P2: arranged SVG bypasses the appearance preparation used for the drawing and PNG

**Location:** `DiagramArrangementWindow.xaml.cs:301`.

The vector export writes raw `snapshot.Render.Svg`. The displayed drawing goes through `NativeSvgService.PrepareDocument` via the native presentation adapter. That preparation resolves variables, adds a painted background and adjusts inherited node text for contrast. The ordinary canonical SVG command already exports `svg.Prepare(value)` (`MainWindow.xaml.cs:563`). The new export therefore has different appearance from the accepted visible snapshot and PNG.

**Repro:** arrange a dark Flowchart containing `style A fill:#ffffff`. The raw export contains `fill="var(--_text)"`, which resolves to the light `--fg:#F3F4F6`, on the explicitly white node. The visible/prepared drawing correctly uses `#202124`. The raw SVG also has no explicit `native-background`, while the prepared SVG does. `run4/styled-raw-export.svg`, `styled-prepared.svg` and `styled-actual-drawing.png` retain the artefacts; `receipt.json` records the mismatches. The PNG was visually inspected and has readable dark text on that node.

**Impact:** vector recipients get nearly white-on-white node labels and a transparent canvas where the native view/PNG has an explicit dark canvas. Existing string-equality assertions compare the SVG export with the same raw renderer string, so they cannot detect this discrepancy.

**Correction:** export a prepared, verified vector representation consistent with the displayed native presentation, retaining SVG primitives and markers. Test styled light/dark nodes, actual vector content/background and label contrast rather than only raw-string identity. Keep PNG sourced from the immutable drawing to exclude inspection overlays.

### UI-4 — P2: arranged output bypasses the existing conflict/recovery writer

**Location:** `DiagramArrangementWindow.xaml.cs:298-302`, especially line 302.

`File.WriteAllBytesAsync` opens/truncates the destination directly. It does not capture expected destination bytes, detect a competing change during PNG preparation, stage a new output, or preserve an existing target on a partial write. The existing canonical export uses `ExportFileStore.ReadExistingAsync` and the established recoverable write flow (`MainWindow.xaml.cs:562-564`); this new path bypasses it. Its token is cancelled by owned-window closure, which is currently possible while work is active.

**Evidence level:** confirmed source-level regression in failure/conflict handling. A narrow immediate-cancel write diagnostic completed before cancellation; it did **not** reproduce a partial write. No disk-full or removable-drive failure is claimed. `run4/receipt.json` preserves that negative result honestly.

**Impact:** an error or cancellation after the destination is opened can leave an incomplete export and lose the old file; an externally changed destination is overwritten without conflict handling. This conflicts with the native instruction to preserve changed files and robust output handling.

**Correction:** route arranged SVG/PNG through the existing export service or an equivalent durable commit, capture expected bytes before conversion, and retain recoverable state on failure. Also reuse the canonical extension check: the current Save dialogue filter/AddExtension alone does not enforce the selected format. Test cancelled dialogues, failed new-file writes, replacement failure/recovery and a changed target during preparation.

## Contract coverage

| Contract | Assessment | Evidence / limit |
| --- | --- | --- |
| Source stays portable and layout history is separate | PASS | No source write in arrangement commands; existing rerun preserves exact source file and separate Undo/Redo. |
| Exact source/document ownership before async presentation and save/export | PASS with limitation | Context checks document membership, saving/path/root and source relocation; presentation checks owner after work. Existing stale-owner test passed. Parent-close data retention fails UI-1. |
| Stale candidate/generation rejection | FAIL | UI-2 independently reproduced. |
| Navigate default, manual moves, pins and same-size selection redraw | PASS for shown-window code paths | Existing 80-check rerun; independent pin/move handlers. Pointer-frame logic separates ghost from accepted drawing. |
| Real mouse pan, Space/middle drag, capture loss, wheel anchoring and physical arrow input | UNVERIFIED independently | Source handlers reviewed and hosted harness coverage inspected; no desktop-input claim from the local handler harness. |
| Canonical versus accepted arranged view and explicit exports | PASS for command separation | Exports are disabled for canonical/proposed snapshots; ordinary source-based export path remains separate. |
| Layout Undo/Redo/reset/revert | PASS for validated happy paths | Existing shown-window rerun checks restored snapshots/pins/selection. Candidate invalidation fails UI-2. |
| Dirty close, editor Apply close and app close | FAIL | UI-1 independently reproduces dirty child loss for both editor families. |
| Light/dark appearance refresh preserves positions/pins | PASS for local shown windows | Existing validation rerun switches appearance and rehydrates history while preserving saved state. SVG export appearance fails UI-3. |
| Actual High Contrast, large DPI, screen-reader usability | UNVERIFIED independently | Dynamic resources and UIA names reviewed; no OS mode/input certification made here. Existing hosted evidence must be checked separately. |
| SVG remains vector / full drawing, no UI overlay | PASS structurally; FAIL appearance | Raw SVG retains vector paths/text/markers, no bitmap wrapper; UI-3 produces different appearance. |
| PNG uses accepted full drawing without overlays or crop | PASS | Immutable `inspection.Drawing` is exported directly; source excludes Overlay/DragGhost, zoom and scroll. Existing exact-PNG checks and independent styled PNG generation. |
| Marker/label occurrence geometry and stale overlay invalidation | PASS within existing supported fixtures | Native presentation validates source/layout occurrence correspondence; same-size retained overlay invalidation explicitly rerun. Unsupported groups/shapes fail closed. Full private topology unverified. |
| Dialog cancellation and output failure/partial success | FAIL / UNVERIFIED fault injection | Cancellation before selection returns without writing. Durable/conflict path fails source review in UI-4; actual partial-write injection not reproduced. |
| Persistence store security/conflicts and engine layout quality | OUTSIDE primary review | Read integration; separate specialist review required. No conclusion about private SPC topology. |

## Recommendation

**NO-GO for acceptance of this head.** Fix UI-1 through UI-4, add focused outcome tests, rebuild the reviewed binaries and rerun the independent repros plus the existing native acceptance. Then reassess terminal hosted pointer/keyboard, High Contrast and regression results against the exact new head. No change to renderer deadlines or acceptance thresholds is suggested.

## Re-review 1 — uncommitted corrections, 28 October 2026

The correction build was reviewed independently. Native DLL SHA-256: `57689FDF94399682DD0403366F3D4C95A7820732B89EA409FDF415C7D0D6EF99`. `rereview/run3/receipt.json` verifies the four relevant portable-PDB source hashes against the current tree. The original requested head and its evidence above remain unchanged; these conclusions apply to the later uncommitted correction tree.

The separate `rereview/ReviewRereview.csproj` diagnostic was compiled against the new native output. It tests actual handlers in shown WPF windows with controlled discard answers. It does not inject desktop input or bypass the hosted-input restriction. An initial harness attempt deadlocked by synchronously waiting on the asynchronous export store on its UI thread; that was a harness error, corrected by pumping the Dispatcher before the completed run. The initial directories remain, and no product regression is attributed to them.

| Finding / contract | Resolution |
| --- | --- |
| UI-1 dirty arrangement loss on owner close | **RESOLVED / PASS.** Both concrete editor types now explicitly close arrangement children and propagate declined close. Independent checks cover editor close, `CloseForOwner`, pending-source rejection, declined source Apply without a source callback, then explicit discard permitting Apply and closure. |
| UI-2 old candidate losing a newer pin | **RESOLVED / PASS.** Accepted mutations invalidate the list; reinjecting an old candidate is rejected by its captured generation without losing the pin. |
| UI-3 vector export appearance mismatch | **RESOLVED / PASS.** The command prepares SVG. Independent prepared-SVG rasterisation equals accepted native PNG byte-for-byte; the styled light node retains contrasting text, a vector background, and no image wrapper. |
| UI-4 unsafe direct output write | **RESOLVED / PASS for covered failure paths.** Command now uses the existing `ExportFileStore`, expected bytes before conversion, extension validation, a post-conversion ownership check and the owning app's recovery location. Independent store checks preserve a competing external change and a pre-cancelled replacement, and commit the prepared output. Actual disk-full/mid-write hardware failure remains unverified; no new general export-store defect was found. |
| Pending editor draft ownership | **PASS.** Both ownership guards reject the original arrangement source while a new source draft is pending; an attempted arrangement mutation leaves the accepted snapshot untouched. |
| Candidate selection/acceptance agreement | **FAIL: UI-5 below.** |

### UI-5 — P2: a second candidate selection during preview accepts the first candidate

**Location in the correction tree:** `src/native/LensDocsStudio.Native/DiagramArrangementWindow.xaml.cs:191-193`, with `Run` dropping busy operations at line 82.

The candidate selector remains enabled while `PreviewCandidate` awaits native preparation. Selecting another item during that interval changes `CandidateChoices.SelectedItem`, but the second handler is dropped by `Run` because `busy` is true. `proposed` and the displayed preview remain the first snapshot. After rendering ends, `AcceptCandidate` reads that old proposed snapshot and the second choice's generation. Both candidates share the generation, so it accepts the first snapshot while the selector reports the second.

**Independent repro:** generate the two dense-Flowchart candidates, select Fewer crossings, immediately select Compact while the first preview is busy, wait, then Accept. `rereview/run3/receipt.json` records `BusyDuringSecondChoice=true`, `SelectorEnabled=true`, selected strategy `Compact (2)`, proposed and accepted strategy `FewerCrossings (3)`. This is a distinct race from UI-2; generation protection alone cannot identify the selected snapshot.

**Impact:** the user accepts a layout other than the one they selected, with different area/crossing trade-offs, violating explicit candidate acceptance.

**Correction:** disable candidate selection throughout preparation or queue/refresh the newest selection, and defensively require the accepted `proposed` snapshot to match the selected candidate. Add a focused rapid-selection regression that verifies the accepted snapshot is the latest selected/displayed candidate or that acceptance is rejected until preview matches.

**Recommendation after re-review 1: NO-GO until UI-5 is corrected and rerun.** The original four findings are resolved. Physical pointer/keyboard, actual High Contrast, DPI and final hosted results remain separately scoped acceptance conditions.

## Re-review 2 — final UI correction, 28 October 2026

**UI-5 is RESOLVED / PASS.** The candidate selector is disabled for the entire asynchronous operation, then restored in `finally`. Acceptance additionally requires `ReferenceEquals(proposed, candidate.Snapshot)`, so a mismatched proposal cannot be accepted even if a programmatic selection change bypasses the disabled UI.

The independent diagnostic was rebuilt only within this ignored review directory and rerun against the stable native DLL. No shared project was built or product source edited by this reviewer. DLL SHA-256: `6F6698F680BBA45265CA75AB28A413A96E3A8F57E081291ED9875D736A0B3349`. All four portable-PDB source checks match the correction tree.

`rereview/run4/receipt.json`: **23 checks PASS, zero failures**. The original rapid-selection reproduction now records `BusyDuringSecondChoice=true`, `SelectorEnabled=false`; forced programmatic mismatch leaves the accepted canonical snapshot intact (`Accepted=0`). After selecting Compact again and waiting for its preview, acceptance installs that exact selected snapshot. This verifies rejection and the subsequent successful user path. Original owner-close, source Apply, ownership, stale-pin, vector/native appearance, output conflict, cancellation and successful commit checks all remain passing in this run.

The preceding failing receipts and the original diagnostic source snapshot (`rereview/Program.run3.cs.txt`) remain available. The final diagnostic source is `rereview/Program.cs`.

### Final assessment for this UI/export review

- **PASS:** all five actionable findings UI-1 through UI-5 are resolved in the independently tested correction build.
- **PASS:** exact source ownership, separate accepted layout history, owner-close cancellation, source Apply arbitration, candidate generation/selection agreement, prepared vector appearance, full immutable PNG export and existing conflict/recovery write integration within the exercised cases.
- **UNVERIFIED by this reviewer:** physical mouse/keyboard on the correction build, actual High Contrast, large-DPI/screen-reader behaviour, disk-full/mid-write hardware failure, private SPC topology and final hosted/installed regression outcomes. Existing source/evidence review is not substituted for those runs.

**Recommendation: CONDITIONAL GO for the corrected UI/export scope**, conditional on the coordinating review's terminal regression/input results for the exact final revision. No remaining concrete UI/export defect was found. This supersedes the earlier NO-GO recommendations for the uncorrected head and first correction tree; it does not certify the separate engine, latency, private-topology or installation contracts.

