# Native Windows engineering handover

Status: available for internal preview with documented limitations. The pagination update passes local validation and awaits Windows/web CI; the last fully inspected product evidence is revision 230852b. Production certification is incomplete. This report distinguishes implementation from evidence and does not authorise installation on the user's active desktop, merging, release publication or tag changes.

Automatic reading now uses native pages for long documents, with explicit Continuous/Pages choices, asynchronous distant navigation, preserved text automation and page-sized media. All 800 core tests and seven focused hidden-HWND pagination outcomes pass. The new desktop workflow checks page buttons, distant Find selection, complete Undo and saved reading preferences; it awaits isolated CI. Portable appearance exports use version 1.1 while retaining version 1.0 import compatibility.

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

Exports retain editable or selectable content where supported. Code colours, Unicode, spacing, links, image budgets and safe metadata have focused checks. Revision 230852b adds checked PDF font fallback and repairs supplementary-character copy mappings in bounded, self-generated PDFs. Six local outcomes pass; emoji/CJK/combined rasters were inspected. Missing glyphs fail explicitly before output bytes reach the file store. Complex-script shaping, bidirectional layout, joined/colour emoji, PDF strikeout/highlight and broader Word/template fidelity remain limited. Native Word package/schema tests do not establish how every destination application renders a document.

## Installer and update status

The self-contained x64 Inno installer and portable ZIP use distinct product, installation, state and association identities. Uninstall preserves user workspaces/state. CI exercises installation, a newer preview version, uninstall and reinstall; it compares binary/registration metadata and retained state hashes. Both preview versions come from the same source revision, so this is not historical schema migration.

Update policy checks compatible releases, signed metadata, size/hash, actual Windows Authenticode/revocation and exact publisher identity. Users choose download and installation separately. Production trust is intentionally unconfigured. Seven isolated signature cases and seven combined discovery/download/final-verification cases pass with disposable CI trust; no downloaded fixture is executed and test certificates/private keys are removed. Production issuer/timestamp/revocation and a positive signed-installer upgrade require separate certification. See [update and packaging](update-and-packaging.md).

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Local pagination Release | Zero warnings/errors |
| Core tests | 800 passed; artifacts/native/tests/native-pagination.trx |
| Native pagination | Seven hidden-HWND outcomes; artifacts/native/pagination and validation/native-pagination.json; expanded 57-workflow desktop suite pending CI |
| Full native harness | 40 diagram cases and export checks, zero failures; artifacts/native/validation |
| PDF Unicode | Six outcomes pass; artifacts/native/pdf-unicode/native-pdf-unicode.json and inspected PNG |
| Export browser checks | 12 Chromium/Edge cases from disk and static hosting, including theme/print/forced colours; latest completed 676523e CI also passes |
| Complete native PR/push | 230852b: 38230558112 / 38230551640; 791 core, seven signature, seven combined update, 12 export-browser and 56 desktop workflows in built/installed/upgraded/reinstalled apps |
| Complete web PR/push | 230852b: 38230558094 / 38230551635; 262 cases each in 10.7 minutes |
| Actual PDF export | Expanded Windows Export-menu Unicode check passes in all four native contexts; built-window screenshot and exported PDF raster inspected |
| Downloaded current CI evidence | artifacts/native/ci-230852b-push; all six PDF outcomes, retained CJK/Arabic/Hebrew characters, certificate cleanup and simulated upgrade with 20 preserved state files confirmed |
| Scoped security review | Sealed sequential review at b45246e; two low-severity, user-assisted allocation findings subsequently corrected with bounded regression fixtures |

The [validation report](validation-report.md) retains exact histories, failed/cancelled runs and limits. Local checks use hidden/windowless native fixtures. Actual keyboard, Windows clipboard, pickers, installer execution and certificate changes run only in isolated Windows CI. Earlier real Word/Excel checks used Office 16.0.20430.20092; new syntax-colour destination rendering has not been repeated in Office.

Performance evidence is fixture-specific. Earlier actual application samples observed 2.63 seconds launch-to-preview, 636 ms keyboard delivery/observation, 103 ms save and 124 ms small-image paste, including UIA polling overhead. A 1,001-file/4.14 MB fixture measured enumeration at 53–64 ms, reads at 5.6–11.3 ms, parsing at 180–517 ms and editor insertion/layout at 0.3–1.6 ms. Chunked native construction yields to input, and automatic pagination now bounds initial visible layout. Product hidden-HWND samples for 4,000 items observed roughly 52–144 ms first input after attachment and 0.7–0.8 seconds distant navigation while input continued. Continuous/individual-page layout remains synchronous; oversized blocks and physical DPI certification remain open. These are not universal latency or leak guarantees.

## Build artefacts and references

Build from the repository root with dotnet build src/native/LensDocsStudio.Native.slnx -c Release. Packaging uses scripts/native/Build-NativePackage.ps1 with a fresh preview version and an installed Inno Setup compiler. Packaging requires development tools; normal self-contained execution does not use a developer build command.

The inspected push artefact contains matching installers, portable ZIPs, version metadata, checksums and unsigned update metadata under artifacts/native/ci-230852b-push/packages/0.1.0-preview.171 and 0.1.1-preview.171. Metadata records exact source revision 230852b693f7282ced8d6d0e42606cc059169a2d, sourceDirty false, selfContained true and signingRequested false. Do not run downloaded installers on the active desktop as part of these checks.

- Branch: feature/native-wpf-rebuild.
- Product commit: 230852b, checked PDF Unicode export.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop.
- [Native PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38230558112).
- [Web PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38230558094).

## Remaining acceptance

Internal preview use must account for the documented compatibility/performance limits. Production is not ready. Required external evidence includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachment interoperability, removable-drive/crash interruption testing and authorised production signing/update certification. Historical schema migration needs a real earlier state/schema fixture. Broader supported export/diagram fidelity and large-document responsiveness remain engineering acceptance areas.

No merge, public release or tag change has occurred. No production certificate or signing identity has been provisioned.
