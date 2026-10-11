# Native experience improvements

| Improvement | Impact / risk | Decision / evidence |
|---|---|---|
| Real folders, native pickers, tabs | High / low | Implement; preserve source files |
| Conflict-safe writes, recovery journal | High / medium | Implement before relying on Save |
| Source-aware preview navigation | High / medium | Markdig spans, native WPF elements |
| Persisted relative images | High / medium | Write bytes successfully before inserting links |
| Focus-aware search | High / medium | Route Ctrl+F from focused pane; regression tests |
| Explorer, recent files, drag/drop | High / low | Implement through Windows APIs |
| Native DOCX/PDF, clipboard formats | High / medium | Inspect actual outputs and format contracts |
| Signed user-controlled updates | High / high | Implement trust boundary; production credentials external |
| Browser wrapper | Incompatible | Rejected by native-only requirement |
| Proprietary document store | Portability risk | Rejected; Markdown remains authoritative |

Implementation status is recorded in the parity matrix; decisions alone do not establish completion.
