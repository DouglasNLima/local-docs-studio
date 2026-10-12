# Native Windows engineering handover

The latest verified internal-preview packages are `artifacts/native/ci-b3a4d59-pr/packages/0.1.1-preview.234`, with `0.1.0-preview.234` alongside. Both native and both web b3a4d59 runs pass on their first attempts, including 62 desktop checks in four contexts, the complete installer lifecycle and the new PDF list regressions. Packages identify exact clean PR merge cbaf55e and remain unsigned; push preview.233 independently matches clean b3a4d59. The subsequent Word list correction passes 850 core tests and full local native validation and awaits its own hosted run. Production acceptance remains incomplete.

## Architecture and delivered workflows

The separate `src/native` solution uses .NET 10/WPF, AvalonEdit, Markdig, Mermaider, SharpVectors, Open XML SDK and PDFsharp/MigraDoc. Core owns portable parsing, filesystem operations, state and update policy; Native owns WPF controls and Windows integration. Editing and preview use no browser, JavaScript runtime, Node process or local server. The buildless web app, GitHub Pages and legacy WinUI/WebView2 shell remain separate.

Implemented workflows include:

- Portable UTF-8 documents, native folders/tabs, recent workspaces, source navigation, outline/search and document history.
- Guarded saves, external-change conflicts, snapshots, independent recovery, reviewed moves/renames and portable images/links.
- Markdown editing, code colouring/completion, native Mermaid preview/zoom/export, maths, images, callouts, details and review helpers.
- Rich clipboard/paste, supported DOCX/HTML/PDF imports, HTML/DOCX/PDF exports, Markdown bundles, Docs Sites and explicit artefact review packs.
- Content/Word libraries, generators, export profiles, settings transfer and explicit browser-library migration. Optional artefact metadata stays passive.

Native advantages include filesystem monitoring, Windows pickers/shell actions, source-preserving moves, independent recovery, selectable preview text, pagination and native accessibility exposure. Six persisted diagram palettes stay independent of application appearance and carry through rich exports. Settings transfer 1.2 retains 1.0/1.1 imports. The new recovery actions copy failed diagram code or reveal its source in the editor, rejecting stale preview navigation.

## Parity and compatibility

The later Word list correction is locally validated with 850 core tests, the complete native harness, static checks and zero Release warnings/errors. It fixes repeated continuation numbering, missing first-block markers and omitted empty items while preserving editable native numbering, styles and original image bytes. It bounds images/tables and respects direct template cell margins; code preflight includes list formatting. Eleven core regressions and actual native Mermaid/formula DOCX preparation pass. `artifacts/native/word-list-width-validation` retains the two generated DOCX files and report. This change is outside the b3a4d59 PDF CI and awaits its own hosted validation; actual Word layout/accessibility remains unverified.

The PDF list correction passes both b3a4d59 native runs: one marker per item, nested/continuation alignment, zero/nine-digit starts, formatting/links and table/image bounds. Ninety continuation paragraphs retain exact selectable word order over five pages. All seven hosted push pages were inspected, including a higher-resolution review of page two. Evidence is in `artifacts/native/ci-b3a4d59-push` and `ci-b3a4d59-pr`; both package sets are independently verified and the correction is included in preview.234.

The [parity matrix](functional-parity-matrix.md) distinguishes implemented, tested and unverified behaviour. The [diagram report](diagram-compatibility.md) records 40 corpus cases and another 56 catalogue/theme cases; unsupported syntax produces an explicit explanation. Native palette equivalents do not imply browser Mermaid pixel identity.

Exports preserve editable/selectable text where supported, with checked Unicode font fallback, code colours, links and wrapped strike-through/highlighting. Missing PDF glyphs fail before output is committed. Complex-script shaping, bidirectional text, joined/colour emoji and broader Word/template fidelity remain limited. Package/schema validation cannot certify every destination application's rendering.

## Installation and update trust

Hosted validation confirms installed, upgraded and reinstalled applications load coreclr.dll and wpfgfx_cor3.dll from their own installation directory. Actual executable/module paths are retained in native-ui-runtime.json; the built-app context records its framework runtime. The four contexts and independent package inspection pass in the downloaded 56502e3 PR evidence. This check does not replace SDK-free clean-machine certification.

The self-contained x64 Inno installer and portable ZIP use separate product, state and association identities. CI covers visible wizard choices, Back/cancellation, installation, newer-preview replacement, uninstall/reinstall and reopening retained sessions. Twenty state files and workspace source are preserved. Both preview versions use the same source revision; this does not certify historical schema migration or an SDK-free machine.

