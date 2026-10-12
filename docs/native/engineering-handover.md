# Native Windows engineering handover

The latest verified internal-preview packages are `artifacts/native/ci-f86170d-pr/packages/0.1.1-preview.244`, with `0.1.0-preview.244` alongside. Both native and both web f86170d runs pass on their first attempts, including 912 core tests, 62 desktop checks in four contexts and the complete installer lifecycle. Packages identify exact clean PR merge 14bdb06 and remain unsigned; push preview.243 independently matches clean f86170d. Supported Word styles, code, quotes and the CRLF correction pass in both native runs. The later details-template mitigation and keyboard check need their own CI. Production acceptance remains incomplete.

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

The subsequent details-template mitigation passes the full local native harness and focused native header checks; final light/dark pages are byte-identical to the preceding render. Three fresh process samples pass all 12 nested layout cases, maximum input gap 63.34 ms. Evidence is in `details-memory-template-validation`, `details-memory-template-final` and `details-memory-template-samples.json`. The fixed template cache is per WPF thread and accepts no document text. A hosted Space-key check is added to the existing details workflow, preserving the 62-check count and recording `native-details-keyboard.json` in every context. That keyboard check and the mitigation's effect on intermittent CI delays require hosted evidence; do not claim either from the local samples.

The inherited-numbering batch passes all 912 core tests, the full native harness and a separate CRLF Word fixture, with zero Release warnings/errors and passing static checks. Fourteen new style-list cases reproduce/fix lost markers and hierarchy; one extra code case verifies CRLF conversion. The native report `word-style-lists-full-validation/native-word-style-list-import.json` checks 12 inherited items, six lists and exact Markdown including code/quotes/continuations. Both f86170d hosted runs and downloaded reports pass `-ExpectedCoreTests 912 -RequireWordStyles` with the previous PDF/Word switches. Source-only/field markers and full destination layout remain limited.

The preceding 4adb0ad native runs failed a CRLF-versus-LF Word assertion, now reproduced and corrected. The push also retained a 728.273 ms details input delay, concentrated inside toggle initialisation with no sampled CPU increase. Failures are under `ci-4adb0ad-pr-failure` and `ci-4adb0ad-push-failure`; no timing bound is relaxed and no root cause is claimed. The later f86170d runs pass; preview.244 is now verified, without the subsequent template mitigation.

The later code/quote import batch passes all 897 core tests (14 new cases), the full native harness, static checks and a zero-warning/error Release build. Recognised styles, bounded inheritance and direct outline overrides retain supported block semantics; code is literal, quotes stay grouped and list placement survives. Evidence is under `artifacts/native/word-blocks-after`, `word-blocks-full-validation` and `word-blocks-native`, with seven original failures retained in `word-blocks-before`. The native block report verifies exact code text and one two-paragraph quote within list items. After correcting the CRLF probe, both f86170d hosted runs pass the block checks within 912 core tests. Language, nested quote depth, quote/list orientation and ambiguous template styles remain limited.

The later Word hyperlink import correction passes 883 core tests (15 new link cases), the complete native harness, static checks and a zero-warning/error Release build. It retains supported external links, formatted labels, linked image bytes and paired internal bookmarks; missing/unsupported targets and inert fields have explicit warnings. Native output is in `artifacts/native/word-links-final-validation`; core results are in `word-links-after/word-links-final.trx`, with eight original failures retained in `word-links-before`. Both 0d9d54f native CI runs also pass these 883 tests and the full native harness. File/relative targets, field-based hyperlinks and full Word formatting remain incomplete.

The Word list/heading reimport correction passes 868 core tests (18 new cases) and the full native harness in both 4cbce1d hosted runs. It preserves direct list starts/levels/restarts/continuations, separate numbering instances, empty/first-block items and exact heading levels. Six actual fixture lists retain 12 items and six continuations; native diagram/formula image bytes are unchanged after import. Original local evidence remains in `artifacts/native/word-import-heading-validation` and `word-import-after/word-import-heading-final.trx`. Style-inherited numbering remains limited; do not claim complete Word round-trip fidelity.

The Word export list correction passes 850 core tests and the complete native harness in both 6e572f6 hosted runs. It fixes repeated continuation numbering, missing first-block markers and omitted empty items while preserving editable native numbering, styles and original image bytes. It bounds images/tables and respects direct template cell margins; code preflight includes list formatting. Eleven core regressions and actual native Mermaid/formula DOCX preparation pass. `artifacts/native/word-list-width-validation` retains the original local files and report; both downloaded 6e572f6 evidence sets contain their own generated DOCX files. Actual Word layout/accessibility remains unverified.

The PDF list correction passes both b3a4d59 native runs: one marker per item, nested/continuation alignment, zero/nine-digit starts, formatting/links and table/image bounds. Ninety continuation paragraphs retain exact selectable word order over five pages. All seven hosted push pages were inspected, including a higher-resolution review of page two. Evidence is in `artifacts/native/ci-b3a4d59-push` and `ci-b3a4d59-pr`; both package sets are independently verified and the correction is included in preview.234.

The [parity matrix](functional-parity-matrix.md) distinguishes implemented, tested and unverified behaviour. The [diagram report](diagram-compatibility.md) records 40 corpus cases and another 56 catalogue/theme cases; unsupported syntax produces an explicit explanation. Native palette equivalents do not imply browser Mermaid pixel identity.

Exports preserve editable/selectable text where supported, with checked Unicode font fallback, code colours, links and wrapped strike-through/highlighting. Missing PDF glyphs fail before output is committed. Complex-script shaping, bidirectional text, joined/colour emoji and broader Word/template fidelity remain limited. Package/schema validation cannot certify every destination application's rendering.

## Installation and update trust

