# Native Windows engineering handover

The latest verified internal-preview packages are `artifacts/native/ci-228f17b-push/packages/0.1.1-preview.221`, with the initial `0.1.0-preview.221` version alongside. All four CI runs at 228f17b pass. These packages are unsigned and production certification remains incomplete. These packages include failed-diagram recovery, the corrected editor selection and grouped details expansion, validated in all four desktop contexts. The additional installer-in-use scenario at 6831687 compiles and retains the local execution guard; its new GUI outcomes await CI.

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

The [parity matrix](functional-parity-matrix.md) distinguishes implemented, tested and unverified behaviour. The [diagram report](diagram-compatibility.md) records 40 corpus cases and another 56 catalogue/theme cases; unsupported syntax produces an explicit explanation. Native palette equivalents do not imply browser Mermaid pixel identity.

Exports preserve editable/selectable text where supported, with checked Unicode font fallback, code colours, links and wrapped strike-through/highlighting. Missing PDF glyphs fail before output is committed. Complex-script shaping, bidirectional text, joined/colour emoji and broader Word/template fidelity remain limited. Package/schema validation cannot certify every destination application's rendering.

## Installation and update trust

The self-contained x64 Inno installer and portable ZIP use separate product, state and association identities. CI covers visible wizard choices, Back/cancellation, installation, newer-preview replacement, uninstall/reinstall and reopening retained sessions. Twenty state files and workspace source are preserved. Both preview versions use the same source revision; this does not certify historical schema migration or an SDK-free machine.

Signing is pluggable through an existing certificate or service adapter, with independently verified publisher pins. The user has configured neither identity nor provider. Both preview.221 installers report `NotSigned`, update envelopes contain no identity/signature, and unsigned updates cannot execute. Update operations cancel/reject stale selections and verify signed metadata, size/hash, Windows Authenticode and exact publisher before an authorised launch. Disposable CI certificates test trust paths and are removed afterwards; production trust remains empty.

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Current local Release | Zero warnings/errors |
| Current core tests | 832 passed; `artifacts/native/tests/diagram-recovery.trx` |
| Failed-diagram recovery | 14 light/dark native cases; exact copy/source mapping, accessible controls and valid-render control; `artifacts/native/diagram-failures` |
| Full local native harness | 40 diagram/export cases, zero failures; `artifacts/native/validation` |
| Current static checks | Passed; web source unchanged |
| Native CI at 228f17b | PR 38293103538 and push 38293099393 passed, including 60 desktop workflows in built/installed/upgraded/reinstalled apps |
| Web CI at 228f17b | PR 38293103819 and push 38293099371 passed |
| Diagram paint gate | Eight light/dark frames in the four desktop contexts passed actual visible-node/label checks |
| Current desktop addition | Recovery clipboard, keyboard focus/caret, selection collapse, Undo and saved-file preservation pass in all four contexts |
| Package verification | Four hashes, exact clean source revision, runtime ZIP entries and unsigned metadata verified; `ci-228f17b-push/package-verification.json` |
| Dependency review | Fresh-cache query on 12 October 2026 found no listed vulnerabilities among 37 resolved versions; point-in-time result |
| Scoped security review | Sealed review at b45246e; two low-severity allocation findings subsequently fixed. Later changes are outside that review |

The [validation report](validation-report.md) retains prior failures, evidence and scope. Actual keyboard/clipboard, installer and certificate tests run only in isolated Windows CI. Local checks use windowless/hidden native fixtures; downloaded installers were not launched locally.

Responsiveness remains incomplete: the earlier a3ef323 PR measured a 583 ms closed-details initial pause. The traced 228f17b push sample passes at 55.0 ms first input/maximum gap, with 128.6 ms total construction, but does not establish the intermittent failure's cause. The 250 ms acceptance threshold is unchanged. The later expansion/collapse stall is fixed: all 18 light/dark transitions pass in CI with maximum input 68.9 ms, complete content and formatting. Broader synchronous continuous/page layout still needs acceptance evidence. Actual process interruption recovery passes; physical power loss and removable-drive failure are separate.

## Artefacts and references

Build with `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`. Packaging uses `scripts/native/Build-NativePackage.ps1` and Inno Setup; normal self-contained execution does not require build tools.

The latest verified installer is `artifacts/native/ci-228f17b-push/packages/0.1.1-preview.221/LensDocsStudio.Native-0.1.1-preview.221-win-x64-Setup.exe` (SHA-256 `E8EB8CF310156698FF4D343B4A84485A9463697185AFEE1748F02756EDE517AD`). Its portable ZIP hash is `0C4F723F21076EFCEA0190C36A7BE91D7DB7D46DAF20C09869E41F3590E66160`.

- Branch: `feature/native-wpf-rebuild`.
- Latest fully CI-validated revision: `228f17bcf42cc366b33d6b4c673a28b7cfc5eb05`.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting `develop`.
- [Native push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38293099393).
- [Web push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38293099371).

## Remaining acceptance

Production is not ready. External evidence still includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachments, physical power-loss/removable-drive behaviour, a real historical schema fixture and an authorised production signing/update identity. Compatibility and large-document responsiveness limits remain explicit engineering work. No merge, public release, tag change or production identity provisioning has occurred.
