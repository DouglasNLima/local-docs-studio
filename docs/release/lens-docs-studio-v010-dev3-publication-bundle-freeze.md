# Lens Docs Studio v0.1.0-dev.3 Publication Bundle Freeze

Freeze date: 2026-06-12

## Summary

Phase 3AX freezes a local, unpublished release-candidate publication bundle for a possible future `v0.1.0-dev.3` prerelease.

Reason for candidate: the published `v0.1.0-dev.2` package failed Phase 3AV manual packaged sanity when **Open folder** stayed pending and no usable native folder picker result returned. Phase 3AW added bounded pending guidance for native Open folder requests and verified a freshly rebuilt package manually. Phase 3AX packages that remediation into a local frozen candidate bundle only.

No publication was performed. No release assets, tags, GitHub Releases, or `main` merges were changed. Existing `v0.1.0-dev.1` and `v0.1.0-dev.2` release assets were not changed. Production readiness, go-live approval, stable-channel certification, and stable/latest release status are not claimed.

## Repository State

| Check | Result |
| --- | --- |
| Branch | `develop` |
| Source commit used for frozen artefacts | `ea75e13bf72518eb417cf11e4f62d7730738c8cd` |
| Phase 3AW commit in history | PASS, `ea75e13bf72518eb417cf11e4f62d7730738c8cd` is `HEAD` and is contained in `develop` |
| Tracked status before freeze work | Clean, `## develop...origin/develop` |
| Ignored/generated paths present | `artifacts/`, `node_modules/`, `src/windows/.vs/`, `src/windows/LensDocsStudio.Windows/bin/`, `src/windows/LensDocsStudio.Windows/obj/`, `test-results/` |

`git status --short --ignored` emitted existing Windows long-path warnings while scanning generated WebView2 cache folders under ignored Windows build output. The tracked working tree remained clean before the documentation edit.

## Phase 3AW Remediation Summary

Phase 3AW investigated the Phase 3AV packaged native picker failure. The exact no-dialog/no-return symptom was not reproduced in the same local environment, but a related product defect was confirmed: after dispatching a native `workspace.openFolder` request, the app could remain on `Opening folder from Windows...` for an extended interactive timeout without bounded operator guidance if the picker was hidden, blocked, or otherwise not completing.

The Phase 3AW runtime remediation:

- keeps browser fallback inactive while the Windows native bridge is present;
- records the native Open folder attempt as `pending` after a bounded wait;
- shows `Still waiting for the Windows folder picker. Check for a visible Select Folder window, then choose a folder or cancel.`;
- exposes the pending state in Windows shell diagnostics;
- still accepts later native success or cancellation normally.

Phase 3AW manual packaged verification passed against its freshly rebuilt package: onboarding opened, diagnostics passed, the native picker appeared visibly, a temporary workspace was selected, cancellation returned cleanly, browser fallback remained inactive, and watcher changed-on-disk copy was reached.

## Frozen Artefact Set

Local frozen bundle:

```text
C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3
```

This folder is a local frozen candidate only. It was not uploaded, attached to a release, tagged, or published.

| Artefact | Source path | Frozen file | SHA256 |
| --- | --- | --- | --- |
| Windows ZIP package | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip` | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev.zip` | `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Unsigned Inno Setup installer | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| Installer checksum | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` | Contains installer SHA256 above |
| Installer report | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` | References source commit and installer hash above |
| RC Markdown metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` | References source commit and ZIP hash above |
| RC JSON metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` | `C:\Code\MarkdownReader\artifacts\windows\publication-bundles\v0.1.0-dev.3\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` | References source commit and ZIP hash above |

## Release-Candidate Metadata

| Field | Value |
| --- | --- |
| RC build timestamp UTC | `20260612T144833Z` |
| RC Markdown metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` |
| RC JSON metadata | `C:\Code\MarkdownReader\artifacts\windows\release-candidates\LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.json` |
| Package output folder | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev` |
| ZIP path | `C:\Code\MarkdownReader\artifacts\windows\LensDocsStudio.Windows-0.1.0-dev.zip` |
| ZIP size | 34054203 bytes |
| ZIP SHA256 | `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` |
| Installer path | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |
| Installer SHA256 | `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` |
| Installer checksum path | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` |
| Installer report path | `C:\Code\MarkdownReader\artifacts\installers\inno\LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` |

