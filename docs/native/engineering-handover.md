# Native Windows engineering handover

Newest explicitly verified packages: artifacts/native/ci-a3ef323-push/packages/0.1.1-preview.207 (installer and portable ZIP), with the initial 0.1.0 version alongside. Metadata identifies clean a3ef323 source; hashes, runtime files and unsigned status are verified in package-verification.json. Its successful push covers 59 desktop workflows in four contexts, themes, state restoration and the complete installer lifecycle. Four original Forest screenshots contain complete labels/nodes. The stronger pixel-based desktop capture gate is locally validated and awaits CI. The paired PR's 583 ms closed-details pause remains an unresolved acceptance issue; these packages are internal previews, not production-certified, and were not launched locally.

Diagram themes now offer six saved native choices with independent application appearance and matching image pixels through all rich export paths. All 832 core tests, twelve native theme combinations, the complete native harness and static checks pass locally. Settings transfer 1.2 retains 1.0/1.1 imports. Desktop menu/restart checks, the stricter zoom-window lookup and the complete installer lifecycle pass in native push 38277636125; both web runs pass. Native PR 38277642233 repeats the closed-details first-input failure at 583 ms. The follow-up adds opt-in construction phases and maximum-input-gap checks, validated locally without claiming a latency fix; exact outcomes are retained in the validation report.

Current renderer correction: d692aeb native PR/push 38270165917/38270156848 fail the table-cell input-gap assertion at 426/342 ms; both web runs pass. Empty WPF containers are now attached before cooperative population to avoid moving complete rich subtrees. Nine focused cases, Release (zero warnings/errors), full native integration and static checks pass locally; both native and both web c64f410 CI runs pass. Original failure artefacts and the unchanged 250 ms limit are retained. Existing details cold-start and later expansion latency are not claimed resolved.

Current update hardening: channel changes cancel active operations and reject stale downloads or installation requests. Six isolated interaction cases and 36 update core cases pass; all 814 core tests, Release, full native integration and static checks pass locally. All six interactions also pass in both d692aeb CI artefacts. Interaction fixtures use inert bytes and mocked installer verification, leave embedded trust unchanged and launch no installer.

Status: available for internal preview with documented limitations. Both native and both web CI runs pass at c64f410, including all 814 core tests, nine dense-inline construction/cancellation cases, six update-selection interaction cases, 14 mocked signing-adapter outcomes, bounded session persistence, retained-session reopening and actual interruption recovery in built/reinstalled applications. The earlier c62ec4e PR details-input failure remains unexplained. The user confirms no production certificate, service or publisher identity is configured; unsigned update execution remains blocked. Production certification is incomplete. This report does not authorise installation on the user's active desktop, merging, release publication or tag changes.

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
| Local Release | Current renderer/update changes: zero warnings/errors |
| Core tests | 814 passed locally and in both c64f410 native runs; artifacts/native/tests/native-update-interactions-all.trx and ci-c64f410-push/tests/native-core.trx |
| Update interactions | Six view-model/service cases, including four reproduced failures and valid/unsigned controls; artifacts/native/update-interactions. Mocked installer verification; no launch |
| Inline responsiveness | Nine cases pass in both c64f410 native artefacts; table gaps 37.1/33.6 ms and overall maxima 50.1/51.0 ms. Earlier failed observations remain retained |
| Native pagination | Seven hidden-HWND outcomes; artifacts/native/pagination and validation/native-pagination.json; 58-workflow desktop suite passes both completed b1b500c native runs |
| Full native harness | 40 diagram cases and export checks, zero failures; artifacts/native/validation |
| PDF Unicode | Six outcomes pass; artifacts/native/pdf-unicode/native-pdf-unicode.json and inspected PNG |
| Export browser checks | 12 Chromium/Edge cases from disk and static hosting, including theme/print/forced colours; both 300ab68 native CI runs pass |
| Complete native PR/push | c64f410: 38272377336 / 38272371722; core/signing/native/export/desktop suites, visible installer and retained/interrupted-session checks |
| Complete web PR/push | c64f410: 38272377357 / 38272371725; 262 cases each |
| Actual PDF export | Expanded Windows Export-menu Unicode check passes in all four native contexts; built-window screenshot and exported PDF raster inspected |
| Downloaded reviewed CI evidence | artifacts/native/ci-300ab68-push; both actual interrupted-save reports pass, upgrade/reinstall restore ten documents and preserve 16 private/ten source files, both preview.197 packages match their hashes. Restored/recovered screenshots inspected; see evidence-review.json |
| Package advisories | Fresh-cache NuGet query on 12 October 2026 reports no known vulnerabilities among 37 resolved versions, including transitive dependencies; inventory and lock hashes retained |
| Scoped security review | Sealed sequential review at b45246e; two low-severity, user-assisted allocation findings subsequently corrected with bounded regression fixtures |

