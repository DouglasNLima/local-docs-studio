# Native Windows preview acceptance and engineering delivery

Recorded on 13 October 2026. This report adopts the completed reconstruction at `70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55` and records its next acceptance/hardening phase. The original [reconstruction brief](reconstruction-brief.txt), milestone revisions, failures and package identities remain historical evidence. Reconstruction is not being restarted.

**The implemented edition is ready for an unsigned internal preview. Production readiness is not established.** The brief permits completion when all conditions are satisfied **or remaining blockers are explicitly identified with evidence**. The 28-condition audit below uses that exception openly: it does not turn incomplete production gates into passes. Engineering delivery includes implementation, ordinary defensive review, local and hosted validation, versioned installer/ZIP artefacts, a pushed branch and a draft PR. All four mandatory pipelines pass for final code revision `27d21b43f214c6a2283ff68b2ff7f876e087283d`; the documentation reconciliation follows that revision and its latest head/checks are recorded on PR 21.

## Architecture and technologies

`src/native/LensDocsStudio.Native.slnx` contains four projects: portable Core services, the WPF application, Core tests and the Windows validation executable. The application targets `net10.0-windows` with `UseWPF`; C#/XAML and native controls own editing and preview. CommunityToolkit.Mvvm, AvalonEdit, Markdig, Mermaider, SharpVectors, WpfMath, Open XML SDK and PDFsharp/MigraDoc supply the corresponding native capabilities. Dependencies have locked restore inputs.

The native package contains its .NET/WPF runtime. It has no embedded browser, JavaScript runtime, Node dependency or local server for editing/preview. HTML and Docs Site are independent export formats. The buildless browser/PWA/GitHub Pages edition and the existing WinUI/WebView2 shell remain separate products. Product, installation, state, mutex and association identities are distinct. See [architecture decisions](architecture-decisions.md).

## Implemented features and Windows advantages

The application supports portable Markdown/Mermaid workspaces, folders and multiple tabs; recent documents; source editing, completion and formatting; outline, search, source navigation and history; native preview, reading pages, diagrams and maths; local images and controlled clipboard transfer; DOCX/HTML/PDF text import; DOCX/PDF/HTML/Markdown Bundle/Docs Site export; template/content libraries, generators, profiles, settings transfer and explicit browser-data migration. Optional artefact metadata remains bounded, passive and untrusted.

Native advantages include Windows file/folder dialogues and Explorer actions, filesystem monitoring and conflict handling, reviewed moves that preserve supported links, independent draft recovery and snapshots, restored per-tab workspace context, selectable preview text, native diagram zoom/copy, keyboard-accessible controls and a bundled offline guide. A private table grid edits actual cells, alignment, rows and columns with Cancel/Apply and whole Undo/Redo. Appearance uses neutral surfaces, system/high-contrast resources and restrained Lens accents. The product remains a generic local documentation studio.

Recent completed corrections bound generated DOCX text before buffer growth, retain direct and inherited Word run formatting, handle wrapped UI Automation observation timeouts within the existing deadline, and preserve newer browser save feedback when an older draft lookup finishes. Their reproductions, fixture corrections and original failures remain recorded. See [experience improvements](native-experience-improvements.md) and the [feature matrix](functional-parity-matrix.md).

## Functional parity and compatibility

Essential documented authoring, workspace, import/export and recovery workflows are implemented and exercised. Parity is measured against outcomes rather than browser internals; it is not a claim that every web renderer construct or destination layout is identical.

Mermaid validation covers 40 repository diagram/export cases, another 56 catalogue/theme cases and focused activation/contrast/background checks. Eight documented diagram families are represented. Unsupported activation lifecycles, additional families and unsupported syntax produce an explicit native error with preserved source and source-copy/edit actions. Six native palettes are consistent across supported preview/export paths. See [diagram compatibility](diagram-compatibility.md).

Supported Word imports retain lists, headings, links, paired bookmarks, literal code, grouped quotes, images and run formatting. Document defaults, paragraph/character inheritance and direct overrides are tested; cycles retain readable text with a note and inheritance over 64 levels is rejected. Word templates preserve supported styles, numbering, themes, section defaults, headers/footers and media. Package/schema checks and exact image bytes do not certify Microsoft Word pagination or every template feature. Conditional table/numbering presentation, complex-script formatting, code language, deeper quote structures and unsupported field/file links remain documented conversion limits.

PDF tests cover supported text, links, images, list hierarchy, continuation alignment, width bounds and selectable word order. Complex-script shaping, bidirectional text, joined/colour emoji and tagged accessibility remain unverified. HTML/LaTeX support is bounded by the documented native mapping. Exported sites are tested on disk and a static server; actual Office and Azure DevOps destination fidelity remains a separate gate. Multi-section template-body merging is not added to the established template contract.

## Installer and updater

`scripts/native/Build-NativePackage.ps1` creates isolated, immutable preview directories with a self-contained x64 app, Inno installer, portable ZIP, version metadata, checksums and update metadata. Both preview versions in a CI run come from the same source. Repeated versions fail safely; bit-identical build reproducibility is not claimed. The hosted lifecycle tests install, same-source version upgrade, uninstall and reinstall, preserve workspace/state and verify the runtime modules of each installed app.

