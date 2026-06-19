param(
    [string]$OutputDirectory,
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$AllowDirty,
    [switch]$KeepPackageFolder
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptName = Split-Path -Leaf $PSCommandPath
$scriptVersion = '1.0'
$projectPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows\LensDocsStudio.Windows.csproj'
$packageJsonPath = Join-Path $repoRoot 'package.json'
. (Join-Path $PSScriptRoot 'WindowsPackagePayloadHygiene.ps1')

function Write-WindowsSmokeLine {
    param([string]$Message)
    Write-Host "[windows-local-dev-smoke-package] $Message"
}

function Assert-WindowsSmokePackage {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function ConvertTo-SafePackageToken {
    param([string]$Value)
    return ($Value -replace '[^\w.-]', '-')
}

function Get-DefaultPackageVersion {
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    $baseVersion = [string]$packageJson.version
    Assert-WindowsSmokePackage (-not [string]::IsNullOrWhiteSpace($baseVersion)) 'package.json must define a version for Windows packaging.'
    return $baseVersion
}

function Assert-ZipEntries {
    param(
        [string]$ZipPath,
        [string[]]$RequiredEntries,
        [string[]]$BlockedPatterns
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
        foreach ($requiredEntry in $RequiredEntries) {
            Assert-WindowsSmokePackage ($entries -contains $requiredEntry) "Windows local dev smoke ZIP is missing required entry: $requiredEntry"
        }

        foreach ($entry in $entries) {
            foreach ($blockedPattern in $BlockedPatterns) {
                Assert-WindowsSmokePackage (-not [regex]::IsMatch($entry, $blockedPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) "Windows local dev smoke ZIP contains blocked entry: $entry"
            }
        }
    } finally {
        $archive.Dispose()
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\local-dev-package-smoke'
}

$branch = (& git -C $repoRoot branch --show-current).Trim()
Assert-WindowsSmokePackage (-not [string]::IsNullOrWhiteSpace($branch)) 'Could not determine current Git branch.'
$commitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
Assert-WindowsSmokePackage ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commitSha)) 'Could not determine current Git commit SHA.'
$shortSha = (& git -C $repoRoot rev-parse --short HEAD).Trim()
Assert-WindowsSmokePackage ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($shortSha)) 'Could not determine current Git short SHA.'

$dirtyStatus = @(& git -C $repoRoot status --short)
$isDirty = $dirtyStatus.Count -gt 0
if ($isDirty -and -not $AllowDirty) {
    Write-WindowsSmokeLine 'Dirty working tree detected. Re-run with -AllowDirty only for temporary local validation, or commit/stash changes first.'
    $dirtyStatus | ForEach-Object { Write-WindowsSmokeLine "DIRTY $_" }
    throw 'Refusing to create Windows local dev smoke ZIP from a dirty working tree.'
}

if ($isDirty) {
    Write-WindowsSmokeLine 'WARNING: Dirty working tree detected. Metadata will record dirty status for this local-only Windows smoke ZIP.'
    $dirtyStatus | ForEach-Object { Write-WindowsSmokeLine "DIRTY $_" }
}

$outputDirectoryPath = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$zipFileName = "LensDocsStudio.Windows-local-dev-$shortSha-word-template-smoke.zip"
$zipPath = Join-Path $outputDirectoryPath $zipFileName
$packageFolder = Join-Path $outputDirectoryPath "LensDocsStudio.Windows-local-dev-$shortSha-word-template-smoke"
$appExecutablePath = Join-Path $packageFolder 'LensDocsStudio.Windows.exe'
$staticAppRoot = Join-Path $packageFolder 'StaticApp'
$metadataPath = Join-Path $packageFolder 'lens-docs-studio-windows-local-dev-smoke.json'
$baseVersion = Get-DefaultPackageVersion
$packageVersion = ConvertTo-SafePackageToken "$baseVersion-local-dev-$shortSha-word-template-smoke"
$buildOutputFolder = Join-Path $repoRoot "src\windows\LensDocsStudio.Windows\bin\x64\$Configuration\net8.0-windows10.0.19041.0\$RuntimeIdentifier"

if (Test-Path -LiteralPath $packageFolder) {
    Write-WindowsSmokeLine "Cleaning $packageFolder"
    Remove-Item -LiteralPath $packageFolder -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Write-WindowsSmokeLine "Removing previous ZIP $zipPath"
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null

Write-WindowsSmokeLine "Publishing Windows shell ($Configuration, $RuntimeIdentifier, $packageVersion)..."
dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained false `
    -p:Platform=x64 `
    -p:WindowsPackageType=None `
    -p:Version=$packageVersion `
    -o $packageFolder
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Assert-WindowsSmokePackage (Test-Path -LiteralPath $appExecutablePath -PathType Leaf) "Published executable was not found: $appExecutablePath"
Assert-WindowsSmokePackage (Test-Path -LiteralPath $staticAppRoot -PathType Container) "Published StaticApp folder was not found: $staticAppRoot"

foreach ($resourceFileName in @('App.xbf', 'MainWindow.xbf', 'LensDocsStudio.Windows.pri')) {
    $resourceSource = Join-Path $buildOutputFolder $resourceFileName
    $resourceTarget = Join-Path $packageFolder $resourceFileName
    Assert-WindowsSmokePackage (Test-Path -LiteralPath $resourceSource -PathType Leaf) "Required WinUI resource was not found after publish: $resourceSource"
    Copy-Item -LiteralPath $resourceSource -Destination $resourceTarget -Force
}

Write-WindowsSmokeLine 'Removing runtime-generated WebView2/cache payloads from package output...'
$removedRuntimePayload = @(Clear-WindowsPackageRuntimePayload -RootPath $packageFolder)
if ($removedRuntimePayload.Count -gt 0) {
    Write-WindowsSmokeLine "Removed $($removedRuntimePayload.Count) blocked runtime payload path(s)."
}
Assert-WindowsPackagePayloadClean -RootPath $packageFolder -Context 'Windows local dev smoke package folder'

Write-WindowsSmokeLine 'Validating packaged static assets...'
& (Join-Path $PSScriptRoot 'Test-WindowsStaticAssets.ps1') -NoBuild -StaticAppRoot $staticAppRoot
if ($LASTEXITCODE -ne 0) {
    throw "Packaged static asset validation failed with exit code $LASTEXITCODE."
}

$requiredEntries = @(
    'LensDocsStudio.Windows.exe',
    'StaticApp/index.html',
    'StaticApp/service-worker.js',
    'StaticApp/manifest.webmanifest',
    'StaticApp/assets/styles/app.css',
    'StaticApp/assets/scripts/main.js',
    'StaticApp/assets/scripts/exports/export-service.js',
    'StaticApp/assets/scripts/exports/word-template-service.js',
    'StaticApp/assets/vendor/manifest.json',
    'StaticApp/assets/vendor/fflate-0.8.2.browser.js',
    'lens-docs-studio-windows-local-dev-smoke.json'
)

$blockedPatterns = @(
    '^\.git(?:/|$)',
    '^node_modules(?:/|$)',
    '^test-results(?:/|$)',
    '^playwright-report(?:/|$)',
    '^artifacts/windows/local-dev-smoke(?:/|$)',
    '^artifacts/windows/local-dev-smoke-validation(?:/|$)',
    '^artifacts/windows/local-dev-package-smoke(?:/|$)',
    '^artifacts/windows/local-dev-package-smoke-validation(?:/|$)',
    '^artifacts/word-templates(?:/|$)',
    '\.docx$',
    '\.dotx$',
    '\.docm$',
    '\.dotm$',
    'corporate',
    'template-pack',
    'LensDocsStudio\.Windows\.exe\.WebView2',
    'EBWebView',
    'IndexedDB',
    'Local Storage',
    'Session Storage'
)

$packageFileCount = @(Get-ChildItem -LiteralPath $packageFolder -Recurse -File -Force).Count
$metadata = [ordered]@{
    artifactType = 'windows-local-dev-smoke'
    branch = $branch
    commitSha = $commitSha
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    scriptName = $scriptName
    scriptVersion = $scriptVersion
    officialRelease = $false
    releaseArtifact = $false
    installer = $false
    notOfficialRelease = $true
    notInstaller = $true
    intendedSmokeTarget = 'word-export-templates'
    packageKind = 'windows'
    packageVersion = $packageVersion
    packageFileCount = $packageFileCount
    dirtyWorkingTree = $isDirty
    dirtyStatus = @($dirtyStatus)
    expectedImportantFiles = $requiredEntries
    excludedPathRules = @(
        '.git/',
        'node_modules/',
        'test-results/',
        'playwright-report/',
        'artifacts/windows/local-dev-smoke/',
        'artifacts/windows/local-dev-smoke-validation/',
        'artifacts/windows/local-dev-package-smoke/',
        'artifacts/windows/local-dev-package-smoke-validation/',
        'artifacts/word-templates/',
        '*.docx',
        '*.dotx',
        'WebView2 runtime data',
        'IndexedDB/browser storage',
        'corporate/user-provided Word templates and extracted media'
    )
    localOnlyNotice = 'This ZIP is a local Windows development smoke artefact for manual validation of Word export templates. It is not an official release, release asset, installer asset, frozen artefact, publication bundle, or production-readiness claim.'
}

$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
Assert-WindowsSmokePackage (Test-Path -LiteralPath $metadataPath -PathType Leaf) "Metadata file was not created: $metadataPath"

$packageItems = Get-ChildItem -LiteralPath $packageFolder -Force
Assert-WindowsSmokePackage ($packageItems.Count -gt 0) "Package output folder is empty: $packageFolder"
Assert-WindowsPackagePayloadClean -RootPath $packageFolder -Context 'Windows local dev smoke package folder before ZIP'

Write-WindowsSmokeLine "Creating ZIP $zipPath"
Compress-Archive -LiteralPath $packageItems.FullName -DestinationPath $zipPath -Force
Assert-WindowsSmokePackage (Test-Path -LiteralPath $zipPath -PathType Leaf) "ZIP was not created: $zipPath"
Assert-WindowsPackageZipPayloadClean -ZipPath $zipPath -Context 'Windows local dev smoke package ZIP'
Assert-ZipEntries -ZipPath $zipPath -RequiredEntries $requiredEntries -BlockedPatterns $blockedPatterns

if (-not $KeepPackageFolder) {
    Remove-Item -LiteralPath $packageFolder -Recurse -Force
}

Write-Host ''
Write-WindowsSmokeLine 'PASS: Windows local dev smoke ZIP created.'
Write-WindowsSmokeLine "Output ZIP path: $zipPath"
Write-WindowsSmokeLine "Current branch: $branch"
Write-WindowsSmokeLine "Current commit SHA: $commitSha"
Write-WindowsSmokeLine "Dirty working tree: $isDirty"
Write-WindowsSmokeLine "Packaged files: $packageFileCount"
Write-WindowsSmokeLine 'NOTE: This is a local Windows development smoke ZIP only. It is not an official release artefact, installer, release asset, frozen artefact, publication bundle, or production-readiness claim.'