## Frozen File Verification

Hashes were recomputed from the frozen bundle copies, not only from the working artefact folders.

| Check | Result |
| --- | --- |
| Frozen ZIP SHA256 matches documented ZIP SHA256 | PASS |
| Frozen installer SHA256 matches documented installer SHA256 | PASS |
| Frozen installer `.sha256` matches frozen installer hash | PASS |
| Frozen RC JSON references source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` | PASS |
| Frozen RC JSON references ZIP SHA256 `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9` | PASS |
| Frozen installer report references source commit `ea75e13bf72518eb417cf11e4f62d7730738c8cd` | PASS |
| Frozen installer report references installer SHA256 `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB` | PASS |

## Read-Only GitHub Release And Tag Check

Read-only commands used:

```powershell
git ls-remote origin "refs/tags/v0.1.0-dev.3" "refs/tags/v0.1.0-dev.3^{}"
gh release view v0.1.0-dev.3 --repo DouglasNLima/local-docs-studio --json tagName,isPrerelease,isDraft,isImmutable,name,url,targetCommitish,createdAt,publishedAt,assets
gh api repos/DouglasNLima/local-docs-studio/git/ref/tags/v0.1.0-dev.3
```

| Check | Result |
| --- | --- |
| Tag `v0.1.0-dev.3` | Missing |
| Release `v0.1.0-dev.3` | Missing, `gh release view` returned `release not found` |
| Attached assets | None, because the release is missing |
| Later publication disposition | Would be create-new if explicitly approved later, not update-existing |

No release or tag command modified GitHub state.

## Validation

| Command | Result | Notes |
| --- | --- | --- |
| `npm run test:static` | PASS | Static checks passed for 46 module files, 53 shell assets, 150 vendor assets, and 52 runtime external-dependency scans. |
| `npm run test:browser` | PASS | Completed 236 Playwright browser smoke tests across Chromium and Microsoft Edge. |
| `dotnet build src/windows/LensDocsStudio.Windows.sln` | PASS | Build succeeded with 0 warnings and 0 errors. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` | PASS | Debug Windows shell build succeeded and packaged `StaticApp/` verified 53 service-worker assets and 150 vendor assets. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` | PASS | Final RC metadata pass wrote `LensDocsStudio.Windows-0.1.0-dev-rc-20260612T144833Z.md` and `.json`; ZIP SHA256 `8EDD6AF39590E28FB24412177145C36C85E6174B8AFEDC0EF5DBE2A9A61437E9`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` | PASS | Development build native bridge smoke completed successfully. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke` | PASS | Rebuilt local Windows folder/ZIP package; native smoke intentionally skipped by this package command flag. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild` | PASS | Built unsigned Inno Setup installer from the final package folder; installer SHA256 `CE22AD4E11C5C9CC4F67B469DF12683FDC4F66994007E995F85FAF03F59795DB`. |
| `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe` | PASS | Packaged native bridge smoke completed successfully against the final rebuilt package executable. |

## Manual Packaged Sanity

Optional manual packaged sanity for Phase 3AX was skipped because no checked-in helper automates the real native picker UI and this freeze did not have a separate human/operator interaction step beyond the automated packaged native smoke. The current manual packaged evidence remains the Phase 3AW verification against a freshly rebuilt package, where onboarding, diagnostics, visible native picker selection, cancellation, inactive browser fallback, and watcher changed-on-disk copy were observed as PASS.

No Stage B watcher/conflict pass is inferred from Phase 3AX automation alone.

## Publication Boundary

This Phase 3AX freeze:

- did not publish `v0.1.0-dev.3`;
- did not create or move tags;
- did not create, edit, publish, unpublish, or delete GitHub Releases;
- did not edit, delete, replace, re-upload, rebuild, or republish existing release assets;
- did not change `v0.1.0-dev.1` or `v0.1.0-dev.2` release assets;
- did not merge to `main`;
- did not stage generated package, installer, downloaded, or frozen bundle artefacts as source;
- did not claim production readiness, go-live approval, stable-channel certification, or stable/latest release status.

## Verdict

The local `v0.1.0-dev.3` publication bundle is frozen and ready for a later publication approval gate if one is explicitly granted. That later gate would still need to create the missing tag/release and upload assets intentionally. This document is not that approval and performs no publication.
