# Phase 3AL First-Run Onboarding Copy Polish

Date: 2026-06-11

## Status

Phase 3AL is the second `v0.1.0-dev.2` implementation slice on `develop`.

This is not a release approval, does not claim production readiness or go-live approval, does not merge to `main`, does not create tags or releases, and does not edit, rebuild, replace, upload, or republish `v0.1.0-dev.1` release assets.

## Why This Followed Diagnostics Visibility

Phase 3AK made bridge and Open folder diagnostics clearer. Phase 3AL uses that improved diagnostics entry point in the first-run and empty-state experience so new users have a calmer path:

- Start with a documentation folder when they want workspace-style browsing and watching.
- Use Open file for a single Markdown, Mermaid, or text document.
- Use Diagnostics if Open folder does not work in the Windows desktop shell.
- Keep safe expectations that files stay local unless the user chooses to save, copy, export, or import content.

## User-Visible Copy Improvements

- The welcome state now uses the generic product positioning: "Local Markdown, Mermaid, and documentation studio".
- The primary welcome action is Open folder.
- Open file remains available as the single-document path.
- Diagnostics is available directly from the welcome and empty file-list states.
- The sidebar empty state explains folder mode as workspace-style browsing and watching.
- The Windows first-run setup wizard explains packaged local files and the Windows bridge in simpler terms.
- The setup workspace step points users to Windows shell diagnostics if Open folder does not work.

## Behaviour Changes

No file, folder, bridge, fallback, import, export, save, watcher, or package behaviour changed.

The only runtime interaction change is that existing Windows shell diagnostics can now be opened from the welcome and empty file-list states. Browser fallback remains blocked while a native WebView2 bridge is present but `workspace.openFolder` is missing or failing.

## Tests Added Or Updated

- Added browser coverage for first-run welcome copy, local-file expectations, Open folder as the primary action, Open file availability, Diagnostics availability, and absence of release-readiness wording.
- Added browser coverage that the empty file-list state keeps Open folder first and opens Diagnostics.
- Extended Windows setup wizard browser coverage for packaged local file wording, no-backend wording, workspace guidance, and Diagnostics recovery copy.
- Existing packaged WebView2 fallback-blocking diagnostics tests remain in place.

## Runtime And Package Impact

Phase 3AL changes static browser runtime HTML/JavaScript plus browser smoke tests and documentation. Because packaged Windows builds include the static app assets, fresh package artefacts would be required before any future `v0.1.0-dev.2` publication from this commit or a descendant.

No installer or release artefact was built, uploaded, replaced, or republished by this evidence note.

## Validation Results

Record validation after the Phase 3AL implementation commit:

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | PASS | Completed 232 Playwright browser smoke tests across Chromium and Microsoft Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings for release documentation filenames and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored local RC report `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T213946Z.md`; ZIP SHA256 `B2235033E2F579ED41D390058DDBF9B2E21FA0EC1594D7CC217F0D219CC94B1F`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Rebuilt ignored local package folder and ZIP; native smoke intentionally skipped by this command flag. |
| Packaged native smoke against freshly rebuilt package executable | PASS | `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` passed. |

## Evidence Boundary

Phase 3AL does not alter the `v0.1.0-dev.1` release, release assets, tags, releases, or `main`.

Production readiness, go-live approval, stable-channel certification, and `v0.1.0-dev.2` publication remain unclaimed.
