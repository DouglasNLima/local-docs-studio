# Local Docs Studio

A local-first Markdown, Mermaid, and documentation studio that runs entirely in the browser and can be published from GitHub Pages. Open local `.md`, `.markdown`, `.mmd`, and `.mermaid` files, preview diagrams, review documents, save local edits when the browser supports it, and export clean docs packages without a backend.

## Use The App

1. Open `index.html` through GitHub Pages or a local static server.
2. Choose **Open file** for one document or **Open folder** for a folder of Markdown and Mermaid files.
3. Edit in the Markdown toolbar or type directly in the editor.
4. Use **Render** to refresh the preview, then export or copy the rendered output.

Files stay in the browser unless you save, copy, or export them.
Use **Help > Open feature guide** to open the local Markdown feature guide inside the app in read-only mode.

## Key Features

- Markdown preview powered by Marked and DOMPurify.
- Mermaid diagrams inside fenced `mermaid` or `mmd` code blocks, plus Azure DevOps `::: mermaid` wiki blocks.
- Per-diagram Mermaid actions for copying source and exporting SVG or PNG.
- Syntax-highlighted code blocks with one-click copy buttons.
- Rendered tables with one-click copy as Excel-friendly TSV.
- Excel/spreadsheet paste support that converts HTML table or TSV clipboard data into Markdown tables, plus Edit > Paste Special actions for table, text, code block, quote, HTML-to-Markdown, list, checklist, numbered list, and Mermaid block paste.
- Standalone `.mmd` and `.mermaid` diagram rendering.
- Local folder browser with filtering and dirty-file markers.
- Markdown editor with line numbers, formatting buttons, `Ctrl/Cmd+Z`, `Ctrl/Cmd+Y`, and common formatting shortcuts.
- Lightweight Mermaid autocomplete and pre-render validation for `.mmd`, `.mermaid`, and fenced Mermaid blocks.
- Editor/Split/Preview layout modes, light/dark theme toggle, preview maximisation, diagram zoom, and optional outline.
- Preview follow for editor selections, enabled by default and toggleable from the preview header.
- Drag-and-drop PNG, JPEG, GIF, and WebP image insertion as session assets that are included in HTML, Word, and Docs Site exports.
- HTML export, Word `.docx` export, copy HTML, and copy rendered text.
- PDF export through the browser print dialogue with a clean print stylesheet.
- Markdown Bundle ZIP export for editable docs plus image assets, with an optional Azure DevOps Mermaid syntax checkbox.
- ZIP import for app bundles and generic Markdown/Mermaid documentation ZIPs.
- Folder-to-docs-site export as a GitHub Pages-ready ZIP.
- Document UX in the preview with hierarchical outline, rendered-text search, and non-blocking document review notes.
- Mermaid Diagram Studio with templates, snippets, source copy, SVG export, and PNG export.
- Local Content Studios for README/project docs, release notes, and requirements/user stories.
- Built-in read-only feature guide available from the Help menu.
- Optional PWA/offline shell for repeat use.

## Document UX

The preview pane includes document-focused review tools that do not change exported files:

- **Outline** builds a navigable table of contents from rendered headings and highlights the active section while you scroll.
- **Find** searches rendered document text, highlights matches, and skips interactive UI text such as code/table-copy and diagram-export buttons.
- **Review** shows word count, reading time, content counts, and soft notes such as missing H1 titles, heading-level jumps, Mermaid errors, external links, and dirty files.

These tools are app-only. HTML, Word, and Docs Site exports are regenerated from a clean render and do not include search marks or review UI.

## Editor UX

The editor stays buildless and native, using a `textarea` with a synchronised line-number gutter instead of a heavy IDE component.

- **Layout** in the View menu switches between Editor, Split, and Preview modes. The choice is saved locally.
- **Follow selection** in the preview header highlights matching preview text for short editor selections and scrolls that match into view in Split mode. It is best-effort, can be turned off per browser, and the editor and preview still remember their own scroll positions across renders and file switches.
- **Mermaid autocomplete** appears with `Ctrl/Cmd+Space` in Mermaid files or Mermaid fenced blocks, and can also open from Mermaid-like line prefixes. Use arrow keys, `Enter` or `Tab` to insert a snippet, and `Escape` to close it.
- **Mermaid validation** runs before each diagram render. Fenced Mermaid blocks and Azure DevOps `::: mermaid` blocks are both accepted. Invalid diagrams show a localised error with copy/jump actions while the rest of the Markdown continues rendering.
- **Paste Special** in the Edit menu can paste clipboard content as a Markdown table, plain text, fenced code block, quote, Markdown-converted HTML, list, checklist, numbered list, or Mermaid block. Regular paste automatically converts spreadsheet tables when the clipboard provides HTML table or tab-separated text; plain text stays plain.
- **Image drag-and-drop** on the editor inserts Markdown such as `![diagram](assets/images/diagram.png)`. The Markdown file save-back writes the link text only; the image binaries live in the browser session and are embedded or bundled when you export. User-supplied SVG images are blocked as assets for security; rendered Mermaid diagrams can still be exported as SVG.

