# Native implementation progress

Current milestone: validated native internal preview. **Production is not ready; the reconstruction goal remains incomplete.** The full scope is in [the reconstruction brief](reconstruction-brief.txt). Earlier implementation records are retained in [history through 2af4471](history/implementation-progress-through-2af4471.md).

The latest verified packages are unsigned 0.1.1-preview.246, built from clean PR merge `504cf6e34d53a4bfb58b7a2c2f9d78def7efa34d` for head `2af4471fcdd982fa4888b025bd1ad08f058d91c7`. Both native and both web runs pass on attempt 1. Downloaded evidence verifies 912 core tests, 40 diagram/export cases, four 62-check desktop contexts per native run, installed runtime origins and the complete installer lifecycle. Exact run IDs, package paths and hashes are in the [engineering handover](engineering-handover.md).

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-sensitive search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens sessions |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours, details and source mapping; diagram/export and catalogue/theme corpora |
| Editing and exports | Native editing/completion, image management, clipboard formats, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Desktop workflows | 62 actual checks in built, installed, upgraded and reinstalled applications in both current native runs |
| Installer | Isolated self-contained per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall |
| Updates | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

## Latest engineering result

The details toggle parses fixed application-owned XAML once per WPF thread. It preserves the native Expander/ToggleButton, bindings, geometry and accessibility. The full local harness and focused header checks pass; final light/dark PNGs match the preceding renders byte for byte. Three fresh local processes pass 12 nested construction cases, maximum input gap 63.34 ms. The hosted keyboard regression now passes in all eight application contexts, preserving header focus and Markdown through Space expansion/collapse. The hosted details cases record first input at 23.9392 ms (PR) and 20.2413 ms (push), with maximum input gaps of 29.7557 ms and 28.9035 ms respectively. The historical intermittent delay is not proven eliminated and the 250 ms construction-input bound is unchanged.

Supported Word import now retains direct and inherited numbering, linked numbering styles, starts, nesting, continuations, restart rules, headings, web/email links, bookmarks, literal code and grouped quotes. Actual DOCX fixtures retain six lists, 12 items, six continuations and exact native image payloads; switching those items to inherited styles returns identical Markdown. The reproduced CRLF assertion mismatch is corrected. PDF/Word list export corrections and data-preservation regressions remain in the current passing suite.

The [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md) retain feature-specific evidence, scope and prior failures. The sealed security review remains immutable; subsequent source changes are not represented as covered by that review.

## Remaining acceptance and next work

Do not repeat passed tests without a new change, failure or unresolved question. Continue requirement-level acceptance work from the evidence above: intermittent construction delays and broader continuous/page layout, supported import/export destination fidelity, and external validation remain incomplete. Do not replace these with an aggregate green-CI claim.

Required external evidence includes production issuer/timestamp/revocation and a positive signed upgrade; SDK-free clean Windows installation; physical screen-reader/DPI/multi-monitor behaviour; live Azure DevOps attachment destinations; genuine historical schemas; and physical power-loss/removable-drive failures. The user's signing configuration is confirmed empty and is not a pending question. No certificate or publisher may be invented.

Actual Word pagination/style-derived template layout, code language/deeper quote reconstruction, field/file links, PDF complex-script/BiDi/joined-emoji fidelity, tagged PDF accessibility and explicit unsupported Mermaid/HTML/LaTeX forms remain documented limits. Source-only checks and windowless fixtures do not establish destination behaviour.

No local foreground application, Office, clipboard, installer or certificate-store interaction occurred in this batch. The branch remains `feature/native-wpf-rebuild`, with [draft PR 21](https://github.com/DouglasNLima/local-docs-studio/pull/21) targeting `develop`. No merge, public release or tag change is authorised.
