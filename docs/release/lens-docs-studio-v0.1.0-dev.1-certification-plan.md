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
- [x] PACKAGE_AFFECTING
- [ ] INSTALLER_AFFECTING
- [ ] PACKAGE_AND_INSTALLER_AFFECTING

The original planning scope was release documentation, guidance, and manual evidence tracking only. Phase 3U.2 adds a temporary browser UI diagnostic aid under Help so operators can inspect Windows shell, native bridge, capability, and Open folder routing state before retrying manual watcher evidence. No Windows shell native source, package script, installer config, service worker, manifest, release asset, tag, or release changes are included in the current scope.

## Changes Since v0.1.0-dev

- WebView2 uninstall cleanup decision: `WEBVIEW2_UNINSTALL_CLEANUP_DOCUMENTED_ONLY`. The WebView2 user data folder may contain browser-local user/session state, so silent uninstall cleanup is not approved for this dev cycle.
- Watcher/conflict evidence: checklist added in `docs/release/lens-docs-studio-watcher-conflict-manual-evidence.md`; the Phase 3U.1 packaged ZIP retry launched the app, but the manual evidence is `BLOCKED` at the folder-picker gate because **Open folder** produced an `Open` file dialog and the app reported `Native bridge did not respond.`
- Phase 3U.2 diagnostic aid: **Help > Windows shell diagnostics** reports browser/PWA versus WebView2 shell mode, bridge ping result, safe capability labels, Open folder routing, browser fallback route, and the next operator step without showing local absolute paths.
- ZIP vs installer wording: clarified across release guidance so ZIP is the portable/fallback package and the Inno installer is the easier Windows install path.
- Any runtime/package/installer config changes: browser UI diagnostics changed; package, installer, Windows native source, release assets, tags, and releases are unchanged.

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

## Artefact Decision

### ZIP

- [x] Reuse existing v0.1.0-dev ZIP
- [ ] Build new v0.1.0-dev.1 ZIP
- [ ] Not applicable

Reason:

No Windows app runtime, package asset, static asset, or package script change requires a new ZIP. Publishing a new prerelease with unchanged binary artefacts may confuse testers, so a new ZIP should be built only if a future phase adds package-affecting changes or explicitly chooses a release-notes-only prerelease with clear wording.

### Installer

- [x] Reuse existing v0.1.0-dev installer
- [ ] Build new v0.1.0-dev.1 installer
- [ ] Not applicable

Reason:

No installer config, file association, setup wizard, uninstall, shortcut, runtime prerequisite, or Inno script change requires a new installer. A new installer should be built only if a future phase changes installer behaviour or chooses to cut fresh assets for release clarity.

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
- [ ] Build and certify new ZIP/installer for v0.1.0-dev.1.
- [x] Defer v0.1.0-dev.1 and move to v0.1.0-rc.1 planning.
- [ ] Pause pending more feedback.

Because the current scope is docs-only and the watcher/conflict manual evidence remains blocked at the folder-picker gate, do not publish `v0.1.0-dev.1` yet. Prefer adding a temporary operator debug overlay before the next manual retry, then either keep these docs on `develop` for `v0.1.0-rc.1` planning or publish a docs-only prerelease only if there is a clear communication reason.
