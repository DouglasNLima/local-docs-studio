# Lens Docs Studio v0.1.0-dev.2 Phase 3AV Manual Packaged Sanity Evidence

Evidence date: 2026-06-12

## Summary

Phase 3AV attempted real manual packaged sanity against the published `v0.1.0-dev.2` Windows artefacts. This checkpoint is documentation/evidence-only: it did not change runtime code, rebuild packages, rebuild installers, edit release assets, create tags, create releases, or merge to `main`.

Manual packaged sanity result: **PACKAGED_MANUAL_SANITY_FAIL**

The first-run/onboarding and diagnostics checks were observed in the real packaged app. The native folder picker check failed: selecting **Open folder** in the packaged WebView2 app changed the app status to `Opening folder from Windows...`, but no native `Select Folder` picker appeared in the Windows desktop session and the app did not return a selected or cancelled folder result. The watcher/conflict checks could not be completed because a workspace could not be selected through the packaged native folder picker.

No production readiness, go-live approval, stable-channel certification, or production release status is claimed.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Start commit | `0809f52b92330e4aefdf4db129fd71c3c55831bd` |
| Phase 3AU commit in history | PASS, `0809f52b92330e4aefdf4db129fd71c3c55831bd` is `HEAD` and an ancestor of `HEAD` at the start of the checkpoint |
| Tracked status at start | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |
| Downloaded verification assets | Stored under ignored path `artifacts/phase-3av-v010-dev2/` |

`git status --short --ignored` emitted existing Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. The tracked working tree remained clean before the Phase 3AV documentation edit.

## Release And Artefact Verification

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.2`

| Check | Result |
| --- | --- |
| Release tag | PASS, `v0.1.0-dev.2` |
| Release state | PASS, prerelease |
| ZIP artefact used | `LensDocsStudio.Windows-0.1.0-dev.zip` downloaded from the published release |
| Installer artefact verified | `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` downloaded from the published release |
| ZIP SHA256 | PASS, `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B` |
| Installer SHA256 | PASS, `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |

The ZIP was extracted only under the ignored temporary evidence folder:

```text
C:\Code\MarkdownReader\artifacts\phase-3av-v010-dev2\zip-extract
```

Packaged executable used:

```text
C:\Code\MarkdownReader\artifacts\phase-3av-v010-dev2\zip-extract\LensDocsStudio.Windows.exe
```

## Environment

| Field | Value |
| --- | --- |
| OS | Microsoft Windows 11 Home `10.0.26200`, 64-bit |
| App package version observed | `v0.1.0 (build 58)` |
| Packaged app version in WebView2 process | `0.1.0-dev+5602358ef241c144fccae13e980c9eb2920126d8` |
| WebView2 runtime observed | `149.0.4022.62` |
| Temporary workspace prepared | `C:\Code\MarkdownReader\artifacts\phase-3av-v010-dev2\manual-workspace` |
| Temporary test file prepared | `phase-3av-sanity.md` with non-private synthetic content |
| Observation/control aid | WebView2 remote debugging enabled with `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9230` after coordinate/UI Automation control proved insufficient |

The remote debugging port was used only to observe and click controls in the visible packaged WebView2 app. It did not modify the published package, release artefacts, tags, releases, or app source.

## First-Run And Onboarding

Result: **PASS**

Observed in the real packaged app after launching the extracted published ZIP executable:

| Check | Observation |
| --- | --- |
| Onboarding copy renders | PASS, Windows setup dialog rendered with `Welcome`, `Set up the local Windows desktop shell.`, and generic Lens Docs Studio local documentation studio wording |
| Open folder primary action | PASS, after skipping setup the empty-state and preview onboarding showed **Open folder** before **Open file**; the preview action was styled as the primary action |
| Open file remains available | PASS, **Open file** was visible in the file panel and preview onboarding |
| Diagnostics reachable | PASS, **Diagnostics** was visible from the first-run/empty-state actions and opened the diagnostics panel |
| Files-stay-local copy | PASS, preview onboarding stated: `Your files stay local unless you choose to save, copy, export, or import content.` |

No private document content was used or captured for this section.

## Diagnostics

Result: **PACKAGED_DIAGNOSTICS_PASS**

Diagnostics was opened from the first-run/empty-state **Diagnostics** button. **Retry bridge check** was clicked once and the diagnostic values remained healthy.

| Check | Observation |
| --- | --- |
| Diagnostics panel opens | PASS |
| Retry bridge check works | PASS |
| Running in browser/PWA | `No` |
| WebView2 shell detected | `Yes` |
| Bridge message handler registered | `Yes` |
| Bridge ping | `Pass` |
| Protocol version | `1` |
| Host | `LensDocsStudio.Windows` |
| Last native request | `lensDocs.native.ping` |
| Last native response | `lensDocs.native.pong` |
| Last native error | `None` |
| `workspace.openFolder` capability | `available` |
| Route decision | `native bridge` |
| Open folder will use native bridge | `Yes` |
| Browser fallback state | `inactive in packaged WebView2` |
| Browser fallback route | `Not active` |
| Diagnostics copy | Bounded, operator-oriented, and did not include private document content |

