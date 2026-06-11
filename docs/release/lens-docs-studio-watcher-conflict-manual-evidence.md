# Lens Docs Studio Watcher/Conflict Manual Evidence

Status: Required evidence checklist for `v0.1.0-dev.1` certification.

This document records the human-only Windows picker pass for native workspace watcher and conflict behaviour. It must be completed against a packaged Windows shell or installed prerelease build before publishing `v0.1.0-dev.1`.

Do not mark items as passed unless a tester performed the action through the real Windows folder picker and observed the result manually. Automated browser tests and native smoke tests are supporting evidence, not a substitute for this checklist.

## Test Context

| Field | Value |
| --- | --- |
| Tester | Codex attempted packaged launch and folder-picker retry; manual watcher observations not completed. |
| Date | 2026-06-10 23:41:20 +01:00 |
| Build or installer asset | `LensDocsStudio.Windows-0.1.0-dev.zip` from the published `v0.1.0-dev` prerelease |
| Commit SHA | App asset version `0.1.0-dev+8b215d039188af94d32857239e6829916f4b74fc`; docs retry run from `a5e471e133351008f2a116aceefa980c63dcaad4` |
| Windows version | Microsoft Windows 11 Home `10.0.26200` |
| Install method | Extracted ZIP app from the published prerelease |
| Workspace path type | Temporary local folder under `%TEMP%\LDS-WatcherManual` |

## Phase 3U Attempt

Phase 3U used the existing published ZIP asset without modifying the `v0.1.0-dev` release, tags, or assets.

- Release checked: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`
- Asset source: `LensDocsStudio.Windows-0.1.0-dev.zip`
- Extracted packaged app: `%TEMP%\LensDocsStudio-v0.1.0-dev-release\extracted\LensDocsStudio.Windows.exe`
- Manual workspace: `%TEMP%\LensDocsStudio-WatcherManual`
- Test file: `watcher-evidence.md`
- Packaged launch result: the Windows process started from the extracted prerelease ZIP.

The required human-only watcher/conflict observations were not completed in this environment. The app launch and workspace preparation are supporting setup evidence only; they do not satisfy the manual picker checklist below.

## Checklist

- [ ] Open Lens Docs Studio in the Windows shell.
- [ ] Use **File > Open folder** and choose a real Windows folder picker workspace.
- [ ] Confirm the workspace browser loads supported Markdown, Mermaid, or text files with relative paths only.
- [ ] Open a Markdown file from the selected workspace.
- [ ] With no local edits, modify that file externally in another editor.
- [ ] Confirm the external clean change marker or status appears.
- [ ] Use **Refresh active file**.
- [ ] Confirm the editor reloads the external clean change.
- [ ] Modify the same file externally again.
- [ ] Make a local dirty edit in Lens Docs Studio before refreshing.
- [ ] Confirm the dirty/external-conflict marker or status appears.
- [ ] Cancel **Refresh active file**.
- [ ] Confirm the local dirty edit remains in the editor.
- [ ] Use **Refresh active file** again and confirm the prompt.
- [ ] Confirm refresh.
- [ ] Confirm the editor reloads the external content and the local dirty edit is discarded only after confirmation.
- [ ] Delete the active workspace file externally.
- [ ] Confirm Lens Docs Studio indicates the deleted state and does not silently erase the editor content.
- [ ] Rename a workspace file externally.
- [ ] Confirm Lens Docs Studio indicates or handles the rename safely, without exposing absolute paths or discarding unsaved edits.
- [ ] Reopen or switch files as needed and confirm the workspace remains usable after changed, deleted, and renamed events.

## Verdict

Result: **MANUAL_WATCHER_CONFLICT_BLOCKED**

The manual human-only watcher/conflict pass remains blocked because the required observation steps were not completed through the real packaged Windows UI and folder picker. The `v0.1.0-dev.1` watcher/conflict certification blocker is not cleared.

Scenario results:

| Scenario | Result | Notes |
| --- | --- | --- |
| Clean external change | BLOCKED | Packaged app launch was prepared, but the external-change marker and explicit refresh behaviour were not manually observed. |
| Dirty conflict cancel | BLOCKED | The local dirty edit preservation path was not manually observed. |
| Dirty conflict confirm | BLOCKED | The explicit confirmation reload path was not manually observed. |
| External delete | BLOCKED | The deleted-state marker and content-preservation behaviour were not manually observed. |
| External rename | BLOCKED | Rename handling was not manually observed. |

## Notes

- Phase 3S added this checklist only; it did not fake manual evidence.
- Phase 3T carried this requirement forward in `docs/release/lens-docs-studio-v0.1.0-dev.1-certification-plan.md`; it still must not be marked passed without a real packaged Windows picker pass.
- Phase 3U confirmed the published ZIP could be downloaded, extracted, and launched, but did not complete the human-only UI evidence. A future tester must still perform the checklist and update this verdict to `MANUAL_WATCHER_CONFLICT_PASS`, `MANUAL_WATCHER_CONFLICT_PASS_WITH_NOTES`, or `MANUAL_WATCHER_CONFLICT_FAILED`.
- Supporting automated coverage remains the native bridge smoke and focused browser regressions for watcher/conflict behaviour.
- If any checklist item fails, capture exact steps, screenshots where useful, affected file state, and whether content was preserved before deciding whether the issue blocks `v0.1.0-dev.1`.

## Phase 3U.1 Retry Attempt

Phase 3U.1 used the simplified step-by-step flow and did not modify the published `v0.1.0-dev` release, tags, or assets.

- Release checked: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`
- Asset source used: `LensDocsStudio.Windows-0.1.0-dev.zip` from the published `v0.1.0-dev` prerelease, because the installed app was not present at `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe`
- Extracted packaged app: `%TEMP%\LensDocsStudio-v0.1.0-dev-release\extracted\LensDocsStudio.Windows.exe`
- Manual workspace: `%TEMP%\LDS-WatcherManual`
- Test file prepared: `watcher.md`
- Packaged launch result: the Windows process started and showed the real packaged app UI.
- Setup result: the first-run setup screen was skipped through the visible packaged UI.
- Folder-picker result: blocked before workspace selection. Invoking **Open folder** showed an `Open` file dialog with Markdown/Mermaid file filters, and the app status reported `Native bridge did not respond.` This was not the required Windows folder picker and did not permit the `%TEMP%\LDS-WatcherManual` workspace to be selected as a folder.

