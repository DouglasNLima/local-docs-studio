# Functional parity matrix

Reference: docs/tool-guide.md, browser regression tests, registries/content.js and the newer main image/navigation/template contracts. All rows start incomplete until validated outcomes are recorded.

| User need / existing contract | Native design | Status |
|---|---|---|
| Markdown/Mermaid/text open, save, Save as, UTF-8 | Native pickers, portable source, safe writes | In progress |
| Real folder tree, create/rename/move/delete/reveal | Reviewed file moves, source-span link edits, Windows deletion and recovery | Partial: file creation/rename/deletion and snapshot recovery pass UI checks; folder moves and case-only renames remain |
| Tabs, recent files, navigation history | Native tabs, persisted recent paths and bounded viewport history | Core and desktop navigation checks pass; broader closed-tab/history cases remain |
| Undo/redo, syntax, formatting, lists/tables, autocomplete | AvalonEdit with Core editing plans, native completion and one undo group per action | Core/native checks pass for formatting and bounded Mermaid completion. All 24 desktop/installed outcomes pass at f33a590, including the completion popup. Wikilink completion and broader editor parity remain |
| Paste Special, HTML/TSV conversion, rich clipboard | Windows Unicode text/CF_HTML, native Edit menu and Ctrl+Shift+V | Native/installed rich paste and source-preserving Markdown/image transfer pass 22 desktop outcomes at dd25fb0. Actual Word/Excel checks pass; live Azure DevOps attachment validation remains |
| Markdown headings/tables/tasks/code/quotes/images | Markdig with native WPF preview | Core/native checks pass for ordered starts including zero, column alignment, independent emphasis and soft/hard breaks; broader Markdown coverage remains |
| Responsive image preparation and large documents | Background raster preparation, bounded caches and cancellable batched WPF construction | Image preparation passes native/installed checks. Four dispatcher checks verify input responsiveness and cancellation during 1,200-paragraph construction; viewer pagination, oversized blocks, incremental reuse and broader performance certification remain |
| Callouts/badges/details/figures/anchors/progress/math | Explicit safe semantic mapping | Callouts, status/keyboard labels, progress and named/empty anchors retain native semantics/source mapping and export content. Anchor insertion/label editing, unique destinations and Word/PDF bookmarks pass core/native checks; its desktop regression awaits CI. Details/image figures/math and broader anchor forms remain |
| Editor/preview/split, outline, search, source following | Native panels and span mapping | Incomplete |
| Mermaid types, styles, subgraphs, themes, invalid input | Mermaider plus native SVG adapter | In progress |
| Mermaid zoom/copy/SVG/PNG | Native drawing window and guarded bitmap/vector export | Partial: native zoom UI and SVG/PNG corpus pass; destination clipboard validation remains |
| Portable local images, paste/drop/URL, assets | Persistent drafts, guarded Save as, explicit HTTPS downloads, native copying and a workspace image manager | Core audit/rename/removal/recovery and desktop/installed CI pass. Markdown/image clipboard transfer retains source and saved pixels in actual clipboard/paste/Save as checks at dd25fb0 |
| External changes, conflict flow, snapshots/recovery | Fingerprints, journals and a native snapshot browser | Partial: saved/unsaved text and local image copies retained; recovery opens new drafts; broader crash certification remains |
| Internal links, insertion, back/forward, wikilinks | Workspace bounded resolution, searchable document/heading/anchor picker | Native insertion and navigation checks pass. The picker now includes unsaved anchors/current headings; its new exact-destination desktop regression awaits CI. Wikilinks remain |
| Review/governance, links/assets audit, docs map | Source-aware analysis | Asset usage audit implemented with draft overrides, missing-image detection and incomplete-scan warnings; other review features incomplete |
| ZIP/Markdown Bundle/artefact imports | Bounded extraction, passive metadata | Partial: ZIP tests verify actual-byte limits, declared sizes, content checksums and no partial destination on rejection; artefact reader remains |
| DOCX/HTML/PDF text imports | Native parsers, safe assets | Partial: conversion tests pass; broader corpus remains |
| HTML/DOCX/PDF export | Local independent exporters | Actual outputs and structural checks pass; DOCX retains callouts/list starts/alignment/extended emphasis. PDF retains explicit deletion markers; strikeout/highlight fidelity remains |
| DOCX templates, styles, numbering, sections/media | Open XML package preservation | Partial: styles, numbering, headers/footers/media; full contract remains |
| Docs Site and Markdown Bundle export | Guarded static ZIPs with native preparation and offline export templates | Bundle tests and all 27 desktop/installed outcomes pass at 95a811f, preserving drafts/assets and excluding private state. Docs Site metadata, navigation, direct section search, named/empty anchors and themes pass core/native/browser checks. Artefact fallback, wikilinks and broader export controls remain |
| Optional artefact review pack, front matter priority | Explicit safe rebuilt manifest | Incomplete |
| Templates/snippets/studios/export profiles | Local library with legacy JSON import | Incomplete |
| Legacy migration, help, preferences | Explicit import; no browser profile scanning | Incomplete |
| Light/dark/system, keyboard, UIA, DPI/window restore | Fluent controls, system colours, per-monitor DPI and saved placement | Partial: native UI checks pass; broader accessibility and multi-monitor certification remain |
| Installer, uninstall, coexistence, portable ZIP | Isolated self-contained Inno package | Partial: host install/upgrade/uninstall/reinstall and fresh-runner lifecycle pass; clean-machine certification remains |
| GitHub Releases, channels, notifications, secure updates | Compatible signed metadata and consent | Partial: UI/policy/negative trust tests pass; positive signed upgrade remains |
| Existing web/PWA/Pages and legacy shell | Preserve separate pipelines | All four CI runs pass at 95a811f. A real asynchronous draft-recovery overwrite is fixed with a deterministic regression; static checks and all 256 local browser tests pass, followed by both complete web CI runs |
