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

## Future Roadmap

- Native file open/save bridge for Markdown, Mermaid, and supported import/export workflows.
- Native workspace/folder bridge for durable folder access, workspace recall, and folder-oriented workflows.
- Offline runtime hardening, including packaged asset coverage, WebView2 origin behaviour, service worker expectations, and clear fallback messages.
- Windows installer and packaging for offline distribution.
- First-run setup wizard for initial preferences, file association prompts, offline readiness, and migration notes.
- File associations for `.md`, `.markdown`, `.mmd`, and `.mermaid`.
- Release flow from `develop` to `main`, including browser static checks, Windows shell smoke checks, release notes, and GitHub Pages publication validation.

## Non-Goals For This Step

This roadmap update does not implement runtime behaviour. Native bridges beyond diagnostics, installer changes, file associations, and release automation remain future implementation work.
