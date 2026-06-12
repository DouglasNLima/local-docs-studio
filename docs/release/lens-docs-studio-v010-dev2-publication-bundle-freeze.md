# Lens Docs Studio v0.1.0-dev.2 Publication Bundle Freeze

Checkpoint date: 2026-06-12

## Status

Phase 3AR is a no-publication pre-publication checkpoint for a possible future `v0.1.0-dev.2` prerelease/dev release.

This checkpoint freezes one final local `v0.1.0-dev.2` publication bundle for later approval-gate review. It does not publish `v0.1.0-dev.2`, create tags or GitHub Releases, upload release assets, merge to `main`, edit/delete/replace/re-upload/rebuild/republish any GitHub release asset, change `v0.1.0-dev.1` assets, or claim production readiness or go-live approval.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Starting commit | `5602358ef241c144fccae13e980c9eb2920126d8` |
| Phase 3AQ in history | PASS; `HEAD` was exactly `5602358ef241c144fccae13e980c9eb2920126d8` at the start of the checkpoint. |
| Tracked status before artefact rebuild | Clean; `git status --short --branch` returned `## develop...origin/develop`. |
| Ignored/generated paths | Existing ignored outputs under `artifacts/`, `node_modules/`, Windows `.vs/`, `bin/`, `obj/`, and `test-results/` remained outside git. Ignored-status enumeration reported Windows filename-length warnings from generated WebView2 cache paths; those paths were not staged or edited. |

## Freeze Source Decision

| Field | Value |
| --- | --- |
| Final artefact source commit | `5602358ef241c144fccae13e980c9eb2920126d8` |
| Final evidence/documentation commit | This Phase 3AR document commit, created after the local bundle freeze. |
| Same commit? | No. The frozen artefacts use the clean Phase 3AQ baseline commit; Phase 3AR adds evidence only after the freeze. |
| Suitability for publication handoff | The source commit contains the completed Phase 3AK, 3AL, and 3AM runtime/UX slices and the Phase 3AN, 3AO, 3AP, and 3AQ documentation/evidence baseline. Rebuilding from it changes bundled documentation/evidence metadata compared with the earlier Phase 3AQ source commit, but no runtime behaviour changed during Phase 3AR. |

## Frozen Local Bundle

Frozen bundle path:

`C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\`

| Artefact | Frozen path | SHA256 |
| --- | --- | --- |
| Windows package ZIP | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\LensDocsStudio.Windows-0.1.0-dev.zip` | `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B` |
| Unsigned Inno Setup installer | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |
| Installer checksum | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Matches installer hash above. |
| Installer report | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | n/a |
| Release-candidate report | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.md` | n/a |
| Release-candidate metadata | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.2\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.json` | n/a |

Original final output paths before freezing:

- Package directory: `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev`
- ZIP: `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip`
- Installer: `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe`
- Installer checksum: `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256`
- Installer report: `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md`
- RC report: `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.md`
- RC metadata: `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.json`
- RC metadata timestamp: `20260612T105712Z`

## Frozen File Verification

Verification was performed from the copied files under `artifacts/windows/publication-bundles/v0.1.0-dev.2/`.

| Check | Result |
| --- | --- |
| Frozen ZIP hash matches documented final ZIP hash | PASS |
| Frozen ZIP hash matches RC metadata `zipSha256` | PASS |
| Frozen installer hash matches documented final installer hash | PASS |
| Frozen installer `.sha256` file matches frozen installer hash | PASS |
| RC metadata references final source commit `5602358ef241c144fccae13e980c9eb2920126d8` | PASS |
| RC metadata references branch `develop` and result `PASS` | PASS |

## Validation Results

| Command | Result | Notes |
| --- | --- | --- |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Initial Phase 3AR package rebuild passed. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Initial Phase 3AR installer rebuild passed. |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | PASS | Full Playwright browser suite passed: 232 tests using Chromium and Microsoft Edge. No Phase 3AR timeout occurred, so no targeted rerun was needed. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing Windows App SDK PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Debug packaged `StaticApp/` validation passed for 53 service-worker assets and 150 vendor assets. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Package RC certification passed and wrote Markdown/JSON metadata. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development native bridge smoke passed. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Explicit final package rebuild passed. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Explicit final installer rebuild passed. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev\LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke passed against the final rebuilt package executable. |
| Final RC certification after explicit rebuild | PASS | A final RC certification was run so the frozen RC metadata hash matches the frozen ZIP. |
| Final installer rebuild after final RC certification | PASS | The frozen installer was rebuilt from the final certified package. |
| Final packaged native bridge smoke after final installer rebuild | PASS | Packaged native bridge smoke passed against the final frozen package executable. |

## Browser Timeout Triage

Phase 3AQ browser evidence remains:

- Full `npm run test:browser` timed out on one Chromium fake WebView2 menu-click test.
- Direct targeted rerun of the failing test passed.
- That timeout was not classified as a full PASS in Phase 3AQ.

Phase 3AR browser evidence:

- `npm run test:browser` passed in full.
- No Phase 3AR browser timeout occurred.
- No targeted direct rerun was needed in Phase 3AR.

## Manual Packaged Sanity

Result: `BLOCKED_MANUAL_PACKAGED_SANITY_NOT_EXECUTED`

Exact reason: the available automated tools can run the packaged native smoke harness, but they cannot provide real interactive observation/control of the native Windows WebView2 window, onboarding flow, native folder picker, or a controlled watcher/conflict scenario. Because the requested checklist requires observed real packaged-app interactions, no manual PASS is claimed.

The following requested manual checks were therefore not claimed as observed in the real packaged app:

- First-run/onboarding copy rendering.
- Open folder as the primary action.
- Open file availability.
- Diagnostics reachable from onboarding/empty state.
- Diagnostics route/fallback clarity.
- Native Open folder picker selection of a temporary workspace.
- Watcher/conflict copy using the new app/disk wording in a controlled scenario.

Automated packaged native bridge smoke did pass against the final package executable, but it is not a substitute for manual packaged sanity.

## Read-Only GitHub Release State

Read-only checks only were used.

| Check | Result |
| --- | --- |
| `gh release view v0.1.0-dev.2 --json tagName,name,isDraft,isPrerelease,assets,url` | `release not found` |
| `git ls-remote --tags origin refs/tags/v0.1.0-dev.2` | No matching remote tag returned. |
| Attached assets | None, because no release exists. |
| Later publication mode | Create-new, if and only if a later approval gate explicitly authorises publication. |

## Publication Boundary

- No publication was performed.
- No GitHub release assets were created, edited, deleted, replaced, re-uploaded, rebuilt, republished, or otherwise changed.
- No tags or GitHub Releases were created.
- No merge to `main` was performed.
- No `v0.1.0-dev.1` release assets were changed.
- Generated package, installer, release-candidate, and frozen bundle artefacts remain ignored local outputs and are not staged as source.
- Production readiness, go-live approval, stable-channel certification, stable/latest positioning, code signing, auto-update, MSIX, Store, and winget are not claimed.

## Approval-Gate Readiness

The frozen `v0.1.0-dev.2` local bundle is ready for a later publication approval gate to review as a candidate asset set. It is not publication approval, not a release, and not a production-readiness or go-live claim.

A later approval gate should explicitly decide whether to accept or defer the blocked manual packaged sanity evidence before any publication action.
