# Lens Docs Studio Installer Upgrade And Uninstall Evidence Plan

Planning date: 2026-06-12

## Purpose

Phase 3AO defines the installer upgrade and uninstall evidence required before any future installer-affecting `v0.1.0-dev.2` publication is considered.

This is a documentation and planning gate only. It does not rebuild packages or installers, publish release assets, create tags or releases, merge to `main`, approve `v0.1.0-dev.2` publication, or claim production readiness, go-live approval, or stable-channel certification.

## Baseline From v0.1.0-dev.1

`v0.1.0-dev.1` is the current published prerelease/dev release:

`https://github.com/DouglasNLima/local-docs-studio/releases/tag/v0.1.0-dev.1`

Baseline evidence:

- Phase 3AD froze the authorised publication bundle.
- Publication evidence records the ZIP and unsigned Inno Setup installer uploaded from that frozen bundle.
- Phase 3AF verified the release tag, prerelease state, exact asset set, downloaded hashes, downloaded ZIP smoke, and contained silent installer smoke.
- Phase 3AG closed the release evidence chain as a verified prerelease/dev release without changing runtime code, release assets, tags, releases, or `main`.

Published `v0.1.0-dev.1` artefact hashes:

| Artefact | SHA256 |
| --- | --- |
| `LensDocsStudio.Windows-0.1.0-dev.zip` | `3C9C343B8ABB06655B6A2DD55CACEEFF2542ED64BCF4FF943AFC7A5957C79A7A` |
| `LensDocsStudio.Windows-0.1.0-dev-Setup.exe` | `4C2A79C7DF446957DE6F6D2E41271D0C7563178BB47ECACC09E001752C930745` |

Phase 3AF contained silent installer smoke used:

```powershell
LensDocsStudio.Windows-0.1.0-dev-Setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$env:TEMP\LensDocsStudio-Phase3AF-Install"
```

That smoke passed install, native smoke, uninstall, uninstall-entry cleanup, Lens-owned file-association cleanup, and suppressed shortcut checks. The temporary install root remained only with WebView2 runtime data, matching the documented retention caveat, and was then manually removed.

## Future v0.1.0-dev.2 Installer Evidence Goals

Before any future installer-affecting `v0.1.0-dev.2` publication:

- Record the exact source commit for the candidate package and installer.
- Record candidate ZIP and installer paths, sizes, and SHA256 hashes.
- Verify clean install, silent install, uninstall, app launch, and upgrade from `v0.1.0-dev.1` using temporary install paths where practical.
- Verify Start Menu and optional Desktop shortcut creation and cleanup.
- Verify optional file association registration and uninstall cleanup without touching Windows `UserChoice` or unrelated defaults.
- Verify installer-owned application files are installed under the requested directory and removed on uninstall.
- Verify WebView2 data retention is observed and documented rather than silently deleted.
- Verify the installed executable passes native bridge smoke against the installed path.
- Confirm no published release assets are modified without explicit release-maintenance approval.

The goal is evidence for a prerelease/dev installer candidate, not production certification.

## Upgrade Scenarios To Verify

Upgrade evidence should use `v0.1.0-dev.1` as the previous published baseline and a separately built future `v0.1.0-dev.2` installer candidate.

Minimum scenarios:

| Scenario | Expected evidence |
| --- | --- |
| Upgrade from `v0.1.0-dev.1` default install path | Previous version installs, candidate installer completes, installed executable launches, native smoke passes, and application files reflect the candidate version. |
| Upgrade from `v0.1.0-dev.1` temp custom install path | Previous version installs to a temp directory, candidate installer targets the same directory, native smoke passes, and stale package files are not left unexpectedly. |
| Upgrade after app launch | Launch previous install once, close it, run candidate installer, launch upgraded executable, and note any retained WebView2 state. |
| Upgrade with no optional file associations | Candidate install leaves Lens-owned association keys absent unless the association task is explicitly selected. |
| Upgrade with optional file associations | Lens-owned ProgIds and extension values point at the upgraded executable path and still avoid Windows `UserChoice`. |

Rollback is not assumed. If the installer supports downgrading or same-version reinstall, document the observed behaviour separately rather than implying support.

## Uninstall Scenarios To Verify

