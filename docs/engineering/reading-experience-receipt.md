# Reading experience and productivity — engineering receipt

Status: implementation in progress. This document is an engineering checkpoint, not publication acceptance.

## Baseline and boundaries

- Goal: `01a1b05e-1f98-7fe2-9aca-a6064536894d`, one consolidated Draft PR targeting `develop`.
- Initial checkout: `main`, commit `68f1dc955b27a47347a6fccc52a463472b601a1b`, tree `7847c00786aba15bc3d29746a9a983f8b70b9690`. Untracked `.codex-remote-attachments/` retained untouched.
- Fetched `develop`: `99349d82eb06960eb1f9785cf384db2373aed4fa`, tree `e4d137a12cbdd827583fcb841612890dd88736ec`. Baseline runs in a separate detached worktree so implementation cannot change its test inputs.
- Feature branch: `feature/reading-experience-productivity`, created from that exact baseline after static validation and a successful native build.
- Root and native `AGENTS.md` read; no further nested agent instructions exist at this baseline.
- Integrated corrections inspected: [Find #72](https://github.com/DouglasNLima/local-docs-studio/pull/72) and [selection #74](https://github.com/DouglasNLima/local-docs-studio/pull/74). Open work #69–#71 and #73 is not integrated by this task.
- [Markdown Viewer](https://github.com/ThisIs-Developer/Markdown-Viewer) consulted for functional inspiration only. No source copied and no dependency added. Scoped MIT and existing notices remain applicable.
- No merge, release, tag, deployment, production signing, updater execution or branch-protection changes are authorised.

## Checkpoints and dependencies

1. A — record baseline, architecture and existing failures; run static, browser and Core checks.
2. B — preview-independent readable-buffer statistics, true persistence state, existing Review entry, automated Web/Core/native control checks; commit.
3. C — extend existing outlines with hierarchy and configurable Copy TOC using renderer anchors, preserve per-document expansion; commit.
4. D — contextual slash commands routed through existing insertion services, one undo operation, keyboard/IME/read-only guards; commit.
5. E — document comparison with per-document buffers and pane-local presentation, focus, Find and scroll; commit.
6. F — final regression, architecture/security review, documentation and exact-commit evidence, one Draft PR, terminal Actions inspection.

Statistics depend on buffer lifecycle, not preview. Outline and TOC share heading identities. Slash commands depend on existing edit transactions. Comparison depends on all three using the correct document and must retain existing save, conflict, recovery and navigation ownership.

## Baseline results

| Check | Result |
| --- | --- |
| Static integrity | PASS: 50 modules, 61 shell assets, 151 vendor assets, 59 runtime dependency scans; 212 service-worker assets with bounded concurrency |
| Native Release build, locked restore | PASS: .NET 10.0.401, zero warnings/errors |
| Core | PASS: 2,298 passed, zero failed/skipped; `baseline-core.trx` |
| Chromium and Edge full browser matrix | PASS: 278 passed (139 per browser), zero failed/skipped, 14.9 minutes |
| Continuous 250 ms | Historical FAIL retained from #74; fresh final gate pending |
| High Contrast appearance | Historical FAIL retained; prior synthetic/UIA successes do not clear it |
| Physical screen reader, clean machine, physical DPI | NOT RUN; cannot be certified from hosted tests |

Local raw logs are retained in the detached baseline worktree (`baseline-web.log`, `baseline-native-build.log`, `baseline-core.log`, `artifacts/reading-experience/baseline/`).

## Phase 1 contract and implementation

Words use the existing Review expression (Native now evaluates Unicode scalars so supplementary letters/numbers agree with Web): Unicode letters/numbers joined by an internal ASCII apostrophe or hyphen. Prose, heading labels, table cells, link labels, image alternative text and supported semantic captions count. Code blocks (including Mermaid), inline code, front matter and raw HTML blocks do not count. Inline formatting preserves word boundaries. This aligns Web statistics and Review with Native's pre-existing Review rules; the old Web DOM count included code and could concatenate blocks.

Characters are Unicode scalar values in the same readable text, with all whitespace runs normalised to one ASCII space, blocks separated by one space and no leading/trailing space. They include punctuation and emoji, exclude Markdown syntax and URL destinations, and are not source-byte, UTF-16-unit or grapheme counts. Empty documents have zero words, characters and reading minutes. Reading time remains `ceil(words / 220)`; hours only change display.

The 350 ms trailing debounce schedules O(n) parsing/visiting and O(n) temporary memory, bounded by the existing 4 Mi source-character Review limit and a depth of 64. Web parsing uses a local module worker; superseded work is terminated. Native parsing runs in a serial background gate with cancellation and document/version/generation checks. Neither path renders diagrams or loads assets. Visible metrics never change source, history, caret, selection or scroll. Persistence state is derived from the document model, independently of render status.

## Delivery and final acceptance

Phase verdicts, added tests, exact final commit/tree, workflow URLs, measurements and final readiness are pending. No phase is yet declared validated. Production readiness remains NO-GO while independent performance/accessibility/trust gates remain unresolved.

### Phase 1 checkpoint evidence

- Native locked Release build: PASS, zero warnings/errors (`phase1-build-03.log`).
- Focused Core: PASS, 33 tests (`phase1-core-03.log`). Existing Review rules and scalar-character fixtures cover empty/prose/table/image/fence/front matter/formatting/entities, supplementary Unicode, details and image figures, long buffers, cancellation and limits.
- Compiled WPF/AvalonEdit/automation peers: PASS, 15 checks (`artifacts/reading-experience/phase1-controls-03/completion.json`). Actual document edits, Undo/Redo, selection preservation, tab changes and persistence states are exercised with preview scheduling stopped. This is not physical GUI input or OS High Contrast evidence.
- Browser checkpoint: PASS, 26 tests (13 per Chromium/Edge) in `phase1-web-04.log`, including keyboard Review activation, Undo/Redo, large buffer supersession, Unicode and semantic parity, narrow forced-colours, delayed/failed writes, and existing Find/Review regressions.
- Development failures retained: a mistaken expected character total in a new test (correct canonical readable string has 40 scalars); an accidental Windows text-encoding conversion during editing (repaired from UTF-8 source before clean reruns). No existing test threshold changed. The first mixed browser run is not final-source acceptance.
- Save completion now preserves a newer buffer and recovery draft. The status distinguishes the version written to disk from subsequent unsaved edits. No automatic write was added.

### Phase 2 checkpoint and architecture

- Extended the existing Web outline and native ListBox, retaining their source navigation and keyboard contracts. Expansion belongs to the document; active section identity comes from existing renderer targets.
- Shared Web heading collection now drives the live outline and Docs Site export. Native TOCs use Markdig's existing `DocumentHeading.Anchor`. Existing platform anchor conventions are preserved rather than changing saved links.
- Copy is separate from insertion. The configurable native dialog and Web controls consume a current-buffer heading snapshot, reject invalid ranges and never write the source. Numbered-list indentation is three spaces per level so a pasted TOC parses as an actual nested list.
- The Web Markdown details extension is shared by preview and readable statistics. Source locations inside nested tokens now use bounded parent ranges and a precomputed line-offset index; this also corrects navigation into collapsible sections.
- Checkpoint PASS: 16 focused browser tests (8 each Chromium/Edge), 8 additional existing navigation/selection/Find/Docs Site regressions, 64 focused Core tests, locked Release build with zero warnings/errors, 23 cumulative actual compiled WPF/AvalonEdit/UIA-peer checks. Follow-up verification after numbered indentation correction is recorded separately. Full final-source validation remains pending.
- Evidence: `phase2-web.log`, `phase2-regression.log`, `phase2-core.log`, `phase2-build-02.log`, `artifacts/reading-experience/phase2-controls/completion.json`. Assembly source stamps at this checkpoint identify the preceding commit plus working-tree changes; they are not final immutable commit evidence.

### Additional acceptance slice — native flowchart fidelity

The user added a fifth workstream to the same Goal and Draft PR. The exact supplied SPC Markdown fixture is retained for raw Mermaider 0.15.1 reproduction, a bounded renderer-input correction, nine-node/nine-edge/eight-class semantics, source preservation, label fidelity, Visual Edit and canonical export regression. No dependency upgrade is planned. This slice is not yet implemented or validated.

- Phase 2 follow-up PASS: 4 Chromium/Edge advanced-outline tests and 42 focused Core tests after the numbered indentation correction, including parsing the copied nested list (phase2-web-final.log, phase2-core-final.log).

### Phase 3 checkpoint and architecture

- Added contextual prose-line slash menus to the existing Web textarea and AvalonEdit. Keyboard focus stays in the editor, selection is announced through ARIA/UIA, unavailable commands show their prerequisite, and popup placement considers available space.
- Formatting and helper dialogs retain ownership of insertion. A slash range is passed to the existing Web commands; Native composes the existing Core edit against a virtual query-free buffer and applies it through `EditorFormattingService`. No intermediate deletion is published to persistence/history. Dialog cancellation preserves source and completed insertion is one Undo action.
- H1–H6, paragraph, bullet/numbered/check lists, quote, code, Mermaid, table, link, document link, image, image figure, callout, details, rule, ISO date/time and built-in snippet are covered. Native figure/details starter structures use existing semantic editors' services. Web exposes the existing Usage snippet; Native opens its catalogue.
- Guards cover read-only/standalone Mermaid, IME, literal URLs/paths/quotes, code fences/indented code, front matter, comments and raw HTML text containers. Escape keeps literal text. Initial lexical scanning is O(n), capped at 4 Mi source characters; a validated unchanged prefix is reused while filtering. Popup layout caches the trigger's position until layout changes. No preview is needed.
- PASS: 61 focused Core tests (`phase3-core-guards.log`), 10 focused Chromium/Edge slash tests (`phase3-web-guards.log`), 16 earlier browser integration tests including existing Mermaid completion, Find and selection (`phase3-web-final.log`), and two large-prefix browser checks with diagnostic attachments (`phase3-performance-web.log`). Static integrity PASS: 55 modules, 66 shell assets, 217 cache attempts.
- PASS: locked Release build with zero warnings/errors (`phase3-build-checkpoint.log`) and 30 cumulative compiled WPF/AvalonEdit/UIA-peer checks (`artifacts/reading-experience/phase3-checkpoint/completion.json`). The actual native command list applies H6, closes, and restores the exact query with Undo/Redo.
- Diagnostic sample on this development host: 1,048,578 source characters, 56.25 ms initial native context query, 14.91 ms for 100 cached queries. These are observations, not new thresholds or certification. Continuous 250 ms remains an independent mandatory gate.
- Development command failure retained: the first build invocation used the wrong solution filename; rerunning against `LensDocsStudio.Native.slnx` succeeded. No gate or threshold was changed. Physical input, OS High Contrast and screen-reader certification remain NOT RUN for this checkpoint.
