# Functional parity matrix

Reference: docs/tool-guide.md, browser regression tests, registries/content.js and the newer main image/navigation/template contracts. All rows start incomplete until validated outcomes are recorded.

| User need / existing contract | Native design | Status |
|---|---|---|
| Markdown/Mermaid/text open, save, Save as, UTF-8 | Native pickers, portable source, safe writes | In progress |
| Real folder tree, create/rename/move/delete/reveal | Reviewed file moves, source-span link edits, Windows deletion and recovery | Partial: file creation/rename/deletion and snapshot recovery pass UI checks; folder moves and case-only renames remain |
| Tabs, recent files, navigation history | Native tabs, persisted recent paths and bounded viewport history | Core and desktop navigation checks pass; broader closed-tab/history cases remain |
| Undo/redo, syntax, formatting, lists/tables, autocomplete | AvalonEdit | Incomplete |
| Paste Special, HTML/TSV conversion, rich clipboard | Windows clipboard formats | Incomplete |
| Markdown headings/tables/tasks/code/quotes/images | Markdig with native WPF preview | In progress |
| Callouts/badges/details/figures/anchors/progress/math | Explicit safe semantic mapping | Incomplete |
| Editor/preview/split, outline, search, source following | Native panels and span mapping | Incomplete |
| Mermaid types, styles, subgraphs, themes, invalid input | Mermaider plus native SVG adapter | In progress |
| Mermaid zoom/copy/SVG/PNG | Native drawing window and guarded bitmap/vector export | Partial: native zoom UI and SVG/PNG corpus pass; destination clipboard validation remains |
| Portable local images, paste/drop/URL, assets | Persistent draft assets, guarded Save as and relative paths | Partial: recovery/portability integration checks pass; URL, asset management and clipboard transfer remain |
| External changes, conflict flow, snapshots/recovery | Fingerprints, journals and a native snapshot browser | Partial: saved/unsaved text and local image copies retained; recovery opens new drafts; broader crash certification remains |
| Internal links, insertion, back/forward, wikilinks | Workspace bounded resolution, searchable document/heading picker | Native insertion and navigation checks pass; wikilinks remain |
| Review/governance, links/assets audit, docs map | Source-aware analysis | Incomplete |
| ZIP/Markdown Bundle/artefact imports | Bounded extraction, passive metadata | Partial: ZIP tests pass; artefact reader remains |
| DOCX/HTML/PDF text imports | Native parsers, safe assets | Partial: conversion tests pass; broader corpus remains |
| HTML/DOCX/PDF export | Local independent exporters | Partial: actual outputs and structural checks pass |
| DOCX templates, styles, numbering, sections/media | Open XML package preservation | Partial: styles, numbering, headers/footers/media; full contract remains |
| Docs Site and Markdown Bundle export | Static portable packages | Bundle tested; Docs Site incomplete |
| Optional artefact review pack, front matter priority | Explicit safe rebuilt manifest | Incomplete |
| Templates/snippets/studios/export profiles | Local library with legacy JSON import | Incomplete |
| Legacy migration, help, preferences | Explicit import; no browser profile scanning | Incomplete |
| Light/dark/system, keyboard, UIA, DPI/window restore | Fluent controls, system colours, per-monitor DPI and saved placement | Partial: native UI checks pass; broader accessibility and multi-monitor certification remain |
| Installer, uninstall, coexistence, portable ZIP | Isolated self-contained Inno package | Partial: host install/upgrade/uninstall/reinstall and fresh-runner lifecycle pass; clean-machine certification remains |
| GitHub Releases, channels, notifications, secure updates | Compatible signed metadata and consent | Partial: UI/policy/negative trust tests pass; positive signed upgrade remains |
| Existing web/PWA/Pages and legacy shell | Preserve separate pipelines | Baseline passed; web PR CI passed at 5ee7202; push rerun has intermittent server/rendering failures |
