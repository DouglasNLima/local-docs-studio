# Native Windows engineering handover

The latest verified internal-preview packages are `artifacts/native/ci-56b5161-push/packages/0.1.1-preview.211`, with the initial `0.1.0-preview.211` version alongside. All four CI runs at 56b5161 pass. These packages are unsigned and production certification remains incomplete. Current failed-diagram recovery changes pass local checks and await actual desktop CI; they are not in preview.211.

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

Signing is pluggable through an existing certificate or service adapter, with independently verified publisher pins. The user has configured neither identity nor provider. Both preview.211 installers report `NotSigned`, update envelopes contain no identity/signature, and unsigned updates cannot execute. Update operations cancel/reject stale selections and verify signed metadata, size/hash, Windows Authenticode and exact publisher before an authorised launch. Disposable CI certificates test trust paths and are removed afterwards; production trust remains empty.

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Current local Release | Zero warnings/errors |
| Current core tests | 832 passed; `artifacts/native/tests/diagram-recovery.trx` |
| Failed-diagram recovery | 14 light/dark native cases; exact copy/source mapping, accessible controls and valid-render control; `artifacts/native/diagram-failures` |
| Full local native harness | 40 diagram/export cases, zero failures; `artifacts/native/validation` |
| Current static checks | Passed; web source unchanged |
| Native CI at 56b5161 | PR 38281535734 and push 38281531643 passed, including 59 desktop workflows in built/installed/upgraded/reinstalled apps |
| Web CI at 56b5161 | PR 38281535675 and push 38281531500 passed |
| Diagram paint gate | Eight light/dark frames in the four desktop contexts passed actual visible-node/label checks |
| Current desktop addition | 60th workflow covers recovery clipboard, keyboard focus/caret, Undo and saved-file preservation; awaiting CI |
| Package verification | Four hashes, exact clean source revision, runtime ZIP entries and unsigned metadata verified; `ci-56b5161-push/package-verification.json` |
| Dependency review | Fresh-cache query on 12 October 2026 found no listed vulnerabilities among 37 resolved versions; point-in-time result |
| Scoped security review | Sealed review at b45246e; two low-severity allocation findings subsequently fixed. Later changes are outside that review |

The [validation report](validation-report.md) retains prior failures, evidence and scope. Actual keyboard/clipboard, installer and certificate tests run only in isolated Windows CI. Local checks use windowless/hidden native fixtures; downloaded installers were not launched locally.

Responsiveness remains incomplete: the earlier a3ef323 PR measured a 583 ms closed-details initial pause. The traced 56b5161 push sample passes at 78.7 ms first input/maximum gap, with 140.6 ms total construction, but does not establish the intermittent failure's cause. The 250 ms acceptance threshold is unchanged. Later closed-details expansion and synchronous continuous/page layout still need acceptance evidence. Actual process interruption recovery passes; physical power loss and removable-drive failure are separate.

## Artefacts and references

Build with `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`. Packaging uses `scripts/native/Build-NativePackage.ps1` and Inno Setup; normal self-contained execution does not require build tools.

The latest verified installer is `artifacts/native/ci-56b5161-push/packages/0.1.1-preview.211/LensDocsStudio.Native-0.1.1-preview.211-win-x64-Setup.exe` (SHA-256 `A155F18A6BDF38EC573FDB5B752FEEBB58D0B01B5D8F6E363CDBA21A53923146`). Its portable ZIP hash is `CEE2BC44ECFEB6AB6AB73980802A9850AA9A7821B3301FBDCAE15CBCB9B847BB`.

- Branch: `feature/native-wpf-rebuild`.
- Latest fully CI-validated revision: `56b5161310bef5b03c5640f517f384274ee1136a`.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting `develop`.
- [Native push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38281531643).
- [Web push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38281531500).

## Remaining acceptance

Production is not ready. External evidence still includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachments, physical power-loss/removable-drive behaviour, a real historical schema fixture and an authorised production signing/update identity. Compatibility and large-document responsiveness limits remain explicit engineering work. No merge, public release, tag change or production identity provisioning has occurred.
