# Windows Offline Distribution Roadmap

Lens Docs Studio is moving towards a Windows desktop distribution while preserving the existing static browser/PWA app as the core runtime. The Windows app hosts the same Markdown, Mermaid, import, export, Docs Site, and local documentation workflows through WinUI 3 and WebView2.

## Branch Strategy

- `develop` is the active implementation branch for Windows shell work, browser runtime changes, tests, and documentation updates.
- `main` remains the stable publication branch for GitHub Pages and release-ready documentation.
- Release candidates should flow from `develop` to `main` after static browser validation, Windows shell validation, and documentation review.
- GitHub Pages remains a secondary web demo, fallback, and validation target. It should continue proving that the core runtime works as static files without a backend.

## Product Direction

The primary distribution direction is a fully offline-capable Windows desktop app built with WinUI 3 and WebView2. The desktop host should package the static app assets locally and load them through WebView2 virtual host mapping, avoiding any requirement for a production local HTTP server.

The static browser/PWA app remains the core runtime. It must continue to run from GitHub Pages, from a local static server for development and validation, and from packaged desktop assets. Runtime features should stay browser-defensive so unsupported native capabilities degrade cleanly in web mode.

## Hosting Model

- Package `index.html`, `assets/`, `docs/`, `manifest.webmanifest`, `icon.svg`, `md-mmd-renderer-v5.html`, and `service-worker.js` with the Windows app.
- Prefer WebView2 virtual host mapping for packaged assets, using an app-local origin such as `https://lens-docs-studio.local/`.
- Keep local static servers as development and validation tools only.
- Do not introduce a backend, production server route, production build step, CDN dependency, or cloud service for normal desktop operation.
- Keep GitHub Pages compatibility as a regression target for the shared runtime.

## Runtime Modes

Lens Docs Studio supports these runtime modes:

1. Browser / GitHub Pages.
2. Browser / local static server.
3. Windows shell / development run.
4. Windows shell / packaged local assets.
5. Windows shell / offline mode.

Browser / GitHub Pages and browser / local static server modes validate the shared static runtime. Windows shell / development run builds the WinUI 3 host and copies the shared runtime into `StaticApp/`. Windows shell / packaged local assets and Windows shell / offline mode load that copied runtime through WebView2 virtual host mapping. In the Windows production path, a local HTTP server is not required and the app must not depend on GitHub Pages, CDNs, external scripts, external stylesheets, or development-only files.

## Implemented Phase 2B

- Native single-file open/save/save-as bridge for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files.
- Windows-native open and save pickers through the WinUI 3/WebView2 host.
- UTF-8 text-only reads and writes with a 5 MB file size limit.
- Opaque host-owned `nativeHandleId` values instead of exposing absolute paths to the web app.
- Browser, PWA, and GitHub Pages mode continue to use the existing browser picker, File System Access, and download fallbacks.
- **Help > Check Windows bridge** reports `diagnostics.ping`, `file.open`, `file.save`, and `file.saveAs` when hosted by the Windows shell.

## Implemented Phase 2C

- Native open folder through the WinUI 3/WebView2 host.
- Recursive workspace discovery for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files.
- Conservative native workspace limits: 5 MB per file, 500 loaded supported files, and 12 directory levels.
- Safe skipped-file metadata for oversized files, invalid UTF-8 files, unreadable files, and files skipped by workspace limits.
- Opaque host-owned `nativeWorkspaceId` and `nativeHandleId` values. Absolute selected-folder and file paths stay in memory in the Windows host.
- Native workspace file save through `workspace.saveFile`, using the existing active-file save flow and preserving dirty-state semantics.
- Native Markdown file creation inside the selected workspace through `workspace.createFile`, with relative-path, extension, boundary, and no-overwrite checks enforced by the host.
- Browser, PWA, and GitHub Pages mode continue to use existing browser folder pickers and fallbacks.
- **Help > Check Windows bridge** reports `workspace.openFolder`, `workspace.saveFile`, and `workspace.createFile` in addition to the Phase 2B capabilities when hosted by the Windows shell.

## Implemented Phase 2D

