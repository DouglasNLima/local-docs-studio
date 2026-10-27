# Native experience improvements

## Complex ER and preview loading - 27 October 2026

Incoming documents mask outgoing preview content immediately and show a delayed centred native ring while work remains pending. Updates retain the same document's accepted preview with compact feedback. Request-owned lifecycle state rejects stale completion/cancellation; existing viewport restoration retains scroll, zoom and selection ownership. Windows animation preferences permit a static busy ring. Retry preview uses current source and appearance, without caching transient failures. Large PNG exports preserve the entire canvas at a bounded scale. [Evidence and limitations](complex-er-preview-loading.md).

## Native desktop UX refinements - 27 October 2026

Caption theming, bidirectional Find/counters, direct diagram panning, verified ER/Flowchart inspection and preview zoom are implemented from develop `0df86ad`, with local native runtime and regression evidence. See the [source-labelled implementation and validation receipt](native-desktop-ux-refinements.md). Final acceptance and hosted CI remain pending; existing release and paused-PR boundaries are unchanged.

## Goal IX - Mermaid creation hub, 25 October 2026

[Draft PR #49](https://github.com/DouglasNLima/local-docs-studio/pull/49) adds the
native contextual toolbar and searchable 24-family picker, with 28 validated
starting points dispatched to the existing visual editors. Captured owner/version
checks and `MermaidDocumentEditSession` retain private drafts and atomic Apply.
See the [architecture, compatibility and retained failure record](mermaid-creation-hub.md).
Use the exact-source engineering receipt in Draft PR #49 for the post-commit
verdict. Hosted acceptance requires every mandatory gate on that same head;
intermediate results cannot certify it. Personal-host GUI tests are **NOT RUN**.

The accepted starting develop is `9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f`, tree
`1fb777317ad6e5c8531eebcb565fefae4227e354`, after PR #47. PR #48's separate
High Contrast/f05/Find/beta outcomes and paused PR #38 remain unchanged. Earlier
milestone sections below retain their original source-labelled historical status.
No production release, merge, signing, installer execution on a personal host,
association change or controlled 250 ms re-certification is authorised.

## Seventeen native Mermaid editors - Expansion IV

Kanban, Requirement and basic GitGraph extend the fourteen existing families. Use
Visual Edit, standalone Mermaid or the Insert command, with canonical native output
and exact source preservation. Fixed Kanban indentation, Requirement symbolic names
and GitGraph branch/head dependencies have explicit protection. Mermaider stays at
0.15.1 and the official .NET 10/WPF runtime is unchanged.

The [Draft PR #43 delivery record](https://github.com/DouglasNLima/local-docs-studio/pull/43)
identifies exact final-source checks, source-labelled Windows captures and fresh
unsigned internal packages. Acceptance requires completed gates for the identified
source. Earlier Expansion III records below remain historical and retain their real
source and package attribution. Stock Continuous timing is paused; .749 remains
unresolved. No production signing or public release is authorised.


## Fourteen native Mermaid editors � Expansion III

XY Chart, Radar and Sankey add category comparisons, positional profiles and weighted
flows through the shared source editor, canonical native output and owner Apply
sessions. See the [grammar/protection contract](xy-radar-sankey-editors.md) and
[engineering receipt](expansion-iii-engineering-receipt.md). Implementation, initial
local checks and final certification are distinct: hosted normal/High Contrast UIA,
installed-authoring acceptance and final clean-source packages are pending. Mermaider
0.15.1, the official .NET 10/WPF runtime and stock Continuous are unchanged. PR #38
remains paused; the 250 ms goal is incomplete and .749 unresolved.


## Eleven native Mermaid editors — Expansion II

Timeline, User Journey and Quadrant join the existing eight normal editor families.
They reuse #40's source occurrences, source-group patching, private history, shared
native workspace and owner Apply session. No renderer/runtime upgrade, browser
fallback or LayoutExperiment is included. See the [syntax matrix](timeline-journey-quadrant-editors.md)
and [engineering receipt](expansion-ii-engineering-receipt.md) for source-labelled
verification, known protections and the Draft PR's final package/results record.
Stock Continuous and paused #38 retain their documented status. Internal packaging
and actual OS/installer acceptance are isolated; optional associations stay disabled.


## Accepted consolidation — 19 October 2026

The product decision accepts the bounded production improvements in PRs #36/#37
and pauses draft #38 at `31fa3ad12f3e0c87f5e45cea6f97801af53b67cb`. Stock Continuous
remains the ordinary renderer. #36 merged as `1944e500b4b3fbd78acc28414098a88b6c81537c`;
#37 now targets develop. Its final PR receipt owns the actual merge identity,
post-integration validation and fresh internal preview provenance. Those results
must be established at the final develop revision, independently of feature-head
receipts. The original 250 ms target remains incomplete, `.749` unresolved and
#38's Input verdict failing/INSUFFICIENT EVIDENCE. No active architecture migration
or deadline blocks further Mermaid editors. See the [consolidation contract and
next-editor handoff](accepted-native-consolidation.md).

The records below are historical, with their original source and acceptance scope.

Continuation after `0237b08`: **implemented with bounded limitations; original goal INCOMPLETE**. Document-owned flat viewport ranges and owner-local colour brushes reduce demonstrated dispatcher work while preserving stock WPF rendering. Exact six-fixture first-layout acceptance stays red (1f0463e replications: 46/66 and 16/66 failures); all 264 independent correctness outcomes and 132 raster pairs pass. Automatic actual High Contrast enable/disable passes without manual ApplyTheme. Original `.749` first-launch causality remains unresolved. Native segmentation and native-semantics framework reuse prototypes both miss 250 ms; the modified framework is not shipped. A different Continuous layout/selection/UIA architecture requires an explicit scope decision. Final-source CI, package and integration statuses belong to the [source-labelled report and final PR receipt](continuous-preview-performance-refinement.md#stock-and-framework-replication-at-1f0463e); no intermediate package certifies a later head.


This Continuous preview continuation is stacked on open PR #36 at 9a64dfa5. Its [focused report](continuous-preview-performance-refinement.md) owns the plain-text/table refinement, current-version and selection guards, controlled six-fixture comparison and new interaction coverage. The controlled 250 ms target remains incomplete: list, mixed and some paragraph/table samples fail. Earlier green compatibility/package records do not certify it; the final Draft PR #37 receipt owns exact-head checks and package readiness.

The dedicated [Windows preview readiness report](windows-preview-readiness.md) records the subsequent release-readiness candidate, guide, package provenance and available acceptance. Historical evidence below retains its original revision and scope.

The targeted [performance continuation](performance-and-responsiveness.md) reduces redundant native formatting elements and initial list allocations, adds exact source/RTF/formatting/stale-render regressions, and fixes a reproduced active-tab close selection race. Continuous list median input improves 923.42 → 727.88 ms on the named machine, but still fails 250 ms; mixed/4.14 MB and the original intermittent details failure remain unresolved. Existing delivery evidence below keeps its historical scope. New-build desktop proof is hosted; the optional local manual retest remains BLOCKED/NOT RUN.

## Current delivery record

The [engineering acceptance report](engineering-completion-report.md) is the current 28-condition record. Delivered revision `70a2a9d0dc2c9fd1e4cebaf28c96015fde5d1a55` passes four verified hosted runs: native push/PR `38371057232`/`38371063213` (1,037 Core tests, 40 corpus cases), web push/PR `38371057253`/`38371063230` (264 cases each). Branch-source preview .287 and PR-merge .288 retain distinct source/hash labels. Earlier `7008751`, 1,035-test and .285/.286 evidence below remains historical.

The acceptance phase executed isolated native/Word Computer Use and representative persisted-workspace exports. Product corrections at `27d21b43` and final validation revision `f7d5d557a7e5601f4147af403dd4f1fe189bb65c` pass all four hosted pipelines. Final native push/PR `38419939879`/`38419949514` verify 1,055 Core/40 corpus; web push/PR `38419939724`/`38419949513` verify 268 cases. Thirteen observation and 13 persisted-workflow checks pass. Read the report for exact branch-source .299 pins, retained .291/earlier identities, original failures, real destination findings and the continuous-layout input defect. Clean Windows, live Excel/Azure DevOps, physical accessibility/hardware and production signing remain explicitly limited. Unsigned updates stay blocked; historical protected scans remain unchanged.

Actual Word reinspection retains multiline cells, literal values, embedded diagrams/images and synthetic header/footer, removes the field-free update prompt, and preserves a saved Portuguese edit through actual reopening. Actual native clipboard-image insertion/save and the pinned .287 product opening a copied workspace pass with exact source/image bytes and bundled runtime origins on the developer desktop. An independently reproduced web in-flight render/save-status race is corrected with status ownership; 268 local/hosted Chromium/Edge cases pass, including the queued-render control and unchanged delayed-draft assertions. The first acceptance native-push/web-PR failures remain in the report; semantic UIA menu invocation retains the original Updates window/trust assertions and deadline. Current live Excel paste is BLOCKED by a first-run global-settings modal; earlier Excel evidence is historical.

Later validation corrections recognise Win32 ERROR_TIMEOUT observations and require the real error Window plus a ready acknowledgement before one invocation. Original failing logs/screenshots remain, provider/child-readiness deadlines and source/Undo assertions stay unchanged, and all four final-validation runs pass. These are evidence-backed automation corrections; they are not represented as product data-loss fixes or physical certification. Installed-context diagnostics are included in the subsequent evidence upload reconciliation.

## Implemented outcomes

The table retains the design choices, test scope and explicit limits. Supported inherited Word formatting and bounded generated-text rejection now pass all four actual application contexts in both baseline runs. The private table grid and editable semantic labels retain source and whole Undo/Redo. [Earlier chronological evidence](history/native-experience-improvements-through-7008751.md) remains available.

| Improvement | Impact / risk | Implemented outcome and limit |
|---|---|---|
| Real workspaces and native pickers | High / low | Windows folders/files, native open/save dialogues, tabs, recents, Explorer reveal and drag/drop; portable source remains authoritative |
| Guarded writes and recoverable changes | High / medium | Fingerprints protect external edits; durable writes, original/planned journals, saved/unsaved snapshots and private draft recovery retain recoverable content |
| Reviewed file/folder moves | High / medium | Preserve confirmed edits, binary assets and empty folders; review ordinary/wiki references, refuse changed inventories and update tabs/history. File and folder case changes pass desktop round trips; raw HTML references remain explicit manual work |
| Folder monitoring without losing position | High / low | Debounced native tree reconciliation preserves surviving controls, selection/expansion and unsaved buffers. Seven real filesystem/WPF checks and desktop workflows pass |
| Source-aware navigation | High / medium | Native outline, headings/named anchors, exact source selection and bounded Back/Forward. Deferred native scrolling reveals actual text bounds, including hidden details and zoomed targets |
| Focus-aware Find and native commands | High / medium | Context-specific editor/preview search, editor replacement, keyboard focus return, routed context actions and complete undo groups pass native/desktop checks |
| Avoid unused preview work | High / low | Editor-only mode retains current headings without hidden image/diagram/document construction. Explicit text copy verifies current edits and clipboard ownership; four local outcomes and desktop workflow 53 pass |
| Responsive preparation | High / medium | Worker preparation and bounded frozen image/diagram caches; cancellable nested WPF construction. Construction gates pass, while separately measured 4,000-item continuous first layout delays input by about two seconds locally. Automatic mode already uses Pages for large sources; the continuous-mode defect and unresolved intermittent construction failure remain explicit |
| Native code, details, captions and maths | High / medium | Selectable syntax, accessible expanders, source-preserving caption/details dialogues and native formula drawings. Source-aware copy/search and real exports pass; unsupported forms retain source |
| Portable image persistence | High / medium | Persist draft/workspace assets before insertion, finalise relative paths during Save as and protect selection during asynchronous preparation |
| Explicit remote images | Medium / medium | User-triggered bounded HTTPS retrieval, native raster validation and local persistence; preview never fetches remote references automatically |
| Image management | High / medium | Native reference audit, reviewed renames, recoverable unused-asset removal and independent copied pixels; incomplete audits block removal |
| Rich native clipboard and paste | High / medium | Windows Unicode/CF_HTML, Markdown with portable images, formatted HTML/quoted TSV conversion and Paste Special preserve source and one Undo. Actual Word's named corrected cases pass; earlier Excel checks retain their historical scope. Current live Excel paste and Azure DevOps attachment acceptance are BLOCKED; local payload/CSV round-trips pass |
| Scoped table copy and CSV | High / low | Native table context actions isolate current content; literal Excel-cell metadata and guarded UTF-8 CSV retain identifiers, Unicode, quoting and source |
| Offline native Mermaid | High / medium | Native preview, independent zoom window and SVG/PNG output, with editable source and explicit invalid-input feedback. Visual inspection corrected inherited label contrast, transparent export backgrounds and missing standalone activation bars. Six native cases and 17 activation core cases pass; diagram-compatibility.md records exact supported forms and limits |
| Wiki links and completion | High / low | Unambiguous full-path suggestions retain aliases, source spans and Undo; duplicate names expose choices. Navigation, review, moves and exported sites stay workspace-bounded |
| Workspace maps and advisory review | High / low | Searchable current-draft links, exact source navigation, native diagrams and passive reports. Virtualised advisory notes retain line/column locations; partial/unreadable scans remain distinct |
| Word formatting and content libraries | High / medium | Private validated reusable templates, selection, rename and reversible archive/restore survive restart. Fresh DOCX packages retain supported styles, numbering and referenced media; complex multi-section merging remains limited |
| Safe explicit browser migration | High / medium | Content-library JSON, original Word-template copies and portable appearance JSON transfer without browser-profile scanning or replacing originals. Imported settings remain private until Apply |
| Native generators and session profiles | Medium / low | Searchable 32-template/example and 25-snippet catalogue with labelled editable fields, source previews and complete Undo; export profiles remain session choices |
| Portable local exports | High / medium | Independent DOCX/PDF/HTML, generic Markdown bundles and static Docs Sites include supported current content/assets, exclude private state and guard destination writes. Site search opens exact sections |
| Passive optional artefact context | Medium / low | Session-only imported metadata, accessible filters and explicit safe rebuilt review packs preserve supplied evidence labels. Generic bundles omit metadata; restart clears it |
| Native settings and offline help | High / low | Ctrl+, supplies durable appearance/reading preferences with Cancel and transfer; F1 provides twelve bundled searchable topics without changing source |
| Windows appearance and placement | High / low | Fluent WPF controls, neutral themes, high-contrast colours and native placement persistence. Long statuses now retain Cancel/caret space; Find/Replace wraps at narrow widths. Four hidden native layouts pass bounds/accessibility-name/source checks, with inspected light/dark output. Both f7dc5a3 desktop runs pass; broad screen-reader, DPI and multi-monitor certification remains |
| Distant editor navigation | High / low | Source/search/review/map targets are checked after wrapped virtual-line layout; stale source/documents/selections and superseded requests cannot scroll a replacement target. A 4.14 MB native fixture passes exact source/Undo and visible-position checks; real desktop search workflow 55 passes in both b45246e runs, with the final selected target visibly inside the editor |
| Isolated per-user installation | High / medium | Self-contained Inno wizard, optional shortcuts/Open with, files-in-use handling and retained user data coexist with the old shell; fresh-runner lifecycle passes, SDK-free clean-machine certification remains |
| Secure user-controlled updates | High / high | Product/channel/architecture policy, signed metadata, integrity/Authenticode checks and separate download/install consent. Production identities and positive signed-update certification remain external |

An embedded browser and proprietary document store were rejected because they do not meet the native and portability requirements. Measured WPF list subdivision offered no useful layout improvement and was removed. Automatic recovery/image pruning remains deferred to preserve recoverable data.

Earlier implementation decisions, run-specific results and failed scenarios remain intact in the [experience history through b3a4d59](history/native-experience-improvements-through-b3a4d59.md). Current package paths and next actions are in the [engineering handover](engineering-handover.md).
