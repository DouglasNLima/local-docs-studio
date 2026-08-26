# Phase 3AW Open Folder Picker Remediation

Date: 2026-06-12

## Summary

Phase 3AW investigated the packaged **Open folder** native picker failure recorded in Phase 3AV for the published `v0.1.0-dev.2` Windows ZIP package.

Phase 3AV failure summary: onboarding and diagnostics passed in the real published packaged app, but **Open folder** stayed at `Opening folder from Windows...`; no usable native folder picker result returned, and watcher/conflict checks were blocked.

Phase 3AW made a runtime hardening change for the native Open folder pending path. Production readiness, go-live approval, stable-channel certification, and production release status are not claimed.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Phase 3AV commit in history | PASS, `63ce12a5245ec939c363bf54f7646671a8a78036` is an ancestor of `HEAD` |
| Tracked status before changes | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

## Published Package Reproduction

Published executable checked:

```text
C:\Code\MarkdownReader\artifacts\phase-3av-v010-dev2\zip-extract\LensDocsStudio.Windows.exe
```

Launch/control method: visible packaged executable with WebView2 remote debugging enabled as an observation/control aid on `127.0.0.1:9230`.

Observed result in Phase 3AW:

| Check | Observation |
| --- | --- |
| Onboarding renders | PASS |
| Diagnostics pass | PASS |
| Open folder route | Native bridge |
| `workspace.openFolder` capability | Available |
| Browser fallback | Inactive in packaged WebView2 |
| Open folder status after click | `Opening folder from Windows...` |
| Native picker window | Visible `Select Folder` dialog, class `#32770` |
| Dialog ownership | Owned by the Lens Docs Studio main window |
| Dialog position | On-screen |

Phase 3AW could not reproduce the exact Phase 3AV "no native picker appeared" symptom in the same local environment. The published package failed in Phase 3AV but showed a visible, owned native picker in this retry.

## Current Develop Comparison

A fresh package was built from current `develop` before the remediation edit:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke
```

Fresh executable checked:

```text
C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev\LensDocsStudio.Windows.exe
```

Observed result:

| Check | Observation |
| --- | --- |
| Onboarding renders | PASS |
| Diagnostics pass | PASS |
| Open folder route | Native bridge |
| Browser fallback | Inactive in packaged WebView2 |
| Native picker window | Visible `Select Folder` dialog, class `#32770` |
| Cancellation | Closing the picker returned `Open folder cancelled.` |

Classification: published package failed in Phase 3AV, but both the published package and current `develop` showed a visible native picker in Phase 3AW. The original no-dialog symptom is therefore treated as not reproduced in this retry, with an environment/session-specific possibility remaining.

## Diagnosis

Diagnostics and Open folder use the same native bridge client/session. The diagnostics ping and capability check reached the native host and reported `workspace.openFolder` as available.

The Windows native service already marshals picker work to the UI dispatcher when needed, resolves the owner HWND at invocation time, activates/restores/foregrounds the app window, initialises the folder picker with the owner HWND, returns `{ cancelled: true }` for cancellation, and returns structured safe native errors for attach/show failures.

Root cause found in Phase 3AW: the exact Phase 3AV no-picker condition was not reproduced. A related product defect was confirmed in the browser runtime path: after dispatching a native Open folder request, the app could remain on `Opening folder from Windows...` for an extended interactive timeout without bounded operator guidance if the native picker was hidden, blocked, or otherwise not completing.

## Fix

Runtime code changed.

The Open folder native route now starts a short pending notice timer after dispatching `workspace.openFolder`. If the native request has not completed after the bounded wait, the app:

- keeps browser fallback inactive;
- records the Open folder attempt as `pending`;
- shows `Still waiting for the Windows folder picker. Check for a visible Select Folder window, then choose a folder or cancel.`;
- exposes the same pending state and bounded message in Windows shell diagnostics;
- still accepts later native success or cancellation normally.

The fix does not add browser fallback in packaged WebView2, hidden workspace injection, debug-only shortcuts, command-line workspace bypasses, localStorage bypasses, or fake workspace selection.

## Tests Added Or Updated

- Added browser coverage that the welcome **Open folder** action uses the native bridge route in packaged/fake WebView2 mode and does not call browser `showDirectoryPicker`.
- Added browser coverage for a non-returning native `openFolder` request: the app shows bounded pending guidance, records diagnostics as `pending`, and keeps browser fallback inactive.
- Existing cancellation, malformed response, native error, delayed native response, fallback-blocking, diagnostics, onboarding, and watcher/conflict browser coverage remains in place.

Focused validation before the full sweep:

| Command | Result |
| --- | --- |
| `npx playwright test tests/browser/app-smoke.spec.mjs --grep "welcome Open folder|native open folder pending" --reporter=line` | PASS, 4 tests across Chromium and Microsoft Edge |

## Manual Packaged Verification

Manual packaged verification was run against the freshly rebuilt Phase 3AW package:

```text
C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev\LensDocsStudio.Windows.exe
```

Temporary workspace:

```text
C:\Temp\LensDocsStudio-Phase3AW
```

Observed result:

| Check | Result |
| --- | --- |
| Onboarding renders | PASS |
| Diagnostics opens | PASS |
| Retry/diagnostic bridge state | PASS, bridge ping and `workspace.openFolder` available |
| Open folder opens real native picker | PASS, visible `Select Folder` dialog |
| Picker visible and usable | PASS |
| Selecting temporary workspace completes | PASS, `phase-3aw.md` loaded from Windows |
| Cancellation returns clean state | PASS, `Open folder cancelled.` |
| Browser fallback remains inactive | PASS |
| Watcher/conflict copy path reachable | PASS, external edit produced `This file changed on disk. Use Refresh active file to read the disk version.` and `externalChanged` state |

Remote debugging and Win32 dialog control were used only to observe and operate the visible packaged UI and native picker. No workspace was injected into the app, and no browser fallback or hidden bypass was used.

## Remaining Limitations

The exact Phase 3AV no-dialog/no-return condition was not reproduced during Phase 3AW. The remediation improves the operator-facing pending path if that condition recurs, but a future prerelease package should still be manually checked in a clean Windows session.

Fresh Windows package artefacts are required for any future `v0.1.0-dev.3` or equivalent prerelease that includes this runtime change.

## Validation

Full validation for the Phase 3AW runtime remediation:

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | PASS | Completed 236 Playwright browser smoke tests across Chromium and Microsoft Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored local RC report `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260612T141139Z.md`; ZIP SHA256 `5292D8E845D9C2985BEFF04C466D48700C141BFC4F045A9546CD56920053AFCE`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Final ignored local Windows folder/ZIP package rebuilt; native smoke intentionally skipped by this package command flag. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the final freshly rebuilt executable. |

## Evidence Boundary

- Phase 3AW did not edit, delete, replace, re-upload, rebuild, or republish `v0.1.0-dev.2` release assets.
- Phase 3AW did not edit `v0.1.0-dev.1` release assets.
- Phase 3AW did not create tags or releases.
- Phase 3AW did not merge to `main`.
- Phase 3AW used only temporary local workspaces under `C:\Temp\LensDocsStudio-Phase3AW`.
- Phase 3AW did not overwrite user data.
- Phase 3AW does not claim production readiness or go-live approval.