Result: **MANUAL_WATCHER_CONFLICT_BLOCKED**

Blocker category: **MANUAL_WATCHER_CONFLICT_BLOCKED_FOLDER_PICKER**

Recommended next action: **add a temporary operator debug overlay** so the next manual run can confirm native bridge readiness and folder-picker availability before the watcher scenarios begin.

The `v0.1.0-dev.1` watcher/conflict certification blocker is not cleared.

Scenario results:

| Scenario | Result | Notes |
| --- | --- | --- |
| Clean external change | BLOCKED | Workspace folder could not be opened through the required Windows folder picker. |
| Dirty conflict cancel | BLOCKED | Workspace folder could not be opened through the required Windows folder picker. |
| Dirty conflict confirm | BLOCKED | Workspace folder could not be opened through the required Windows folder picker. |
| External delete | BLOCKED | Workspace folder could not be opened through the required Windows folder picker. |
| External rename | BLOCKED | Workspace folder could not be opened through the required Windows folder picker. |

## Phase 3U.2 Operator Diagnostics

Phase 3U.2 adds a temporary diagnostic aid for the next packaged Windows shell retry. It is diagnostic readiness only; it does not clear the watcher/conflict blocker and does not replace the manual checklist above.

Open **Help > Windows shell diagnostics** before selecting a watcher workspace. The report should be recorded with the next retry evidence and should not include screenshots or notes that reveal absolute local paths.

Required values before watcher manual evidence can proceed:

- **Running in Windows WebView2 shell**: `Yes`.
- **Ping result**: `Pass`.
- **Host**: `LensDocsStudio.Windows`.
- **Capabilities**: `diagnostics.ping`, `workspace.openFolder`, `workspace.watch`, and `workspace.refreshFile` must be present. `workspace.saveFile` and `workspace.createFile` should also be present for normal workspace operation.
- **Open folder will use native bridge**: `Yes`.
- **Browser fallback active**: `No`.

If native bridge ping fails:

- Do not begin watcher evidence.
- Record the ping result, host value if any, last native request, last native response, last native error, and whether the app was launched from the packaged `LensDocsStudio.Windows.exe`.
- Re-run the packaged native bridge smoke before attempting the manual watcher pass again.

If `workspace.openFolder` is missing:

- Do not begin watcher evidence.
- Record the full capability list from the diagnostics panel.
- Treat the build as unable to provide the required Windows folder-picker path for this checklist.

If **Open folder** routes to a file picker:

