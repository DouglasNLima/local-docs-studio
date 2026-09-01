# Lens Docs Studio

Local Markdown, Mermaid, and documentation studio.

Lens Docs Studio is a local-first documentation workspace with a static browser/PWA runtime and an emerging Windows desktop shell. The primary product direction is a fully offline-capable Windows app built with WinUI 3 and WebView2, while the same core runtime must continue to work from GitHub Pages and local static validation. Open local `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files, preview diagrams, review documents, save local edits when the host supports it, and export clean documentation packages without a backend.

## Use The App

1. Open `index.html` through GitHub Pages or a local static server.
2. Choose **Open file** for one document, **Open folder** for a workspace folder of Markdown and Mermaid files, or **Import document** to convert DOCX, HTML, or PDF into Markdown.
3. Edit in the Markdown toolbar or type directly in the editor.
4. Use **Render** to refresh the preview, then export or copy the rendered output.

Files stay in the browser unless you save, copy, or export them.
Use **Help > Open feature guide** to open the local Markdown feature guide inside the app in read-only mode.

## Windows Shell

The first Windows desktop shell lives under `src/windows/LensDocsStudio.Windows/`. It uses WinUI 3 and WebView2 to host the same static app from packaged local files, keeping the browser and GitHub Pages runtime unchanged.

The desktop direction is offline-first: package the static assets with the app, load them through WebView2 virtual host mapping, and avoid requiring a production local HTTP server. GitHub Pages remains a secondary web demo, fallback, and validation target for the shared runtime.

Supported runtime modes:

- Browser / GitHub Pages: static web publication from `main` after the shared browser checks pass.
- Browser / local static server: development and validation through a simple static server such as `python -m http.server 4173`.
- Windows shell / development run: `dotnet run` builds the shell and copies the shared static runtime into `StaticApp/`.
- Windows shell / packaged local assets: WebView2 loads the copied app through `https://lens-docs-studio.local/`, backed by packaged files rather than a server.
- Windows shell / offline mode: the packaged shell can load the app, CSS, JavaScript modules, local vendor libraries, the help guide, templates, snippets, and existing export workflows without internet access.

For Windows production usage, a local HTTP server is not required. Runtime dependencies are pinned under `assets/vendor/`, and the Windows project copies `index.html`, `md-mmd-renderer-v5.html`, `manifest.webmanifest`, `icon.svg`, `service-worker.js`, `assets/`, and `docs/` into the desktop output. Offline hardening does not add an installer, auto-update, account sync, Git integration, native PDF export, or any network fallback.

Run it locally from the repository root:

```powershell
dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj
```

Build it with:

```powershell
dotnet build src/windows/LensDocsStudio.Windows.sln
```

Create the Phase 3B folder/ZIP package with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1
```

The package script publishes a framework-dependent `win-x64` folder to `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/`, creates `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev.zip`, validates `StaticApp/`, and runs the native bridge smoke harness against the packaged executable unless `-NoSmoke` is passed. Run the package by launching `LensDocsStudio.Windows.exe` from the package folder. The package is intentionally a folder/ZIP distributable; it does not add MSIX, signing, certificates, an installer wizard, auto-update, store metadata, or a WebView2 fixed runtime/bootstrapper.

Generate a local development smoke ZIP for browser-only manual validation with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/New-LocalDevSmokeZip.ps1
```

This writes `artifacts/windows/local-dev-smoke/LensDocsStudio-local-dev-<short-sha>-word-template-smoke.zip` and includes the static app shell, service worker, manifest, app scripts/styles, local vendor files, the feature guide, and the Word export template service. It is deliberately marked as a local development smoke ZIP, not an official release, release asset, installer asset, frozen artefact, production-readiness claim, or publication bundle. The script refuses a dirty working tree by default; use `-AllowDirty` only for temporary local validation, where the generated metadata records the dirty state.

Generate the Windows local development smoke ZIP for manually testing the Word template MVP inside the Windows shell with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/New-WindowsLocalDevSmokePackage.ps1
```

This writes `artifacts/windows/local-dev-package-smoke/LensDocsStudio.Windows-local-dev-<short-sha>-word-template-smoke.zip`. Unlike the browser-only local dev smoke ZIP, it publishes the Windows executable shell and includes the packaged `StaticApp/` with `index.html`, `service-worker.js`, `manifest.webmanifest`, app scripts/styles, local vendor files, `assets/scripts/exports/export-service.js`, and `assets/scripts/exports/word-template-service.js`. The ZIP includes `lens-docs-studio-windows-local-dev-smoke.json` metadata marking it as a local Windows development smoke ZIP, not an official release, release asset, installer, installer asset, frozen artefact, production-readiness claim, or publication bundle. The script refuses a dirty working tree by default; use `-AllowDirty` only for temporary local validation, where the generated metadata records the dirty state.

Certify a Windows package release candidate with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
```

The RC script builds the folder/ZIP package, validates the packaged `StaticApp/`, runs the native bridge smoke harness against the packaged executable, calculates the ZIP SHA256 checksum, and writes a Markdown report plus JSON metadata under `artifacts/windows/release-candidates/`. Use `docs/release/lens-docs-studio-windows-package-rc-checklist.md` for manual packaged-app smoke. The certification is an audit gate for the folder/ZIP package only; it does not add MSIX, signing, certificates, Store publishing, auto-update, installer prerequisite bootstrapping, telemetry, cloud sync, or a merge to `main`.

The Phase 3F installer decision gate keeps the folder/ZIP package as the short-term release candidate artefact and defers MSIX or classic installer implementation to a separate spike after signing, prerequisite, file association, and update policy decisions. See `docs/architecture/windows-installer-decision-gate.md`.

The Phase 3L installer spike compares MSIX, WiX Toolset, Inno Setup, continuing ZIP plus GitHub Releases, and a later winget path. It recommends `Phase 3M - Classic Installer MVP with Inno Setup` while keeping the certified ZIP as the internal RC fallback. See `docs/architecture/windows-installer-spike.md`.

Build the Phase 3M internal unsigned Inno Setup installer MVP with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1
```

The installer build wraps the existing certified Windows package output, validates packaged `StaticApp/`, writes a single setup executable plus `.sha256` and report under `artifacts/installers/inno/`, installs per-user under `%LOCALAPPDATA%\Programs\Lens Docs Studio`, creates a Start Menu shortcut, offers an optional Desktop shortcut, and keeps file associations as an unchecked per-user task. It does not install runtimes, publish or upload a release, create or move tags, sign binaries, add auto-update, merge to `main`, or replace the ZIP fallback. Uninstall removes installer-owned files, shortcuts, and Lens registry entries; WebView2 user data may remain under `%LOCALAPPDATA%\Programs\Lens Docs Studio\LensDocsStudio.Windows.exe.WebView2` so browser-local state is not silently deleted. See `docs/release/lens-docs-studio-inno-installer-mvp.md`.

The Phase 3N installer RC asset plan recommended adding the certified unsigned Inno Setup installer, checksum, and report to the existing `v0.1.0-dev` draft prerelease in a later explicit upload phase, with the release left unpublished and the ZIP kept as the primary fallback. See `docs/release/lens-docs-studio-installer-rc-asset-plan.md`.

Phase 3Q records that `v0.1.0-dev` is a public GitHub prerelease, not stable/latest. Later prerelease cycles published and verified `v0.1.0-dev.1`, `v0.1.0-dev.2`, `v0.1.0-dev.3`, and `v0.1.0-dev.4` as prerelease/dev releases. The current published prerelease/dev release for new validation is `v0.1.0-dev.4`; `v0.1.0-dev.3`, `v0.1.0-dev.2`, and `v0.1.0-dev.1` are superseded for new validation but preserved historically, and `v0.1.0-dev` is older/stale. Phase 3BL post-publication verification passed for `v0.1.0-dev.4`, including the expected six-asset set, downloaded ZIP SHA256 `06BFA3896E1CCA48F1DC87D2F49D2387A8BCB93078E9F070764F22B048DD860C`, downloaded installer SHA256 `E52DB8B71B34E269F1754E2CCB6AB1691F38E6172CD2C826299A5DCF4A6652FC`, downloaded ZIP smoke, payload hygiene, and installer smoke. Phase 3AW remains the referenced manual packaged sanity/remediation evidence because Phase 3BL skipped interactive manual packaged sanity. True upgrade from `v0.1.0-dev.3` remains blocked by the already-published baseline installer rollback recorded in Phase 3BH. No further release asset action is required unless a future release is authorised, and no older release assets were modified by the `v0.1.0-dev.4` publication, verification, or closure evidence. The ZIP remains available as the portable fallback for manual extraction, smoke validation, and environments avoiding installers; it does not create Start Menu or uninstall entries. The unsigned Inno Setup installer is the easier Windows install path with a Start Menu shortcut, optional Desktop shortcut, optional/default-safe file associations, uninstall support, and the WebView2 user data caveat. Runtime prerequisites remain separate, and structured prerelease feedback should use `docs/release/lens-docs-studio-prerelease-feedback-intake.md`. See `docs/release/lens-docs-studio-v010-dev4-release-closure.md` and `docs/release/lens-docs-studio-prerelease-guidance.md` for the current prerelease closure and guidance.

Prepare a dry-run GitHub Release artefact set with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun
```

