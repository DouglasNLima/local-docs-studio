# Native validation report

Status: implementation in progress; not certified for internal preview or production.

## Baseline (11 October 2026)

- Revision: 2cc3b5680958c9c553dad23856e6010c158fccd7 (origin/develop).
- npm ci: passed, zero reported vulnerabilities.
- npm test: passed; 254 Chromium/Edge tests in 9.3 minutes. Local static server exercised throughout.
- Static checks: 49 modules, 56 shell assets, 150 vendor assets and 55 external-dependency scans passed.
- dotnet build src/windows/LensDocsStudio.Windows.sln -c Release: passed, 0 errors, 6 pre-existing PRI qualifier warnings.
- Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -KeepSmokeRoot: passed.
- Evidence: artifacts/native/baseline/web-tests.log, legacy-build.log, legacy-smoke.log.
- Legacy fixture root: %TEMP%/lens-docs-studio-native-smoke-6551c34c70ad4597a8030c76c69e0f82.

## Acceptance items

Native Release build: passed with 0 warnings and 0 errors. Core tests: 67 passed, including signed update policy, semantic version ordering, download integrity, publisher failure and redirect rejection. Locked solution restore passed, including win-x64 packaging dependencies.

An actual WPF window was launched against a copied mixed.md fixture using isolated state in artifacts/native/gui/state. Computer Use observed native editor/preview, diagram labels and shapes, outline and automation names. Ctrl+F selected the editor context; search selected Checklist; Escape restored editor focus. Unicode source was saved, inspected on disk and restored after reopening.

FlaUI UIA3 automation passed six outcomes: startup file loading, editor search/focus/input, actual UTF-8 save, preview-context search, session restoration and persisted update-channel settings with untrusted download/install actions disabled. Both application processes exited successfully. Evidence: artifacts/native/ui/native-ui.json. The harness caught a re-entrant closing defect, fixed by leaving the Closing event before asynchronous save and final shutdown. This does not certify all keyboard/accessibility/DPI scenarios.

The expanded Mermaid corpus exercises 40 light/dark cases with zero unexpected failures. Incomplete autocomplete.mmd (source: flo) and broken-mermaid.md are explicitly expected-invalid. Flowchart and sequence PNGs were visually inspected; broader visual fidelity is still open. Evidence: artifacts/native/validation/mermaid-corpus.json.

Mixed-document exports produced schema-valid DOCX, native PDF and standalone HTML. The PDF contains one A4 page, readable extracted text and one diagram image; Poppler rendering was inspected for clipping and overlap. PDF output is currently untagged. Core tests inspect Word template headers, footers, media, style mapping and relationships; this is partial template validation, not complete equivalence.

The native PR and push CI jobs passed at 3fe3cb7. Web PR CI passed; one push Edge test timed out, passed three isolated local repetitions, then passed the failed-job rerun (run 38140730585). The import/export/package/update milestone still requires fresh CI.

The 0.1.0-preview.3 self-contained x64 package compiled with Inno Setup. Installer: 58,172,249 bytes; portable ZIP: 83,334,923 bytes. An actual per-user lifecycle test passed installation, shortcuts, Open with registration, installed-app save/reopen, uninstall, reinstall and final uninstall. Workspace source, Markdown defaults and legacy shell registration were preserved. Evidence: artifacts/native/install-smoke/dd4b6d53aed1467eb8f533a615918e72/installer-results.json. This was the development host, not clean-machine certification; a different-version upgrade was not included in this run.

The actual Windows Authenticode API rejected the unsigned installer with 0x800B0100. RSA manifest signing and installer trust boundaries have deterministic development tests. Positive production signer verification and the helper-driven signed upgrade flow remain unvalidated; no trusted signing identity is embedded. Updated packages will additionally include the update UI and generated dependency notices.

Cold mixed-document preview observed in the UI: 4092 ms on this host. The isolated corpus's first native render was 717 ms; subsequent simple render 35 ms. These are observations, not performance certification.

Subsequent desktop work passes seven UI outcomes, adding an actual native Mermaid window and a 175% zoom assertion. Latest evidence remains artifacts/native/ui/native-ui.json. Light/dark controls, clamped working-area sizing and diagram windows were visually inspected. Per-monitor DPI metadata and placement persistence are implemented but multi-monitor and screen-reader validation remain open.

The SVG safety harness rejects eight active/external-resource payloads and preserves local fragment paint definitions. All 40 diagram cases still pass after validation was added. Evidence: artifacts/native/validation/native-svg-safety.json and validation-summary.json. A single warm fixture took 67.5884 ms for first conversion and 0.075 ms for twenty cached reuses; this measures cache reuse, not full preview or large-document performance.

Preview 4 to preview 5 upgrade, uninstall, reinstall and final uninstall passed with actual installed-app UI checks and preservation assertions. Evidence: artifacts/native/install-smoke/9519c7521c754b94a822a7c035520bea/installer-results.json. This is development-host evidence; cleanMachineCertification remains false.

At 5ee7202, native PR CI 38144532410 and push CI 38144529767 passed, including the fresh Windows runner's installer lifecycle; web PR CI 38144532440 passed. Web push 38144529782 failed an Edge readiness check; its rerun failed different Chromium rendering and Edge readiness checks (the latter reported ERR_CONNECTION_REFUSED). No web runtime source changed. Logs: artifacts/native/web-ci-5ee7202-failure.log and web-ci-5ee7202-rerun-failure.log. Three local repetitions of the original Edge test passed. The push run is not claimed as passed.

Broad interactive GUI, complete export fidelity, performance, installer, update trust, clean-machine and final CI certification remain pending. No unexecuted check is reported as passed.

## Navigation, assets and recovery follow-up

Release build passes without warnings/errors. Core suite: 78 passed (artifacts/native/tests/native-core.trx). New coverage includes navigation branching, bounded recent paths, portable link encoding, shared image references, Save as rebasing/cancellation, drive-root containment and malformed recovery preservation.

Native UI Automation passes 11 outcomes (artifacts/native/ui/native-ui.json, fixture ui-a9cf84c3d3cb48c5a2d7e65bad9c20b9). It inserts a document link through the native picker, invokes the real WPF hyperlink via UIA TextPattern, tests Alt+Left/Right and Back, checks recent-path persistence, pastes an actual bitmap into an unsaved tab and saves it through the Windows file dialogue. The saved Markdown and PNG are inspected on disk. A text-only paste enablement defect discovered in this run was corrected with native command routing. The harness checks foreground process identity before keyboard sequences.

Native asset integration also passes recovery of an unsaved image, real PNG decoding, image resolution for exports after Save as, and insertion anchors preserving intervening edits (artifacts/native/validation/native-assets.json). All 40 diagram cases and existing export checks pass. Draft images remain retained for recovery; automated cache pruning is not implemented.

At 8b31163, both native CI runs (38146723379 and 38146720387) passed and web push CI 38146720477 passed. Web PR CI 38146723536 failed three different Edge cases; all nine local repetitions of those cases passed (artifacts/native/baseline/edge-8b31163-recheck.log). Failure log retained as artifacts/native/web-ci-8b31163-failure.log. This follow-up batch still needs its own CI results.
