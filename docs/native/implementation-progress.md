# Native implementation progress

Current milestone: native workspace navigation, portable assets and recovery robustness.

- Isolated worktree and feature/native-wpf-rebuild branch from develop 2cc3b5680958c9c553dad23856e6010c158fccd7.
- Original web checkout and untracked attachments preserved.
- Reviewed feature guide, browser test inventory, native filesystem/watcher services, static publication workflow, Inno installer and Word template contracts (including newer main).
- .NET SDK 10.0.401, Windows x64 and Inno Setup available.
- Baseline legacy Release build passed: 0 errors, 6 existing PRI qualifier warnings.
- Baseline legacy native bridge smoke passed; output files checked by the existing harness.
- Full web baseline passed: 254 browser tests and static integrity checks. Evidence: artifacts/native/baseline.
- Native Release build passes with zero warnings/errors; 67 core tests pass.
- Implemented guarded UTF-8 saves, portable path resolution, folder enumeration, recovery/session stores, source-aware Markdig parsing and Mermaid error handling.
- Integrated WPF workspace tree, tabs, AvalonEdit, source-tagged FlowDocument preview, native Mermaid drawing, diagram SVG/PNG export, outline, contextual find/replace, basic formatting, local image ingestion, themes/layout and external-change indicators.
- Actual WPF GUI inspected through Computer Use; contextual search, Unicode save and session restoration validated. FlaUI exercises six desktop outcomes, including update settings, and verifies clean process exits. Its first run exposed and led to a fix for re-entrant WPF shutdown.
- Forty native light/dark Mermaid corpus cases pass, including explicit expected-invalid inputs. Selected PNGs and the generated PDF were visually inspected; broader visual fidelity remains open.
- DOCX, HTML and local PDF export implemented. DOCX schema/style/relationship tests pass; generated PDF text and diagram image verified with PdfPig. Template export preserves package headers/footers/media, but the full page-master/prototype contract is incomplete.
- ZIP and DOCX/HTML/PDF text import implemented with bounded input, passive metadata and new-folder persistence. Generic Markdown bundles include current editor text and portable assets, excluding the artefact manifest.
- Export replacement checks the destination under an exclusive handle and records a durable recovery journal before committing. Import staging keeps failed conversions out of completed workspaces.
- Self-contained packaging and the separate Inno installer passed a development-host install/save/reopen/uninstall/reinstall lifecycle. Same-state activations use a same-user named pipe; a native-only mutex protects files in use during installation.
- Integrated periodic GitHub release checks, native update settings, version/channel/architecture/product/OS eligibility, RSA-PSS manifest verification, streamed download integrity and pinned Windows Authenticode publisher validation. Separate download/install commands and a temporary native launcher protect graceful shutdown. Production trust remains unconfigured; unsigned previews can open eligible release pages but cannot run downloaded installers.
- Added preview packaging, optional existing-certificate signing, manifest signing integration and dependency notices. Publication remains a separate unauthorised action.
- Native CI workflow added independently of the unchanged Pages pipeline.
- Native diagrams are prepared on a background STA thread and frozen drawings are reused through bounded caches. Eight unsafe SVG resource payloads are rejected and the forty-case corpus passes. Native zoom adds a seventh passing UI outcome.
- Built-in WPF Fluent resources provide light/dark controls, Windows theme changes and high-contrast colours. Per-monitor DPI metadata, working-area sizing and saved window placement are implemented; multi-monitor and screen-reader certification remain open.
- Different-version upgrade from preview 4 to preview 5 passed on the development host, including actual installed-app checks, uninstall/reinstall and preservation of source, existing defaults and legacy registration.
- Native PR/push CI and web PR CI passed at 5ee7202. The web push run and rerun failed in different browser tests (including a refused local-server connection); logs are retained for investigation.
- Recent documents/workspaces, bounded Back/Forward history and a searchable native document-link picker with heading targets are implemented. Ten desktop outcomes pass, including link insertion, actual internal navigation and persisted recent paths.
- Unsaved images are retained in an isolated draft folder recorded in session recovery. Save as copies referenced images to portable relative locations and rebases links inside a workspace. Leaving document links in another workspace requires an explicit in-product confirmation. Native PNG decoding, recovered-draft exports and source-anchor insertion pass integration checks.
- Malformed save journals are retained and reported without hiding other drafts. Malformed session versions are archived before future state writes can replace them. Drive-root workspaces use correct containment checks.
- Core suite: 78 tests pass. Native rendering/export/asset integration: 40 diagram cases plus SVG safety and recovered-image checks pass. Desktop harness: 11 outcomes pass, including actual clipboard bitmap paste into an unsaved document and Save through the Windows dialogue. The image test exposed AvalonEdit's text-only Paste enablement; native routed paste handling now accepts image-only clipboard data.

Next executable tasks: check this batch in CI; implement safe rename/move/delete and affected-link planning, image clipboard portability and asset management; continue editing, review and template parity. Positive signed upgrade and clean-machine certification remain external acceptance prerequisites.

Known implementation limitations: full-document native preview rebuild; HTML shown as source; full template fidelity, Docs Site, asset management, richer editing/review, general preferences and installer/update certification are incomplete. Temporary draft assets are retained for recovery; automatic cache pruning is not implemented. Core save uses an exclusive handle and durable pre-write journal, preserving file ACLs and providing explicit recovery after interruption. Update launcher copies remain in the local cache for diagnosis.

Open external acceptance prerequisites: trusted production signing identity and clean Windows installation/update certification. GUI automation is available. No release or merge authorised.
