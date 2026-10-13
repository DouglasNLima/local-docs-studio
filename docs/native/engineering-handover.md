# Native Windows engineering handover

**Internal preview; production acceptance remains incomplete.** The latest verified package is unsigned preview.259 at clean branch head 51688aa7433dc4cdf29e837bf9df6456622224fe. Downloaded native push run 38332285897 verifies 936 core tests, 40 corpus cases and four 64-check desktop contexts, including actual Word import and retained workspace images/links after switching folders, Save and restart. The full installer lifecycle passes with 20 preserved state files, two restored sessions and bundled installed runtimes. Both web runs pass 262 tests each. The parallel native PR run 38332288667 fails while observing the preview TextPattern after an upgraded application restarts; this failure remains recorded.

The workspace correction is included in preview.259. All twelve phase reports across the built, installed, upgraded and reinstalled applications pass exact draft/save, visible images, two-way links and retained roots. The upgraded application screenshot after restart was visually inspected. The PR failure occurs before its preview Text provider is available; the editor already contains the restored text. The harness now waits for preview completion and observes provider availability within its existing deadline, retaining every image/link assertion. This later harness correction awaits hosted confirmation.

Current source adds native Insert commands for all six status kinds, keyboard shortcut labels and horizontal rules, closing a concrete gap against the web guide. Labels remain passive portable text, existing spans can be edited, invalid input stays in the dialogue, Cancel preserves source, and each action uses one undo group. The Release solution builds without warnings; 968 core tests, the complete 40-case native rendering/export harness and ten windowless AvalonEdit insertion/selection/Undo/Redo/read-only cases pass. A new 65th desktop workflow covers the actual menus, dialogues and preview; hosted confirmation is pending. These commands are not in preview.259. The follow-up also keeps a preceding pipe table intact by separating a new label paragraph; edits inside a table cell remain inline. Both first hosted probes fail at the new helper's SetFocus before Undo; it now uses the established editor click, retaining complete source comparisons.

Latest installer: artifacts/native/ci-51688aa-push/packages/0.1.1-preview.259/LensDocsStudio.Native-0.1.1-preview.259-win-x64-Setup.exe (58,888,018 bytes). SHA-256: AFFB9437722CD4581CA5E6A5985569F8D4D53F1EA41CAB4E3FBFF1E1AAA39DBB. ZIP SHA-256: 1237BC317349561699115073F7A970992C5D21D28ADD1160A7DF39E8943F7E4F. Evidence: artifacts/native/ci-51688aa-push/{package-verification,runtime-evidence-review,package-runtime-inspection}.json. All four package hashes and exact clean source metadata pass; both 529-entry ZIPs contain the required native runtimes and none of the explicitly checked browser runtimes. This unsigned package includes the workspace correction and predates inline insertion commands. No downloaded installer was run locally.

## Architecture and implemented workflows

The separate `src/native` solution uses C#, .NET 10, WPF/XAML, CommunityToolkit.Mvvm, AvalonEdit, Markdig, Mermaider, SharpVectors, Open XML SDK and PDFsharp/MigraDoc. Core owns domain/parsing, filesystem, imports/exports, state and update policy; Native owns Windows services and presentation. Editing, preview and diagrams do not host a browser or JavaScript runtime. The buildless web/PWA/GitHub Pages application and legacy WinUI/WebView2 shell remain separate.

Implemented workflows cover portable UTF-8 documents and workspace assets; folders, tabs, recent items, outline and context-sensitive search; formatting, completion, source navigation and history; guarded saves, external-change conflicts, snapshots and independent recovery; native Markdown/Mermaid/maths rendering; images, clipboard formats, supported DOCX/HTML/PDF imports and DOCX/PDF/HTML/ZIP/Docs Site exports; content/template libraries, generators, profiles, settings transfer and explicit legacy migration. Optional artefact metadata remains passive.

Windows improvements include native pickers and Explorer actions, filesystem monitoring, reviewed link-preserving moves, selectable preview text, paginated reading, native accessibility metadata and restored workspaces. Six persisted diagram palettes remain independent of application appearance. Failed diagrams expose native source-copy and edit actions. See the [parity matrix](functional-parity-matrix.md), [experience improvements](native-experience-improvements.md) and [architecture decisions](architecture-decisions.md) for feature-level contracts and limits.

## Earlier preview.246 verification

The next source change preserves Word image descriptions. Local validation now passes 924 core tests and all 40 native corpus cases; the 12 new regressions reproduce 11 pre-fix failures plus one passing fallback control. A new 63rd actual desktop workflow covers Word import cancellation, complete warning exposure and portable saved image output, but awaits hosted execution. The package and downloaded CI evidence below remain preview.246 and do not yet certify this change. The later documentation-only c044a61 CI passed all four runs.