Minimum uninstall checks:

- Uninstall after clean install with default tasks.
- Uninstall after clean install with `/NOICONS /TASKS=` and a temp install path.
- Uninstall after launching the app once.
- Uninstall after selecting the optional Desktop shortcut task.
- Uninstall after selecting the optional file association task.
- Uninstall after an upgrade from `v0.1.0-dev.1` to the candidate installer.

For each uninstall, record:

- Uninstaller exit code.
- Whether the HKCU uninstall entry remains.
- Whether Start Menu and Desktop shortcuts remain.
- Whether Lens-owned ProgIds remain.
- Whether Lens-owned extension values remain.
- Whether the install directory remains and, if so, what category of residue is present.
- Whether any user documents or external workspace files were touched.

## File Association Checks

Supported Lens-owned file association keys:

- `HKCU:\Software\Classes\LensDocsStudio.Markdown`
- `HKCU:\Software\Classes\LensDocsStudio.Mermaid`
- `HKCU:\Software\Classes\LensDocsStudio.Text`
- Lens-owned values under `.md`, `.markdown`, `.mmd`, `.mermaid`, and `.txt`

Checks:

- Associations remain unchecked by default.
- Selecting the task writes only HKCU per-user keys.
- Open commands quote the installed executable path and pass `"%1"`.
- The installer does not write Windows `UserChoice`.
- Uninstall removes Lens-owned ProgIds and Lens-owned extension values.
- Uninstall does not remove unrelated default values or unrelated `OpenWithProgids` entries.

## Shortcut Checks

Verify these paths by resolving the actual current-user shell folders on the test machine:

| Shortcut | Expected behaviour |
| --- | --- |
| Start Menu shortcut | Created by default and points to the installed executable. Removed by uninstall. |
| Desktop shortcut | Created only when the optional task is selected. Removed by uninstall. |
| Suppressed shortcut install | `/NOICONS /TASKS=` leaves Start Menu group and Desktop shortcut absent. |

Unexpected shortcut residue is a failure unless it is intentionally retained and documented before publication.

## Install Directory Checks

Default path:

```text
%LOCALAPPDATA%\Programs\Lens Docs Studio
```

Evidence runs should prefer temp install paths for contained tests:

```text
%TEMP%\LensDocsStudio-<phase>-CleanInstall
%TEMP%\LensDocsStudio-<phase>-SilentInstall
%TEMP%\LensDocsStudio-<phase>-Upgrade
```

Checks:

- `LensDocsStudio.Windows.exe` exists after install.
- `StaticApp/` exists after install.
- Installed `StaticApp/` passes packaged static asset validation when the scenario requires it.
- Native smoke passes against the installed executable.
- Uninstall removes installer-owned files and directories except documented retained WebView2 data.
- Tests do not use private document folders, real user workspaces, or production install paths unless an operator explicitly approves that manual evidence.

## WebView2 And Cache Residue Expectations

The current MVP decision is `WEBVIEW2_UNINSTALL_CLEANUP_DOCUMENTED_ONLY`.

Expected possible residue:

```text
<install-root>\LensDocsStudio.Windows.exe.WebView2
```

This folder can contain browser-local user state, local storage, first-run setup state, session data, and caches. Installer uninstall must not silently delete it unless a separate future decision adds an explicit and safe clean-up option.

Evidence should categorise remaining install-root contents as:

- **Expected retained WebView2 data**: acceptable if documented and manually removable after confirming no browser-local state is needed.
- **Unexpected installer-owned file residue**: fail unless explained by a known Inno behaviour and accepted before publication.
- **User-created data**: must not be touched by install, upgrade, or uninstall tests.

## Silent Install And Uninstall Checks

Silent install command shape:

```powershell
& $InstallerPath /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="$TempInstallPath" /LOG="$InstallLog"
```

Silent uninstall command shape:

```powershell
& $UninstallExe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG="$UninstallLog"
```

Checks:

- Exit code is `0`.
- Logs are captured under a temp evidence folder.
- No prompts are required.
- No elevation is requested for current-user install.
- `/DIR` is honoured for temp install paths.
- `/NOICONS /TASKS=` suppresses Start Menu group and Desktop shortcut creation.

## Rollback And Supersedence Considerations

