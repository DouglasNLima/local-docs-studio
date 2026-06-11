# Lens Docs Studio v0.1.0-dev.1 Phase 3AF Post-Publication Verification

Verification date: 2026-06-11

## Summary

Phase 3AF is a read-only post-publication verification checkpoint for the GitHub prerelease `v0.1.0-dev.1`.

Release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

No GitHub release assets, tags, or releases were edited, deleted, replaced, re-uploaded, or created during this checkpoint. No merge to `main` was performed. No production readiness, go-live approval, or stable-channel certification is claimed.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Start commit | `28f9dd2705e62ea8e74bea213c5c47e0b1cc64a4` |
| Publication evidence commit in history | PASS, `28f9dd2705e62ea8e74bea213c5c47e0b1cc64a4` is an ancestor of `HEAD` |
| Tracked status at start | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |
| Downloaded verification assets | Stored under ignored path `artifacts/windows/download-verification/v0.1.0-dev.1/` |

`git status --short --ignored` emitted Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. The tracked working tree remained clean before the Phase 3AF documentation edit.

## Read-Only Release Verification

Read-only commands used:

```powershell
gh release view v0.1.0-dev.1 --repo DouglasNLima/local-docs-studio --json tagName,name,isDraft,isPrerelease,url,createdAt,publishedAt,targetCommitish,assets,isImmutable
gh api repos/DouglasNLima/local-docs-studio/releases/tags/v0.1.0-dev.1
git ls-remote origin "refs/tags/v0.1.0-dev.1" "refs/tags/v0.1.0-dev.1^{}"
```

| Check | Result |
| --- | --- |
| Tag `v0.1.0-dev.1` exists | PASS |
| Release `v0.1.0-dev.1` exists | PASS |
| Release is draft | PASS, `false` |
| Release is prerelease | PASS, `true` |
| Latest production release visibility | PASS with note: `gh release view` without a tag returned `release not found`, so no latest production release was visible through this GitHub CLI endpoint. The tagged release itself is explicitly a prerelease. |
| Tag object | `efc9b8bf26e8ada69ee58c90c8595e32eba72963` |
| Peeled tag commit | PASS, `c776fffdc8364c2c978f8106869a2d6bf50477ff` |
| Release API `target_commitish` | Note: GitHub reported `main`, but the actual tag peels to the frozen source commit above. |

## Asset List

Expected asset set matched exactly. No unexpected extra assets were attached.

| Asset | Size (bytes) | GitHub digest |
| --- | ---: | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 33972441 | `sha256:3c9c343b8abb06655b6a2dd55caceeff2542ed64bcf4ff943afc7a5957c79a7a` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | 29656077 | `sha256:4c2a79c7df446957de6f6d2e41271d0c7563178bb47ecacc09e001752c930745` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | 110 | `sha256:e53adc81514f9906573b9607bc5c77e95b75bffd7f778f61854bf86ade89b892` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | 151586 | `sha256:c55648c907e203e36cab1dcea0ed7d7bac48cff8d7e0647682c72f5e0133c312` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.md` | 8434 | `sha256:97cc6a5067b25f0f88b448884e01b7bb1460cd9ef3211e4efa616304f865a95c` |
| `LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.json` | 4941 | `sha256:68006892265688390edde53bd17accc8f08e97940f64775543fd8193edf544e8` |

## Download Verification

Assets were downloaded with:

```powershell
gh release download v0.1.0-dev.1 --repo DouglasNLima/local-docs-studio --dir artifacts/windows/download-verification/v0.1.0-dev.1 --clobber
```

| Check | Result |
| --- | --- |
| Downloaded ZIP SHA256 | PASS, `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` |
| Downloaded installer SHA256 | PASS, `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` |
| Installer `.sha256` file | PASS, contains `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745  LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |
| RC Markdown metadata | PASS with note: references frozen source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff` and ZIP SHA256 `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A`; installer SHA256 is not present in this package RC Markdown. |
| RC JSON metadata | PASS with note: references frozen source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff` and ZIP SHA256 `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A`; installer SHA256 is not present in this package RC JSON. |
| Installer report metadata | PASS, references frozen source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff` and installer SHA256 `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745`. |

## Downloaded ZIP Smoke

The downloaded ZIP was extracted to:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.1\extracted
```

Executable path:

```text
C:\Code\MarkdownReader\artifacts\windows\download-verification\v0.1.0-dev.1\extracted\LensDocsStudio.Windows.exe
```

| Check | Result |
| --- | --- |
| Extract downloaded ZIP | PASS |
| Static package verification | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1 -NoBuild -StaticAppRoot artifacts/windows/download-verification/v0.1.0-dev.1/extracted/StaticApp` verified 53 service-worker assets and 150 vendor assets. |
| Packaged native smoke against downloaded executable | PASS, `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/download-verification/v0.1.0-dev.1/extracted/LensDocsStudio.Windows.exe -TimeoutSeconds 60` completed successfully. |

Stage A and Stage B watcher/conflict evidence remains referenced from Phase 3AA. Phase 3AF did not re-claim or repeat the manual Stage A diagnostics, native picker workspace selection, or Stage B watcher/conflict scenarios. Phase 3AF only confirms the published ZIP asset can be downloaded, extracted, statically verified, and smoke-tested through the native bridge harness.

## Installer Smoke

A contained silent installer smoke was run because no existing default install, uninstall entry, Start Menu group, or Lens-owned file-association keys were present before the test.

Pre-checks:

| Check | Result |
| --- | --- |
| Default installed executable existed | `false` |
| HKCU uninstall entry existed | `false` |
| Lens-owned file association ProgIDs existed | `false` |
| Start Menu group existed | `false` |

Install command shape:

```powershell
LensDocsStudio.Windows-0.1.0-dev-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3AF-Install"
```

| Check | Result |
| --- | --- |
| Silent install exit code | PASS, `0` |
| Installed executable | `C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3AF-Install\LensDocsStudio.Windows.exe` |
| Native smoke against installed executable | PASS |
| Uninstaller present | PASS |
| Silent uninstall exit code | PASS, `0` |
| Uninstall registry entry after uninstall | PASS, absent |
| Lens-owned file association ProgIDs after uninstall | PASS, absent |
| Start Menu group after suppressed-task install | PASS, absent |
| Desktop shortcut after suppressed-task install | PASS, absent |
| Install root after uninstall | Present with WebView2 runtime data only, matching the documented WebView2 data retention caveat. The temporary smoke install folder was then manually removed. |

Installer smoke logs were written under `%TEMP%`:

```text
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3AF-install.log
C:\Users\dougl\AppData\Local\Temp\LensDocsStudio-Phase3AF-uninstall.log
```

## Validation

Validation for the Phase 3AF documentation evidence update:

| Command | Result |
| --- | --- |
| `npm run test:static` | PASS, static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS, 0 warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS, verified 53 service-worker assets and 150 vendor assets in packaged `StaticApp`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS, wrote ignored local RC report and JSON metadata under `artifacts/windows/release-candidates/`. The validation-generated ZIP is not the published Phase 3AF asset. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS, Windows native bridge smoke completed successfully. |
| `npm run test:browser` | Not rerun because Phase 3AF changed only release documentation and evidence, not frontend/runtime/static assets. |

## Evidence Boundary

- Phase 3AF did not edit, delete, replace, re-upload, or create release assets.
- Phase 3AF did not create, move, or delete tags.
- Phase 3AF did not create, edit, or delete GitHub releases.
- Phase 3AF did not merge to `main`.
- Phase 3AF did not change runtime code.
- Phase 3AF did not claim production readiness, go-live approval, or stable-channel certification.
