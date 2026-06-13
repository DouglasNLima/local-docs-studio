param(
    [string]$OutputDirectory,
    [string]$Phase = 'Phase 3BP manual packaged sanity helper',
    [string]$ReleaseVersionUnderTest = 'v0.1.0-dev.5 candidate, if separately authorised',
    [string]$PackagePath = 'PACKAGE_FILE_NAME_ONLY',
    [string]$ExecutablePath = 'EXECUTABLE_FILE_NAME_ONLY',
    [string]$SourceCommit,
    [string]$PackageSha256 = 'SHA256_IF_ALREADY_APPROVED',
    [string]$InstallerSha256 = 'SHA256_IF_APPLICABLE_AND_ALREADY_APPROVED',
    [switch]$LaunchExecutable
)

$ErrorActionPreference = 'Stop'

function Write-ManualSanityLine {
    param([string]$Message)
    Write-Host "[windows-manual-sanity] $Message"
}

function ConvertTo-NeutralFileName {
    param([string]$Value)

    $safe = $Value -replace '[^\w.-]', '-'
    if ([string]::IsNullOrWhiteSpace($safe)) {
        return 'manual-packaged-sanity'
    }

    return $safe
}

function Get-GitValue {
    param([string[]]$Arguments)

    $result = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return 'unknown'
    }

    return (($result | Out-String).Trim())
}

if ([string]::IsNullOrWhiteSpace($SourceCommit)) {
    $SourceCommit = Get-GitValue -Arguments @('rev-parse', 'HEAD')
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\manual-packaged-sanity'
}

$timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
$localTimestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$workspaceName = "LensDocsStudio-ManualSanity-$localTimestamp"
$workspaceRoot = Join-Path ([System.IO.Path]::GetTempPath()) $workspaceName
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$checklistPath = Join-Path $resolvedOutputDirectory ("LensDocsStudio-Windows-manual-packaged-sanity-{0}.md" -f $timestamp)

New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workspaceRoot 'docs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workspaceRoot 'diagrams') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workspaceRoot 'notes') -Force | Out-Null

Set-Content -LiteralPath (Join-Path $workspaceRoot 'README.md') -Encoding utf8 -Value @'
# Manual Sanity Fixture

Synthetic content for packaged verification.

Use this workspace only for the manual packaged sanity checklist.
'@

Set-Content -LiteralPath (Join-Path $workspaceRoot 'docs\overview.md') -Encoding utf8 -Value @'
# Overview Fixture

This generated file is safe sample Markdown for manual packaged verification.
'@

Set-Content -LiteralPath (Join-Path $workspaceRoot 'diagrams\sample.mmd') -Encoding utf8 -Value @'
flowchart TD
  Start[Generated fixture] --> Check[Manual packaged check]
'@

Set-Content -LiteralPath (Join-Path $workspaceRoot 'notes\conflict.md') -Encoding utf8 -Value @'
# Conflict Fixture

Initial generated text for clean external change and dirty conflict checks.
'@

