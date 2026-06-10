# Lens Docs Studio Inno Installer MVP

Status: Phase 3M.1 certified internal installer MVP.

This phase adds a repeatable classic Windows installer build path with Inno Setup while keeping the certified Windows folder/ZIP package as the internal release-candidate fallback.

## Scope

The MVP installer:

- Builds from the existing Windows folder package under `artifacts/windows/LensDocsStudio.Windows-<version>/`.
- Produces one unsigned setup executable under `artifacts/installers/inno/`.
- Installs per-user to `%LOCALAPPDATA%\Programs\Lens Docs Studio`.
- Creates a Start Menu shortcut by default.
- Offers an optional Desktop shortcut.
- Offers optional per-user file associations for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Uninstalls installer-owned files, shortcuts, and Lens-owned registry entries.
- Produces a SHA256 checksum and a Markdown build report.

The MVP does not publish a release, upload assets, create or move tags, merge to `main`, sign binaries, add auto-update, add MSIX, add WiX, install runtimes, download prerequisites, or change runtime app behaviour.

## Runtime Prerequisites

The installer deliberately documents prerequisites rather than bootstrapping them:

- .NET 8 Desktop Runtime.
- Windows App SDK Runtime matching the project package reference.
- Evergreen Microsoft Edge WebView2 Runtime.

This keeps the first installer artefact small and offline. Missing runtime handling remains a manual tester prerequisite for Phase 3M. A later phase can add detection or bootstrapping only after clean-machine validation proves the extra installer complexity is justified.

## File Associations

File associations are included as an unchecked optional Inno task.

When selected, the installer writes only per-user `HKCU:\Software\Classes` keys for:

- `.md` and `.markdown` through `LensDocsStudio.Markdown`.
- `.mmd` and `.mermaid` through `LensDocsStudio.Mermaid`.
- `.txt` through `LensDocsStudio.Text`.

The installer does not write Windows `UserChoice`, does not force Lens Docs Studio as the default app, and does not require administrator rights. Windows may still ask the user to confirm Lens Docs Studio through **Open with** or Settings. The manual register/unregister scripts remain available for ZIP and development workflows.

## Build

Install Inno Setup 6, then run from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1
```

Useful options:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -Version 0.1.0-dev -Configuration Release
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -InnoCompilerPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1 -DryRun
```

The script:

1. Finds `ISCC.exe` from `-InnoCompilerPath`, `PATH`, or common Inno Setup install folders.
2. Builds the Windows package through `Build-WindowsPackage.ps1` unless `-NoPackageBuild` is supplied.
3. Validates the packaged `StaticApp/` with `Test-WindowsStaticAssets.ps1`.
4. Compiles `installer/inno/LensDocsStudio.iss`.
5. Writes the installer, SHA256 file, and report to `artifacts/installers/inno/`.

If Inno Setup is missing, the script exits safely with:

```text
INNO_INSTALLER_MVP_BLOCKED_INNO_SETUP_NOT_INSTALLED
```

It still writes an ignored report under `artifacts/installers/inno/` explaining the block.

## Generated Artefacts

Generated files are ignored and must not be committed:

```text
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256
artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md
```

## Phase 3M.1 Certification Result

Certification ran on commit `9439f95813d6e8654032b7fef3b414af1fa267e6` from `develop`.

- Inno Setup 6.7.3 was installed per-user with `winget`.
- `ISCC.exe` path: `C:\Users\dougl\AppData\Local\Programs\Inno Setup 6\ISCC.exe`.
- Installer build: `PASS`.
- Installer artefact: `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe`.
- Installer SHA256: `152051F7CDAB5A33F8D8E5C219F937A54777687B8980D750B486BE572883FA8E`.
- Installer report: `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md`.
- Baseline install used the default per-user path and did not require elevation.
- Start Menu shortcut was created and pointed at the installed executable.
- Optional Desktop shortcut was selected for the smoke and pointed at the installed executable.
- Installed-app native bridge smoke passed against `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe`.
- File associations were left unchecked; no Lens ProgId keys or `UserChoice` writes were observed.
- Uninstall completed and removed shortcuts plus the installed-app entry. A WebView2 runtime data folder remained under the install path after the native smoke: `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2\EBWebView`.
- The GitHub draft release remained unpublished.

Phase 3N records the release-asset decision in `docs/release/lens-docs-studio-installer-rc-asset-plan.md`. It recommends a later explicit upload of the certified installer `.exe`, `.sha256`, and report to the existing draft prerelease only after release notes are amended, with the ZIP remaining the primary fallback.

## Manual Installer Smoke

When an installer is successfully built, validate on a Windows test machine:

1. Run the setup executable without administrator elevation.
2. Keep the default install folder.
3. Select the Desktop shortcut task if that path is being tested.
4. Leave file associations unchecked for the baseline install test.
5. Launch Lens Docs Studio from the Start Menu shortcut.
6. Launch Lens Docs Studio from the Desktop shortcut when selected.
7. Open the app and run **Help > Check Windows bridge**.
8. Confirm a local Markdown file can open, edit, and save.
9. Uninstall Lens Docs Studio from Windows Settings or the uninstaller shortcut.
10. Confirm the install folder is removed, except for any user-created data outside the installer-owned folder.

Run a separate opt-in association test only after the baseline install/uninstall passes. Confirm registry writes stay under `HKCU:\Software\Classes`, supported files can open with Lens Docs Studio when Windows allows it, and uninstall removes Lens-owned ProgIds without touching unrelated defaults or `UserChoice`.

## Validation

For Phase 3M source changes, run:

```powershell
npm run test:static
dotnet build src/windows/LensDocsStudio.Windows.sln
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1
```

If Inno Setup is not installed, the last command should produce `INNO_INSTALLER_MVP_BLOCKED_INNO_SETUP_NOT_INSTALLED`; that is a safe Phase 3M validation block, not a silent failure.
