# Native validation report

Status: internal development preview. Local integration and substantial Windows CI coverage pass; production certification is incomplete. Local foreground desktop access is unavailable. Hidden native controls/HWNDs are exercised locally; actual keyboard, clipboard, picker and installed-app workflows run in isolated Windows CI.

Current validation-client change follows Microsoft's separate MTA/STA guidance. Focused in-memory checks pass for exact text/HTML/binary formats, cross-thread frozen image pixels, unchanged originals and owner-apartment data access. They never access the Windows clipboard. Evidence: artifacts/native/clipboard-apartments/native-clipboard-apartments.json. Actual desktop delivery and the timeout diagnosis remain unverified for this change; native-ui-threading.json will record the real apartments and UIA timeout settings in CI.

| Layer | Executed result and evidence |
|---|---|
| Build | .NET 10 Release build passes with zero warnings/errors through 9fddfd9 |
| Core | All 726 tests pass; artifacts/native/tests/native-core-preview-scheduling.trx |
| Native integration | Complete harness passes: actual WPF controls, source mapping, editing/Undo, images, libraries, settings, workspace/recovery, native SVG and actual exports; artifacts/native/validation/validation-summary.json and native-*.json |
| Diagram/export corpus | 40 cases, zero failures; schema-valid DOCX and locally generated PDF; artifacts/native/validation/mermaid-corpus.json and pdf-inspection.json |
| Catalogue and maths | 56 catalogue diagram/theme cases and 32 native maths theme cases, with inspected renders; native-catalogue.json and native-maths.json |
| Actual desktop | At 9fddfd9, native PR/push 38203569347/38203565583 pass all 53 workflows in built, installed and reinstalled apps; artifacts/native/native-ci-9fddfd9-pr.log |
| Browser preservation | Both 9fddfd9 web runs 38203569315/38203565517 pass all 262 Chromium/Edge cases; artifacts/native/web-ci-9fddfd9-pr.log. All 260 pre-settings cases also pass locally in 9.3 minutes; browser-template-migration.log |
| Exported Docs Site | Eight disk/HTTP browser cases pass in the 9fddfd9 native PR run |
| Static integrity | 50 modules, 57 shell assets, 150 vendor assets and 56 external-dependency scans pass through 9fddfd9 |
| Destination applications | Actual Word/Excel checks pass on Office 16.0.20430.20092; wider destination coverage remains separate |
| Installation | Self-contained Inno build and built/installed/reinstalled workflow contexts pass on fresh Windows CI runners; those runners contain development tools and do not establish SDK-free clean-machine certification |

The 53 completed desktop workflows include real folders and reviewed moves, file/folder case changes, external modifications, snapshots/recovery, image persistence and copying, source-aware navigation, context-specific search, formatting/Undo, clipboard representations, generated files, Word/content libraries and restoration, captions/details/progress/maths, wiki links, workspace maps, settings import/export and passive artefact imports/exports. Exact assertions remain in the native validation project; counts do not imply unsupported-format parity.

The current Editor-only change adds four locally passing unshown-control outcomes: current outline navigation without preview construction, latest explicit text/closed-details preparation with Undo preservation, resumed visible rendering and correct single-pane Find context, and cancellation after intervening edits. Evidence: artifacts/native/validation/native-preview-scheduling.json and artifacts/native/preview-scheduling-final/native-preview-scheduling.json. Desktop workflow 53 passes in both 9fddfd9 native runs, including installed/reinstalled contexts. Its actual screenshot is in artifacts/native/ci-9fddfd9-pr/ui/native-editor-only-preview.png.

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
