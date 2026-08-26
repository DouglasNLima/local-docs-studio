# Lens Docs Studio v0.1.0-dev.4 Publication Bundle Freeze

Date: 2026-06-13

## Summary

Phase 3BJ freezes a local, no-publication `v0.1.0-dev.4` release-candidate bundle after the Phase 3BI payload hygiene guard.

This is a publication-bundle freeze for later approval review only. It did not publish `v0.1.0-dev.4`, create or move a tag, create or edit a GitHub Release, upload or replace assets, merge to `main`, claim production readiness, or claim go-live approval.

Runtime app code did not change in this phase. The only source change is this evidence document. Generated package, installer, release-candidate, verification, Playwright, Windows build, and frozen bundle artefacts remain ignored and were not staged.

## Reason For Candidate

`v0.1.0-dev.4` is the next local prerelease candidate after the targeted `develop` implementation and evidence slices:

- Phase 3BE added explicit-user-action Diagnostics support bundle generation with allowlisted local metadata, bounded redaction, preview-first copy/export, and no upload path.
- Phase 3BG improved Diagnostics copy/export wording and bounded local action messages while preserving the Phase 3BE privacy model.
- Phase 3BI added Windows ZIP and Inno installer payload hygiene guards so runtime-generated WebView2 user data, browser storage, caches, and logs cannot enter package or installer artefacts.

Phase 3BH upgrade evidence remains limited: a true upgrade from the already-published `v0.1.0-dev.3` baseline installer is still blocked because that published baseline installer rolls back while copying a deep runtime-generated WebView2 Service Worker cache path. Phase 3BJ does not alter that published baseline asset.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Source commit used for frozen artefacts | `ce0d34bf19aaac4d67e91bef05343bc608cbd433` |
| Phase 3BI commit in history | PASS, `ce0d34bf19aaac4d67e91bef05343bc608cbd433` is `HEAD` at freeze time |
| Tracked status before edits | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

`git status --short --ignored` also reported long-path warnings inside ignored WebView2 runtime output under Windows build folders. Those paths are generated runtime/build output, not tracked source.

## Inputs Reviewed

- `docs/release/phase-3be-support-bundle-implementation.md`
- `docs/release/phase-3bg-diagnostics-export-copy-improvements.md`
- `docs/release/phase-3bh-installer-upgrade-evidence.md`
- `docs/release/phase-3bi-installer-payload-hygiene.md`
- `docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md`
- `scripts/windows/WindowsPackagePayloadHygiene.ps1`
- `scripts/windows/Test-WindowsPackagePayloadHygiene.ps1`
- `scripts/windows/Build-WindowsPackage.ps1`
- `scripts/windows/Build-WindowsInnoInstaller.ps1`
- `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`

## Frozen Bundle

Frozen local bundle path:

```text
C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.4
```

The bundle was copied from the final local rebuild outputs and the latest release-candidate metadata. It remains ignored and was not staged.

