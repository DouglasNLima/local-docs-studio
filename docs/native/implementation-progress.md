# Native implementation progress

Current gate: internal preview hardening. The authorised work remains continuous; no merge or public release is authorised. The .NET 10/WPF application is separate from the preserved browser app and WinUI/WebView2 shell.

Current validation change separates windowless MTA UIA calls from a pumping STA clipboard dispatcher. In-memory cross-apartment checks retain exact Unicode/HTML/custom bytes, frozen image pixels and unchanged originals without reading or writing the Windows clipboard. Build, focused checks and the full native integration harness pass; desktop verification and any connection to the timeout remain pending. Historical progress/validation entries have moved into linked history files so this record describes current acceptance.

| Reference | Current value |
|---|---|
| Develop baseline | 2cc3b5680958c9c553dad23856e6010c158fccd7 |
| Branch | feature/native-wpf-rebuild |
| Draft PR | [21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop |
| Latest implementation | 9fddfd9, Editor-only preview scheduling and current text copying |
| Latest complete native CI | 9fddfd9, PR/push 38203569347/38203565583: 726 core tests and 53 workflows in built, installed and reinstalled applications |
| Latest complete web CI | Both 9fddfd9 runs 38203569315/38203565517: all 262 cases |
| Current validation | Clean Release build, all 726 core tests, full native integration and static integrity pass locally; all 53 workflows pass in both 9fddfd9 native runs; separate validation-thread changes await CI |

Implemented workflows include native folders/tabs/history, guarded UTF-8 saves, external-change handling, recovery/snapshots, reviewed file/folder moves with case-only names, portable images, native editing/search, rich clipboard/paste, imports/exports, reusable Word/content libraries, browser migration, source-aware review and workspace maps. Optional artefact metadata stays passive and session-only. The parity matrix records the remaining limits for each contract.

The latest renderer change yields during nested lists, tables, quotes and details. Local first-input latency for a 4,000-item list improved from 322.6 ms to roughly 1–2 ms. Profiling identified mutable shared-brush tracking overhead; freezing those brushes reduced a diagnostic layout comparison from 2.1 seconds to 1.3 seconds. Final WPF layout remains synchronous and large-document certification is incomplete.

Editor-only layout now updates headings without preparing hidden diagrams, images, formulae or WPF document controls. Explicit rendered-text copying verifies the current document/source and clipboard sequence. Showing Split/Preview resumes rendering, and single-pane Find targets the visible control. Four native scheduling outcomes pass; workflow 53 passes actual clipboard, Undo and layout/search checks in both 9fddfd9 native runs. The actual Editor-only screenshot is retained in the PR run artefact.

Native push 38202782258 at d86a5ac repeats a wrapped UIA editor-provider timeout immediately after figure workflows despite a bounded observation retry. Preceding outcomes and native integration pass. Its failed report is retained; the cause is unresolved. Paired native PR 38202785031 reached installed lifecycle before the next push cancelled it; it is not counted as passed. New failure diagnostics capture the test-owned window through Win32 and native thread stacks independently of UIA. Full heap/environment dumps are not requested. Local foreground desktop access remains unavailable; actual keyboard/clipboard workflows run in Windows CI.

Next executable work:

1. Push the validation-thread correction and consolidated records, let both native and web runs finish, and inspect any failure diagnostics. All four 9fddfd9 runs have completed successfully.
2. Correct confirmed regressions and rerun the affected native/desktop workflows. Preserve failed evidence and avoid counting cancelled or failed runs as passed.
3. Continue measured responsiveness, supported export/diagram compatibility and accessibility acceptance; retain explicit unsupported forms and performance limits.
4. Complete SDK-free clean-Windows and positive signed-update certification when the required environment and authorised production trust are available. Do not provision production identities or publish a release without the required authority.

Build/install procedures are in [update and packaging](update-and-packaging.md). Current results and evidence locations are in [validation](validation-report.md); feature outcomes are in [functional parity](functional-parity-matrix.md). The detailed revision chronology is preserved in [implementation history](history/implementation-progress-through-9fddfd9.md).
