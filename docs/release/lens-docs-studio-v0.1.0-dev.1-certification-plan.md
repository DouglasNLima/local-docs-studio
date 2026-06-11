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

## Changes Since v0.1.0-dev

- WebView2 uninstall cleanup decision: `WEBVIEW2_UNINSTALL_CLEANUP_DOCUMENTED_ONLY`. The WebView2 user data folder may contain browser-local user/session state, so silent uninstall cleanup is not approved for this dev cycle.
- Watcher/conflict evidence: checklist added in `docs/release/lens-docs-studio-watcher-conflict-manual-evidence.md`; the Phase 3U.1 packaged ZIP retry launched the app, but the manual evidence is `BLOCKED` at the folder-picker gate because **Open folder** produced an `Open` file dialog and the app reported `Native bridge did not respond.`
- Phase 3U.2 diagnostic aid: **Help > Windows shell diagnostics** reports browser/PWA versus WebView2 shell mode, bridge ping result, safe capability labels, Open folder routing, browser fallback route, and the next operator step without showing local absolute paths.
- Phase 3V Open folder routing fix: **File > Open folder** now uses a fresh native capability probe and fails closed in WebView2 when `workspace.openFolder` is missing or the bridge does not respond, instead of silently falling through to the browser folder/file-input route.
- Phase 3W checkpoint: fresh local package, package RC, and Inno installer artefacts were rebuilt from current `develop`; automated static, browser, Windows build, package RC, static asset, and native smoke validation passed. Manual packaged diagnostics and watcher/conflict evidence remained blocked because this agent session could launch the packaged app but could not operate and observe the real WebView2 diagnostics UI.
- ZIP vs installer wording: clarified across release guidance so ZIP is the portable/fallback package and the Inno installer is the easier Windows install path.
- Any runtime/package/installer config changes: browser UI diagnostics and Open folder routing changed; package source contents are changed, installer config is unchanged, and release assets, tags, and releases are unchanged.

## Manual Watcher/Conflict Evidence

Result:

- [ ] PASS
- [ ] PASS_WITH_NOTES
- [ ] NOT_RUN
- [x] BLOCKED
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

Any future `v0.1.0-dev.1` release notes must state:

- `v0.1.0-dev.1` is a prerelease.
- It is not stable/latest.
- ZIP is the portable/fallback package and installer is the easier Windows install path.
- Runtime prerequisites remain external.
- Installer remains unsigned.
- WebView2 user data may remain after uninstall.
- Watcher/conflict manual evidence status, including whether it passed, passed with notes, failed, was blocked, or was not run.
- Whether assets are reused from `v0.1.0-dev` or rebuilt for `v0.1.0-dev.1`.

## Publication Plan

Do not execute in this phase.

Future phase:

- Tag: `v0.1.0-dev.1`, only if publication is approved later.
- Target commit: the future validated `develop` commit selected at publication time.
- Assets: reuse `v0.1.0-dev` assets for docs-only communication, or build fresh ZIP/installer assets only after package/installer-affecting changes or an explicit release clarity decision.
- Release notes: use the requirements above and keep prerelease, unsigned installer, runtime prerequisite, ZIP versus installer, WebView2 uninstall, and watcher/conflict evidence caveats visible.
- Verification: repeat release page verification after publication if a future phase creates the prerelease.

## Verdict

- [ ] DEV1_CERTIFICATION_READY
- [ ] DEV1_CERTIFICATION_READY_WITH_NOTES
- [x] DEV1_CERTIFICATION_BLOCKED_WATCHER_EVIDENCE_BLOCKED
- [ ] DEV1_CERTIFICATION_BLOCKED_ARTEFACT_DECISION
- [ ] DEV1_CERTIFICATION_BLOCKED_VALIDATION

## Recommendation

- [ ] Publish docs-only v0.1.0-dev.1 prerelease later.
- [x] Build and certify new ZIP/installer for v0.1.0-dev.1.
- [x] Defer v0.1.0-dev.1 and move to v0.1.0-rc.1 planning.
- [ ] Pause pending more feedback.

Because Phase 3V changes runtime routing and watcher/conflict manual evidence remains blocked until the packaged picker pass is repeated, do not publish `v0.1.0-dev.1` yet. Phase 3W proved fresh local ZIP and installer artefacts can be rebuilt and pass automated validation, but any future publication still needs fresh ZIP and installer artefacts from the selected publication commit plus real packaged diagnostics/watcher evidence or an explicit documented exception.