- Native workspace watching starts after `workspace.openFolder` and is disposed when a different workspace opens, the app closes, smoke completes, or watcher errors occur.
- The Windows host watches only the selected workspace root and only supported editable files: `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Host-to-web watcher events use `lensDocs.native.workspaceChanged` with protocol version `1`, the active `nativeWorkspaceId`, safe relative paths, and `changed`, `created`, `deleted`, or host-recognised `renamed` changes.
- Explicit native refresh uses `lensDocs.native.refreshWorkspaceFile` / `lensDocs.native.refreshWorkspaceFileResult` and reads only a validated file belonging to the selected native workspace.
- Watcher events are debounced for 500 ms and coalesced. Deleted beats changed, changed-created files remain created, and recognised renames avoid separate delete/create notifications.
- Native save/create operations suppress matching watcher noise for a best-effort two-second window. Real external changes should still be surfaced when uncertain.
- The web app marks external changes without automatic merge or automatic dirty reload. Dirty editor content is preserved until the user explicitly refreshes or saves.
- Browser, PWA, GitHub Pages, and non-native browser folder workflows are unaffected and do not expose `workspace.watch` or `workspace.refreshFile`.
- **Help > Check Windows bridge** reports `workspace.watch` and `workspace.refreshFile` in addition to the Phase 2B and Phase 2C capabilities when hosted by the Windows shell.

## Implemented Phase 2E

- The workspace list exposes compact state markers for clean, dirty, externally changed, externally deleted, externally renamed, and dirty external-conflict files.
- Selecting an affected native workspace file shows a concise status message that explains whether to refresh, preserve local edits, recover deleted content, or review a rename before saving.
- Clean externally changed files reload only through explicit **Refresh active file** and clear the marker after a successful `workspace.refreshFile` response.
- Dirty externally changed or renamed files keep local editor content until the user explicitly confirms refresh; cancelling refresh preserves both the content and the conflict marker.
- Deleted active files keep their in-memory editor content. Refresh reports that the file was deleted outside Lens Docs Studio and does not erase the editor.
- Rename handling remains non-destructive: clean recognised renames move the record to the new relative path, while dirty renames stay on the in-memory record with a conflict marker.
- Browser, PWA, GitHub Pages, and non-native browser folder workflows are unaffected; native IDs and watcher state remain in memory and are not written to exports.
- The automated smoke harness remains focused on stable bridge coverage. Conflict prompts and marker states are covered by fake WebView2 browser tests plus the manual smoke path below.

## Implemented Phase 3A

- Static checks verify runtime module syntax, relative imports, service-worker cache entries, vendor manifest entries, required pinned vendor dependencies, shell references, manifest icons, Windows static asset copy configuration, and offline runtime documentation.
- Runtime external dependency scanning covers the app shell, service worker, manifest, app CSS, vendor manifest, and first-party JavaScript modules. It blocks external script/style/CDN/font runtime references while allowing documentation examples, generated export XML namespaces, and user-authored Markdown links.
- `scripts/windows/Test-WindowsStaticAssets.ps1` builds or inspects the Windows output, locates `StaticApp/`, verifies required app shell files, every service-worker runtime asset, every vendor manifest entry, the help guide, web manifest, and icon, then scans packaged runtime files for unexpected external script, style, CDN, or remote CSS dependencies.
- The Windows native bridge smoke records that WebView2 loaded from `https://lens-docs-studio.local/` and did not use localhost or loopback HTTP for the smoke workflow.
- Network request interception is not part of Phase 3A smoke automation. The certification uses static URL scanning plus packaged asset validation to avoid brittle WebView2 network automation while still enforcing offline runtime completeness.
- Offline-capable for this phase means the Windows app can launch from packaged local assets, load CSS and JavaScript modules, load vendored Markdown, Mermaid, Highlight.js, DOMPurify, KaTeX, ZIP, Word, and PDF import dependencies, open the local help guide, use templates and snippets, use native single-file and workspace workflows, detect external workspace changes, and run existing export paths without GitHub Pages, CDN access, or a local HTTP server.

## Implemented Phase 3B

