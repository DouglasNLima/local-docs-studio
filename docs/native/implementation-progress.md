# Native implementation progress

Current source adds native Insert commands for all six status kinds, keyboard shortcut labels and horizontal rules, closing a concrete gap against the web guide. Labels remain passive portable text, existing spans can be edited, invalid input stays in the dialogue, Cancel preserves source, and each action uses one undo group. The Release solution builds without warnings; 963 core tests, the complete 40-case native rendering/export harness and eight windowless AvalonEdit insertion/selection/Undo/Redo/read-only cases pass. A new 65th desktop workflow covers the actual menus, dialogues and preview; hosted confirmation is pending. These commands are not in preview.259.

Current milestone: validated native internal preview. **Production is not ready; the reconstruction goal remains incomplete.** The full scope is in [the reconstruction brief](reconstruction-brief.txt). Earlier implementation records are retained in [history through 2af4471](history/implementation-progress-through-2af4471.md).

The latest verified package is unsigned preview.259 at clean branch head 51688aa7433dc4cdf29e837bf9df6456622224fe. Downloaded native push run 38332285897 verifies 936 core tests, 40 corpus cases and four 64-check desktop contexts, including actual Word import and retained workspace images/links after switching folders, Save and restart. The full installer lifecycle passes with 20 preserved state files, two restored sessions and bundled installed runtimes. Both web runs pass 262 tests each. The parallel native PR run 38332288667 fails while observing the preview TextPattern after an upgraded application restarts; this failure remains recorded.

The workspace correction is included in preview.259. All twelve phase reports across the built, installed, upgraded and reinstalled applications pass exact draft/save, visible images, two-way links and retained roots. The upgraded application screenshot after restart was visually inspected. The PR failure occurs before its preview Text provider is available; the editor already contains the restored text. The harness now waits for preview completion and observes provider availability within its existing deadline, retaining every image/link assertion. This later harness correction awaits hosted confirmation.

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-sensitive search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens sessions |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours, details and source mapping; diagram/export and catalogue/theme corpora |
| Editing and exports | Native editing/completion, image management, clipboard formats, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Desktop workflows | 64 actual checks in built, installed, upgraded and reinstalled applications in verified preview.259; the new inline insertion workflow awaits hosted validation |
| Installer | Isolated self-contained per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall |
| Updates | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

## Earlier investigation notes

The first later workspace desktop probe (a66d3a5 PR run 38331665586) stops before switching folders because its typing helper does not emit the two supplied LF characters. Retained session bytes contain the exact Unicode phrase after unchanged saved text; the inspected screenshot agrees. The fixture now types that explicit phrase without assuming Enter events. All following exact-source, native-image, two-way-link and restart assertions remain required. This new GUI outcome is still pending; original failure evidence is retained.

The pending Word desktop check now follows the existing hyperlink tests by obtaining embedded images through TextPattern range children. A hidden-HWND reproduction finds no image in ordinary tree traversal, then the correctly named image after requesting the text range's children; its inspected page 2 PNG and geometry preserve source. That verifies native peers and rendering, while remote UI Automation delivery remains pending. Earlier pagination-only probes and failures remain recorded in the validation report.

The next change preserves Word image descriptions as literal Markdown alternative text, including explicit empty descriptions and different descriptions for a shared image. Twelve focused regressions pass; the corrected pre-fix fixture reproduced 11 failures with one unchanged fallback control. The complete local core suite passes 924 tests and the native harness passes all 40 corpus cases. A new 63rd desktop workflow exercises actual Word import, destination cancellation, all conversion warnings, image preview and portable saved output; hosted execution is pending. Preview.246 was the verified package before the subsequent preview.256 result above.

The details toggle parses fixed application-owned XAML once per WPF thread. It preserves the native Expander/ToggleButton, bindings, geometry and accessibility. The full local harness and focused header checks pass; final light/dark PNGs match the preceding renders byte for byte. Three fresh local processes pass 12 nested construction cases, maximum input gap 63.34 ms. The hosted keyboard regression now passes in all eight application contexts, preserving header focus and Markdown through Space expansion/collapse. The hosted details cases record first input at 23.9392 ms (PR) and 20.2413 ms (push), with maximum input gaps of 29.7557 ms and 28.9035 ms respectively. The historical intermittent delay is not proven eliminated and the 250 ms construction-input bound is unchanged.

Supported Word import now retains direct and inherited numbering, linked numbering styles, starts, nesting, continuations, restart rules, headings, web/email links, bookmarks, literal code and grouped quotes. Actual DOCX fixtures retain six lists, 12 items, six continuations and exact native image payloads; switching those items to inherited styles returns identical Markdown. The reproduced CRLF assertion mismatch is corrected. PDF/Word list export corrections and data-preservation regressions remain in the current passing suite.

The [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md) retain feature-specific evidence, scope and prior failures. The sealed security review remains immutable; subsequent source changes are not represented as covered by that review.

## Remaining acceptance and next work

Do not repeat passed tests without a new change, failure or unresolved question. Continue requirement-level acceptance work from the evidence above: intermittent construction delays and broader continuous/page layout, supported import/export destination fidelity, and external validation remain incomplete. Do not replace these with an aggregate green-CI claim.

Required external evidence includes production issuer/timestamp/revocation and a positive signed upgrade; SDK-free clean Windows installation; physical screen-reader/DPI/multi-monitor behaviour; live Azure DevOps attachment destinations; genuine historical schemas; and physical power-loss/removable-drive failures. The user's signing configuration is confirmed empty and is not a pending question. No certificate or publisher may be invented.

Actual Word pagination/style-derived template layout, code language/deeper quote reconstruction, field/file links, PDF complex-script/BiDi/joined-emoji fidelity, tagged PDF accessibility and explicit unsupported Mermaid/HTML/LaTeX forms remain documented limits. Source-only checks and windowless fixtures do not establish destination behaviour.

No local foreground application, Office, clipboard, installer or certificate-store interaction occurred in this batch. The branch remains `feature/native-wpf-rebuild`, with [draft PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) targeting `develop`. No merge, public release or tag change is authorised.