The Phase 3G release preparation script builds and certifies the Windows ZIP package, copies the certified ZIP into `artifacts/releases/<tag>/`, writes a `.sha256` checksum, generates release notes from `docs/release/templates/github-release-notes.md`, and prints the exact `gh release create` command. Dry-run is the default: no GitHub release is created, no files are uploaded, no local tags are created, and no merge to `main` is performed. Use `-Publish` only after reviewing the generated release notes, checksum, and RC report. See `docs/release/github-release-publication-flow.md`.

Certify the dry-run output before any manual draft prerelease publication with:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsGitHubReleaseDryRun.ps1
```

The review gate reruns release preparation in dry-run mode, validates the ZIP, checksum, RC report, release notes, tag/target strategy, and generated GitHub CLI command, then writes an ignored review report under `artifacts/releases/<tag>/`. A ready result means the artefact set is suitable for an intentional manual draft prerelease step; it still does not publish, upload, create tags, sign the ZIP, or merge to `main`.

Review an existing GitHub draft prerelease with `docs/release/lens-docs-studio-draft-release-review.md`. The checklist covers live release metadata, notes, assets, ZIP download, SHA256 verification, extraction, packaged app launch, native bridge smoke, remaining manual RC tester checks, known limitations, and the draft-release verdict without publishing.

For the public `v0.1.0-dev` prerelease publication record and feedback intake, see `docs/release/lens-docs-studio-v0.1.0-dev-publication-record.md`, `docs/release/lens-docs-studio-v0.1.0-dev-feedback-intake.md`, and `docs/release/lens-docs-studio-v0.1.0-dev-known-issues.md`.

The shell requires the .NET SDK, Windows App SDK runtime, and WebView2 Runtime. It adds native single-file open, save, and save-as dialogues for UTF-8 Markdown, Mermaid, and text files up to 5 MB. It also adds a native workspace foundation for opening a selected folder, loading supported files recursively, creating Markdown files in that workspace, saving workspace files through host-owned opaque handles, detecting external changes in the selected native workspace, and revealing selected workspace locations in File Explorer. The Windows file association MVP adds command-line startup file handling and manual per-user HKCU registration scripts for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`. The Windows first-run setup wizard appears only in the Windows shell, can be skipped, can be reopened from **Help > Open setup wizard**, and stores completion in browser-local storage. It does not add recent native folders, installers, auto-update, disk delete/rename/move operations initiated from the app, or native export behaviour yet.

## Roadmap And Branches