- `scripts/windows/Build-WindowsPackage.ps1` creates a repeatable folder/ZIP distributable under `artifacts/windows/`, using a framework-dependent `win-x64` publish of the unpackaged WinUI 3 shell.
- The package output keeps `LensDocsStudio.Windows.exe` and the copied `StaticApp/` folder together so WebView2 can load the app from `https://lens-docs-studio.local/` without GitHub Pages, a CDN, an external URL, or a local HTTP server.
- `scripts/windows/Test-WindowsStaticAssets.ps1` accepts `-StaticAppRoot` so the validator can inspect the actual package output as well as the latest development build output.
- `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` accepts `-AppExecutablePath` so the automated native bridge smoke can run against the packaged executable when practical.
- The packaging MVP requires the .NET desktop runtime, the matching Windows App SDK runtime, and the Evergreen WebView2 Runtime. It does not bundle WebView2 Fixed Version Runtime.
- This phase deliberately excludes MSIX, signing, certificates, file associations, installer wizard UI, auto-update, store publishing metadata, and prerequisite bootstrapping.

## Implemented Phase 3C

- `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` creates a repeatable release-candidate gate for the Windows folder/ZIP package.
- The RC gate builds the package with the Phase 3B packaging script, validates the packaged `StaticApp/`, runs the native bridge smoke harness against the packaged executable, calculates the ZIP SHA256 checksum, and writes Markdown plus JSON report artefacts under `artifacts/windows/release-candidates/`.
- The report records package version, branch, commit SHA, build timestamp, configuration, runtime identifier, output folder, ZIP path, ZIP size, checksum, runtime prerequisites, validation commands, command output, durations, and pass/fail results.
- `docs/release/lens-docs-studio-windows-package-rc-checklist.md` captures manual packaged-app smoke steps and explicit certification claims/non-claims.
- The RC gate certifies only the folder/ZIP package, packaged static runtime validation, packaged native bridge smoke, metadata capture, and documented manual smoke scope. It does not add MSIX, signing, certificates, Store publishing, auto-update, file associations, installer prerequisite bootstrapping, telemetry, cloud sync, or a merge to `main`.

## Implemented Phase 3D

- The Windows shell accepts supported startup file path arguments for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Startup files are validated by the native file service before reading: the path must exist, be a file, use a supported extension, stay within the 5 MB native file limit, and decode as UTF-8.
- The host waits for WebView2 initialisation and a web-app `lensDocs.native.appReady` message before sending `lensDocs.native.startupFile`.
- Startup file payloads reuse the native single-file shape with content, display name, extension, and a host-owned opaque `nativeHandleId`; absolute paths stay in the Windows host.
- Invalid startup file arguments produce safe status messages through `lensDocs.native.startupFileError`; no-argument launch behaviour is unchanged.
- `scripts/windows/Register-WindowsFileAssociations.ps1` registers per-user HKCU file associations for development/package testing, using `LensDocsStudio.Markdown`, `LensDocsStudio.Mermaid`, and `LensDocsStudio.Text` ProgIds and an open command of `"<path-to-LensDocsStudio.Windows.exe>" "%1"`.
- `scripts/windows/Unregister-WindowsFileAssociations.ps1` removes only Lens Docs Studio ProgIds and Lens-owned extension values. It leaves unrelated defaults and Windows `UserChoice` keys alone.
- Both file association scripts support `-DryRun`, and `scripts/windows/Test-WindowsStaticAssets.ps1` parses the scripts and validates dry-run output.
- The Windows native bridge smoke launches the shell with a temporary startup `.md` file argument, confirms the file loads, saves it through the existing active-file save flow, and validates the file content after smoke completion.
- This MVP is not MSIX, not signed, not installer-integrated, not machine-wide, and does not implement single-instance forwarding.

## Implemented Phase 3E

- The Windows shell shows a compact first-run setup wizard only when the app is hosted by `LensDocsStudio.Windows`, the native bridge reports workspace capability, and local browser storage has not recorded completion.
- Browser, GitHub Pages, local-server, and PWA mode do not auto-open the wizard. **Help > Open setup wizard** can reopen it manually; browser mode shows a safe Windows-only readiness message.
- Setup completion is stored locally with `lensDocs.windowsSetup.completed`, `lensDocs.windowsSetup.completedAt`, and `lensDocs.windowsSetup.version`. Resetting browser storage may show the wizard again.
- The readiness step reports only safe data: Windows host detected, native bridge availability, packaged origin, WebView2 runtime availability when reported, and capability labels. It does not expose usernames, machine names, environment variables, executable paths, secrets, or arbitrary local paths.
- The workspace step calls the existing native `workspace.openFolder` flow only after the user clicks **Open a workspace folder**.
- The file association step is guidance-only. It shows supported extensions and the per-user registration command, but the app does not write registry keys, does not require administrator rights, does not write `UserChoice`, and does not perform machine-wide setup.
- The starter step can open the Markdown + Mermaid sample, open the local feature guide, or start an unsaved blank Markdown document.
- The automated native bridge smoke suppresses the wizard through the smoke-only capability so smoke automation does not need to dismiss first-run setup. Normal launches remain unchanged.
- This MVP is not MSIX, not an installer wizard, not a WebView2 bootstrapper, not auto-update, not telemetry, not cloud sync, and not a complex settings page.

