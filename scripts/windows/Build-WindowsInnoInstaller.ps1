param(
    [string]$Version = '0.1.0-dev',
    [string]$Configuration = 'Release',
    [switch]$NoPackageBuild,
    [string]$InnoCompilerPath,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$innoScriptPath = Join-Path $repoRoot 'installer\inno\LensDocsStudio.iss'
$packageRoot = Join-Path $repoRoot 'artifacts\windows'
$installerRoot = Join-Path $repoRoot 'artifacts\installers\inno'
. (Join-Path $PSScriptRoot 'WindowsPackagePayloadHygiene.ps1')

function Write-InnoLine {
    param([string]$Message)
    Write-Host "[windows-inno-installer] $Message"
}

function Assert-Inno {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function ConvertTo-SafeName {
    param([string]$Value)
    return ($Value -replace '[^\w.-]', '-')
}

function ConvertTo-MarkdownCodeBlock {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return '_No output captured._'
    }

    return "``````text`n$Value`n``````"
}

function Resolve-InnoCompiler {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (Test-Path -LiteralPath $RequestedPath -PathType Leaf) {
            return (Resolve-Path -LiteralPath $RequestedPath).Path
        }

        throw "Inno Setup compiler was not found at InnoCompilerPath: $RequestedPath"
    }

    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $knownPaths = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($path in $knownPaths) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            return (Resolve-Path -LiteralPath $path).Path
        }
    }

    return $null
}

function New-InstallerReport {
    param(
        [string]$Path,
        [string]$Result,
        [string]$Version,
        [string]$Configuration,
        [string]$CommitSha,
        [string]$PackageFolder,
        [string]$InstallerPath,
        [string]$InstallerSha256,
        [string]$InnoCompiler,
        [string]$Details
    )

    $timestamp = (Get-Date).ToUniversalTime().ToString('o')
    $packageDisplay = if ($PackageFolder) { "``$PackageFolder``" } else { '_Not resolved._' }
    $installerDisplay = if ($InstallerPath) { "``$InstallerPath``" } else { '_Not generated._' }
    $shaDisplay = if ($InstallerSha256) { "``$InstallerSha256``" } else { '_Not generated._' }
    $compilerDisplay = if ($InnoCompiler) { "``$InnoCompiler``" } else { '_Not available._' }

    $report = @"
# Inno Installer MVP Build Report

Result: **$Result**

## Metadata

| Field | Value |
| --- | --- |
| Product | Lens Docs Studio |
| Version | ``$Version`` |
| Configuration | ``$Configuration`` |
| Commit SHA | ``$CommitSha`` |
| Timestamp (UTC) | ``$timestamp`` |
| Inno compiler | $compilerDisplay |
| Package folder | $packageDisplay |
| Installer path | $installerDisplay |
| Installer SHA256 | $shaDisplay |

## Runtime Prerequisites

- .NET 8 Desktop Runtime.
- Windows App SDK Runtime matching the project package reference.
- Evergreen Microsoft Edge WebView2 Runtime.

The installer does not install, download, or bootstrap prerequisites.

## Installer Decisions

- Installs per-user under ``%LOCALAPPDATA%\Programs\Lens Docs Studio``.
- Uses ``PrivilegesRequired=lowest`` and does not require administrator elevation.
- Creates a Start Menu shortcut by default.
- Offers an optional Desktop shortcut.
- Offers optional per-user file associations for ``.md``, ``.markdown``, ``.mmd``, ``.mermaid``, and ``.txt``.
- File associations write only ``HKCU:\Software\Classes`` and do not write Windows ``UserChoice``.
- Does not publish a release, upload assets, create tags, move tags, or merge to ``main``.

## Details

$(ConvertTo-MarkdownCodeBlock $Details)
"@

    $report | Set-Content -LiteralPath $Path -Encoding utf8
}

Assert-Inno (Test-Path -LiteralPath $innoScriptPath -PathType Leaf) "Missing Inno Setup script: $innoScriptPath"
New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null

$safeVersion = ConvertTo-SafeName $Version
$packageFolder = Join-Path $packageRoot "LensDocsStudio.Windows-$safeVersion"
$appExecutablePath = Join-Path $packageFolder 'LensDocsStudio.Windows.exe'
$staticAppRoot = Join-Path $packageFolder 'StaticApp'
$outputBaseFilename = "LensDocsStudio.Windows-$safeVersion-Setup"
$installerPath = Join-Path $installerRoot "$outputBaseFilename.exe"
$shaPath = "$installerPath.sha256"
$reportPath = Join-Path $installerRoot "$outputBaseFilename-report.md"
$commitSha = ((& git -C $repoRoot rev-parse HEAD 2>$null) | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($commitSha)) {
    $commitSha = 'unknown'
}

