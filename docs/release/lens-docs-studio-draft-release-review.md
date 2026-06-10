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

Phase 3K closure status: `INTERNAL_RC_MANUAL_SMOKE_PASSED_WITH_NOTES`.

### Launch

- [x] Extract ZIP to clean folder.
- [x] Run packaged app through native bridge smoke.
- [x] App opens without local server in native bridge smoke.
- [x] App opens from normal interactive command launch.
- [x] Help guide opens.
- [x] Native bridge diagnostics pass in native bridge smoke.
- [x] Native bridge diagnostics pass through first-run runtime readiness.

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
- [x] External change marker appears.
- [x] Refresh external change.
- [x] Dirty conflict preserves local edits.

### Setup / Release UX

- [x] First-run setup appears when expected.
- [x] Skip setup works.
- [x] Help > Open setup wizard works.
- [x] File association guidance is clear.

## Phase 3K Manual Smoke Closure

Review date: `2026-06-10`

Draft release URL: `https://github.com/DouglasNLima/local-docs-studio/releases/tag/untagged-79b1580fe9b2fc43270b`

Follow-up decision: Phase 3L keeps this draft as an internal RC and proceeds with the installer spike in `docs/architecture/windows-installer-spike.md`. The spike recommends `Phase 3M - Classic Installer MVP with Inno Setup` while preserving the ZIP release as the fallback RC artefact.

ZIP SHA256 verification:

```text
8CD380C42C33AAB8FE02C82184FF0A6FFDE6FC0C6B077BE40E0CF6A51C7E6188
8CD380C42C33AAB8FE02C82184FF0A6FFDE6FC0C6B077BE40E0CF6A51C7E6188  LensDocsStudio.Windows-0.1.0-dev.zip
```

Result: `PASS`. The downloaded ZIP hash still matches the uploaded `.sha256` file.

### First-Run Setup Wizard

Result: `PASS`.

Evidence:

- Clean packaged WebView2 state was used by removing only the generated `artifacts/release-review/v0.1.0-dev/extracted/LensDocsStudio.Windows.exe.WebView2` review folder.
- Normal packaged command launch opened `https://lens-docs-studio.local/index.html` and auto-showed the Windows setup wizard.
- Welcome step was readable.
- Runtime readiness showed `Windows host detected: Ready`, `Native bridge available: Ready`, `Offline packaged assets loaded: Packaged origin`, `App origin: https://lens-docs-studio.local/`, and `WebView2 runtime: Available`.
- Runtime readiness showed ready capability labels for `diagnostics.ping`, `file.open`, `file.save`, `file.saveAs`, `workspace.openFolder`, `workspace.saveFile`, `workspace.createFile`, `workspace.watch`, and `workspace.refreshFile`.
- Workspace step offered `Open a workspace folder` and `Continue without workspace`; continuing without workspace advanced safely.
- File association step stated the wizard is guidance-only and does not write registry keys.
- Starter step opened the Markdown and Mermaid sample and rendered preview content.
- Completing setup wrote `lensDocs.windowsSetup.completed=true`, `lensDocs.windowsSetup.version=phase-3e-mvp`, and persisted across reload without reopening automatically.
- `Help > Open setup wizard` reopened the completed wizard.
- Clearing the generated review local storage and using `Skip setup` wrote the same completion state and closed the wizard safely.
- Opening the packaged static app directly in Chromium browser mode did not auto-show the Windows setup wizard and had no `window.chrome.webview` bridge.

### Help Guide

Result: `PASS`.

Evidence:

- `Help > Open feature guide` worked from the packaged app.
- The app stayed on `https://lens-docs-studio.local/index.html`; no `localhost`, `127.0.0.1`, or GitHub Pages URL was required.
- Status reported `Feature guide opened read-only.`
- The editor was read-only and loaded `# Lens Docs Studio Feature Guide`.
- Preview rendered guide sections including `Open And Edit`, `Markdown Editor`, `Preview And Review`, `Mermaid Diagrams`, and `Exports`.
- Mermaid rendering reported `1 diagram`.

### File Association Guidance

Result: `PASS`.

Evidence:

- Setup wizard listed supported extensions: `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Setup wizard showed the per-user registration command path:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -ExecutablePath "<path-to-LensDocsStudio.Windows.exe>"
```

- Setup wizard explicitly stated that file association setup is guidance-only and does not write registry keys.
- `Register-WindowsFileAssociations.ps1 -DryRun` reported HKCU-only planned writes and ended with `Registration complete. Windows may still ask the user to confirm the default app in Settings or Open with.`
- `Unregister-WindowsFileAssociations.ps1 -DryRun` reported no registry removal and confirmed unrelated user defaults and Windows `UserChoice` keys were not removed.
- Register and unregister scripts remain available outside the app under `scripts/windows/`.

### Watcher / Conflict UX

Result: `PASS_WITH_NOTES`.

Evidence:

- The downloaded packaged executable passed `Run-WindowsNativeBridgeSmoke.ps1 -AppExecutablePath artifacts/release-review/v0.1.0-dev/extracted/LensDocsStudio.Windows.exe -TimeoutSeconds 90 -KeepSmokeRoot` after the earlier interactive package process was closed.
- Retained smoke result showed:
  - `WebView2 loaded packaged static assets`: passed, origin `https://lens-docs-studio.local`.
  - `WebView2 did not require a local HTTP server`: passed, href `https://lens-docs-studio.local/index.html`.
  - `Fixture workspace opened`: passed.
  - `Workspace file saved`: passed for `README.md`.
  - `Workspace file created, if capability exists`: passed for `notes/smoke-created.md`.
  - `Smoke workspace file externally changed`: passed for `docs/overview.md`.
  - `Workspace watcher event received`: passed for `docs/overview.md`.
  - `Workspace watcher event used a relative path`: passed.
- Focused Chromium browser regressions passed for the detailed UI states:
  - external change marker and explicit refresh;
  - dirty local edits preserved on external change;
  - dirty refresh confirmation, cancel, and confirmed reload;
  - deleted active native file preserving editor content;
  - created and renamed native workspace files reported safely;
  - dirty local edits preserved on native workspace rename;
  - malformed, unsafe, and unknown-workspace watcher events ignored.

Note: the packaged native smoke proves the real downloaded Windows shell, native bridge, workspace open/save/create, and watcher event path. The detailed dirty/delete/rename UI assertions were covered by the existing focused browser regression harness rather than by manually driving Windows picker and filesystem rename/delete interactions in the packaged shell.

## Phase 3K Validation

Commands:

```powershell
Get-FileHash artifacts/release-review/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip -Algorithm SHA256
Get-Content artifacts/release-review/v0.1.0-dev/LensDocsStudio.Windows-0.1.0-dev.zip.sha256
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -DryRun
pwsh -NoLogo -NoProfile -File scripts/windows/Unregister-WindowsFileAssociations.ps1 -DryRun
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -AppExecutablePath artifacts/release-review/v0.1.0-dev/extracted/LensDocsStudio.Windows.exe -TimeoutSeconds 90 -KeepSmokeRoot
npx playwright test tests/browser/app-smoke.spec.mjs --project=chromium -g "Windows setup|Help menu opens the feature guide|fake WebView2 watcher"
```

Results:

- SHA256 verification: `PASS`.
- Register dry run: `PASS`; no registry writes performed.
- Unregister dry run: `PASS`; no registry removals performed.
- Native bridge smoke against downloaded ZIP executable: `PASS` on rerun after closing the earlier interactive package instance.
- Focused Chromium setup/help/watcher regressions: `PASS`, 15 passed.

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
