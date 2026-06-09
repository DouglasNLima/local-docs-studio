# Lens Docs Studio Windows Shell

This project is the first Windows desktop shell for Lens Docs Studio. It hosts the existing static browser app in WinUI 3 with WebView2 and does not replace the Markdown, Mermaid, import, export, or Docs Site runtime.

The Windows app is the primary product distribution direction. The static browser/PWA app remains the core runtime and must continue to work from GitHub Pages and local static validation.

## Prerequisites

- Windows 10 version 2004 or later.
- .NET 8 SDK or newer.
- Windows App SDK runtime matching the project package version.
- WebView2 Runtime, normally provided by Microsoft Edge on current Windows installations.

## Run Locally

From the repository root:

```powershell
dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj
```

The project copies `index.html`, `assets/`, `docs/`, `manifest.webmanifest`, `icon.svg`, `md-mmd-renderer-v5.html`, and `service-worker.js` into the output folder under `StaticApp/`. WebView2 loads the app through the local virtual host `https://lens-docs-studio.local/`.

Packaged static assets and WebView2 virtual host mapping are the preferred desktop hosting model. A local HTTP server is useful for development and validation, but it should not become a production requirement.

## Build

```powershell
dotnet build src/windows/LensDocsStudio.Windows.sln
```

If build tooling is missing, install the WinUI 3/Windows App SDK development components through Visual Studio Installer and retry the command.

## Scope

This shell is intentionally thin. It creates the desktop window, initialises WebView2, loads the packaged static app, and exposes a narrow native bridge for single-file open/save/save-as. File associations, packaging, auto-update, folder watching, workspace bridging, and native export flows are left for later phases.

## Roadmap

- Native workspace/folder bridge.
- Offline runtime hardening.
- Windows installer and packaging.
- First-run setup wizard.
- File associations for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Release flow from `develop` to `main`, where `develop` is the active implementation branch and `main` remains the stable publication branch.

GitHub Pages should stay available as a secondary web demo, fallback, and validation target for the shared static runtime.

## Native Bridge

The shell registers a fail-closed WebView2 message handler. The static app can send `lensDocs.native.ping` through **Help > Check Windows bridge**, and the host replies with `lensDocs.native.pong`, protocol version `1`, `LensDocsStudio.Windows`, the app version when available, and these capabilities:

- `diagnostics.ping`
- `file.open`
- `file.save`
- `file.saveAs`

The file bridge supports `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files. It reads and writes UTF-8 text only and rejects files above 5 MB. Native open and save-as use Windows file pickers. Native save writes only to an existing host-owned opaque `nativeHandleId`; the web app never sends arbitrary paths. The host keeps the handle-to-path map in memory for this phase.

The bridge intentionally does not expose folder selection, workspace access, recursive directory listing, file watchers, file associations, native PDF export, Git operations, shell commands, usernames, environment variables, secrets, machine names, or unrestricted filesystem access. Browser and GitHub Pages mode remain supported and report the bridge as unavailable without errors.

Manual smoke:

1. Run `dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj`.
2. Use **File > Open file** to open a `.md` file.
3. Edit the file.
4. Use **File > Save changes**.
5. Reopen the file externally and confirm the content changed.
6. Use **File > Save as** to save a copy.
7. Use **Help > Check Windows bridge** and confirm `file.open`, `file.save`, and `file.saveAs` are reported.
8. Open the static app in a normal browser and confirm no native bridge errors are reported.