- `develop` is the active implementation branch.
- `main` remains the stable publication branch.
- The Windows desktop app is the primary distribution direction.
- The static browser/PWA app remains the core runtime and must keep working from GitHub Pages and local static validation.
- Phase 2B adds native single-file open/save/save-as for `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files through an opaque WebView2 bridge handle.
- Phase 2C adds native open folder, recursive workspace discovery, workspace file save, and native Markdown file creation through opaque workspace and file handles.
- Phase 2D adds native workspace external-change detection, safe relative watcher events, explicit native refresh, and non-destructive web UI markers for changed, created, deleted, and renamed workspace files.
- Phase 2E refines the native workspace conflict UX with distinct changed, deleted, renamed, dirty, and dirty-external-conflict indicators plus explicit refresh/discard prompts.
- Phase 3B adds the first repeatable folder/ZIP Windows package flow.
- Phase 3C adds the release-candidate certification gate for the Windows folder/ZIP package.
- Phase 3D adds a controlled Windows file association MVP for manual per-user registration and startup file arguments.
- Phase 3E adds a compact in-app Windows first-run setup wizard for runtime readiness, optional workspace opening, file association guidance, and starter documents.
- Phase 3F records the Windows installer decision gate: ZIP and GitHub Releases first, then an MSIX/classic installer spike once signing and update strategy are clear.
- Phase 3G adds a dry-run-first GitHub Release ZIP publication flow for preparing release notes, checksum, RC report, and the `gh release create` command.
- Phase 3H adds a dry-run review gate for certifying the prepared GitHub Release artefact set before any intentional draft prerelease publication.
- Phase 3J adds the live GitHub draft release review checklist for keeping an uploaded prerelease draft ready for internal RC testing without publishing it.
- Phase 3L records the MSIX versus classic installer spike and recommends a classic installer MVP with Inno Setup.
- Phase 3M adds the internal unsigned Inno Setup installer MVP build path while keeping ZIP plus GitHub Releases as the fallback.
- Phase 3N records the installer RC asset plan for optionally adding the certified Inno Setup installer to the existing draft prerelease without publishing it.
- Phase 3Q records the public `v0.1.0-dev` prerelease publication and adds structured feedback intake plus known caveats.
- Phase 3R records prerelease feedback triage and recommends targeted `v0.1.0-dev.1` planning scope.
- Phase 3AG closes `v0.1.0-dev.1` as a published and post-publication-verified prerelease/dev release. No release assets require further action unless a future release is authorised.
- Phase 3AH records the post-`v0.1.0-dev.1` backlog and the original `v0.1.0-dev.2` planning gate.
- Phase 3AI records the candidate `v0.1.0-dev.2` scope and prerelease feedback intake process. It is planning-only and does not approve a new release.
- Phase 3AJ records `v0.1.0-dev.2` implementation readiness, proposed slices, validation expectations, and the recommended first implementation slice. It is not a release approval.
- Phase 3AL polishes first-run and empty-state copy so Open folder is the primary onboarding action, Open file remains available, Diagnostics is reachable, and local/offline expectations stay clear. It is not a release approval.
- Phase 3AM polishes watcher/conflict copy so changed-on-disk and unsaved-app-edit decisions are clearer while preserving the existing non-destructive refresh behaviour. It is not a release approval.
- Phase 3AN records an optional troubleshooting/support bundle design contract. It is design-only, does not implement generation, and does not approve release publication.
- Phase 3AO records installer upgrade/uninstall evidence planning before any future installer-affecting `v0.1.0-dev.2` publication. It is documentation-only, does not rebuild installers, and does not approve release publication.
- Phase 3AP records stale older prerelease guidance. Phase 3BM rebaselines that guidance so `v0.1.0-dev.4` is the current recommended prerelease/dev release for new validation; `v0.1.0-dev.3`, `v0.1.0-dev.2`, and `v0.1.0-dev.1` are superseded for new validation but preserved historically, and `v0.1.0-dev` is older/stale unless explicitly investigating historical behaviour.
- Phase 3AS records authorised `v0.1.0-dev.2` prerelease publication from the frozen Phase 3AR publication bundle. Manual packaged sanity remains blocked because real interactive packaged WebView2/onboarding/native picker/watcher observation was not executed.
- Phase 3AT verifies the published `v0.1.0-dev.2` prerelease: tag, prerelease state, expected asset set, downloaded ZIP SHA256, downloaded installer SHA256, downloaded ZIP smoke, and contained installer smoke passed. No production readiness/go-live approval, `main` merge, release asset change, or `v0.1.0-dev.1` asset change is claimed.
- Phase 3AV records a manual packaged sanity failure for the published `v0.1.0-dev.2` native Open folder path, Phase 3AW remediates bounded pending guidance on `develop`, Phase 3AX freezes a local `v0.1.0-dev.3` publication bundle, Phase 3AY audits that frozen bundle with no publication, Phase 3BA verifies the published `v0.1.0-dev.3` prerelease, and Phase 3BB closes it as the current verified prerelease/dev release at that time. Phase 3AW remains the referenced manual packaged sanity/remediation evidence, and Phase 3BA skipped interactive manual packaged sanity.
- Phase 3BC records the post-`v0.1.0-dev.3` backlog and candidate `v0.1.0-dev.4` scope as documentation/planning only. Phase 3BD adds the implementation readiness plan for the `v0.1.0-dev.4` cycle, Phase 3BE implements explicit-user-action Diagnostics support bundle generation, Phase 3BG improves diagnostics copy/export, Phase 3BI adds payload hygiene guards, Phase 3BL verifies the published prerelease, and Phase 3BM closes it as the current verified prerelease/dev release. Phase 3BF defines the manual packaged sanity helper/checklist approach as planning only. `v0.1.0-dev.4` does not claim production readiness or go-live approval.
- Phase 3BN records the post-`v0.1.0-dev.4` backlog and candidate `v0.1.0-dev.5` scope as documentation/planning only. Phase 3BO converts that candidate scope into an implementation readiness plan and recommends manual packaged sanity helper implementation as the first slice only if explicitly approved. Phase 3BP implements that first helper/checklist slice as Windows script, test, and documentation only, without runtime app changes, package publication, release assets, tags, releases, `main` merge, production readiness, or go-live approval. Phase 3BQ implements the browser-local Word export template MVP, Phase 3BR adds a browser-only local development smoke ZIP generator for manually testing that MVP from the current `develop` tree, and Phase 3BS adds a Windows local dev smoke ZIP generator for manually testing the same MVP inside the Windows shell without creating or changing releases, tags, release assets, installer assets, publication bundles, frozen artefacts, or production-readiness claims. `v0.1.0-dev.4` remains the current verified prerelease/dev release for new validation; `v0.1.0-dev.5` is not approved, has no committed release date, and does not claim production readiness or go-live approval.
- Phase 3S documents the WebView2 uninstall cleanup decision, adds the manual watcher/conflict evidence checklist, and clarifies ZIP versus installer wording for the next dev prerelease.
- Future Windows work includes a fuller installer path, single-instance forwarding, and a release flow from `develop` to `main`.

See `docs/architecture/windows-offline-distribution-roadmap.md` for the current Windows offline distribution roadmap.
See `docs/architecture/windows-installer-decision-gate.md` for the installer strategy decision gate.
See `docs/architecture/windows-installer-spike.md` for the Phase 3L installer spike and Phase 3M recommendation.
See `docs/release/lens-docs-studio-installer-rc-asset-plan.md` for the Phase 3N installer draft-release asset plan.
See `docs/release/lens-docs-studio-v0.1.0-dev-publication-record.md` for the Phase 3Q public prerelease publication record.
See `docs/release/lens-docs-studio-v0.1.0-dev.1-planning.md` for the Phase 3R prerelease feedback triage and `v0.1.0-dev.1` planning record.
See `docs/release/lens-docs-studio-v010-dev1-release-closure.md` for the Phase 3AG release closure and roadmap rebaseline.
See `docs/roadmap/lens-docs-studio-post-v010-dev1-backlog.md` for the Phase 3AH post-prerelease backlog and `v0.1.0-dev.2` planning gate.
See `docs/roadmap/lens-docs-studio-v010-dev2-candidate-scope.md` for the Phase 3AI candidate `v0.1.0-dev.2` scope, `docs/roadmap/lens-docs-studio-v010-dev2-implementation-readiness.md` for the Phase 3AJ implementation readiness plan, `docs/architecture/lens-docs-studio-support-bundle-design.md` for the Phase 3AN support bundle contract, `docs/release/lens-docs-studio-installer-upgrade-uninstall-evidence-plan.md` for the Phase 3AO installer evidence plan, `docs/release/lens-docs-studio-prerelease-guidance.md` for the current prerelease guidance, `docs/release/lens-docs-studio-v010-dev3-release-closure.md` for the Phase 3BB closure and roadmap rebaseline, `docs/roadmap/lens-docs-studio-post-v010-dev3-backlog.md` for the Phase 3BC post-dev.3 backlog, `docs/roadmap/lens-docs-studio-v010-dev4-candidate-scope.md` for the historical candidate dev.4 scope, `docs/roadmap/lens-docs-studio-v010-dev4-implementation-readiness.md` for the Phase 3BD implementation readiness plan, `docs/release/phase-3be-support-bundle-implementation.md`, `docs/release/phase-3bg-diagnostics-export-copy-improvements.md`, `docs/release/phase-3bh-installer-upgrade-evidence.md`, and `docs/release/phase-3bi-installer-payload-hygiene.md` for dev.4 implementation evidence, `docs/release/lens-docs-studio-v010-dev4-publication-record.md`, `docs/release/lens-docs-studio-v010-dev4-post-publication-verification.md`, and `docs/release/lens-docs-studio-v010-dev4-release-closure.md` for dev.4 publication, verification, and closure evidence, `docs/roadmap/lens-docs-studio-post-v010-dev4-backlog.md` for the Phase 3BN post-dev.4 backlog, `docs/roadmap/lens-docs-studio-v010-dev5-candidate-scope.md` for candidate dev.5 planning, `docs/roadmap/lens-docs-studio-v010-dev5-implementation-readiness.md` for the Phase 3BO implementation readiness plan, `docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md` for the Phase 3BF manual packaged sanity helper design, `docs/release/phase-3bp-manual-packaged-sanity-helper.md` for the implemented helper slice, and `docs/release/lens-docs-studio-prerelease-feedback-intake.md` for the prerelease feedback intake process.
See `docs/release/lens-docs-studio-watcher-conflict-manual-evidence.md` for the packaged watcher/conflict evidence chain that cleared before `v0.1.0-dev.1` publication.
See `docs/release/windows-first-run-setup-mvp.md` for the Windows setup wizard MVP notes.

### Windows Native Bridge

The Windows shell includes a narrow native bridge. Use **Help > Check Windows bridge** to send a versioned ping from the web app to the WebView2 host. A successful response reports `LensDocsStudio.Windows` and the `diagnostics.ping`, `file.startupOpen`, `file.open`, `file.save`, `file.saveAs`, `workspace.openFolder`, `workspace.saveFile`, `workspace.createFile`, `workspace.watch`, `workspace.refreshFile`, `filesystem.resolvePath`, and `shell.revealInExplorer` capabilities.

Native file and workspace operations support `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` files. Single files and workspace files are limited to 5 MB per file. Native workspace discovery loads up to 500 supported files and recurses up to 12 directory levels. Oversized, unreadable, invalid UTF-8, or limit-skipped files are reported with safe relative paths and user-facing reasons. The host keeps full paths in memory behind opaque `nativeWorkspaceId` and `nativeHandleId` values; the web app requests an absolute path only for the explicit **Copy path** action. **Copy relative path** stays relative to the opened workspace.

The sidebar context menu and toolbar reveal action share the native `filesystem.resolvePath` and `shell.revealInExplorer` routes. File targets are validated against their host-owned handle or workspace root before the host opens `explorer.exe` with the file selected; directory targets open the directory itself. Missing, invalid, or inaccessible locations return bounded status messages. Manual **Move** actions change only the in-memory workspace list order and **Remove from workspace** never deletes the source file.

Startup file arguments use the same native single-file model. When Windows launches `LensDocsStudio.Windows.exe "C:\path\README.md"`, the host validates that the path exists, is a file, uses a supported extension, is no larger than 5 MB, and can be read as UTF-8. After WebView2 and the web bridge are ready, the host sends `lensDocs.native.startupFile` with the normal file payload and a host-owned `nativeHandleId`. Invalid startup files are reported through a safe status message.

Native workspace watching starts after a Windows workspace folder is opened and is disposed when another workspace opens, the app closes, smoke completes, or the watcher fails. The host reports only supported-file changes under the selected root through `lensDocs.native.workspaceChanged`; payload paths are relative and revalidated before the web app marks records. Watcher events are debounced for 500 ms and coalesced so deletes win over changes, create-plus-change remains created, and host-recognised renames are reported as renames. Native save/create operations are suppressed for a short best-effort two-second window; if suppression is uncertain, the app prefers showing an external-change marker.

The web app never auto-merges or auto-reloads dirty content. Changed files are marked as changed outside the app, dirty files keep local edits, deleted active files keep their in-memory content, created files are added as marked workspace records when the host provides a handle, and safe renames update clean records while dirty records remain marked for explicit action. The workspace list uses compact state dots for clean, dirty, externally changed, externally deleted, externally renamed, and dirty external-conflict records. **Refresh active file** uses `lensDocs.native.refreshWorkspaceFile` for native workspace records and confirms before discarding dirty local edits. If a file was deleted outside Lens Docs Studio, refresh does not erase the editor; keep the in-memory content and use **Save as** or copy the text to recover it.

The bridge does not expose recent native folders, native PDF export, Git operations, shell commands, delete/rename/move operations initiated from the app, local paths in browser/PWA mode, environment data, usernames, secrets, machine names, or general-purpose host execution. Browser and GitHub Pages mode continue to use the existing browser picker, File System Access, and download fallbacks; native watcher and startup file capabilities are unavailable there.

**Help > Windows shell diagnostics** includes **Create support bundle**. The action is explicit and local: it first creates an inspectable JSON preview from allowlisted operational metadata, then enables **Copy summary** and **Export local JSON**. The bundle records safe state such as app version/build, runtime mode category, bridge ping and capability labels, Open folder route/attempt state, selected workspace presence, count buckets, and watcher event category/timestamp. It excludes document content, imported ZIP content, full private paths, secrets, tokens, connection strings, emails, raw stack traces, screenshots, browser storage, and WebView2 user data. The app does not upload the bundle or add telemetry.

### Windows First-Run Setup

The Windows shell shows a compact first-run setup wizard when `lensDocs.windowsSetup.completed` is not present in local browser storage. Browser, GitHub Pages, local-server, and PWA mode do not auto-open it. Users can skip setup, finish setup, or reopen it later through **Help > Open setup wizard**. Resetting local browser storage may show it again.

The wizard checks safe Windows readiness details: host identity, native bridge availability, packaged origin, WebView2 runtime availability when reported, and bridge capability labels. It can open the existing native workspace folder picker, show guidance for the per-user file association scripts, open the Markdown + Mermaid sample, open the local feature guide, or start an unsaved blank Markdown document.

File association setup remains guidance-only inside the wizard. It shows the supported extensions and the `Register-WindowsFileAssociations.ps1` command, but the app itself does not write registry keys, does not require administrator rights, and does not write Windows `UserChoice`. The completion state uses `lensDocs.windowsSetup.completed`, `lensDocs.windowsSetup.completedAt`, and `lensDocs.windowsSetup.version`.

The automated native bridge smoke suppresses first-run setup by using the smoke-only bridge capability. Normal Windows launches are unaffected.

### Windows File Associations

The file association MVP is for manual development and package testing. It registers per-user `HKCU:\Software\Classes` entries only, requires no administrator rights, and supports `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`.

Register the current packaged executable:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -ExecutablePath "artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/LensDocsStudio.Windows.exe"
```

