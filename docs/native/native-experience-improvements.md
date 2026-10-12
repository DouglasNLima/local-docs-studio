# Native experience improvements

The native reconstruction keeps portable Markdown and real filesystem ownership while improving desktop workflows. At 9fddfd9 all 53 workflows pass in built, installed and reinstalled applications in both native CI runs, including Editor-only scheduling. Validation-thread separation awaits its own desktop evidence. Failed parallel runs and remaining limits are recorded in the validation report.

| Improvement | Impact / risk | Implemented outcome and limit |
|---|---|---|
| Real workspaces and native pickers | High / low | Windows folders/files, native open/save dialogues, tabs, recents, Explorer reveal and drag/drop; portable source remains authoritative |
| Guarded writes and recoverable changes | High / medium | Fingerprints protect external edits; durable writes, original/planned journals, saved/unsaved snapshots and private draft recovery retain recoverable content |
| Reviewed file/folder moves | High / medium | Preserve confirmed edits, binary assets and empty folders; review ordinary/wiki references, refuse changed inventories and update tabs/history. File and folder case changes pass desktop round trips; raw HTML references remain explicit manual work |
| Folder monitoring without losing position | High / low | Debounced native tree reconciliation preserves surviving controls, selection/expansion and unsaved buffers. Seven real filesystem/WPF checks and desktop workflows pass |
| Source-aware navigation | High / medium | Native outline, headings/named anchors, exact source selection and bounded Back/Forward. Deferred native scrolling reveals actual text bounds, including hidden details and zoomed targets |
| Focus-aware Find and native commands | High / medium | Context-specific editor/preview search, editor replacement, keyboard focus return, routed context actions and complete undo groups pass native/desktop checks |
| Avoid unused preview work | High / low | Editor-only mode retains current headings without hidden image/diagram/document construction. Explicit text copy verifies current edits and clipboard ownership; four local outcomes and desktop workflow 53 pass |
| Responsive preparation | High / medium | Worker preparation and bounded frozen image/diagram caches; cancellable nested WPF construction. A 4,000-item list yields to input promptly, but final layout still costs about one second locally |
| Native code, details, captions and maths | High / medium | Selectable syntax, accessible expanders, source-preserving caption/details dialogues and native formula drawings. Source-aware copy/search and real exports pass; unsupported forms retain source |
| Portable image persistence | High / medium | Persist draft/workspace assets before insertion, finalise relative paths during Save as and protect selection during asynchronous preparation |
| Explicit remote images | Medium / medium | User-triggered bounded HTTPS retrieval, native raster validation and local persistence; preview never fetches remote references automatically |
| Image management | High / medium | Native reference audit, reviewed renames, recoverable unused-asset removal and independent copied pixels; incomplete audits block removal |
| Rich native clipboard and paste | High / medium | Windows Unicode/CF_HTML, Markdown with portable images, formatted HTML/TSV conversion and Paste Special preserve source and one Undo. Actual Word/Excel checks pass; live Azure DevOps attachment acceptance remains |
| Scoped table copy and CSV | High / low | Native table context actions isolate current content; literal Excel-cell metadata and guarded UTF-8 CSV retain identifiers, Unicode, quoting and source |
| Offline native Mermaid | High / medium | Native preview, independent zoom window and SVG/PNG output, with editable source and explicit invalid-input feedback. The measured compatibility corpus passes; broader constructs remain a gate |
| Wiki links and completion | High / low | Unambiguous full-path suggestions retain aliases, source spans and Undo; duplicate names expose choices. Navigation, review, moves and exported sites stay workspace-bounded |
| Workspace maps and advisory review | High / low | Searchable current-draft links, exact source navigation, native diagrams and passive reports. Virtualised advisory notes retain line/column locations; partial/unreadable scans remain distinct |
| Word formatting and content libraries | High / medium | Private validated reusable templates, selection, rename and reversible archive/restore survive restart. Fresh DOCX packages retain supported styles, numbering and referenced media; complex multi-section merging remains limited |
| Safe explicit browser migration | High / medium | Content-library JSON, original Word-template copies and portable appearance JSON transfer without browser-profile scanning or replacing originals. Imported settings remain private until Apply |
| Native generators and session profiles | Medium / low | Searchable 32-template/example and 25-snippet catalogue with labelled editable fields, source previews and complete Undo; export profiles remain session choices |
| Portable local exports | High / medium | Independent DOCX/PDF/HTML, generic Markdown bundles and static Docs Sites include supported current content/assets, exclude private state and guard destination writes. Site search opens exact sections |
| Passive optional artefact context | Medium / low | Session-only imported metadata, accessible filters and explicit safe rebuilt review packs preserve supplied evidence labels. Generic bundles omit metadata; restart clears it |
| Native settings and offline help | High / low | Ctrl+, supplies durable appearance/reading preferences with Cancel and transfer; F1 provides twelve bundled searchable topics without changing source |
| Windows appearance and placement | High / low | Fluent WPF controls, neutral themes, high-contrast colours and native placement persistence. Broad screen-reader, DPI and multi-monitor certification remains |
| Isolated per-user installation | High / medium | Self-contained Inno wizard, optional shortcuts/Open with, files-in-use handling and retained user data coexist with the old shell; fresh-runner lifecycle passes, SDK-free clean-machine certification remains |
| Secure user-controlled updates | High / high | Product/channel/architecture policy, signed metadata, integrity/Authenticode checks and separate download/install consent. Production identities and positive signed-update certification remain external |

An embedded browser and proprietary document store were rejected: neither meets the authorised native and portability requirements. WPF list subdivision into 128-item groups was measured and removed because it offered no useful layout improvement. Automatic recovery/image pruning remains deferred to avoid discarding recoverable user data.

These are implemented outcomes with stated evidence limits, not production certification. Detailed contracts and remaining gaps are in the functional parity matrix and validation report.
