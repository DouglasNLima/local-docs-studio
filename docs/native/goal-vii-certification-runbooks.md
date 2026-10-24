# Goal VII: outstanding Windows certification protocols

Recorded 24 October 2026. These are executable/manual prerequisites, **not passed
tests**. See [current decisions](current-windows-readiness.md). Use synthetic/copied
fixtures only. A release owner must authorise the VM/image/snapshot and operator;
an ordinary desktop or `windows-latest` developer-tools image cannot substitute
for a clean Windows 11 machine. Never disable Windows protections or install a
production certificate. Retain original failed evidence and stop on a failure.

## Common evidence contract

For each session record full Git SHA/tree, exact installer/portable version/hash,
branch-source versus merge checkout, run/attempt, UTC times, VM/snapshot identity,
OS edition/build/UBR and image provenance, standard/elevated user context, missing
tools and external prerequisites. Inspect packages independently on a validation
station without executing their binaries. Use a new evidence directory each time.
Retain initial/final source bytes, workspace/state SHA-256 manifests, screenshots,
UIA trees, focus AutomationId/Name, caret/selection/scroll, actual export dimensions,
and failures. Redact user/machine identifiers from shared records.

```powershell
Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber
Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' |
    Select-Object DisplayVersion, CurrentBuildNumber, UBR
whoami /groups
Get-FileHash '<installer>' -Algorithm SHA256
Get-FileHash '<portable.zip>' -Algorithm SHA256
```

The commands below use explicit placeholder paths to immutable evidence; substitute
them before execution. A package verifier needs PowerShell 7/.NET on the validation
station. Do not install validation SDKs or tools into the clean test VM to make a
clean-machine claim. The environment preflight supports Windows PowerShell and
must run before adding any optional tool.

## Actual interactive DPI: NOT RUN / BLOCKED

1. Start an authorised disposable VM with interactive Windows display settings.
   Record snapshot, adapter/monitor identity, native resolution, current scale and
   display orientation. Prove that the driver supports real per-monitor scaling;
   a resolution change alone is insufficient. If unavailable, record BLOCKED.
2. On that VM only, use **Settings → System → Display → Scale** to select 125%.
   Sign out/restart if Windows requires it. Capture Settings before/after and record
   the Windows-reported effective DPI of the actual top-level app HWND and monitor,
   plus process awareness. Expected effective window DPI is 120 at 125%, 144 at
   150%, 192 at 200%; record actual values, never infer them from a screenshot.
3. Query `GetDpiForWindow(HWND)`, `MonitorFromWindow(HWND, MONITOR_DEFAULTTONEAREST)`,
   `GetDpiForMonitor(HMONITOR, MDT_EFFECTIVE_DPI, ...)` and
   `GetProcessDpiAwareness(processHandle, ...)` via a read-only Win32 probe. Record
   probe process/thread awareness as well: virtualised monitor DPI from an unaware
   probe is not independent proof. Use the actual app process/window identity,
   not the PowerShell window, another process or an offscreen WPF surface.
4. At 880×600 and 1280×800 logical window sizes, exercise all 24 matrix families.
   Tab/Shift+Tab from occurrence list through inspector/Update/Reset, history and
   Apply/Cancel. Retain real focused element and source caret/selection. Scroll
   source and inspector independently; toggle the source panel, resize and verify
   offsets/pending errors remain. Check full names, clipped controls and footer.
5. Click actual canonical geometry at known source occurrences (including Packet
   multirow fields and Venn overlap). Verify pointer-to-occurrence correspondence
   and protected/unrendered keyboard access. Fit, zoom, pan, apply/owner Undo/Redo;
   reopen owner and restart app. Export PNG and record actual pixel width/height
   from the saved file, source and file hashes; do not resample captures.
6. Repeat at 150% (optionally 200%) from a recorded state. If two real monitors are
   available, move the window between different scales, record both HWND/monitor
   DPIs and repeat pointer/focus/export checks. Otherwise mark multi-monitor NOT RUN.
7. Restore the VM snapshot. A RenderTargetBitmap DPI argument, text-size setting,
   resized screenshot or the existing temporary resolution helper is not this test.

## Physical screen reader: NOT RUN

On an authorised disposable interactive Windows VM, record the actual installed
Narrator/NVDA/JAWS version, voice, keyboard layout and operator. UIA-only tests do
not count. Begin with all five specialist windows and one shared window; then
sample **every remaining family** using the matrix (24 total, C4 Context/Container/
Component sampled within its one family). For each:

1. Open Visual Edit by keyboard. Record initial focus and the audible window/family
   announcement. Traverse Tab and Shift+Tab to the full occurrence list, inspector,
   pending Update/Reset, private history, Apply and Cancel. No invisible trap.
2. Select duplicate/long/unrendered/protected occurrences; record the spoken exact
   occurrence, selected state, property label/value and protection explanation.
   Use Locate/Edit source; verify source caret/selection and return focus.
3. Type invalid pending input, attempt Update/Apply/selection, and record audible
   status/error/disabled-action explanation without losing input. Reset and commit
   a valid private edit. Test private Undo/Redo and dirty close/discard/cancellation.