Unregister Lens Docs Studio association keys and values:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Unregister-WindowsFileAssociations.ps1
```

Both scripts support `-DryRun`. Registration uses ProgIds `LensDocsStudio.Markdown`, `LensDocsStudio.Mermaid`, and `LensDocsStudio.Text` with an open command of `"<path-to-LensDocsStudio.Windows.exe>" "%1"`. The scripts do not write `UserChoice`, do not require machine-wide registry access, and do not remove unrelated user defaults. Windows may still require confirmation in **Open with** or Settings before it makes Lens Docs Studio the default app. This MVP is not MSIX, not signed, not installer-integrated, and does not implement single-instance forwarding.

Automated Windows native bridge smoke:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1
```

Use `-NoBuild` to reuse the latest built shell, and `-TimeoutSeconds 90` on slower machines. The script creates a temporary smoke root, writes Markdown and Mermaid fixtures, launches the WinUI/WebView2 shell with `--smoke-native-bridge --smoke-root "<temp-folder>" "<temp-folder>\startup-file.md"`, waits for `smoke-result.json`, validates startup-file and fixture content, and exits non-zero on failure. It intentionally does not automate Windows file or folder picker UI.

The smoke-only bridge capabilities `smoke.nativeFixtures` and `smoke.workspaceChange` plus the `lensDocs.native.smoke.*` messages are unavailable in normal launches. When enabled, fixture operations are limited to the explicit smoke root and cannot browse arbitrary paths, expose environment details, run host commands, or weaken production bridge validation. The smoke validates shell launch, WebView2 app load, bridge ping, startup file argument loading and save, single-file open/save/save-as, workspace open/save/create, workspace watcher event delivery with relative paths, protocol safety, structured completion, and clean shell shutdown. Conflict prompts are covered by fake WebView2 browser tests and the manual smoke path below, keeping the Windows smoke harness small and stable.