- A future `v0.1.0-dev.2` prerelease, if approved and published, supersedes `v0.1.0-dev.1` only through explicit release notes and documentation.
- Supersedence does not require editing, deleting, replacing, re-uploading, rebuilding, or republishing `v0.1.0-dev.1` assets.
- Same-version reinstall and downgrade behaviour are not assumed. Test and document them only if the candidate installer is expected to support them.
- If upgrade or uninstall evidence fails, stop release planning and record the failure. Do not publish around failed installer evidence.
- Any release asset correction, replacement, or removal requires a separate authorised release-maintenance decision.

## PASS, FAIL, And BLOCKED

| Result | Meaning |
| --- | --- |
| PASS | The scenario completed, expected artefacts or registry state matched the plan, native smoke passed where required, and no protected user data or release assets were touched. |
| FAIL | The scenario ran but produced unexpected installer-owned residue, incorrect shortcut or registry state, failed native smoke, unexpected prompts/elevation, user data impact, or release asset mutation. |
| BLOCKED | The scenario could not be run because a prerequisite, candidate artefact, clean test environment, permission, required hash, or explicit approval was missing. |

Blocked evidence is not a pass. A future publication gate must either resolve the block or explicitly defer the affected installer claim.

## What Must Not Be Touched

Installer evidence must not touch:

- User documents, private Markdown workspaces, customer data, or private workspace folders.
- Browser-local storage or WebView2 user data except for explicitly named temporary install roots used by the test.
- Real default file associations outside Lens-owned HKCU keys.
- Windows `UserChoice`.
- Machine-wide registry keys.
- `v0.1.0-dev.1` release assets.
- Existing GitHub tags or releases.
- `main`.

Use disposable temp fixtures and temp install roots. Do not point tests at real work documents.

## Future Test Matrix

| Test | Inputs | PASS criteria | Notes |
| --- | --- | --- | --- |
| Clean install to temp path | Candidate installer path and SHA256; temp install root | Installer exits `0`, executable and `StaticApp/` exist, native smoke passes, expected shortcuts match selected tasks. | Prefer no file associations for baseline. |
| Silent install to temp path | Candidate installer, `/VERYSILENT`, `/NOICONS`, `/TASKS=`, `/DIR` | Installer exits `0`, no prompts, no shortcuts, executable exists, native smoke passes. | Capture install log. |
| Uninstall after clean install | Uninstaller from clean install | Uninstall exits `0`, uninstall entry removed, installer-owned files removed, only documented WebView2 data may remain. | Record directory residue. |
| Uninstall after app launch | Clean install, launch app once, close app | Uninstall exits `0`, WebView2 residue is categorised, shortcuts and Lens registry keys clean up. | Confirms post-launch WebView2 behaviour. |
| Upgrade from `v0.1.0-dev.1` to candidate `v0.1.0-dev.2` | Published baseline installer plus candidate installer | Upgrade completes, candidate executable launches, native smoke passes, shortcuts and file associations point at candidate path. | Requires explicit baseline and candidate hashes. |
| Reinstall same version if supported | Candidate installer installed twice | Behaviour is documented as supported, blocked, or failed; no stale duplicate shortcuts or registry entries. | Do not assume support. |
| Verify no unexpected shortcuts remain | Each uninstall scenario | Start Menu and Desktop shortcuts are absent unless intentionally retained and documented. | Includes `/NOICONS` scenario. |
| Verify Lens file-association keys after uninstall | Association-selected install and uninstall | Lens-owned ProgIds and extension values are absent, unless intentionally retained and documented before publication. | `UserChoice` and unrelated defaults remain untouched. |
| Verify installed executable passes native smoke | Clean install, silent install, upgrade install | `Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath <installed-exe>` passes. | Use temp fixtures only. |
| Verify no release assets are modified without approval | Before and after evidence run | Published asset lists and hashes are unchanged unless a separate release-maintenance approval exists. | Read-only release inspection only. |

## Evidence Documentation Template

Future execution should record:

