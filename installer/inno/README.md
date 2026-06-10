# Inno Setup Installer Spike Notes

Status: Phase 3L spike only.

This folder intentionally contains notes, not a production Inno Setup installer. Do not add generated setup executables, signed binaries, downloaded runtime installers, or release artefacts here during Phase 3L.

## Candidate Use

Inno Setup is the recommended next installer MVP path for Lens Docs Studio. It can wrap the existing Windows folder package, create shortcuts, offer optional tasks, support silent install/uninstall, and stay small enough for controlled GitHub Releases validation.

## Proposed Phase 3M MVP

- Use `scripts/windows/Build-WindowsPackage.ps1` output as the installer source.
- Install per-user by default and avoid elevation.
- Create a Start Menu shortcut.
- Offer an optional Desktop shortcut.
- Offer optional per-user file associations for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Avoid Windows `UserChoice` writes.
- Detect or warn for missing .NET 8 Desktop Runtime, Windows App SDK Runtime, and Evergreen WebView2 Runtime.
- Keep WebView2 bootstrapper, runtime bootstrapper, auto-update, and signing out of the first MVP.
- Produce artefacts that can later be signed and attached to GitHub Releases with SHA256 checksums.

## Future Prototype Questions

- Which per-user install directory should be used?
- What is the least surprising default for file association tasks?
- Which prerequisite checks can be robust without administrator rights?
- What silent install/uninstall switches should be documented for winget readiness?
- How should upgrade-over-install handle stale `StaticApp/` assets?

