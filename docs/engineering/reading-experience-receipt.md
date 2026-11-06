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
