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

Native CI at 78a9f0e passed build, 78 core tests, diagrams, exports and native asset integration, then failed the Save-dialogue UI step in runs 38148772391 and 38148769157. Downloaded evidence in artifacts/native/ci-78a9f0e showed a valid save under Untitled.md: UIA SetValue had not committed the test's requested filename. The harness now types and verifies the filename, then clicks Save. The corrected eleven-step desktop run passes locally. An additional core regression covers forced finalisation out of private draft folders even when the state folder is nested inside the selected workspace. Busy operations are serialised rather than silently ignored; native integration checks cover their completion order. Fresh CI is required for these corrections.

## Filesystem changes

At df784df, native PR CI 38149596203 and push CI 38149591752 both passed, including the 79-test core suite, corrected desktop workflow, packaging and installed-app lifecycle. Web PR CI 38149596198 passed; web push 38149591816 failed (log retained as artifacts/native/web-ci-df784df-failure.log).

The subsequent filesystem batch builds without warnings/errors and passes 87 core tests. New tests cover incoming/reference links and source rebasing, BOM/line-ending preservation, opting out of link changes, collision/traversal rejection, external edits after planning, early and late write failures with rollback, interrupted-plan recovery without filesystem replay, and navigation paths after rename. Evidence: artifacts/native/tests/native-core.trx.

The native desktop harness passes 13 outcomes, adding real rename/review, on-disk incoming-link changes, refresh of open editor text, and creating/editing/saving a workspace file. Evidence: artifacts/native/ui/native-ui.json. File/folder creation and reviewed document moves are implemented; deletion, folder moves and case-only renames remain open. This filesystem batch needs its own CI validation.

At ecc5fba, native PR CI 38151048556 and push CI 38151043810 passed, including installed-app lifecycle. Web PR CI 38151048570 passed. The rename desktop check now includes unsaved incoming-link edits, preservation through Save, and a second review of the rebuilt move plan.

The snapshot/deletion follow-up passes 95 core tests and 15 native desktop outcomes. Core coverage checks independent image copies, exact source/BOM retention, missing-image warnings, legacy/malformed snapshots, shell cancellation, external changes and rollback destination collisions. The desktop harness deletes an isolated fixture through the real Windows shell, verifies separate saved/unsaved snapshots, inspects the unsaved version and saves its recovered draft to an explicitly chosen destination. Evidence: artifacts/native/tests/native-core.trx and artifacts/native/ui/native-ui.json. The first desktop run exposed a status message being hidden by preview completion; the two status fields are now independent. Folder moves and case-only renames remain open; this follow-up requires fresh CI.

At ecc5fba, web push CI 38151043807 also passed, completing all four native/web runs. At c7467b4, native PR CI 38151922098 and push CI 38151918564 passed, including packaging and actual installed snapshot/deletion workflows; web PR CI 38151921996 passed.

The next image-download batch builds without warnings/errors and passes 104 core tests. Transport checks cover HTTPS redirects, credentials/scheme rejection, request privacy, content types, empty/oversized bodies, streamed limits and cancellation/timeouts. Native integration passes a simulated HTTPS response through the actual Windows decoder and PNG persistence, rejects spoofed SVG bytes, and rejects a valid 81-megapixel compressed PNG before creating its pixel cache. All 40 diagram cases, export checks and the 15 desktop outcomes pass again. Fresh CI remains pending.

Web push CI 38151918586 at c7467b4 failed two Edge readiness checks with ERR_CONNECTION_REFUSED; log: artifacts/native/web-ci-c7467b4-failure.log. The test server follow-up uses HTTP/1.1, a 128-connection listen queue and a configurable local port (the default remains 4173). CI owns its server and retains failure traces/screenshots. No web runtime module or production hosting requirement changes.

Local validation of that follow-up: static checks passed; the full browser run passed 250 and failed four in 16.8 minutes (artifacts/native/baseline/web-static-server-check.log). Traces showed the native-workspace creation test in both browsers checking the menu before the preceding asynchronous action closed it. That test now waits for closure before reopening. Two other Chromium tests failed while creating a browser context because the browser process was closed. All four passed in a focused 36.7-second rerun (web-static-server-recheck.log). Initial failure traces remain under artifacts/native/web-static-server-first-results. This is not reported as an entirely green full run; fresh CI must validate the combined change.

Rendered-image copying adds a 16th passing desktop outcome: the actual native image context menu copies bitmap pixels, a new document pastes them, and Save creates a distinct image asset while retaining the original. The harness restores its previous clipboard only if the Windows clipboard sequence still belongs to the test, preserving intervening clipboard updates by other applications. Evidence: artifacts/native/ui/native-ui.json, fixture ui-3cc0e9c835f547b98c9b8a8a396d7619.

## Workspace image management

The asset-management batch builds without warnings/errors and passes 122 core tests. Coverage includes reference-style and encoded links, draft overrides, missing assets, embedded HTML, opaque references, native-state exclusion, binary-preserving rename, new references after review, destination conflicts, rollback after a later locked document, independent removal snapshots, cancelled recycling, external replacement preservation and interrupted journal reads without filesystem replay. Evidence: artifacts/native/tests/native-core.trx.

The desktop harness now contains 18 outcomes, adding reviewed image rename with unsaved text and Windows image removal followed by snapshot recovery/Save. These new outcomes are not yet reported as passed: two local attempts stopped at the existing first search step with FindText unavailable; a third could not acquire foreground focus. Evidence: artifacts/native/ui-asset-first-attempt and artifacts/native/ui/native-ui.json. No fixture image was removed during these failed attempts.

At 5210d81 native PR CI 38153333135 and push CI 38153330229 passed. Web PR CI 38153332644 passed 238 and failed 16; push CI 38153330231 passed 244 and failed 10. Logs and PR traces are retained in artifacts/native/web-ci-5210d81-*. Traces expose an emoji assertion racing the subsequent Rendered status, a bridge message assertion made before asynchronous dispatch, and an import test waiting indefinitely for worker interception. Separate browser-context closures remain unexplained. These failures are not a green web certification.

At 05e9fec, native push CI 38155146121 passed all 18 desktop outcomes, self-contained packaging and the installed application/uninstall lifecycle. Native PR CI 38155150412 passed build/core/diagram/export checks, then its screenshot showed Untitled.md after the harness attempted Create workspace file: the coordinate click selected the adjacent New document item during menu layout. Menu commands in the harness now use the native Invoke pattern. Each future harness result is retained in its individual fixture directory as well as the latest summary.

Three web timing corrections passed twice in both browsers (12/12, 50.2 seconds; artifacts/native/baseline/web-timing-recheck.log). The bridge assertion now awaits dispatch; the emoji check verifies the lasting source/preview outcome; the DOCX test gates the real worker input instead of relying on interception of a potentially cached script. Its prior failure trace showed conversion had already completed while the test waited for interception. CI browser-process diagnostics are enabled to investigate the remaining context closures. The complete local follow-up is running; no result is assumed.
