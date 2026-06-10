# Inno Setup Installer MVP

Status: Phase 3M internal installer MVP.

This folder contains the Inno Setup authoring for the internal unsigned Lens Docs Studio installer MVP. Do not add generated setup executables, signed binaries, downloaded runtime installers, or release artefacts here.

## Files

- `LensDocsStudio.iss`: Inno Setup script that wraps the certified Windows folder package.
- `scripts/windows/Build-WindowsInnoInstaller.ps1`: repository build wrapper that validates the package, compiles the installer, writes SHA256, and creates a report.
- `docs/release/lens-docs-studio-inno-installer-mvp.md`: validation and manual smoke notes.

## MVP Behaviour

- Use `scripts/windows/Build-WindowsPackage.ps1` output as the installer source.
- Install per-user under `%LOCALAPPDATA%\Programs\Lens Docs Studio`.
- Avoid elevation with `PrivilegesRequired=lowest`.
- Create a Start Menu shortcut.
- Offer an optional Desktop shortcut.
- Offer unchecked optional per-user file associations for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Avoid Windows `UserChoice` writes.
- Document .NET 8 Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime prerequisites without bootstrapping them.
- Keep WebView2 bootstrapper, runtime bootstrapper, auto-update, public release upload, and signing out of the MVP.
- Produce ignored installer artefacts that can later be signed and attached to GitHub Releases with SHA256 checksums after release policy changes.

Build from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1
```

If Inno Setup is not installed, the script exits safely with `INNO_INSTALLER_MVP_BLOCKED_INNO_SETUP_NOT_INSTALLED` and writes an ignored report under `artifacts/installers/inno/`.

## Future Prototype Questions

- Which prerequisite checks can be robust without administrator rights?
- What silent install/uninstall switches should be documented for winget readiness?
- How should upgrade-over-install handle stale `StaticApp/` assets?
