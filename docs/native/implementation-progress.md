# Native implementation progress

Current milestone: baseline certification and native technical feasibility.

- Isolated worktree and feature/native-wpf-rebuild branch from develop 2cc3b5680958c9c553dad23856e6010c158fccd7.
- Original web checkout and untracked attachments preserved.
- Reviewed feature guide, browser test inventory, native filesystem/watcher services, static publication workflow, Inno installer and Word template contracts (including newer main).
- .NET SDK 10.0.401, Windows x64 and Inno Setup available.
- Baseline legacy Release build passed: 0 errors, 6 existing PRI qualifier warnings.
- Baseline legacy native bridge smoke passed; output files checked by the existing harness.
- Full web baseline passed: 254 browser tests and static integrity checks. Evidence: artifacts/native/baseline.
- Native Release build passes with zero warnings/errors; 24 core tests pass.
- Implemented guarded UTF-8 saves, portable path resolution, folder enumeration, recovery/session stores, source-aware Markdig parsing and Mermaid error handling.
- Integrated WPF workspace tree, tabs, AvalonEdit, source-tagged FlowDocument preview, native Mermaid drawing, diagram SVG/PNG export, outline, contextual find/replace, basic formatting, local image ingestion, themes/layout and external-change indicators.
- Actual WPF GUI inspected through Computer Use; editor search and focus return validated. Broader UI testing remains.
- Initial native Mermaid corpus identified expected-invalid autocomplete input; expanding the corpus and native visual coverage.
- Native CI workflow added independently of the unchanged Pages pipeline.

Next executable tasks: complete save/reopen GUI validation; run expanded native Mermaid corpus; implement DOCX and PDF exports with structural tests; extend filesystem/image/session regression coverage; then imports, remaining feature parity, installer and secure update workflow.

Known implementation limitations: full-document native preview rebuild; HTML shown as source; unsaved-image insertion currently requests an initial save; template/export/import, asset management, richer editing/review/navigation, settings/update UI and installer are incomplete. Core save uses an exclusive handle and durable pre-write journal (preserving file ACLs), with explicit recovery after interruption rather than an unlocked replacement race.

Open external acceptance prerequisites: trusted production signing identity; disposable clean Windows installation/update environment. These do not block independent development. GUI capability is being investigated. No release or merge authorised.
