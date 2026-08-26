param(
    [string]$Configuration = 'Release',
    [string]$Version,
    [string]$VersionSuffix = 'dev',
    [string]$RuntimeIdentifier = 'win-x64',
    [int]$TimeoutSeconds = 60,
    [switch]$KeepOutput,
    [string]$ReportPath
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$packageJsonPath = Join-Path $repoRoot 'package.json'
$projectPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows\LensDocsStudio.Windows.csproj'
$packageRoot = Join-Path $repoRoot 'artifacts\windows'
$reportRoot = Join-Path $packageRoot 'release-candidates'
. (Join-Path $PSScriptRoot 'WindowsPackagePayloadHygiene.ps1')

function Write-RcLine {
    param([string]$Message)
    Write-Host "[windows-package-rc] $Message"
}

function Assert-Rc {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function ConvertTo-SafePackageName {
    param([string]$Value)
    return ($Value -replace '[^\w.-]', '-')
}

function Get-DefaultPackageVersion {
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $baseVersion = [string]$packageJson.version
    Assert-Rc (-not [string]::IsNullOrWhiteSpace($baseVersion)) 'package.json must define a version for Windows packaging.'

    if ([string]::IsNullOrWhiteSpace($VersionSuffix)) {
        return $baseVersion
    }

    return "$baseVersion-$VersionSuffix"
}

function Get-GitValue {
    param([string[]]$Arguments)

    $result = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        return 'unknown'
    }

    return (($result | Out-String).Trim())
}

function Get-ProjectPackageVersion {
    param(
        [xml]$Project,
        [string]$PackageName
    )

    $reference = $Project.Project.ItemGroup.PackageReference | Where-Object { $_.Include -eq $PackageName } | Select-Object -First 1
    if (-not $reference) {
        return 'unknown'
    }

    return [string]$reference.Version
}

function ConvertTo-MarkdownCodeBlock {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return '_No output captured._'
    }

    return "``````text`n$Value`n``````"
}

function Invoke-RcStep {
    param(
        [string]$Name,
        [string]$CommandText,
        [string[]]$Command
    )

    Write-RcLine $Name
    $executable = $Command[0]
    $arguments = if ($Command.Count -gt 1) { $Command[1..($Command.Count - 1)] } else { @() }
    $startedAt = Get-Date
    $output = & $executable @arguments 2>&1 | ForEach-Object { $_.ToString() }
    $exitCode = if ($null -ne $LASTEXITCODE) { [int]$LASTEXITCODE } else { 0 }
    $finishedAt = Get-Date
    $outputText = ($output -join [Environment]::NewLine).Trim()

    [pscustomobject]@{
        name = $Name
        command = $CommandText
        exitCode = $exitCode
        result = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
        startedAt = $startedAt.ToUniversalTime().ToString('o')
        finishedAt = $finishedAt.ToUniversalTime().ToString('o')
        durationSeconds = [math]::Round(($finishedAt - $startedAt).TotalSeconds, 2)
        output = $outputText
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-DefaultPackageVersion
}

$safeVersion = ConvertTo-SafePackageName $Version
$outputFolder = Join-Path $packageRoot "LensDocsStudio.Windows-$safeVersion"
$zipPath = Join-Path $packageRoot "LensDocsStudio.Windows-$safeVersion.zip"
$staticAppRoot = Join-Path $outputFolder 'StaticApp'
$appExecutablePath = Join-Path $outputFolder 'LensDocsStudio.Windows.exe'

$timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $reportRoot "LensDocsStudio.Windows-$safeVersion-rc-$timestamp.md"
}

$resolvedReportPath = [System.IO.Path]::GetFullPath($ReportPath)
$reportDirectory = Split-Path -Parent $resolvedReportPath
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

$projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
$windowsAppSdkVersion = Get-ProjectPackageVersion -Project $projectXml -PackageName 'Microsoft.WindowsAppSDK'
$webView2PackageVersion = Get-ProjectPackageVersion -Project $projectXml -PackageName 'Microsoft.Web.WebView2'
$targetFramework = ([string]$projectXml.Project.PropertyGroup.TargetFramework).Trim()
$targetPlatformMinVersion = ([string]$projectXml.Project.PropertyGroup.TargetPlatformMinVersion).Trim()

$branch = Get-GitValue -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
$commitSha = Get-GitValue -Arguments @('rev-parse', 'HEAD')
$workingTreeStatus = Get-GitValue -Arguments @('status', '--short')
$dotnetSdkVersion = ((& dotnet --version 2>$null) | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($dotnetSdkVersion)) {
    $dotnetSdkVersion = 'unknown'
}

$buildCommand = @(
    'pwsh',
    '-NoLogo',
    '-NoProfile',
    '-File',
    (Join-Path $PSScriptRoot 'Build-WindowsPackage.ps1'),
    '-Configuration',
    $Configuration,
    '-Version',
    $Version,
    '-RuntimeIdentifier',
    $RuntimeIdentifier,
    '-NoSmoke'
)
if ($KeepOutput) {
    $buildCommand += '-KeepOutput'
}

