# Native desktop UX modernisation

This implementation starts from `c566144529c3c0e4393c1604544201b936e4a6a2` on `release/native-preview-readiness`. Work is isolated on `feature/native-desktop-ux-modernisation`. It preserves .NET 10, WPF, AvalonEdit, Markdig and the native FlowDocument preview. The web edition and the rejected Continuous viewer experiment remain separate.

The baseline problems are wrapping document tabs, a verbose workspace header, a narrow outline, hidden formatting actions, inaccessible panel toggles and inconsistent command hierarchy. The work proceeds through visual foundation, workspace and tabs, productivity, independent source-aware synchronisation, viewing controls and final certification.

## Evidence and status

- The unmodified Core baseline passed locally: 1,057 passed, zero failures and zero skipped tests.
- Existing native validation at the exact base passed in [run 38530939885](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38530939885).
- New-build desktop and installer validation runs only on disposable GitHub-hosted Windows. No new application is installed or exercised on the user's ordinary desktop.
- Screenshot dimensions, theme, source identity and physical display bounds are recorded together. UI Automation evidence does not constitute physical accessibility or DPI certification.
- The overall implementation remains active. Feature, synchronisation, package and final CI evidence will be recorded here as those checks complete.

The first implementation provides shared neutral shell styles, original vector command artwork, a seven-resolution ICO generated from its SVG source, application/executable/installer branding, grouped toolbars with native overflow, a single-row scrollable tab strip, independent resizable panels, a concise workspace name, a formatting toolbar, Quick Open, Command Palette, a quick theme menu, Full Screen, Focus Mode and a contextual Cancel control.

The local native solution builds without warnings. The expanded Core suite passes 1,075 tests. A separate headless WPF check verifies both mapping directions for all repeated occurrences, literal Unicode, links, inline code and emphasis. Formatting delimiters alone are rejected as exact literal selections, and pointers from another FlowDocument cannot map into the current result. Live synchronisation is still being implemented; these mapping checks establish its foundation.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+P | Quick Open by workspace filename or relative path |
| Ctrl+Shift+P | Command Palette |
| Ctrl+Alt+B | Toggle workspace |
| Ctrl+Alt+O | Toggle outline |
| Ctrl+Tab / Ctrl+Shift+Tab | Next / previous document |
| F11 | Full Screen |
| Ctrl+Alt+Z | Focus Mode |
| Escape | Close search first, then exit Full Screen or Focus Mode |

The established shortcuts, including Ctrl+B for bold, Ctrl+I for italic, Ctrl+S to save and Ctrl+Shift+K for workspace links, remain available.

Workspace filenames and relative paths can now be filtered without reading document contents or replacing surviving tree nodes. Expand/collapse all, refresh, file/folder icons, local edit indicators and full path tooltips share the new command surfaces. The workspace context menu uses the item actually clicked, validates its captured workspace before execution and reuses reviewed file/folder moves. File deletion retains the existing confirmation, snapshots, conflict handling and Windows recycling. Root mutation and unsupported folder deletion are disabled; a safe folder-deletion path is still outstanding.

Tabs provide middle-click close, close others, close to the right, copy/reveal and pins. Pinned tabs must be unpinned before normal closing, and bulk close skips pins. Each dirty-document prompt retains its existing save/cancel behaviour; a cancelled close ends the remaining bulk operation. Pin order and the active document are restored from private session state. Outline titles are indented by heading level, searchable and available in full through tooltips; caret navigation updates the current section without rebuilding the preview.

Windowless workspace tests verify filtering, retained tree nodes/expansion, protected pins, active-document survival, session restoration and unchanged source bytes. Hosted visual evidence is available from [run 38548452142](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38548452142). Its temporary display change enabled the requested native screenshot sizes without changing persistent display settings. Review found active-tab visibility and dark overflow issues, corrected in the following checkpoint. Full native CI remains pending after correcting resource loading in headless shell construction.

## Private interface preferences

`Desktop` adds sidebar visibility, bounded sidebar/outline widths and three independent opt-in synchronisation preferences. Missing desktop preferences use safe defaults. `ShowOutline`, theme, layout and reading mode keep their existing contracts. Focus Mode temporarily hides panels without rewriting these settings. Full Screen saves and restores native window placement, including the Windows monitor-placement fallback.

## Boundaries

UI operations must preserve source bytes, selection, undo history, draft recovery, relative links and image assets. Private interface settings remain in application state. Existing preferences load with safe defaults. Appearance changes merge into the latest preferences, preserving update consent and recent paths.

Continuous mode retains its existing warning and performance boundary. An explicit reading choice is never silently changed. Synchronisation must use source spans and revision guards; generated or transformed content receives semantic highlighting when literal character correspondence cannot be established.