$compilerPath = Resolve-InnoCompiler -RequestedPath $InnoCompilerPath
if ([string]::IsNullOrWhiteSpace($compilerPath)) {
    $blocked = 'INNO_INSTALLER_MVP_BLOCKED_INNO_SETUP_NOT_INSTALLED'
    $details = @"
$blocked

Install Inno Setup 6 or pass -InnoCompilerPath with the full path to ISCC.exe.
"@
    New-InstallerReport -Path $reportPath -Result $blocked -Version $Version -Configuration $Configuration -CommitSha $commitSha -PackageFolder $packageFolder -InstallerPath $null -InstallerSha256 $null -InnoCompiler $null -Details $details
    Write-InnoLine $blocked
    Write-InnoLine "Report: $reportPath"
    exit 2
}

Write-InnoLine "Using Inno Setup compiler: $compilerPath"

if (-not $NoPackageBuild) {
    Write-InnoLine "Building Windows package ($Configuration, $Version)..."
    & (Join-Path $PSScriptRoot 'Build-WindowsPackage.ps1') -Configuration $Configuration -Version $Version -NoSmoke
    if ($LASTEXITCODE -ne 0) {
        throw "Build-WindowsPackage.ps1 failed with exit code $LASTEXITCODE."
    }
} else {
    Write-InnoLine "Reusing existing Windows package: $packageFolder"
}

Assert-Inno (Test-Path -LiteralPath $packageFolder -PathType Container) "Windows package folder was not found: $packageFolder"
Assert-Inno (Test-Path -LiteralPath $appExecutablePath -PathType Leaf) "Packaged executable was not found: $appExecutablePath"
Assert-Inno (Test-Path -LiteralPath $staticAppRoot -PathType Container) "Packaged StaticApp folder was not found: $staticAppRoot"
Assert-WindowsPackagePayloadClean -RootPath $packageFolder -Context 'Inno installer package source'

Write-InnoLine 'Validating packaged StaticApp assets...'
& (Join-Path $PSScriptRoot 'Test-WindowsStaticAssets.ps1') -StaticAppRoot $staticAppRoot
if ($LASTEXITCODE -ne 0) {
    throw "Packaged static asset validation failed with exit code $LASTEXITCODE."
}

$arguments = @(
    $innoScriptPath,
    "/DAppVersion=$Version",
    "/DPackageSource=$packageFolder",
    "/DOutputDir=$installerRoot",
    "/DOutputBaseFilename=$outputBaseFilename"
)

if ($DryRun) {
    $details = "Dry run only. Would run: `"$compilerPath`" $($arguments -join ' ')"
    New-InstallerReport -Path $reportPath -Result 'DRY_RUN' -Version $Version -Configuration $Configuration -CommitSha $commitSha -PackageFolder $packageFolder -InstallerPath $installerPath -InstallerSha256 $null -InnoCompiler $compilerPath -Details $details
    Write-InnoLine 'DRY_RUN: Inno compiler was found and package validation passed.'
    Write-InnoLine "Report: $reportPath"
    exit 0
}

if (Test-Path -LiteralPath $installerPath -PathType Leaf) {
    Write-InnoLine "Removing previous installer: $installerPath"
    Remove-Item -LiteralPath $installerPath -Force
}

if (Test-Path -LiteralPath $shaPath -PathType Leaf) {
    Remove-Item -LiteralPath $shaPath -Force
}

Write-InnoLine 'Compiling installer...'
$compileOutput = & $compilerPath @arguments 2>&1 | ForEach-Object { $_.ToString() }
$compileText = ($compileOutput -join [Environment]::NewLine).Trim()
if ($LASTEXITCODE -ne 0) {
    New-InstallerReport -Path $reportPath -Result 'FAIL' -Version $Version -Configuration $Configuration -CommitSha $commitSha -PackageFolder $packageFolder -InstallerPath $installerPath -InstallerSha256 $null -InnoCompiler $compilerPath -Details $compileText
    throw "ISCC.exe failed with exit code $LASTEXITCODE. See report: $reportPath"
}

Assert-Inno (Test-Path -LiteralPath $installerPath -PathType Leaf) "Installer was not created: $installerPath"
$installerSha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
"$installerSha256  $(Split-Path -Leaf $installerPath)" | Set-Content -LiteralPath $shaPath -Encoding ascii

New-InstallerReport -Path $reportPath -Result 'PASS' -Version $Version -Configuration $Configuration -CommitSha $commitSha -PackageFolder $packageFolder -InstallerPath $installerPath -InstallerSha256 $installerSha256 -InnoCompiler $compilerPath -Details $compileText

Write-InnoLine 'PASS: Inno Setup installer created.'
Write-InnoLine "Installer: $installerPath"
Write-InnoLine "SHA256: $installerSha256"
Write-InnoLine "SHA256 file: $shaPath"
Write-InnoLine "Report: $reportPath"
