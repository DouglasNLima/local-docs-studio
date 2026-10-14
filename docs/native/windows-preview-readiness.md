# Windows preview readiness

Recorded 14 October 2026. **READY WITH RESTRICTIONS** for controlled internal evaluation with copied/synthetic workspaces in approved x64 Windows environments. Prefer Pages for large documents, inspect important exports and follow the approved procedure for unsigned software. Clean Windows 11 and production signing certification remain **BLOCKED**. This is an internal preview, not a production release.

## Exact candidate and distribution

Selected preview: **`0.1.1-preview.317`**, product `LensDocsStudio.Native`, clean **branch-source `dd6c4c33f1a6fa86206e76b2153afcc7b151a523`**, `release/native-preview-readiness`. Base: verified native `9c7454eeb892614af93881b762a1b6aecc3259d2`. Draft [PR 23](https://github.com/DouglasNLima/local-docs-studio/pull/23) targets `feature/native-wpf-rebuild`. PR 21 remains the reconstruction review; PR 22 remains Draft and isolated with its **NO-GO** decision. No experimental code, flag, executable or dependency is integrated.

This report completion is a later documentation-only revision; its final head/checks are recorded in PR 23. Builds from that head retain their own source/version metadata. **Do not relabel `.317` as the report-only head or a PR merge checkout.**

Build: Release, locked NuGet restore, `net10.0-windows`, `win-x64`, self-contained Core/WPF **10.0.13**, no trimming/single-file publishing/development symbols. Native C#/XAML/AvalonEdit and FlowDocument remain supported. Browser/PWA/Pages and the legacy Windows shell remain separate. Installed editing/preview require no embedded browser, JavaScript runtime, local server or development tools.

Source run: [native push 38523259733](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38523259733), attempt 1. `native-validation` artefact **11874889099**, retained until **28 October 2026, 07:00:15 UTC**. The independently downloaded 303,751,346-byte archive matches GitHub's digest:

`9451b179b5cb5da47542bef849a593923d16f48480219ad137257c38642d6912`

Local immutable evidence root: `artifacts/native/ci-dd6c4c3-push`, relative to this worktree. Selected package: `evidence/packages/0.1.1-preview.317/`.

| Output | SHA-256 |
| --- | --- |
| `LensDocsStudio.Native-0.1.1-preview.317-win-x64-Setup.exe` | `77b36337e2df38256375eba4caae76f8a2a656d8b371a203cfc00615201d52e0` |
| `LensDocsStudio.Native-0.1.1-preview.317-win-x64.zip` | `36d1954a8e4fcac47565971a93e902ea550da60f2485e0df0acc06bd507e3a2f` |

Adjacent `version.json`, `SHA256SUMS.txt`, `native-update.json` and hosted `package-verification.json` agree. The ZIP includes `PREVIEW-GUIDE.md`, `PREVIEW-RELEASE-NOTES.md` and third-party notices/licences. Independent local verification checks **528 payload files**, exact apphost/Native/Core version and Git source, included runtime/components, unsigned signatures and payload hygiene. No private documents, source/tests, development symbols, signing keys or experimental viewer binaries were found. Inspection does not execute the preview. Locked repeatable packaging is established; byte-for-byte reproducibility is not claimed.

Separately verified **PR preview `.318`** is merge checkout **`5ab09e6ca945bdb285ca66ec078f10979fee8f28`**, [native PR 38523263365](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38523263365), artefact 11874334996. Installer/ZIP hashes: `6ca3094bc4d8a265996293392aafbea01d9e5e41a7a35d887b8736e49a57e35d` / `fb157117f36ba9febe366a58adf4cecc65c5c4ed9b55317b89f8f14949618ae9`. Checkout logs, sidecar and embedded binary identities agree. This is distinct from the selected branch-source package.

## Verified acceptance

Both native candidate runs and both web runs pass. [Web push 38523259663](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38523259663) and [web PR 38523263467](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38523263467) each verify 268 browser cases and static integrity; Pages deployment is skipped. Final report-only head validation is separately linked in PR 23, preserving these candidate identities.

| Gate | Status and actual scope |
| --- | --- |
| Restore / Release build / Core | **PASS:** 1,057 tests, zero failed/skipped; zero build warnings/errors |
| Rendering and persisted workflows | **PASS:** 40 corpus/export cases; 13 nested Portuguese-workspace/image/bundle/Mermaid/DOCX/PDF/HTML/site/template checks validate files, contents and source |
| Local browser regression | **PASS:** all 268 Chromium/Edge cases; static integrity passes |
| Hosted native desktop | **PASS:** four contexts × **68 checks**, built/installed/upgraded/reinstalled |
| First-run entry points and Help | **PASS:** optional empty-draft workspace/document/offline-help actions; typing hides guidance; source and unsaved-close prompts retained. Fixture-process launch is not a cold OS test |
| Version identification | **PASS:** About displays exact executable version/source in all contexts, including installed/upgraded `.317` |
| Continuous guidance | **PASS:** accessible quiet native Label; established 262,144-character / 1,000-mapped-element criteria; explicit mode, final target, selection, source, Undo and update consent retained |
| Visible installer wizard | **PASS:** five checks cover keyboard/path/task review, Back/Cancel, Install/Finish, live-unsaved-app files-in-use blocking and source/default/legacy preservation |
| Installer lifecycle | **PASS on hosted Windows:** `0.1.0-preview.317` → `0.1.1-preview.317`, same source; registration/binary/metadata agreement; 20 private-state files and workspace bytes retained through upgrade/uninstall/reinstall; actual tabs/text/settings restored; final uninstall removes native binaries/registrations |
| Runtime | **PASS:** bundled Core/WPF 10.0.13; three installed contexts load CoreCLR/WPF graphics from their own installation |
| Authoring/export/recovery | **PASS:** edit/Undo/Redo/save, relative images, internal links, Mermaid, Split/Preview, real DOCX/PDF output, close/reopen, copied source/image bytes and interrupted-save recovery |
| Package rejection controls | **PASS:** wrong source/version/hash, dirty source, tampered ZIP and excluded viewer with recomputed checksum rejected |
| Unicode/malformed input | **PASS:** three separate hosted processes exit 0 after native layout **and raster** for ordinary Unicode, malformed/unpaired input and `a` plus 65,536 combining marks; original source and complete preview text retained. Not universal resource/input certification |
| Update trust | **PASS within development scope:** channel/version/architecture/product/signature/tamper/offline/cancellation/state fixtures; six interactions, 14 adapters and disposable Windows trust checks. Empty production trust and blocked unsigned download/execution retained |
| Continuous 250 ms objective | **FAIL:** selected push list **1,287.85 ms**, table **251.80 ms**, mixed **655.26 ms**. Successful evidence recording/compatibility CI does not make these timings PASS |
| SDK-free clean Windows 11 | **BLOCKED:** no suitable authorised disposable image; hosted/developer environments are not certification |
| Production-signed upgrade | **BLOCKED:** no production signing identity or manifest pins |

Hosted coverage: **Windows Server 2025**, `windows-2025-vs2026`, build 26100, with development tools. Per-user scope/path, Start Menu, initially unchecked Desktop/Open with options, native mutex/AppId/ProgID, legacy isolation and data retention were inspected/exercised. Installer admission starts at Windows 10 build 17763; this is not certification of every Windows edition/architecture.

The developer desktop is Windows 11 Home by CIM, build **26300.9550 / 26H2**, SDK 10.0.401 and development tools; its registry Windows 10 Home label is stale. Read-only `.317` preflight: package **PASS**, environment **BLOCKED**, all 12 local lifecycle steps **NOT RUN**. No new native preview application/installer was launched or installed there. Local native checks use hidden/windowless validation surfaces without foreground, system clipboard, installer, certificate-store or private-state actions. Infrastructure was inspected once; no host virtualisation/security/Office setting changed.

## Corrections and evidence

User-visible improvements: optional empty-draft entry points, informed explicit Continuous with accessible advice, exact version/source in About. Packaging requires clean committed source, distinguishes branch/PR provenance, omits symbols and bundles usable guides/notes. Native CI includes this branch and PR base. Data/recovery, module boundaries, browser/legacy behaviour and timing thresholds remain intact.

Original failures remain preserved: `ede9dc3` native push/PR [38520515161](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38520515161) / [38520546519](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38520546519) used unsupported editor UIA SetFocus; the established window/click path retains assertions. `4c4e8f9` push/PR [38521236464](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38521236464) / [38521242074](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38521242074) exposed visible advice suppressed from UIA control view. A windowless regression reproduces it; the named Label passes locally and in every final desktop context. No deadline was increased. Earlier local `.20261014` remains a separately labelled `ede9dc3` packaging check; superseded partial runs are not acceptance passes.

Evidence: `ci-dd6c4c3-push/{github-artifact.json,archive-verification.json,independent-package-verification.json,preflight/}` and extracted `evidence/{tests,validation,ui,input-safety,installer-wizard,install-smoke,package-controls,packages}`. Actual outputs/bytes, installed About/quick-start screenshots, source identities, runtime paths, preserved state and failure logs remain available. PR and earlier failure directories remain separate and immutable.

## Security and remaining restrictions

Ordinary scoped defensive review is **PARTIAL**: workspace/reparse/ZIP boundaries, DOCX/PDF budgets, explicit remote-image consent, clipboard/recovery/state isolation, installer scope and unchanged update trust inspected; relevant regressions pass. npm and all four native projects' transitive NuGet audits report no known vulnerable packages. This is not a new full protected scan or proof of absence of vulnerabilities.

Sealed assessment `4ffc7b82-e3ee-4441-b50a-c1b6fae9f12b` remains for `70a2a9d`, including its Medium aggregate-PDF finding. The pre-append page/aggregate character/UTF-8/cancellation correction remains; all ten PDF limit regressions pass. PdfPig internal materialisation/cancellation is outside that guarantee. Frozen protected `36e5ad44-ba7f-4cc1-8f8d-b24b9b629856` (`cf436b0..840cf15`) remains incomplete; a fresh protected scan is **NOT RUN**. No old assessment is relabelled as current coverage.

Apphost/installer are **NotSigned**. No production certificate, service, publisher, thumbprint or manifest key was invented/configured. Windows publisher/SmartScreen policy may block evaluation; never disable protections. Trusted unsigned in-app download/execution remains blocked. Removed disposable development certificates do not establish a production-signed upgrade.

Continuous remains slow in documented large/complex cases; the 4.14 MB full-layout/navigation limit and intermittent **587.9933 ms** details construction delay remain unresolved. Automatic Pages and explicit Continuous are preserved. Unsupported Mermaid, import/template/export fidelity, PDF shaping/bidirectional/emoji/tagged-accessibility, physical screen-reader/DPI/multi-monitor and real hardware interruption remain **PARTIAL / NOT RUN** within their documented boundaries.

Prior real Word open/edit/save/reopen retains its limited scope. Live Excel remains **BLOCKED** by first-run global Office settings; Azure DevOps by the unavailable authorised disposable work item. No blocked attempt or global configuration change was repeated. See [engineering acceptance](engineering-completion-report.md), [performance](performance-and-responsiveness.md), [packaging/trust](update-and-packaging.md), [parity](functional-parity-matrix.md) and [handoff](continuation-handoff.md) for previous evidence.

## Retrieve and evaluate

Actions artefacts expire; these are not permanent public distribution links. With repository access, choose a **new empty destination**:

```powershell
gh run download 38523259733 --repo DouglasNLima/local-docs-studio --name native-validation --dir C:\Preview\LensDocsStudio-317
Get-FileHash C:\Preview\LensDocsStudio-317\packages\0.1.1-preview.317\LensDocsStudio.Native-0.1.1-preview.317-win-x64-Setup.exe -Algorithm SHA256
Get-FileHash C:\Preview\LensDocsStudio-317\packages\0.1.1-preview.317\LensDocsStudio.Native-0.1.1-preview.317-win-x64.zip -Algorithm SHA256
```

Compare this report's hashes and inspect adjacent `version.json`, then follow the [preview guide](windows-preview-guide.md) and [release notes](windows-preview-release-notes.md). Installer/portable options preserve ordinary Markdown/assets; private state uses `%LOCALAPPDATA%\LensDocsStudio.Native` and survives uninstall. Evaluate copied/synthetic documents in an approved environment, use offline F1 help, inspect exports and share only sanitised version/source/action/error details. No automatic support upload or telemetry is added.

No PR merge, public Release, tag movement, production signing or ordinary desktop installation was authorised/performed. Remaining external gates are disclosed restrictions, not completed certification.
