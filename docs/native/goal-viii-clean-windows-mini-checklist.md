# Later clean Windows 11 acceptance — NOT RUN

This standalone operator checklist is for a later, independently documented clean
x64 Windows 11 system. Goal VIII uses the existing development computer and does
not satisfy this checklist. Keep the detailed [Goal VII protocols](goal-vii-certification-runbooks.md)
for exact family, DPI, audible-reader and historical-lineage procedures.

## Select and verify the exact bytes

Use the final Draft PR engineering receipt to select a feature package by its
complete source SHA/tree, CI run/attempt, version, artefact expiry and independently
calculated installer/portable SHA-256. A matching version name alone is insufficient.
Do not replace feature-source evidence with integrated-develop evidence.

The frozen **baseline**, suitable only when deliberately testing that baseline, is:

| Pin | Expected value |
|---|---|
| Source | `9b1e73bbf564c53b48149e8c0dcd6fd7acbe308f` |
| Tree | `1fb777317ad6e5c8531eebcb565fefae4227e354` |
| Version | `0.1.1-preview.871` |
| Installer SHA-256 | `ef2320c6efdfe344d7a39190e3cb4187b15377b1448b3452a0f9599266fbd2bb` |
| Portable SHA-256 | `a56e95e450cf9ca36d50199ee8ad825c5e701c4cf5e35170a47da2d9b269babc` |
| Native run / attempt | `40108034236` / `1` |
| Artefact / expiry | `12566795323` / 7 November 2026, 16:10:15 UTC |

On a separate validation station, independently inspect original archive bytes,
sidecars, source/tree, unsigned status, Core/WPF 10.0.13 and the 528-file payload
with `Test-NativePreviewPackage.ps1`. Retain its report and raw checksum files.
Copy verified bytes into the clean system without adding an SDK or validation
toolchain to it. Expired or missing original bytes mean BLOCKED, never a guessed hash.

## Operator record

For every row record PASS, FAIL, BLOCKED or NOT RUN, UTC time, exact package/source,
OS edition/build/UBR, user elevation, evidence filenames and operator observations.
Use only new synthetic workspaces. Stop at failures and preserve them. Never bypass
SmartScreen, organisation policy or trust warnings.

| Check | Required observation | Current result |
|---|---|---|
| Machine provenance | Independently documented Windows 11 image/system and clean baseline; standard user; no previous native/legacy product, SDK, Visual Studio, MSBuild, Node/npm, Python or other development tools, including outside PATH | NOT RUN |
| Read-only qualification | Run unchanged `Test-NativePreviewPreflight.ps1 -EnvironmentOnly` before introducing tools; PARTIAL is not certification; retain installed-app, shortcut, association and private-state observations | NOT RUN |
| Actual installation | Visible unsigned wizard, Back/Cancel, explicit test install path, no file associations, no defaults change; no elevation or certificate installation | NOT RUN |
| First launch | About source/version matches pins; actual CoreCLR/WPF modules load from installed payload; native preview works without SDK/server/browser renderer | NOT RUN |
| Workspace and exports | LF/CRLF, Café😀🚀, duplicate/long headings, lists/tables/details, relative image/document link and standalone Mermaid; real DOCX/PDF/PNG/HTML/Docs Site; inspect Unicode, coloured code, canonical diagram images/source and asset hashes | NOT RUN |
| Actual 125% and 150% DPI | Operator changes scale only in the authorised clean session; record Settings, real HWND DPI (120/144), physical bounds, pointer/keyboard/viewport/export checks for all 24 families at 880×600 and 1280×800 logical sizes | NOT RUN |
| Physical reader | Real Narrator/NVDA/JAWS version and human listening transcript; full family names/occurrences, labelled properties, errors, disabled actions, focus return and keyboard traps; UIA counts alone do not pass | NOT RUN |
| Owner and recovery | Private validation, Apply/Cancel, one-step owner Undo/Redo, stale-owner rejection, saved and untitled dirty buffers, interrupted save, restart/reopen and external-change conflict preserve exact source and assets | NOT RUN |
| Historical upgrade | Separately authorised original pinned historical binaries AND original state/workspace, pre/post hashes and migration observations; the same-source two-version pair is SYNTHETIC | NOT RUN |
| Uninstall and state retention | Normal close, uninstall, exact document/private-state retention, reinstall restores state; final native registration/shortcuts removed and original associations unchanged; never delete workspaces/private state | NOT RUN |

For rollback, preserve failed current state first, then restore only into a separate
copied workspace/profile in an authorised disposable snapshot with its matching
original version. Do not overwrite newer data or perform an unproved downgrade.
Production signing, release, updater publication, associations and public deployment
require separate decisions. **PUBLIC PRODUCTION remains BLOCKED.**
