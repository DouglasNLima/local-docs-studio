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

## Private interface preferences

`Desktop` adds sidebar visibility, bounded sidebar/outline widths and three independent opt-in synchronisation preferences. Missing desktop preferences use safe defaults. `ShowOutline`, theme, layout and reading mode keep their existing contracts. Focus Mode temporarily hides panels without rewriting these settings. Full Screen saves and restores native window placement, including the Windows monitor-placement fallback.

## Boundaries

UI operations must preserve source bytes, selection, undo history, draft recovery, relative links and image assets. Private interface settings remain in application state. Existing preferences load with safe defaults. Appearance changes merge into the latest preferences, preserving update consent and recent paths.

Continuous mode retains its existing warning and performance boundary. An explicit reading choice is never silently changed. Synchronisation must use source spans and revision guards; generated or transformed content receives semantic highlighting when literal character correspondence cannot be established.
