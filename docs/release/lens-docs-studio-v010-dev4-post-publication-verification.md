# Lens Docs Studio v0.1.0-dev.4 Phase 3BL Post-Publication Verification

Verification date: 2026-06-13

## Summary

Phase 3BL is a read-only post-publication verification checkpoint for the GitHub prerelease `v0.1.0-dev.4`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.4`

No GitHub release assets, tags, or releases were edited, deleted, replaced, re-uploaded, rebuilt, republished, or created during this checkpoint. No merge to `main` was performed. No production readiness, go-live approval, stable-channel certification, stable/latest positioning, or production release status is claimed.

`v0.1.0-dev.1`, `v0.1.0-dev.2`, and `v0.1.0-dev.3` assets were checked read-only and were not changed by this checkpoint.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Start commit | `a8f478ed9fb3bfd8c3c9beba495fc1a7f5775df3` |
| Publication documentation commit in history | PASS, `a8f478ed9fb3bfd8c3c9beba495fc1a7f5775df3` is `HEAD` and is an ancestor of `HEAD` |
| Tracked status at start | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, and `test-results/` |
| Downloaded verification assets | Stored under ignored path `artifacts/windows/download-verification/v0.1.0-dev.4/` |

`git status --short --ignored` emitted Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. The tracked working tree remained clean before the Phase 3BL documentation edit.

## Read-Only Release Verification

Read-only commands used:

```powershell
gh release view v0.1.0-dev.4 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,name,url,targetCommitish,assets
git ls-remote --tags origin refs/tags/v0.1.0-dev.4
gh release view v0.1.0-dev.1 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,assets
gh release view v0.1.0-dev.2 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,assets
gh release view v0.1.0-dev.3 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,assets
```

| Check | Result |
| --- | --- |
| Tag `v0.1.0-dev.4` exists | PASS |
| Release `v0.1.0-dev.4` exists | PASS |
| Release is draft | PASS, `false` |
| Release is prerelease | PASS, `true` |
| Release is not a production release | PASS, tagged release is explicitly a prerelease/dev release |
| Tag target | PASS, `2555a7554e9e625af7e5d9da93725f77f4997ded` |
| Release API `target_commitish` | Informational, `main`; tag ref resolves to the expected target above |
| Frozen artefact source commit | PASS, `ce0d34bf19aaac4d67e91bef05343bc608cbd433` is referenced by the downloaded RC metadata and setup report |
| `v0.1.0-dev.1` assets | PASS, read-only metadata remained available and this checkpoint made no changes |
| `v0.1.0-dev.2` assets | PASS, read-only metadata remained available and this checkpoint made no changes |
| `v0.1.0-dev.3` assets | PASS, read-only metadata remained available and this checkpoint made no changes |

Provenance note: the release tag points to publication evidence commit `2555a7554e9e625af7e5d9da93725f77f4997ded`, while the frozen Windows package and installer artefacts were built from source commit `ce0d34bf19aaac4d67e91bef05343bc608cbd433`. This relationship is intentional and is documented in the v0.1.0-dev.4 publication record and in this checkpoint.

## Asset List

Expected asset set matched exactly. No unexpected extra assets were attached.

| Asset | Size (bytes) | GitHub digest |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 34115305 | `sha256:06bfa3896e1cca48f1dc87d2f49d2387a8bcb93078e9f070764f22b048dd860c` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 25936701 | `sha256:e52db8b71b34e269f1754e2ccb6ab1691f38e6172cd2c826299a5dcf4a6652fc` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | `sha256:7cc4292b719b4c63bb4e45ee7ae6ad2025e486c109d3b3cec72d7783ea769270` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 64593 | `sha256:546ba57bce5e3267ab8fdfcff06df33c28a4151e9ff7ed0c85ead290aad9337a` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.md` | 9011 | `sha256:0dc848c1c6c151462bf85b10deb4a333317f93fbdb9f3ec94f0fb029dcecc579` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260613T105206Z.json` | 5534 | `sha256:06dae25675b9851ea8b9e9325b3f3451107a98eb73e6e50a9b0bed8ccb6e64d1` |

## Download Verification

Assets were downloaded with:

```powershell
gh release download v0.1.0-dev.4 --repo DouglasNLima/local-docs-studio --dir artifacts/windows/download-verification/v0.1.0-dev.4 --clobber
```

| Check | Result |
| --- | --- |
| Downloaded ZIP SHA256 | PASS, `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C` |
| Downloaded installer SHA256 | PASS, `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |
| Installer `.sha256` file | PASS, contains `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC  LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |
| RC Markdown metadata | PASS with note: references frozen source commit `ce0d34bf19aaac4d67e91bef05343bc608cbd433` and RC-gate ZIP SHA256 `87AB075661DFFA7829FABB2F94E4042BBBA688CF637BD94E62E90C4514944FDD`; installer SHA256 is not present in this package RC Markdown |
| RC JSON metadata | PASS with note: references frozen source commit `ce0d34bf19aaac4d67e91bef05343bc608cbd433` and RC-gate ZIP SHA256 `87AB075661DFFA7829FABB2F94E4042BBBA688CF637BD94E62E90C4514944FDD`; installer SHA256 is not present in this package RC JSON |
| Installer report metadata | PASS, references frozen source commit `ce0d34bf19aaac4d67e91bef05343bc608cbd433` and installer SHA256 `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC` |

The RC metadata preserves the RC-gate ZIP hash from the earlier package validation step. The downloaded ZIP hash above is the final published ZIP hash recorded in the publication bundle freeze and publication record.

## Downloaded ZIP Smoke

The downloaded ZIP was extracted to:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.4\extracted
```

