# Lens Docs Studio Final Internal RC Review

Final internal release-candidate review for the complete `v0.1.0-dev` draft prerelease, covering both the Windows ZIP package and the unsigned Inno Setup installer.

This review did not publish the draft release, create or move tags, create another release, merge to `main`, replace release assets, or rebuild artefacts.

## Release

- Tag: `v0.1.0-dev`
- URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/untagged-4f2f8830fe49faeb9207`
- Draft: `true`
- Prerelease: `true`
- Target commit: `8b215d039188af94d32857239e6829916f4b74fc`
- Review date: `2026-06-10`
- Repository: `DouglasNLima/local-docs-studio`
- Review branch: `develop`

## Git State

- Branch: `develop`
- Worktree before review: clean
- Local `develop` HEAD during review: `0f07ab45b2709238c0e65bb0c15e13a8b51e6cf8`
- `origin/develop` during review: `0f07ab45b2709238c0e65bb0c15e13a8b51e6cf8`
- Draft release target commit: `8b215d039188af94d32857239e6829916f4b74fc`

The draft release target commit matched the expected release commit. The local `develop` branch had advanced beyond the draft release target at review time.

## Assets

- ZIP: `LensDocsStudio.Windows-0.1.0-dev.zip`
- ZIP SHA256 file: `LensDocsStudio.Windows-0.1.0-dev.zip.sha256`
- RC report: `LensDocsStudio.Windows-0.1.0-dev-rc-report.md`
- Installer: `LensDocsStudio.Windows-0.1.0-dev-Setup.exe`
- Installer SHA256 file: `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256`
- Installer report: `LensDocsStudio.Windows-0.1.0-dev-Setup-report.md`

All expected assets were present on the draft release and downloaded from GitHub to `artifacts/release-review/v0.1.0-dev-final/`.

## Checksums

- ZIP SHA256: `8CD380C42C33AAB8FE02C82184FF0A6FFDE6FC0C6B077BE40E0CF6A51C7E6188`
- ZIP SHA256 result: matched `LensDocsStudio.Windows-0.1.0-dev.zip.sha256`
- Installer SHA256: `152051F7CDAB5A33F8D8E5C219F937A54777687B8980D750B486BE572883FA8E`
- Installer SHA256 result: matched `LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256`
- Installer known SHA256 result: matched the required review value

## Release Notes Review

- [x] ZIP fallback stated
- [x] ZIP remains the primary fallback
- [x] Installer stated as an additional internal RC asset
- [x] Installer stated as unsigned
- [x] Installer stated as Inno Setup based
- [x] Runtime prerequisites stated
- [x] File association behaviour stated
- [x] Start Menu shortcut stated
- [x] Desktop shortcut behaviour stated
- [x] Uninstall/WebView2 data caveat stated
- [x] Not stable/public production release stated

The draft release notes were updated during this review to explicitly state that the draft is not a stable or public production release. The release remained draft/prerelease and no assets were changed.

## ZIP Smoke

- [x] Downloaded from GitHub
- [x] SHA256 verified
- [x] Extracted
- [x] Executable found at `artifacts/release-review/v0.1.0-dev-final/zip-extracted/LensDocsStudio.Windows.exe`
- [x] Native smoke passed
- [x] App did not require a local server

Command:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 `
  -AppExecutablePath "artifacts/release-review/v0.1.0-dev-final/zip-extracted/LensDocsStudio.Windows.exe" `
  -TimeoutSeconds 90 `
  -KeepSmokeRoot
```

Result:

```text
[windows-native-smoke] PASS: Windows native bridge smoke completed successfully.
```

Smoke root:

```text
C:\Users\dougl\AppData\Local\Temp\lens-docs-studio-native-smoke-c2a020f0cee041c7b1af3345a7014bf6
```

## Installer Smoke

- [x] Downloaded from GitHub
- [x] SHA256 verified
- [x] Silent per-user install completed with exit code `0`
- [x] App launched
- [x] Native smoke passed
- [x] Start Menu shortcut verified and pointed to the installed executable
- [x] Desktop shortcut behaviour verified: no Desktop shortcut was created by the default silent install
- [x] File association task left unchecked/default-safe; no Lens-specific ProgIDs were present after install
- [x] Uninstalled with exit code `0`
- [x] Start Menu shortcut removed
- [x] Apps/uninstall registry entry removed
- [x] Remaining data documented

Installed executable:

```text
C:\Users\dougl\AppData\Local\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe
```

Installed smoke command:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 `
  -AppExecutablePath "$env:LOCALAPPDATA\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe" `
  -TimeoutSeconds 90 `
  -KeepSmokeRoot
```

Result:

```text
[windows-native-smoke] PASS: Windows native bridge smoke completed successfully.
```

Smoke root:

```text
C:\Users\dougl\AppData\Local\Temp\lens-docs-studio-native-smoke-523fdb44c90f440f83de70d6a018434e
```

Remaining data after uninstall:

```text
C:\Users\dougl\AppData\Local\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2
```

This matches the documented WebView2 user data caveat.

## Validation

- [x] `npm run test:static` passed
- [ ] `npm run test:browser` not run

Browser tests were skipped because this phase changed only release documentation and draft release notes after successful native ZIP and installer smoke tests.

## Verdict

- [x] `FINAL_INTERNAL_RC_READY_TO_PUBLISH_AS_PRERELEASE`
- [ ] `FINAL_INTERNAL_RC_READY_WITH_NOTES`
- [ ] `FINAL_INTERNAL_RC_NEEDS_FIXES`
- [ ] `FINAL_INTERNAL_RC_BLOCKED`

## Notes

- The draft release was not published.
- No release assets were created, moved, replaced, or deleted.
- No tags were created or moved.
- No merge to `main` was performed.
- The release URL changed from the earlier draft URL token after editing draft notes; the current draft URL is recorded above.
- Generated review artefacts remain under `artifacts/release-review/v0.1.0-dev-final/` and are not source-controlled.
