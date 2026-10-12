# Native validation report

Status: internal development preview. Local integration and substantial Windows CI coverage pass; production certification is incomplete. Local foreground desktop access is unavailable. Hidden native controls/HWNDs are exercised locally; actual keyboard, clipboard, picker and installed-app workflows run in isolated Windows CI.

The validation client follows Microsoft's separate MTA/STA guidance. Focused in-memory checks pass for exact text/HTML/binary formats, cross-thread frozen image pixels, unchanged originals and owner-apartment data access without accessing the Windows clipboard. Evidence: artifacts/native/clipboard-apartments/native-clipboard-apartments.json. Both ce3cf9f native runs pass every built/installed/reinstalled workflow. Their native-ui-threading.json records actual apartments and UIA timeout settings; the earlier timeout's cause remains unproven.

| Layer | Executed result and evidence |
|---|---|
| Build | .NET 10 Release build passes with zero warnings/errors, including the shell correction |
| Core | All 726 tests pass; artifacts/native/tests/native-core-preview-scheduling.trx |
| Native integration | Complete harness passes: actual WPF controls, source mapping, editing/Undo, images, libraries, settings, workspace/recovery, native SVG and actual exports; artifacts/native/validation/validation-summary.json and native-*.json |
| Diagram/export corpus | 40 cases, zero failures; schema-valid DOCX and locally generated PDF; artifacts/native/validation/mermaid-corpus.json and pdf-inspection.json |
| Catalogue and maths | 56 catalogue diagram/theme cases and 32 native maths theme cases, with inspected renders; native-catalogue.json and native-maths.json |
| Actual desktop | At ce3cf9f, native PR/push 38204610723/38204607899 pass all 53 workflows in built, installed and reinstalled apps; artifacts/native/native-ci-ce3cf9f-pr.log and native-ci-ce3cf9f-push.log. New minimum-window workflow 54 awaits CI |
| Browser preservation | Both ce3cf9f web runs 38204610690/38204607927 pass all 262 Chromium/Edge cases; artifacts/native/web-ci-ce3cf9f-pr.log. All 260 pre-settings cases also pass locally in 9.3 minutes; browser-template-migration.log |
| Exported Docs Site | Eight disk/HTTP browser cases pass in the ce3cf9f native PR run |
| Static integrity | 50 modules, 57 shell assets, 150 vendor assets and 56 external-dependency scans pass after the shell correction |
| Destination applications | Actual Word/Excel checks pass on Office 16.0.20430.20092; wider destination coverage remains separate |
| Installation | Self-contained Inno build and built/installed/reinstalled workflow contexts pass on fresh Windows CI runners; those runners contain development tools and do not establish SDK-free clean-machine certification |

The 53 completed desktop workflows include real folders and reviewed moves, file/folder case changes, external modifications, snapshots/recovery, image persistence and copying, source-aware navigation, context-specific search, formatting/Undo, clipboard representations, generated files, Word/content libraries and restoration, captions/details/progress/maths, wiki links, workspace maps, settings import/export and passive artefact imports/exports. Exact assertions remain in the native validation project; counts do not imply unsupported-format parity.

Editor-only scheduling adds four locally passing unshown-control outcomes: current outline navigation without preview construction, latest explicit text/closed-details preparation with Undo preservation, resumed visible rendering and correct single-pane Find context, and cancellation after intervening edits. Evidence: artifacts/native/validation/native-preview-scheduling.json and artifacts/native/preview-scheduling-final/native-preview-scheduling.json. Desktop workflow 53 passes in both ce3cf9f native runs, including installed/reinstalled contexts. The actual Editor-only and workspace screenshots from artifacts/native/ci-9fddfd9-pr/ui were inspected.

Minimum-size shell validation reproduced a zero-width Cancel button and partially clipped Find/Replace Close button with long status messages. The corrected status grid reserves action/caret space and the search bar wraps. Four hidden native layouts (860 × 540 and 1,280 × 780, light/dark) pass full control-bounds, enabled-state, accessible-name and unchanged-source assertions. Twelve 100/150/200% raster PNGs are retained under artifacts/native/shell-layout-after; all four 100% layouts plus minimum light 150% and dark 200% output were inspected. The full harness also passes. This is not physical-monitor DPI or screen-reader speech certification. Desktop workflow 54 resizes the real app, operates replacement/Close, verifies Undo and saved bytes, and awaits CI.

A recurring UIA failure remains unresolved. Native push 38201348833 at 5cb0aeb and 38202782258 at d86a5ac time out reading the editor Value pattern immediately after figure workflows. The latter fails despite the bounded read-only retry. The paired 5cb0aeb PR passes all three desktop contexts; the failed push runs are not counted as passed. Evidence: native-ci-5cb0aeb-push-failure.log, native-ci-d86a5ac-push-failure.log and ci-d86a5ac-push/ui/native-ui.json under artifacts/native. New failure handling captures the owned window independently of UIA and uses MiniDumpWithThreadInfo for native stacks without requesting a full heap/environment dump. No root cause is claimed yet.

Performance findings are fixture measurements, not general guarantees:

| Workload | Observed result | Evidence under artifacts/native |
|---|---|---|
| 4,000-item list construction before nested yielding | Queued input delayed 322.6 ms | layout-before-nested-yield/native-large-block.json |
| Same list after nested yielding | Roughly 1–2 ms to queued input; complete source/text preserved and obsolete work cancelled | validation/native-large-block.json and native-nested-layout.json |
| Final list viewer layout | Diagnostic attach/measure/arrange comparison: 2,130 ms before brush freezing, 1,296 ms after; later full run approximately 986 ms | layout-measured-stages, layout-frozen-brushes and validation/native-nested-layout.json |
| Nested 1,000-row table and 1,200-paragraph quote/details | Input/cancellation, complete source mapping and actual final-target selection pass | validation/native-nested-layout.json and native-large-*.png |

All four large-block viewport PNGs were inspected with their final text visible. The diagnostic viewer measures both initial attachment and explicit 640 × 300 layout; it does not establish a universal application frame time. A 128-item list-group experiment showed no useful improvement and was removed. The retained trace is layout-profile.nettrace. Final layout, very large individual paragraphs/code blocks, incremental reuse, broad high-DPI/scrolling and memory/startup workloads remain incomplete measurements.

Outstanding acceptance includes broader supported Mermaid/export fidelity, live Azure DevOps attachment behaviour, accessibility/multi-monitor coverage and SDK-free clean-Windows certification. Explicit unsupported HTML/LaTeX retains source; PDF strikeout/highlight fidelity remains limited. Production signing identities and a positive signed upgrade require authorised external prerequisites. The current unsigned preview cannot execute an untrusted downloaded update. No merge, release or production-readiness claim has been made.

Commands and product boundaries are in README.md and [update and packaging](update-and-packaging.md). Earlier baseline, failures, corrections, exact run IDs and artefact paths remain in [validation history](history/validation-report-through-9fddfd9.md). Historical pending statements describe their revision; this document is the current acceptance record.
