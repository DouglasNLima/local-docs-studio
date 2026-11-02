# Native editor-to-preview selection reliability

Engineering receipt — 2 November 2026. Draft PR [#74](https://github.com/DouglasNLima/local-docs-studio/pull/74), targeting `develop`; starting revision `ab21919a42ec8b3d17d9ba2e39b4e7e41459e206`.

## Decision

**NO-GO for the reported intermittent first mouse-selection defect.** The final synthetic matrix passed in both baseline and candidate, so it does not establish the cause of that report or prove it fixed. A separate obsolete-navigation defect is reproduced and corrected. Keep this PR in Draft.

The reproducible selection evidence below is pinned to `8a94d3cb12c99ffaa2d0bfb32ea73633dd77a9ed` and diagnostic baseline `ccfce7d8a1c444517e1bc4f4d39992e30e29af1d`. The subsequent compatibility correction restores the original compiled API without changing selection mapping or the timer. Final receipt-only revalidation pins and terminal CI outcomes are recorded in the [Draft PR](https://github.com/DouglasNLima/local-docs-studio/pull/74).

No merge, publication, signing or release is authorised. Governance maintenance uses a separate amendment for the twelve changed frozen source/tracking files; the original Goal #62 evidence, Find amendment, licence scope and all unrelated trust/performance digests stay intact. The rights ledger retains existing classifications and introduces no new grant. PR #73 is untouched. Existing SignPath, High Contrast, Continuous 250 ms and Native Stable blockers remain independent.

## Investigation and before evidence

Inspected `MainWindow.Synchronisation.cs`, `PreviewSourceMap.cs`, `PreviewNavigationService.cs`, `PreviewSemanticAnchors.cs`, `NativeMarkdownRenderer.cs`, AvalonEdit focus wiring and existing selection validations. Existing UIA `Select()` checks do not exercise an actual mouse selection after leaving preview focus.

The synthetic fixture has six columns and rows Q-01–Q-15, repeated `planning`, `within`, `proposer`, `RED` and `validity`, bold/italic delimiters, punctuation, Unicode and wrapped scope cells. It contains no customer content. Mapping checks visit every occurrence with LF and CRLF, verify rendered text and exact UTF-16 source offsets, and cover cross-inline selections and syntax-only endpoints. These checks pass before the correction; there is no evidence supporting a global text-search workaround.

Hosted diagnostic attempts are retained separately:

| Revision / run | Outcome | Interpretation |
| --- | --- | --- |
| `b2849f1`, [41437810215](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41437810215) | Preview UIA `SetFocus` rejected before source input | Harness failure; no product reproduction |
| `ca668ff`, [41438356952](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41438356952) | First ten real mouse selections pass in normal and actual OS High Contrast; distant viewport preparation fails | Harness failure after ten successful operations; no product reproduction |
| `ccfce7d`, [41439109310](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41439109310) | Same ten selections pass; preparing a distant caret fails | UIA selection does not move AvalonEdit's caret; subsequent Left uses the old caret |
| `aeb9602`, [41440268944](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41440268944) | Scroll-percentage setup does not expose the target in baseline or candidate | Harness failure before selection; wrapped extent is not a fixed source-length proportion |
| `84c5b01`, [41441389995](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41441389995) | All 30 double-clicks and one sustained drag pass in both binaries, normal and High Contrast | Keyboard fixture starts from the wrong caret after Left; source selection differs from its expectation, so this is not a mapping failure |
| `fbfa94e`, [41442154931](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41442154931) | 37 observations pass in each binary/appearance | Independent typed input events were incorrectly assumed to form one Undo transaction; use the existing atomic plain-paste command for one-edit/one-Undo coverage |
| `64fb3da`, [41443004186](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41443004186) | All 41 Continuous observations pass in all eight runs, including diagnostics disabled | Pages focus preparation queried visible geometry before pagination supplied it; a bounded readiness observation now precedes the setup click |
| `8a94d3c`, [41444280009](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41444280009) | Both matrix jobs pass: all eight sessions, 82 observations each | 656 observations and 480 single completed word double-clicks; zero selection-action retries, exact source offsets, focus, geometry and saved bytes preserved |

The final harness prepares the source row through the app's existing explicit Find navigation, clicks visible preview text to establish focus and collapse its selection, then performs one completed double-click or drag. The pinned [AvalonEdit TextRangeProvider](https://github.com/icsharpcode/AvalonEdit/blob/862415d51eddc9eac93f462dbc522ffbf929cd52/ICSharpCode.AvalonEdit/Editing/TextRangeProvider.cs) confirms that UIA ScrollIntoView has no implementation and Select changes the selection without moving the caret. Scroll-percentage preparation also proved unreliable for the lazily measured wrapped extent; these setup failures remain recorded, not counted as selection defects. UIA text lookup is confined to test coordinates and expected endpoints. Observation polling never repeats the selection action.

An independent hidden WPF reader check reproduces **queued selection surviving a zoom change**: queue `BringIntoViewAsync`, change `Zoom`, pump the real dispatcher, and observe that the obsolete request returns true. The old request checks cancellation, document identity and the destination selection snapshot; it does not check zoom or reading-mode identity. `DocumentPreview` previously queued fitting without cancelling navigation. The before check fails at `Continuous: Zoom invalidates queued selection` after passing newest-occurrence, explicit cancellation and view replacement checks.

The identical compiled probe also verifies this against both genuine product assemblies in the hosted job. In Continuous and Pages, the baseline applies the obsolete second `planning` at rendered offset **9**; the candidate rejects publication and retains offset **0**. The [baseline](evidence/selection-sync/baseline-zoom.json) and [candidate](evidence/selection-sync/candidate-zoom.json) records contain the same probe SHA-256 and distinct product SHA-256 values. This is evidence for zoom invalidation, not a causal link to the unobserved first-selection failure.

The [hosted summary](evidence/selection-sync/hosted-summary.json) identifies all eight successful runs. Source mapping visits all fifteen rows; actual mouse targets move between rows Q-15, Q-01, Q-08 and Q-02, then repeat earlier occurrences. The retained [normal baseline](evidence/selection-sync/normal-baseline-first-selection.jsonl), [normal candidate](evidence/selection-sync/normal-interactions-first-selection.jsonl), [High Contrast baseline](evidence/selection-sync/high-contrast-baseline-first-selection.jsonl) and [High Contrast candidate](evidence/selection-sync/high-contrast-interactions-first-selection.jsonl) excerpts show the first focus transition through completed selection and navigation. Ordering observed: editor mouse-down cancels prior work, focus changes, AvalonEdit reports the source span, mouse-up completes, then the timer and deferred native navigation map that span. No dropped, mismapped or overwritten completed selection was observed in this matrix.

## Correction

`DocumentPreview` invalidates pending navigation on document, reading-mode and zoom changes. Navigation captures viewing mode and zoom and checks them again at its deferred publication boundaries. A caller validity predicate lets editor selection synchronisation recheck the generation, current immutable source version, visible Split layout, originating focus and exact source start/length after asynchronous layout/pagination. This also prevents comparing pointers from replaced documents.

The original four-argument navigation entry point is retained; an explicit five-argument overload carries the predicate. Initial extension of the signature exposed `MissingMethodException` in the existing paired shown-shell harness against an older product. The compatibility correction restores that binary contract; the current compiled scheduling harness also passes locally when loaded with the old product DLL. This branch-induced harness failure is separate from the unchanged performance threshold.

No source-map search or debounce increase is introduced. The synchronisation timer remains 60 ms. Preview-to-editor mapping and its navigation are unchanged.

## Semantic fallback contract

Exact selections use literal source positions, including UTF-16 Unicode positions, LF/CRLF offsets and mapped endpoints spanning multiple formatted inlines. Formatting and other source gaps inside a selection retain their real rendered content; source endpoints which cannot be represented literally do not acquire invented character positions.

For syntax-only selections or unmappable transformed endpoints, collapse the preceding literal highlight, navigate to the parser's semantic source block and show its block indicator with `Selection synchronised as a preview block.` The block is the existing source anchor at or immediately preceding the source offset; it does not claim exact word correspondence. If no block exists, clear the indicator and report `This source selection has no rendered text.` An empty editor caret with no literal endpoint also collapses stale text highlighting. There is no matching-word substitution.

## Validation

- Local Release build: zero warnings/errors (isolated output used while the earlier corpus process held its binaries).
- Core: 2,298 passed, zero skipped.
- Existing native corpus before and after the navigation correction: 202 diagram/export cases each, zero failures.
- Static web integrity: passed.
- Browser regressions: 278 Chromium/Edge checks passed.
- Native-generated exports from disk/static hosting: all 12 Chromium/Edge checks passed locally. An earlier hosted run ended one browser context unexpectedly; final hosted rerun status is reported separately on the PR.
- Focused navigation checks after correction: 16 passed across Continuous and Pages, including validity rechecks after deferral, zoom/view changes, render replacement and exact repeated occurrences.
- Hosted normal/actual Windows High Contrast comparison: all eight sessions pass, with diagnostics on/off, 656 observations, 480 single completed word double-clicks and zero action retries. Baseline also passes; the reported intermittent defect remains inconclusive.
- Governance: foundation/rights checks and all 24 governance tests pass; original grants and unrelated trust/performance pins are preserved.
- Continuous performance acceptance: **FAIL** independently at the unchanged **250 ms** threshold. In [run 41444279957](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41444279957), replications fail 38/66 and 29/66 candidate performance samples while correctness passes. First-execution table layout is 355.99 ms and 506.64 ms. [Source-labelled measurements](evidence/selection-sync/performance-summary.json). Visible Continuous interaction checks pass in both appearances. No performance acceptance is claimed.

Final mouse checks cover Continuous/Pages, distant and repeated occurrences, sustained drag across the 60 ms timer, keyboard selection, zoom, rapid alternation, source replacement during pending navigation, preview regeneration, exact Undo and syntax fallback. They assert source offsets, rendered range endpoints/text, visible geometry, retained editor focus and unchanged saved bytes. A passing baseline does not establish the reported defect as fixed.

Diagnostics require `LENS_NATIVE_SELECTION_DIAGNOSTICS=1`; at most 2,000 JSONL events per window record monotonic timestamps, generation, input/focus state, source offsets and navigation outcome, never document text. Full synthetic logs remain in hosted artifacts; bounded first-operation excerpts are committed. The event cap can omit later events; all scenario outcomes are separately asserted through native UIA. The default application does not write these logs.

Actual OS High Contrast selection success does not certify existing colour/appearance blockers. UIA observation timings are not a substitute for the 250 ms input acceptance gate. The fixture reproduces the requested table structure, not every state of the user's original session. The first-selection cause remains inconclusive, and neither the baseline's success nor the independent zoom correction changes that NO-GO decision.