Windows packaged asset validation:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1
```

Use `-NoBuild` to inspect the latest `StaticApp/` output. The check verifies the shell, service worker cache list, vendor manifest, pinned vendor files, help guide, web manifest, icon, packaged runtime files, and Windows file association scripts, then scans runtime files for unexpected external script, style, CDN, or remote CSS dependencies.

Pass `-StaticAppRoot` to validate a specific package output, for example `artifacts/windows/LensDocsStudio.Windows-0.1.0-dev/StaticApp`.

Manual smoke path:

1. Run the Windows shell with `dotnet run --project src/windows/LensDocsStudio.Windows/LensDocsStudio.Windows.csproj`.
2. Use **Help > Check Windows bridge** and confirm `workspace.openFolder`, `workspace.saveFile`, `workspace.watch`, `workspace.refreshFile`, `filesystem.resolvePath`, and `shell.revealInExplorer` are reported.
3. Use **File > Open folder**.
4. Select a folder containing `.md`, `.markdown`, `.mmd`, `.mermaid`, or `.txt` files.
5. Confirm the workspace browser loads relative paths.
6. Select multiple files and confirm editor/preview update.
7. Edit a workspace file.
8. Use **File > Save changes**.
9. Modify the selected file externally while Lens Docs Studio has no local edits and confirm an external-change marker/status appears.
10. Use **Refresh active file** and confirm the content updates.
11. Modify the same file externally again.
12. Make local edits in Lens Docs Studio before refreshing.
13. Confirm local edits remain and the dirty/external-conflict marker appears.
14. Cancel **Refresh active file** and confirm local edits remain.
15. Confirm refresh and verify external content loads.
16. Delete a workspace file externally and confirm local content is not silently erased.
17. Rename a workspace file externally and confirm the safe renamed indication.
18. Use **File > Open file**, **File > Save changes**, and **File > Save as** to confirm single-file native operations still work.
19. Open the same app in a normal browser or PWA mode and confirm there are no native bridge errors.

## Key Features

- Markdown preview powered by Marked and DOMPurify.
- Mermaid diagrams inside fenced `mermaid` or `mmd` code blocks, plus Azure DevOps `::: mermaid` wiki blocks.
- Per-diagram Mermaid actions for copying source and exporting SVG or PNG, with a persisted diagram theme selector.
- Syntax-highlighted code blocks with one-click copy buttons.
- Rendered tables with one-click copy as Excel-friendly TSV.
- Semantic progress bars inserted from the editor toolbar with preset colours.
- Toolbar helpers for emoji, callouts, status badges, details blocks, image figures, keyboard shortcuts, and anchors.
- Formatted clipboard paste that converts HTML content into Markdown, with spreadsheet table support for HTML table or TSV clipboard data, plus Edit > Paste Special actions for table, text, code block, quote, HTML-to-Markdown, list, checklist, numbered list, and Mermaid block paste.
- Editor copy support for Markdown with rendered Mermaid diagrams embedded as pasteable image data for work item fields that do not render Mermaid source.
- DOCX, HTML, and text-only PDF import that converts documents into clean editable Markdown with supported embedded images as exportable session assets where available.
- Standalone `.mmd`, `.mermaid`, and `.txt` text-file editing.
- Local workspace browser with flat list or folder tree views, filtering, dirty-file markers, external-change markers, and add/new-file actions.
- Markdown editor with line numbers, formatting buttons, `Ctrl/Cmd+Z`, `Ctrl/Cmd+Y`, and common formatting shortcuts.
- Manual browser-local snapshots for comparing, restoring, and deleting explicit document versions.
- Lightweight Mermaid autocomplete and pre-render validation for `.mmd`, `.mermaid`, and fenced Mermaid blocks.
- Topbar Editor/Split/Preview layout buttons, light/dark theme toggle, preview maximisation, diagram zoom, and optional outline.
- Preview follow for editor selections, enabled by default and toggleable from the preview header.
- Drag-and-drop PNG, JPEG, GIF, and WebP image insertion as session assets that are included in HTML, Word, and Docs Site exports.
- Managed asset library for previewing session images, renaming Markdown references, and removing unused image assets.
- HTML export, Word `.docx` export, reusable Word export templates from imported `.docx` files, copy HTML, and copy rendered text.
- PDF export through the browser print dialogue with a clean print stylesheet.
- Markdown Bundle ZIP export for editable docs plus image assets, with an optional Azure DevOps Mermaid syntax checkbox.
- ZIP import for app bundles and generic Markdown/Mermaid documentation ZIPs.
- Folder-to-docs-site export as a GitHub Pages-ready ZIP.
- Optional Markdown front matter for Docs Site titles, descriptions, ordering, tags, draft badges, and navigation groups.
- Document UX in the preview with hierarchical outline, rendered-text search, non-blocking document review notes, Markdown governance linting, workspace link/asset audits, and a generated docs map.
- Mermaid Diagram Studio with templates, snippets, source copy, SVG export, and PNG export.
- Local Content Studios for README/project docs, release notes, and requirements/user stories.
- Browser-local templates, snippets, and export profiles with JSON import/export.
- Built-in read-only feature guide available from the Help menu.
- Optional PWA/offline shell for repeat use.

## Document UX

The preview pane includes document-focused review tools that do not change exported files:

- **Outline** builds a navigable table of contents from rendered headings and highlights the active section while you scroll.
- **Find** searches rendered document text, highlights matches, and skips interactive UI text such as code/table-copy and diagram-export buttons.
- **Review** shows word count, reading time, content counts, and soft notes such as missing H1 titles, heading-level jumps, Mermaid errors, external links, and dirty files.
- **Governance** scans the active document and loaded workspace for heading hierarchy jumps, broken internal links and wikilinks, missing image alt text, inconsistent Markdown tables, US spellings with British English suggestions, and TODO/FIXME release markers.
- **Workspace audit** adds notes for unresolved wikilinks, missing relative document links, unmanaged local images, orphaned managed assets, and pages without backlinks.
- **Docs map** in the View menu opens a read-only virtual Markdown document with a Mermaid graph plus link and unresolved-link tables.

These tools are app-only. HTML, Word, and Docs Site exports are regenerated from a clean render and do not include search marks or review UI.

## Editor UX

The editor stays buildless and native, using a `textarea` with a synchronised line-number gutter instead of a heavy IDE component.

- The topbar layout buttons switch between Editor, Split, and Preview modes. The choice is saved locally.
- **Save** writes back to an opened local file when the browser grants file access. **Save as** chooses a new destination, and **Refresh active file** reloads the linked local file after confirmation when local edits would be discarded.
- When a workspace folder is opened with browser file-system access, **New Markdown file** creates the file inside that folder. **Add file to workspace** keeps the current workspace loaded while adding more Markdown or Mermaid files.
- The file browser can switch between a flat list and a folder tree. Tree view keeps the loaded workspace hierarchy visible and includes expand, collapse, and reveal-active controls.
- **Follow selection** in the preview header highlights matching preview text for short editor selections and scrolls that match into view in Split mode. It is best-effort, can be turned off per browser, and the editor and preview still remember their own scroll positions across renders and file switches.
- **Mermaid autocomplete** appears with `Ctrl/Cmd+Space` in Mermaid files or Mermaid fenced blocks, and can also open from Mermaid-like line prefixes. Use arrow keys, `Enter` or `Tab` to insert a snippet, and `Escape` to close it.
- **Mermaid validation** runs before each diagram render. Fenced Mermaid blocks and Azure DevOps `::: mermaid` blocks are both accepted. Invalid diagrams show a localised error with copy/jump actions while the rest of the Markdown continues rendering.
- **Progress bar** in the editor toolbar opens a small dialogue for text, percentage, and preset colour, then inserts semantic HTML that renders in the preview and exports.
- **Rich insert helpers** add emoji, callouts, status badges, collapsible details, captioned image figures, keyboard shortcuts, and anchors from compact dialogues.
- **Paste Special** in the Edit menu can paste clipboard content as a Markdown table, plain text, fenced code block, quote, Markdown-converted HTML, list, checklist, numbered list, or Mermaid block. Regular paste automatically converts formatted HTML into Markdown and keeps spreadsheet tables as Markdown tables when the clipboard provides HTML table or tab-separated text; plain text stays plain, and **Paste as text** forces unformatted text.
- **Image drag-and-drop** on the editor inserts Markdown such as `![diagram](assets/images/diagram.png)`. The Markdown file save-back writes the link text only; the image binaries live in the browser session and are embedded or bundled when you export. User-supplied SVG images are blocked as assets for security; rendered Mermaid diagrams can still be exported as SVG.
- **Manage assets** in the View menu shows session images, usage counts, rename controls that update editable Markdown references, and removal for unused assets.
- **Create snapshot** and **Manage snapshots** in the File menu store explicit browser-local document versions separately from automatic draft recovery.

## Local Content Studios

Use **Create** to generate editable Markdown from local templates. The studios are deterministic and browser-only: no AI calls, API keys, backend, or uploaded source.

- **Project Docs** creates README files, architecture overviews, ADRs, runbooks, API notes, onboarding guides, and docs-pack indexes.
- **Release Notes** creates customer notes, technical changelogs, sprint summaries, and migration notes with semantic-version-friendly headings.
- **Requirements** creates PRDs, feature briefs, user story sets, acceptance criteria, Gherkin scenarios, and journey maps.

Each studio also includes snippets you can insert at the cursor. Generated files behave like normal Markdown files, so you can edit, save, export to HTML or Word, and include them in a Docs Site ZIP.

The Create menu also includes a browser-local library. Save the current document as a reusable template, save the current selection as a snippet, export/import the library as JSON, and reload those local entries without a backend.

## Mermaid Diagram Studio

Use **Studio** for a focused Mermaid authoring workflow. **Studio tools** provides templates for common diagram types and snippets for common Mermaid patterns.

Each rendered Mermaid diagram also includes compact actions:

- **Copy** for that diagram's source.
- **SVG** for that specific rendered diagram.
- **PNG** for a raster image of that specific diagram.

The Export menu includes document-level diagram actions:

- **Export SVG** for the current rendered Mermaid diagram.
- **Export PNG** for a raster image version.
- **Copy Mermaid** for the current standalone diagram source or first Mermaid block.
- **Copy Markdown with images** from the Edit menu or Editor context menu to copy Markdown while replacing complete Mermaid blocks with inline PNG image references. Its rich clipboard carrier keeps the source Markdown literal and exposes only the image elements needed for automatic attachment handling in Azure DevOps Markdown work item fields; see [`docs/release/copy-markdown-with-images-azure-devops-fidelity.md`](docs/release/copy-markdown-with-images-azure-devops-fidelity.md).

Use the **Diagram theme** selector in the preview header to choose Auto, Lens, Default, Neutral, Forest, or Dark. Auto follows the app light/dark theme; explicit choices keep rendered diagrams fixed. SVG, PNG, Word, PDF, copy-as-image, and Docs Site exports use the currently rendered Mermaid theme.

## Docs Site Builder

Open a folder of Markdown and Mermaid files, toggle **Docs site** to preview the folder as a navigable documentation site, then choose **Export Docs Site** from the Export menu.

Docs Site Builder 2.0 opens a custom export dialogue for the site title, short description, and initial theme. The exported site can be opened directly from its `index.html` file or hosted on GitHub Pages, and includes Light, Dark, and System theme switching, heading navigation for each page, and static full-text search across page titles, paths, headings, body text, and code blocks. If the folder contains `README.md` or `index.md`, that document becomes the home page; otherwise the export creates a compact generated home page with bundle stats and page cards.

Markdown files can start with front matter to control Docs Site metadata without rendering that block as document content:

```markdown
---
title: API Guide
description: Internal API docs
order: 20
tags: [api, auth]
draft: false
navGroup: Guides
---
```

The generated ZIP contains:

- `index.html` as the static site entry point.
- `assets/docs-site.css` with the exported site layout and themes.
- `assets/docs-site-data.js` so the site works when opened from disk without a local server.
- `assets/docs-site.js` with navigation, theme switching, search, code/table-copy, and Mermaid diagram actions.
- `assets/search-index.json` with rendered pages and the local search index.
- `site-manifest.json` with format version, home page, page, heading, and diagram export stats.
- `README.md` with deployment notes.

Open `index.html` directly from the extracted folder, or upload the ZIP contents to GitHub Pages or any static web host, keeping the `assets/` folder beside `index.html`.

## Advanced Import And Export

- **Export PDF** prepares a clean print view and opens the browser print dialogue. Choose **Save as PDF** in the browser to create the file.
- **Word export templates** let you import a `.docx` as a browser-local template pack, then choose it from the Export menu before creating Word output. Use **Manage** beside the Word template selector to list imported templates, select one, rename it, or delete it. Export clones the package as a presentation base, replaces its document body, and preserves the selected content page master, styles, numbering, theme, font table, settings, header/footer variants, relationships, fields, shapes, and referenced media such as logos. Imported packs are stored in IndexedDB for this browser profile; the ignored `artifacts/word-templates/` path is reserved for local/debug copies and must not be committed with user or corporate assets.
- **Export Markdown Bundle** creates a ZIP with every loaded `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` file, current in-memory edits, image assets, and `lens-docs-studio-bundle.json` metadata. Enable **Azure DevOps Mermaid syntax** to write Mermaid blocks as `::: mermaid` containers and convert top-level `flowchart` declarations to `graph` for DevOps compatibility.
- **Export artefact review pack** appears after a valid artefact bundle import and explicitly includes a rebuilt safe `lens-artifact-bundle.json` alongside the normal Markdown Bundle manifest. Generic Markdown Bundle export never includes artefact metadata.
- **Built-in export profiles** are session-only presets for generic documentation, GitHub Pages docs sites, Azure DevOps Wiki Markdown, and artefact review work. Applying the Azure DevOps Wiki Markdown preset uses a session override and does not write the existing DevOps preference key. Saved local export profiles still use the existing local library storage key.
- **Import ZIP** accepts Markdown Bundles from this app and generic ZIPs that contain Markdown/Mermaid files and PNG, JPEG, GIF, or WebP images. Imported files are editable virtual documents in the browser; SVG image assets are skipped for security.
- **Import document** converts `.docx`, `.html`, `.htm`, and `.pdf` files into editable Markdown. Word means modern `.docx`; legacy `.doc` files need conversion outside the browser first. Embedded PNG, JPEG, GIF, and WebP images become managed session assets. PDF import is text-only and creates page sections without OCR, image extraction, or visual layout reconstruction.
- ZIP import does not convert rendered HTML back into Markdown. If a Docs Site ZIP only contains static HTML plus deployment notes, only editable Markdown/Mermaid files found in that ZIP are imported.

### Word Export Templates

Import a corporate or personal `.docx` from **Export > Import Word template**, confirm the display name, then choose it in the **Word template** selector before selecting **Export Word**. The template remains local to the current browser profile and is not uploaded, shared, or added to exported Markdown Bundles.

Use **Export > Manage** to see **No template / Default Word export** alongside every imported template. Each entry shows its selected state, original filename, and import date where available. You can select a template directly, rename its display name without changing its stable identity or stored DOCX package, or delete it after confirmation. Deleting the selected template clears the saved selection and returns Word export to the default styling; missing or stale selections are repaired the same way on reload.

The template pack manifest records the template id, display name, source filename, creation time, detected capabilities, detected style ids/names where practical, and a semantic mapping for document title, headings, body text, tables, lists, code, quotes, and captions. Markdown H1 becomes the document title; H2 becomes the first content-heading level, H3 the next, and so on. If a mapped heading is automatically numbered by Word, a compatible literal Markdown counter is omitted from the exported heading text without changing the source Markdown.

Export uses the original Open XML package as its base and replaces only the main body. It selects a normal content section and preserves its page size, margins, first/even/default header and footer references, relationships, media, decorative shapes, and Word fields. The Markdown title updates intended running-title locations while retaining their run formatting. Tables reuse a detected Word table style or a representative direct-formatting prototype; exceptionally tall Mermaid diagrams are split into page-width slices. Missing optional parts use deterministic fallbacks and malformed templates fail without producing a knowingly corrupt `.docx`.

Known limitations: this is not a visual Word template designer and does not edit templates or preserve arbitrary source body/front-matter content. Export selects one detected normal content page master rather than distributing Markdown across multiple source sections. Templates and logos are user/organisation-provided assets, and should only be imported or shared when you have permission to use them.

### Local Dev Smoke ZIP

The local dev smoke ZIP packages the browser app assets needed to serve Lens Docs Studio from a simple static server while testing the current working implementation. It differs from the Windows package and official release flows: it does not build the native shell, create an installer, prepare release notes, publish or upload assets, create tags, touch GitHub Releases, freeze a publication bundle, or imply production readiness.

Generate it from the repository root:

```powershell
cd C:\Code\MarkdownReader

