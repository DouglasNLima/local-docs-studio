param(
    [string]$Configuration = 'Release',
    [string]$Version,
    [string]$VersionSuffix = 'dev',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$NoSmoke,
    [switch]$KeepOutput
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$projectPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows\LensDocsStudio.Windows.csproj'
$packageJsonPath = Join-Path $repoRoot 'package.json'
$packageRoot = Join-Path $repoRoot 'artifacts\windows'
. (Join-Path $PSScriptRoot 'WindowsPackagePayloadHygiene.ps1')

function Write-PackageLine {
    param([string]$Message)
    Write-Host "[windows-package] $Message"
}

function Assert-Package {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-DefaultPackageVersion {
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $baseVersion = [string]$packageJson.version
    Assert-Package (-not [string]::IsNullOrWhiteSpace($baseVersion)) 'package.json must define a version for Windows packaging.'

    if ([string]::IsNullOrWhiteSpace($VersionSuffix)) {
        return $baseVersion
    }

    return "$baseVersion-$VersionSuffix"
}

function ConvertTo-SafePackageName {
    param([string]$Value)
    return ($Value -replace '[^\w.-]', '-')
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-DefaultPackageVersion
}

$safeVersion = ConvertTo-SafePackageName $Version
$outputFolder = Join-Path $packageRoot "LensDocsStudio.Windows-$safeVersion"
$zipPath = Join-Path $packageRoot "LensDocsStudio.Windows-$safeVersion.zip"
$appExecutablePath = Join-Path $outputFolder 'LensDocsStudio.Windows.exe'
$staticAppRoot = Join-Path $outputFolder 'StaticApp'
$buildOutputFolder = Join-Path $repoRoot "src\windows\LensDocsStudio.Windows\bin\x64\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier"

if ((Test-Path -LiteralPath $outputFolder) -and -not $KeepOutput) {
    Write-PackageLine "Cleaning $outputFolder"
    Remove-Item -LiteralPath $outputFolder -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Write-PackageLine "Removing previous ZIP $zipPath"
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null

Write-PackageLine "Publishing Windows shell ($Configuration, $RuntimeIdentifier, $Version)..."
dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained false `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:Version=$Version `
    -o $outputFolder
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Assert-Package (Test-Path -LiteralPath $appExecutablePath -PathType Leaf) "Published executable was not found: $appExecutablePath"
Assert-Package (Test-Path -LiteralPath $staticAppRoot -PathType Container) "Published StaticApp folder was not found: $staticAppRoot"

foreach ($resourceFileName in @('App.xbf', 'MainWindow.xbf', 'LensDocsStudio.Windows.pri')) {
    $resourceSource = Join-Path $buildOutputFolder $resourceFileName
    $resourceTarget = Join-Path $outputFolder $resourceFileName
    Assert-Package (Test-Path -LiteralPath $resourceSource -PathType Leaf) "Required WinUI resource was not found after publish: $resourceSource"
    Copy-Item -LiteralPath $resourceSource -Destination $resourceTarget -Force
}

Write-PackageLine 'Removing runtime-generated WebView2/cache payloads from package output...'
$removedRuntimePayload = @(Clear-WindowsPackageRuntimePayload -RootPath $outputFolder)
if ($removedRuntimePayload.Count -gt 0) {
    Write-PackageLine "Removed $($removedRuntimePayload.Count) blocked runtime payload path(s)."
}
Assert-WindowsPackagePayloadClean -RootPath $outputFolder -Context 'Windows package folder'

Write-PackageLine 'Validating packaged static assets...'
& (Join-Path $PSScriptRoot 'Test-WindowsStaticAssets.ps1') -StaticAppRoot $staticAppRoot
if ($LASTEXITCODE -ne 0) {
    throw "Packaged static asset validation failed with exit code $LASTEXITCODE."
}
$staticValidation = 'PASS'

$smokeValidation = 'SKIPPED'
if (-not $NoSmoke) {
    Write-PackageLine 'Running native bridge smoke against packaged executable...'
    & (Join-Path $PSScriptRoot 'Run-WindowsNativeBridgeSmoke.ps1') -NoBuild -AppExecutablePath $appExecutablePath
    if ($LASTEXITCODE -ne 0) {
        throw "Packaged native bridge smoke failed with exit code $LASTEXITCODE."
    }
    $smokeValidation = 'PASS'
}

Write-PackageLine "Creating ZIP $zipPath"
$packageItems = Get-ChildItem -LiteralPath $outputFolder -Force
Assert-Package ($packageItems.Count -gt 0) "Package output folder is empty: $outputFolder"
Assert-WindowsPackagePayloadClean -RootPath $outputFolder -Context 'Windows package folder before ZIP'
Compress-Archive -LiteralPath $packageItems.FullName -DestinationPath $zipPath -Force
Assert-Package (Test-Path -LiteralPath $zipPath -PathType Leaf) "ZIP artefact was not created: $zipPath"
Assert-WindowsPackageZipPayloadClean -ZipPath $zipPath -Context 'Windows package ZIP'

Write-Host ''
Write-PackageLine 'PASS: Windows folder/ZIP package created.'
Write-PackageLine "Output folder: $outputFolder"
Write-PackageLine "ZIP path: $zipPath"
Write-PackageLine "App executable: $appExecutablePath"
Write-PackageLine "Static asset validation: $staticValidation"
Write-PackageLine "Native bridge smoke: $smokeValidation"
