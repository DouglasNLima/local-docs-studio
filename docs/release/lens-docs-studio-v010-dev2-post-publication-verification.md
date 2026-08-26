# Lens Docs Studio v0.1.0-dev.2 Phase 3AT Post-Publication Verification

Verification date: 2026-06-12

## Summary

Phase 3AT is a read-only post-publication verification checkpoint for the GitHub prerelease `v0.1.0-dev.2`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.2`

No GitHub release assets, tags, or releases were edited, deleted, replaced, re-uploaded, or created during this checkpoint. No merge to `main` was performed. No production readiness, go-live approval, stable-channel certification, or production release status is claimed.

`v0.1.0-dev.1` assets were checked read-only and were not changed by this checkpoint.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Start commit | `f5104521f684de007e00593d5be3e1f370fcf5c8` |
| Publication evidence commit in history | PASS, `f5104521f684de007e00593d5be3e1f370fcf5c8` is `HEAD` and is an ancestor of `HEAD` |
| Tracked status at start | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |
| Downloaded verification assets | Stored under ignored path `artifacts/windows/download-verification/v0.1.0-dev.2/` |

`git status --short --ignored` emitted Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. The tracked working tree remained clean before the Phase 3AT documentation edit.

## Read-Only Release Verification

Read-only commands used:

```powershell
gh release view v0.1.0-dev.2 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,createdAt,publishedAt,assets
gh api repos/DouglasNLima/local-docs-studio/git/ref/tags/v0.1.0-dev.2
gh api repos/DouglasNLima/local-docs-studio/git/tags/24b700ed1e873d048cf381be8b7840a0275dbeff
git ls-remote origin "refs/tags/v0.1.0-dev.2" "refs/tags/v0.1.0-dev.2^{}"
gh release view --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,name,url,targetCommitish,createdAt,publishedAt
gh release view v0.1.0-dev.1 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,createdAt,publishedAt,assets
```

| Check | Result |
| --- | --- |
| Tag `v0.1.0-dev.2` exists | PASS |
| Release `v0.1.0-dev.2` exists | PASS |
| Release is draft | PASS, `false` |
| Release is prerelease | PASS, `true` |
| Release is not a production release | PASS, tagged release is explicitly a prerelease; `gh release view` without a tag returned `release not found`, so no latest production release was visible through this GitHub CLI endpoint |
| Tag object | `24b700ed1e873d048cf381be8b7840a0275dbeff` |
| Peeled tag commit | PASS, `5602358ef241c144fccae13e980c9eb2920126d8` |
| Release API `target_commitish` | Note: GitHub reported `main`, but the actual annotated tag peels to the frozen artefact source commit above |
| `v0.1.0-dev.1` assets | PASS, read-only metadata matched the previously documented six-asset inventory, sizes, digests, and upload/update timestamps |

## Asset List

Expected asset set matched exactly. No unexpected extra assets were attached.

| Asset | Size (bytes) | GitHub digest |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 34031627 | `sha256:c7c9e52322eda140d9ab60f9d8d2bf257eed9ea898f50fc8efa2d90a04a9bf0b` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 29689316 | `sha256:01c60dfaa57754effccc763051d5eede0db4e3284936c8bf7d1189d30dca6c21` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | `sha256:a87719d745532bcc2a42686306a74a996257429968c1d869fbd3a5c43f557d52` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 154014 | `sha256:cfe430bf659cc5379b735b3d3ea9720a52321e1e69762f0b18a6a376fc93d465` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.md` | 8432 | `sha256:68cc61fa1547863aee664bc3673856a6f97138855c1384d2ed488abbdd350ea6` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T105712Z.json` | 4940 | `sha256:9d1a124461a3d98399d5a0dd31b6071e9e6ae73ac11c0516e723ccb5e0fc4726` |

## Download Verification

Assets were downloaded with:

```powershell
gh release download v0.1.0-dev.2 --repo DouglasNLima/local-docs-studio --dir artifacts/windows/download-verification/v0.1.0-dev.2 --clobber
```

| Check | Result |
| --- | --- |
| Downloaded ZIP SHA256 | PASS, `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B` |
| Downloaded installer SHA256 | PASS, `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |
| Installer `.sha256` file | PASS, contains `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21  LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |
| RC Markdown metadata | PASS with note: references frozen source commit `5602358ef241c144fccae13e980c9eb2920126d8` and ZIP SHA256 `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B`; installer SHA256 is not present in this package RC Markdown |
| RC JSON metadata | PASS with note: references frozen source commit `5602358ef241c144fccae13e980c9eb2920126d8` and ZIP SHA256 `C7C9E52322EDA140D9AB60F9D8D2BF257EED9EA898F50FC8EFA2D90A04A9BF0B`; installer SHA256 is not present in this package RC JSON |
| Installer report metadata | PASS, references frozen source commit `5602358ef241c144fccae13e980c9eb2920126d8` and installer SHA256 `01C60DFAA57754EFFCCC763051D5EEDE0DB4E3284936C8BF7D1189D30DCA6C21` |

## Downloaded ZIP Smoke

The downloaded ZIP was extracted to:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.2\extracted
```

