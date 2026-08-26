# Lens Docs Studio v0.1.0-dev.1 Certification Plan

## Context

- Current published prerelease: `v0.1.0-dev` at `https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev`
- Current develop commit before Phase 3U.2: `b9cefdc6dab5363b3d1cb82cf50f10b61a7ea6f1`
- Previous release target: `v0.1.0-dev`
- Planned release tag: `v0.1.0-dev.1`
- Planning date: `2026-06-10`
- Repository: `DouglasNLima/local-docs-studio`
- Branch policy: work directly on `develop`; do not modify the published `v0.1.0-dev` release.

## Scope Classification

- [ ] DOCS_ONLY
- [ ] PACKAGE_AFFECTING
- [ ] INSTALLER_AFFECTING
- [x] PACKAGE_AND_INSTALLER_AFFECTING

The original planning scope was release documentation, guidance, and manual evidence tracking only. Phase 3U.2 added a temporary browser UI diagnostic aid under Help so operators can inspect Windows shell, native bridge, capability, and Open folder routing state before retrying manual watcher evidence.

Phase 3V changes runtime UI routing for **File > Open folder** in the packaged WebView2 shell. Because the packaged static app contents changed, `v0.1.0-dev.1` is no longer docs-only and requires fresh ZIP and installer artefacts if published.

Phase 3W is an evidence/artefact-alignment checkpoint for the Phase 3V package-affecting change. It rebuilt local ZIP, package RC, and Inno installer artefacts from `develop` commit `e2026d728c131cf2e203928487b9aeb09b744da7`, but did not publish or upload them.

Phase 3AB is an artefact rebaseline checkpoint for the Phase 3AA native picker completion hardening. It rebuilt local ZIP, package RC, and Inno installer artefacts from `develop` commit `055e51dedacd28ea277a480b60bd0333feec6004`, after Phase 3AA completed packaged Stage A diagnostics, real native picker workspace selection, and Stage B watcher/conflict evidence.

