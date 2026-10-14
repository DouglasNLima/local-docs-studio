# Native desktop UX modernisation

This implementation starts from `c566144529c3c0e4393c1604544201b936e4a6a2` on `release/native-preview-readiness`. Work is isolated on `feature/native-desktop-ux-modernisation`. It preserves .NET 10, WPF, AvalonEdit, Markdig and the native FlowDocument preview. The web edition and the rejected Continuous viewer experiment remain separate.

The baseline problems are wrapping document tabs, a verbose workspace header, a narrow outline, hidden formatting actions, inaccessible panel toggles and inconsistent command hierarchy. The work proceeds through visual foundation, workspace and tabs, productivity, independent source-aware synchronisation, viewing controls and final certification.

## Evidence and status

- The unmodified Core baseline passed locally: 1,057 passed, zero failures and zero skipped tests.
- Existing native validation at the exact base passed in [run 38530939885](https://github.com/DouglasNLima/local-docs-studio/actions/runs/38530939885).
- New-build desktop and installer validation runs only on disposable GitHub-hosted Windows. No new application is installed or exercised on the user's ordinary desktop.
- Screenshot dimensions, theme, source identity and physical display bounds are recorded together. UI Automation evidence does not constitute physical accessibility or DPI certification.
- The overall implementation remains active. Feature, synchronisation, package and final CI evidence will be recorded here as those checks complete.

## Boundaries

UI operations must preserve source bytes, selection, undo history, draft recovery, relative links and image assets. Private interface settings remain in application state. Existing preferences load with safe defaults. Appearance changes merge into the latest preferences, preserving update consent and recent paths.

Continuous mode retains its existing warning and performance boundary. An explicit reading choice is never silently changed. Synchronisation must use source spans and revision guards; generated or transformed content receives semantic highlighting when literal character correspondence cannot be established.