Updates use compatible GitHub release metadata with product/channel/version/architecture/OS/asset eligibility. Non-blocking discovery, notification, separate user-authorised actions, unsaved-document handling, signed manifest validation, Windows Authenticode/publisher checks and final verification are implemented. Rejected/cancelled updates retain local state and remove failed download bytes.

The confirmed production signing configuration is empty: no certificate, service, thumbprint or verified publisher. Certificate-store and service adapters are pluggable, but no identity is invented or provisioned. `update-trust.json` still has no manifest keys or publishers. **Unsigned update download and execution remain blocked.** Current packages are `NotSigned` manual internal previews, with empty update-envelope identity/signature. Disposable hosted trust fixtures are removed after testing and never configure production trust or execute a production update. A positive production-signed upgrade remains unverified. See [update and packaging](update-and-packaging.md).

## Preview acceptance phase

**Internal preview: supported within the recorded scope. Genuinely clean Windows: BLOCKED. Real external applications: PARTIAL. Production certification: BLOCKED.** Available acceptance checks have been executed and the targeted corrections pass mandatory hosted CI. The remaining continuous-layout defect and unresolved intermittent construction timing are explicit below.

### Adopted delivery and independently checked package

The four final delivery runs at `70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55` are verified successful: [native push 38371057232](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38371057232), [native PR 38371063213](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38371063213), [web push 38371057253](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38371057253) and [web PR 38371063230](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38371063230). Both native runs report 1,037 Core tests and 40 corpus cases; each web run reports 264 browser cases. These supersede the earlier 1,035-test baseline as the delivered hosted result, without changing that baseline's labels.

The downloaded branch-source preview is `0.1.1-preview.287`, source `70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55`, clean checkout, x64, self-contained, unsigned internal preview. Installer SHA-256: `3332AB0E708FDEE462B9039C8AC4E92EEB26BA0576DEF4335C7EB9A11A593A66`; portable ZIP: `1B88784184A2F0D868FE5DCFE72285B10E08A9E951386248DAB9B6C3D720C70F`. The native push `native-validation` artefact is ID `11799893618`, archive digest `sha256:82d7c8edc0a5a4d42a08c9899ae16943c17291a6dc8e178f97244135f16d9833`. Evidence is retained in `artifacts/native/ci-70a2a9d-push`; no local installer was executed.

PR preview `.288` is a different checkout: merge `be4f05c2cd3fbcb5dab0e420d905b395f7a70e0c`. Its installer hash is `C0CBC35AF0635C60AEF0D42BFA378CB560F1E4861EFA54464819D1759F6866A2`, ZIP `6960259F0C759C7D74BCDFE3D56707EDE4028728A27BEA05D2623928E4861B1A`. It is not represented as the branch-source package.

### Environment, procedure and actual acceptance results

The available Windows desktop has .NET SDK 10.0.401, Node and development tools. Registry evidence reports build `26300.9550`, display version `26H2` and the stale product label `Windows 10 Home`; a supported Windows 11 release/image has not been independently established. No accessible disposable Windows VM/image was found. Running virtualisation services do not establish an available VM. Hosted Windows runners also contain development tools. **Neither environment is clean-machine certification.**

The user authorised isolated manual native/Word/Excel checks with synthetic files, all test documents, temporary files, logs and native state under this worktree's ignored `acceptance/` directory. Startup state isolation was inspected before launch: `--state-directory` resolves before instance/session/recovery construction, the activation identity is scoped to that state root, and draft/library/update-cache paths derive from it. The existing verified native executable and subsequently the pinned CI .287 portable product were used. No installation, uninstallation, update, system-setting, association or certificate operation was performed. Personal documents and unrelated Office sessions were not accessed. Office was launched only after checking that Word/Excel were absent; external field updates were declined and AutoSave was visibly off. Clipboard contents were retained only in memory; sequence guards preserved newer clipboard content rather than overwriting it (`clipboard-restoration*.json`).

