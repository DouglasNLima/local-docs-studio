# Native implementation progress

The next import-fidelity slice preserves direct single strikethrough and single underline, including supported combinations with bold/italic, links and tables. Double strikethrough becomes single strikethrough with a conversion note; explicit off values remain plain text. Nine focused regressions pass after seven pre-fix failures and one passing off-state control. All 1,011 core tests pass, and the native Word preview verifies both actual WPF decorations and the retained image/source. Inherited run formatting and other underline styles remain outside this slice; final hosted validation is pending.

DOCX text growth is reproduced and corrected. A 6,167-byte package expanded one shared hyperlink into 34,524,000 Markdown characters before the fix. Word import now bounds generated text at 33,554,432 UTF-16 characters before buffer growth, including intermediate links, escaped runs, code fences and streamed table content. Sixteen regressions and the existing suite pass (1,002 core tests); the local native corpus passes all 40 cases. The WPF app launched locally with isolated state, loaded an unchanged document and closed gracefully after readiness. The extended hosted import rejection/recovery check awaits CI. Mandatory workflows are unchanged. This does not resolve the earlier intermittent preview pause or production acceptance dependencies.

## Previous milestone evidence

The current change adds validation-only stack capture for the unresolved preview pause. A separate hidden helper observes validation input pulses and captures the owned process only after 300 ms, beyond the unchanged 250 ms acceptance limit. A deliberate windowless wait verifies the watched thread in a minimal dump; normal/idle input and unrelated process requests do not produce captures. Diagnostic failure preserves the original rendering exception. No application rendering behaviour has been changed or certified fixed; hosted diagnostic evidence remains pending.

The native table grid now passes actual keyboard editing, Cancel/Apply, row/column operations, alignment, reopening/new insertion, native preview and whole Undo/Redo in all four preview.269 application contexts. It closes the fixed-example insertion gap against the web table editor. All 986 core tests, 40 corpus cases and four hidden grid layouts pass in the native push run. The parallel PR run fails a retained nested-details responsiveness check at 587.9933 ms; its source/content remain intact and the cause is unresolved. Production acceptance remains incomplete.

Native Insert commands now provide all six status kinds, editable literal badge and shortcut labels, and horizontal rules. Preview.266 verifies actual Cancel, retained invalid input, reopened labels, native preview text, complete Undo/Redo and unchanged saved source in all four application contexts. Insertion after a pipe table preserves its cells and line endings; in-cell edits remain inline. The reinstalled-app PNG was inspected. The installer-discovery harness correction now passes both hosted wizard and full lifecycle runs, alongside four hidden-HWND checks and warning-free builds. Both cancel/install discovery records identify the expected native class and product caption; no observation deadline or interaction assertion was weakened.

Current milestone: validated native internal preview. **Production is not ready; the reconstruction goal remains incomplete.** The full scope is in [the reconstruction brief](reconstruction-brief.txt). Earlier implementation records are retained in [history through 2af4471](history/implementation-progress-through-2af4471.md).

The latest independently verified package is unsigned preview.269 at clean branch revision 77be5707d5a67dfcb5525d699eeb2dee691985a2. Native push 38344045663 passes: 986 core tests, 40 corpus cases, ten inline-editor cases, four hidden table layouts and four 66-check desktop contexts. The complete installer lifecycle preserves 20 state files and restores two sessions, with bundled installed runtimes. Both web runs (PR 38344048273 and push 38344045667) pass 262 tests each. Native PR 38344048333 fails the existing nested-details input deadline; it is not a successful certification run. The last verified PR-merge package remains preview.266 at merge 2040ba39eac131c035407128dc412efb66b86613.

The workspace correction passes all twelve phase reports in the verified preview.269 native push run: folder switching, Save and restart in the built, installed, upgraded and reinstalled apps preserve exact drafts, saved source, images, two-way links and per-tab roots. The earlier PR provider-readiness failure and original evidence remain recorded; the bounded preview-completion/provider observation now passes this full lifecycle.

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-sensitive search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens sessions |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours, details and source mapping; diagram/export and catalogue/theme corpora |
| Editing and exports | Native editing/completion, image management, clipboard formats, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Desktop workflows | 66 actual checks in built, installed, upgraded and reinstalled applications in verified preview.269, including table editing, inline labels and retained workspace context |
| Installer | Isolated self-contained per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall |
| Updates | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

