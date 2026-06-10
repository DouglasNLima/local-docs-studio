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

This shell is intentionally thin. It creates the desktop window, initialises WebView2, loads the packaged static app, and exposes a narrow native bridge for single-file open/save/save-as plus native workspace open folder/save/create-file/watch/refresh operations. File associations, packaging, auto-update, recent native folders, delete/rename/move operations initiated from the app, and native export flows are left for later phases.

## Roadmap

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
- `workspace.openFolder`
- `workspace.saveFile`
- `workspace.createFile`
- `workspace.watch`
- `workspace.refreshFile`

The file bridge supports `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files. It reads and writes UTF-8 text only and rejects files above 5 MB. Native open and save-as use Windows file pickers. Native save writes only to an existing host-owned opaque `nativeHandleId`; the web app never sends arbitrary paths. The host keeps the handle-to-path map in memory for this phase.

The workspace bridge uses a Windows folder picker, discovers supported files recursively, and returns only safe relative paths plus opaque `nativeWorkspaceId` and `nativeHandleId` values. Workspace discovery uses these conservative limits: 5 MB per file, 500 loaded supported files, and 12 directory levels. Oversized files, invalid UTF-8 files, unreadable files, and files skipped by limits are returned as skipped metadata with relative paths and safe reasons. New Markdown files can be created inside the selected native workspace when the path is relative, uses a supported extension, does not escape the selected folder, and does not overwrite an existing file.

The native watcher starts only for the selected Windows workspace root. It watches supported editable files (`.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`), validates every event path back inside the workspace root, and sends only relative paths to the web app through `lensDocs.native.workspaceChanged`. It stops when a new workspace opens, the app closes, smoke completes, or watcher errors occur.

Watcher events are debounced for 500 ms and coalesced: deletes win over changes, a created file that also changes stays created, and host-recognised renames are sent as renames. Native save/create operations track recently written relative paths and suppress matching watcher noise for a best-effort two-second window. If suppression is uncertain, the app prefers showing an external-change marker rather than hiding a real change.

The web app owns all user-facing decisions. It marks records as externally changed, preserves dirty in-memory edits, keeps deleted active-file content in memory, adds safe created files when the host provides a handle, updates clean renamed records, and reloads only when the user explicitly uses **Refresh active file**. Native refresh uses `lensDocs.native.refreshWorkspaceFile` and reads only a validated file that belongs to the selected native workspace.

The bridge intentionally does not expose recent native folders, file associations, native PDF export, Git operations, shell commands, usernames, environment variables, secrets, machine names, absolute workspace paths, delete/rename/move operations initiated from the app, or unrestricted filesystem access. Browser and GitHub Pages mode remain supported and report the bridge as unavailable without errors.

## Automated Native Bridge Smoke

Run the Windows smoke harness from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
```

Optional flags:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -TimeoutSeconds 90
```

The script creates a temporary smoke root with a single Markdown file and a small workspace, builds the shell unless `-NoBuild` is passed, launches the executable with `--smoke-native-bridge --smoke-root "<temp-folder>"`, waits for `smoke-result.json`, validates the changed fixture files, and returns a non-zero exit code if any assertion fails.

The smoke harness does not automate native picker UI. Instead, the host exposes `smoke.nativeFixtures`, `smoke.workspaceChange`, and the `lensDocs.native.smoke.*` messages only when the smoke flag is present. Those fixture messages are fail-closed, root-bound to `--smoke-root`, and do not expose usernames, machine names, environment variables, unrestricted browsing, shell commands, or arbitrary host operations. Normal launches do not show smoke controls or smoke capabilities.

The smoke validates shell launch, WebView2 app load, `diagnostics.ping`, native file open/save/save-as through controlled fixtures, native workspace open/save/create through controlled fixtures, one external workspace change event with a relative path, absence of bridge protocol errors, structured completion, and clean shell shutdown. If it fails, inspect the console summary and, when `-KeepSmokeRoot` is used, the retained `smoke-result.json` and fixture files.

Manual smoke:

1. Run `dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj`.
2. Use **Help > Check Windows bridge** and confirm `workspace.openFolder`, `workspace.saveFile`, `workspace.watch`, and `workspace.refreshFile` are reported.
3. Use **File > Open folder**.
4. Select a folder containing `.md`, `.markdown`, `.mmd`, `.mermaid`, or `.txt` files.
5. Confirm the workspace browser loads relative paths.
6. Select multiple files and confirm editor/preview update.
7. Edit a workspace file.
8. Use **File > Save changes**.
9. Modify that file externally in another editor while Lens Docs Studio has no local edits.
10. Confirm an external-change indicator/status appears.
11. Use **Refresh active file** and confirm content updates.
12. Modify the file again externally while local edits exist in Lens Docs Studio.
13. Confirm local edits are preserved and conflict/external-change indication appears.
14. Delete or rename a workspace file externally and confirm safe indication.
15. Use **File > Open file**, **File > Save changes**, and **File > Save as** to confirm single-file native operations still work.
16. Open the static app in a normal browser and confirm no native bridge errors are reported.