| Requirement | Environment and procedure | Execution / final verdict | Observed result and evidence | Defect or limitation |
|---|---|---|---|---|
| Clean installation and 12-step lifecycle | Read-only package/environment preflight; inspected available VM tools | **BLOCKED**; lifecycle **NOT RUN** locally | `acceptance/preflight/pinned-287/preflight.json` and `pinned-291/preflight.json`: pinned bytes/identity PASS, environment BLOCKED; wrong hash/source controls rejected without creating or overwriting evidence | Disposable, supported Windows 11 image/snapshot without development tools required; hosted lifecycle remains its original narrower proof |
| Native editing, tabs, nested workspace, save/restart | Computer Use on existing native build and synthetic Portuguese workspace | **PASS** for executed workflow | `acceptance/isolation-check.json`, `manual-edit-result.json`, screenshots and persisted `workspace/aceitacao.md`: literal Portuguese marker, observed Undo/Redo, exact saved bytes and restored document; nested guide/tab visible | Does not certify every possible folder or external-change sequence |
| Clipboard image insertion | Copy synthetic preview pixels, paste into editor, save, copy whole workspace and open its document in the pinned package | **PASS** for executed workflow | `acceptance/manual-image-paste.json`, `manual-portability.json`, relative `assets/images/pasted-image-*.png`, exact copied source/image hashes and preserved original input | File-picker/drag-drop modes retain hosted UIA proof; no new manual claim for those modes |
| Portable workspace and representative output | New public-service integration check saves two nested Portuguese documents, bundles to another root, reopens and exports | **PASS** | `acceptance/validation/workflow-corrected/native-acceptance-workflow.json`: 13 checks; exact source/image bytes, repeated asset reuse, native Mermaid/line break, TSV/CSV, DOCX import/export, selectable PDF Unicode, HTML, Docs Site, SVG/PNG and synthetic template header/footer | Actual output files retained alongside original/copied workspaces; this is artefact validation, not destination pagination |
| Manual DOCX/PDF export | Actual native export menu and Windows save dialogues | **PASS** for file generation | `acceptance/office/native-portuguese.docx` (40,753 bytes), `.pdf` (83,509 bytes), screenshots and DOCX inspection | Original DOCX cell-newline failure and unnecessary Word prompt retained; corrections described below |
| Microsoft Word | Actual installed Word; open native exports, inspect headings/images/table/lists/pages, edit/save/reopen local synthetic copy and inspect corrected template | **PARTIAL** overall; named observations **PASS** | `acceptance/word-save-result.json`, `office/portuguese-docx-inspection.json`, `office/corrected-word-save.json`, screenshots: corrected line break, three image occurrences, literal identifiers/formula-like text, synthetic header/footer, no unnecessary field-update prompt; reopened UI and saved XML retain the Portuguese edit | Arbitrary templates, all inherited formatting, explicit page-break variations and universal pagination remain unverified |
| Microsoft Excel | Separately inspected/copied synthetic XLSX (no macros, external relationships or formulas), guarded safe-mode launch, actual UI inspection | **BLOCKED** live paste; local payloads **PASS** | Earlier combined creation/launch was rejected by automatic review; narrower preparation/launch succeeded. `office/excel-current-blocker.json` and screenshot show the blocking “Default File Types” first-run modal; selecting global Office defaults exceeds the authorised boundary. Native copy formats and corrected TSV/CSV round-trips are verified locally | No format selection or workbook edit/paste occurred. Escape/Alt+F4 were ignored; only owned PID 49176 was terminated, and original workbook SHA-256 is unchanged. Historical Excel evidence is not current acceptance |
| Azure DevOps | Local native clipboard/Markdown payload checks | **PARTIAL** locally; live destination **BLOCKED** | Bounded source/CF_HTML/image/export tests and persisted payloads; source remains editable | No authorised authenticated disposable work-item location available; attachment/paste fidelity unverified |
| Accessibility and display | Hidden-HWND UIA/layout checks, light/dark themes and 100/125/150/200% raster output | **PARTIAL** | `native-shell-layout.json` and PNGs in current validation; existing keyboard/name/pattern/selection assertions preserved | Raster output is not actual DPI switching, multiple-monitor or physical screen-reader certification; system settings unchanged |
| Recovery, updates and data integrity | Full Core/native suites, negative/cancelled operations and previously guarded hosted interruption/lifecycle checks | **PASS** within synthetic/automated scope | Current TRX/native reports plus revision-labelled hosted evidence; original input files preserved | Real historical schema fixtures, physical power loss and removable-drive failure remain **BLOCKED** by missing safe environment/data |

Generated acceptance evidence is intentionally ignored, not published with private desktop screenshots. The report identifies exact local paths; repeatable integration outputs are also produced by standard native CI validation and uploaded with its normal artefact. Failed observations are retained in separate directories. Tool/fixture corrections (filename separators, bounded modal coordinates, preference casing, split Word text runs, apphost invocation and correct repeated-image expectations) are recorded in `acceptance/procedure-corrections.json`; they are not represented as product fixes.

The actual `.287` portable process opened `acceptance/workspace-copy` with fresh `acceptance/package-state` and consent disabled. Its product version is `0.1.1-preview.287+70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55`; CLR/JIT/WPF native modules originate inside the 529-entry pinned package. `packaged-preview-launch.json`/`packaged-preview-runtime.json` record this direct execution. No runtime was installed or local server started for the native app. The developer tools already present prevent this finding from becoming clean-machine certification.

### Confirmed corrections and regression evidence

