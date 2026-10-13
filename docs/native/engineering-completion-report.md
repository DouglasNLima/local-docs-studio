# Native Windows engineering delivery

Recorded on 13 October 2026. This report is the current acceptance record for the original [reconstruction brief](reconstruction-brief.txt). It supersedes older milestone counts and pending statements; original failures remain in the validation history and retained artefacts.

**The implemented edition is ready for an unsigned internal preview. Production readiness is not established.** The brief permits completion when all conditions are satisfied **or remaining blockers are explicitly identified with evidence**. The 28-condition audit below uses that exception openly: it does not turn incomplete production gates into passes. Engineering delivery includes implementation, ordinary defensive review, local and hosted validation, versioned installer/ZIP artefacts, a pushed branch and a draft PR. The final documentation/regression revision must retain successful mandatory CI before the goal is closed.

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

## Exact validation evidence

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

“Proven” below always means the named tested scope. “Partial” or “unverified” is a remaining acceptance limitation, not a pass. Paths are relative to the workspace. Hosted baseline evidence roots are `artifacts/native/ci-7008751-{push,pr}`; they can be downloaded using the run references below.

| # | Original condition | State, evidence and remaining scope |
|---|---|---|
| 1 | Genuine WPF desktop application | Proven: `UseWPF`/`net10.0-windows`, actual desktop processes and four-context `native-ui.json` reports |
| 2 | No embedded browser hosts application UI | Implemented/inspected: native project dependencies and `package-runtime-inspection.json`; no known browser payload in checked archives |
| 3 | No HTML/JavaScript runtime for editing/preview | Implemented/validated: native preview/corpus and checked package; independent HTML export is separate |
| 4 | Installed execution without development tools/server | Bundled runtime origin proven in all installed contexts; SDK-free Windows certification remains unverified |
| 5 | Essential existing workflows supported | Implemented and exercised; [feature matrix](functional-parity-matrix.md) records bounded import/render/export and destination fidelity limits |
| 6 | Portable readable Markdown | Proven for fixtures: exact UTF-8 bytes, guarded saves and restored drafts in desktop/save-recovery reports |
| 7 | Images/links survive workspace copying | Proven for fixtures: copied Word assets, relative links and per-tab roots in Word import/workspace-context reports |
| 8 | Mermaid validated against real fixtures | Proven for documented 40-case corpus and catalogue/theme cases; unsupported forms remain explicit errors |
| 9 | Imports/exports meet documented contracts | Supported byte/package/geometry/source contracts pass; full Office/PDF complex-script/destination fidelity remains partial |
| 10 | Safe legacy-data migration | Explicit supported browser JSON/DOCX imports pass and preserve originals; actual historical native schemas/data remain unverified |
| 11 | Feasible discovery improvements implemented | Implemented with [experience evidence](native-experience-improvements.md); broad responsiveness and physical accessibility limits remain |
| 12 | Meaningful native Windows advantages | Proven in pickers, tabs, monitoring, reviewed moves, clipboard, selectable preview and recovery workflows |
| 13 | Predictable keyboard/focus/selection/scroll | Automated supported workflows pass; physical screen-reader/display coverage and historical intermittent construction delays remain partial |
| 14 | Safe unsaved edits/external changes | Conflict, independent recovery and actual process-interruption checks pass; physical power loss/removable-drive failure remains unverified |
| 15 | Reproducible installer build | Repeatable isolated same-source process and independently checked hashes; bit-identical builds are not claimed |
| 16 | Clean-machine installation validated | Unverified: hosted runners contain development tools; runtime-origin reports explicitly say `CleanMachineCertification: false` |
| 17 | Uninstall preserves documents | Proven within hosted install/upgrade/uninstall/reinstall: 20 state files, workspace source and restored sessions |
| 18 | Compatible GitHub release discovery | Implemented and fixture-validated by update policy/interaction reports; no public release published |
| 19 | Non-interrupting notifications | Implemented and fixture-validated startup/periodic checks and persisted interval; no automatic installation |
| 20 | Installation requires user authorisation | Explicit actions, unsaved handling and final verification pass; positive production execution remains unverified |
| 21 | Update security/trust validated | Disposable/negative trust paths pass; production identity, pinned keys and positive signed upgrade are not established |
| 22 | Failed updates preserve local state | Rejection/cancellation, failed downloads and installer fixture preservation pass; actual interrupted production-signed upgrade remains unverified |
| 23 | Critical automated workflow coverage | Core, corpus, export, desktop, recovery, trust and installer suites pass in documented scope; no exhaustive-coverage claim |
| 24 | Actual desktop GUI validation | Built/installed/upgraded/reinstalled UIA workflows and inspected PNGs pass; physical accessibility/Office/display acceptance remains separate |
| 25 | Recorded build/CI results | Four successful baseline runs and independent downloaded reports below; final verification revision tracked by PR checks |
| 26 | Existing web application functional | 264 Chromium/Edge cases and static integrity checks pass in both hosted runs; buildless publication preserved, deployment skipped on feature/PR |
| 27 | No knowingly hidden critical regression | All discovered failures/limits are disclosed; no current critical regression established by ordinary review. Unfinished protected scan and unresolved timing cause are unclaimed |
| 28 | Accurate implementation documentation | This audit/report, updated tracking documents, guide, handoff and draft PR distinguish implemented, tested, internal preview and production status |

## Artefacts, branch, PR and Actions

Branch: `feature/native-wpf-rebuild`. Draft [PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) targets `develop`. Implementation commits include `c226533` (DOCX text bounds), `5976b24` (direct decorations), `23bcb83` (wrapped observation timeout), `1c3e7b9` (inherited formatting) and `7008751` (browser recovery/status correction). The report and two final regressions follow this application baseline; their latest commit/checks are recorded on the PR without relabelling baseline packages.

| Pipeline | Baseline result |
|---|---|
| Native push | [38367786368](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367786368): success; preview .285, exact branch source |
| Native PR | [38367792092](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367792092): success; preview .286, exact checkout merge |
| Web push | [38367786419](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367786419): success, 264 cases |
| Web PR | [38367792045](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38367792045): success, 264 cases |

Latest baseline branch installer: `artifacts/native/ci-7008751-push/packages/0.1.1-preview.285/LensDocsStudio.Native-0.1.1-preview.285-win-x64-Setup.exe`. Installer SHA-256: `4E0CCC388D5EE0187956D9E27C164AC9A9589322B20F8DF9673C0FBEC0707C47`. The adjacent portable ZIP SHA-256 is `8A77CD58D788D2CA3B57D70B09408CF4DBCFCFBB4B7EF396008923D1DDE6D327`.

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
| Supported Office/Azure DevOps destinations | Native/package/clipboard checks and earlier Office fixture evidence do not establish every destination's layout/attachments. Exercise the actual supported products and live destination workflows |
| Historical user data and hardware failures | Supported synthetic migration and process interruption pass. Supply genuine historical schema fixtures and validate real power-loss/removable-drive scenarios without risking user originals |
| Broad rendering/fidelity/performance | Unsupported constructs and conversion notes are documented. Continuous layout remains synchronous; retained intermittent delay has no established cause. Expand only specific supported contracts with reproductions and meaningful tests |
| Protected security review | Frozen `36e5ad44-ba7f-4cc1-8f8d-b24b9b629856` was last recorded running in validation for `cf436b0..840cf15`. Its attack-path/final completion is not recorded and cannot certify later code. Resume through permitted tools when available, preserving its immutable scope |

These are evidence-backed residual gates under the original brief's completion clause. Current results support internal evaluation of the installer-based native product; they do not establish production certification.