git checkout develop
git pull origin develop

pwsh -NoLogo -NoProfile -File scripts/windows/New-LocalDevSmokeZip.ps1
```

The generated ZIP is written under ignored `artifacts/windows/local-dev-smoke/` with a name like `LensDocsStudio-local-dev-<short-sha>-word-template-smoke.zip`. It includes `index.html`, `service-worker.js`, `manifest.webmanifest`, `assets/styles/app.css`, app scripts, local vendor assets, `docs/tool-guide.md`, `assets/scripts/exports/export-service.js`, `assets/scripts/exports/word-template-service.js`, and `lens-docs-studio-local-dev-smoke.json` metadata. It excludes source control internals, `node_modules/`, test output, Playwright reports, generated ignored local output, imported Word template packs, corporate `.docx` templates, extracted logos/media, IndexedDB/browser storage, and machine-local configuration.

Expand and serve it locally:

```powershell
Expand-Archive `
  -Path artifacts\windows\local-dev-smoke\LensDocsStudio-local-dev-<short-sha>-word-template-smoke.zip `
  -DestinationPath artifacts\windows\local-dev-smoke\expanded `
  -Force

cd artifacts\windows\local-dev-smoke\expanded
python -m http.server 4173
```

Open `http://127.0.0.1:4173/`.