Phase 3AD is the authoritative local publication bundle freeze for `v0.1.0-dev.1`. It froze the final local ZIP, installer, installer checksum, installer report, and release-candidate metadata under `artifacts/windows/publication-bundles/v0.1.0-dev.1/` from source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff`. Phase 3AD did not publish anything and did not create or modify release assets, tags, releases, or `main` merges.

Phase 3AF is the read-only post-publication verification checkpoint for the published `v0.1.0-dev.1` GitHub prerelease. It verifies the tag, prerelease state, published asset set, downloaded asset hashes, downloaded ZIP smoke, and a contained installer smoke in `docs/release/phase-3af-post-publication-verification.md`. Phase 3AF does not change release assets, tags, releases, runtime code, or `main`, and does not claim production readiness or go-live approval.

Phase 3AG closes the `v0.1.0-dev.1` prerelease evidence chain as documentation-only release closure and roadmap rebaseline. `v0.1.0-dev.1` is published as a prerelease, post-publication verification passed, Stage A and Stage B packaged evidence passed, and no release assets require further action unless a future release is authorised.

## Changes Since v0.1.0-dev

- WebView2 uninstall cleanup decision: `WEBVIEW2_UNINSTALL_CLEANUP_DOCUMENTED_ONLY`. The WebView2 user data folder may contain browser-local user/session state, so silent uninstall cleanup is not approved for this dev cycle.
- Watcher/conflict evidence: checklist added in `docs/release/lens-docs-studio-watcher-conflict-manual-evidence.md`; the Phase 3U.1 packaged ZIP retry launched the app, but the manual evidence is `BLOCKED` at the folder-picker gate because **Open folder** produced an `Open` file dialog and the app reported `Native bridge did not respond.`
- Phase 3U.2 diagnostic aid: **Help > Windows shell diagnostics** reports browser/PWA versus WebView2 shell mode, bridge ping result, safe capability labels, Open folder routing, browser fallback route, and the next operator step without showing local absolute paths.
- Phase 3V Open folder routing fix: **File > Open folder** now uses a fresh native capability probe and fails closed in WebView2 when `workspace.openFolder` is missing or the bridge does not respond, instead of silently falling through to the browser folder/file-input route.
- Phase 3W checkpoint: fresh local package, package RC, and Inno installer artefacts were rebuilt from current `develop`; automated static, browser, Windows build, package RC, static asset, and native smoke validation passed. Manual packaged diagnostics and watcher/conflict evidence remained blocked because this agent session could launch the packaged app but could not operate and observe the real WebView2 diagnostics UI.
- Phase 3X checkpoint: the Phase 3W packaged executable was operated through the real WebView2 UI, **Help > Windows shell diagnostics** was opened, **Retry bridge check** was used, and diagnostics passed with `workspace.openFolder` available and Open folder routing set to `native bridge`. Stage B watcher/conflict evidence remains blocked because a temporary workspace could not be selected through a reliable packaged UI route in this agent session; the debug-launched UI reported `Native bridge did not respond.` when **Open folder** was clicked.
- Phase 3Y checkpoint: the `Native bridge did not respond.` blocker was diagnosed as a web bridge request timeout mismatch. Diagnostics used a quick ping, but `workspace.openFolder` opened an interactive Windows folder picker through the same `2500 ms` command timeout. Phase 3Y keeps the quick timeout for diagnostics and ordinary bridge probes, gives `openFolder()` a `300000 ms` interactive picker timeout, adds delayed-picker regression coverage, rebuilds the local package, and re-checks packaged diagnostics. Stage B remains `BLOCKED_STAGE_B_WORKSPACE_NOT_SELECTED` because this agent session still could not select the temporary workspace through the real packaged folder picker.
- Phase 3AA checkpoint: native folder picker owner HWND resolution, UI dispatcher marshalling, foregrounding, structured picker errors, and cancellation preservation were hardened. The rebuilt packaged app completed Stage A diagnostics, selected a workspace through the real native picker, and passed Stage B clean external change, dirty conflict cancel, and dirty conflict confirm scenarios.
- Phase 3AB checkpoint: fresh local package, package RC, and Inno installer artefacts were rebuilt from Phase 3AA commit `055e51dedacd28ea277a480b60bd0333feec6004`; all requested static, browser, Windows build, package RC, static asset, native smoke, packaged native smoke, package build, and installer build validation passed. The Phase 3W artefacts from `e2026d728c131cf2e203928487b9aeb09b744da7` are superseded and stale.
- Phase 3AF checkpoint: after publication, the `v0.1.0-dev.1` prerelease, tag, exact six-asset set, downloaded ZIP and installer hashes, extracted ZIP static assets, packaged native bridge smoke, and contained installer install/smoke/uninstall behaviour were verified. No GitHub release assets, tags, releases, `main` merges, runtime code, production readiness claim, or go-live approval changed.
- Phase 3AG checkpoint: release closure records `v0.1.0-dev.1` as a published and verified prerelease/dev release. The older `v0.1.0-dev` assets remain older/stale relative to `v0.1.0-dev.1` and were not modified. No further release-asset action is required unless a future release is authorised.
- ZIP vs installer wording: clarified across release guidance so ZIP is the portable/fallback package and the Inno installer is the easier Windows install path.
- Any runtime/package/installer config changes: browser UI diagnostics and Open folder routing changed; package source contents changed, installer config is unchanged, and Phase 3AG does not change release assets, tags, or releases.

## Manual Watcher/Conflict Evidence

Result:

- [x] PASS
- [ ] PASS_WITH_NOTES
- [ ] NOT_RUN
- [ ] BLOCKED
- [ ] FAILED

Evidence notes:

Phase 3U.1 downloaded and extracted the published `v0.1.0-dev` ZIP asset, prepared a temporary `%TEMP%\LDS-WatcherManual` workspace, launched `LensDocsStudio.Windows.exe` from the extracted packaged app, and reached the real packaged app UI. The installed app source was unavailable, so the published ZIP was used.

The required observations through the real Windows folder picker were not completed. Invoking **Open folder** displayed an `Open` file dialog with Markdown/Mermaid file filters, and the app status reported `Native bridge did not respond.` Because the required folder picker could not be used to select the workspace folder, the manual evidence remains blocked rather than passed.

Blocker category: `MANUAL_WATCHER_CONFLICT_BLOCKED_FOLDER_PICKER`

Recommended next action: run **Help > Windows shell diagnostics** in the packaged Windows shell, record the diagnostic fields in the manual evidence document, and proceed to watcher/conflict evidence only if the native bridge ping passes and `workspace.openFolder` is advertised.

Automated browser tests, fake WebView2 coverage, or packaged launch alone are not substitutes for this item. Before `v0.1.0-dev.1` is published, a tester should complete `docs/release/lens-docs-studio-watcher-conflict-manual-evidence.md` against a ZIP or installed Windows shell build and update the result to `PASS`, `PASS_WITH_NOTES`, or `FAILED`.

Phase 3U.3 used the current `develop` package path with diagnostics built into the distributed package, then completed static and native smoke checks before recording blocker status.

- Package source used: `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip` built from `ab6e380685aae7d9aac45e3c6e516245a4767f75`.
- Validation commands that passed in this phase:
  - `npm run test:static`
  - `dotnet build src/windows/LensDocsStudio.Windows.sln`
  - `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
  - `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -NoSmoke`
  - `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe`