- Branch, source commit, and tracked git status before and after evidence.
- Candidate package path, installer path, file sizes, and SHA256 hashes.
- Baseline `v0.1.0-dev.1` asset hashes used for upgrade.
- Exact command lines for install, uninstall, static validation, and native smoke.
- Temp install roots and temp evidence log paths.
- PASS, FAIL, or BLOCKED verdict for each matrix row.
- Shortcut, registry, uninstall entry, install directory, and WebView2 residue observations.
- Confirmation that user documents and private workspace data were not touched.
- Confirmation that no release assets, tags, releases, or `main` merges changed.
- Confirmation that production readiness and go-live approval were not claimed.

## Future Execution Prompt

Use this prompt only after a future `v0.1.0-dev.2` package and installer candidate exists:

```text
You are working in C:\Code\MarkdownReader on branch develop.

Objective:
Run installer upgrade and uninstall evidence for a future v0.1.0-dev.2 installer candidate. This is an evidence run only, not release publication.

Required inputs:
- Explicit source commit for the v0.1.0-dev.2 candidate:
  <commit-sha>
- Explicit candidate ZIP/package path and SHA256:
  <package-path>
  <package-sha256>
- Explicit candidate installer path and SHA256:
  <installer-path>
  <installer-sha256>
- Explicit v0.1.0-dev.1 baseline installer path or downloaded release asset path and SHA256:
  <baseline-installer-path>
  <baseline-installer-sha256>

Constraints:
- Work directly on develop.
- Do not publish a release.
- Do not create, edit, delete, replace, re-upload, rebuild, or republish GitHub release assets.
- Do not create or move tags.
- Do not merge to main.
- Do not claim production readiness, go-live approval, stable-channel certification, or stable/latest positioning.
- Use temporary install paths under %TEMP% for clean install, silent install, and upgrade scenarios.
- Do not overwrite, read, delete, or modify user documents, private workspaces, customer data, browser-local storage outside the named temp install roots, or private workspace data.
- Do not write machine-wide registry keys or Windows UserChoice.
- Keep WebView2 user data cleanup documented-only unless a separate approved clean-up decision exists.

Tasks:
1. Confirm branch, source commit, baseline history, and tracked/ignored repository state.
2. Verify all supplied package and installer hashes before executing them.
3. Record the v0.1.0-dev.1 baseline asset hashes used for upgrade.
4. Run clean install to a temp path and verify executable, StaticApp, shortcuts, and native smoke.
5. Run silent install to a temp path with /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS /TASKS= /CURRENTUSER /DIR="<temp-path>" and capture logs.
6. Run uninstall after clean install and silent install; capture exit codes, logs, shortcut cleanup, uninstall-entry cleanup, Lens-owned registry cleanup, install-directory residue, and WebView2 residue.
7. Launch the app once from an installed temp path, close it, uninstall, and categorise any WebView2 residue.
8. Run upgrade from v0.1.0-dev.1 to the candidate installer using a temp install path, then verify candidate executable launch, native smoke, shortcuts, file associations when selected, and install-directory state.
9. Test same-version reinstall only if supported or explicitly requested; otherwise mark it BLOCKED or out of scope with reason.
10. Inspect release state read-only to confirm no published assets were modified.
11. Document PASS, FAIL, or BLOCKED for every matrix row in docs/release/ or an approved evidence path.
12. If the evidence run changes only documentation, stage docs only, commit, and push to origin/develop.
13. If installer defects require a runtime, script, or installer fix, stop before committing and report the defect, evidence, and proposed fix scope.

Suggested docs-only commit message:
docs: record installer upgrade evidence

Final response must include:
- Branch
- Source commit tested
- Candidate package and installer hashes
- Baseline v0.1.0-dev.1 installer hash
- Evidence document path
- PASS, FAIL, and BLOCKED summary
- Native smoke result
- Shortcut, file association, install directory, WebView2 residue, and silent install/uninstall results
- Whether code changed or docs only
- Commit hash and push result if a docs-only evidence commit was made
- Skipped validation and reason
- Confirmation that no release publication occurred
- Confirmation that no release assets, tags, releases, or main merges were changed
- Confirmation that production readiness/go-live was not claimed
```

## Release Boundary

Phase 3AO is planning only. It does not build or publish installer artefacts.

`v0.1.0-dev.1` release assets remain untouched. Future `v0.1.0-dev.2` package and installer candidates require separate build, evidence, approval, and publication gates before any upload is considered.
