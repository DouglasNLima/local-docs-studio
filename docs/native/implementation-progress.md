# Native implementation progress

Current milestone: native filesystem editing, recovery and asset workflows. This remains an incomplete internal preview, with no release or merge authorised.

## Baseline and isolation

- Feature branch/worktree: feature/native-wpf-rebuild, based on develop 2cc3b5680958c9c553dad23856e6010c158fccd7. Original main checkout and unrelated attachments are preserved.
- Reviewed the feature guide, browser tests, existing Windows integrations, installer, publication rules and newer main image/navigation/Word template contracts.
- Baseline web static checks and all 254 Chromium/Edge tests passed. Legacy Windows Release build passed with six existing PRI warnings; native bridge smoke passed. Evidence: artifacts/native/baseline.
- The .NET 10/WPF solution, state, Inno identity, associations and update product are separate from the web application and WinUI/WebView2 shell.

## Implemented and exercised

- Guarded UTF-8 saves, exclusive conflict checks, durable recovery journals, session/draft recovery, queued commands and background import/export. Malformed recovery records are retained and reported.
- WPF workspace tree, native pickers, tabs, recent paths, Back/Forward viewports, internal document/heading links, outline, focused editor/preview search, AvalonEdit formatting and source-aware native preview.
- File/folder creation, reviewed document rename/move with incoming/outgoing Markdown link updates, rollback and named recovery drafts. Actual unsaved linked edits survive the save/replan/review flow.
- Confirmed Windows deletion with separate saved/unsaved snapshots; a native snapshot browser restores new drafts for explicit Save as. Local images are copied into snapshots where possible, with warnings for missing assets. Manual and pre-reload snapshots use the same store.
- Persistent clipboard/drop/local images, draft asset recovery and portable Save as. Rendered images have a native Copy image context action; paste/save in another document creates an independent portable asset. Explicit HTTPS image downloads have bounded size, redirects and timeouts; no document content, cookies or local paths are sent. Native raster dimensions are checked before the decoded pixel cache is allocated.
- Native workspace image manager with background previews, saved-file draft references and missing/unreferenced lists. Reviewed image rename updates exact Markdown URL spans; confirmed removal retains independent image snapshots. Incomplete audits, HTML references, conflicts and native state receive conservative handling. Core checks and desktop/installed CI pass.
- Browser-free Mermaider rendering, native SVG validation, frozen drawing caches, background STA preparation, zoom and SVG/PNG exports. Forty light/dark corpus cases pass.
- Serial background raster preview preparation with frozen image reuse by content hash, aggregate memory/source budgets and a bounded cache. Native layout no longer opens image files. Full FlowDocument construction remains on the UI thread.
- Bounded ZIP, DOCX, HTML and PDF text imports; guarded DOCX, HTML, local PDF and generic Markdown Bundle exports. Template styles and package headers/footers/media are preserved, with full template contracts still incomplete.
- Copy rendered content prepares sanitised CF_HTML and Unicode text, embedding native images/diagrams and retaining table cells. Source/image limits, cancellation and clipboard sequence checks protect preparation; external destination fidelity remains to be validated.
- Automatic HTML/TSV paste and nine Paste Special modes, including literal text with Ctrl+Shift+V. HTML import shares conversion for nested lists, code delimiters, literal punctuation and bounded merged tables. Embedded images are persisted before an undoable anchored source replacement.
- Copy Markdown with images retains literal source around prepared images/complete Mermaid blocks and carries ordered image elements in CF_HTML. Native paste saves embedded raster data to portable assets. Remote references and incomplete fences stay literal; shared image references expand only at their image occurrences.
- Native callouts retain labels, formatted content and source mapping. Ordered starts, including zero, table alignment, independent emphasis, scripts, insertion/highlight and soft/hard line breaks render correctly. DOCX preserves these text properties; PDF preserves supported properties and explicit deletion markers.
- Fluent light/dark/system controls, high-contrast resources, working-area sizing and saved window placement. Operation and preview status are separate so preview completion cannot hide recovery feedback.
- Separate self-contained installer/portable packaging and native CI. Development-host preview 4 → 5 upgrade, uninstall/reinstall and preservation checks passed. Fresh Windows runners also exercise installed-app workflows.
- Compatible GitHub release discovery, channel preferences, signed-manifest policy, bounded downloads, integrity and pinned Authenticode checks, separate download/install consent and temporary update launcher. Production trust is unconfigured; unsigned builds cannot execute downloaded installers.

## Current evidence

- Local Release build: no warnings/errors. Core: 187 tests pass, including source/CRLF clipboard fidelity, the existing Azure DevOps fidelity fixture, first-save draft links, five callout kinds, Word schema/text styles and ordered clipboard text.
- Native integration: 40 diagram cases, unsafe SVG rejection, actual DOCX/PDF/HTML outputs, thirteen image/operation checks, including saved Markdown source fidelity and pixel-identical clipboard image transfer, rich image paste, anchored block insertion, budgets and cancellation.
- Desktop harness: all 21 outcomes pass in both native CI runs at 7ad9a13, including rich copy/paste, saved images, CSV, invalid-image selection preservation, Ctrl+Shift+V and Undo. The same checks pass after installation/reinstallation. Local foreground access is unavailable. Remote transport uses deterministic simulated responses in integration tests.
- Six native preview checks pass: frozen pixels, content reuse/invalidation, memory budgets/eviction, unresolved resources, cancellation and rendering after source image removal. Both native CI runs at 0e7c343 pass, including 21 desktop outcomes and installed lifecycle.
- Seven native Markdown outcomes pass: callouts, numbering, alignment, emphasis, line breaks, actual light/dark page rendering and PDF content. Images were visually inspected; DOCX is schema-validated. PDF strikeout/highlight formatting remains incomplete.
- Native PR CI 38161563199 at dd25fb0 passes all 22 desktop outcomes before installation, after installation and after reinstall. The unchanged source/pixel check verifies the corrected first-save handling of ordinary draft links.
- Optional local Office destination checks pass for Word and Excel: actual saved DOCX preserves Unicode, tables, bold and original diagram pixels; XLSX preserves adjacent cells, formula-like text and leading-zero identifiers. Evidence: artifacts/native/office-certified/office-clipboard.json. The original Excel instance was preserved.
- All four native/web CI runs passed at ecc5fba. At c7467b4, both native CI runs and web PR CI passed; web push failed two Edge readiness checks with refused local-server connections.
- At 5210d81 both native CI runs passed; web PR CI passed 238/254 and push CI passed 244/254. Traces show transient status assertions, an asynchronous bridge assertion and an import-worker interception wait; other failures report closed browser contexts. Investigation remains open; no web runtime source changed.

## Remaining work

Next: validate the new Markdown rendering outcomes in CI and continue safe HTML semantics, editing, review and template parity. The clipboard desktop/installed workflow passes 22 outcomes at dd25fb0. Playwright is updated from 1.60.0 (Chromium 148) to 1.64.0 (Chromium 156). Static checks and all 254 local Chromium/Edge tests pass in 12.9 minutes; fresh web CI remains pending.

Other gaps: folder moves/case-only rename, safe HTML semantics, incremental/large-document preview, Docs Site and artefact reader/review pack, full Word template fidelity, template/snippet libraries, general preferences/migration/help, destination clipboard validation, accessibility and multi-monitor certification. Draft images, recovery records and update-helper copies remain retained; automatic pruning is not implemented.

External acceptance prerequisites: a trusted production signing identity and clean Windows install/update certification. Fresh CI runner evidence does not establish SDK-free clean-machine or positive signed-upgrade certification.
