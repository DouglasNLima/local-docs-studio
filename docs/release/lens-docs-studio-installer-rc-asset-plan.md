# Lens Docs Studio Installer RC Asset Plan

Status: Phase 3N planning gate.

This plan decides whether the certified Phase 3M.1 Inno Setup installer should be added as extra assets on the existing `v0.1.0-dev` GitHub draft prerelease. It does not upload assets, publish the draft, create a new release, create or move tags, merge to `main`, sign binaries, add auto-update, add MSIX/WiX, add runtime bootstrapping, or change app behaviour.

Post-publication note: Phase 3Q later recorded `v0.1.0-dev` as a public GitHub prerelease with the ZIP fallback and unsigned Inno Setup installer assets present. See `docs/release/lens-docs-studio-v0.1.0-dev-publication-record.md`; keep this Phase 3N document as the historical upload plan.

## Context

The Windows-first release path is active on `develop`. ZIP plus GitHub Releases remains the internal release-candidate fallback, and the current GitHub release is still a draft prerelease for controlled RC review.

Phase 3M added a local Inno Setup installer MVP. Phase 3M.1 installed Inno Setup, built the real installer, and recorded an install/uninstall smoke result. The certified installer is an internal unsigned RC artefact; it is not a stable installer and it does not install runtime prerequisites.

## Current Draft Release State

Live GitHub release metadata checked with:

```powershell
gh release view v0.1.0-dev --repo DouglasNLima/local-docs-studio --json tagName,targetCommitish,isDraft,isPrerelease,url,assets,body,name
```

Current state:

| Field | Value |
| --- | --- |
| Repository | `DouglasNLima/local-docs-studio` |
| Tag | `v0.1.0-dev` |
| Target commit | `8b215d039188af94d32857239e6829916f4b74fc` |
| Draft | `true` |
| Prerelease | `true` |
| URL | `https://github.com/DouglasNLima/local-docs-studio/releases/tag/untagged-79b1580fe9b2fc43270b` |

Current uploaded assets:

| Asset | SHA256 / digest | Notes |
| --- | --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | `8cd380c42c33aab8fe02c82184ff0a6ffde6fc0c6b077be40e0cf6a51c7e6188` | Primary internal RC fallback. |
| `LensDocsStudio.Windows-0.1.0-dev.zip.sha256` | `97fd5bcbda8a7e7415f24c783acd638dde36566daa24c80f547866d8d2b6e721` | Uploaded checksum file asset digest. |
| `LensDocsStudio.Windows-0.1.0-dev-rc-report.md` | `27d454c43bbd5e8e418a725e744dc43e75ed2323b96d1b7c92e7ccd444b4688d` | ZIP RC report asset digest. |

The draft release target still matches the ZIP RC baseline recorded by the draft release review. The installer build was certified later from commit `9439f95813d6e8654032b7fef3b414af1fa267e6`, and the latest known Phase 3M.1 documentation commit is `b74b6242ee7c8dcef29ccdaa2244551b4714612a`. Do not retarget the draft or move tags in this phase; instead, make installer provenance explicit in the release notes before any installer asset upload.

## Current Installer Certification State

Installer report checked at:

```text
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md
```

Certification state:

| Field | Value |
| --- | --- |
| Result | `PASS` |
| Build commit | `9439f95813d6e8654032b7fef3b414af1fa267e6` |
| Latest Phase 3M.1 commit | `b74b6242ee7c8dcef29ccdaa2244551b4714612a` |
| Inno Setup | `6.7.3` |
| Inno compiler | `C:\Users\dougl\AppData\Local\Programs\Inno Setup 6\ISCC.exe` |
| Installer path | `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe` |
| Installer report path | `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md` |
| Installer SHA256 file | `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256` |

The installer smoke result recorded in Phase 3M.1:

- Baseline install used the default per-user path and did not require elevation.
- Start Menu shortcut was created and pointed at the installed executable.
- Optional Desktop shortcut was selected for the smoke and pointed at the installed executable.
- Installed-app native bridge smoke passed against `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe`.
- File associations were left unchecked; no Lens ProgId keys or `UserChoice` writes were observed.
- Uninstall completed and removed shortcuts plus the installed-app entry.
- Caveat: a WebView2 runtime data folder remained under the install path after native smoke: `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2\EBWebView`.

## Asset Candidates

If the next phase approves upload, add these assets to the existing draft release:

```text
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md
```

Do not commit these generated artefacts.

## SHA256 Values

The installer SHA256 was verified locally with:

```powershell
Get-FileHash -Algorithm SHA256 artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe
Get-Content artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256
```

Expected installer SHA256:

```text
152051F7CDAB5A33F8D8E5C219F937A54777687B8980D750B486BE572883FA8E
```

The `.sha256` file currently contains:

```text
152051F7CDAB5A33F8D8E5C219F937A54777687B8980D750B486BE572883FA8E  LensDocsStudio.Windows-0.1.0-dev-Setup.exe
```