## Implemented Phase 3F

- `docs/architecture/windows-installer-decision-gate.md` records the Windows installer decision gate.
- The accepted short-term path keeps the current folder/ZIP package as the release candidate artefact and uses GitHub Releases for ZIP publication when ready.
- MSIX and classic installer implementation are deferred to a separate spike that must validate signing, prerequisite handling, shortcuts, file associations, uninstall, upgrade, and clean-machine behaviour.
- The current runtime prerequisite policy remains framework-dependent: .NET desktop runtime, matching Windows App SDK runtime, and Evergreen WebView2 Runtime.
- WebView2 bootstrapper, WebView2 Fixed Version Runtime, self-contained publish, installer-owned file associations, auto-update, winget manifests, and production installer scripts are not implemented in this phase.

## Implemented Phase 3G

- `scripts/windows/Prepare-WindowsGitHubRelease.ps1` prepares a GitHub Release artefact set for the certified Windows folder/ZIP package.
- The script is dry-run-first. It builds and certifies the package by default, copies the certified ZIP into `artifacts/releases/<tag>/`, writes a SHA256 checksum file, generates release notes, and prints the exact draft prerelease `gh release create` command without publishing.
- `docs/release/templates/github-release-notes.md` provides the release notes template, and `docs/release/github-release-publication-flow.md` documents dry-run, publish, checksum, GitHub CLI, and tag strategy.
- The flow refuses dirty worktrees, non-`develop` branches, stale target commits, failed RC certification, and missing checksums unless an explicit local rehearsal override is supplied where documented.
- This phase does not add auto-update, MSIX, a classic installer, signing, Store publishing, WebView2 bootstrapper, WebView2 Fixed Version Runtime, machine-wide file associations, release CI/CD, a merge to `main`, or automatic GitHub Release publication.

## Implemented Phase 3H

- `scripts/windows/Test-WindowsGitHubReleaseDryRun.ps1` adds a dry-run review gate for the prepared GitHub Release artefact set.
- The gate reruns release preparation in dry-run mode by default, validates the artefact folder, ZIP, SHA256 file, RC report, release notes, tag/target strategy, and generated draft prerelease `gh release create` command.
- The gate writes an ignored review report under `artifacts/releases/<tag>/` with the product, version, tag, target commit, package ZIP, SHA256 value, RC report path, release notes path, generated command, verdict, manual publication checklist, and known limitations.
- `CERTIFIED_DRAFT_RELEASE_READY` means the local dry-run artefacts are ready for an intentional manual draft prerelease publication step. It does not publish, upload, create tags, sign the ZIP, add MSIX, add a classic installer, add auto-update, or merge to `main`.

## Implemented Phase 3L

- `docs/architecture/windows-installer-spike.md` records the MSIX versus classic installer spike after the internal RC manual smoke passed with notes.
- The spike compares MSIX, WiX Toolset, Inno Setup, continuing ZIP plus GitHub Releases only, and a later winget path.
- The recommended next phase is `Phase 3M - Classic Installer MVP with Inno Setup`.
- The current ZIP and GitHub Releases path remains the internal RC fallback while the installer remains unsigned and prototype-only.
- Runtime prerequisites stay documented and framework-dependent for now: .NET 8 Desktop Runtime, Windows App SDK Runtime matching the project package reference, and Evergreen WebView2 Runtime.
- WebView2 bootstrapper, Fixed Version Runtime, runtime bootstrappers, code signing, auto-update, public publication, MSIX, WiX, and production Inno installer output remain out of scope for Phase 3L.

## Implemented Phase 3M

