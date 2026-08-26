# Phase 3AM Watcher/Conflict UX Copy Refinement

Date: 2026-06-12

## Status

Phase 3AM is the third `v0.1.0-dev.2` implementation slice on `develop`.

This is not a release approval, does not claim production readiness or go-live approval, does not merge to `main`, does not create tags or releases, and does not edit, rebuild, replace, upload, or republish `v0.1.0-dev.1` release assets.

## Why This Followed Onboarding

Phase 3AK made bridge and Open folder diagnostics easier to report. Phase 3AL then made Open folder the primary first-run action and clarified local-file expectations. Phase 3AM follows those slices by polishing the next user decision point: what to do when a workspace file changes on disk while Lens Docs Studio is open.

This copy was selected after onboarding because users who start with a folder are more likely to encounter file watching and refresh choices. The wording now explains the app version and disk version without changing watcher behaviour.

## User-Visible Copy Improvements

- Clean changed files now say they changed on disk and that **Refresh active file** reads the disk version.
- Dirty conflicts now say the file changed on disk while unsaved app edits exist.
- The refresh prompt now uses **Keep app edits** for cancel and **Use disk version** for confirm.
- Deleted-file guidance now says the app keeps current content so users can copy it or use **Save as**.
- Renamed-file guidance now uses "renamed on disk" and asks users to review before saving.
- Dirty-state labels now say "edited in the app" instead of "edited in memory".
- Normal watcher copy avoids raw native bridge, WebView2, watcher event, stack, production, and go-live terminology.

## Behaviour Changes

No watcher, conflict, save, refresh, native bridge, folder-picker, fallback, import, export, or package behaviour changed.

Existing Phase 3AA semantics are preserved:

- Clean external changes remain safe and reload only through the existing explicit refresh action.
- Dirty conflict cancel keeps the current unsaved app edits.
- Dirty conflict confirm accepts the disk version through the existing refresh path.
- Browser folder/file-input fallback remains blocked while the native WebView2 bridge is present and expected to handle Open folder.

## Tests Added Or Updated

- Updated fake WebView2 watcher coverage for clean changed-on-disk copy.
- Updated dirty conflict coverage for **Keep app edits** and **Use disk version** labels.
- Kept assertions that cancel sends no refresh request and preserves app edits.
- Kept assertions that confirm sends one refresh request and loads the disk version.
- Added copy-safety coverage that the dirty conflict prompt avoids production/go-live claims and raw stack/native watcher details.
- Updated dirty-state expectations from "edited in memory" to "edited in the app".

## Runtime And Package Impact

Phase 3AM changes static browser runtime JavaScript plus browser smoke tests and documentation. Because packaged Windows builds include the static app assets, fresh package artefacts must be rebuilt before any future `v0.1.0-dev.2` publication from this commit or a descendant.

No installer or release artefact was built, uploaded, replaced, or republished by this evidence note.

## Validation Results

Record validation after the Phase 3AM implementation commit:

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | PASS | Completed 232 Playwright browser smoke tests across Chromium and Microsoft Edge. An initial run failed because one browser File System Access test still expected the previous clean-change button label; the expectation was updated and the full suite passed on rerun. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings for release documentation filenames and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated ignored local RC report `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260612T082322Z.md`; ZIP SHA256 `9D958352C18C90532882DFA5DD876BC9725AAF4F1C9CAFD0B0C5D364219E9712`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Rebuilt ignored local package folder and ZIP; native smoke intentionally skipped by this command flag. |
| Packaged native smoke against freshly rebuilt package executable | PASS | `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` passed. |

## Evidence Boundary

Phase 3AM does not alter the `v0.1.0-dev.1` release, release assets, tags, releases, or `main`.

Production readiness, go-live approval, stable-channel certification, and `v0.1.0-dev.2` publication remain unclaimed.