| Artefact | Path | SHA256 |
| --- | --- | --- |
| Windows ZIP package | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip` | `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Inno installer | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |
| Installer SHA256 file | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Contains `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |
| Installer report | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | Generated locally |
| Release-candidate report | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.md` | Generated locally |
| Release-candidate metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.json` | Generated locally |

The RC metadata records commit `ce0d34bf19aaac4d67e91bef05343bc608cbd433`, validation result `PASS`, and RC-gate ZIP SHA256 `87AB075661DFFA7829FABB2F94E4042BBBA688CF637BD94E62E90C4514944FDD`. The frozen ZIP hash above comes from the final requested package rebuild after the RC gate; both were built from the same source commit.

## Frozen Bundle Verification

| Check | Result |
| --- | --- |
| Frozen ZIP hash matches final documented ZIP hash | PASS, `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Frozen installer hash matches final documented installer hash | PASS, `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |
| Installer `.sha256` matches installer hash | PASS |
| RC metadata references source commit | PASS, `ce0d34bf19aaac4d67e91bef05343bc608cbd433` |
| RC metadata records validation result | PASS |
| Frozen ZIP blocked WebView2/runtime payload check | PASS, 0 blocked paths |
| Frozen installer report blocked payload path check | PASS, no `LensDocsStudio.Windows.exe.WebView2`, `EBWebView`, Service Worker cache, browser storage, or runtime cache payload paths found |

## Read-Only Release And Tag Check

Read-only checks against `origin` and GitHub showed:

| Check | Result |
| --- | --- |
| Remote tag `v0.1.0-dev.4` | Missing |
| GitHub Release `v0.1.0-dev.4` | Missing, `gh release view v0.1.0-dev.4` returned `release not found` |
| Attached assets | None, because the release is absent |
| Later publication mode | Create-new approval gate, not update-existing |

No GitHub release, tag, or asset state was modified.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackagePayloadHygiene.ps1` | PASS | Fake WebView2/cache payloads were detected and cleaned; `StaticApp/` fixture assets remained allowed. |
| `npm run test:static` | PASS | Static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans. |
| `npm run test:browser` | PASS | 244 Playwright browser smoke tests passed across Chromium and Microsoft Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 6 existing PRI qualifier warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Included file association dry runs and payload hygiene self-test; verified 54 service-worker assets and 150 vendor assets in packaged `StaticApp/`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Generated RC report and JSON metadata listed above; RC-gate ZIP SHA256 `87AB075661DFFA7829FABB2F94E4042BBBA688CF637BD94E62E90C4514944FDD`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Final ignored Windows folder/ZIP package rebuilt; native smoke intentionally skipped by this command flag. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Final ignored Inno installer rebuilt from the guarded package source; installer SHA256 `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the final rebuilt package executable. |
| Clean install/uninstall of final local installer | PASS_WITH_EXPECTED_RUNTIME_WEBVIEW2_RESIDUE | Installed to a temporary root, native smoke passed, support bundle/copy/export UI strings were present, uninstall removed installer-owned executable files, and runtime-generated `LensDocsStudio.Windows.exe.WebView2` residue remained after app launch. |

Clean install evidence details:

| Check | Result |
| --- | --- |
| Install root | `C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BJ\CleanInstall` |
| Install exit code | PASS, `0` |
| Installed executable exists before uninstall | PASS |
| Installed native smoke | PASS |
| Installed `StaticApp/` validation | PASS |
| Support bundle UI present | PASS, `Create support bundle` found |
| Diagnostics copy UI present | PASS, `Copy summary` found |
| Diagnostics export UI present | PASS, `Export local JSON` found |
| Uninstall exit code | PASS, `0` |
| Installed executable after uninstall | PASS, absent |
| Install-root residue after uninstall | Expected runtime-generated `LensDocsStudio.Windows.exe.WebView2` folder |

The first clean-install evidence helper run had a script-ordering mistake: it checked the installed executable after uninstall, although install, static validation, native smoke, and uninstall had already succeeded. The corrected rerun captured the pre-uninstall executable state before uninstall and passed.

## Publication Readiness Boundary

The frozen local `v0.1.0-dev.4` bundle is ready for a later explicit publication approval gate, subject to reviewer acceptance of this evidence and any required release notes. It is not published, approved for production, or approved for go-live by this phase.

Before any later publication, the operator must still explicitly approve creating the missing `v0.1.0-dev.4` tag/release and uploading the frozen artefacts. That later gate must preserve the frozen hashes or record any newly authorised rebuild separately.

## Evidence Boundary

- Runtime code did not change in Phase 3BJ.
- Existing prerelease assets were not changed.
- Release assets were not edited, deleted, replaced, re-uploaded, rebuilt, republished, or published.
- No `v0.1.0-dev.4` tag or release was created.
- No merge to `main` was made.
- No production readiness, go-live approval, stable-channel certification, or stable/latest positioning is claimed.
- Generated package, installer, downloaded, frozen bundle, release-candidate, Playwright, and Windows build outputs remain ignored and were not staged.