Manual Word template smoke:

1. Add Markdown containing a title, heading 1/2/3, body paragraphs, bullet list, numbered list, table, quote, code block, Mermaid diagram, and optional image.
2. Open **Export > Documents**.
3. Import a `.docx` template and confirm the display name.
4. Open **Manage** and verify the template, original filename, import date, and selected state.
5. Import a second template, select the first one, rename it, and confirm the main selector updates immediately.
6. Reload and verify the renamed selected template is still active.
7. Export Word, then open or inspect the exported `.docx`.
8. Confirm there is no Word repair prompt and supported styles, header, footer, and logo are present where expected.
9. Delete the selected template, confirm the destructive prompt, and verify the second template remains while **No template / Default Word export** becomes selected.
10. Reload and export again to confirm the deleted template stays gone and default Word export still works.

The HSI/TEKenable corporate document, or any other organisation-provided template, may be used locally for smoke testing only when the tester has permission. It must not be committed, added to generated repository artefacts, uploaded as a release asset, or included in the local dev smoke ZIP.

### Windows Local Dev Smoke ZIP

The Windows local dev smoke ZIP packages the Windows executable shell and updated static app assets for local manual validation. It differs from the browser-only local dev ZIP because it runs through WebView2 inside `LensDocsStudio.Windows.exe`; it differs from official release flows because it does not create a GitHub Release, upload assets, create tags, build an installer, freeze a publication bundle, or claim production readiness.

Generate it from a clean `develop` working tree:

```powershell
cd C:\Code\MarkdownReader

git checkout develop
git pull origin develop

pwsh -NoLogo -NoProfile -File scripts/windows/New-WindowsLocalDevSmokePackage.ps1
```

The generated ZIP is written under ignored `artifacts/windows/local-dev-package-smoke/` with a name like `LensDocsStudio.Windows-local-dev-<short-sha>-word-template-smoke.zip`. It includes `LensDocsStudio.Windows.exe`, required .NET and Windows package output files, the packaged `StaticApp/`, `StaticApp/index.html`, `StaticApp/service-worker.js`, `StaticApp/manifest.webmanifest`, app scripts/styles, local vendor assets, `StaticApp/assets/scripts/exports/export-service.js`, `StaticApp/assets/scripts/exports/word-template-service.js`, and `lens-docs-studio-windows-local-dev-smoke.json` metadata. It excludes source control internals, `node_modules/`, test output, Playwright reports, browser-only local dev smoke ZIPs, generated validation folders, imported Word template packs, corporate `.docx` templates, extracted logos/media, IndexedDB/browser profile data, secrets, and machine-local configuration.

Expand and run it locally:

```powershell
Expand-Archive `
  -Path artifacts\windows\local-dev-package-smoke\LensDocsStudio.Windows-local-dev-<short-sha>-word-template-smoke.zip `
  -DestinationPath artifacts\windows\local-dev-package-smoke\expanded `
  -Force

cd artifacts\windows\local-dev-package-smoke\expanded
.\LensDocsStudio.Windows.exe
```

Manual Windows Word template smoke:

1. Launch `LensDocsStudio.Windows.exe`.
2. Add Markdown containing a title, heading 1/2/3, body paragraphs, bullet list, numbered list, table, quote, code block, Mermaid diagram, and optional image.
3. Open **Export > Documents**.
4. Import a permitted `.docx` template and confirm the display name.
5. Open **Manage**, verify the original filename and selected state, then import a second template.
6. Rename the first template and confirm the selector updates immediately; reload and verify the rename and selection persist.
7. Export Word and inspect the exported `.docx`.
8. Confirm no Microsoft Word repair prompt and that supported styles, header/footer, and logo are present where expected.
9. Delete the selected template after confirmation and verify the second template remains while **No template / Default Word export** is selected.
10. Reload and export Word again to confirm the deleted template stays gone and default fallback still works.

The HSI/TEKenable corporate document, or any other organisation-provided template, may be used locally for smoke testing only when the tester has permission. It must not be committed, included in generated repository artefacts, uploaded as a release asset, or embedded in the package. Template packs remain browser-profile-local; export selects a detected normal content page master and this ZIP is not a substitute for future release certification.

## Optional Lens Artefact Bundles

ZIPs generated by Lens-family tools or compatible generators may include an optional `lens-artifact-bundle.json` manifest. When present, Lens Docs Studio imports the Markdown and Mermaid files as normal editable virtual documents, opens a safe declared entry document where available, and shows a compact reader panel from the ZIP metadata.

The reader panel is collapsible, searchable, filterable by kind and evidence label, and navigates only to imported records whose paths were validated. Filters are local to the panel, reset with the workspace, do not affect generic file search, and are not stored.

Docs Site export may use safe artefact metadata as display-only page title, order, navigation group, and evidence label hints when Markdown front matter does not supply those values. It also writes safe bundle display metadata to `site-manifest.json` as `artifactBundle`. Source Markdown and Mermaid files are not modified.

The metadata is treated as untrusted context. Evidence labels are displayed as supplied by the bundle; candidate findings remain candidate findings, and Lens Docs Studio does not validate, confirm, upgrade, downgrade, infer, or analyse findings. Invalid, unsupported, oversized, or unsafe manifest metadata never blocks safe Markdown/Mermaid import.

The manifest is ZIP-import only, session-only, and browser-local unless the user explicitly exports an artefact review pack. It does not add direct Lens integrations, external service calls, telemetry, accounts, cloud sync, or new persistence keys. Round-trip certification covers valid, rich, invalid, unsafe, front matter precedence, and generic ZIP fixtures under `tests/fixtures/artifact-bundles/`.

For the versioned contract see `docs/architecture/lens-artifact-bundle-contract.md`. Future producers should use `docs/integration/lens-artifact-bundle-producer-guide.md`; release candidates should use `docs/release/lens-docs-studio-artefact-bundle-manual-smoke.md`.

## Project Structure

The core runtime is static and buildless. GitHub Pages can serve it directly without npm, a backend, or a bundler, and the Windows shell hosts the same static assets through WebView2 virtual host mapping.

The Lens Docs Studio identity uses the `#FF883E` accent in a restrained way for primary actions, selected states, focus states, and brand moments while keeping the product generic for local Markdown, Mermaid, and documentation workflows.