4. Apply, verify owner focus, one-step document Undo/Redo and saved source. Reopen,
   Cancel and confirm exact owner preservation. Repeat essential traversal under
   actual OS High Contrast and both minimum sizes. Retain an operator transcript
   of audible outcomes and failures, not merely a UIA tree or synthetic assertion.

## SDK-free clean Windows 11: BLOCKED / NOT RUN

1. Release owner supplies an authorised x64 Windows 11 VM/snapshot with verifiable
   OS provenance and standard-user account, no prior Lens native/legacy app, SDK,
   Visual Studio, MSBuild, Node/npm, Python or other development tools. Inspect
   PATH, installed-app inventory, SDK roots and files outside PATH. `windows-latest`
   cannot satisfy this. Record networking and external prerequisites.
2. Before installation run the read-only preflight from a copied script:

   ```powershell
   powershell -NoProfile -File .\Test-NativePreviewPreflight.ps1 -EnvironmentOnly -OutputDirectory C:\Evidence\clean-before
   ```

   PARTIAL preflight is not a pass; separately establish VM/OS/user provenance.
   BLOCKED means stop qualification. Independently inspect and hash both exact
   packages on the validation station. Move only the verified bytes into the VM.
3. Capture the complete visible installer wizard as standard user: custom path,
   task summary, Back/Cancel, install/Finish, publisher warnings and files-in-use
   refusal with an unsaved synthetic document. Use the established internal
   **no-file-associations** policy; retain normal product defaults. Never change
   Windows default apps or bypass trust restrictions. A policy refusal is evidence.
4. First launch without an SDK/server/browser runtime. Record loaded CoreCLR/WPF
   paths from the bundled installation, About version/source, a new workspace,
   Markdown editing/save/reopen, relative image insertion and a native diagram.
   Export real DOCX/PDF/PNG/HTML/Docs Site; inspect files/Unicode/colour and preserve
   source/image hashes. The product's runtime claim must not rely on validation
   tools that were installed into the VM.
5. Restart and verify saved and unsaved session recovery. Run the checklist below,
   uninstall, confirm ordinary workspace and private-state preservation, then
   reinstall the same exact package and verify restored tabs/settings/source.
   Record final absence of native binaries/registration after final uninstall.
6. Retain failed stages and restore the snapshot. No ordinary desktop installation.

## Historical upgrade: NOT CERTIFIED

Read `tests/fixtures/native-historical-previews.json` and
`scripts/native/Test-NativeHistoricalPackage.ps1`. Its six original version/source/
run/artifact/SHA-256 pins are immutable. The `historical-upgrade` selector in
`native.yml` remains restricted to the integration branch; skipped is not PASS.
Do not relax that selector for this goal or manufacture a historical version.

A separately authorised disposable historical session needs the original immutable
installer/portable bytes **and the actual original application/state/workspace**.
Verify original pins, inventory and back up state, then run the original validation
project against the historical installation before upgrading. Preserve pre/post
hashes and actual restored tabs/templates/images/library/recovery. If bytes,
original state or authorisation are unavailable, keep NOT CERTIFIED. The ordinary
24 × 3 installed phases and `0.1.0` → `0.1.1` same-source pair prove synthetic
lifecycle only; they establish no historical lineage or `.749`/`.853` root cause.

## Release candidate and rollback/data-preservation checklist

| Scenario | Required observation | Available evidence level |
|---|---|---|
| Before upgrade | Close app normally; hash/copy complete synthetic workspace and `%LOCALAPPDATA%\LensDocsStudio.Native` including recovery/session/preferences/library/templates; record versions and relative roots | Existing hosted synthetic lifecycle; clean/historical VM still required |
| Dirty saved document | Restart/upgrade does not overwrite external changes; exact dirty buffer and original saved bytes separately preserved; conflict/cancel leaves both recoverable | Existing recovery/owner tests; manual VM prerequisite |
| Untitled draft/session | Exact Unicode text, tab order, selection, images and pending recovery survive restart; Cancel never discards without consent | Existing synthetic session evidence; exhaustive selection/caret manual |
| Templates/images/content library | Original bytes, chosen template, relative links and portable workspace relocation remain usable | Existing synthetic export/workspace tests; manual recipient inspection |
| Clean install/uninstall/reinstall | Real wizard, standard-user path, bundled runtime, preserved data, restored state; final registration removal | Hosted developer-tools image only until clean VM certified |
| Open with/default apps | Compare native/legacy ProgIDs, .md/.mmd/.mermaid and UserChoice snapshots before/after internal test; no associations selected | Existing internal exclusion policy; no optional association change authorised |
| Safe failure/cancellation | Installer Cancel/Back/files-in-use, denied destination, interrupted save and invalid source leave previous data recoverable | Existing synthetic controls; real interruption/hardware manual |
| Rollback | Stop app; archive failed current state first; restore backup to a separate copied workspace/profile in a disposable snapshot, with the matching original version; verify hashes before reopening | Manual protocol, not certified downgrade/migration |

Never overwrite newer data with an old backup in place. Preserve failed state and
recovery files, compare hashes, obtain explicit data-owner consent for any later
destructive restore, and keep an untouched copy. Uninstall is not permission to
delete workspaces/private state. Production signing, updater manifests/feeds,
release/tag/Pages publication and optional association certification require a
separate decision; this goal authorises none of them.
