# Native editor-to-preview selection reliability

Engineering receipt — 2 November 2026. Draft PR [#74](https://github.com/DouglasNLima/local-docs-studio/pull/74), targeting `develop`; starting revision `ab21919a42ec8b3d17d9ba2e39b4e7e41459e206`.

## Decision

**NO-GO while the reported intermittent first mouse-selection failure remains inconclusive.** A separate stale-navigation defect has been reproduced and corrected. It is not yet established as the cause of the user's reported behaviour. Hosted comparison and final-source validation are in progress.

No merge, publication, signing or release is authorised. PR #73 is untouched. Existing SignPath, High Contrast, Continuous 250 ms and Native Stable blockers remain independent.

## Investigation and before evidence

Inspected `MainWindow.Synchronisation.cs`, `PreviewSourceMap.cs`, `PreviewNavigationService.cs`, `PreviewSemanticAnchors.cs`, `NativeMarkdownRenderer.cs`, AvalonEdit focus wiring and existing selection validations. Existing UIA `Select()` checks do not exercise an actual mouse selection after leaving preview focus.

The synthetic fixture has six columns and rows Q-01–Q-15, repeated `planning`, `within`, `proposer`, `RED` and `validity`, bold/italic delimiters, punctuation, Unicode and wrapped scope cells. It contains no customer content. Mapping checks visit every occurrence with LF and CRLF, verify rendered text and exact UTF-16 source offsets, and cover cross-inline selections and syntax-only endpoints. These checks pass before the correction; there is no evidence supporting a global text-search workaround.

Hosted diagnostic attempts are retained separately:

| Revision / run | Outcome | Interpretation |
| --- | --- | --- |
| `b2849f1`, [41437810215](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41437810215) | Preview UIA `SetFocus` rejected before source input | Harness failure; no product reproduction |
| `ca668ff`, [41438356952](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41438356952) | First ten real mouse selections pass in normal and actual OS High Contrast; distant viewport preparation fails | Harness failure after ten successful operations; no product reproduction |
| `ccfce7d`, [41439109310](https://github.com/DouglasNLima/local-docs-studio/actions/runs/41439109310) | Same ten selections pass; preparing a distant caret fails | UIA selection does not move AvalonEdit's caret; subsequent Left uses the old caret |

The final harness prepares the source row through the app's existing explicit Find navigation, clicks visible preview text to establish focus and collapse its selection, then performs one completed double-click or drag. The pinned [AvalonEdit TextRangeProvider](https://github.com/icsharpcode/AvalonEdit/blob/862415d51eddc9eac93f462dbc522ffbf929cd52/ICSharpCode.AvalonEdit/Editing/TextRangeProvider.cs) confirms that UIA ScrollIntoView has no implementation and Select changes the selection without moving the caret. Scroll-percentage preparation also proved unreliable for the lazily measured wrapped extent; these setup failures remain recorded, not counted as selection defects. UIA text lookup is confined to test coordinates and expected endpoints. Observation polling never repeats the selection action.

An independent hidden WPF reader check reproduces **queued selection surviving a zoom change**: queue `BringIntoViewAsync`, change `Zoom`, pump the real dispatcher, and observe that the obsolete request returns true. The old request checks cancellation, document identity and the destination selection snapshot; it does not check zoom or reading-mode identity. `DocumentPreview` previously queued fitting without cancelling navigation. The before check fails at `Continuous: Zoom invalidates queued selection` after passing newest-occurrence, explicit cancellation and view replacement checks.

## Correction

`DocumentPreview` invalidates pending navigation on document, reading-mode and zoom changes. Navigation captures viewing mode and zoom and checks them again at its deferred publication boundaries. A caller validity predicate lets editor selection synchronisation recheck the generation, current immutable source version, visible Split layout, originating focus and exact source start/length after asynchronous layout/pagination. This also prevents comparing pointers from replaced documents.

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
- Focused navigation checks after correction: 16 passed across Continuous and Pages, including validity rechecks after deferral, zoom/view changes, render replacement and exact repeated occurrences.
- Hosted normal/actual Windows High Contrast comparison: pending.
- Continuous performance acceptance: unchanged 250 ms threshold; final hosted result pending and reported independently.

Final mouse checks cover Continuous/Pages, distant and repeated occurrences, sustained drag across the 60 ms timer, keyboard selection, zoom, rapid alternation, source replacement during pending navigation, preview regeneration, exact Undo and syntax fallback. They assert source offsets, rendered range endpoints/text, visible geometry, retained editor focus and unchanged saved bytes. A passing baseline does not establish the reported defect as fixed.

Diagnostics require `LENS_NATIVE_SELECTION_DIAGNOSTICS=1`; at most 2,000 JSONL events record timing, generation, input/focus state, source offsets and navigation outcome, never document text. The default application does not write these logs.
