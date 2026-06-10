# Lens Docs Studio Windows Shell

This project is the first Windows desktop shell for Lens Docs Studio. It hosts the existing static browser app in WinUI 3 with WebView2 and does not replace the Markdown, Mermaid, import, export, or Docs Site runtime.

The Windows app is the primary product distribution direction. The static browser/PWA app remains the core runtime and must continue to work from GitHub Pages and local static validation.

## Prerequisites

- Windows 10 version 2004 or later.
- .NET 8 SDK or newer.
- Windows App SDK runtime matching the project package version.
- Evergreen WebView2 Runtime, normally provided by Microsoft Edge on current Windows installations.

## Run Locally

From the repository root:

```powershell
dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj
```

The project copies `index.html`, `assets/`, `docs/`, `manifest.webmanifest`, `icon.svg`, `md-mmd-renderer-v5.html`, and `service-worker.js` into the output folder under `StaticApp/`. WebView2 loads the app through the local virtual host `https://lens-docs-studio.local/`.

Packaged static assets and WebView2 virtual host mapping are the preferred desktop hosting model. A local HTTP server is useful for development and validation, but it should not become a production requirement.

Supported runtime modes:

- Browser / GitHub Pages.
- Browser / local static server.
- Windows shell / development run.
- Windows shell / packaged local assets.
- Windows shell / offline mode.

The Windows shell / packaged local assets and Windows shell / offline mode paths use the copied `StaticApp/` folder. A production Windows launch must not depend on GitHub Pages, a CDN, an external URL, or a local server for the app shell, CSS, JavaScript modules, local vendor libraries, the help guide, templates, snippets, or existing export flows.

## Build

```powershell
dotnet build src/windows/LensDocsStudio.Windows.sln
```

If build tooling is missing, install the WinUI 3/Windows App SDK development components through Visual Studio Installer and retry the command.

## Folder/ZIP Package

Create the Phase 3B package from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1
```

By default this creates `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/` and `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip`. Launch the packaged app with `LensDocsStudio.Windows.exe` inside the output folder. The executable expects `StaticApp/` beside it and loads `index.html` through the WebView2 virtual host, without a local HTTP server.

The package is framework-dependent for this MVP. It requires the .NET desktop runtime, the Windows App SDK runtime that matches `Microsoft.WindowsAppSDK`, and the Evergreen WebView2 Runtime. Later installer phases may bootstrap prerequisites, but Phase 3B does not bundle WebView2 Fixed Version Runtime.

Useful options:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -Configuration Release -VersionSuffix dev -NoSmoke -KeepOutput
```

This phase is limited to a folder/ZIP distributable. It does not add MSIX, signing, certificates, an installer wizard, auto-update, store metadata, or a WebView2 fixed runtime/bootstrapper.

## Release Candidate Certification

Create and certify a Windows package release candidate from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
```

The certification script builds the folder/ZIP package, validates the packaged `StaticApp/`, runs the native bridge smoke harness against the packaged executable, calculates the ZIP SHA256 checksum, and writes Markdown plus JSON report artefacts under `artifacts/windows/release-candidates/`.

Use an explicit RC version when preparing a named candidate:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1 -Version 0.1.0-rc.1 -TimeoutSeconds 90
```

Use `docs/release/lens-docs-studio-windows-package-rc-checklist.md` for manual packaged-app smoke. The RC gate certifies only the folder/ZIP package, packaged static runtime validation, packaged native bridge smoke, package metadata, and documented manual smoke scope. It does not add MSIX, signing, certificates, Store publishing, auto-update, installer prerequisite bootstrapping, telemetry, cloud sync, or a merge to `main`.

Phase 3F records the installer decision gate in `docs/architecture/windows-installer-decision-gate.md`. The accepted short-term path is to keep the certified folder/ZIP artefact for release candidates and GitHub Releases, then run a separate MSIX/classic installer spike before adding production installer scripts, signing, prerequisite bootstrapping, auto-update, or winget metadata.

Prepare a dry-run GitHub Release artefact set from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun
```

The release preparation script builds and certifies the Windows ZIP by default, then writes `artifacts/releases/<tag>/` with the ZIP, `.sha256` checksum, RC report, generated release notes, and the draft prerelease `gh release create` command. It does not publish, upload files, create local tags, add signing, add an installer, or merge to `main` unless a later release operator explicitly chooses the documented publish path. See `docs/release/github-release-publication-flow.md`.

## Offline Static Asset Validation

Run the packaged asset check from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
```

Use `-NoBuild` to inspect the latest build output, or pass `-StaticAppRoot` to validate a specific package output. The script locates or uses the selected `StaticApp/` output, checks `index.html`, `assets/styles/app.css`, `assets/scripts/main.js`, every service-worker runtime asset, `assets/vendor/manifest.json`, every listed vendor file, `docs/tool-guide.md`, `manifest.webmanifest`, and `icon.svg`, then scans packaged runtime files for unexpected external script, style, CDN, or remote CSS dependencies.