1. **PDF retained-text resource exhaustion:** a shared-content PDF can repeat decoded text across pages while staying within compressed-input/page-count limits. `ImportPdf` now checks cumulative characters and UTF-8 bytes before every retained append, bounds page text, and checks cancellation before input/parsing/page/extraction stages. Titles, headings and separators count towards the output budget. Ten small synthetic PDF regressions pass; original bytes remain unchanged. This closes the application aggregation finding; PdfPig's internal page allocations and non-cancellable parser calls are an explicit residual boundary.
2. **Multiline table clipboard corruption:** actual native UI copy emitted an unquoted newline, splitting one table cell into rows. TSV now quotes newline/tab/quote-bearing cells, doubles quotes and retains empty edge cells. Paste retains embedded tabs and recognised attribute-free `<br>` becomes a typed native line break. Unknown HTML attributes and code examples stay inert/literal. A second copy and CSV round-trip verify actual cell values, not only generated Markdown. Original failures: `acceptance/regressions/tsv-original` and `paste-break-original`; 61 affected clipboard/inline cases pass.
3. **DOCX decoded controls:** actual Word displayed `&#10;` cell content as one line. Decoded newlines/tabs now produce proper Word break/tab elements, retaining formatting. Original failure: `acceptance/regressions/word-cell-original`. The affected Word/export tests verify schema-valid output and exact controls.
4. **Unnecessary Word confirmation:** even field-free exports requested field updates on open. The request is now emitted only for actual document/header/footer fields; supported template PAGE fields retain their update request. Original failure: `acceptance/regressions/word-field-prompt-original`; 250 affected Word/export cases pass.
5. **Existing web save-feedback race:** an in-flight preview could finish after a successful save and replace its confirmation with “Rendered”. The separate web product now tracks status revisions and lets preview completion update its status only if it still owns that revision. Original reproduction uses a held Mermaid render: save/source bytes are verified before release, then the old completion demonstrably replaced the message. Original trace is retained in `acceptance/regressions/web-active-render-original`. The queued-render control, in-flight case and original delayed-draft assertions pass in both browsers; later edits still produce fresh render feedback. Service-worker cache v68 delivers the corrected modules. Native editing/preview receives no browser runtime.

Current local validation of these corrections: Release build with zero warnings/errors, **1,055 Core tests passed, zero failed/skipped**, full native **40 corpus cases, zero failures**, the 13-check representative workflow PASS, and **268 Chromium/Edge browser cases passed** (12.6 minutes). Evidence: `acceptance/tests/core-final/core.trx`, `acceptance/validation/word-and-workflow-corrected/validation-summary.json`, `native-acceptance-workflow.json`, `acceptance/tests/web-full` and artefacts. Static checks pass for the existing 50 modules, 57 shell assets, 150 vendor assets and 56 dependency scans. The final hosted results independently confirm these counts.

### Retained first acceptance CI outcomes and targeted follow-up

Commit `8dd8f3afeba6bfb02512a3ebbcfe6490005a0c4e` produced mixed evidence: [native push 38394168646](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38394168646) **FAIL**, [native PR 38394172454](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38394172454) **PASS**, [web push 38394168502](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38394168502) **PASS**, [web PR 38394172345](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38394172345) **FAIL**. The native passing run verifies 1,055 Core cases, the corpus/new workflow and original guarded desktop/lifecycle suites. Its .290 preview is PR-merge source, not a branch-source package.

The failing native run passed build/Core/signature/corpus/site checks, then did not observe the Updates window within the existing desktop deadline. Its screenshot leaves Help open; the coordinate click had not activated the command. Evidence is retained in `acceptance/ci-8dd8-push-failure` (artefact `11811159233`), including control tree, desktop/window PNGs, thread dump and original log. The follow-up observes the visible enabled Updates item, records its bounds/support and invokes its supported UIA command exactly once. The same window, consent/trust assertions and 20-second deadline remain; no action retry or longer timeout is introduced. This is an automation-action correction, not an invented updater defect.

The failing web run reports 263 passes and one save-feedback failure, retained in `acceptance/ci-8dd8-web-pr-failure` (artefact `11811641419`) and its original log. Its independent passing sibling does not eliminate the race. The controlled reproduction and status-ownership correction above justify another hosted run of changed code. Neither failed run was rerun unchanged until green.

### Verified final code revision and preview artefact

Acceptance fixes are committed as `8dd8f3afeba6bfb02512a3ebbcfe6490005a0c4e` and final code revision `27d21b43f214c6a2283ff68b2ff7f876e087283d`. All four final code pipelines are verified successful:

| Pipeline | Verified result at 27d21b43 |
|---|---|
| Native push | [38404441225](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38404441225): 1,055 Core tests, zero failed/skipped; 40 corpus cases, zero failures |
| Native PR | [38404448470](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38404448470): 1,055 Core tests, zero failed/skipped; 40 corpus cases, zero failures |
| Web push | [38404441087](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38404441087): 268 Chromium/Edge cases pass |
| Web PR | [38404448444](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38404448444): 268 Chromium/Edge cases pass |

The downloaded native push `native-validation` artefact is ID `11818883983`, 303,943,871 bytes, archive digest `sha256:a6e91840d4456cf560b4fa9f1e7ad8ba90fbcfaf82a52b7b8efd014cddcf34de`. Independent inspection of its reports verifies four distinct desktop contexts × 66 checks, the 13-check persisted-workspace workflow, wizard/lifecycle success, preservation of 20 state files, actual restored sessions after upgrade/reinstall and unchanged production trust. Hosted lifecycle reports explicitly set `cleanMachineCertification: false`. Evidence: `acceptance/ci-27d21b4-push`, `ci-27d21b4-runs.json`, `ci-27d21b4-verification.json` and the four original downloaded logs.