Phase 3U.3 blockers remaining:

- Stage A diagnostics is not completed in this phase because interactive packaged diagnostics could not be recorded here.
- Stage B scenarios remain not run.
- Manual evidence result therefore remains **MANUAL_WATCHER_CONFLICT_BLOCKED**.

Phase 3V root cause and status:

- Root cause found: the Open folder UI used the same browser fallback pattern as normal browser mode after a missing or failed `workspace.openFolder` capability probe. In a packaged WebView2 shell this could surface the browser fallback picker/file-input route and confuse manual evidence, even though the native bridge object was present.
- Fix made: Open folder now probes `workspace.openFolder` immediately before routing. If the bridge is present and the capability is missing or the ping fails, the app stops with a clear status message and sends operators to **Help > Windows shell diagnostics** rather than opening a browser fallback picker.
- Diagnostics now report **Open folder route**, browser fallback activity, safe last native request/error state, and include **Retry bridge check**.
- Manual packaged route verification after this fix: not yet completed in this document.
- Watcher/conflict evidence: still blocked until a tester completes the packaged Windows folder-picker pass.

Phase 3W evidence/artefact alignment:

- Source commit: `e2026d728c131cf2e203928487b9aeb09b744da7` on `develop`.
- Fresh local ZIP: `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip`, SHA256 `607C83FFC171C684C1F37599A189F44D5619BCFC56634212047D80D930153FF5`.
- Fresh local RC report: `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T102300Z.md`.
- Fresh local installer: `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe`, SHA256 `D143B628CD83940D50414F82DAD88A0BD98A1865ACED2E64D37996727792AAB3`.
- Validation passed: `npm run test:static`, `npm run test:browser`, `dotnet build src/windows/LensDocsStudio.Windows.sln`, `scripts/windows/Test-WindowsStaticAssets.ps1`, `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`, `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`, `scripts/windows/Build-WindowsPackage.ps1 -NoSmoke`, packaged native bridge smoke against the rebuilt executable, and `scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild`.
- Stage A packaged diagnostics: blocked. The freshly rebuilt packaged executable launched with window title `Lens Docs Studio`, but the session could not operate **Help > Windows shell diagnostics**, use **Retry bridge check**, and observe the real diagnostics values.
- Stage B watcher/conflict scenarios: blocked because Stage A was not verified.
- No GitHub release assets, releases, tags, or `main` merges were changed.

Phase 3X packaged diagnostics and watcher checkpoint:

- Evidence commit at start: `1f920052ce9fb4588d7cdb00a5618d8bc2206629` on `develop`.
- Artefact used: `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe`, from the Phase 3W package built from `e2026d728c131cf2e203928487b9aeb09b744da7`.
- Stage A packaged diagnostics: **PACKAGED_DIAGNOSTICS_PASS**. Observed in the real WebView2 UI after **Retry bridge check**: Windows WebView2 shell `Yes`, bridge ping `Pass`, `workspace.openFolder` `available`, Open folder route decision `native bridge`, Open folder will use native bridge `Yes`, browser fallback active `No`, browser fallback route `Not active`.
- Stage B watcher/conflict scenarios: **BLOCKED_STAGE_B_WORKSPACE_NOT_SELECTED**. A temporary workspace under `%TEMP%\LDS-Phase3X-WatcherManual` was prepared, but no scenario reached external-change execution because the workspace folder was not selected through a reliable packaged UI route in this environment. A debug-launched instance of the same packaged executable reported `Native bridge did not respond.` when **Open folder** was clicked, and no browser fallback picker was observed.
- Scenario verdicts: Clean external change `BLOCKED`; dirty conflict cancel `BLOCKED`; dirty conflict confirm `BLOCKED`.
- No GitHub release assets, releases, tags, or `main` merges were changed.

Phase 3Y packaged workspace-selection checkpoint:

- Evidence commit at start: `fdce6bfe82fdac24d64edab762c3481648d1ec5b` on `develop`.
- Runtime fix: `assets/scripts/native/native-bridge-client.js` now applies a `300000 ms` timeout only to `openFolder()` so the interactive Windows folder picker is not falsely failed by the default `2500 ms` bridge timeout.
- Regression coverage: `tests/browser/app-smoke.spec.mjs` includes a delayed fake WebView2 `openFolder` response and verifies the workspace loads without `Native bridge did not respond.`
- Fresh package used after the fix: `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe`, rebuilt locally from `develop` during Phase 3Y.
- Stage A packaged diagnostics: **PACKAGED_DIAGNOSTICS_PASS**. Observed in the rebuilt packaged WebView2 UI through a debug-launched DOM inspection after **Retry bridge check**: Windows WebView2 shell `Yes`, bridge ping `Pass`, `workspace.openFolder` `available`, Open folder route decision `native bridge`, Open folder will use native bridge `Yes`, browser fallback active `No`, browser fallback route `Not active`.
- Workspace-selection result: **BLOCKED_STAGE_B_WORKSPACE_NOT_SELECTED**. After **File > Open folder**, the app stayed on `Opening folder from Windows...` beyond the previous `2500 ms` timeout and no browser fallback appeared, but Windows UI Automation did not expose a selectable folder picker window to the agent session. The temporary workspace under `%TEMP%\LDS-Phase3Y-WatcherManual` was not selected through the real packaged picker.
- Scenario verdicts: Clean external change `BLOCKED`; dirty conflict cancel `BLOCKED`; dirty conflict confirm `BLOCKED`.
- No GitHub release assets, releases, tags, or `main` merges were changed.

Phase 3AA native picker and Stage B checkpoint:

- Evidence commit: `055e51dedacd28ea277a480b60bd0333feec6004` on `develop`.
- Runtime fix: `NativeWorkspaceService.OpenFolderAsync()` now marshals picker work to the UI dispatcher when needed, resolves and foregrounds the owner window at picker invocation time, returns structured native picker attach/show errors, and preserves cancellation as `{ cancelled: true }`.
- Stage A packaged diagnostics: **PACKAGED_DIAGNOSTICS_PASS**. Observed in the real packaged WebView2 UI after **Retry bridge check** with Windows WebView2 shell `Yes`, bridge ping `Pass`, `workspace.openFolder` `available`, Open folder route decision `native bridge`, Open folder will use native bridge `Yes`, browser fallback active `No`, and browser fallback route `Not active`.
- Native picker selection: **WORKSPACE_SELECTED_THROUGH_REAL_PACKAGED_PICKER**. The real Windows `Select Folder` dialog was observed, owned by the Lens Docs Studio window, and `C:\Temp\LensDocsStudio-StageB` was selected through the native picker.
- Stage B watcher/conflict scenarios: **PACKAGED_STAGE_B_PASS**. Clean external change, dirty conflict cancel, and dirty conflict confirm all passed in the real packaged app.
- Browser fallback was not re-enabled. No GitHub release assets, releases, tags, or `main` merges were changed.

Phase 3AB release-candidate artefact rebaseline:

- Source commit: `055e51dedacd28ea277a480b60bd0333feec6004` on `develop`.
- Fresh local ZIP: `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip`, SHA256 `DF29869E85AADC3C79525D65CC9DC224B7B23FCF67F9C1CF423D31923A328B03`.
- Fresh local RC report: `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.md`.
- Fresh local RC metadata: `artifacts/windows/release-candidates/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T162341Z.json`.
- Fresh local installer: `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe`, SHA256 `29A4A985DABB8D7991E27C6F0E611A42EC7B31CA5B122B361ACA2B16B9893EDE`.
- Installer SHA256 file: `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup.exe.sha256`.
- Installer report: `artifacts/installers/inno/LensDocsStudio.Windows-0.1.0-dev-Setup-report.md`.
- Validation passed: `npm run test:static`, `npm run test:browser`, `dotnet build src/windows/LensDocsStudio.Windows.sln`, `scripts/windows/Test-WindowsStaticAssets.ps1`, `scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`, `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`, `scripts/windows/Build-WindowsPackage.ps1 -NoSmoke`, packaged native smoke against the rebuilt executable, and `scripts/windows/Build-WindowsInnoInstaller.ps1 -NoPackageBuild`.
- Phase 3W artefacts from `e2026d728c131cf2e203928487b9aeb09b744da7` are superseded and stale because Phase 3AA changed Windows runtime code afterward.
- These are local release-candidate artefacts only. No GitHub release assets, releases, tags, or `main` merges were changed, and no production readiness or go-live claim is made.