## Scope

This shell is intentionally thin. It creates the desktop window, initialises WebView2, loads the packaged static app, handles supported startup file arguments, exposes a narrow native bridge for single-file open/save/save-as plus native workspace open folder/save/create-file/watch/refresh operations, and supports a compact in-app first-run setup wizard. Fuller installer work, auto-update, recent native folders, single-instance forwarding, delete/rename/move operations initiated from the app, and native export flows are left for later phases.

## Roadmap

- Offline runtime hardening.
- Folder/ZIP Windows package MVP.
- Windows folder/ZIP release candidate certification.
- Windows file associations MVP.
- Windows first-run setup wizard MVP.
- Windows installer decision gate.
- GitHub Release ZIP publication flow.
- Fuller Windows installer work.
- Release flow from `develop` to `main`, where `develop` is the active implementation branch and `main` remains the stable publication branch.

GitHub Pages should stay available as a secondary web demo, fallback, and validation target for the shared static runtime.

## Native Bridge

The shell registers a fail-closed WebView2 message handler. The static app can send `lensDocs.native.ping` through **Help > Check Windows bridge**, and the host replies with `lensDocs.native.pong`, protocol version `1`, `LensDocsStudio.Windows`, the app version when available, and these capabilities:

- `diagnostics.ping`
- `file.startupOpen`
- `file.open`
- `file.save`
- `file.saveAs`
- `workspace.openFolder`
- `workspace.saveFile`
- `workspace.createFile`
- `workspace.watch`
- `workspace.refreshFile`

The file bridge supports `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files. It reads and writes UTF-8 text only and rejects files above 5 MB. Native open and save-as use Windows file pickers. Native save writes only to an existing host-owned opaque `nativeHandleId`; the web app never sends arbitrary paths. The host keeps the handle-to-path map in memory for this phase.

Startup file arguments use the same bridge payload as native open. When the shell starts with a supported file path argument, the host validates that the path exists, is a file, uses a supported extension, is no larger than 5 MB, and can be read as UTF-8. After the web app sends `lensDocs.native.appReady`, the host sends `lensDocs.native.startupFile` with a host-owned `nativeHandleId`. The web app loads it as a Windows startup file and later saves through the existing `file.save` bridge path. Invalid startup files produce a safe status message and normal no-argument launch behaviour is unchanged.

The workspace bridge uses a Windows folder picker, discovers supported files recursively, and returns only safe relative paths plus opaque `nativeWorkspaceId` and `nativeHandleId` values. Workspace discovery uses these conservative limits: 5 MB per file, 500 loaded supported files, and 12 directory levels. Oversized files, invalid UTF-8 files, unreadable files, and files skipped by limits are returned as skipped metadata with relative paths and safe reasons. New Markdown files can be created inside the selected native workspace when the path is relative, uses a supported extension, does not escape the selected folder, and does not overwrite an existing file.

The native watcher starts only for the selected Windows workspace root. It watches supported editable files (`.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`), validates every event path back inside the workspace root, and sends only relative paths to the web app through `lensDocs.native.workspaceChanged`. It stops when a new workspace opens, the app closes, smoke completes, or watcher errors occur.

Watcher events are debounced for 500 ms and coalesced: deletes win over changes, a created file that also changes stays created, and host-recognised renames are sent as renames. Native save/create operations track recently written relative paths and suppress matching watcher noise for a best-effort two-second window. If suppression is uncertain, the app prefers showing an external-change marker rather than hiding a real change.

The web app owns all user-facing decisions. It marks records as externally changed, preserves dirty in-memory edits, keeps deleted active-file content in memory, adds safe created files when the host provides a handle, updates clean renamed records, and reloads only when the user explicitly uses **Refresh active file**. Native refresh uses `lensDocs.native.refreshWorkspaceFile` and reads only a validated file that belongs to the selected native workspace. Phase 2E adds compact changed, deleted, renamed, dirty, and dirty-external-conflict markers in the workspace list. Dirty refresh prompts must be confirmed before local edits are discarded; cancelled refresh keeps the editor content and marker. Deleted-file refresh reports that the file no longer exists and keeps the in-memory content available for **Save as** or copying.

The bridge intentionally does not expose recent native folders, native PDF export, Git operations, shell commands, usernames, environment variables, secrets, machine names, absolute workspace paths, delete/rename/move operations initiated from the app, or unrestricted filesystem access. Browser and GitHub Pages mode remain supported and report the bridge as unavailable without errors.

## First-Run Setup Wizard

The Windows shell can show a compact first-run setup wizard after the web app confirms the Windows native bridge. It appears only in the Windows desktop shell when `lensDocs.windowsSetup.completed` is absent from local browser storage. Browser, GitHub Pages, local-server, and PWA launches do not auto-open it.

The wizard is an in-app onboarding aid, not an installer wizard. It checks safe readiness details, offers the native workspace folder picker, shows file association guidance, and can open a starter document. It can be skipped, and it can be reopened later from **Help > Open setup wizard**.

Stored local keys:

- `lensDocs.windowsSetup.completed`
- `lensDocs.windowsSetup.completedAt`
- `lensDocs.windowsSetup.version`

The readiness step reports only safe host data: `LensDocsStudio.Windows`, the packaged origin, a WebView2 runtime availability flag when the host can report it, and capability labels such as `diagnostics.ping`, `file.open`, and `workspace.openFolder`. It does not expose usernames, machine names, environment variables, full executable paths, arbitrary local paths, or secrets.

File association setup is guidance-only in the wizard. It lists `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`, then shows the per-user `Register-WindowsFileAssociations.ps1` command for the packaged executable. The app does not write registry keys, does not require administrator rights, does not write `UserChoice`, and does not perform machine-wide setup.

The automated native bridge smoke is not required to complete the wizard. Smoke launches expose the smoke-only bridge capability, and the web app suppresses first-run setup for that capability so existing smoke automation can run without modal interaction. Normal launches still show first-run setup when storage is clean.

## Windows File Associations

Manual development/package registration lives in `scripts/windows/Register-WindowsFileAssociations.ps1` and `scripts/windows/Unregister-WindowsFileAssociations.ps1`. The scripts register only per-user `HKCU:\Software\Classes` keys, require no administrator rights, and support `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.