Executable path:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.2\extracted\LensDocsStudio.Windows.exe
```

| Check | Result |
| --- | --- |
| Extract downloaded ZIP | PASS |
| Static package verification | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1 -NoBuild -StaticAppRoot artifacts/windows/download-verification/v0.1.0-dev.2/extracted/StaticApp` verified 53 service-worker assets and 150 vendor assets |
| Packaged native smoke against downloaded executable | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/download-verification/v0.1.0-dev.2/extracted/LensDocsStudio.Windows.exe -TimeoutSeconds 60` completed successfully |

## Installer Smoke

A contained silent installer smoke was run because no default install, uninstall entry, desktop shortcut, or Lens-owned registry entries were present before the clean rerun. A pre-existing Start Menu group was detected before the rerun, and the suppressed-shortcut install/uninstall left it absent afterwards.

An earlier installer-smoke wrapper attempt left a stale per-user uninstall entry pointing at the Phase 3AT temp install path after the temp install directory had already been removed. That entry was created by this checkpoint attempt and was removed before the clean rerun.

Install command shape:

```powershell
LensDocsStudio.Windows-0.1.0-dev-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3AT-Install"
```

| Check | Result |
| --- | --- |
| Silent install exit code | PASS, `0` |
| Installed executable | `C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3AT-Install\LensDocsStudio.Windows.exe` |
| Native smoke against installed executable | PASS |
| Uninstaller present | PASS |
| Silent uninstall exit code | PASS, `0` |
| Uninstall registry entry after uninstall | PASS, absent |
| Lens-owned file association ProgIDs after uninstall | PASS, absent |
| Start Menu group after suppressed-task install/uninstall | PASS, absent |
| Desktop shortcut after suppressed-task install/uninstall | PASS, absent |
| Install root after uninstall | Present with WebView2 runtime data only, matching the documented WebView2 data retention caveat. The temporary smoke install folder was then manually removed. |

Installer smoke logs were written under `%TEMP%`:

```text
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3AT-install.log
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3AT-uninstall.log
```

## Manual Packaged Sanity

Manual packaged sanity remains `BLOCKED_MANUAL_PACKAGED_SANITY_NOT_EXECUTED`.

No real interactive packaged UI observation/control was completed during Phase 3AT for first-run/onboarding copy, Open folder primary placement, Open file availability, Diagnostics reachability, diagnostics route/fallback states, native folder picker opening, or watcher/conflict wording.

## Validation

Validation for the Phase 3AT documentation evidence update:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, 6 existing PRI qualifier warnings and 0 errors |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp` |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS, wrote ignored local RC report and JSON metadata under `artifacts/windows/release-candidates/`; validation-generated ZIP SHA256 was `AD76A0C33F147518D18FCF7B2082D8EC2AC519BE3DD8238A60F509BC4A1F9B3D` and is not the published Phase 3AT asset |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS, Windows native bridge smoke completed successfully |
| `npm run test:browser` | Not rerun because Phase 3AT changed only release documentation and evidence, not frontend/runtime/static assets |

## Evidence Boundary

- Phase 3AT did not edit, delete, replace, re-upload, or create release assets.
- Phase 3AT did not create, move, or delete tags.
- Phase 3AT did not create, edit, or delete GitHub releases.
- Phase 3AT did not merge to `main`.
- Phase 3AT did not change runtime code.
- Phase 3AT did not change `v0.1.0-dev.1` release assets.
- Phase 3AT did not claim production readiness, go-live approval, stable-channel certification, or production release status.
