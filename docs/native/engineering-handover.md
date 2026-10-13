# Native Windows engineering handover

**Internal preview; production acceptance remains incomplete.** The latest verified package is 0.1.1-preview.246, built from clean PR merge `504cf6e34d53a4bfb58b7a2c2f9d78def7efa34d` for branch head `2af4471fcdd982fa4888b025bd1ad08f058d91c7`. Both native and both web CI runs pass on their first attempts. The remaining requirements below are open acceptance items, not passed checks.

## Architecture and implemented workflows

The separate `src/native` solution uses C#, .NET 10, WPF/XAML, CommunityToolkit.Mvvm, AvalonEdit, Markdig, Mermaider, SharpVectors, Open XML SDK and PDFsharp/MigraDoc. Core owns domain/parsing, filesystem, imports/exports, state and update policy; Native owns Windows services and presentation. Editing, preview and diagrams do not host a browser or JavaScript runtime. The buildless web/PWA/GitHub Pages application and legacy WinUI/WebView2 shell remain separate.

Implemented workflows cover portable UTF-8 documents and workspace assets; folders, tabs, recent items, outline and context-sensitive search; formatting, completion, source navigation and history; guarded saves, external-change conflicts, snapshots and independent recovery; native Markdown/Mermaid/maths rendering; images, clipboard formats, supported DOCX/HTML/PDF imports and DOCX/PDF/HTML/ZIP/Docs Site exports; content/template libraries, generators, profiles, settings transfer and explicit legacy migration. Optional artefact metadata remains passive.

Windows improvements include native pickers and Explorer actions, filesystem monitoring, reviewed link-preserving moves, selectable preview text, paginated reading, native accessibility metadata and restored workspaces. Six persisted diagram palettes remain independent of application appearance. Failed diagrams expose native source-copy and edit actions. See the [parity matrix](functional-parity-matrix.md), [experience improvements](native-experience-improvements.md) and [architecture decisions](architecture-decisions.md) for feature-level contracts and limits.

## Current verification

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
