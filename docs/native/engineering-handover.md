# Native Windows engineering handover

The latest verified internal-preview packages are `artifacts/native/ci-0c20f64-push/packages/0.1.1-preview.227`, with `0.1.0-preview.227` alongside. Both native CI runs pass, including the Word allocation correction, hosted-only test guards, complete installer cancellation with an unsaved draft and four desktop contexts. These packages are unsigned; production acceptance remains incomplete. Both web runs also pass; the PR required a rerun after a Chromium teardown crash with all assertions passing. That failure evidence is retained. A separate diagram clipboard regression is compiled and committed as 9c07ab7 but is not in preview.227 and still needs hosted execution.

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

The next compiled validation requires installed, upgraded and reinstalled applications to load coreclr.dll and wpfgfx_cor3.dll from their own installation directory. It records actual executable/module paths in native-ui-runtime.json; the built-app context records its framework runtime. Hosted results remain pending. Package contents alone do not establish which runtime a running process selected, and this check does not replace SDK-free clean-machine certification.

The self-contained x64 Inno installer and portable ZIP use separate product, state and association identities. CI covers visible wizard choices, Back/cancellation, installation, newer-preview replacement, uninstall/reinstall and reopening retained sessions. Twenty state files and workspace source are preserved. Both preview versions use the same source revision; this does not certify historical schema migration or an SDK-free machine.

Signing is pluggable through an existing certificate or service adapter, with independently verified publisher pins. The user has configured neither identity nor provider. Both preview.227 installers report `NotSigned`, update envelopes contain no identity/signature, and unsigned updates cannot execute. Update operations cancel/reject stale selections and verify signed metadata, size/hash, Windows Authenticode and exact publisher before an authorised launch. Disposable CI certificates test trust paths and are removed afterwards; production trust remains empty.

## Validation and evidence

| Evidence | Result and scope |
|---|---|
| Current local Release | Zero warnings/errors |
| Current core tests | 839 passed; `artifacts/native/tests/native-word-code-budget-all.trx` |
| Failed-diagram recovery | 14 light/dark native cases; exact copy/source mapping, accessible controls and valid-render control; `artifacts/native/diagram-failures` |
| Full local native harness | 40 diagram/export cases, zero failures; `artifacts/native/word-budget-validation` |
| Current static checks | Passed; web source unchanged |
| Native CI at 0c20f64 | PR 38300926460 and push 38300922818 passed, including 839 core tests and 60 desktop workflows in built/installed/upgraded/reinstalled apps |
| Web CI at 0c20f64 | Push 38300922779 and PR 38300926434 attempt 2 pass; original teardown crash evidence retained |
| Diagram paint gate | Eight light/dark frames in the four desktop contexts passed actual visible-node/label checks |
| Current desktop addition | Recovery clipboard, keyboard focus/caret, selection collapse, Undo and saved-file preservation pass in all four contexts |
| Package verification | Four hashes, exact clean source revision, runtime ZIP entries and unsigned metadata verified; `ci-0c20f64-push/package-verification.json` |
| Installer files in use | All four preservation assertions pass; five wizard outcomes pass, exact snapshots retained and unsaved-draft screenshot inspected; `ci-0c20f64-push/evidence-review.json` |
| Local test isolation | Five desktop/Office commands and installer script refuse before arguments/paths; `artifacts/native/desktop-test-guards.json` |
| Dependency review | Fresh-cache query on 12 October 2026 found no listed vulnerabilities among 37 resolved versions; point-in-time result |
| Scoped security review | Sealed b45246e..cf436b0 review 475201d4-5562-4ffa-bc7a-166818c61152: 101 inventory plus six supplemental files, one Low Word code-allocation finding, one rejected colour hypothesis. The later fix is tested separately, outside that immutable review |

The [validation report](validation-report.md) retains prior failures, evidence and scope. Actual keyboard/clipboard, installer and certificate tests run only in isolated Windows CI. Local checks use windowless/hidden native fixtures; downloaded installers were not launched locally.

Responsiveness remains incomplete: the earlier a3ef323 PR measured a 583 ms closed-details initial pause and a later local table sample reached 360.6 ms. The 0c20f64 push sample passes at 65.6778 ms first input/maximum gap and 139.5582 ms total construction, but does not establish the intermittent cause. The 250 ms threshold is unchanged. Later expansion/collapse is corrected: all 18 light/dark transitions pass with maximum input 74.9417 ms, complete content and formatting. Broader continuous/page layout still needs evidence. Actual process interruption recovery passes; physical power loss and removable-drive failure remain separate.

## Artefacts and references

Build with `dotnet build src/native/LensDocsStudio.Native.slnx -c Release`. Packaging uses `scripts/native/Build-NativePackage.ps1` and Inno Setup; normal self-contained execution does not require build tools.

The latest verified installer is `artifacts/native/ci-0c20f64-push/packages/0.1.1-preview.227/LensDocsStudio.Native-0.1.1-preview.227-win-x64-Setup.exe` (SHA-256 `309131F00DBCF5C040D17F6217773C49AE52209C7C386BB3D10ED42554268FE8`). Its portable ZIP hash is `065F8401BED158E1601967BF2A00068C9BA1717B7F2AB38AABB8B0E338D0CF9D`.

- Branch: `feature/native-wpf-rebuild`.
- Latest CI and package-validated revision: `0c20f64870c94264b06b0fd643dc2c54eea72557`; web PR passed on attempt 2.
- Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting `develop`.
- [Native push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38300922818).
- [Web push validation](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38300922779).

## Remaining acceptance

Production is not ready. External evidence still includes an SDK-free clean Windows installation, physical multi-monitor/DPI and screen-reader checks, live Azure DevOps attachments, physical power-loss/removable-drive behaviour, a real historical schema fixture and an authorised production signing/update identity. Compatibility and large-document responsiveness limits remain explicit engineering work. No merge, public release, tag change or production identity provisioning has occurred.