The exact branch-source package is **`0.1.1-preview.291`**, clean source `27d21b43f214c6a2283ff68b2ff7f876e087283d`, self-contained x64, unsigned internal preview. Installer `LensDocsStudio.Native-0.1.1-preview.291-win-x64-Setup.exe`: 58,882,908 bytes, SHA-256 `BA2EFD08636F8B7B673E0656982A449E246B01F6C8E8AFCF4C53AD0A96BD2647`. Portable ZIP: 84,272,814 bytes, SHA-256 `8DCE568B482FD6483800E91F644F2E6026230E40F5E8FBAC0BEF6817A683C14B`, 529 runtime entries. Installer status is `NotSigned`; update-envelope key/signature are empty. Production trust is the empty embedded source configuration and the disposable verification report confirms it was unchanged. The read-only .291 preflight passes package pins and blocks this developer environment. No downloaded installer was executed locally.

The native PR run uses its PR-merge checkout and produces .292; it is not the .291 branch-source package. Earlier .287/.288/.285/.286 and failed acceptance artefacts retain their own labels and hashes. This report's subsequent documentation-only reconciliation does not relabel .291 as a package from its later head. PR 21 records that head's exact revision/checks after push; no merge, release or tag change is authorised.

### Retained documentation-head failure and validation follow-up

Documentation revision `b4f77beaba42b98b303acf00b1e880c99a9fdbac` also received mandatory CI. Native push [38409836185](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38409836185) **FAIL** after Core, corpus, desktop and interrupted-save checks passed. Original log/artefact are retained in `acceptance/ci-b4f77be-push-original-failure.log` and `ci-b4f77be-push-failure` (artefact `11823150166`, archive digest `sha256:2d51b9ad57a48a49c6ec0e1fc8cacf3327d9ea8e70c76463cae80592eb2e9029`). The wizard report has no completed checks and `installationStarted: false`: UIA `FromHandle` returned `Win32Exception` with native `ERROR_TIMEOUT` 1460 and a generic HRESULT, which the existing observation-timeout classifier did not recognise.

A deterministic local regression reproduces that exact exception escaping the helper (`acceptance/regressions/win32-observation-original.log`). The validation-only correction recognises native error 1460 within the existing observation deadline; it repeats no installer/input action and changes no wizard assertions, timeout, source fixture or disposable-environment guard. Four additional observations verify recovery, unrelated Win32 errors propagating unchanged, and persistent Win32 timeout failure with the original exception retained. All 13 observation cases pass; the Release build has zero warnings/errors. Broader native validation in `acceptance/validation/win32-observation-final` passes 40 corpus cases with zero failures and the 13-check persisted-workflow integration. Hosted follow-up remains mandatory, with its exact current head/results recorded on PR 21. Native application/Core/web runtime code remains at the verified 27d21b43 product revision. This failure is retained rather than replacing the previous green evidence or rerunning it unchanged until green.

### Performance investigation and remaining product defects

Original nested-details failure is retained exactly: run [38344048333](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38344048333), `ci-77be570-pr-failure/native-validation/validation/native-nested-details-construction.json`: input `587.9933 ms`, initial call `587.7282 ms`, construction `671.4819 ms`, complete text/source preserved. Toggle initialisation reached `319.1709 ms`, header attachment `587.3879 ms`; thread CPU and GC counters did not advance through those stages. Scheduling/waiting is a plausible inference, **not an established root cause**.

One fresh-process `--layout` execution, with the original fixtures and unchanged 250 ms construction assertions, recorded details input `21.6582 ms`, maximum gap `32.3050 ms`, construction `150.2499 ms`. This did not reproduce the original stall and does not establish a fix. The initial invocation through `dotnet <dll>` was rejected by the existing diagnostic apphost guard; using the actual validation apphost corrected that procedure, without altering the guard.

New instrumentation queues input before continuous viewer attachment/Measure/Arrange/UpdateLayout, then observes when it can run. In that fresh process, list first-layout input was **2,022.4487 ms**, table **771.3626 ms**, details **349.7660 ms**, quote **230.9251 ms**. The later full run recorded list **1,911.21 ms**, table **558.29 ms**, details **284.17 ms**, quote **329.08 ms**. This directly demonstrates continuous first-layout input delay; it is distinct from renderer construction. Per-viewport evidence now records a separate **FAIL** against 250 ms rather than presenting a successful construction result as layout acceptance. `PassedScope` names the unchanged construction/source/cancellation/navigation gates.

**Remaining defect:** WPF continuous layout synchronously processes large blocks. Automatic mode already selects Pages for at least 1,000 source elements or 256 KiB; explicit Continuous remains a documented limitation. A speculative renderer rewrite or silent override of the requested mode is not justified by this phase. Original fixtures, thresholds, source/selection checks and failures remain intact; no sleeps or unchanged retry-until-pass were introduced. The intermittent details-construction cause remains unresolved separately.

### Security assessment and residual certification