## Recommendation

Recommended decision: `A. Add the Inno installer .exe and .sha256 as additional draft release assets.`

Also upload the installer report as supporting evidence in the same next phase. The installer has a passing build report, a matching SHA256, and a passing baseline install/uninstall smoke. Adding assets to an existing draft release is acceptable because `gh release upload` uploads files to the existing release asset set and does not publish the draft.

Conditions for the next phase:

- Keep the GitHub release as draft and prerelease.
- Do not create a new release.
- Do not create or move tags.
- Do not retarget the draft release.
- Amend release notes before upload or before any later publication.
- Stop if any candidate asset name already exists on the draft release unless an operator explicitly approves a clobbering plan.
- Keep the ZIP as the primary fallback and do not replace or delete existing ZIP assets.

## Release Notes Impact

Before installer assets are uploaded or the draft is ever published, amend the draft release notes to mention:

- ZIP package remains available and remains the primary fallback.
- Installer is an unsigned internal RC installer.
- Installer is Inno Setup based.
- Runtime prerequisites are still required: .NET 8 Desktop Runtime, matching Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- File association task is optional and unchecked/default-safe.
- Start Menu shortcut is created by default.
- Desktop shortcut is optional.
- Uninstall passed, with the WebView2 user data caveat.
- Installer report records build commit `9439f95813d6e8654032b7fef3b414af1fa267e6`, while the existing draft release target remains the ZIP RC baseline `8b215d039188af94d32857239e6829916f4b74fc`.

Do not edit GitHub release notes in this planning phase.

## Upload Plan

Prepare this command for Phase 3N.1 only, after release notes are reviewed and duplicate asset names are checked:

```powershell
gh release upload v0.1.0-dev `
  artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe `
  artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256 `
  artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md `
  --repo DouglasNLima/local-docs-studio
```

Do not use `--clobber` in Phase 3N.1 unless a later explicit instruction approves replacing matching assets. `gh release upload --help` warns that `--clobber` deletes existing matching assets before re-uploading them.

Suggested duplicate-name check before upload:

```powershell
gh release view v0.1.0-dev `
  --repo DouglasNLima/local-docs-studio `
  --json assets
```

If any existing asset name matches one of the three installer candidate names, stop.

## Verification Plan After Upload

After a future approved upload:

1. Confirm the release remains `Draft: true` and `Prerelease: true`.
2. Confirm the original ZIP, ZIP `.sha256`, and ZIP RC report assets are still present.
3. Confirm the installer `.exe`, installer `.sha256`, and installer report assets are present.
4. Download the installer `.exe` and `.sha256` from the draft release to a clean ignored review folder.
5. Verify the downloaded installer SHA256 matches `152051F7CDAB5A33F8D8E5C219F937A54777687B8980D750B486BE572883FA8E`.
6. Review the uploaded installer report.
7. Confirm release notes describe the installer as unsigned/internal RC, Inno Setup based, and prerequisite-dependent.
8. Confirm the release was not published and no new release or tag was created.

Optional post-upload smoke on a Windows test machine:

1. Install without elevation to the default per-user path.
2. Leave file associations unchecked.
3. Launch from Start Menu.
4. Run **Help > Check Windows bridge**.
5. Open, edit, and save a local Markdown file.
6. Uninstall from Windows Settings or the uninstaller shortcut.
7. Confirm shortcuts and installer-owned files are removed, allowing for the documented WebView2 user data caveat.

## Rollback Plan

If Phase 3N.1 uploads the installer assets and a problem is found while the release is still draft:

1. Keep the release as draft and prerelease.
2. Do not publish, create another release, move tags, or merge to `main`.
3. Remove only the newly uploaded installer assets if explicitly instructed.
4. Leave the existing ZIP, ZIP `.sha256`, and ZIP RC report assets untouched.
5. Revert or amend any release-note additions about the installer if the installer assets are removed.
6. Record the rollback decision in release docs before any renewed upload attempt.

If the issue is checksum, provenance, or install/uninstall confidence, choose decision `C` in a follow-up phase and rebuild/re-certify before considering another upload.

## Non-goals

- Publishing the draft release.
- Creating a new release.
- Creating or moving tags.
- Uploading assets in this planning phase.
- Replacing or deleting existing ZIP assets.
- Merging to `main`.
- Implementing code signing.
- Implementing auto-update.
- Implementing MSIX or WiX.
- Implementing a runtime bootstrapper.
- Changing runtime app behaviour.

## Final Decision

Decision: `A`.

Add the certified Inno Setup installer `.exe`, `.sha256`, and report as additional assets to the existing `v0.1.0-dev` draft prerelease in a separate explicitly approved upload phase. Keep the release unpublished, keep the ZIP assets as the primary fallback, and update draft release notes before upload or publication so the installer is clearly described as unsigned, internal RC, prerequisite-dependent, and certified separately from the ZIP baseline.
