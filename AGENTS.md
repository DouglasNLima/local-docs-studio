# AGENTS.md

Guidance for coding agents working in this repository.

## Project Overview

This is Lens Docs Studio, a buildless, browser-only Markdown, Mermaid, and documentation studio. It must remain compatible with publication on GitHub Pages and should run directly from GitHub Pages or from a local static server, without a backend or production build step.

The product remains a generic local documentation studio. Do not position it as a Power Platform Lens-only tool. Use the generic tagline: Local Markdown, Mermaid, and documentation studio.

The Lens visual identity uses `#FF883E` as a restrained brand accent for primary actions, selected indicators, active states, focus states, and compact brand moments. Prefer neutral surfaces (`#FAFAFA`, `#F8F9FB`, `#F3F4F6`, `#EDEDED`) and high-contrast text. Avoid broad orange panels, saturated backgrounds, or noisy command surfaces.

Use British English for first-party UI, docs, comments, test names, logs, generated copy, examples, templates, and snippets where wording is not an immutable external/API term.

The app supports Markdown editing and preview, Mermaid rendering, ZIP import/export, Word/HTML/PDF-oriented exports, docs-site export, local file/folder workflows, document outline/search/review helpers, and a PWA/offline shell.

Optional Lens artefact bundle support is ZIP-import context only unless the user explicitly exports an artefact review pack. Keep it optional, treat `lens-artifact-bundle.json` metadata as untrusted, never infer evidence levels, never convert candidate findings into confirmed findings, and preserve generic Markdown/Mermaid workflows.

## Repository Layout

- `index.html`: public app shell.
- `md-mmd-renderer-v5.html`: legacy redirect to `index.html`.
- `assets/styles/app.css`: application UI styles.
- `assets/scripts/main.js`: ESM entry point.
- `assets/scripts/app-controller.js`: service composition and app-level event flow.
- `assets/scripts/dom.js`: DOM lookup and element references.
- `assets/scripts/state/config.js`: constants and initial state.
- `assets/scripts/document/`: preview document UX, outline, search, selection, and scroll sync.
- `assets/scripts/editor/`: editor behaviour, history, formatting actions, autocomplete, and paste handling.
- `assets/scripts/files/`: open/save, ZIP import, folder records, and recent handles.
- `assets/scripts/rendering/`: Markdown, Highlight.js, Mermaid, diagram frames, and zoom.
- `assets/scripts/exports/`: HTML, Word, PDF print, Markdown Bundle, Docs Site, SVG/PNG, clipboard, and export flows.
- `assets/scripts/exports/export-profile-service.js`: session-only built-in export profile presets.
- `assets/scripts/ui/artifact-bundle-reader.js`: optional artefact reader panel rendering and local filters.
- `assets/scripts/ui/`: menus, file list, theme, layout, status, resizers, and general UI behaviour.
- `assets/scripts/utils/`: shared browser, binary, file, formatting, DevOps Markdown, and ZIP helpers.
- `assets/scripts/registries/content.js`: examples, templates, snippets, and local content studios.
- `docs/tool-guide.md`: built-in read-only feature guide opened from Help.
- `tests/static/check-assets.mjs`: static integrity checks for modules, imports, service worker cache, shell, manifest, workflow, and README guidance.
- `tests/browser/app-smoke.spec.mjs`: Playwright smoke/regression coverage.
- `tests/fixtures/`: Markdown, Mermaid, docs-site, and ZIP-related fixtures.

Generated artefacts such as `node_modules/`, `test-results/`, `playwright-report/`, and local logs are ignored and should not be edited as source.

## Development Commands

Install dependencies:

```sh
npm ci
```

Install Playwright browsers when needed:

```sh
npx playwright install chromium
```

Run all checks:

```sh
npm test
```

Run only static checks:

```sh
npm run test:static
```

Run browser smoke tests:

```sh
npm run test:browser
```

Run a local static server:

```sh
python -m http.server 4173
```

Then open `http://127.0.0.1:4173/`.

The Playwright config starts the same server automatically and uses `http://127.0.0.1:4173` as `baseURL`.

## Architecture And Coding Conventions

- Keep the app buildless. Do not introduce a bundler, transpiler, framework, or backend unless explicitly requested.
- Use native ESM modules and relative imports.
- Treat GitHub Pages compatibility as a project requirement. Changes must continue to work when the repository is served as static files from GitHub Pages.
- Keep local asset references relative where the app, manifest, service worker, and static checks expect relative paths.
- Preserve the current service-oriented structure. Add behaviour to the owning service instead of growing `index.html` or `app-controller.js` unnecessarily.
- Keep UI styles in `assets/styles/app.css`; avoid inline styles except for dynamic values that genuinely need runtime calculation.
- Prefer existing utilities in `assets/scripts/utils/` before adding new helpers.
- Keep browser-only behaviour defensive. File System Access API, clipboard APIs, downloads, and print flows need fallbacks or clear status messages when browser support is limited.
- Keep exports clean. App-only review/search/outline UI should not leak into generated HTML, Word, Docs Site, or Markdown Bundle output unless intentionally added.
- Keep Lens artefact support passive. Do not add direct links or integrations to other Lens tools, and do not call external services from artefact bundle metadata.
- Keep built-in export profiles session-only. Applying the Azure DevOps Wiki Markdown preset must use a session override and must not write the existing DevOps preference key unless the user explicitly uses the persisted toggle or saved local profile flow.
- Keep generic Markdown Bundle export free of `lens-artifact-bundle.json`. Include a rebuilt safe artefact manifest only through the explicit artefact review pack export path.
- Keep CDN/offline behaviour in sync. When adding local modules or cacheable static assets, update `service-worker.js` so `npm run test:static` continues to pass.
- Preserve legacy entry behaviour in `md-mmd-renderer-v5.html`.

## Testing Guidance

Before handing off meaningful changes, run the narrowest useful check:

- For module/import/service worker/shell changes, run `npm run test:static`.
- For UI behaviour, rendering, import/export, layout, clipboard, menu, theme, or browser workflow changes, run `npm run test:browser`.
- For broad changes, run `npm test`.

Browser tests include Chromium and Microsoft Edge projects. Edge must be installed locally for the `edge` project; CI on `windows-latest` is expected to have it available.

If you add new user-visible behaviour, prefer extending `tests/browser/app-smoke.spec.mjs` with a focused smoke/regression test and use fixtures under `tests/fixtures/` when possible.

## Important Maintenance Notes

- `tests/static/check-assets.mjs` verifies that every module in `assets/scripts/` is cached by the service worker. New modules usually require a `service-worker.js` cache update.
- The service worker must continue caching `fflate@0.8.2` for ZIP import support.
- `manifest.webmanifest` should keep `start_url` and `scope` as `./` for GitHub Pages hosting.
- GitHub Pages deployment should stay fully static. Do not depend on server routes, server-side APIs, environment variables, filesystem writes, or runtime build output.
- `README.md` is part of the static validation surface. Keep development, export, import, and publishing guidance accurate when those workflows change.
- Generated Docs Site ZIP contents under `test-results/` are test outputs, not source templates.
- The app intentionally has no production build command. Do not add one just to run or publish the existing app.

## Agent Workflow

1. Inspect the relevant service/module before editing.
2. Make small, scoped changes that follow the existing module boundaries.
3. Update tests, fixtures, docs, service worker cache entries, or README guidance when the change affects them.
4. Run the most relevant checks and report any skipped checks with the reason.

Prefer preserving user data and browser-local workflows. Avoid changes that silently discard unsaved edits, local handles, session images, imported ZIP contents, or export settings.