A separate standard assessment of all **398 tracked `src/native` files** at adopted revision `70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55` completed: scan `4ffc7b82-e3ee-4441-b50a-c1b6fae9f12b`. Independent reviews covered workspace/reparse/ZIP boundaries, inert metadata/HTML, remote image consent, clipboard, resource budgets, recovery/writes, updates and operational test guards. One validated Medium finding, `csf_dcf4b7fff09128c9ad24f4d6`, concerns PDF aggregate text and is corrected above. Fresh pre-patch and candidate-patch reviews found no blocking bypass within that reported boundary; focused and full regressions pass.

Canonical report: `C:/Users/dougl/.codex/state/plugins/codex-security/scans/MarkdownReader/70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55_20261013T101150Z_x5e8jq11/report.md`, with sealed coverage/findings/threat model and SARIF. The tool warned that working-tree contents changed during the scan; tracked native source was unchanged until scan completion, while untracked acceptance evidence was created. Its results remain for the original snapshot, not a scan certificate for the later patch. Derived fix evidence is `acceptance/regressions/pdf-fix-review.json`; canonical findings were not rewritten. Tool-reported usage: 58,262,867 total tokens, 58,109,632 input (56,735,488 cached), 153,235 output, 39,415 reasoning, four threads, `codex_rollout` source. This is returned telemetry, not a newly billed-token estimate.