## Local Content Studios

Use **Create** to generate editable Markdown from local templates. The studios are deterministic and browser-only: no AI calls, API keys, backend, or uploaded source.

- **Project Docs** creates README files, architecture overviews, ADRs, runbooks, API notes, onboarding guides, and docs-pack indexes.
- **Release Notes** creates customer notes, technical changelogs, sprint summaries, and migration notes with semantic-version-friendly headings.
- **Requirements** creates PRDs, feature briefs, user story sets, acceptance criteria, Gherkin scenarios, and journey maps.

Each studio also includes snippets you can insert at the cursor. Generated files behave like normal Markdown files, so you can edit, save, export to HTML or Word, and include them in a Docs Site ZIP.

## Mermaid Diagram Studio

Use **Studio** for a focused Mermaid authoring workflow. **Studio tools** provides templates for common diagram types and snippets for common Mermaid patterns.

Each rendered Mermaid diagram also includes compact actions:

- **Copy** for that diagram's source.
- **SVG** for that specific rendered diagram.
- **PNG** for a raster image of that specific diagram.

The Export menu includes document-level diagram actions:

- **Export SVG** for the current rendered Mermaid diagram.
- **Export PNG** for a raster image version.
- **Copy Mermaid** for the current standalone diagram source or first Mermaid block.

## Docs Site Builder

Open a folder of Markdown and Mermaid files, toggle **Docs site** to preview the folder as a navigable documentation site, then choose **Export Docs Site** from the Export menu.

Docs Site Builder 2.0 opens a custom export dialogue for the site title, short description, and initial theme. The exported site includes Light, Dark, and System theme switching, heading navigation for each page, and static full-text search across page titles, paths, headings, body text, and code blocks. If the folder contains `README.md` or `index.md`, that document becomes the home page; otherwise the export creates a compact generated home page with bundle stats and page cards.

The generated ZIP contains:

- `index.html` as the static site entry point.
- `assets/docs-site.css` with the exported site layout and themes.
- `assets/docs-site.js` with navigation, theme switching, search, code/table-copy, and Mermaid diagram actions.
- `assets/search-index.json` with rendered pages and the local search index.
- `site-manifest.json` with format version, home page, page, heading, and diagram export stats.
- `README.md` with deployment notes.

Upload the ZIP contents to GitHub Pages or any static web host, keeping the `assets/` folder beside `index.html`.

## Advanced Import And Export

- **Export PDF** prepares a clean print view and opens the browser print dialogue. Choose **Save as PDF** in the browser to create the file.
- **Export Markdown Bundle** creates a ZIP with every loaded `.md`, `.markdown`, `.mmd`, and `.mermaid` file, current in-memory edits, image assets, and `local-docs-studio-bundle.json` metadata. Enable **Azure DevOps Mermaid syntax** to write Mermaid blocks as `::: mermaid` containers and convert top-level `flowchart` declarations to `graph` for DevOps compatibility.
- **Import ZIP** accepts Markdown Bundles from this app and generic ZIPs that contain Markdown/Mermaid files and PNG, JPEG, GIF, or WebP images. Imported files are editable virtual documents in the browser; SVG image assets are skipped for security.
- ZIP import does not convert rendered HTML back into Markdown. If a Docs Site ZIP only contains static HTML plus deployment notes, only editable Markdown/Mermaid files found in that ZIP are imported.

## Project Structure

The app is static and buildless. GitHub Pages can serve it directly without npm, a backend, or a bundler.