Capabilities observed:

```text
diagnostics.ping
file.open
file.save
file.saveAs
workspace.openFolder
workspace.saveFile
workspace.createFile
workspace.watch
workspace.refreshFile
```

## Native Folder Picker

Result: **FAIL**

Steps performed against the real packaged app from the published ZIP:

1. Launch `C:\Code\MarkdownReader\artifacts\phase-3av-v010-dev2\zip-extract\LensDocsStudio.Windows.exe`.
2. Skip the setup dialog to reach the empty-state onboarding surface.
3. Open **Diagnostics** and confirm WebView2/native bridge routing is healthy.
4. Close diagnostics.
5. Click **Open folder** from the empty-state file panel.

Observed result:

- The app status changed to `Opening folder from Windows...`.
- The **Open folder** button received the active/focus state.
- No browser fallback picker appeared.
- No visible native `Select Folder` picker appeared in the desktop session.
- UI Automation top-level window enumeration found the packaged Lens Docs Studio window and WebView pane, but no native picker dialog.
- After an additional wait, the app still reported `Opening folder from Windows...`.
- No selected workspace result and no clean cancelled state reached the app.

Expected result:

- A real native Windows folder picker should appear.
- Selecting `C:\Code\MarkdownReader\artifacts\phase-3av-v010-dev2\manual-workspace` should load the workspace.
- Cancelling a second picker attempt should return a clean cancelled state without showing browser fallback.

Because the picker did not appear, the native picker selection and cancellation checks failed for this packaged release.

## Watcher And Conflict Copy

Result: **BLOCKED_BY_NATIVE_PICKER_FAILURE**

The watcher/conflict checks were not executed because the temporary workspace could not be selected through the real packaged native folder picker.

| Scenario | Result | Reason |
| --- | --- | --- |
| Clean external change uses clear changed-on-disk wording | BLOCKED | Workspace not opened through native folder picker |
| Dirty conflict prompt uses app/disk language | BLOCKED | Workspace not opened through native folder picker |
| Keep app edits keeps unsaved app edits | BLOCKED | Workspace not opened through native folder picker |
| Use disk version reloads the disk version | BLOCKED | Workspace not opened through native folder picker |
| Diagnostics/evidence excludes private document content | PASS for attempted evidence | Only synthetic temporary Markdown content was prepared; it was not opened in the app |

No watcher/conflict pass is inferred from previous releases, local development builds, automated tests, or diagnostics alone.

## Classification

Final Phase 3AV classification: **PACKAGED_MANUAL_SANITY_FAIL**

Failure category: **NATIVE_FOLDER_PICKER_NOT_VISIBLE_OR_COMPLETING**

The failure is product-facing in the packaged `v0.1.0-dev.2` flow: diagnostics reports the native bridge and `workspace.openFolder` capability as available, but the manual **Open folder** action remains pending without presenting a usable native picker or returning a result.

## Follow-Up Recommendation

Do not fix runtime code in Phase 3AV. Recommended follow-up:

- Investigate the packaged `workspace.openFolder` host path for `v0.1.0-dev.2`, focusing on picker owner HWND activation, picker show/completion, and whether the bridge request can remain pending without surfacing a structured native error.
- Add or reuse a focused packaged-app regression that can detect `workspace.openFolder` pending indefinitely after the UI route calls native Open folder.
- Preserve the current no-browser-fallback behaviour while improving the native picker failure path so the operator receives a bounded diagnostic error instead of an indefinite pending state.
- Re-run manual packaged sanity only against a newly authorised package or prerelease after a runtime remediation is explicitly approved.

## Validation

Validation requested for this documentation/evidence-only checkpoint:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, build succeeded with 6 existing PRI qualifier warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, built the Windows shell and verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp/` |

The following were intentionally not run because Phase 3AV is documentation/evidence-only and the instructions explicitly excluded them:

- `npm run test:browser`
- package rebuilds
- installer rebuilds
- release commands
- automated package smoke

## Evidence Boundary

- Phase 3AV did not edit, delete, replace, re-upload, rebuild, or republish release assets.
- Phase 3AV did not create, move, or delete tags.
- Phase 3AV did not create, edit, publish, unpublish, or delete GitHub Releases.
- Phase 3AV did not merge `develop` to `main`.
- Phase 3AV did not change runtime code.
- Phase 3AV did not rebuild package or installer artefacts.
- Phase 3AV did not change `v0.1.0-dev.1` release assets.
- Phase 3AV did not overwrite user data.
- Phase 3AV used ignored temporary workspaces and files under `artifacts/phase-3av-v010-dev2/`.
- Phase 3AV did not claim production readiness or go-live approval.
