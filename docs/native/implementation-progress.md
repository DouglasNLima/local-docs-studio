# Native implementation progress

Current gate: internal preview hardening. The authorised work remains continuous; no merge or public release is authorised. The .NET 10/WPF application is separate from the preserved browser app and WinUI/WebView2 shell.

The current correction reveals distant editor targets after wrapped virtual-line layout. A 4.14 MB fixture reproduced a final target below the viewport; deferred navigation now verifies actual positions and rejects stale source, replacement documents, changed selections and superseded requests. Both native CI runs pass all 55 built/installed/reinstalled workflows, and both browser runs pass all 262 cases. Actual minimum-window and long-editor screenshots have been inspected. Historical progress/validation entries remain in linked history files.

| Reference | Current value |
|---|---|
| Develop baseline | 2cc3b5680958c9c553dad23856e6010c158fccd7 |
| Branch | feature/native-wpf-rebuild |
| Draft PR | [21](https://github.com/DouglasNLima/local-docs-studio/pull/21), targeting develop |
| Latest implementation | Deferred long-document editor navigation and measured filesystem/editor/export workloads, following f7dc5a3 minimum-size controls |
| Latest complete native CI | b45246e, PR/push 38206432086/38206428005: 726 core tests and 55 workflows in built, installed and reinstalled applications |
| Latest complete web CI | Both b45246e runs 38206432137/38206427996: all 262 cases |
| Current validation | Clean Release build and full native integration pass locally; all four b45246e CI runs pass; actual search/minimum-window output and application timing samples inspected |

Implemented workflows include native folders/tabs/history, guarded UTF-8 saves, external-change handling, recovery/snapshots, reviewed file/folder moves with case-only names, portable images, native editing/search, rich clipboard/paste, imports/exports, reusable Word/content libraries, browser migration, source-aware review and workspace maps. Optional artefact metadata stays passive and session-only. The parity matrix records the remaining limits for each contract.

The latest renderer change yields during nested lists, tables, quotes and details. Local first-input latency for a 4,000-item list improved from 322.6 ms to roughly 1–2 ms. Profiling identified mutable shared-brush tracking overhead; freezing those brushes reduced a diagnostic layout comparison from 2.1 seconds to 1.3 seconds. Final WPF layout remains synchronous and large-document certification is incomplete.

Editor-only layout now updates headings without preparing hidden diagrams, images, formulae or WPF document controls. Explicit rendered-text copying verifies the current document/source and clipboard sequence. Showing Split/Preview resumes rendering, and single-pane Find targets the visible control. Four native scheduling outcomes pass; workflow 53 passes actual clipboard, Undo and layout/search checks in both 9fddfd9 native runs. The actual Editor-only screenshot is retained in the PR run artefact.

Minimum-size controls pass four hidden light/dark layouts and the full installer lifecycle. Long-editor measurements use 1,001 real documents, 20,000 paragraphs, three filesystem/parser samples and 15 visible native insertion/Undo samples. The focused run observes 53–64 ms enumeration, 5.6–11.3 ms reads, 180–517 ms parsing, 273 ms initial editor layout and 0.3–1.6 ms insertion/layout. The b45246e PR's actual application samples observe 2.63 s launch-to-preview, 636 ms keyboard delivery/observation, 103 ms save and 124 ms small-image paste; these include UIA/polling overhead and are not cold-cache or universal guarantees.

Scoped security review is in progress at b45246e (scan 5ccf4c42-57c8-4923-ac76-02084b4c0bbc), with 63 of 309 native files reviewed. Update/activation and workspace/recovery/image groups have no established reportable finding. Import review has source-validated a medium-severity DOCX issue: different relationship IDs for one image retain duplicate full image arrays without an aggregate limit. Local dependency inspection confirms that alias path; no runtime reproducer was executed. Correction and the remaining source review are outstanding. This is a sequential review without an independent sub-agent audit; partial coverage is not security certification.

Native push 38202782258 at d86a5ac repeats a wrapped UIA editor-provider timeout immediately after figure workflows despite a bounded observation retry. Its failed report is retained; the cause is unresolved. Paired native PR 38202785031 was cancelled and is not counted as passed. At ce3cf9f the client separates windowless MTA automation from pumping STA clipboard operations, with exact in-memory format/pixel checks. All desktop contexts pass in both ce3cf9f runs, without proving the earlier timeout's cause. Failure diagnostics capture the test-owned window through Win32 and native thread stacks independently of UIA; no full heap/environment dump is requested. Local foreground desktop access remains unavailable.

Next executable work:

1. Complete the remaining scoped security review and validate any source-backed candidates before reporting findings. Preserve the current partial checkpoint and its limitations.
2. Correct confirmed regressions and rerun the affected native/desktop workflows. Preserve failed evidence and avoid counting cancelled or failed runs as passed.
3. Continue measured responsiveness, supported export/diagram compatibility and accessibility acceptance; retain explicit unsupported forms and performance limits.
4. Complete SDK-free clean-Windows and positive signed-update certification when the required environment and authorised production trust are available. Do not provision production identities or publish a release without the required authority.

Build/install procedures are in [update and packaging](update-and-packaging.md). Current results and evidence locations are in [validation](validation-report.md); feature outcomes are in [functional parity](functional-parity-matrix.md). The detailed revision chronology is preserved in [implementation history](history/implementation-progress-through-9fddfd9.md).