$staticCommand = @(
    'pwsh',
    '-NoLogo',
    '-NoProfile',
    '-File',
    (Join-Path $PSScriptRoot 'Test-WindowsStaticAssets.ps1'),
    '-StaticAppRoot',
    $staticAppRoot
)

$smokeCommand = @(
    'pwsh',
    '-NoLogo',
    '-NoProfile',
    '-File',
    (Join-Path $PSScriptRoot 'Run-WindowsNativeBridgeSmoke.ps1'),
    '-NoBuild',
    '-AppExecutablePath',
    $appExecutablePath,
    '-TimeoutSeconds',
    [string]$TimeoutSeconds
)

$steps = @()
$steps += Invoke-RcStep -Name 'Build Windows folder and ZIP package' -CommandText "pwsh -NoLogo -NoProfile -File scripts/windows/Build-WindowsPackage.ps1 -Configuration $Configuration -Version $Version -RuntimeIdentifier $RuntimeIdentifier -NoSmoke" -Command $buildCommand

Assert-Rc (Test-Path -LiteralPath $outputFolder -PathType Container) "Package output folder was not found: $outputFolder"
Assert-Rc (Test-Path -LiteralPath $zipPath -PathType Leaf) "Package ZIP was not found: $zipPath"
Assert-Rc (Test-Path -LiteralPath $staticAppRoot -PathType Container) "Packaged StaticApp folder was not found: $staticAppRoot"
Assert-Rc (Test-Path -LiteralPath $appExecutablePath -PathType Leaf) "Packaged executable was not found: $appExecutablePath"
Assert-WindowsPackagePayloadClean -RootPath $outputFolder -Context 'Windows release-candidate package folder'
Assert-WindowsPackageZipPayloadClean -ZipPath $zipPath -Context 'Windows release-candidate package ZIP'

$steps += Invoke-RcStep -Name 'Validate packaged StaticApp' -CommandText "pwsh -NoLogo -NoProfile -File scripts/windows/Test-WindowsStaticAssets.ps1 -StaticAppRoot `"$staticAppRoot`"" -Command $staticCommand
$steps += Invoke-RcStep -Name 'Run packaged native bridge smoke' -CommandText "pwsh -NoLogo -NoProfile -File scripts/windows/Run-WindowsNativeBridgeSmoke.ps1 -NoBuild -AppExecutablePath `"$appExecutablePath`" -TimeoutSeconds $TimeoutSeconds" -Command $smokeCommand

$zipItem = Get-Item -LiteralPath $zipPath
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
$overallResult = if (@($steps | Where-Object { $_.exitCode -ne 0 }).Count -eq 0) { 'PASS' } else { 'FAIL' }
$resolvedOutputFolder = (Resolve-Path -LiteralPath $outputFolder).Path
$resolvedZipPath = (Resolve-Path -LiteralPath $zipPath).Path
$workingTreeDisplay = if ([string]::IsNullOrWhiteSpace($workingTreeStatus)) { 'clean' } else { 'dirty' }

$metadata = [pscustomobject]@{
    product = 'Lens Docs Studio'
    packageVersion = $Version
    commitSha = $commitSha
    branch = $branch
    workingTreeStatus = $workingTreeDisplay
    buildTimestampUtc = $timestamp
    configuration = $Configuration
    runtimeIdentifier = $RuntimeIdentifier
    outputFolder = $resolvedOutputFolder
    zipPath = $resolvedZipPath
    zipSizeBytes = $zipItem.Length
    zipSha256 = $zipHash
    runtimePrerequisites = @(
        ".NET desktop runtime for $targetFramework",
        "Windows App SDK runtime $windowsAppSdkVersion",
        "Evergreen Microsoft Edge WebView2 Runtime compatible with Microsoft.Web.WebView2 $webView2PackageVersion"
    )
    validationResult = $overallResult
    validationSteps = $steps
}