Hosted validation confirms installed, upgraded and reinstalled applications load coreclr.dll and wpfgfx_cor3.dll from their own installation directory. Actual executable/module paths are retained in native-ui-runtime.json; the built-app context records its framework runtime. The four contexts and independent package inspection pass in the downloaded 56502e3 PR evidence. This check does not replace SDK-free clean-machine certification.

The self-contained x64 Inno installer and portable ZIP use separate product, state and association identities. CI covers visible wizard choices, Back/cancellation, installation, newer-preview replacement, uninstall/reinstall and reopening retained sessions. Twenty state files and workspace source are preserved. Both preview versions use the same source revision; this does not certify historical schema migration or an SDK-free machine.

Signing is pluggable through an existing certificate or service adapter, with independently verified publisher pins. The user has configured neither identity nor provider. Both preview.244 installers report `NotSigned`, update envelopes contain no identity/signature, and unsigned updates cannot execute. Update operations cancel/reject stale selections and verify signed metadata, size/hash, Windows Authenticode and exact publisher before an authorised launch. Disposable CI certificates test trust paths and are removed afterwards; production trust remains empty.

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Current local Release | Zero warnings/errors |
| Current core tests | 912 passed; `artifacts/native/word-style-lists-after/word-style-lists-final.trx` |
| Failed-diagram recovery | 14 light/dark native cases; exact copy/source mapping, accessible controls and valid-render control; `artifacts/native/diagram-failures` |
| Full local native harness | 40 diagram/export cases, zero failures; `artifacts/native/word-style-lists-full-validation` |
| Current static checks | Passed; web source unchanged |
| Native CI at f86170d | PR 38319856432 and push 38319852791 pass on attempt 1; both downloaded reports verify 912 core tests, all PDF/Word groups and 62 desktop workflows in all four contexts |
| Web CI at f86170d | Push 38319852885 and PR 38319856463 pass all 262 cases on their first attempts |
| Diagram paint gate | Eight light/dark frames in the four desktop contexts passed actual visible-node/label checks |
| Current desktop addition | Recovery clipboard, keyboard focus/caret, selection collapse, Undo and saved-file preservation pass in all four contexts |
| Diagram copying | Eight copies retain identical 471 × 149 pixels at 175%/35% zoom, persist after closing the drawing and preserve source; reinstalled PNG inspected |
| Package verification | All eight preview.243/.244 hashes, exact clean revisions, runtime ZIP entries and unsigned metadata verified; `ci-f86170d-pr/package-verification.json` and push counterpart |
| Loaded runtime | All four executable identities and both loaded modules match; installed/upgraded/reinstalled modules originate in the installation directory; `ci-f86170d-pr/runtime-evidence-review.json` and push counterpart |
| Push retry verification | Preview.229 independently matches clean cfe11e5; downloaded core/corpus, four desktop/clipboard contexts, lifecycle and trust evidence pass; `ci-cfe11e5-push-attempt2/evidence-review.json` |
| Installer files in use | All four preservation assertions and five wizard outcomes pass; exact snapshots retained and compared; `ci-cfe11e5-pr/evidence-review.json` |
| Local test isolation | Five desktop/Office commands and installer script refuse before arguments/paths; `artifacts/native/desktop-test-guards.json` |
| Dependency review | Fresh-cache query on 12 October 2026 found no listed vulnerabilities among 37 resolved versions; point-in-time result |
| Scoped security review | Sealed b45246e..cf436b0 review 475201d4-5562-4ffa-bc7a-166818c61152: 101 inventory plus six supplemental files, one Low Word code-allocation finding, one rejected colour hypothesis. The later fix is tested separately, outside that immutable review |

The [validation report](validation-report.md) retains prior failures, evidence and scope. Actual keyboard/clipboard, installer and certificate tests run only in isolated Windows CI. Local checks use windowless/hidden native fixtures; downloaded installers were not launched locally.

Responsiveness remains incomplete: the earlier a3ef323 PR measured a 583 ms closed-details initial pause and a later local table sample reached 360.6 ms. The cfe11e5 PR sample passes at 54.676 ms first input/maximum gap and 117.8524 ms total construction, but does not establish the intermittent cause. The 250 ms threshold is unchanged. Later expansion/collapse is corrected: all 18 light/dark transitions pass with maximum input 66.335 ms, complete content and formatting. Three further local process samples also pass without reproducing the earlier pause. Broader continuous/page layout still needs evidence. Actual process interruption recovery passes; physical power loss and removable-drive failure remain separate.

## Artefacts and references

Build with `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`. Packaging uses `scripts/native/Build-NativePackage.ps1` and Inno Setup; normal self-contained execution does not require build tools.

The latest verified installer is `artifacts/native/ci-f86170d-pr/packages/0.1.1-preview.244/LensDocsStudio.Native-0.1.1-preview.244-win-x64-Setup.exe` (58,860,041 bytes; SHA-256 `6B91C0A18887BF6084287F733FA39F5DE27D8BD25026B830C438E48D8DBB711F`). Its portable ZIP hash is `C81FB2E847A04C3CD79A544FF528895215182B8C56012729A3565880FBEE566D`.

- Branch: `feature/native-wpf-rebuild`.
- Latest package-validated revision: PR merge `14bdb064fc09ccee80b8a875a0d081a6af92b289` for branch head `f86170d5ab4866e858996e58f0b6561caf7db459`.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting `develop`.
- [Native PR validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38319856432).
- [Web push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38319852885).

## Remaining acceptance

Production is not ready. External evidence still includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachments, physical power-loss/removable-drive behaviour, a real historical schema fixture and an authorised production signing/update identity. Compatibility and large-document responsiveness limits remain explicit engineering work. No merge, public release, tag change or production identity provisioning has occurred.