$scenarioRows = @(
    '| Onboarding renders | Packaged app launches; first-run setup renders; generic local documentation wording is visible; Open folder is primary; Open file remains available; setup can be skipped and reopened. |  |  |  |  | |',
    '| Diagnostics opens | Help > Windows shell diagnostics opens without exposing unsafe private values. |  |  |  |  | |',
    '| Retry bridge check passes | Retry bridge check completes; bridge ping, WebView2 shell, capability labels, native route, and browser fallback state are readable. |  |  |  |  | |',
    '| Create support bundle preview-first flow | Create support bundle requires explicit action; copy/export are disabled before preview and enabled only after preview. |  |  |  |  | |',
    '| Copy summary after preview | Copy summary works only after preview and contains bounded operational observations. |  |  |  |  | |',
    '| Export local JSON after preview | Export local JSON works only after preview and uses a neutral filename. |  |  |  |  | |',
    '| Open folder native picker visible | Open folder uses the packaged native route and a visible Windows Select Folder picker appears. |  |  |  |  | |',
    '| Native picker select temporary workspace | Selecting the generated temporary workspace loads safe relative workspace entries. |  |  |  |  | |',
    '| Native picker cancel | A second Open folder attempt can be cancelled and returns a clean cancelled state. |  |  |  |  | |',
    '| Browser fallback inactive in packaged WebView2 | Packaged WebView2 stays on the native route; browser directory picker fallback is inactive. |  |  |  |  | |',
    '| Clean external file change / changed-on-disk copy | Externally edit the active generated fixture while app edits are clean; changed-on-disk copy appears; refresh loads disk content only after explicit action. |  |  |  |  | |',
    '| Dirty conflict keep app edits | Externally edit the active generated fixture, then make unsaved app edits; cancelling refresh preserves app edits and leaves clear conflict copy. |  |  |  |  | |',
    '| Dirty conflict use disk version | From dirty conflict state, confirming refresh loads the disk version only after confirmation and clears the conflict marker appropriately. |  |  |  |  | |',
    '| Installer install/uninstall checks, where applicable | For an authorised installer candidate only: install per-user, launch from Start Menu or installed executable, record optional shortcut/file association choices, uninstall, and record cleanup. |  |  |  |  | |',
    '| Payload hygiene note for package/installer evidence | Evidence references package or installer filename and approved hashes only; do not include package payload listings, private paths, WebView2 data, logs, or screenshots by default. |  |  |  |  | |',
    '| Production readiness/go-live not claimed | Evidence states this manual checklist does not approve production readiness, go-live, stable/latest positioning, or a main merge. |  |  |  |  | |'
)

$checklist = @"
# Lens Docs Studio Windows Manual Packaged Sanity Checklist

Generated: $timestamp

This local template supports real human observation of packaged Lens Docs Studio behaviour. It does not run package builds, installer builds, release commands, tag commands, GitHub Release actions, hidden WebView2 setup, native picker decisions, or result classification.

## Safe Generated Workspace

Use the generated temporary workspace shown by the helper when selecting a folder in the visible Windows picker. Record only `TEMP_WORKSPACE_USED` or the redacted basename `$workspaceName` in evidence; do not record full private paths.

Generated fixture files:

- `README.md`
- `docs/overview.md`
- `diagrams/sample.mmd`
- `notes/conflict.md`

The generated fixture content is synthetic and safe. Do not use private, customer, or project documents.

## Evidence Header

| Field | Value |
| --- | --- |
| Phase | `$Phase` |
| Operator | Initials or role only; avoid full personal names |
| Date UTC | `$timestamp` |
| Release/version under test | `$ReleaseVersionUnderTest` |
| Package path | `$PackagePath` |
| Executable path | `$ExecutablePath` |
| Source commit | `$SourceCommit` |
| Package SHA256 | `$PackageSha256` |
| Installer SHA256 | `$InstallerSha256` |
| Windows version | High-level version/build only |
| Runtime prerequisites | .NET Desktop Runtime: present/missing/unknown; Windows App SDK Runtime: present/missing/unknown; WebView2 Runtime: present/missing/unknown |
| Launch mode | ZIP executable / installed shortcut / installed executable |
| Temporary workspace | `TEMP_WORKSPACE_USED` or `$workspaceName` only |
| Overall classification | PACKAGED_MANUAL_SANITY_PASS / PACKAGED_MANUAL_SANITY_FAIL / PACKAGED_MANUAL_SANITY_BLOCKED, or installer equivalent when installer checks are in scope |

## Privacy And Safety Guardrails

- Use only the generated temporary workspace.
- Do not use private, customer, project, or personal documents.
- Do not paste document contents into evidence beyond confirming generated fixture filenames.
- Do not include screenshots by default.
- Do not include tokens, secrets, API keys, passwords, certificates, cookies, connection strings, full private paths, usernames, emails, machine names, customer names, raw stack traces, browser storage, WebView2 user data, package payload listings, or PII.
- Record only safe operational observations.
- Mark unclear, unreachable, environment-limited, or partially observed scenarios as `BLOCKED`, not `PASS`.
- Treat support bundle JSON as local evidence only until a separate review confirms it is safe to share.
- Do not change release assets, tags, releases, or `main`.

## Manual Scenario Matrix

Use exactly one of `PASS`, `FAIL`, or `BLOCKED` for each scenario. `PASS` requires direct human observation. Automated smoke, prior evidence, expectation, or this helper output cannot turn a scenario into `PASS`.

