# Windows File Associations MVP

Phase 3D adds controlled Windows file association support for development and package testing. It is intentionally manual and per-user.

## Supported Extensions

- `.md`
- `.markdown`
- `.mmd`
- `.mermaid`
- `.txt`

## Startup File Behaviour

The Windows shell accepts a supported file path as a command-line argument:

```powershell
artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe "C:\Docs\README.md"
```

The host validates that the path exists, is a file, has a supported extension, is no larger than 5 MB, and can be read as UTF-8. After WebView2 and the web bridge are ready, the host sends the file through `lensDocs.native.startupFile` using the same host-owned `nativeHandleId` model as native open. The web app can then save through the existing native file bridge.

Invalid startup files show a safe status message. Absolute paths remain host-owned and are not exposed to exports.

## Register

Register associations for a packaged executable:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -ExecutablePath "artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe"
```

Preview planned operations without writing:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -DryRun
```

The register script writes only under `HKCU:\Software\Classes`, uses ProgIds `LensDocsStudio.Markdown`, `LensDocsStudio.Mermaid`, and `LensDocsStudio.Text`, and uses this open command shape:

```text
"<path-to-LensDocsStudio.Windows.exe>" "%1"
```

## Unregister

Remove Lens Docs Studio association keys and values:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Unregister-WindowsFileAssociations.ps1
```

Preview removals without writing:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Unregister-WindowsFileAssociations.ps1 -DryRun
```

The unregister script removes only Lens Docs Studio ProgIds and Lens-owned extension values. It leaves unrelated defaults and Windows `UserChoice` keys in place.

## Boundaries

- Per-user HKCU registration only.
- No administrator rights expected.
- No machine-wide registry writes.
- No registry writes during normal app startup.
- No shell commands are executed from file path input.
- No network calls.
- No MSIX, installer wizard, signing, Store publishing, auto-update, or WebView2 bootstrapper.
- No single-instance forwarding yet; opening a file while Lens Docs Studio is already running may create a new app instance.

Windows may still ask the user to confirm the default app through **Open with** or Settings.
