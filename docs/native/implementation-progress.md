# Native implementation progress

Current gate: internal preview hardening. The authorised work remains continuous; no merge or public release is authorised. The .NET 10/WPF application is separate from the preserved browser app and WinUI/WebView2 shell.

All four 45b5d5d CI runs pass: 778 core tests, seven actual Windows signature checks, 55 workflows in built/installed/upgraded/reinstalled applications, eight exported-site cases and 262 browser cases. The simulated preview upgrade verifies installed versions and retained state/workspace bytes. Earlier certificate stalls and the 2ea009b Save-field observation failure remain recorded; the current bounded fixture and field-readiness checks pass. The link correction has six native outcomes and an inspected PDF. Corrected diagram output and exact supported forms are recorded in the [diagram compatibility report](diagram-compatibility.md).

| Reference | Current value |
|---|---|
| Develop baseline | 2cc3b5680958c9c553dad23856e6010c158fccd7 |
| Branch | feature/native-wpf-rebuild |
| Draft PR | [21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop |
| Latest implementation | Word code colours using shared native tokens, editable tabs/exact line breaks and safe complete plain fallback; one actual Export-menu workflow added for CI |
| Latest complete native CI | 45b5d5d, PR/push 38217770147/38217763979: 778 core tests, seven signature cases and 55 workflows in built, installed, upgraded and reinstalled applications |
| Latest complete web CI | Both 45b5d5d runs 38217770149/38217763858: all 262 cases in 10.5/7.4 minutes |
| Current validation | Word correction: clean Release build, all 788 core tests, ten focused core/five native Word outcomes, complete native harness (including PDF/preview checks; 40 corpus cases, zero failures) and static integrity pass. The new actual Export-menu workflow awaits CI; Word destination rendering for the new colours is unverified |

Implemented workflows include native folders/tabs/history, guarded UTF-8 saves, external-change handling, recovery/snapshots, reviewed file/folder moves with case-only names, portable images, native editing/search, rich clipboard/paste, imports/exports, reusable Word/content libraries, browser migration, source-aware review and workspace maps. Optional artefact metadata stays passive and session-only. The parity matrix records the remaining limits for each contract.

The latest renderer change yields during nested lists, tables, quotes and details. Local first-input latency for a 4,000-item list improved from 322.6 ms to roughly 1–2 ms. Profiling identified mutable shared-brush tracking overhead; freezing those brushes reduced a diagnostic layout comparison from 2.1 seconds to 1.3 seconds. Final WPF layout remains synchronous and large-document certification is incomplete.

Editor-only layout now updates headings without preparing hidden diagrams, images, formulae or WPF document controls. Explicit rendered-text copying verifies the current document/source and clipboard sequence. Showing Split/Preview resumes rendering, and single-pane Find targets the visible control. Four native scheduling outcomes pass; workflow 53 passes actual clipboard, Undo and layout/search checks in both 9fddfd9 native runs. The actual Editor-only screenshot is retained in the PR run artefact.

Minimum-size controls pass four hidden light/dark layouts and the full installer lifecycle. Long-editor measurements use 1,001 real documents, 20,000 paragraphs, three filesystem/parser samples and 15 visible native insertion/Undo samples. The focused run observes 53–64 ms enumeration, 5.6–11.3 ms reads, 180–517 ms parsing, 273 ms initial editor layout and 0.3–1.6 ms insertion/layout. The b45246e PR's actual application samples observe 2.63 s launch-to-preview, 636 ms keyboard delivery/observation, 103 ms save and 124 ms small-image paste; these include UIA/polling overhead and are not cold-cache or universal guarantees.

Scoped security scan 5ccf4c42-57c8-4923-ac76-02084b4c0bbc is sealed for all 309 tracked native files at b45246e. Two source-validated findings concern DOCX relationship-alias allocations and repeated export images. Final severity is low: explicit user action is required and the established effect is local availability. No memory-exhaustion exploit was executed. The present correction passes small-budget regression tests, including a real 64-alias package, without attempting exhaustion. This was a sequential source review without an independent sub-agent audit; it is not production certification. See the validation report for limits and evidence.

Native push 38202782258 at d86a5ac repeats a wrapped UIA editor-provider timeout immediately after figure workflows despite a bounded observation retry. Its failed report is retained; the cause is unresolved. Paired native PR 38202785031 was cancelled and is not counted as passed. At ce3cf9f the client separates windowless MTA automation from pumping STA clipboard operations, with exact in-memory format/pixel checks. All desktop contexts pass in both ce3cf9f runs, without proving the earlier timeout's cause. Failure diagnostics capture the test-owned window through Win32 and native thread stacks independently of UIA; no full heap/environment dump is requested. Local foreground desktop access remains unavailable.

Next executable work:

1. Complete CI for the Word syntax/whitespace correction, including the new native Export-menu workflow. Local Release, all 788 core tests, five native Word outcomes, the complete native harness and static integrity pass. PDF correction ade57cd has completed local validation with inspected pages and is running CI. The docs-only 8b44f23 revision passes all four CI runs (native 38220244442/38220238385; web 38220244488/38220238388), with full logs retained under artifacts/native. The downloaded 45b5d5d result confirms matching upgraded versions, 20 preserved state files/workspace bytes and certificate cleanup. Earlier failed evidence remains retained. No local certificate-store mutation, Office activation or foreground interaction is authorised by the current validation setup.
2. Correct any further confirmed regressions. Preserve failed evidence and avoid counting cancelled or failed runs as passed.
3. Continue measured responsiveness, supported export/diagram compatibility and accessibility acceptance; retain explicit unsupported forms and performance limits.
4. Complete SDK-free clean-Windows and positive signed-update certification when the required environment and authorised production trust are available. Do not provision production identities or publish a release without the required authority.

Build/install procedures are in [update and packaging](update-and-packaging.md). Current results and evidence locations are in [validation](validation-report.md); feature outcomes are in [functional parity](functional-parity-matrix.md). The detailed revision chronology is preserved in [implementation history](history/implementation-progress-through-9fddfd9.md).
