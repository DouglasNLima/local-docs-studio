# Lens Docs Studio v0.1.0-dev.3 Phase 3BA Post-Publication Verification

Verification date: 2026-06-12

## Summary

Phase 3BA is a read-only post-publication verification checkpoint for the GitHub prerelease `v0.1.0-dev.3`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.3`

No GitHub release assets, tags, or releases were edited, deleted, replaced, re-uploaded, or created during this checkpoint. No merge to `main` was performed. No production readiness, go-live approval, stable-channel certification, or production release status is claimed.

`v0.1.0-dev.1` and `v0.1.0-dev.2` assets were checked read-only and were not changed by this checkpoint.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Start commit | `16c0069dee693e49003085936e9ead24cdfb23b2` |
| Publication evidence commit in history | PASS, `16c0069dee693e49003085936e9ead24cdfb23b2` is `HEAD` and is an ancestor of `HEAD` |
| Tracked status at start | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, Windows build output under `src/windows/LensDocsStudio.Windows/bin/` and `src/windows/LensDocsStudio.Windows/obj/`, and prior local test/package outputs |
| Downloaded verification assets | Stored under ignored path `artifacts/windows/download-verification/v0.1.0-dev.3/` |

`git status --short --ignored` emitted Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. The tracked working tree remained clean before the Phase 3BA documentation edit.

## Read-Only Release Verification

Read-only commands used:

```powershell
gh release view v0.1.0-dev.3 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,assets,createdAt,publishedAt
gh release view --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,name,url,targetCommitish,createdAt,publishedAt
gh api repos/DouglasNLima/local-docs-studio/git/ref/tags/v0.1.0-dev.3
git ls-remote --tags origin v0.1.0-dev.3
gh release view v0.1.0-dev.1 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,assets,createdAt,publishedAt
gh release view v0.1.0-dev.2 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,assets,createdAt,publishedAt
```

| Check | Result |
| --- | --- |
| Tag `v0.1.0-dev.3` exists | PASS |
| Release `v0.1.0-dev.3` exists | PASS |
| Release is draft | PASS, `false` |
| Release is prerelease | PASS, `true` |
| Release is not a production release | PASS, tagged release is explicitly a prerelease; `gh release view` without a tag returned `release not found`, so no latest production release was visible through this GitHub CLI endpoint |
| Tag target | PASS, `1e8f15bf177046333cdc86076487cee74f647170` |
| Release API `target_commitish` | PASS, `1e8f15bf177046333cdc86076487cee74f647170` |
| Frozen artefact source commit | PASS, `ea75e13bf72518eb417cf11e4f62d7730738c8cd` is referenced by the downloaded RC metadata and setup report |
| `v0.1.0-dev.1` assets | PASS, read-only metadata matched the previously documented six-asset inventory, sizes, digests, and upload/update timestamps |
| `v0.1.0-dev.2` assets | PASS, read-only metadata matched the previously documented six-asset inventory, sizes, digests, and upload/update timestamps |

Provenance note: the release tag points to publication evidence commit `1e8f15bf177046333cdc86076487cee74f647170`, while the frozen Windows package and installer artefacts were built from source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd`. This relationship is intentional and is documented in the v0.1.0-dev.3 publication record and in this checkpoint.

## Asset List

Expected asset set matched exactly. No unexpected extra assets were attached.

| Asset | Size (bytes) | GitHub digest |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 34054203 | `sha256:8edd6af39590e28fb24412177145c36c85e6174b8afedc0ef5dbe2a9a61437e9` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 29689155 | `sha256:ce22ad4e11c5c9cc4f67b469df12683fdc4f66994007e995f85faf03f59795db` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | `sha256:7a90431dbd2b8a9519345c73b6120f07fe19257008ee0a7b835328c85da0aba4` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 155006 | `sha256:25bd5a158b0d6ad6de9be6107dfc19fb56b48b155b8b7bf8a1f779c1e4d6245c` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` | 8432 | `sha256:b99b9b2cedfbe4fb1a8133fe6d726057ae6f93fc95280ca68ab29bb1f79f87e5` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` | 4940 | `sha256:9c827cac45f2b973f2e493726ee59e3814d875a6e9d5cae32c500b931a82b940` |

## Download Verification

Assets were downloaded with:

```powershell
gh release download v0.1.0-dev.3 --repo DouglasNLima/local-docs-studio --dir artifacts/windows/download-verification/v0.1.0-dev.3 --clobber
```

| Check | Result |
| --- | --- |
| Downloaded ZIP SHA256 | PASS, `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Downloaded installer SHA256 | PASS, `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| Installer `.sha256` file | PASS, contains `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB  LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |
| RC Markdown metadata | PASS with note: references frozen source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` and ZIP SHA256 `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9`; installer SHA256 is not present in this package RC Markdown |
| RC JSON metadata | PASS with note: references frozen source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` and ZIP SHA256 `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9`; installer SHA256 is not present in this package RC JSON |
| Installer report metadata | PASS, references frozen source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` and installer SHA256 `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |

## Downloaded ZIP Smoke

The downloaded ZIP was extracted to:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.3\extracted
```

Executable path:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.3\extracted\LensDocsStudio.Windows.exe
```

| Check | Result |
| --- | --- |
| Extract downloaded ZIP | PASS |
| Static package verification | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1 -NoBuild -StaticAppRoot artifacts/windows/download-verification/v0.1.0-dev.3/extracted/StaticApp` verified 53 service-worker assets and 150 vendor assets |
| Packaged native smoke against downloaded executable | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/download-verification/v0.1.0-dev.3/extracted/LensDocsStudio.Windows.exe -TimeoutSeconds 60` completed successfully |

## Installer Smoke

A contained silent installer smoke was run against the downloaded installer, installing to:

```text
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BA-Install
```

Install command shape:

```powershell
LensDocsStudio.Windows-0.1.0-dev-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3BA-Install"
```

| Check | Result |
| --- | --- |
| Silent install | PASS, installer log recorded `Installation process succeeded` |
| Installed executable | PASS, `C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BA-Install\LensDocsStudio.Windows.exe` existed |
| Native smoke against installed executable | PASS |
| Uninstaller present | PASS, `unins000.exe` existed in the temporary install root |
| Silent uninstall | PASS, uninstaller completed and wrote `C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BA-uninstall.log` |
| Uninstall registry entry after uninstall | PASS, absent |
| Lens-owned file association ProgIDs after uninstall | PASS, absent |
| Start Menu group after install/uninstall | PASS with note: the installer created a Start Menu group during install despite the suppressed-shortcut arguments, and uninstall removed it |
| Desktop shortcut after install/uninstall | PASS, absent |
| Install root after uninstall | Present with WebView2 runtime data only, matching the documented WebView2 data retention caveat. The temporary smoke install folder was then manually removed. |

Installer smoke logs were written under `%TEMP%`:

```text
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BA-install.log
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3BA-uninstall.log
```

## Manual Packaged Sanity

Manual packaged sanity remains `SKIPPED_INTERACTIVE_NATIVE_UI_CONTROL_NOT_EXECUTED`.

No real interactive packaged UI observation/control was completed during Phase 3BA for onboarding rendering, diagnostics, Open folder pending guidance, visible native picker operation, temporary workspace selection, cancellation, browser fallback inactivity, or watcher changed-on-disk copy. Phase 3AW remains the referenced manual packaged verification for those interactive native UI behaviours.

## Validation

Validation for the Phase 3BA documentation evidence update:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, 6 existing PRI qualifier warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp` |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS, wrote ignored local RC report and JSON metadata under `artifacts/windows/release-candidates/`; validation-generated ZIP SHA256 was `18CC675BDC2DDD44DD9B083D684A2420E3211DDE101759B8BD938655B1F6FEE7` and is not the published Phase 3BA asset |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS, Windows native bridge smoke completed successfully |
| `npm run test:browser` | Not rerun because Phase 3BA changed only release documentation and evidence, not frontend/runtime/static assets |

## Evidence Boundary

- Phase 3BA did not edit, delete, replace, re-upload, or create release assets.
- Phase 3BA did not create, move, or delete tags.
- Phase 3BA did not create, edit, or delete GitHub releases.
- Phase 3BA did not merge to `main`.
- Phase 3BA did not change runtime code.
- Phase 3BA did not change `v0.1.0-dev.1` or `v0.1.0-dev.2` release assets.
- Phase 3BA did not claim production readiness, go-live approval, stable-channel certification, or production release status.
