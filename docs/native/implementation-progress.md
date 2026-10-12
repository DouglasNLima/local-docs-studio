# Native implementation progress

Current milestone: native internal preview with implemented workflows and growing acceptance evidence. Production is not ready. The separate .NET 10/WPF application preserves the web/PWA/GitHub Pages product and the legacy shell. The full authorised scope is in [the reconstruction brief](reconstruction-brief.txt); implementation and validation remain one continuous goal.

The latest verified packages are unsigned preview.227 from 0c20f64, including the Word allocation correction and hosted-only test guards. Both native runs and the web push pass; the web PR passes on its second attempt after a Chromium teardown crash with all test assertions passing. Original failure evidence is retained. A separate diagram clipboard regression is compiled and committed as 9c07ab7 and awaits its own hosted run.

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-specific search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens the session |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours and source mapping; 40 diagram/export corpus cases and 56 catalogue/theme cases |
| Editing and exports | Native editing/completion, image management, clipboard representations, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Responsiveness | Cooperative nested construction, paginated reading and grouped details expansion; 18 transition checks retain full text/formatting; intermittent initial construction remains unresolved |
| Desktop workflows | At 0c20f64, 60 actual checks pass in built/installed/upgraded/reinstalled applications |
| Installer | Self-contained isolated per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall pass |
| Update trust | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

Full feature outcomes and limits remain in the [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md).

## Current engineering batch

The sealed b45246e..cf436b0 security review found a Low Word allocation issue: compressed output limits did not constrain per-tab in-memory objects. Commit 0c20f64 preflights at most 65,536 expanded code elements across the document before template/package construction. Paragraph/run formatting, text, tabs and breaks are counted; delimiter handling is shared with the writer to preserve CRLF across colour runs. Seven new regressions pass with all 839 core tests and the complete local native harness. Release has zero warnings/errors. All six desktop/Office/installer commands refuse local execution before arguments/paths; static and script checks pass.

The next desktop regression activates diagram Copy image by keyboard at 175% and 35% zoom. A separate process compares every clipboard pixel and both dimensions with an independent matching-palette rendering, waits for the zoom window to close, then checks persistence and unchanged edited/saved Markdown. Compilation and static checks pass; no local clipboard interaction occurred. Actual hosted execution remains pending. This does not certify external Office or Azure DevOps paste behaviour.

## Evidence and next executable work

- All 0c20f64 runs now pass: native PR/push 38300926460/38300922818, web PR attempt 2/push 38300926434/38300922779. Logs and the original failed browser trace are retained.
- Downloaded push evidence verifies four package hashes, exact clean source, bundled runtime, 839 core tests, four 60-check desktop reports, trust cleanup, five wizard outcomes and 20 preserved lifecycle state files. See artifacts/native/ci-0c20f64-push/evidence-review.json and package-verification.json.
- Push and monitor the compiled diagram clipboard regression, validating the new 61st desktop workflow in all four contexts. Inspect failures before changing assertions.
- Continue the remaining acceptance work below; passing focused checks do not establish the full brief.

Current verified artefacts and hashes are in the [engineering handover](engineering-handover.md). Actual foreground/clipboard, Office, installer and certificate-store tests belong to disposable hosted Windows. Windowless native checks may run locally; no installed or personal application state is used.

## Remaining acceptance

Production signing identity and a positive production-signed upgrade, SDK-free clean Windows, physical accessibility/DPI/multi-monitor checks, real historical schema migration, physical power-loss/removable-drive behaviour and live Azure DevOps attachments remain unverified. Intermittent initial WPF delays, broader continuous/page layout, unsupported Mermaid/HTML/LaTeX forms, PDF complex-script/BiDi/joined-emoji fidelity and broader Word template rendering remain explicit limits. No certificate or publisher may be invented. No merge, release publication or tag changes are authorised.

Earlier revision-specific outcomes, failures and exact evidence are retained in [history through 0c20f64](history/implementation-progress-through-0c20f64.md) and [the earlier implementation history](history/implementation-progress-through-9fddfd9.md). Pending statements in those archives describe their recorded revision.
