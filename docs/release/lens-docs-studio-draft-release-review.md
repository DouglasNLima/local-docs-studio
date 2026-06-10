# Lens Docs Studio Draft Release Review

Use this checklist to review an existing GitHub draft prerelease before internal release-candidate testing. It verifies that the uploaded draft assets, notes, checksum, and packaged Windows shell are suitable to keep as a draft for controlled RC testers.

This checklist does not publish the release, create or move tags, rebuild assets, replace uploaded files, merge to `main`, or certify a stable release.

## Release Metadata

- Tag: `v0.1.0-dev`
- Target commit: `8b215d039188af94d32857239e6829916f4b74fc`
- Draft: `true`
- Prerelease: `true`
- URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/untagged-79b1580fe9b2fc43270b`
- Review date: `2026-06-10`
- Repository: `DouglasNLima/local-docs-studio`
- Source branch policy: `develop`

## Assets

- [x] ZIP: `LensDocsStudio.Windows-0.1.0-dev.zip`
- [x] SHA256 file: `LensDocsStudio.Windows-0.1.0-dev.zip.sha256`
- [x] RC report: `LensDocsStudio.Windows-0.1.0-dev-rc-report.md`
- [x] Release notes: draft notes present on the GitHub release

Release asset sizes from GitHub:

| Asset | Size |
| --- | ---: |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | 33911043 bytes |
| `LensDocsStudio.Windows-0.1.0-dev.zip.sha256` | 104 bytes |
| `LensDocsStudio.Windows-0.1.0-dev-rc-report.md` | 8415 bytes |

## Release Notes Review

- [x] Product name and version are clear.
- [x] Target commit is listed.
- [x] ZIP, SHA256, and RC report assets are named.
- [x] Prerequisites are listed.
- [x] Install and run steps are short and understandable.
- [x] Capabilities describe local Markdown, Mermaid, file, workspace, watcher, setup, and file association workflows.
- [x] Known limitations are explicit.
- [x] SHA256 verification command is included.

## SHA256 Verification

Command:

```powershell
Get-FileHash artifacts/release-review/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip -Algorithm SHA256
Get-Content artifacts/release-review/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip.sha256
```

Result:

```text
8CD380C42C33AAB8FE02C82184FF0A6FFDE6FC0C6B077BE40E0CF6A51C7E6188
8CD380C42C33AAB8FE02C82184FF0A6FFDE6FC0C6B077BE40E0CF6A51C7E6188  LensDocsStudio.Windows-0.1.0-dev.zip
```

The downloaded ZIP hash matches the uploaded `.sha256` file, the release notes, and the known review value.

## ZIP Download And Extraction

Commands:

```powershell
New-Item -ItemType Directory -Force -Path artifacts/release-review/v0.1.0-dev

gh release download v0.1.0-dev `
  --repo DouglasNLima/local-docs-studio `
  --dir artifacts/release-review/v0.1.0-dev

Expand-Archive `
  -Path artifacts/release-review/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip `
  -DestinationPath artifacts/release-review/v0.1.0-dev/extracted `
  -Force
```

Result:

- [x] All three release assets downloaded.
- [x] ZIP extracted to a clean review folder.
- [x] Packaged executable located at `artifacts/release-review/v0.1.0-dev/extracted/LensDocsStudio.Windows.exe`.

## Native Bridge Smoke

Command:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 `
  -AppExecutablePath artifacts/release-review/v0.1.0-dev/extracted/LensDocsStudio.Windows.exe `
  -TimeoutSeconds 90
```

Result:

```text
PASS: Windows native bridge smoke completed successfully.
```

The smoke harness verified the downloaded packaged app launches, loads the packaged static app without a local HTTP server, reports the expected native bridge capabilities, opens and saves a startup file, opens/saves/saves-as a single Markdown file, opens a workspace, saves a workspace file, and creates a workspace file.

## Manual Smoke

### Launch

- [x] Extract ZIP to clean folder.
- [x] Run packaged app through native bridge smoke.
- [x] App opens without local server in native bridge smoke.
- [ ] App opens from normal interactive double-click or command launch.
- [ ] Help guide opens.
- [x] Native bridge diagnostics pass in native bridge smoke.
- [ ] Native bridge diagnostics pass through **Help > Check Windows bridge**.

### File Workflow

- [x] Open `.md` through native bridge smoke.
- [x] Edit through native bridge smoke.
- [x] Save changes through native bridge smoke.
- [x] Save as copy through native bridge smoke.
- [ ] Repeat open/edit/save/save-as interactively.

### Workspace Workflow

- [x] Open workspace folder through native bridge smoke.
- [x] Create Markdown file through native bridge smoke.
- [x] Save workspace file through native bridge smoke.
- [ ] External change marker appears.
- [ ] Refresh external change.
- [ ] Dirty conflict preserves local edits.

### Setup / Release UX

- [ ] First-run setup appears when expected.
- [ ] Skip setup works.
- [ ] Help > Open setup wizard works.
- [ ] File association guidance is clear.

## Known Limitations Confirmed

- [x] Unsigned ZIP.
- [x] No MSIX.
- [x] No installer.
- [x] No auto-update.
- [x] No WebView2 bootstrapper.
- [x] Requires runtime prerequisites.
- [x] File associations manual/dev only.

## Decision

Choose one:

- [x] `DRAFT_RELEASE_READY_FOR_INTERNAL_RC`
- [ ] `DRAFT_RELEASE_NEEDS_DOC_FIX`
- [ ] `DRAFT_RELEASE_NEEDS_PACKAGE_FIX`
- [ ] `DRAFT_RELEASE_BLOCKED`

## Notes

- The draft should remain unpublished until an explicit publication decision is made.
- The uploaded RC report records `Result: PASS` for the package certification on commit `8b215d039188af94d32857239e6829916f4b74fc`.
- The current review confirms draft metadata, notes readability, asset presence, download, SHA256 verification, extraction, and automated native bridge smoke on the downloaded ZIP.
- Interactive tester checks remain open for first-run setup UX, help guide opening, file association guidance, and workspace watcher/conflict UX. These do not block internal RC testing, but they should be completed before publishing beyond the controlled draft prerelease audience.
