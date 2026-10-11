# Native experience improvements

| Improvement | Impact / risk | Decision / evidence |
|---|---|---|
| Real folders, native pickers, tabs | High / low | Implement; preserve source files |
| Conflict-safe writes, recovery journal | High / medium | Implement before relying on Save |
| Source-aware preview navigation | High / medium | Markdig spans, native WPF elements |
| Responsive diagram preview and zoom | High / medium | Background STA conversion, bounded frozen-drawing cache and separate native zoom window; corpus and UI checks pass |
| Windows theme and window placement | High / low | Fluent WPF controls, system/high-contrast colours, working-area sizing and native placement persistence; broader DPI validation remains |
| Persisted relative images | High / medium | Write bytes successfully before inserting links |
| Focus-aware search | High / medium | Native UI tests validate editor and preview contexts, focus return and actual typing |
| Explorer, recent files, drag/drop | High / low | Implement through Windows APIs |
| Document links and navigation history | High / low | Native filtered document/heading picker, portable encoded references, recent lists and Back/Forward; actual UI checks pass |
| Reviewed rename and move | High / medium | Preview incoming/outgoing Markdown link changes, refuse collisions/external edits, retain recovery text and roll back guarded writes; core failure tests and native rename UI pass |
| Recoverable deletion and snapshots | High / medium | Retain saved/unsaved text and local image copies, inspect snapshots in a native browser, restore as a new draft and explicitly choose the saved destination; actual deletion and recovery UI pass |
| Images before the first save | High / medium | Persist PNG assets with draft recovery and finalise portable paths during Save; source anchors protect edits made while loading |
| Explicit image URL insertion | Medium / medium | HTTPS download only after the user's command, bounded transfer and redirects, native raster validation and portable persistence before insertion; transport and real image decoding checks pass |
| Copy rendered images between documents | High / low | Native image context menu writes pixels to the Windows clipboard; paste/save persists an independent asset. Actual desktop automation checks both saved image files |
| Workspace image management | High / medium | Native preview/reference list, reviewed Markdown reference updates and removal with exact binary recovery copies. Incomplete audits block removal; HTML references block automatic rename. Core checks and 18-outcome desktop/installed CI pass at 05e9fec |
| Native DOCX/PDF, clipboard formats | High / medium | Actual outputs and CF_HTML contracts pass. Word destination checks preserve Unicode, bold, tables and original diagram pixels; Excel preserves cells, leading zeroes and formula-like text using fixed literal-cell metadata. Wider destination coverage remains |
| Markdown and images in one copy | High / medium | Preserve literal source, positions and repeated images; replace completed Mermaid blocks with PNGs. Native automatic paste persists portable assets before replacing source. Core, saved-pixel integration and 22-outcome desktop/installed CI pass at dd25fb0; Azure DevOps attachment validation remains |
| Formatted paste and Paste Special | High / medium | Prefer rich HTML over a fallback bitmap, preserve literal source when requested, flatten merged spreadsheet cells explicitly and persist images before replacing selections. Ctrl+Shift+V and failed-paste/Undo preservation pass desktop and installed CI at 7ad9a13 |
| Image-heavy preview responsiveness | High / medium | Move file reads, hashing and raster decoding off the UI thread; reuse frozen pixels by content hash, bound aggregate/cached memory and serialise cancelled preparation. Native integration checks pass; full layout optimisation remains |
| Source-faithful native text layout | High / low | Label callouts, preserve ordered starts including zero, table alignment and independent emphasis. Native light/dark page images and document-object checks pass; Word schema/content and extracted PDF checks cover supported output semantics |
| Signed user-controlled updates | High / high | Integrated separate check/download/install controls and native settings; policy tests pass; production credentials and signed upgrade certification external |
| Per-user installer, coexistence and files in use | High / medium | Separate product identity, optional Open with, graceful-close mutex; actual install/uninstall/reinstall validated on development host |
| Browser wrapper | Incompatible | Rejected by native-only requirement |
| Proprietary document store | Portability risk | Rejected; Markdown remains authoritative |

Implementation status is recorded in the parity matrix; decisions alone do not establish completion.
