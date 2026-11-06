# Reading experience and productivity — engineering receipt

Status: five implementation slices delivered locally; final commit-specific hosted acceptance pending. This is an engineering receipt, not publication acceptance.

## Baseline and boundaries

- Goal: [#75](https://github.com/DouglasNLima/local-docs-studio/issues/75), tracking the same active Codex Goal `01a1b05e-1f98-7fe2-9aca-a6064536894d`; one consolidated Draft PR targets `develop`.
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

The user added a fifth workstream to the same Goal and Draft PR. The exact supplied SPC Markdown fixture is retained for raw Mermaider 0.15.1 reproduction, a bounded renderer-input correction, nine-node/nine-edge/eight-class semantics, source preservation, label fidelity, Visual Edit and canonical export regression. No dependency upgrade is planned. Implementation and checkpoint evidence follow; final commit-specific regression and hosted acceptance remain pending.

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


### Phase 5 checkpoint — native flowchart fidelity

- Root cause reproduced against pinned Mermaider 0.15.1: `FlowchartParser.ClassAssignPattern` ends at the class name, so a valid trailing semicolon bypasses assignment parsing and falls into bare-node parsing. The unadapted exact SPC fixture produces ten nodes, a phantom `class`, and no class assignments. The deliberately failing product regression and passing upstream reproduction are retained in `phase5-reproduction-02.log`.
- A bounded lexical renderer-input adapter recognises complete `class`, `classDef` and `style` statements. It replaces only their terminators, respects quoted/bracketed labels, escaped characters and comments, and rejects malformed directives visibly. Source documents and `DiagramResult.Source` are never rewritten. Comma-separated target IDs and repeated class assignments are supported. Effective private classes preserve assignment order, original named definitions and colours/borders; collisions are avoided. Comma-separated class names are explicitly unsupported, matching the observed limitation in pinned Mermaid.js. Unknown style targets remain styles and create no nodes.
- Limits: existing 100,000 source-character diagram budget, at most 300 targets and 16 explicit classes per target for multiple-class adaptation, 100,000 prepared characters, bounded regular-expression timeouts. Other diagram families bypass adaptation. SVG correction only restores two omitted subgraph properties (numeric dash pattern and safe title colour) against verified renderer-owned identity and geometry; it does not replace layout or routing.
- Exact fixture SHA-256 (UTF-8, LF): `f4830b14c2b6de192b2fc6c28b29bf98a2f6ff52bc7d19cfaa1c08391cd89bae`. It contains nine nodes, nine connections, eight named classes, all nine assignments and five SPC members. With/without styling terminators produces identical native graph semantics.
- `<small>` investigation: upstream native normalisation removes the tag while retaining text and line breaks. The Preview now visibly explains unsupported small/subscript/superscript/highlight typography; canonical SVG carries the explanation. Existing protection prevents destructive Visual Edit transformations of those labels. Read-only visual inspection retains all nine labels and connections. This is a documented typography limitation, not pixel-identical rendering certification. Pinned Web rendering also does not apply the authored SPC title colour in the observed output; its parsed style and group semantics remain present. Native explicitly retains that colour.
- PASS checkpoint: 16 focused Core tests (`phase5-core-final.log`), 4 Chromium/Edge actual-preview/canonical SVG/PNG export tests (`phase5-web-final.log`). Independent Web runtime inspection verifies nine declared nodes (excluding its separate subgraph-container vertex), eight definitions, nine connections and each class assignment. Overlapping class colours are checked independently in both renderers; assignment order, rather than definition order, wins.
- Earlier broader checkpoint PASS: 354 Mermaid/flowchart/arrangement Core regressions (`phase5-core-regression.log`), locked Release build with zero warnings/errors (`phase5-build.log`), 13 actual WPF preview/inspection/export checks across Light/Dark (`artifacts/reading-experience/phase5-native/completion.json`). Native preview, inspection and export use identical canonical PNG bytes. Source bytes remain unchanged. These precede the last class-order correction and require final reruns; they are not final-source PASS.
- Development test failures retained: an initial reproduction test used a non-existent node property; Web graph counting initially included the subgraph container; an overlapping-class expectation incorrectly assumed definition order. Product semantics and tests were corrected from runtime evidence. No historical result, performance threshold or High Contrast gate changed. Hosted CI, final High Contrast and full export regression are pending.


### Phase 4 checkpoint — two-document comparison

- Added two presentation slots, distinct from Editor/Split/Preview. Each slot owns a document record and mode. The existing command surface follows activation; the inactive source is a view of the canonical buffer, never an independently writable copy. Native binds the same AvalonEdit `TextDocument`; Web uses the existing `fileCache` record. Save/export/paste/Find/formatting remain owned by the existing services.
- Selection, caret, scroll and Find snapshots belong to each document. Native Undo stacks were already document-owned; Web now retains histories across switches by file-record identity, including Save As. Retained Web history has an explicit 16-document / 32 Mi UTF-16-code-unit undo/redo budget. Eviction never changes source buffers or recovery. Closing comparison preserves all documents and pending edits. Workspace removal exits safely; rename identity follows the existing record/model.
- Secondary previews use independent renderer state and IDs, the same sanitisation/local assets/path rules, and stale document/version/theme guards. Web Marked instances are now local to each renderer; Mermaid work shares the existing serial queue. Native uses the existing parser, diagram, SVG, image, maths and incremental WPF renderer with the shared work gate. Hidden secondary previews do not render. Unrelated active edits do not cancel/rebuild an unchanged secondary native preview.
- Independent scroll is the default. Explicit proportional sync applies to continuous scrolling; paged native views keep page navigation. Web range and native divider support resizing; narrow windows stack panes. Keyboard activation, ARIA/UIA names and forced-colour/system-resource styles are provided. The previous single-document layout returns when closing comparison.
- File selection now rejects stale asynchronous reads before assigning active document state. A stale Markdown render cannot install HTML after another request has taken ownership. External dirty conflicts have an explicit **External conflict** status; clean external changes retain **External change**.
- PASS checkpoint: 8 focused browser tests across Chromium/Edge (`phase4-web-expanded-02.log`): Editor/Editor, Editor/Preview, Preview/Preview, independent edits/Undo/Redo/Find, real secondary SVG download, scoped fragment/workspace links, Save/Save As ownership, retained external conflict, reopen, narrow forced-colours, and two 5,000-paragraph documents with independent/explicitly synchronised scroll.
- PASS earlier compiled checkpoint: actual WPF/AvalonEdit controls and canonical secondary rendering, independent selection/Find/Undo, real-file save isolation, external conflict rejection and recovery/close preservation (`artifacts/reading-experience/phase4-controls-final/completion.json`). Later lifecycle/appearance changes require the final rerun. Native dialog-driven Save As and shown-window keyboard/pointer checks remain for hosted acceptance; no physical-machine certification is inferred.
- Development failures retained: folder/file picker test setup initially used unsupported Playwright payloads; a conflict label expectation exposed the need to distinguish dirty conflicts; scoped fragment handling initially intercepted wiki links and was corrected to leave document-link routing intact. Initial native compilation found a wrong AvalonEdit scrolling property and missing namespace; subsequent build passed. No existing acceptance threshold was modified.

- Phase 4 final checkpoint PASS: locked Release build, zero warnings/errors (`phase4-build-checkpoint.log`), 42 cumulative compiled WPF/AvalonEdit/UIA-peer checks (`artifacts/reading-experience/phase4-checkpoint/completion.json`), and static integrity (56 modules, 67 shell assets, 218 cache attempts). These remain checkpoint results, pending the final immutable commit and hosted runs.


### Consolidation investigation 1

The complete Core run on `7371190` reported FAIL: 2 failed, 2,379 passed (2,381 total). The fixed 22-topic native help contract caught five new top-level sections; they were integrated as subsections of their existing owning topics, preserving the original test and anchor catalogue. The protected unknown-style contract caught rejection of `class D one,two`. This remains protected inspection with an explicit fidelity warning and default appearance (as independently observed in Mermaid.js), while a narrowly recognised renderer-input comment prevents its phantom node. Separately assigned classes still combine in assignment order. The historical tests were not weakened or changed; final reruns follow.


### Consolidation investigation 2

The browser matrix exposed loss of the existing `data-details-block` marker in the new shared details renderer and an ambiguous accessible-name match between the established Document review button and the new statistics button. The renderer now retains the supported semantic marker; the statistics control has a distinct **Open review** accessible name while keeping its full metrics and Document Review tooltip. Existing helper/Review regressions remain unchanged.

A second complete Core run reported FAIL: a new directive-prefix regular expression timed out during concurrent work, and the existing complete-detailed ER fixture reached its unchanged six-second rendering deadline. The hot prefix check was replaced with bounded ordinal span comparisons, without widening a timeout. The ER deadline failure remains recorded; isolated baseline/candidate investigation and final-source CI are required before attribution. Continuous 250 ms remains a separate gate.


### Technical and security review

- Reviewed current-buffer ownership across debounce, recovery, asynchronous selection and save completion; independent histories and guarded helper-dialog ranges; Marked instance isolation and the shared Mermaid queue; secondary preview cancellation, theme/path/version checks and hidden-pane behaviour; canonical native SVG/PNG ownership; source-only handling of unsupported syntax.
- Rendering continues through existing HTML/SVG sanitisation and local asset/path validation. No document content is sent to an external service. No new runtime dependency, framework, build step, backend, worker host or copied upstream implementation was added. Source remains authoritative and native Visual Edit still uses protected spans.
- Review found and corrected stale-read ownership, overlapping accessible names, lost semantic details attributes, a fixed-help-catalogue change, protected compound-class behaviour and a hot regex prefix check. Existing regression tests exposed these issues and are retained. Both source-selection direction and native caret offset now belong to the document view, including activation after a pointer selection.
- Scoped MIT review grants only enumerated original R1/R2 portions of new implementation/docs. The supplied SPC fixture and other R3–R7 content remain excluded. No dependency version, third-party notice, trust policy, historical rights ledger or performance/High Contrast threshold changed. The repository-designated **current** Goal #62 rights ledger is regenerated as instructed by `scripts/compliance/README.md`; frozen Goal #58/#60 evidence and Goal #62 preserved-file snapshot remain unchanged. A separate exact 41-path amendment binds changed implementation/current-guidance hashes, with negative controls rejecting notice/performance scope changes.
- Hosted interaction testing is isolated in a new normal/actual-High-Contrast job. Its process guard rejects execution on a development host. It exercises real keyboard slash insertion/Undo, statistics UIA, two-document activation/Find/save, exact SPC Preview disclosure, Preview/Preview and close. Hosted results are pending and are not physical-user certification.


### Consolidation checkpoint before the Draft PR

- Initial full Web run: FAIL, 311 passed / 3 failed / 314 total (`consolidation-web-01.log`). The three failures were the details marker in both browsers and the ambiguous Review name in Chromium. The run spans subsequent corrections and is not final-source acceptance. The corrected narrow matrix and exact-head hosted matrix supersede it only for their own source identity.
- Paired investigation: baseline isolated ER PASS 17 (`baseline-er-isolated.log` in the baseline worktree); candidate ER/flowchart/visual tests PASS 78 (`candidate-er-isolated.log`); baseline complete Core PASS 2,298 (`baseline-core-paired.log`). Candidate full diagnostic run PASS 2,382, zero failed/skipped (`consolidation-core-diagnostic.log`, `core-diagnostic.trx`). Earlier candidate full runs with regex/deadline failures remain FAIL, not overwritten. The existing corpus assertion now includes its renderer error message; its success criterion is unchanged. Timing variability remains INCONCLUSIVE and does not clear Continuous 250 ms.
- Current native/control checkpoint: locked build, actual compiled controls and canonical SPC checks are rerun after the final interaction changes. Hosted pointer/keyboard/Save As/High Contrast and the original editor-family suites are pending.
- Governance review: no new dependencies, unchanged Mermaider 0.15.1, R3–R7 exclusions retained, original notice/trust/performance snapshots preserved. The current rights inventory and reviewed original-portion grant are refreshed. Negative controls cover baseline identity, previous hashes, scope omissions, notice/performance injection and malformed/current digest drift.
- The exact final commit/tree, terminal workflow URLs, final counts and consolidated verdict will be recorded in the single Draft PR body and final generated receipt. This avoids a self-referential Git hash in the versioned file. No PASS from an earlier code version certifies that final identity.


- Pre-push correction checkpoint PASS: 20 focused Chromium/Edge tests (`consolidation-browser-fixes.log`); locked native Release build, zero warnings/errors (`consolidation-build-03.log`); 42 actual compiled WPF/AvalonEdit/UIA-peer checks and 13 canonical SPC Light/Dark checks (`consolidation-controls-03` and `consolidation-flowchart-03` completion files). The hosted UI harness is compiled but deliberately NOT RUN on this development host.
- Pre-push provenance PASS: current inventory 2,553 paths; 2,421 protected files; 1,109 enumerated original-portion grant paths; 68 Web notices and 537 native files retained, with 522 exact upstream matches. All 25 governance negative-control tests passed. No administrative settings were changed. Static integrity PASS: 56 modules, 67 shell assets, 218 cached assets.


### Hosted input investigation — first PR candidate

PR #76 candidate `3c1c100b7313b49f2bd742eb8f634fc1e5d28531` passed all 2,382 local Core tests and the hosted push browser matrix. Its new real-input jobs failed before slash insertion. WPF raises `PreviewTextInputStart` for ordinary automatically completed keyboard input as well as ongoing compositions; the initial IME guard incorrectly suppressed both. The guard now distinguishes `TextComposition.AutoComplete` and handles an empty/cancelled composition without suppressing the next ordinary slash. Compiled AvalonEdit routed-event regressions cover automatic input, ongoing composition and cancellation; hosted keyboard acceptance must pass separately. This interpretation was checked against the [upstream WPF input manager](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Input/TextCompositionManager.cs); no upstream code was copied.

The first nominal High Contrast reading job also passed a scalar PowerShell string through array splatting, producing individual character arguments. Its receipt correctly reported `highContrast: false`; therefore that attempt is **FAIL**, not High Contrast evidence. The workflow now passes an explicit scalar `--normal`/`--high-contrast` argument, the harness requires exactly one recognised appearance argument, and its check requires the actual OS state to equal the requested mode. Existing appearance gates and thresholds are unchanged. First-attempt evidence: [native run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42222063594), retained under `artifacts/reading-experience/ci-3c1c100/` locally.


The first ordinary native and desktop-UX jobs also exposed a Pages scroll regression at the new footer height: Continuous aligned correctly, but the source showed Regions 070–073 while the reader remained on the page ending at Region 070; the required representative Region 071 was absent. The existing algorithm chose a preceding heading from a physical one-third point. It now chooses the first-third heading from the actual visible heading range, retaining the exact source point when no heading is visible. The existing real-input scroll tests and their success criteria remain unchanged. This implementation file is added to the exact preserved-source amendment (41 paths); prior selection evidence remains frozen. Hosted acceptance of this correction is pending.

The expanded compiled control test also exposed a test-only race between its direct private render call and the real comparison debounce. It now waits for the actual scheduled secondary document to contain the expected source, under the existing timeout, rather than treating cancellation of a redundant request as completion. No product timeout or readiness gate changed.


### Hosted input investigation — second PR candidate

Candidate `5a17e74504642f3827b1bda911cf20163edc45a3` passed 45 exact-source compiled checks and 13 SPC checks. Both hosted input jobs confirmed real slash keyboard execution with one-step Undo/Redo, source-preserving SPC Preview, separate Find visibility, Save and Save As ownership. Actual High Contrast was verified in the requested appearance. They then failed the return-to-left checkpoint. The failure capture shows the correct left source, restored `Left pending` Find query and one match. The harness had captured its expected buffer immediately after submitting keyboard input, before waiting for the complete text. It now waits for exact paragraph/typing results before switching and checks Undo against the state before that typing group (the separately entered newline is retained). This corrects a new observation race; the required source/Find/save/history outcomes remain checked. [Second native run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42224448346) remains FAIL until superseded by a passing exact-source rerun.

The hosted reading harness now also opens the real native Copy TOC dialogue, changes list style and wrapper, compares the clipboard with its generated anchor-bearing index, and verifies unchanged source in both appearances. Its first compilation exposed nullable UIA lookup handling; explicit missing-control errors were added before the successful build. Hosted execution remains required.

Candidate `8644dc7` local Core result: **FAIL**, 2,381 passed / one unchanged complete-detailed ER six-second deadline failure; the isolated 17-test ER suite passed afterwards. Hosted TOC input exposed missing initialisation of the existing validation-only STA clipboard apartment before its first read; the harness now starts/disposes that service like the other UI suites. This is a harness correction, not a product clipboard change. Earlier failed attempts remain recorded.

Candidate `2e432b3` hosted TOC capture shows the correct numbered index but an unchecked wrapper: the pointer click immediately after combo selection dismissed the open popup. The harness now explicitly collapses the combo and activates the checkbox through its actual UIA Toggle pattern, observing the checked state before the unchanged generated-text assertion. This preserves both the accessibility check and source/clipboard requirements; the failed attempt remains FAIL.

Candidate `5692101` hosted TOC options/copy, slash, source-preserving SPC, Find restoration and Save/Save As all passed before the comparison Undo assertion. The capture/recovery source showed `Left pendin`: native Undo correctly reverted the final injected character, while the test incorrectly expected the entire string input to be grouped. The harness now delimits the final `g` through real caret input, checks Undo against its exact preceding source, then Redo against the complete buffer, and verifies the other file after both operations. Slash insertion still separately requires one coherent Undo transaction. Existing AvalonEdit grouping is preserved; no product history or threshold changes were made.


### Preview/Preview lifecycle correction

The `df42add` hosted journey reached Preview/Preview and exposed a real process crash in WPF `TextSelection.UpdateCaretAndHighlight` during `FlowDocumentReader.OnApplyTemplate` detachment. The secondary reader's dynamic Style lookup was lost while all pane children were removed/reinserted, causing template reapplication. The correction resolves the same existing reader Style once (its theme brushes remain dynamic) and leaves surfaces attached when they already occupy the intended pane. It adds a compiled check for zero reader-template changes and the same preview document during header refresh; the unchanged hosted Preview/Preview/close route must pass independently in normal and actual High Contrast. No exception is swallowed in the application and no text selection setting is disabled. The harness now retains its primary FAIL receipt even if a screenshot cannot be captured after process exit. [Failing run](https://github.com/DouglasNLima/local-docs-studio/actions/runs/42227947253); upstream lifecycle source was inspected, with no source copying.


### Relative-asset cache correction

Final review found a missing path/workspace component in the completed secondary-preview cache key, despite the queue already invalidating that context. A new actual WPF/file test first rendered a one-pixel image, relocated the unchanged Markdown model to another real folder containing a two-pixel image under the same relative reference, and failed waiting for the new image (`ci-relative-cache-reproduction.log`, line 210). The completed cache now includes effective source path and workspace; old preview callbacks also reject changed path ownership. The same test must pass with the exact new pixels, unchanged source version/text and unchanged source files. No automatic source write or asset transfer is introduced.

The preceding `b7b776d` hosted reading journey passed all ten explicit checks plus its intermediate exact-state waits in both normal and actual High Contrast, including native TOC clipboard/options, slash Undo/Redo, SPC source preservation, Find/Save/Save As, independent history, Preview/Preview and closure. This confirms the reader-template correction for that source identity; the final asset-cache candidate still requires its own complete CI evidence.


### Native hardening continuation

Goal #75 / Draft PR #76 continue from `adc68a98d0570bc39858591d252da90a644553d2`, tree `00aac08a1c06821612bbb27b256057bf94a43217`. Five functional slices remain intact. See [hardening and acceptance](../native/native-hardening-acceptance.md) for P0/P1 investigation, unchanged independent gates and separate verdicts. Original failures and receipts remain retained.