- `index.html` is the public app shell.
- `md-mmd-renderer-v5.html` is a compatibility redirect for older links from the original app name.
- `assets/styles/app.css` contains the app UI styles.
- `assets/scripts/main.js` boots the ESM app controller.
- `assets/scripts/app-controller.js` composes the app services and coordinates UI/event flow.
- `assets/scripts/dom.js` centralizes DOM element lookup.
- `assets/scripts/state/config.js` owns app constants and initial state.
- `assets/scripts/document/document-ux-service.js` owns preview outline, search, active section tracking, and document review notes.
- `assets/scripts/editor/editor-service.js` owns editor history, undo/redo, line numbers, Mermaid autocomplete, and Markdown formatting actions.
- `assets/scripts/files/file-service.js` owns open/save, ZIP import, local folder records, and recent file handles.
- `assets/scripts/rendering/render-service.js` owns Markdown, Highlight.js, Mermaid rendering, diagram frames, and zoom.
- `assets/scripts/exports/export-service.js` owns HTML, Word, PDF print, Markdown Bundle, Docs Site, SVG/PNG, clipboard, and export confidence flows.
- `assets/scripts/ui/ui-service.js` owns file-list UI, empty states, save/status controls, menus, theme, and resizers.
- `assets/scripts/utils/` contains shared browser, binary, file, formatting, and ZIP helpers.
- `assets/vendor/` contains pinned browser runtime libraries served locally for GitHub Pages and offline use.
- `assets/scripts/registries/content.js` stores local examples, templates, and snippets.
- `docs/tool-guide.md` is the built-in read-only feature guide opened from the Help menu.

Rendered HTML, Docs Site, and Word exports remain standalone outputs with their own embedded styles/scripts where needed.

## Development Checks

The app has no production build step. The optional npm tooling is only for local regression checks.

```sh
npm ci
npx playwright install chromium
npm test
```

- `npm run test:static` checks module syntax, relative imports, service worker cache assets, and the public shell.
- `npm run test:browser` runs Chromium and Microsoft Edge smoke tests for app load, legacy redirect, rendering, Mermaid errors, editor layout/autocomplete, image assets, PDF print HTML, Markdown bundle import/export, export packages, theme, maximisation, and mobile layout.
- Microsoft Edge must be installed locally for the `edge` Playwright project. The GitHub Actions workflow runs on `windows-latest`, where Edge is available.

## Browser Support

The app works best in Chromium-based browsers such as Chrome and Edge because they support the File System Access API for direct save-back to opened files and persistent recent file handles.

Firefox and Safari can still open files through fallback file pickers and export/download results, but direct save-back and recent file reopen may be unavailable.

## Known Limitations

- Browser security rules mean folder access and recent local handles require user permission.
- Word export converts rendered Mermaid diagrams to images where possible. Very large or unusual SVG diagrams may fall back to SVG packaging.
- Dragged image binaries are session assets. Save-back updates Markdown links, while HTML, Word, and Docs Site exports carry the actual image data. User SVG image files are not imported; use PNG, JPEG, GIF, or WebP for image assets.
- PDF export depends on the browser print dialogue; the app prepares the print document but does not create raw PDF bytes itself.
- ZIP import supports ordinary stored/deflated ZIP entries. Password-protected or encrypted ZIP files are not supported.
- Runtime dependencies are pinned under `assets/vendor/`; no CDN fetch is required for normal app loading after publication.
- GitHub Pages hosting is static; there is no server-side file storage or account sync.

## Publish To GitHub Pages

GitHub Actions is the recommended publication path. The workflow in `.github/workflows/pages.yml` runs the static checks and Playwright tests first, then deploys only the static app files to GitHub Pages from the repository default branch.

Repository settings:

1. Enable GitHub Pages.
2. Set **Source** to **GitHub Actions**.
3. Keep Actions permissions enabled for Pages deployment.

Deployment behaviour:

- Pull requests run the full test suite but do not publish.
- Pushes to the default branch publish only after `npm test` passes.
- The Pages artefact contains `index.html`, the legacy redirect, manifest, service worker, icon, `assets/`, and `docs/`.
- The public URL should open the app at `/`; older links to `/md-mmd-renderer-v5.html` redirect to `/index.html`.

For a manual branch-based fallback, commit `index.html`, `md-mmd-renderer-v5.html`, `assets/`, `docs/`, `manifest.webmanifest`, `service-worker.js`, and `icon.svg`, then configure Pages to serve that branch directly. The GitHub Actions workflow remains the safer default because it blocks deployment when exports or browser smoke tests fail.

For local testing, serve the folder with any static server:

```sh
python -m http.server 4173
```

Then open `http://127.0.0.1:4173/`.