- Record that **Open folder will use native bridge** was `No` or that the native request failed.
- Record the **Browser fallback route** shown by the diagnostics panel.
- Record the visible picker type and filters, but do not include absolute paths or user-specific folder names.
- Keep the verdict as `MANUAL_WATCHER_CONFLICT_BLOCKED`.

Evidence fields for the next retry:

| Field | Value |
| --- | --- |
| Diagnostics opened from | `Help > Windows shell diagnostics` |
| Running in Windows WebView2 shell | |
| Ping result | |
| Host | |
| Capabilities present | |
| Open folder will use native bridge | |
| Browser fallback active | |
| Browser fallback route | |
| Last native request | |
| Last native response | |
| Last native error | |
| Operator next step shown | |
| Did **File > Open folder** show a real folder picker? | |

## Phase 3U.3 Manual Watcher/Conflict Evidence with Diagnostics (Current Develop)

Phase 3U.3 was run using a locally built package from current `develop` so diagnostics UI was present in the app under test.

- Build/validation checks: `npm run test:static`, `dotnet build src/windows/LensDocsStudio.Windows.sln`, `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`, `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke`, `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe`.
- Package source used: current develop publish output at `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` (built from commit `ab6e380685aae7d9aac45e3c6e516245a4767f75`).
- Smoke result: PASS for native bridge flow, including `workspace.openFolder`, `workspace.saveFile`, and `workspace.createFile` as required by existing smoke coverage.

### Stage A — Diagnostics Observation

Stage A has not been completed in this run because this environment could not collect the on-device diagnostics checklist interactively from the packaged UI.

- Stage A result: **DIAGNOSTICS_BLOCKED_OTHER** (Manual diagnostics collection not performed in this environment).
- Notes:
  - The release flow remained blocked at the previous gate because the previous blocker remains unresolved and the watcher checklist still requires a live picker pass.
  - The package produced for this phase passed static and native smoke validation, and does include the Phase 3U.2 diagnostic feature.

### Stage B — Manual Watcher/Conflict Evidence

- Stage B: **not run** (Stage A did not complete).
- Scenario results:
  - Clean external change: BLOCKED
  - Dirty conflict cancel: BLOCKED
  - Dirty conflict confirm: BLOCKED
  - External delete: BLOCKED
  - External rename: BLOCKED

### Verdict

Result: **MANUAL_WATCHER_CONFLICT_BLOCKED**

Overall verdict remains blocked.  
`v0.1.0-dev.1` blocker is **not cleared**.

Required follow-up:

- Run Stage A from a real packaged Windows shell instance via **Help > Windows shell diagnostics**, record the Stage A fields, and continue only if it passes.
- Then run Stage B scenarios 1–5 and update this document with scenario outcomes.

## Phase 3V Packaged Native Bridge / Open Folder Routing Fix

Phase 3V investigated the mismatch between automated packaged native bridge smoke passing and the manual packaged UI showing a file picker plus `Native bridge did not respond.` during **Open folder**.

Root cause found:

- The Open folder UI route used the same browser fallback pattern as browser/PWA mode after a missing or failed `workspace.openFolder` capability probe.
- In a WebView2 shell, that meant a stale, missing, or timed-out bridge capability check could fall through to browser picker/file-input behaviour instead of stopping with clear operator guidance.
- The automated native smoke calls bridge messages directly, so it could pass while the interactive UI route still exposed the fallback path.

Fix made:

- **File > Open folder** now performs a fresh `workspace.openFolder` capability probe.
- If the native bridge is present and `workspace.openFolder` is available, the action calls native `openFolder`.
- If the native bridge is present but the ping fails or `workspace.openFolder` is missing, the action does not open the browser fallback picker. It reports the problem and points the operator to **Help > Windows shell diagnostics**.
- Diagnostics now report **Open folder route**, fallback state, safe last native request/error values, and include **Retry bridge check**.

Manual route verification after the fix:

| Field | Value |
| --- | --- |
| Help > Windows shell diagnostics opened | Not completed in this document |
| Running in Windows WebView2 shell | Not recorded |
| Bridge ping | Not recorded |
| `workspace.openFolder` | Not recorded |
| Open folder route | Not recorded |
| Did **File > Open folder** show a real folder picker? | Not verified |

Result: **MANUAL_WATCHER_CONFLICT_BLOCKED**

The routing fix changes packaged runtime behaviour, so `v0.1.0-dev.1` now requires fresh ZIP and installer artefacts if published. Watcher/conflict evidence remains blocked until the fixed packaged app is launched manually, diagnostics pass, and the full checklist above is completed through the real Windows folder picker.