Daybreak access was unavailable; [protected access](https://chatgpt.com/cyber) remains separate. Frozen historical scan `36e5ad44-ba7f-4cc1-8f8d-b24b9b629856` for `cf436b0..840cf15` was not retargeted or claimed complete. No production signing identity exists, production trust stays empty, and unsigned update download/execution remains blocked.

## Historical validation evidence through 7008751

The application/browser baseline is branch revision `7008751a52cb9a782619e97aac27cd6bcc899bdf`. Native PR packages identify the actual checkout merge `22ce42192ecfbacbc5116c6a7498c67f608c2283`; that merge is not relabelled as the branch revision.

| Evidence | Observed result and scope |
|---|---|
| Local Core | 1,037 passed, zero failed/skipped in `artifacts/native/final-roadmap-core/core.trx`; includes two additional shared-metadata regressions published with this report |
| Local Word styles | 24 cases within the full suite; schema-valid DOCX bytes, inheritance/toggles/overrides/cycles/depth/budgets and original-input preservation |
| Local shared drawing metadata | Two bounded public-import cases: four repeated image references retain exact labels/one asset; 64 references exceed a 4,096-character generated-text budget and fail recoverably, with original bytes unchanged |
| Local native rendering | 40 corpus cases, zero failures in `word-run-styles-final-validation/validation-summary.json`; actual WPF strike/underline preview inspected |
| Hosted Core at baseline | 1,035 passed, zero failed/skipped in each downloaded `tests/native-core.trx`; the two final regressions are additional to this baseline |
| Hosted native rendering | 40 corpus cases, zero failures per run; supported export, settings, migration, diagrams, source and layout reports retained |
| Actual Windows application | Four contexts × 66 workflow checks per native run, including table editing, inherited Word formatting, excessive-text rejection, portability, keyboard and recovery |
| Installation/state | Visible wizard plus complete lifecycle pass; 20 state files preserved, two sessions restored and all three installed runtime origins verified per run |
| Trust | Pluggable signing policy, disposable Windows certificate and combined signed-metadata/negative cases pass; production trust remains unchanged and test certificates are removed |
| Web | Local `npm test` and each hosted web run pass 264 Chromium/Edge cases; static checks pass for 50 modules, 57 shell assets, 150 vendor assets and 56 dependency scans |
| Packages | Eight installer/ZIP hashes and sizes, exact clean checkout revisions, unsigned metadata/envelopes and four 529-entry runtime archives independently checked |
| Dependency advisory audit | Native direct/transitive NuGet audit lists no vulnerable packages across four projects on 13 October; this is point-in-time advisory coverage |

Ordinary defensive review covers passive imported metadata, cancellation, bounded style traversal and generated text, input preservation, guarded filesystem operations and unchanged empty update trust. It is not a completed protected security scan or a proof of absence of vulnerabilities. The earlier immutable review and the still-incomplete frozen `cf436b0..840cf15` protected scan keep their original scope; Daybreak availability was not used to block ordinary engineering or bypass controls.

Hosted desktop, clipboard, installer and certificate tests ran on genuine disposable GitHub-hosted Windows. This batch used local headless browser tests and windowless/native checks; it did not execute downloaded installers or spoof hosted-runner guards.

## Performance findings and retained failures

At the baseline, the push/PR construction fixtures record maximum input gaps of 32.3464/39.9304 ms and first input of 13.544/12.0888 ms. All four nested fixtures pass the unchanged input/source/cancellation gates and produce no real stall captures. The 4,000-item continuous list still needs 515.2925/484.4685 ms to attach/layout synchronously.

Across the two hosted samples, enumerating 1,001 documents takes 8.629–15.0347 ms and parsing the 4,140,025-byte, 20,000-paragraph fixture takes 272.8228–343.8452 ms. Programmatic visible edits take 0.303–1.5015 ms. Actual fixture-process launch through ready text/Mermaid preview takes 1.685–1.699 s and image paste through persisted bytes/source takes 13.3442–16.8088 ms. Small mixed-fixture DOCX/PDF/HTML generation takes 3.2445–3.372/19.7261–45.5691/1.9364–8.0019 ms after preparation, excluding file writes.

These are prepared-runner observations with warm caches, not universal keyboard latency, cold Windows startup or an installed-app memory budget. `native-ui-performance.json` measures the actual app; `native-performance.json` memory includes the validation host and preceding cases.

The earlier native PR run `38344048333` failed nested-details input at 587.9933 ms. Its cause remains unresolved and original evidence is retained in `ci-77be570-pr-failure`. Later green runs do not prove its elimination. Validation-owned stack capture diagnoses future observed failures without changing the product or the 250 ms gate. The earlier wrapped UIA timeout and browser recovery/status race have separate reproduced corrections; assertions and deadlines were retained. No unchanged failure was repeatedly rerun until green.

## Original 28-condition acceptance audit

Statuses apply only to the stated scope: **PASS**, **FAIL**, **PARTIAL**, **BLOCKED**, **NOT RUN**. PARTIAL/BLOCKED are not passes. Current acceptance evidence is above; historical hosted roots `artifacts/native/ci-7008751-{push,pr}` and delivered roots `ci-70a2a9d-{push,pr}` retain their original revision labels.

| # | Original condition | Verdict | Evidence and remaining scope |
|---|---|---|---|
| 1 | Genuine WPF desktop application | PASS | `UseWPF`/`net10.0-windows`, actual manual desktop and hosted four-context UIA reports |
| 2 | No embedded browser hosts application UI | PASS | Native dependencies and checked runtime archives; web/legacy products remain separate |
| 3 | No HTML/JavaScript runtime for editing/preview | PASS | Native corpus and actual selectable preview; HTML is an independent export |
| 4 | Installed execution without development tools/server | PARTIAL | Bundled installed runtime origins verified on hosted runners; SDK-free machine unavailable |
| 5 | Essential existing workflows supported | PASS | Existing supported suite plus representative persisted-workspace integration; bounded contracts retained |
| 6 | Portable readable Markdown | PASS | Exact manual saved UTF-8 and bundle copied bytes; guarded saves/recovery |
| 7 | Images/links survive workspace copying | PASS | Representative copied-root integration retains exact image/source bytes and exports; relative/wiki link fixtures retained |
| 8 | Mermaid validated against real fixtures | PASS | 40 native corpus and 56 catalogue/theme cases; new real native export/preview; documented unsupported forms excluded |
| 9 | Imports/exports meet documented contracts | PARTIAL | Supported artefact checks pass, reproduced table/Word defects corrected; destination/pagination/shaping limits remain |
| 10 | Safe legacy-data migration | PARTIAL | Supported synthetic JSON/DOCX migrations preserve originals; representative historical data unavailable |
| 11 | Feasible discovery improvements implemented | PASS | Targeted clipboard/control/prompt corrections with original failures and regressions; no redesign |
| 12 | Meaningful native Windows advantages | PASS | Native pickers/tabs/clipboard/images/selectable preview plus reviewed moves/monitoring/recovery |
| 13 | Predictable keyboard/focus/selection/scroll | PARTIAL | Supported UIA/selection gates pass; continuous layout input FAIL above; intermittent construction cause unresolved |
| 14 | Safe unsaved edits/external changes | PARTIAL | Synthetic conflicts/recovery/process-interruption checks pass; physical hardware failure unverified |
| 15 | Reproducible installer build | PASS | Repeatable isolated packaging and independently pinned artefact hashes; no bit-identical reproducibility claim |
| 16 | Clean-machine installation validated | BLOCKED | Developer tools detected, no suitable disposable Windows image; local lifecycle NOT RUN |
| 17 | Uninstall preserves documents | PASS | Hosted install/upgrade/uninstall/reinstall retains 20 state files, documents and restored sessions; no local uninstall |
| 18 | Compatible GitHub release discovery | PASS | Product/channel/version/architecture eligibility fixtures; no legacy shell offered as WPF update |
| 19 | Non-interrupting notifications | PASS | Startup/periodic/interval and notification fixtures; no automatic installation |
| 20 | Installation requires user authorisation | PASS | Separate actions, cancellation, unsaved handling and final verification; production positive path separately blocked |
| 21 | Update security/trust validated | PARTIAL | Disposable/negative verification passes; empty production pins, no genuine identity or positive signed upgrade |
| 22 | Failed updates preserve local state | PARTIAL | Rejection/cancellation/download cleanup and installer fixtures pass; interrupted production-signed upgrade unavailable |
| 23 | Critical automated workflow coverage | PASS | 1,055 local and hosted Core, 40 native corpus, 13 persisted-workflow checks, four hosted 66-check desktop contexts and lifecycle |
| 24 | Actual desktop GUI validation | PARTIAL | Actual native/Word open/edit/save/reopen and hosted UIA; Excel first-run configuration blocks paste, live Azure DevOps/physical accessibility unavailable |
| 25 | Recorded build/CI results | PASS | Exact adopted 70a and final-code 27d21b43 runs verified; original 8dd8 failures retained; latest documentation-head checks recorded on PR |
| 26 | Existing web application functional | PASS | Current static checks, 268 local cases and both final-code hosted web runs pass; Pages deployment skipped on this branch |
| 27 | No knowingly hidden critical regression | PASS | Independent scoped scan's Medium PDF finding corrected/reviewed/tested; parser/performance/external limitations explicit |
| 28 | Accurate implementation documentation | PASS | Current evidence/status audit reconciles final code/CI/package, actual manual findings and blockers; PR tracks the subsequent documentation head |

## Historical artefacts, branch, PR and Actions through 7008751

Branch: `feature/native-wpf-rebuild`. Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) targets `develop`. Implementation commits include `c226533` (DOCX text bounds), `5976b24` (direct decorations), `23bcb83` (wrapped observation timeout), `1c3e7b9` (inherited formatting) and `7008751` (browser recovery/status correction). The report and two final regressions follow this application baseline; their latest commit/checks are recorded on the PR without relabelling baseline packages.

