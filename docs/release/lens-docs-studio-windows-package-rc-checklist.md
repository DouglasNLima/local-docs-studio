# Lens Docs Studio Windows Package Release Candidate Checklist

Use this checklist for release candidate validation of the Windows folder/ZIP package. The RC gate is for the current unpackaged framework-dependent Windows distributable only. It keeps Lens Docs Studio generic, local-first, offline-capable, and static-runtime based.

## Automated RC Gate

Run from the repository root on `develop`:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1
```

The script builds the Windows package, validates the packaged `StaticApp/` and file association script dry-runs, runs the native bridge smoke harness against the packaged executable, calculates the ZIP SHA256 checksum, and writes a Markdown report plus matching JSON metadata under `artifacts/windows/release-candidates/`.

Phase 3F keeps this folder/ZIP package as the short-term release candidate artefact. The installer decision gate in `docs/architecture/windows-installer-decision-gate.md` defers MSIX, classic installer work, signing, prerequisite bootstrapping, auto-update, and winget metadata to later phases.

Phase 3G adds a dry-run-first GitHub Release preparation flow for the certified ZIP:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun
```

That script prepares the release artefact folder, checksum, generated release notes, RC report copy, and draft prerelease GitHub CLI command. It does not publish, upload files, create local tags, sign the package, add an installer, add auto-update, or merge to `main`. See `docs/release/github-release-publication-flow.md`.

Use explicit release-candidate version values when preparing a named RC:

```powershell
pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsPackageReleaseCandidate.ps1 -Version 0.1.0-rc.1 -TimeoutSeconds 90
```

## Manual Packaged App Smoke

- [ ] Extract the generated ZIP into a clean local folder.
- [ ] Launch `LensDocsStudio.Windows.exe` from the extracted folder.
- [ ] Confirm the app opens without a local HTTP server, GitHub Pages, or a CDN.
- [ ] With clean WebView2 app storage, confirm the first-run setup wizard appears.
- [ ] Use **Skip setup**, relaunch, and confirm the wizard does not auto-open again.
- [ ] Use **Help > Open setup wizard** and confirm it reopens after completion.
- [ ] In the setup wizard, confirm runtime readiness reports the Windows host, native bridge, packaged origin, WebView2 runtime availability when reported, and safe capability labels.
- [ ] In the setup wizard, use **Open a workspace folder** and confirm it opens the normal native folder picker.
- [ ] In the setup wizard, confirm file association guidance lists `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt` and does not write registry keys.
- [ ] In the setup wizard, open the Markdown + Mermaid sample or feature guide and confirm it loads.
- [ ] Use **Help > Check Windows bridge** and confirm `diagnostics.ping`, `file.startupOpen`, `file.open`, `file.save`, `file.saveAs`, `workspace.openFolder`, `workspace.saveFile`, `workspace.createFile`, `workspace.watch`, and `workspace.refreshFile` are reported.
- [ ] Launch `LensDocsStudio.Windows.exe` with a supported `.md` file path argument and confirm that file loads.
- [ ] Edit the startup file and use **File > Save changes** to confirm it saves through the native handle.
- [ ] Use **Help > Open feature guide** and confirm the local guide opens in read-only mode.
- [ ] Use **File > Open file** with a `.md`, `.markdown`, `.mmd`, `.mermaid`, or `.txt` file.
- [ ] Edit the file and use **File > Save changes**.
- [ ] Use **File > Save as** and confirm the new file is written.
- [ ] Use **File > Open folder** with a folder containing Markdown and Mermaid files.
- [ ] Confirm the workspace browser shows safe relative paths and selecting files updates editor and preview.
- [ ] Edit a workspace file and use **File > Save changes**.
- [ ] Create a new Markdown file in the workspace and confirm it appears in the list.
- [ ] Modify a clean workspace file externally and confirm an external-change marker/status appears.
- [ ] Use **Refresh active file** and confirm the external content loads.
- [ ] Modify the same file externally again, make local edits before refresh, and confirm the dirty external-conflict marker appears.
- [ ] Cancel refresh and confirm local edits remain.
- [ ] Confirm refresh and verify the external content loads only after explicit confirmation.
- [ ] Delete a workspace file externally and confirm local content is not silently erased.
- [ ] Rename a workspace file externally and confirm the safe renamed indication.
- [ ] Disconnect network access or otherwise block internet access, relaunch from the extracted folder, and confirm the app still opens, renders Markdown, renders Mermaid, opens the feature guide, and can run local save workflows.
- [ ] Run `pwsh -NoLogo -NoProfile -File scripts/windows/Register-WindowsFileAssociations.ps1 -DryRun` and confirm planned HKCU operations are printed without writing.
- [ ] Run `pwsh -NoLogo -NoProfile -File scripts/windows/Unregister-WindowsFileAssociations.ps1 -DryRun` and confirm planned removals are printed without writing.
- [ ] If safe on the tester machine, register the package executable, open a supported file through Windows **Open with** or double-click, confirm it loads, then unregister.

## Certification Claims

- [ ] The package was built by `scripts/windows/Build-WindowsPackage.ps1`.
- [ ] The packaged `StaticApp/` was validated by `scripts/windows/Test-WindowsStaticAssets.ps1`.
- [ ] The file association scripts parsed and passed `-DryRun` validation.
- [ ] The packaged executable passed `scripts/windows/Run-WindowsNativeBridgeSmoke.ps1`, including startup file argument load/save.
- [ ] The first-run setup wizard was manually checked or explicitly deferred with a reason.
- [ ] The generated report records package version, branch, commit SHA, build timestamp, output folder, ZIP path, ZIP size, SHA256 checksum, runtime prerequisites, validation commands, command output, and results.
- [ ] If preparing a GitHub Release draft, `scripts/windows/Prepare-WindowsGitHubRelease.ps1 -DryRun` generated the release notes, checksum, RC report copy, ZIP copy, and `gh release create` command under `artifacts/releases/<tag>/`.
- [ ] The package is a folder/ZIP distributable for manual RC testing.

## Certification Non-Claims

- [ ] No MSIX package, classic installer, installer wizard, signing, certificates, Store publishing, auto-update, winget manifest, machine-wide file associations, WebView2 bootstrapper, WebView2 Fixed Version Runtime, telemetry, cloud sync, or merge to `main` is included. The first-run setup wizard is in-app onboarding only.
- [ ] The RC gate does not prove prerequisites are installed on tester machines.
- [ ] The RC gate does not perform antivirus reputation checks, accessibility audits, performance benchmarks, telemetry reviews, network interception, or cross-machine install validation.
- [ ] The RC gate does not certify new editor/runtime behaviour beyond packaged asset validation, packaged native bridge smoke, and this manual checklist.
- [ ] The file association MVP does not set Windows `UserChoice`, does not guarantee default-app selection without user confirmation, and does not implement single-instance forwarding.

## Runtime Prerequisites

- Windows 10 version 2004 / build 19041 or newer.
- .NET desktop runtime for the Windows target framework.
- Windows App SDK runtime matching the `Microsoft.WindowsAppSDK` package reference.
- Evergreen Microsoft Edge WebView2 Runtime.

The Phase 3C package remains framework-dependent. Later phases may add installer prerequisite checks or bootstrapping.
