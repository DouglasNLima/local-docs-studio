# Native implementation progress

Current milestone: native internal preview with implemented workflows and growing acceptance evidence. Production is not ready. The separate .NET 10/WPF application preserves the web/PWA/GitHub Pages product and the legacy shell. The full authorised scope is in [the reconstruction brief](reconstruction-brief.txt); implementation and validation remain one continuous goal.

The latest verified packages are unsigned preview.230 from the cfe11e5 pull-request validation merge f1590a4. Both native and both web runs pass. Four application contexts pass 61 desktop checks, including actual diagram clipboard pixels and preservation. Native push 38304289265 passes on attempt 2 after initial UIA window acquisition timed out; the original failure is retained. The subsequent runtime-origin assertion is compiled and committed as 9599171 but awaits its own hosted run.

## Delivered and validated

| Area | Current outcome |
|---|---|
| Documents and workspace | Portable UTF-8 files/assets, native folders/tabs, reviewed moves, source navigation, context-specific search and restoration |
| Data integrity | Conflict checks, independent recovery, snapshots, safe writes and failed-save restart/retry; simulated upgrade/reinstall preserves 20 state files and reopens the session |
| Native preview | Markdown semantics, browser-free Mermaid, maths, images, code colours and source mapping; 40 diagram/export corpus cases and 56 catalogue/theme cases |
| Editing and exports | Native editing/completion, image management, clipboard representations, supported imports, DOCX/PDF/HTML/ZIP/Docs Site exports, libraries and explicit migration |
| Responsiveness | Cooperative nested construction, paginated reading and grouped details expansion; 18 transition checks retain full text/formatting; intermittent initial construction remains unresolved |
| Desktop workflows | At cfe11e5, the native PR passes 61 actual checks in built/installed/upgraded/reinstalled applications |
| Installer | Self-contained isolated per-user product; visible wizard, files-in-use cancellation, installation, same-source upgrade, uninstall and reinstall pass |
| Update trust | User-controlled discovery/download/install, cancellation and final verification; pluggable signing, empty production trust and blocked unsigned execution |

Full feature outcomes and limits remain in the [parity matrix](functional-parity-matrix.md), [architecture decisions](architecture-decisions.md), [experience improvements](native-experience-improvements.md) and [validation report](validation-report.md).

## Current engineering batch

The next compiled check records the application's actual executable and loaded coreclr.dll/wpfgfx_cor3.dll after editor/diagram readiness. Installed, upgraded and reinstalled workflows explicitly require both modules to come from the installation directory; package metadata cannot disable the assertion. Release builds without warnings/errors, static checks and PowerShell parsing pass, and the new option refuses local desktop execution. Hosted results and SDK-free clean-machine certification remain pending.

Three separate local processes rechecked the unresolved construction delay without changing source or timing bounds. All twelve nested cases preserve text/source and pass; maximum input gap is 89.58 ms. Details component initialisation takes 10.77–15.67 ms. Samples under artifacts/native/construction-cfe11e5 do not reproduce or explain the historical failure.

The sealed b45246e..cf436b0 security review found a Low Word allocation issue: compressed output limits did not constrain per-tab in-memory objects. Commit 0c20f64 preflights at most 65,536 expanded code elements across the document before template/package construction. Paragraph/run formatting, text, tabs and breaks are counted; delimiter handling is shared with the writer to preserve CRLF across colour runs. Seven new regressions pass with all 839 core tests and the complete local native harness. Release has zero warnings/errors. All six desktop/Office/installer commands refuse local execution before arguments/paths; static and script checks pass.

The diagram clipboard regression passes in all four cfe11e5 native PR contexts. Keyboard Copy image at 175% and 35% produces the same 471 × 149 pixels as a separately prepared matching-palette reference, persists after closing the zoom window and retains edited/saved Markdown. The reinstalled PNG was inspected. No local clipboard interaction occurred; external Office/Azure DevOps paste behaviour remains separate.

## Evidence and next executable work

- All 0c20f64 runs now pass: native PR/push 38300926460/38300922818, web PR attempt 2/push 38300926434/38300922779. Logs and the original failed browser trace are retained.
- Downloaded push evidence verifies four package hashes, exact clean source, bundled runtime, 839 core tests, four 60-check desktop reports, trust cleanup, five wizard outcomes and 20 preserved lifecycle state files. See artifacts/native/ci-0c20f64-push/evidence-review.json and package-verification.json.
- Completed cfe11e5 native PR 38304292939 and web PR/push 38304296486/38304289262 pass. Downloaded PR evidence verifies all four clipboard/desktop contexts, exact clean merge source, preview.230 package hashes/runtime, 839 core tests, trust cleanup and lifecycle preservation. See artifacts/native/ci-cfe11e5-pr/evidence-review.json and package-verification.json.
- Native push 38304289265 attempt 2 also passes; its explicit artefact 11772379399 is downloaded and verified under artifacts/native/ci-cfe11e5-push-attempt2, including preview.229 packages at clean cfe11e5 and four 61-check contexts. The first attempt's initial UIA timeout, screenshot, session and dump analysis remain under artifacts/native/ci-cfe11e5-push. Push and monitor the subsequent compiled runtime-origin assertion; its results remain separate from these completed packages.
- Continue the remaining acceptance work below; passing focused checks do not establish the full brief.

Current verified artefacts and hashes are in the [engineering handover](engineering-handover.md). Actual foreground/clipboard, Office, installer and certificate-store tests belong to disposable hosted Windows. Windowless native checks may run locally; no installed or personal application state is used.

## Remaining acceptance

Production signing identity and a positive production-signed upgrade, SDK-free clean Windows, physical accessibility/DPI/multi-monitor checks, real historical schema migration, physical power-loss/removable-drive behaviour and live Azure DevOps attachments remain unverified. Intermittent initial WPF delays, broader continuous/page layout, unsupported Mermaid/HTML/LaTeX forms, PDF complex-script/BiDi/joined-emoji fidelity and broader Word template rendering remain explicit limits. No certificate or publisher may be invented. No merge, release publication or tag changes are authorised.

Earlier revision-specific outcomes, failures and exact evidence are retained in [history through 0c20f64](history/implementation-progress-through-0c20f64.md) and [the earlier implementation history](history/implementation-progress-through-9fddfd9.md). Pending statements in those archives describe their recorded revision.
