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
- Browser-free Mermaider rendering, native SVG validation, frozen drawing caches, background STA preparation, zoom and SVG/PNG exports. Forty light/dark corpus cases pass.
- Bounded ZIP, DOCX, HTML and PDF text imports; guarded DOCX, HTML, local PDF and generic Markdown Bundle exports. Template styles and package headers/footers/media are preserved, with full template contracts still incomplete.
- Fluent light/dark/system controls, high-contrast resources, working-area sizing and saved window placement. Operation and preview status are separate so preview completion cannot hide recovery feedback.
- Separate self-contained installer/portable packaging and native CI. Development-host preview 4 → 5 upgrade, uninstall/reinstall and preservation checks passed. Fresh Windows runners also exercise installed-app workflows.
- Compatible GitHub release discovery, channel preferences, signed-manifest policy, bounded downloads, integrity and pinned Authenticode checks, separate download/install consent and temporary update launcher. Production trust is unconfigured; unsigned builds cannot execute downloaded installers.

## Current evidence

- Local Release build: no warnings/errors. Core: 104 tests pass, including remote-image policy and streamed-size/cancellation cases.
- Native integration: 40 diagram cases, unsafe SVG rejection, actual DOCX/PDF/HTML outputs, eight image/operation checks, and a valid oversized PNG rejected by its dimensions.
- Desktop harness: 16 outcomes pass, including copying actual rendered pixels into a second document and preserving both saved image files. The remote transport itself uses deterministic simulated responses in integration tests.
- All four native/web CI runs passed at ecc5fba. At c7467b4, both native CI runs and web PR CI passed; web push failed two Edge readiness checks with refused local-server connections.
- The local web follow-up uses HTTP/1.1, a larger connection queue and retained failure traces. Static checks passed; the full browser run passed 250 tests and failed four. Two failures exposed an asynchronous menu-test race, now corrected, and two reported closed browser processes. All four passed on a focused rerun. Fresh CI is required; no web runtime source changed.

## Remaining work

Next: inspect CI for the image-download and browser-test infrastructure changes; implement image clipboard portability and asset management; continue richer editing, review and template parity.

Other gaps: folder moves/case-only rename, safe HTML semantics, incremental/large-document preview, Docs Site and artefact reader/review pack, full Word template fidelity, template/snippet libraries, general preferences/migration/help, destination clipboard validation, accessibility and multi-monitor certification. Draft images, recovery records and update-helper copies remain retained; automatic pruning is not implemented.

External acceptance prerequisites: a trusted production signing identity and clean Windows install/update certification. Fresh CI runner evidence does not establish SDK-free clean-machine or positive signed-upgrade certification.
