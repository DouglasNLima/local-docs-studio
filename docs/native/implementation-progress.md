# Native implementation progress

Current gate: internal preview hardening. The authorised work remains continuous; no merge or public release is authorised. The .NET 10/WPF application is separate from the preserved browser app and WinUI/WebView2 shell.

The current shell correction keeps Cancel visible when status messages are long and wraps Find/Replace controls at narrow widths. Hidden native windows pass minimum/normal-size checks in both themes; 100/150/200% raster output is retained and inspected. A real desktop resize/replacement/Undo regression is added for the next CI run. Historical progress/validation entries remain in linked history files.

| Reference | Current value |
|---|---|
| Develop baseline | 2cc3b5680958c9c553dad23856e6010c158fccd7 |
| Branch | feature/native-wpf-rebuild |
| Draft PR | [21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop |
| Latest implementation | Minimum-size shell controls and regression coverage, following ce3cf9f validation-thread separation |
| Latest complete native CI | ce3cf9f, PR/push 38204610723/38204607899: 726 core tests and 53 workflows in built, installed and reinstalled applications |
| Latest complete web CI | Both ce3cf9f runs 38204610690/38204607927: all 262 cases |
| Current validation | Clean Release build, full native integration and static integrity pass locally; shell geometry checks pass; new desktop workflow 54 awaits CI |

Implemented workflows include native folders/tabs/history, guarded UTF-8 saves, external-change handling, recovery/snapshots, reviewed file/folder moves with case-only names, portable images, native editing/search, rich clipboard/paste, imports/exports, reusable Word/content libraries, browser migration, source-aware review and workspace maps. Optional artefact metadata stays passive and session-only. The parity matrix records the remaining limits for each contract.

The latest renderer change yields during nested lists, tables, quotes and details. Local first-input latency for a 4,000-item list improved from 322.6 ms to roughly 1–2 ms. Profiling identified mutable shared-brush tracking overhead; freezing those brushes reduced a diagnostic layout comparison from 2.1 seconds to 1.3 seconds. Final WPF layout remains synchronous and large-document certification is incomplete.

Editor-only layout now updates headings without preparing hidden diagrams, images, formulae or WPF document controls. Explicit rendered-text copying verifies the current document/source and clipboard sequence. Showing Split/Preview resumes rendering, and single-pane Find targets the visible control. Four native scheduling outcomes pass; workflow 53 passes actual clipboard, Undo and layout/search checks in both 9fddfd9 native runs. The actual Editor-only screenshot is retained in the PR run artefact.

Native push 38202782258 at d86a5ac repeats a wrapped UIA editor-provider timeout immediately after figure workflows despite a bounded observation retry. Its failed report is retained; the cause is unresolved. Paired native PR 38202785031 was cancelled and is not counted as passed. At ce3cf9f the client separates windowless MTA automation from pumping STA clipboard operations, with exact in-memory format/pixel checks. All desktop contexts pass in both ce3cf9f runs, without proving the earlier timeout's cause. Failure diagnostics capture the test-owned window through Win32 and native thread stacks independently of UIA; no full heap/environment dump is requested. Local foreground desktop access remains unavailable.

Next executable work:

1. Push the minimum-size shell correction and let its native/web runs finish. Inspect actual desktop screenshots and any failure diagnostics; all four ce3cf9f runs have completed successfully.
2. Correct confirmed regressions and rerun the affected native/desktop workflows. Preserve failed evidence and avoid counting cancelled or failed runs as passed.
3. Continue measured responsiveness, supported export/diagram compatibility and accessibility acceptance; retain explicit unsupported forms and performance limits.
4. Complete SDK-free clean-Windows and positive signed-update certification when the required environment and authorised production trust are available. Do not provision production identities or publish a release without the required authority.

Build/install procedures are in [update and packaging](update-and-packaging.md). Current results and evidence locations are in [validation](validation-report.md); feature outcomes are in [functional parity](functional-parity-matrix.md). The detailed revision chronology is preserved in [implementation history](history/implementation-progress-through-9fddfd9.md).
