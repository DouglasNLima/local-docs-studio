# Phase 3AK Diagnostics Visibility Polish

Date: 2026-06-11

## Status

Phase 3AK is the first `v0.1.0-dev.2` implementation slice on `develop`.

This is not a release approval, does not claim production readiness or go-live approval, does not merge to `main`, does not create tags or releases, and does not edit, rebuild, replace, upload, or republish `v0.1.0-dev.1` release assets.

## Why This Was Needed

The `v0.1.0-dev.1` evidence chain proved the packaged diagnostics, native folder picker, cancellation path, and watcher/conflict scenarios in Phase 3AA/3AB, but the diagnostic panel still compressed several different support states into broad yes/no wording.

Phase 3AK makes the bridge and Open folder route easier to report when a user or tester needs to distinguish a missing bridge, a failing ping, a missing `workspace.openFolder` capability, a blocked WebView2 fallback, a browser/PWA fallback, cancellation, timeout, and native picker errors.

## User-Visible Changes

- The Windows shell diagnostics dialog now shows a pending state while the retry check is running.
- Diagnostics now report **WebView2 shell detected** as `Yes`, `No`, or `Unknown`.
- The bridge ping and `workspace.openFolder` capability are shown separately.
- `workspace.openFolder` is reported as `available`, `missing`, `failing`, or `pending`.
- The Open folder route now distinguishes native bridge routing from blocked packaged WebView2 routing and browser/PWA fallback routing.
- Browser fallback is shown as inactive in packaged WebView2, available only in browser/PWA context when supported, or unavailable in the current browser.
- The last Open folder attempt is recorded safely as not attempted, native picker opened, folder selected, user cancelled, native error, timeout, bridge unavailable, capability missing, browser picker opened, or browser picker error.
- Native Open folder error text shown in status and diagnostics is bounded and redacts common local path shapes.

## Route And Fallback Boundaries

- Packaged WebView2 with a healthy `workspace.openFolder` capability continues to use the native bridge.
- Packaged WebView2 with a present bridge but missing or failing `workspace.openFolder` stays blocked and does not fall through to the browser picker.
- Browser/PWA mode can still use `showDirectoryPicker` or the existing file-input fallback where supported.
- Cancellation remains distinct from native error.
- Native picker timeout remains distinct from other native errors.

## Runtime And Package Impact

Phase 3AK changes browser runtime JavaScript and browser smoke tests. Because packaged Windows builds include the static app assets, fresh package artefacts would be required before any future `v0.1.0-dev.2` publication from this commit or a descendant.

No installer or release artefact was built, uploaded, replaced, or republished by this evidence note.

## Validation Results

Record validation after the Phase 3AK implementation commit:

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | PASS | Completed 228 Playwright browser smoke tests across Chromium and Microsoft Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings for release documentation filenames and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored local RC report `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T210301Z.md`; ZIP SHA256 `1B34BC7EB2C3E46CEAEB2063BD0AB889B203975D27E17104F36019CC901CAC3F`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Rebuilt ignored local package folder and ZIP; native smoke intentionally skipped by this command flag. |
| Packaged native smoke against freshly rebuilt package executable | PASS | `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` passed. |

## Evidence Boundary

Phase 3AK does not alter the `v0.1.0-dev.1` release, release assets, tags, releases, or `main`.

Production readiness, go-live approval, stable-channel certification, and `v0.1.0-dev.2` publication remain unclaimed.