| Pipeline | Baseline result |
|---|---|
| Native push | [38367786368](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367786368): success; preview .285, exact branch source |
| Native PR | [38367792092](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367792092): success; preview .286, exact checkout merge |
| Web push | [38367786419](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367786419): success, 264 cases |
| Web PR | [38367792045](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367792045): success, 264 cases |

Historical 7008751 baseline branch installer: `artifacts/native/ci-7008751-push/packages/0.1.1-preview.285/LensDocsStudio.Native-0.1.1-preview.285-win-x64-Setup.exe`. Installer SHA-256: `4E0CCC388D5EE0187956D9E27C164AC9A9589322B20F8DF9673C0FBEC0707C47`. The adjacent portable ZIP SHA-256 is `8A77CD58D788D2CA3B57D70B09408CF4DBCFCFBB4B7EF396008923D1DDE6D327`.

The PR-merge preview .286 installer SHA-256 is `6A1C6CF9944E74FDCF183EA7D37C66041A40D196143DA2AC52382CB7B98FE8AB`; its ZIP is `039B0E12DA0D0B1C1DEF7F10B6FD5EFC4AF9C06FAB4B3D49A846627C30BF94BA`. Both evidence roots contain `package-verification.json`, `package-runtime-inspection.json` and `runtime-evidence-review.json` generated after independent download. Original hosted reports/packages are in each run's `native-validation` artefact. Those local verification summaries are not generated by the CI upload itself.

```powershell
gh run download 38367786368 --name native-validation --dir artifacts/native/ci-7008751-push
gh run download 38367792092 --name native-validation --dir artifacts/native/ci-7008751-pr
dotnet build src/native/LensDocsStudio.Native.slnx -c Release
dotnet test src/native/LensDocsStudio.Core.Tests -c Release
npm test
```

Existing download directories must be retained or a new destination chosen. Local/hosted failure evidence, performance samples, defensive review and advisory audit are under `artifacts/native`; generated evidence is ignored source. The source guide explains build/package usage. No merge, release publication, tag movement or production identity provisioning is part of this delivery.

## Remaining production prerequisites and engineering limits

| Remaining item | Evidence and next acceptance action |
|---|---|
| Authorised production identity and signed update | Empty `update-trust.json` and `NotSigned` package records. Configure the real certificate/service and reviewed publisher/key pins only after identifiers are available; test actual authorised signed installation/upgrade and interruption |
| SDK-free clean Windows | Installed module origins pass but hosted runtime reports explicitly disclaim clean-machine certification. Install/run/uninstall on a genuinely clean supported Windows image without development tools |
| Physical accessibility/displays | Automated names/patterns/focus and hidden layout checks are retained. Validate a real screen reader, physical DPI changes and multi-monitor placement |
| Supported Office/Azure DevOps destinations | Actual Word open/inspect/edit/save/reopen of the corrected synthetic template passes within its named scope. Broader Word pagination/templates remain unverified; Excel's first-run settings modal blocks live paste under the authorised boundary; an authorised disposable Azure DevOps location is unavailable |
| Historical user data and hardware failures | Supported synthetic migration and process interruption pass. Supply genuine historical schema fixtures and validate real power-loss/removable-drive scenarios without risking user originals |
| Broad rendering/fidelity/performance | Unsupported constructs and conversion notes are documented. Continuous layout remains synchronous; retained intermittent delay has no established cause. Expand only specific supported contracts with reproductions and meaningful tests |
| Security certification and parser limits | Separate scoped assessment at 70a completed and the aggregate-PDF finding is corrected/reviewed/tested. PdfPig internal materialisation remains outside application output budgets. Frozen protected `36e5ad44-ba7f-4cc1-8f8d-b24b9b629856` for `cf436b0..840cf15` remains incomplete and cannot certify later code; preserve its scope/access restrictions |

These are evidence-backed residual gates under the original brief's completion clause. Current results support internal evaluation of the installer-based native product; they do not establish production certification.
