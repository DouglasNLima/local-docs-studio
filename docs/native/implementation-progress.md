# Native implementation progress

Current milestone: native internal preview with implemented workflows and growing acceptance evidence. Production is not ready. The separate .NET 10/WPF application preserves the web/PWA/GitHub Pages product and the legacy shell. The full authorised scope is in [the reconstruction brief](reconstruction-brief.txt); implementation and validation remain one continuous goal.

The latest verified packages are unsigned preview.234 from the b3a4d59 pull-request merge cbaf55e. Both native and both web runs pass on their first attempts. Downloaded PR and push evidence verifies 839 core tests, 40 diagram/export cases, four 62-check desktop contexts, installed runtime origins and the complete installer lifecycle. Five PDF list outcome groups pass, including exact selectable content across 90 continuation paragraphs; all seven hosted push PDF pages were inspected. The subsequent Word list correction passes 850 core tests and full native validation locally and awaits its own hosted run.

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-specific search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens the session |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours and source mapping; 40 diagram/export corpus cases and 56 catalogue/theme cases |
| Editing and exports | Native editing/completion, image management, clipboard representations, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Responsiveness | Cooperative nested construction, paginated reading and grouped details expansion; 18 transition checks retain full text/formatting; intermittent initial construction remains unresolved |
| Desktop workflows | At b3a4d59, both native runs pass 62 actual checks in built/installed/upgraded/reinstalled applications |
| Installer | Self-contained isolated per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall pass |
| Update trust | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

Full feature outcomes and limits remain in the [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md).

## Current engineering batch

Word export had the same continuation defect: seven new regressions first failed on repeated numbering, missing first-block markers and omitted empty items. The correction retains native editable numbering, explicit unnumbered continuations, nested scopes, template styles and bounded image/table widths. Eleven core regressions now pass, including custom cell margins and code-allocation preflight. All 850 core tests, the complete native harness and static checks pass locally. Native preparation retains 12 marked items plus first-block Mermaid/formula images in schema-valid DOCX packages. Evidence is under artifacts/native/word-lists-before, word-lists-after and word-list-width-validation. Hosted validation and actual Word pagination/wide-marker/accessibility checks remain pending.

PDF export now keeps one marker per list item and applies a distinct indentation at each nested level. Continuation paragraphs, quotes, headings and code retain their item alignment; tables and images fit the remaining width. The original fixture reproduced three copies of `7.` and flat subitems. The corrected PDF, its nested/table variants and all five pagination pages were inspected. Release builds without warnings/errors, all 839 core tests, static checks and the full 40-case native harness pass locally. The focused regression also checks exact selectable word order across 90 continuation paragraphs. Hosted validation now passes in both b3a4d59 native runs, with all seven hosted push PDF pages inspected.

The loaded-runtime check passes in native PR 38307579534. The actual executable matches the expected application in all four contexts, each loads one coreclr.dll/wpfgfx_cor3.dll, and all three installed contexts load both from their installation directory. The requirement is explicit and cannot be disabled by package metadata. Downloaded reports, clean source and all four package hashes are verified under artifacts/native/ci-56502e3-pr. The new option refuses local desktop execution. SDK-free clean-machine certification remains separate.

Three separate local processes rechecked the unresolved construction delay without changing source or timing bounds. All twelve nested cases preserve text/source and pass; maximum input gap is 89.58 ms. Details component initialisation takes 10.77–15.67 ms. Samples under artifacts/native/construction-cfe11e5 do not reproduce or explain the historical failure.

The sealed b45246e..cf436b0 security review found a Low Word allocation issue: compressed output limits did not constrain per-tab in-memory objects. Commit 0c20f64 preflights at most 65,536 expanded code elements across the document before template/package construction. Paragraph/run formatting, text, tabs and breaks are counted; delimiter handling is shared with the writer to preserve CRLF across colour runs. Seven new regressions pass with all 839 core tests and the complete local native harness. Release has zero warnings/errors. All six desktop/Office/installer commands refuse local execution before arguments/paths; static and script checks pass.

The diagram clipboard regression passes in all four cfe11e5 native PR contexts. Keyboard Copy image at 175% and 35% produces the same 471 × 149 pixels as a separately prepared matching-palette reference, persists after closing the zoom window and retains edited/saved Markdown. The reinstalled PNG was inspected. No local clipboard interaction occurred; external Office/Azure DevOps paste behaviour remains separate.

## Evidence and next executable work

- All 0c20f64 runs now pass: native PR/push 38300926460/38300922818, web PR attempt 2/push 38300926434/38300922779. Logs and the original failed browser trace are retained.
- Downloaded push evidence verifies four package hashes, exact clean source, bundled runtime, 839 core tests, four 60-check desktop reports, trust cleanup, five wizard outcomes and 20 preserved lifecycle state files. See artifacts/native/ci-0c20f64-push/evidence-review.json and package-verification.json.
- Completed cfe11e5 native PR 38304292939 and web PR/push 38304296486/38304289262 pass. Downloaded PR evidence verifies all four clipboard/desktop contexts, exact clean merge source, preview.230 package hashes/runtime, 839 core tests, trust cleanup and lifecycle preservation. See artifacts/native/ci-cfe11e5-pr/evidence-review.json and package-verification.json.
- Native push 38304289265 attempt 2 also passes; its explicit artefact 11772379399 is downloaded and verified under artifacts/native/ci-cfe11e5-push-attempt2, including preview.229 packages at clean cfe11e5 and four 61-check contexts. The first attempt's initial UIA timeout, screenshot, session and dump analysis remain under artifacts/native/ci-cfe11e5-push. The later runtime-origin assertion passes in 56502e3 as recorded above; monitor the subsequent PDF list correction separately.
- Continue the remaining acceptance work below; passing focused checks do not establish the full brief.

Current verified artefacts and hashes are in the [engineering handover](engineering-handover.md). Actual foreground/clipboard, Office, installer and certificate-store tests belong to disposable hosted Windows. Windowless native checks may run locally; no installed or personal application state is used.

## Remaining acceptance

Production signing identity and a positive production-signed upgrade, SDK-free clean Windows, physical accessibility/DPI/multi-monitor checks, real historical schema migration, physical power-loss/removable-drive behaviour and live Azure DevOps attachments remain unverified. Intermittent initial WPF delays, broader continuous/page layout, unsupported Mermaid/HTML/LaTeX forms, PDF complex-script/BiDi/joined-emoji fidelity and broader Word template rendering remain explicit limits. No certificate or publisher may be invented. No merge, release publication or tag changes are authorised.

Earlier revision-specific outcomes, failures and exact evidence are retained in [history through 0c20f64](history/implementation-progress-through-0c20f64.md) and [the earlier implementation history](history/implementation-progress-through-9fddfd9.md). Pending statements in those archives describe their recorded revision.