## Earlier investigation notes

The first workspace desktop probe (a66d3a5 PR run 38331665586) stopped before switching folders because its typing helper did not emit the two supplied LF characters. Retained session bytes and the inspected screenshot agreed. The corrected fixture types the explicit Unicode phrase without assuming Enter events. Its exact-source, native-image, two-way-link and restart assertions now pass in all twelve preview.264 phase reports; original failure evidence is retained.

The Word desktop check obtains embedded images through TextPattern range children, as the existing hyperlink checks do. A hidden-HWND reproduction found no image in ordinary tree traversal, then the correctly named image through the text range; its inspected page 2 PNG and geometry preserved source. Actual remote UI Automation now passes in all four preview.264 contexts. Earlier pagination-only probes and failures remain recorded in the validation report.

Word image descriptions remain literal Markdown alternative text, including explicit empty descriptions and different descriptions for a shared image. Twelve focused regressions reproduced 11 pre-fix failures with one unchanged fallback control. The 63rd desktop workflow covers actual Word import, destination cancellation, all conversion warnings, image preview and portable saved output. It passed in preview.256 and remains covered by the current preview.266 lifecycle and 968-test core suite.

The details toggle parses fixed application-owned XAML once per WPF thread. It preserves the native Expander/ToggleButton, bindings, geometry and accessibility. The full local harness and focused header checks pass; final light/dark PNGs match the preceding renders byte for byte. Three fresh local processes pass 12 nested construction cases, maximum input gap 63.34 ms. The hosted keyboard regression now passes in all eight application contexts, preserving header focus and Markdown through Space expansion/collapse. The hosted details cases record first input at 23.9392 ms (PR) and 20.2413 ms (push), with maximum input gaps of 29.7557 ms and 28.9035 ms respectively. The historical intermittent delay is not proven eliminated and the 250 ms construction-input bound is unchanged.

Supported Word import now retains direct and inherited numbering, linked numbering styles, starts, nesting, continuations, restart rules, headings, web/email links, bookmarks, literal code and grouped quotes. Actual DOCX fixtures retain six lists, 12 items, six continuations and exact native image payloads; switching those items to inherited styles returns identical Markdown. The reproduced CRLF assertion mismatch is corrected. PDF/Word list export corrections and data-preservation regressions remain in the current passing suite.

The [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md) retain feature-specific evidence, scope and prior failures. The sealed security review remains immutable; subsequent source changes are not represented as covered by that review.

## Remaining acceptance and next work

Do not repeat passed tests without a new change, failure or unresolved question. Continue requirement-level acceptance work from the evidence above: intermittent construction delays and broader continuous/page layout, supported import/export destination fidelity, and external validation remain incomplete. Do not replace these with an aggregate green-CI claim.

Required external evidence includes production issuer/timestamp/revocation and a positive signed upgrade; SDK-free clean Windows installation; physical screen-reader/DPI/multi-monitor behaviour; live Azure DevOps attachment destinations; genuine historical schemas; and physical power-loss/removable-drive failures. The user's signing configuration is confirmed empty and is not a pending question. No certificate or publisher may be invented.

Actual Word pagination/style-derived template layout, code language/deeper quote reconstruction, field/file links, PDF complex-script/BiDi/joined-emoji fidelity, tagged PDF accessibility and explicit unsupported Mermaid/HTML/LaTeX forms remain documented limits. Source-only checks and windowless fixtures do not establish destination behaviour.

No local foreground application, Office, clipboard, installer or certificate-store interaction occurred in this batch. The branch remains `feature/native-wpf-rebuild`, with [draft PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) targeting `develop`. No merge, public release or tag change is authorised.