Signing is pluggable through an existing certificate or service adapter, with independently verified publisher pins. The user has configured neither identity nor provider. Both preview.234 installers report `NotSigned`, update envelopes contain no identity/signature, and unsigned updates cannot execute. Update operations cancel/reject stale selections and verify signed metadata, size/hash, Windows Authenticode and exact publisher before an authorised launch. Disposable CI certificates test trust paths and are removed afterwards; production trust remains empty.

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Current local Release | Zero warnings/errors |
| Current core tests | 850 passed; `artifacts/native/word-lists-after/word-list-widths.trx` |
| Failed-diagram recovery | 14 light/dark native cases; exact copy/source mapping, accessible controls and valid-render control; `artifacts/native/diagram-failures` |
| Full local native harness | 40 diagram/export cases, zero failures; `artifacts/native/word-list-width-validation` |
| Current static checks | Passed; web source unchanged |
| Native CI at b3a4d59 | PR 38310145934 and push 38310140757 pass on attempt 1; both downloaded reports verify 839 core tests, PDF list regressions and 62 desktop workflows in all four contexts |
| Web CI at b3a4d59 | Push 38310140748 and PR 38310145715 pass all 262 cases on their first attempts |
| Diagram paint gate | Eight light/dark frames in the four desktop contexts passed actual visible-node/label checks |
| Current desktop addition | Recovery clipboard, keyboard focus/caret, selection collapse, Undo and saved-file preservation pass in all four contexts |
| Diagram copying | Eight copies retain identical 471 × 149 pixels at 175%/35% zoom, persist after closing the drawing and preserve source; reinstalled PNG inspected |
| Package verification | Four preview.234 hashes, exact clean PR merge revision, runtime ZIP entries and unsigned metadata verified; `ci-b3a4d59-pr/package-verification.json` |
| Loaded runtime | All four executable identities and both loaded modules match; installed/upgraded/reinstalled modules originate in the installation directory; `ci-b3a4d59-pr/runtime-evidence-review.json` |
| Push retry verification | Preview.229 independently matches clean cfe11e5; downloaded core/corpus, four desktop/clipboard contexts, lifecycle and trust evidence pass; `ci-cfe11e5-push-attempt2/evidence-review.json` |
| Installer files in use | All four preservation assertions and five wizard outcomes pass; exact snapshots retained and compared; `ci-cfe11e5-pr/evidence-review.json` |
| Local test isolation | Five desktop/Office commands and installer script refuse before arguments/paths; `artifacts/native/desktop-test-guards.json` |
| Dependency review | Fresh-cache query on 12 October 2026 found no listed vulnerabilities among 37 resolved versions; point-in-time result |
| Scoped security review | Sealed b45246e..cf436b0 review 475201d4-5562-4ffa-bc7a-166818c61152: 101 inventory plus six supplemental files, one Low Word code-allocation finding, one rejected colour hypothesis. The later fix is tested separately, outside that immutable review |

The [validation report](validation-report.md) retains prior failures, evidence and scope. Actual keyboard/clipboard, installer and certificate tests run only in isolated Windows CI. Local checks use windowless/hidden native fixtures; downloaded installers were not launched locally.

Responsiveness remains incomplete: the earlier a3ef323 PR measured a 583 ms closed-details initial pause and a later local table sample reached 360.6 ms. The cfe11e5 PR sample passes at 54.676 ms first input/maximum gap and 117.8524 ms total construction, but does not establish the intermittent cause. The 250 ms threshold is unchanged. Later expansion/collapse is corrected: all 18 light/dark transitions pass with maximum input 66.335 ms, complete content and formatting. Three further local process samples also pass without reproducing the earlier pause. Broader continuous/page layout still needs evidence. Actual process interruption recovery passes; physical power loss and removable-drive failure remain separate.

## Artefacts and references

Build with `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`. Packaging uses `scripts/native/Build-NativePackage.ps1` and Inno Setup; normal self-contained execution does not require build tools.

The latest verified installer is `artifacts/native/ci-b3a4d59-pr/packages/0.1.1-preview.234/LensDocsStudio.Native-0.1.1-preview.234-win-x64-Setup.exe` (SHA-256 `217C355C518303D985086F9974422FCC97D38CF8C5FBC00A7AFA4213CC651D82`). Its portable ZIP hash is `0D2D0D7352CF77C10F4DE3FEF04621CA72C4795130AC9E7575285379143605ED`.

- Branch: `feature/native-wpf-rebuild`.
- Latest package-validated revision: PR merge `cbaf55eea7c5473f76b07df07b5487d8c2493c6a` for branch head `b3a4d59cc6b32dbe201b3109f54994fa49e0cdea`.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting `develop`.
- [Native PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38310145934).
- [Web push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38310140748).

## Remaining acceptance

Production is not ready. External evidence still includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachments, physical power-loss/removable-drive behaviour, a real historical schema fixture and an authorised production signing/update identity. Compatibility and large-document responsiveness limits remain explicit engineering work. No merge, public release, tag change or production identity provisioning has occurred.
