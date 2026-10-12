# Native Windows engineering handover

Current renderer hardening: inline trees and coloured code now share the preview's input/cancellation budget. Four new cases reproduce and correct blocked input and ignored cancellation within individual rich paragraphs/headings/table cells, preserving complete text, formatting and source mapping. Focused/full native validation, Release and static checks pass; its CI is pending. The earlier details cold-start observation is separate and remains unexplained.

Status: available for internal preview with documented limitations. Both native and both web CI runs pass at 300ab68, including all 812 core tests, 14 mocked signing-adapter outcomes, bounded session persistence, retained-session reopening and actual interruption recovery in built/reinstalled applications. The earlier c62ec4e PR details-input failure remains unexplained. The user confirms no production certificate, service or publisher identity is configured; unsigned update execution remains blocked. Production certification is incomplete. This report does not authorise installation on the user's active desktop, merging, release publication or tag changes.

Automatic reading now uses native pages for long documents, with explicit Continuous/Pages choices, asynchronous distant navigation, preserved text automation and page-sized media. All 800 core tests and seven focused hidden-HWND pagination outcomes pass. The 57th desktop workflow checks page buttons, distant Find range/bounds, complete Undo and saved reading preferences; it passes in all four application contexts. A subsequent screenshot inspection identified missing visible selection, corrected and verified by actual pixel comparison while keyboard focus stays in Find. Portable appearance exports use version 1.1 while retaining version 1.0 import compatibility.

## Architecture and delivered workflows

The separate src/native solution uses .NET 10/WPF, AvalonEdit, Markdig, Mermaider, native Windows rendering, Open XML SDK and PDFsharp/MigraDoc. Core owns portable parsing, document/filesystem operations, state and update policy; Native owns WPF controls, Windows integration and export preparation. Native editing and preview require no browser, JavaScript runtime, Node process or local server. The buildless web app, GitHub Pages and legacy WinUI/WebView2 shell remain separate.

Implemented workflows include:

- Portable UTF-8 documents, native folders/tabs, recent workspaces, source-aware navigation, outline/search and document history.
- Guarded saves, external-change conflicts, snapshots, independent recovery, reviewed file/folder moves, case-only renames and portable images/links.
- Markdown editing, bounded code colouring/completion, native Mermaid preview/zoom/export, maths, images/captions, callouts, details, anchors, progress and review helpers.
- Windows rich clipboard/paste, supported DOCX/HTML/PDF imports, HTML/DOCX/PDF exports, Markdown bundles, Docs Sites and explicit artefact review packs.
- Content/Word libraries, generators, export profiles, appearance transfer and explicit browser-library migration. Optional artefact metadata remains passive and session-only.

Native advantages include filesystem change monitoring, Windows pickers and shell actions, reviewed source-preserving moves, keyboard editing and context commands, independent recovery, native image handling, selectable preview content and Windows accessibility exposure. The [experience report](native-experience-improvements.md) links each improvement to its validation.

## Parity and compatibility

The [parity matrix](functional-parity-matrix.md) records implementation, tested outcomes and limitations per workflow. Essential editing, file safety, rendering and export paths have automated coverage; broad parity is not declared complete.

The [diagram compatibility report](diagram-compatibility.md) records supported Mermaid forms and explicit unsupported input. The corpus contains 40 cases. Corrected background/label contrast and supported sequence activations preserve source. Unsupported forms produce a readable error/source fallback rather than claiming browser Mermaid equivalence.

Exports retain editable or selectable content where supported. Code colours, Unicode, spacing, links, image budgets and safe metadata have focused checks. Revision 230852b adds checked PDF font fallback and repairs supplementary-character copy mappings in bounded, self-generated PDFs. Six local outcomes pass; emoji/CJK/combined rasters were inspected. Missing glyphs fail explicitly before output bytes reach the file store. PDF strike-through and neutral highlighting now retain selectable text, wrapped geometry and real links across 17 pages in six local outcomes; actual Export-menu validation passes in all four application contexts of both completed native runs, and the resulting PDF was inspected. Complex-script shaping, bidirectional layout, joined/colour emoji and broader Word/template fidelity remain limited. Native Word package/schema tests do not establish how every destination application renders a document.

## Installer and update status

An additional installed-session check targets the actual state directory created before upgrade, then repeats after reinstall. It verifies every restored tab, native settings and semantic session continuity, with source/private-file hashes retained; normal viewport/window and backup changes are explicitly accounted for. It passes in both 41d06d7 native runs (38254608535/38254600748). The earlier lifecycle hash checks did not establish that later application versions read the retained session.

The self-contained x64 Inno installer and portable ZIP use distinct product, installation, state and association identities. Uninstall preserves user workspaces/state. CI exercises installation, a newer preview version, uninstall and reinstall; it compares binary/registration metadata and retained state hashes. Both preview versions come from the same source revision, so this is not historical schema migration. The established lifecycle uses silent mode. A new guarded CI workflow now implements visible wizard keyboard navigation, optional choices, cancellation, installation/Finish and cleanup; it builds cleanly. Its first actual runs failed at the welcome page because the test expected a legacy arrow suffix absent from Inno 6.7.1. After correcting button labels and explicit Yes activation, all four wizard outcomes pass in both 2c16d47 native runs. Welcome, tasks, Ready, confirmation and Finish screenshots were inspected.

