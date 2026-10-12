# Native implementation progress

Current milestone: native internal preview with implemented workflows and growing acceptance evidence. Production is not ready. The separate .NET 10/WPF application preserves the web/PWA/GitHub Pages product and the legacy shell. The full authorised scope is in [the reconstruction brief](reconstruction-brief.txt); implementation and validation remain one continuous goal.

The latest fully verified packages are unsigned preview.225 from cf436b0. Both native and both web CI runs pass. The later Word allocation correction is committed and pushed as 0c20f64; its CI is running. A separate diagram clipboard regression is compiled locally and awaits its own hosted run.

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-specific search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens the session |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours and source mapping; 40 diagram/export corpus cases and 56 catalogue/theme cases |
| Editing and exports | Native editing/completion, image management, clipboard representations, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Responsiveness | Cooperative nested construction, paginated reading and grouped details expansion; 18 transition checks retain full text/formatting; intermittent initial construction remains unresolved |
| Desktop workflows | At cf436b0, 60 actual checks pass in built/installed/upgraded/reinstalled applications |
| Installer | Self-contained isolated per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall pass |
| Update trust | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

Full feature outcomes and limits remain in the [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md).

## Current engineering batch

The sealed b45246e..cf436b0 security review found a Low Word allocation issue: compressed output limits did not constrain per-tab in-memory objects. Commit 0c20f64 preflights at most 65,536 expanded code elements across the document before template/package construction. Paragraph/run formatting, text, tabs and breaks are counted; delimiter handling is shared with the writer to preserve CRLF across colour runs. Seven new regressions pass with all 839 core tests and the complete local native harness. Release has zero warnings/errors. All six desktop/Office/installer commands refuse local execution before arguments/paths; static and script checks pass.

The next desktop regression activates diagram Copy image by keyboard at 175% and 35% zoom. A separate process compares every clipboard pixel and both dimensions with an independent matching-palette rendering, waits for the zoom window to close, then checks persistence and unchanged edited/saved Markdown. Compilation and static checks pass; no local clipboard interaction occurred. Actual hosted execution remains pending. This does not certify external Office or Azure DevOps paste behaviour.

## Evidence and next executable work

- Monitor 0c20f64 native PR/push 38300926460/38300922818 and web PR/push 38300926434/38300922779 through terminal results; inspect failures before changing assertions.
- Download and verify the completed push artefact against the exact source revision, including package hashes/runtime, trust cleanup, desktop reports and installer preservation.
- Push the compiled diagram clipboard regression after retaining the current run's result; validate the new 61st desktop workflow in all four application contexts.
- Continue the remaining acceptance work below; passing focused checks do not establish the full brief.

Current verified artefacts and hashes are in the [engineering handover](engineering-handover.md). Actual foreground/clipboard, Office, installer and certificate-store tests belong to disposable hosted Windows. Windowless native checks may run locally; no installed or personal application state is used.

## Remaining acceptance

Production signing identity and a positive production-signed upgrade, SDK-free clean Windows, physical accessibility/DPI/multi-monitor checks, real historical schema migration, physical power-loss/removable-drive behaviour and live Azure DevOps attachments remain unverified. Intermittent initial WPF delays, broader continuous/page layout, unsupported Mermaid/HTML/LaTeX forms, PDF complex-script/BiDi/joined-emoji fidelity and broader Word template rendering remain explicit limits. No certificate or publisher may be invented. No merge, release publication or tag changes are authorised.

Earlier revision-specific outcomes, failures and exact evidence are retained in [history through 0c20f64](history/implementation-progress-through-0c20f64.md) and [the earlier implementation history](history/implementation-progress-through-9fddfd9.md). Pending statements in those archives describe their recorded revision.
