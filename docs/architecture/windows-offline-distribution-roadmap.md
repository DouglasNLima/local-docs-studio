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

## Future Roadmap

- Native workspace external-change detection and file watching.
- Offline runtime hardening, including packaged asset coverage, WebView2 origin behaviour, service worker expectations, and clear fallback messages.
- Windows installer and packaging for offline distribution.
- First-run setup wizard for initial preferences, file association prompts, offline readiness, and migration notes.
- File associations for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.
- Release flow from `develop` to `main`, including browser static checks, Windows shell smoke checks, release notes, and GitHub Pages publication validation.

## Implemented Windows Smoke Harness

- `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1` creates controlled temporary fixtures, builds or reuses the Windows shell, launches it with `--smoke-native-bridge --smoke-root "<temp-folder>"`, waits for `smoke-result.json`, checks fixture file content, and exits non-zero on failure.
- The smoke-only `smoke.nativeFixtures` capability is reported only when `--smoke-native-bridge` is present. Normal Windows shell launches, browser mode, and GitHub Pages mode do not expose smoke fixture APIs.
- Smoke fixture operations are limited to the explicit smoke root. They do not automate Windows picker UI, expose local environment details, run shell commands, reveal unrestricted paths, or change production bridge validation.
- The smoke covers shell start, WebView2 app load, bridge diagnostics, native single-file open/save/save-as, native workspace open/save/create, protocol-error reporting, structured completion, and clean shutdown.

Run it with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
```

Use `-NoBuild -TimeoutSeconds 90` to reuse an existing build on slower validation hosts.

## Non-Goals For Phase 2B

Phase 2B does not implement open folder, workspace folder bridging, recursive directory access, file watchers, recent files through the native bridge, file associations, native drag/drop integration, native PDF export, Git integration, installer/MSIX work, auto-update, or arbitrary native command execution.

## Non-Goals For Phase 2C

Phase 2C does not implement file watchers, external-change live notifications, recent native folders, workspace restore after restart, file associations, native drag/drop integration, native PDF export, Git integration, installer/MSIX work, auto-update, a full first-run wizard, delete/rename/move operations, arbitrary host command execution, or an editor rewrite.

## Manual Smoke

1. Run `dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj`.
2. Use **Help > Check Windows bridge** and confirm `workspace.openFolder` and `workspace.saveFile` capabilities.
3. Use **File > Open folder**.
4. Select a folder containing `.md`, `.markdown`, `.mmd`, `.mermaid`, or `.txt` files.
5. Confirm the workspace browser loads relative paths.
6. Select multiple files and confirm editor/preview update.
7. Edit a workspace file.
8. Use **File > Save changes**.
9. Reopen the file externally and confirm the content changed.
10. Use **File > Open file**, **File > Save changes**, and **File > Save as** to confirm single-file native operations still work.
11. Open the app in normal browser mode and confirm no native bridge errors.