- `index.html` is the public app shell.
- `md-mmd-renderer-v5.html` is a compatibility redirect for older links from the original app name.
- `assets/styles/app.css` contains the app UI styles.
- `docs/architecture/lens-docs-studio-ui-definitions.md` exports the reusable UI definitions and token CSS for carrying the Lens Docs Studio look and feel into another app.
- `docs/architecture/windows-offline-distribution-roadmap.md` captures the Windows offline distribution direction and branch strategy.
- `docs/architecture/word-export-templates.md` captures the local Word template-pack MVP, storage, mapping, and limitations.
- `docs/architecture/lens-docs-studio-support-bundle-design.md` captures the design-only contract for a possible future local troubleshooting/support bundle.
- `docs/testing/lens-docs-studio-manual-packaged-sanity-helper-plan.md` captures the manual packaged sanity helper/checklist approach; `scripts/windows/New-WindowsManualPackagedSanityChecklist.ps1` implements the local checklist generator and keeps real native picker selection as a human-observed step.
- `assets/scripts/main.js` boots the ESM app controller.
- `assets/scripts/app-controller.js` composes the app services and coordinates UI/event flow.
- `assets/scripts/dom.js` centralises DOM element lookup.
- `assets/scripts/state/config.js` owns app constants and initial state.
- `assets/scripts/document/document-ux-service.js` owns preview outline, search, active section tracking, and document review notes.
- `assets/scripts/editor/editor-service.js` owns editor history, undo/redo, line numbers, Mermaid autocomplete, and Markdown formatting actions.
- `assets/scripts/files/file-service.js` owns open/save, document and ZIP import, local folder records, and recent file handles.
- `assets/scripts/rendering/render-service.js` owns Markdown, Highlight.js, Mermaid rendering, diagram frames, and zoom.
- `assets/scripts/exports/export-service.js` owns HTML, Word, PDF print, Markdown Bundle, artefact review pack, Docs Site, SVG/PNG, clipboard, and export confidence flows.
- `assets/scripts/exports/export-profile-service.js` owns session-only built-in export profiles.
- `assets/scripts/ui/artifact-bundle-reader.js` owns the optional artefact reader panel.
- `assets/scripts/ui/ui-service.js` owns file-list UI, empty states, save/status controls, menus, theme, and resizers.
- `assets/scripts/utils/` contains shared browser, binary, file, formatting, HTML-to-Markdown, and ZIP helpers.
- `assets/vendor/` contains pinned browser runtime libraries served locally for GitHub Pages and offline use.
- `assets/scripts/registries/content.js` stores local examples, templates, and snippets.
- `docs/tool-guide.md` is the built-in read-only feature guide opened from the Help menu.
- `docs/integration/lens-artifact-bundle-producer-guide.md` documents how compatible tools can create safe optional artefact bundle ZIPs.
- `docs/release/lens-docs-studio-artefact-bundle-manual-smoke.md` captures the release candidate smoke checklist for the artefact bundle flow.
- `docs/release/lens-docs-studio-windows-package-rc-checklist.md` captures the Windows folder/ZIP package release candidate checklist.
- `docs/release/lens-docs-studio-draft-release-review.md` captures the live GitHub draft prerelease review checklist.
- `tests/fixtures/artifact-bundles/` contains source-controlled Markdown, Mermaid, JSON, and asset fixtures used to build deterministic ZIPs during browser tests.
- `src/windows/LensDocsStudio.Windows/` contains the optional WinUI 3 and WebView2 desktop shell that hosts the same static app from local packaged files.

Rendered HTML, Docs Site, and Word exports remain standalone outputs with their own embedded styles/scripts where needed.

## Development Checks

The app has no production build step. The optional npm tooling is only for local regression checks.

```sh
npm ci
npx playwright install chromium
npm test
```

- `npm run test:static` checks module syntax, relative imports, service worker cache assets, and the public shell.
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1` checks the Windows `StaticApp/` output for complete packaged offline assets and unexpected runtime external dependencies.
- `pwsh -NoLogo -NoProfile -File scripts/windows/New-LocalDevSmokeZip.ps1` creates an ignored browser-only local development smoke ZIP under `artifacts/windows/local-dev-smoke/` for manual Word template MVP validation; `scripts/windows/Test-LocalDevSmokeZip.ps1` validates its packaging contract and key include/exclude rules.
- `pwsh -NoLogo -NoProfile -File scripts/windows/New-WindowsLocalDevSmokePackage.ps1` creates an ignored local Windows development smoke ZIP under `artifacts/windows/local-dev-package-smoke/` for manual Word template MVP validation inside the Windows shell; `scripts/windows/Test-WindowsLocalDevSmokePackage.ps1` validates its packaging contract, metadata, and key include/exclude rules.
- `pwsh -NoLogo -NoProfile -File scripts/windows/New-WindowsManualPackagedSanityChecklist.ps1` creates safe generated fixtures and a local manual packaged sanity checklist under ignored `artifacts/windows/manual-packaged-sanity/`; native picker selection and results remain human-observed.
- `pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1` creates and certifies a Windows folder/ZIP release candidate, then writes auditable report metadata.
- `pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsInnoInstaller.ps1` builds the internal unsigned Inno Setup installer MVP from the certified Windows package output, or reports `INNO_INSTALLER_MVP_BLOCKED_INNO_SETUP_NOT_INSTALLED` when Inno Setup is missing.
- `pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun` prepares the GitHub Release ZIP artefact set, checksum, release notes, and draft prerelease `gh release create` command without publishing.
- `npm run test:browser` runs Chromium and Microsoft Edge smoke tests for app load, legacy redirect, rendering, Mermaid errors, editor layout/autocomplete, image assets, PDF print HTML, PDF text import, Markdown bundle import/export, artefact bundle round-trip certification, export packages, theme, maximisation, and mobile layout.
- Microsoft Edge must be installed locally for the `edge` Playwright project. The GitHub Actions workflow runs on `windows-latest`, where Edge is available.

## Browser Support

The app works best in Chromium-based browsers such as Chrome and Edge because they support the File System Access API for direct save-back to opened files and persistent recent file handles.

Firefox and Safari can still open files through fallback file pickers and export/download results, but direct save-back and recent file reopen may be unavailable.

## Known Limitations

- Browser security rules mean folder access and recent local handles require user permission.
- Word export converts rendered Mermaid diagrams to images where possible. Very large or unusual SVG diagrams may fall back to SVG packaging.
- Dragged image binaries are session assets. Save-back updates Markdown links, while HTML, Word, and Docs Site exports carry the actual image data. User SVG image files are not imported; use PNG, JPEG, GIF, or WebP for image assets.
- Converted DOCX/HTML/PDF files are virtual Markdown documents. Use Save changes or Export Markdown Bundle to persist the converted Markdown and any managed image assets.
- PDF import extracts text only. It does not perform OCR, import images, or preserve visual layout.
- PDF export depends on the browser print dialogue; the app prepares the print document but does not create raw PDF bytes itself.
- ZIP import supports ordinary stored/deflated ZIP entries. Password-protected or encrypted ZIP files are not supported.
- Runtime dependencies are pinned under `assets/vendor/`; no CDN fetch is required for normal app loading after publication.
- GitHub Pages hosting is static; there is no server-side file storage or account sync.

## Publish To GitHub Pages

GitHub Actions is the recommended publication path. Release-ready changes flow from `develop` to `main`; the workflow in `.github/workflows/pages.yml` runs the static checks and Playwright tests first, then deploys only the static app files to GitHub Pages from `main`.

Repository settings:

1. Enable GitHub Pages.
2. Set **Source** to **GitHub Actions**.
3. Keep Actions permissions enabled for Pages deployment.

Deployment behaviour:

- Pull requests run the full test suite but do not publish.
- Pushes to `main` publish only after `npm test` passes.
- The Pages artefact contains `index.html`, the legacy redirect, manifest, service worker, icon, `assets/`, and `docs/`.
- The public URL should open the app at `/`; older links to `/md-mmd-renderer-v5.html` redirect to `/index.html`.

For a manual branch-based fallback, commit `index.html`, `md-mmd-renderer-v5.html`, `assets/`, `docs/`, `manifest.webmanifest`, `service-worker.js`, and `icon.svg`, then configure Pages to serve that branch directly. The GitHub Actions workflow remains the safer default because it blocks deployment when exports or browser smoke tests fail.

For local testing, serve the folder with any static server:

```sh
python -m http.server 4173
```

Then open `http://127.0.0.1:4173/`.