The [validation report](validation-report.md) retains exact histories, failed/cancelled runs and limits. Local checks use hidden/windowless native fixtures. Actual keyboard, Windows clipboard, pickers, installer execution and certificate changes run only in isolated Windows CI. Earlier real Word/Excel checks used Office 16.0.20430.20092; new syntax-colour destination rendering has not been repeated in Office.

Performance evidence is fixture-specific. Earlier actual application samples observed 2.63 seconds launch-to-preview, 636 ms keyboard delivery/observation, 103 ms save and 124 ms small-image paste, including UIA polling overhead. A 1,001-file/4.14 MB fixture measured enumeration at 53–64 ms, reads at 5.6–11.3 ms, parsing at 180–517 ms and editor insertion/layout at 0.3–1.6 ms. Chunked native construction yields to input, and automatic pagination now bounds initial visible layout. Product hidden-HWND samples for 4,000 items observed roughly 52–144 ms first input after attachment and 0.7–0.8 seconds distant navigation while input continued. Continuous/individual-page layout remains synchronous; oversized blocks and physical DPI certification remain open. These are not universal latency or leak guarantees.

## Build artefacts and references

Newest verified packages: artifacts/native/ci-c64f410-push/packages/0.1.0-preview.203 and 0.1.1-preview.203. Both installers and ZIPs match SHA-256 records; metadata identifies clean source c64f41057ef3b13d00ca302540d1335740e7bee1, self-contained internal previews and Unsigned signing. Windows reports NotSigned for both installers, update envelopes have empty key/signature fields, and each payload matches its actual installer bytes. ZIP inspection confirms bundled coreclr, hostfxr, WPF and apphost files. No package was executed locally. The 0.1.1 installer SHA-256 is 235A0595532270F1DDD9E355F238ADBDE6F3962A2F6DCD6C9923DE460B94596C; its ZIP is 0925613001D08BB6DCB8B425EBD515945508FAD51988760CF97768D6046A994C. Exact checks are in package-verification.json.

Previously inspected packages are under artifacts/native/ci-300ab68-push/packages/0.1.0-preview.197 and 0.1.1-preview.197. All four hashes match; version metadata records exact source 300ab68532173f82afb2e67ccc6746508323d8db, a clean tree, self-contained internal preview and signingProvider Unsigned. Windows reports NotSigned for both installers; both update manifests have no signing key/signature. The 0.1.1 installer SHA-256 is AB4BC908DAF650D31895994CBB98DDD990D1081CAF6516B8F06FBBA4BE68A9AB; its ZIP is B4255D3513E0B1205862C172917354106372210B85CC7AA16428ED9E1D43BFDD. package-verification.json retains the exact read-only checks; neither installer was executed locally. These packages include bounded session persistence and precede the inline/update-interaction fixes.

Build from the repository root with dotnet build src/native/LensDocsStudio.Native.slnx -c Release. Packaging uses scripts/native/Build-NativePackage.ps1 with a fresh preview version and an installed Inno Setup compiler. Packaging requires development tools; normal self-contained execution does not use a developer build command.

The inspected push artefact contains matching installers, portable ZIPs, version metadata, checksums and unsigned update metadata under artifacts/native/ci-b1b500c-push/packages/0.1.0-preview.179 and 0.1.1-preview.179. Metadata records exact source revision b1b500cc5c5258a2bcf6bc663e359fc787636757, sourceDirty false, selfContained true and signingRequested false. Do not run downloaded installers on the active desktop as part of these checks.

The inspected 0.1.1-preview.179 installer SHA-256 is BF4C8168D8EDF71CF4EADAE748521CB2BE0E121AB35977ED3990301606B0548E; the portable ZIP is 83B9CBCD998007CA4A0A6E308C2BC2AC091DADEFC6E233AEAE7AF226E80C93C0.

- Branch: feature/native-wpf-rebuild.
- Latest fully CI-validated implementation: c64f410, including parent-first rich rendering, stale-update rejection, bounded session persistence, optional signing adapters and actual interruption recovery.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop.
- [Native PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38272377336).
- [Web PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38272377357).

## Remaining acceptance

Internal preview use must account for the documented compatibility/performance limits. Production is not ready. Required external evidence includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachment interoperability, removable-drive/crash interruption testing and authorised production signing/update certification. Historical schema migration needs a real earlier state/schema fixture. Broader supported export/diagram fidelity and large-document responsiveness remain engineering acceptance areas.

No merge, public release or tag change has occurred. No production certificate or signing identity has been provisioned.