Executable path:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.4\extracted\LensDocsStudio.Windows.exe
```

| Check | Result |
| --- | --- |
| Extract downloaded ZIP | PASS |
| Static package verification | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1 -NoBuild -StaticAppRoot artifacts/windows/download-verification/v0.1.0-dev.4/extracted/StaticApp` verified 54 service-worker assets and 150 vendor assets |
| Packaged native smoke against downloaded executable | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/download-verification/v0.1.0-dev.4/extracted/LensDocsStudio.Windows.exe -TimeoutSeconds 60` completed successfully |
| Payload hygiene against extracted package | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackagePayloadHygiene.ps1 -PackageRoot artifacts/windows/download-verification/v0.1.0-dev.4/extracted` detected, cleaned, and allowed the expected fixture paths |

## Installer Smoke

A contained silent installer smoke was run against the downloaded installer, installing to:

```text
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BL\Install
```

Install command shape:

```powershell
LensDocsStudio.Windows-0.1.0-dev-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3BL\Install"
```

| Check | Result |
| --- | --- |
| Baseline default install before smoke | PASS, no default install detected at the temporary test path |
| Baseline uninstall entry before smoke | PASS, absent at `HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Lens Docs Studio_is1` |
| Baseline desktop shortcut before smoke | Present before smoke; treated as pre-existing local residue and not created by this checkpoint |
| Baseline Start Menu group before smoke | Present before smoke; treated as pre-existing local residue and later removed by the contained uninstall |
| Baseline Lens-owned ProgIDs before smoke | `LensDocsStudio.markdown` and `LensDocsStudio.mermaid` were present before smoke; treated as pre-existing local residue and not created by this checkpoint |
| Silent install | PASS, exit code `0` |
| Installed executable | PASS, `C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BL\Install\LensDocsStudio.Windows.exe` existed |
| Native smoke against installed executable | PASS |
| Support bundle UI present | PASS, installed static app contains support bundle UI strings |
| Diagnostics copy/export UI present | PASS, installed static app contains diagnostics, `Copy summary`, and `Export local JSON` strings |
| Uninstaller present | PASS, `unins000.exe` existed in the temporary install root |
| Silent uninstall | PASS, exit code `0` |
| Installed executable after uninstall | PASS, absent |
| Uninstall registry entry after uninstall | PASS, absent |
| Start Menu group after uninstall | PASS, absent |
| Install root after uninstall | Present with expected runtime-generated `LensDocsStudio.Windows.exe.WebView2` data after app launch; the temporary smoke install folder was then removed |

Because the desktop shortcut and two Lens-owned ProgIDs existed before the contained smoke, they were recorded as baseline local residue rather than installer output from this checkpoint. This checkpoint did not enable file association tasks.

## Manual Packaged Sanity

Manual packaged sanity remains `SKIPPED_INTERACTIVE_NATIVE_UI_CONTROL_NOT_EXECUTED`.

No real interactive packaged UI observation/control was completed during Phase 3BL for onboarding rendering, diagnostics, preview-first support bundle copy/export behaviour, Open folder pending guidance, visible native picker operation, temporary workspace selection, cancellation, browser fallback inactivity, or watcher changed-on-disk copy. Phase 3AW remains the referenced manual packaged verification for interactive native UI behaviours including onboarding, diagnostics, visible native picker selection, cancellation, inactive browser fallback, and watcher changed-on-disk copy after the Open folder pending guidance remediation. Phase 3BJ and this checkpoint both verified support bundle and diagnostics copy/export UI presence through packaged installed static assets.

## Validation

Validation for the Phase 3BL documentation evidence update:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 47 module files, 54 shell assets, 150 vendor assets, and 53 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, 6 existing PRI qualifier warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, verified 54 service-worker assets and 150 vendor assets in packaged `StaticApp/` |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS, wrote ignored local RC report and JSON metadata under `artifacts/windows/release-candidates/`; validation-generated ZIP SHA256 was `A859EEEBDA9A182D584BD0A04ECC3F85C4059BB51D14652877DF8A4AF41A2517` and is not the published Phase 3BL asset |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS, Windows native bridge smoke completed successfully |
| `npm run test:browser` | Not rerun because Phase 3BL changed only release documentation and evidence, not frontend/runtime/static assets |

## Evidence Boundary

- Phase 3BL did not edit, delete, replace, re-upload, rebuild, republish, or create release assets.
- Phase 3BL did not create, move, or delete tags.
- Phase 3BL did not create, edit, or delete GitHub releases.
- Phase 3BL did not merge to `main`.
- Phase 3BL did not change runtime code.
- Phase 3BL did not change `v0.1.0-dev.1`, `v0.1.0-dev.2`, or `v0.1.0-dev.3` release assets.
- Phase 3BL did not claim production readiness, go-live approval, stable-channel certification, stable/latest positioning, or production release status.
- Downloaded release assets and extracted verification payloads remain under ignored `artifacts/` paths and were not staged.
