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
| Images before the first save | High / medium | Persist PNG assets with draft recovery and finalise portable paths during Save; source anchors protect edits made while loading |
| Native DOCX/PDF, clipboard formats | High / medium | Inspect actual outputs and format contracts |
| Signed user-controlled updates | High / high | Integrated separate check/download/install controls and native settings; policy tests pass; production credentials and signed upgrade certification external |
| Per-user installer, coexistence and files in use | High / medium | Separate product identity, optional Open with, graceful-close mutex; actual install/uninstall/reinstall validated on development host |
| Browser wrapper | Incompatible | Rejected by native-only requirement |
| Proprietary document store | Portability risk | Rejected; Markdown remains authoritative |

Implementation status is recorded in the parity matrix; decisions alone do not establish completion.