| Scenario | Human-observed checks | PASS | FAIL | BLOCKED | Failure category | Bounded notes |
| --- | --- | --- | --- | --- | --- | --- |
$($scenarioRows -join [Environment]::NewLine)

Suggested failure categories: `APP_DID_NOT_LAUNCH`, `ONBOARDING_COPY_UNEXPECTED`, `DIAGNOSTICS_UNAVAILABLE`, `BRIDGE_RETRY_UNEXPECTED`, `SUPPORT_BUNDLE_PREVIEW_GATING_FAILED`, `SUPPORT_BUNDLE_PRIVACY_LEAK`, `NATIVE_PICKER_NOT_VISIBLE`, `NATIVE_PICKER_SELECTION_FAILED`, `NATIVE_PICKER_CANCEL_FAILED`, `BROWSER_FALLBACK_ACTIVE_IN_PACKAGED_WEBVIEW2`, `WATCHER_STATUS_MISSING`, `DIRTY_CONFLICT_UNEXPECTED_RESULT`, `INSTALLER_STATE_UNEXPECTED`, `ENVIRONMENT_PREREQUISITE_MISSING`, `OPERATOR_CONTROL_BLOCKED`.

## Human Steps

1. Confirm the candidate package or installer is separately authorised for manual packaged sanity.
2. Record package and executable filenames only, plus approved hashes if already available.
3. Launch the packaged executable or installed shortcut manually.
4. Complete the onboarding and Diagnostics observations in the scenario matrix.
5. Use Open folder in the app and select the generated temporary workspace through the visible Windows picker.
6. Repeat Open folder and cancel the visible picker.
7. Review support bundle preview/copy/export behaviour without uploading or sharing the JSON.
8. Use an external editor to modify only generated fixture files for clean changed-on-disk and dirty conflict checks.
9. Complete installer checks only when the authorised evidence scope includes an installer candidate.
10. Classify every scenario as `PASS`, `FAIL`, or `BLOCKED`.

## Cleanup

- Close Lens Docs Studio.
- Delete the generated temporary workspace unless short-term local investigation is needed.
- Remove extracted ZIP folders created only for the check, or keep them under ignored local `artifacts/` with a bounded reason.
- If installer checks were in scope, uninstall the app and remove installer-owned shortcuts. Record the WebView2 user data caveat rather than deleting browser-local state silently.
- Do not commit generated checklists, support bundles, downloaded packages, installer outputs, logs, screenshots, WebView2 data, or temporary workspaces unless a future documentation summary is explicitly approved.

## Evidence Boundary

This checklist does not claim production readiness, go-live approval, stable-channel certification, stable/latest positioning, public rollout, release publication, release asset validity, or a `main` merge. Existing prerelease assets are not changed by creating this local checklist.
"@

$checklist | Set-Content -LiteralPath $checklistPath -Encoding utf8

Write-ManualSanityLine "Checklist: $checklistPath"
Write-ManualSanityLine "Temporary workspace: $workspaceRoot"
Write-ManualSanityLine 'Record the workspace as TEMP_WORKSPACE_USED or by redacted basename only.'
Write-ManualSanityLine 'The helper did not automate native picker selection, mark results, change release assets, or publish anything.'

if ($LaunchExecutable) {
    if ([string]::IsNullOrWhiteSpace($ExecutablePath) -or $ExecutablePath -eq 'EXECUTABLE_FILE_NAME_ONLY') {
        throw 'LaunchExecutable requires -ExecutablePath with a local executable path.'
    }

    $resolvedExecutable = Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop
    Write-ManualSanityLine "Launching executable on explicit request: $($resolvedExecutable.Path)"
    Start-Process -FilePath $resolvedExecutable.Path | Out-Null
}

[pscustomobject]@{
    checklistPath = $checklistPath
    temporaryWorkspacePath = $workspaceRoot
    temporaryWorkspaceEvidenceValue = 'TEMP_WORKSPACE_USED'
    temporaryWorkspaceBasename = $workspaceName
    launchRequested = [bool]$LaunchExecutable
} | ConvertTo-Json -Depth 3
