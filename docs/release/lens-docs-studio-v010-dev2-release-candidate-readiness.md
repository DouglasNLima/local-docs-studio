# Lens Docs Studio v0.1.0-dev.2 Release-Candidate Readiness

Checkpoint date: 2026-06-12

## Status

Phase 3AQ is a local release-candidate readiness and artefact rebaseline checkpoint for a possible future `v0.1.0-dev.2` prerelease/dev release.

This checkpoint does not publish `v0.1.0-dev.2`, does not create tags or GitHub Releases, does not upload release assets, does not merge to `main`, does not edit, delete, replace, re-upload, rebuild, or republish `v0.1.0-dev.1` assets, and does not claim production readiness or go-live approval.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Source commit used for local artefacts | `7485422aa0bafcab8addecfa9a21636298e9fae7` |
| Phase 3AP baseline in history | PASS; `HEAD` was exactly `7485422aa0bafcab8addecfa9a21636298e9fae7` at the start of the checkpoint. |
| Tracked status before edits | Clean; `git status --short --untracked-files=all` returned no tracked or untracked source changes. |
| Ignored/generated paths | Existing ignored outputs under `artifacts/`, `test-results/`, Windows `bin/` and `obj/`, and local Playwright/native smoke outputs remained outside git. Some ignored WebView2 cache paths reported filename-length warnings during ignored-status enumeration; they were not staged or edited. |

## Completed Slice Summary

The local candidate source includes the completed `v0.1.0-dev.2` slices through Phase 3AP:

| Phase | Summary | Runtime/package impact |
| --- | --- | --- |
| 3AK | Diagnostics visibility polish for bridge and folder-picker issues. | Static browser runtime and browser smoke tests changed; fresh package and installer candidates required before any future publication. |
| 3AL | First-run and onboarding copy polish. | Static browser runtime, app shell copy, tests, and documentation changed; fresh package and installer candidates required before any future publication. |
| 3AM | Watcher/conflict UX copy refinement. | Static browser runtime and browser smoke tests changed; fresh package and installer candidates required before any future publication. |
| 3AN | Optional troubleshooting/support bundle design. | Design-only; no runtime support bundle generation implemented. |
| 3AO | Installer upgrade and uninstall evidence planning. | Planning-only; no installer evidence execution or publication approval. |
| 3AP | Stale older prerelease guidance. | Documentation-only; `v0.1.0-dev.1` remains the current recommended verified prerelease/dev release. |

## Runtime And Package-Affecting Changes

Phases 3AK, 3AL, and 3AM changed packaged static/runtime code. Because Windows package and installer artefacts include the static app assets, Phase 3AQ rebuilt fresh local package and installer candidates from source commit `7485422aa0bafcab8addecfa9a21636298e9fae7`.

The package was verified to include Phases 3AK, 3AL, and 3AM because the source commit used for the artefacts is the Phase 3AP head that contains those earlier runtime-affecting slices.

## Docs And Design-Only Slices

Phases 3AN, 3AO, and 3AP added support-bundle design, installer evidence planning, and prerelease guidance. They did not implement runtime behaviour, rebuild release assets, approve publication, or claim production readiness.

## Local Artefact Rebaseline

| Artefact | Path | SHA256 | Timestamp (UTC) |
| --- | --- | --- | --- |
| Windows package directory | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev` | n/a | `2026-06-12T09:55:59.0776511Z` |
| Windows package ZIP | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip` | `56BBBA411E384A800831AE05D5A58902FCEE2D8700C3E0E3F4DE71842FCE1296` | `2026-06-12T09:54:35.2497142Z` |
| Unsigned Inno Setup installer | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `B8246DD7EC2A5E8F988A8B6CD36470736BFC2C5BA96EFB92A6D6901F33FC3F41` | `2026-06-12T09:55:44.4822897Z` |
| Installer checksum file | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | n/a | Generated with the installer. |
| Installer report | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | n/a | Generated with the installer. |

Generated local release-candidate metadata:

- `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T095214Z.md`
- `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T095214Z.json`
- `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev.2-phase-3aq-local-rc-readiness.md`
- `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev.2-phase-3aq-local-rc-readiness.json`

These files are ignored local artefacts and are not committed source.

## Validation Results

| Command | Result | Notes |
| --- | --- | --- |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Initial fresh local package folder and ZIP build passed; native smoke intentionally skipped by `-NoSmoke`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Initial installer build passed using the freshly rebuilt package. |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | TIMEOUT | Full browser run reached one Chromium timeout in `fake WebView2 bridge creates Markdown files in native workspaces`; failure artefact showed a menu button was resolved but hidden during click. |
| `npx playwright test tests/browser/app-smoke.spec.mjs:2125 --project=chromium` | PASS | Direct rerun of the timed-out test passed in 2.4 seconds. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 0 warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Debug packaged `StaticApp/` validation passed for 53 service-worker assets and 150 vendor assets. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Local RC package certification passed and wrote Markdown/JSON metadata. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development native bridge smoke passed. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Final local package rebuild passed; this produced ZIP SHA256 `56BBBA411E384A800831AE05D5A58902FCEE2D8700C3E0E3F4DE71842FCE1296`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Final local installer rebuild passed; this produced installer SHA256 `B8246DD7EC2A5E8F988A8B6CD36470736BFC2C5BA96EFB92A6D6901F33FC3F41`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke passed against the final rebuilt package executable. |

## Optional Manual Sanity

Optional packaged manual sanity was skipped in this automated checkpoint because it requires interactive observation of onboarding copy, Diagnostics, native picker UI, and a controlled watcher/conflict scenario. No manual PASS is claimed.

## Known Limitations

- The `npm run test:browser` full run did not produce a clean full-suite PASS because one Chromium test timed out on a menu visibility race; the direct rerun of that exact test passed.
- The installer remains unsigned.
- Runtime prerequisites are still external: .NET Desktop Runtime, Windows App SDK Runtime, and Evergreen Microsoft Edge WebView2 Runtime.
- Installer upgrade and uninstall evidence from `v0.1.0-dev.1` to a future candidate remains planned by Phase 3AO and was not executed here.
- Optional support bundle generation remains design-only and is not implemented.
- No production readiness, go-live approval, stable-channel certification, code signing, auto-update, MSIX, Store, or winget claim is made.

## Release Artefact Expectations

The local package ZIP and unsigned Inno installer are release-candidate artefacts only. They are suitable for a later publication approval gate to review, but Phase 3AQ does not publish them.

Before any future `v0.1.0-dev.2` publication:

- Review the full validation outcome, including the browser full-run timeout and direct rerun pass.
- Decide whether the browser-suite timeout needs a test hardening slice before publication.
- Run or explicitly defer installer upgrade/uninstall evidence using the Phase 3AO plan.
- Confirm target commit, release notes, asset set, checksums, and publication approval.
- Keep `v0.1.0-dev.1` assets untouched unless a separate release-maintenance decision explicitly authorises otherwise.

## Checkpoint Verdict

Phase 3AQ establishes a local release-candidate readiness checkpoint and artefact rebaseline for a possible future `v0.1.0-dev.2` approval gate.

`v0.1.0-dev.2` is not published, not released, not production-ready, and not approved for go-live by this document. No release assets, tags, releases, `main` merges, or `v0.1.0-dev.1` assets were changed.