- `installer/inno/LensDocsStudio.iss` adds the internal unsigned Inno Setup installer MVP authoring.
- `scripts/windows/Build-WindowsInnoInstaller.ps1` builds or reuses the Phase 3B/3C Windows folder package, validates packaged `StaticApp/`, compiles the installer with Inno Setup, writes a SHA256 file, and creates a Markdown report under `artifacts/installers/inno/`.
- The installer installs per-user under `%LOCALAPPDATA%\Programs\Lens Docs Studio`, avoids administrator elevation, creates a Start Menu shortcut, offers an optional Desktop shortcut, and uninstalls installer-owned files.
- File associations are an unchecked optional task, per-user only under `HKCU:\Software\Classes`, and avoid Windows `UserChoice`. The manual register/unregister scripts remain available for ZIP and development workflows.
- Runtime prerequisites remain documented rather than bootstrapped: .NET 8 Desktop Runtime, Windows App SDK Runtime matching the project package reference, and Evergreen WebView2 Runtime.
- Phase 3M does not publish a release, upload installer artefacts, create or move tags, merge to `main`, add signing, add auto-update, add MSIX, add WiX, bundle WebView2 Fixed Version Runtime, or change runtime app behaviour.

## Future Roadmap

- MSIX versus classic installer spike for offline distribution.
- Broader installer validation, using the Phase 3M Inno Setup MVP as input and keeping ZIP as fallback.
- Later signing, prerequisite bootstrapping, shortcuts, uninstall hardening, file associations, MSIX reassessment, and winget publication after the installer artefact is stable.
- Single-instance forwarding for file-open activation.
- Release flow from `develop` to `main`, including browser static checks, Windows shell smoke checks, release notes, and GitHub Pages publication validation.

## Implemented Windows Smoke Harness

- `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` creates controlled temporary fixtures, builds or reuses the Windows shell, launches it with `--smoke-native-bridge --smoke-root "<temp-folder>"`, waits for `smoke-result.json`, checks fixture file content, and exits non-zero on failure.
- The smoke-only `smoke.nativeFixtures` capability is reported only when `--smoke-native-bridge` is present. Normal Windows shell launches, browser mode, and GitHub Pages mode do not expose smoke fixture APIs.
- Smoke fixture operations are limited to the explicit smoke root. They do not automate Windows picker UI, expose local environment details, run shell commands, reveal unrestricted paths, or change production bridge validation.
- The smoke covers shell start, WebView2 app load, bridge diagnostics, startup file argument load/save, native single-file open/save/save-as, native workspace open/save/create, one external workspace watcher event with a relative path, protocol-error reporting, structured completion, and clean shutdown.

Run it with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
```

Use `-NoBuild -TimeoutSeconds 90` to reuse an existing build on slower validation hosts.

## Non-Goals For Phase 2B

Phase 2B does not implement open folder, workspace folder bridging, recursive directory access, file watchers, recent files through the native bridge, native drag/drop integration, native PDF export, Git integration, installer/MSIX work, auto-update, or arbitrary native command execution.

## Non-Goals For Phase 2C

Phase 2C does not implement file watchers, external-change live notifications, recent native folders, workspace restore after restart, native drag/drop integration, native PDF export, Git integration, installer/MSIX work, auto-update, a full first-run wizard, delete/rename/move operations, arbitrary host command execution, or an editor rewrite.

## Manual Smoke

1. Run `dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj`.
2. Use **Help > Check Windows bridge** and confirm `workspace.openFolder`, `workspace.saveFile`, `workspace.watch`, and `workspace.refreshFile` capabilities.
3. Use **File > Open folder**.
4. Select a folder containing `.md`, `.markdown`, `.mmd`, `.mermaid`, or `.txt` files.
5. Confirm the workspace browser loads relative paths.
6. Select multiple files and confirm editor/preview update.
7. Edit a workspace file.
8. Use **File > Save changes**.
9. Modify that file externally in another editor while Lens Docs Studio has no local edits.
10. Confirm external-change indicator/status appears.
11. Use **Refresh active file** and confirm content updates.
12. Modify the same file externally again.
13. Make local edits in Lens Docs Studio before refreshing.
14. Confirm local edits remain and the dirty/external-conflict marker appears.
15. Cancel **Refresh active file** and confirm local edits remain.
16. Confirm refresh and verify external content loads.
17. Delete a workspace file externally and confirm local content is not silently erased.
18. Rename a workspace file externally and confirm the safe renamed indication.
19. Use **File > Open file**, **File > Save changes**, and **File > Save as** to confirm single-file native operations still work.
20. Open the app in normal browser mode and confirm no native bridge errors.