$jsonPath = [System.IO.Path]::ChangeExtension($resolvedReportPath, '.json')
$metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$manualSmoke = @'
1. Extract the ZIP into a clean local folder.
2. Launch `LensDocsStudio.Windows.exe` from the extracted folder.
3. Confirm the app opens without a local HTTP server, GitHub Pages, or a CDN.
4. Use **Help > Check Windows bridge** and confirm the Windows bridge reports `diagnostics.ping`, `file.open`, `file.save`, `file.saveAs`, `workspace.openFolder`, `workspace.saveFile`, `workspace.createFile`, `workspace.watch`, and `workspace.refreshFile`.
5. Use **Help > Open feature guide** and confirm the local guide opens in read-only mode.
6. Use **File > Open file** with a `.md`, `.markdown`, `.mmd`, `.mermaid`, or `.txt` file.
7. Edit the file and use **File > Save changes**.
8. Use **File > Save as** and confirm the new file is written.
9. Use **File > Open folder** with a folder containing Markdown and Mermaid files.
10. Confirm the workspace browser shows safe relative paths and selecting files updates editor and preview.
11. Edit a workspace file and use **File > Save changes**.
12. Create a new Markdown file in the workspace and confirm it appears in the list.
13. Modify a clean workspace file externally and confirm an external-change marker/status appears.
14. Use **Refresh active file** and confirm the external content loads.
15. Modify the same file externally again, make local edits before refresh, and confirm the dirty external-conflict marker appears.
16. Cancel refresh and confirm local edits remain.
17. Confirm refresh and verify the external content loads only after explicit confirmation.
18. Delete a workspace file externally and confirm local content is not silently erased.
19. Rename a workspace file externally and confirm the safe renamed indication.
20. Disconnect network access or otherwise block internet access, relaunch from the extracted folder, and confirm the app still opens, renders Markdown, renders Mermaid, opens the feature guide, and can run local save workflows.
'@

$claimed = @'
- Builds the framework-dependent Windows folder/ZIP package for the selected runtime identifier.
- Verifies the packaged `StaticApp/` contains required app shell files, service-worker cached assets, pinned vendor assets, the help guide, manifest, icon, and no unexpected external runtime script/style/CDN dependencies in scanned runtime files.
- Runs the native bridge smoke harness against the packaged executable.
- Records package version, branch, commit SHA, build timestamp, package paths, ZIP size, SHA256 checksum, runtime prerequisites, commands, outputs, durations, and results.
- Provides manual packaged-app smoke steps for release-candidate testers.
'@

$notClaimed = @'
- No MSIX package, installer wizard, signing, certificates, Store metadata, auto-update, file associations, WebView2 bootstrapper, or WebView2 Fixed Version Runtime is produced.
- No guarantee is made that prerequisites are installed on tester machines.
- No network interception, telemetry review, antivirus reputation check, accessibility audit, performance benchmark, or cross-machine install validation is performed.
- No merge to `main`, public release publication, or stable-channel certification is performed.
- No new editor, export, import, or runtime feature behaviour is certified beyond the packaged asset validation, automated bridge smoke, and listed manual smoke steps.
'@

$stepSummaryRows = $steps | ForEach-Object {
    "| $($_.name) | $($_.result) | $($_.exitCode) | $($_.durationSeconds) |"
}

$stepDetails = $steps | ForEach-Object {
    @"
### $($_.name)

- Result: $($_.result)
- Exit code: $($_.exitCode)
- Duration: $($_.durationSeconds) seconds
- Command: ``$($_.command)``

$(ConvertTo-MarkdownCodeBlock $_.output)
"@
}

$report = @"
# Windows Package Release Candidate Certification

Result: **$overallResult**

## Package Metadata

| Field | Value |
| --- | --- |
| Product | Lens Docs Studio |
| Package version | ``$Version`` |
| Commit SHA | ``$commitSha`` |
| Branch | ``$branch`` |
| Working tree status at start | ``$workingTreeDisplay`` |
| Build timestamp (UTC) | ``$timestamp`` |
| Configuration | ``$Configuration`` |
| Runtime identifier | ``$RuntimeIdentifier`` |
| Target framework | ``$targetFramework`` |
| Target platform minimum | ``$targetPlatformMinVersion`` |
| Output folder | ``$resolvedOutputFolder`` |
| ZIP path | ``$resolvedZipPath`` |
| ZIP size | $($zipItem.Length) bytes |
| ZIP SHA256 | ``$zipHash`` |
| JSON metadata | ``$jsonPath`` |
| dotnet SDK on certification host | ``$dotnetSdkVersion`` |

## Runtime Prerequisites

- .NET desktop runtime for ``$targetFramework``.
- Windows App SDK runtime ``$windowsAppSdkVersion``.
- Evergreen Microsoft Edge WebView2 Runtime compatible with Microsoft.Web.WebView2 ``$webView2PackageVersion``.
- Windows 10 version 2004 / build ``$targetPlatformMinVersion`` or newer.

The package is framework-dependent and does not bundle prerequisite installers.

## Validation Summary

| Step | Result | Exit code | Duration (seconds) |
| --- | --- | ---: | ---: |
$($stepSummaryRows -join [Environment]::NewLine)

## What This Certification Claims

$claimed

## What This Certification Does Not Claim

$notClaimed

## Manual Packaged App Smoke

$manualSmoke

## Validation Details

$($stepDetails -join [Environment]::NewLine)
"@

$report | Set-Content -LiteralPath $resolvedReportPath -Encoding utf8

Write-RcLine "Report: $resolvedReportPath"
Write-RcLine "JSON metadata: $jsonPath"
Write-RcLine "ZIP SHA256: $zipHash"
Write-RcLine "Result: $overallResult"

if ($overallResult -ne 'PASS') {
    exit 1
}

exit 0