Update policy checks compatible releases, signed metadata, size/hash, actual Windows Authenticode/revocation and exact publisher identity. Users choose download and installation separately. Production trust is intentionally unconfigured. Seven isolated signature cases and seven combined discovery/download/final-verification cases pass with disposable CI trust; no downloaded fixture is executed and test certificates/private keys are removed. Production issuer/timestamp/revocation and a positive signed-installer upgrade require separate certification. See [update and packaging](update-and-packaging.md).

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Local Release | Current inline-rendering change: zero warnings/errors |
| Core tests | 812 passed locally and in both 300ab68 native runs; artifacts/native/tests/native-session-all.trx |
| Inline responsiveness | Four before/after cases; artifacts/native/inline-layout and validation/native-inline-layout.json. Local full harness passes; renderer CI pending |
| Native pagination | Seven hidden-HWND outcomes; artifacts/native/pagination and validation/native-pagination.json; 58-workflow desktop suite passes both completed b1b500c native runs |
| Full native harness | 40 diagram cases and export checks, zero failures; artifacts/native/validation |
| PDF Unicode | Six outcomes pass; artifacts/native/pdf-unicode/native-pdf-unicode.json and inspected PNG |
| Export browser checks | 12 Chromium/Edge cases from disk and static hosting, including theme/print/forced colours; both 300ab68 native CI runs pass |
| Complete native PR/push | 300ab68: 38264945184 / 38264935943; core/signing/native/export/desktop suites, visible installer and retained/interrupted-session checks |
| Complete web PR/push | 300ab68: 38264945154 / 38264935984; 262 cases each |
| Actual PDF export | Expanded Windows Export-menu Unicode check passes in all four native contexts; built-window screenshot and exported PDF raster inspected |
| Downloaded current CI evidence | artifacts/native/ci-b1b500c-push; visible search highlight, actual decorated PDF, 58 desktop workflows, certificate cleanup, preview.179 upgrade with 20 preserved state files and package SHA-256 checks confirmed |
| Package advisories | Fresh-cache NuGet query on 12 October 2026 reports no known vulnerabilities among 37 resolved versions, including transitive dependencies; inventory and lock hashes retained |
| Scoped security review | Sealed sequential review at b45246e; two low-severity, user-assisted allocation findings subsequently corrected with bounded regression fixtures |

The [validation report](validation-report.md) retains exact histories, failed/cancelled runs and limits. Local checks use hidden/windowless native fixtures. Actual keyboard, Windows clipboard, pickers, installer execution and certificate changes run only in isolated Windows CI. Earlier real Word/Excel checks used Office 16.0.20430.20092; new syntax-colour destination rendering has not been repeated in Office.

Performance evidence is fixture-specific. Earlier actual application samples observed 2.63 seconds launch-to-preview, 636 ms keyboard delivery/observation, 103 ms save and 124 ms small-image paste, including UIA polling overhead. A 1,001-file/4.14 MB fixture measured enumeration at 53–64 ms, reads at 5.6–11.3 ms, parsing at 180–517 ms and editor insertion/layout at 0.3–1.6 ms. Chunked native construction yields to input, and automatic pagination now bounds initial visible layout. Product hidden-HWND samples for 4,000 items observed roughly 52–144 ms first input after attachment and 0.7–0.8 seconds distant navigation while input continued. Continuous/individual-page layout remains synchronous; oversized blocks and physical DPI certification remain open. These are not universal latency or leak guarantees.

## Build artefacts and references

The newest inspected packages are under artifacts/native/ci-8308c5d-push/packages/0.1.0-preview.195 and 0.1.1-preview.195. All four hashes match; version metadata records exact source 8308c5dad1af21da02b924d8b984bf2fb5043c2d, a clean tree, self-contained internal preview and signingProvider Unsigned. Windows reports NotSigned for both installers; both update manifests have no signing key/signature. The 0.1.1 installer SHA-256 is FC7B999D7923647A4AA0499C9D169DDDC9D84E562782BEAE54B1D0C4EE041AE9; its ZIP is A01AE49F4E3CB93846CBB823A4AD4524E98A0AC55630F15973DC90F13870B308. package-verification.json retains the exact read-only checks; neither installer was executed locally. These packages precede the session/inline fixes now under validation.

Build from the repository root with dotnet build src/native/LensDocsStudio.Native.slnx -c Release. Packaging uses scripts/native/Build-NativePackage.ps1 with a fresh preview version and an installed Inno Setup compiler. Packaging requires development tools; normal self-contained execution does not use a developer build command.

The inspected push artefact contains matching installers, portable ZIPs, version metadata, checksums and unsigned update metadata under artifacts/native/ci-b1b500c-push/packages/0.1.0-preview.179 and 0.1.1-preview.179. Metadata records exact source revision b1b500cc5c5258a2bcf6bc663e359fc787636757, sourceDirty false, selfContained true and signingRequested false. Do not run downloaded installers on the active desktop as part of these checks.

The inspected 0.1.1-preview.179 installer SHA-256 is BF4C8168D8EDF71CF4EADAE748521CB2BE0E121AB35977ED3990301606B0548E; the portable ZIP is 83B9CBCD998007CA4A0A6E308C2BC2AC091DADEFC6E233AEAE7AF226E80C93C0.

- Branch: feature/native-wpf-rebuild.
- Latest fully CI-validated commit: 300ab68, including bounded session persistence, optional signing adapters, actual interruption recovery and installed-session continuity. The inline-rendering follow-up is described above.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop.
- [Native PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38264945184).
- [Web PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38264945154).

## Remaining acceptance

Internal preview use must account for the documented compatibility/performance limits. Production is not ready. Required external evidence includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachment interoperability, removable-drive/crash interruption testing and authorised production signing/update certification. Historical schema migration needs a real earlier state/schema fixture. Broader supported export/diagram fidelity and large-document responsiveness remain engineering acceptance areas.

No merge, public release or tag change has occurred. No production certificate or signing identity has been provisioned.