Register a packaged executable:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -ExecutablePath "artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe"
```

Unregister Lens Docs Studio keys and values:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Unregister-WindowsFileAssociations.ps1
```

Use `-DryRun` on either script to print planned registry operations without writing. Registration uses `LensDocsStudio.Markdown`, `LensDocsStudio.Mermaid`, and `LensDocsStudio.Text` ProgIds with the command `"<path-to-LensDocsStudio.Windows.exe>" "%1"`. The scripts do not write Windows `UserChoice`, do not require machine-wide registry access, and do not remove unrelated defaults. Windows may still ask the user to confirm Lens Docs Studio from **Open with** or Settings.

This MVP is not MSIX, not signed, not installer-integrated, and does not implement single-instance forwarding. If the app is already running, Windows may open another instance.

## Automated Native Bridge Smoke

Run the Windows smoke harness from the repository root:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
```

Optional flags:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -TimeoutSeconds 90
```

The script creates a temporary smoke root with a startup Markdown file, a single Markdown fixture, and a small workspace. It builds the shell unless `-NoBuild` is passed, launches the executable with `--smoke-native-bridge --smoke-root "<temp-folder>" "<temp-folder>\startup-file.md"`, waits for `smoke-result.json`, validates the changed startup and fixture files, and returns a non-zero exit code if any assertion fails.

The smoke harness does not automate native picker UI. Instead, the host exposes `smoke.nativeFixtures`, `smoke.workspaceChange`, and the `lensDocs.native.smoke.*` messages only when the smoke flag is present. Those fixture messages are fail-closed, root-bound to `--smoke-root`, and do not expose usernames, machine names, environment variables, unrestricted browsing, shell commands, or arbitrary host operations. Normal launches do not show smoke controls or smoke capabilities.

The smoke validates shell launch, WebView2 app load from `https://lens-docs-studio.local/`, absence of a localhost/loopback server requirement, `diagnostics.ping`, startup file argument loading and save, native file open/save/save-as through controlled fixtures, native workspace open/save/create through controlled fixtures, one external workspace change event with a relative path, absence of bridge protocol errors, structured completion, and clean shell shutdown. Network inspection is intentionally not part of the smoke harness in this phase; static URL scanning and packaged asset validation cover accidental runtime dependencies without making WebView2 automation brittle. If smoke fails, inspect the console summary and, when `-KeepSmokeRoot` is used, the retained `smoke-result.json` and fixture files.

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
12. Modify the same file externally again.
13. Make local edits in Lens Docs Studio before refreshing.
14. Confirm local edits remain and the dirty/external-conflict marker appears.
15. Cancel **Refresh active file** and confirm local edits remain.
16. Confirm refresh and verify external content loads.
17. Delete a workspace file externally and confirm local content is not silently erased.
18. Rename a workspace file externally and confirm the safe renamed indication.
19. Use **File > Open file**, **File > Save changes**, and **File > Save as** to confirm single-file native operations still work.
20. Open the static app in a normal browser and confirm no native bridge errors are reported.
