# Lens Docs Studio Artefact Bundle Manual Smoke Checklist

Use this checklist for release candidate validation of Lens Docs Studio artefact bundle support. Keep the app generic, static-runtime based, GitHub Pages-compatible, and local-first throughout the smoke. The Windows shell may host the same runtime through WebView2, but artefact bundle behaviour must remain browser/PWA compatible.

## Generic Workflows

- [ ] Open a generic Markdown file, edit text, render preview, and confirm save or download actions remain available.
- [ ] Open a folder with Markdown and Mermaid files, switch files from the sidebar, and confirm file filtering works.
- [ ] Import a generic ZIP without `lens-artifact-bundle.json`; confirm editable records import and the artefact reader stays hidden.
- [ ] Export a Markdown Bundle, re-import it, and confirm `lens-docs-studio-bundle.json` is accepted.
- [ ] Re-import a bundle that uses a legacy Markdown Bundle manifest name and confirm compatibility remains intact.
- [ ] Confirm Word, HTML, PDF, SVG, PNG, copy HTML, and copy text export paths are still available.

## Artefact Bundle Import

- [ ] Import a valid basic artefact bundle; confirm the declared `README.md` opens and Markdown/Mermaid records are editable.
- [ ] Import a valid rich artefact bundle; confirm summary, findings, diagrams, ADR, release notes, runbook, warnings, source tool/version, generated UTC, and evidence labels display.
- [ ] Import an invalid manifest bundle; confirm safe Markdown files still import and the full reader panel is not rendered.
- [ ] Import an unsafe path bundle; confirm safe Markdown files still import and unsafe paths or URLs are not shown.
- [ ] Confirm candidate findings remain candidate findings and confirmed findings appear only where explicitly supplied.

## Reader Panel

- [ ] Use the keyboard to tab to the reader toggle.
- [ ] Press Enter and Space to expand and collapse the reader.
- [ ] Tab to evidence and kind filters; activate a filter with the keyboard.
- [ ] Use reader search and confirm it only filters reader items.
- [ ] Activate a reader item with the keyboard and confirm the correct imported document opens.
- [ ] Confirm reader filters do not affect the generic file list.
- [ ] Change workspace and refresh the page; confirm reader filters do not persist.

## Exports

- [ ] Export a generic Docs Site without artefact metadata; confirm `site-manifest.json` and `assets/search-index.json` remain generic.
- [ ] Export a Docs Site after artefact import; confirm safe `artifactBundle` metadata is present, evidence labels display as supplied, and candidate findings remain candidate findings.
- [ ] Confirm Markdown front matter title, order, and navigation group override conflicting artefact metadata in Docs Site export.
- [ ] Confirm unsafe paths and URLs are absent from `index.html`, `assets/search-index.json`, `site-manifest.json`, and exported `README.md`.
- [ ] Confirm the exported Docs Site uses only local files and no remote scripts or remote resources.
- [ ] Export an ordinary Markdown Bundle after artefact import; confirm `lens-docs-studio-bundle.json` exists and `lens-artifact-bundle.json` does not.
- [ ] Export an artefact review pack explicitly; confirm it contains current Markdown/Mermaid records, managed assets, `lens-docs-studio-bundle.json`, and rebuilt safe `lens-artifact-bundle.json`.
- [ ] Re-import the artefact review pack; confirm the manifest round-trips as safe display metadata and documents remain editable.
- [ ] Confirm the export trust/status text states safe artefact metadata was included.
- [ ] Confirm no export claims Lens Docs Studio validated evidence.

## Static Hosting And Privacy

- [ ] Serve the app from a local static server and confirm the shell opens without a build step.
- [ ] Confirm the PWA/offline shell loads after an initial online/static-server visit.
- [ ] Publish or dry-run the exported Docs Site contents as plain static files and confirm GitHub Pages-style paths work.
- [ ] Confirm first-party UI and docs keep British English.
- [ ] Confirm the Lens Docs Studio name and generic tagline remain visible.
- [ ] Confirm no external service calls occur from manifest metadata.
- [ ] Confirm there is no telemetry, backend, API call, account flow, cloud sync, framework, bundler, transpiler, production build step, production local HTTP server requirement, or automatic publishing.
- [ ] Confirm browser-local persistence keys were not renamed and no new artefact metadata, reader filter, or active built-in profile keys were created.