| Evidence | Verified scope |
|---|---|
| Core | 912 passed in each downloaded TRX |
| Native rendering/exports | 40 diagram/export corpus cases, zero failures; supporting native reports retained |
| Actual desktop | 62 checks in each of built, installed, upgraded and reinstalled contexts, in both native runs |
| Details keyboard | All eight context reports verify header focus, Space expansion/collapse and unchanged Markdown |
| Installation | Five wizard outcomes, four files-in-use preservation assertions, installation, same-source upgrade, uninstall and reinstall |
| Recovery/session | 20 retained state files, unchanged workspace source and two restored sessions per native run |
| Bundled runtime | All three installed contexts load their actual .NET/WPF modules from their own installation directory |
| Web | 262 cases pass in each web run; static publishing remains buildless |
| Package inspection | Eight installer/ZIP hashes, sizes, exact clean revisions, runtime entries and unsigned metadata verified |

The native PR run is [38322046930](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38322046930), with push run [38322043071](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38322043071). Web PR/push runs are [38322046933](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38322046933) and [38322043043](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38322043043). Downloaded evidence is under `artifacts/native/ci-2af4471-pr` and `ci-2af4471-push`; each includes `package-verification.json` and `runtime-evidence-review.json`.

The details toggle now reuses a fixed application-owned XAML template per WPF thread. Local native pattern checks pass, and final local light/dark PNGs are byte-identical to the preceding render. The downloaded preview.246 light/dark details pages were also visually inspected. Three fresh local processes pass all 12 nested construction cases with maximum input gap 63.34 ms. The hosted details cases record first input at 23.9392 ms (PR) and 20.2413 ms (push), with maximum input gaps of 29.7557 ms and 28.9035 ms respectively. These observations do not establish elimination of the historical 583 ms/728.273 ms details or 360.6 ms table pauses; the 250 ms construction-input bound is unchanged. Broader continuous/page layout remains an acceptance limit.

## Compatibility and data safety

Word import preserves supported direct/style-supplied lists, starts, nesting, continuations, restart rules and headings; supported web/email links and paired bookmarks; recognised literal code and grouped quotes. Actual DOCX reimport retains six lists, 12 items, six continuations and two exact native image payloads. Moving those 12 items to inherited styles produces identical Markdown without warnings. CRLF-to-LF code conversion has an explicit regression. Unsupported conversions retain notes.

PDF lists retain one marker per item, nested/continuation alignment and bounded images/tables. Ninety continuation paragraphs retain exact selectable word order across five pages; all seven pages of the earlier hosted list fixture were inspected. Word exports use editable native numbering. Package/schema checks do not certify actual destination pagination, all template styles, complex scripts, bidirectional text, joined/colour emoji or tagged PDF accessibility. Code language, deeper quote structure and field/file links remain limited. The [diagram compatibility report](diagram-compatibility.md) identifies unsupported Mermaid forms rather than substituting a browser or placeholder.

Prior failures and corrections remain in the [validation report](validation-report.md), including the reproduced CRLF assertion mismatch, intermittent WPF delays, browser closure and initial UIA timeout. The sealed `b45246e..cf436b0` security review found one Low Word code-allocation issue addressed by a later bounded preflight and regression tests; later changes are outside that immutable review.

## Installer, updates and artefacts

The self-contained x64 Inno installer and portable ZIP have distinct product, installation, state and association identities. Uninstall preserves documents and application state. The upgrade fixture changes preview versions from the same source; it does not prove historical-schema migration or an SDK-free clean machine.

Signing adapters support a configured certificate or service. The user's confirmed configuration has no certificate, service, thumbprint or verified publisher. Packages remain `NotSigned` internal previews, update envelopes contain no identity/signature and unsigned updates cannot execute. The implemented update flow separates discovery, authorised download, final verification, unsaved-document handling and explicit installation. Disposable CI certificates test signed metadata, Windows trust and publisher pins, then are removed; the combined fixture does not execute a production-signed upgrade. See [update and packaging](update-and-packaging.md).

Build: `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`. Package: `scripts/native/Build-NativePackage.ps1` with Inno Setup. Normal self-contained execution does not require build tools.

- Installer: `artifacts/native/ci-2af4471-pr/packages/0.1.1-preview.246/LensDocsStudio.Native-0.1.1-preview.246-win-x64-Setup.exe` (58,863,020 bytes).
- Installer SHA-256: `8E4C1DFAA0D4825A0DF9524C4087EE755E91DC7CC22078AD656FA8EF8D977B5F`.
- Portable ZIP SHA-256: `DFAEDC77E0A1ED91A9EFA3C8EDED21DE1827721DBA34981CC32FDE8A861F3544`; same package directory.
- Branch: `feature/native-wpf-rebuild`; package source head `2af4471fcdd982fa4888b025bd1ad08f058d91c7`, PR merge `504cf6e34d53a4bfb58b7a2c2f9d78def7efa34d`.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting `develop`.

No downloaded installer, foreground native application, clipboard, Office instance or certificate-store test ran locally in this batch. Actual desktop/installation/trust checks run only on disposable hosted Windows. No merge, public release, tag change or production identity provisioning occurred.

## Remaining acceptance

Production is not ready. Required external evidence includes SDK-free clean Windows installation; physical screen-reader, DPI and multi-monitor behaviour; live Azure DevOps attachment destinations; real historical schema fixtures; physical power-loss/removable-drive failures; and an authorised production signing identity with a positive signed upgrade. Compatibility and responsiveness limits above remain engineering work. Hosted runtime-origin checks and green aggregate CI are not substitutes for those requirements.