Phase 3AD publication bundle freeze:

- Source commit: `c776fffdc8364c2c978f8106869a2d6bf50477ff` on `develop`.
- Frozen bundle: `artifacts/windows/publication-bundles/v0.1.0-dev.1/`.
- Frozen ZIP: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev.zip`, SHA256 `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A`.
- Frozen installer: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-Setup.exe`, SHA256 `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745`.
- Frozen metadata: `artifacts/windows/publication-bundles/v0.1.0-dev.1/LensDocsStudio.Windows-0.1.0-dev-rc-20260611T174119Z.md` and `.json`.
- `v0.1.0-dev.1` tag and release were missing during the read-only Phase 3AD check, so a later authorised publication would be create-new.
- No publication was performed. No GitHub release assets, releases, tags, or `main` merges were changed, and no production readiness or go-live claim is made.

## Artefact Decision

### ZIP

- [ ] Reuse existing v0.1.0-dev ZIP
- [x] Build new v0.1.0-dev.1 ZIP
- [ ] Not applicable

Reason:

Phase 3V changes packaged static runtime behaviour. A new ZIP is required for `v0.1.0-dev.1` if this fix is published.

### Installer

- [ ] Reuse existing v0.1.0-dev installer
- [x] Build new v0.1.0-dev.1 installer
- [ ] Not applicable

Reason:

Installer config is unchanged, but the installed app would otherwise contain stale packaged static assets. A new installer is required for `v0.1.0-dev.1` if this fix is published.

## Required Validation Before Publication

Minimum:

- [ ] `npm run test:static`
- [ ] `dotnet build src/windows/LensDocsStudio.Windows.sln`
- [ ] `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1`
- [ ] `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1`
- [ ] Complete the manual watcher/conflict evidence pass or explicitly record why publication proceeds without it.

If new ZIP artefact:

- [ ] `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1`
- [ ] ZIP SHA256
- [ ] ZIP download/extract/native smoke

If new installer artefact:

- [ ] `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1`
- [ ] installer SHA256
- [ ] install/native smoke/uninstall

If runtime/UI changes:

- [ ] `npm run test:browser`
- [ ] `pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`

## Release Notes Requirements

The published `v0.1.0-dev.1` release notes and closure evidence state:

- `v0.1.0-dev.1` is a prerelease.
- It is not stable/latest.
- ZIP is the portable/fallback package and installer is the easier Windows install path.
- Runtime prerequisites remain external.
- Installer remains unsigned.
- WebView2 user data may remain after uninstall.
- Watcher/conflict manual evidence status, including whether it passed, passed with notes, failed, was blocked, or was not run.
- Assets were rebuilt and published from the frozen Phase 3AD bundle.

## Publication Plan

Publication has been completed for `v0.1.0-dev.1`.

- Tag: `v0.1.0-dev.1`.
- Target commit: frozen source commit `c776fffdc8364c2c978f8106869a2d6bf50477ff`.
- Assets: fresh ZIP and installer assets from the frozen Phase 3AD bundle.
- Release state: prerelease/dev release, not production or stable.
- Verification: Phase 3AF post-publication verification passed.

## Verdict

- [x] DEV1_CERTIFICATION_READY
- [ ] DEV1_CERTIFICATION_READY_WITH_NOTES
- [ ] DEV1_CERTIFICATION_BLOCKED_WATCHER_EVIDENCE_BLOCKED
- [ ] DEV1_CERTIFICATION_BLOCKED_ARTEFACT_DECISION
- [ ] DEV1_CERTIFICATION_BLOCKED_VALIDATION

## Recommendation

- [x] Published `v0.1.0-dev.1` prerelease.
- [x] Built and certified new ZIP/installer for `v0.1.0-dev.1`.
- [ ] Pause pending more feedback.

Phase 3AA completed the real packaged diagnostics, native picker, and watcher/conflict evidence that earlier phases left blocked. Phase 3AB rebuilt fresh local ZIP and installer artefacts from the Phase 3AA commit and passed validation. Phase 3AD froze the authoritative local publication bundle from commit `c776fffdc8364c2c978f8106869a2d6bf50477ff`. Publication evidence and Phase 3AF verification confirm that `v0.1.0-dev.1` is published as a prerelease and passed post-publication checks. Next work is feedback collection, installer/download monitoring, and later planning for the next dev release or release disposition decision. Production readiness remains a separate approval gate.
