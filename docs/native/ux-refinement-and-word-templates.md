# Native UX refinement and Word templates

Work in progress on `feature/native-ux-refinement`, based on verified native modernisation commit `5a373a412e86b93ea8af1b9e5b867250e3d23ee1`. This is an unsigned internal-preview implementation and certification effort. Existing PRs, releases, installed applications and the separate static web edition remain protected.

## Implementation and acceptance ledger

| Milestone | Status |
|---|---|
| A — attached-template compatibility | Implemented; 52 focused Word tests pass, including the new passive-reference and rejecting-resource cases |
| B — template library inspection and empty state | Implemented; native control/summary integration passes; hosted UIA pending |
| C — file icons and workspace closure | Implemented; case-insensitive mapping, property notifications, actual filesystem reconciliation and closed-session restoration pass |
| D — shared dialogue form layout | Implemented; label/target geometry, filtering and fixed footer checks pass locally; hosted audit pending |
| E — export completion actions and session history | Implemented; receipts, cancellation/failure, bounded history and stale-byte rejection pass; shell/hosted acceptance pending |
| F — adaptive application header | Implemented; real control geometry passes five sizes and the measured transition in both directions; hosted keyboard checks pending |
| G — document location, Settings search and polish | Implemented; native controls and staged Settings checks pass; hosted interactions pending |
| H — complete regression, visual acceptance and packages | In progress; 1,137 Core tests and 40 native rendering cases pass; browser/hosted/package certification pending |

## Word compatibility and security

The original service rejected every external resource relationship, including Word's passive attached-template metadata in the document settings part. It now accepts exactly one matching external `attachedTemplate` relationship belonging to the actual settings part and one corresponding direct settings reference. Neither target resolution nor target I/O is performed. The relationship and settings reference are excluded when reusable formatting is extracted into a fresh package. A non-blocking warning explains the omission without displaying the target.

Relationship XML is checked before SDK loading for duplicate/missing identifiers, malformed metadata and unsupported external types. Inconsistent, misplaced, internal and duplicated attached-template references are rejected. Existing safe ordinary hyperlinks retain their existing policy. Other external media, OLE, alternate content and unknown resource relationships remain blocked. ZIP path/entry/CRC/size, XML, macro, field, retained-resource and generated-package validations remain in force.

Formatting extraction retains supported styles, numbering, themes, the final section's supported page settings, inherited header/footer references and supported embedded static resources. It excludes the original body, unrelated personal metadata and unsupported structures. It does not reproduce cover designs or promise complete page fidelity.

The privately supplied original DOCX was independently exercised through an isolated copy: the baseline rejected it; the corrected importer accepted it; input bytes remained unchanged; no attached-template relationship remained. The private document, retained content and target paths are excluded from source control, hosted CI and public evidence. Further library/export acceptance is recorded as it is executed.

## Validation and remaining work

## Native behaviour

The template library inspects the validated retained package asynchronously. Its summary lists bounded style names, heading styles, numbering definitions, page size/margins, headers/footers, embedded images and original import notes. The typography sample uses an allowlist of local font families and clamped sample sizes; imported font strings cannot become WPF font resource URIs. Existing library records and integrity checks are unchanged. Empty and archived-empty states explain import and original-byte preservation.

Workspace identities are `.md`/`.markdown` → Markdown page cue; `.mmd`/`.mermaid` → connected nodes; `.txt` → lined page; images → image geometry; folders → existing folder; fallback → generic document. Shapes use inherited foregrounds, including selected and Windows high-contrast states. Mutable paths notify both icon and accessible description. Enumeration retains its existing supported-file scope.

Close Workspace stops and detaches monitoring before clearing the root, tree and filter. Existing queued callbacks fail their watcher-identity guard. Tabs retain their individual roots, snapshots, draft assets, source and Undo; session persistence records a null visible workspace while retaining those document records. Recent workspaces remain available. The action is shared by File, workspace toolbar and Command Palette.

Each successful file export creates an immutable receipt for the actual saved destination. Actions verify membership in the session history, safe output type, ordinary filesystem path/reparse boundaries, length and SHA-256 before passing a filename to the Windows shell or a quoted selection to the Windows Explorer executable. No external-command field is interpreted. Failed/cancelled writes do not publish a receipt. New status events and dismissal hide current actions; the newest ten distinct outputs remain in memory under Export → Recent exports. DOCX/PDF/HTML, bundles, Docs Site/review ZIPs, CSV, Mermaid SVG/PNG and configuration/library/template-copy exports share this completion model.

The adaptive panel measures the existing menu and full toolbar. If their widths plus the compact brand icon and spacing fit, it arranges them on one row; otherwise the toolbar occupies the next row with its existing overflow. Controls are neither duplicated nor reparented, and layout does not request a document render. Headless geometry checks cover 860×540, 1280×780 (retained regression), 1280×800, 1440×900 and 1920×1080, plus the measured boundary ±1 in both directions. Native hosted keyboard/focus validation remains a separate gate.

Shared form resources define labels, help, field spacing, sections, validation, progress and keyboard-accessible footer actions. Settings uses vertical fields and a scrolling body with a fixed footer. Search hides existing fields rather than recreating them and retains unsaved values. Workspace visibility and independent synchronisation use the existing desktop preference fields, atomically merged only on Apply. Appearance transfer keeps its existing contract. Docs Site and image figure forms now scroll and resize; document-link labels/footer, table alignment, workspace-asset footer and update-channel layout receive targeted corrections. Other inspected list/review/library windows retain their established scrolling structures.

The active tab exposes a concise relative location and full-path tooltip. Workspace, filter, outline and recent-export empty states provide concise guidance. Cancel actions appear only for active operations; output names and long errors keep full accessible status/tooltip text without expanding the footer.

## Evidence in progress

The unmodified base passed **1,086 Core tests**, with zero failures/skips. The first compatibility change passed **52 focused Word tests**, with zero failures/skips. Tests include file/UNC/HTTP/HTTPS targets, a listening loopback endpoint with no connection, an exclusively locked local target, byte preservation, retained formatting and generated DOCX schema checks, malformed references and unsafe external resources. Existing tests retain embedded image, macro, package-size and cancellation coverage.

Current local evidence: **1,137 Core tests, zero failures/skips**; **40 native diagram cases and export checks, zero failures**; complete native solution builds with zero warnings/errors; static integrity passes. Workspace tests exercise externally renamed files, pending notifications, dirty tabs with relative images, null-root restart, reopen and a different workspace, without mutating existing document bytes. Settings tests verify actual label/target geometry, staged values across filters and Cancel/Apply. Shell/control captures were inspected locally; they are hidden-window/raster evidence and do not replace the hosted desktop captures.

Initial compatibility push/PR web and visual runs passed. The initial PR native run [38719464153](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38719464153) failed its existing 15-second static-server startup deadline after Core/rendering checks passed. The corresponding branch passed that stage. Original failure logs are retained locally and in Actions; server diagnostics are now enabled without changing its deadline or retrying unchanged assertions. Final-source runs supersede initial compatibility evidence.

Windows GUI checks and installers run only on disposable hosted Windows infrastructure. No newly built app or installer is launched on the ordinary desktop. Full browser, hosted visual/installation and final package results remain pending; this report does not yet certify preview readiness. Physical DPI/assistive technology and production signing remain outside the hosted evidence. Continuous retains the existing **250 ms** diagnostic target and its documented performance limitation.
