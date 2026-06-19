param(
    [string]$OutputDirectory,
    [switch]$AllowDirty,
    [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptName = Split-Path -Leaf $PSCommandPath
$scriptVersion = '1.0'

function Write-SmokeZipLine {
    param([string]$Message)
    Write-Host "[local-dev-smoke-zip] $Message"
}

function Assert-SmokeZip {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function ConvertTo-RelativeZipPath {
    param([string]$Path)
    return ($Path -replace '\\', '/').TrimStart('./')
}

function Add-PackagePath {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$RelativePath
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return
    }

    $normalised = ConvertTo-RelativeZipPath $RelativePath
    if ($normalised -eq '') {
        return
    }

    $rootedPath = Join-Path $repoRoot ($normalised -replace '/', '\')
    if (Test-Path -LiteralPath $rootedPath -PathType Leaf) {
        [void]$Set.Add($normalised)
    }
}

function Add-PackageDirectory {
    param(
        [System.Collections.Generic.HashSet[string]]$Set,
        [string]$RelativeDirectory
    )

    $rootedDirectory = Join-Path $repoRoot $RelativeDirectory
    Assert-SmokeZip (Test-Path -LiteralPath $rootedDirectory -PathType Container) "Required package directory was not found: $RelativeDirectory"

    Get-ChildItem -LiteralPath $rootedDirectory -Recurse -File | ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($repoRoot, $_.FullName)
        Add-PackagePath -Set $Set -RelativePath $relative
    }
}

function Get-ServiceWorkerLocalAssets {
    $serviceWorkerPath = Join-Path $repoRoot 'service-worker.js'
    $source = Get-Content -LiteralPath $serviceWorkerPath -Raw
    $match = [regex]::Match($source, 'const LOCAL_ASSETS = \[([\s\S]*?)\];')
    Assert-SmokeZip $match.Success 'Could not find LOCAL_ASSETS in service-worker.js.'

    return [regex]::Matches($match.Groups[1].Value, "'([^']+)'") | ForEach-Object {
        $_.Groups[1].Value
    }
}

function Get-VendorManifestAssets {
    $vendorManifestPath = Join-Path $repoRoot 'assets\vendor\manifest.json'
    Assert-SmokeZip (Test-Path -LiteralPath $vendorManifestPath -PathType Leaf) 'assets/vendor/manifest.json was not found.'
    return Get-Content -LiteralPath $vendorManifestPath -Raw | ConvertFrom-Json
}

function Assert-NoBlockedPackagePath {
    param([string]$RelativePath)

    $blockedPatterns = @(
        '^\.git(?:/|$)',
        '^node_modules(?:/|$)',
        '^test-results(?:/|$)',
        '^playwright-report(?:/|$)',
        '^artifacts(?:/|$)',
        '^artifacts/word-templates(?:/|$)',
        '^src/windows/.+/(?:bin|obj)(?:/|$)',
        '\.docx$',
        '\.dotx$',
        '\.docm$',
        '\.dotm$',
        'word-template-pack',
        'WebView2',
        'IndexedDB'
    )

    foreach ($pattern in $blockedPatterns) {
        Assert-SmokeZip (-not [regex]::IsMatch($RelativePath, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) "Blocked package path selected: $RelativePath"
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\local-dev-smoke'
}

$branch = (& git -C $repoRoot branch --show-current).Trim()
Assert-SmokeZip (-not [string]::IsNullOrWhiteSpace($branch)) 'Could not determine current Git branch.'
$commitSha = (& git -C $repoRoot rev-parse HEAD).Trim()
Assert-SmokeZip ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($commitSha)) 'Could not determine current Git commit SHA.'
$shortSha = (& git -C $repoRoot rev-parse --short HEAD).Trim()
Assert-SmokeZip ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($shortSha)) 'Could not determine current Git short SHA.'

$dirtyStatus = @(& git -C $repoRoot status --short)
$isDirty = $dirtyStatus.Count -gt 0
if ($isDirty -and -not $AllowDirty) {
    Write-SmokeZipLine 'Dirty working tree detected. Re-run with -AllowDirty only for local validation, or commit/stash changes first.'
    $dirtyStatus | ForEach-Object { Write-SmokeZipLine "DIRTY $_" }
    throw 'Refusing to create local dev smoke ZIP from a dirty working tree.'
}

if ($isDirty) {
    Write-SmokeZipLine 'WARNING: Dirty working tree detected. Metadata will record dirty status for this local-only smoke ZIP.'
    $dirtyStatus | ForEach-Object { Write-SmokeZipLine "DIRTY $_" }
}

$outputDirectoryPath = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$zipFileName = "LensDocsStudio-local-dev-$shortSha-word-template-smoke.zip"
$zipPath = Join-Path $outputDirectoryPath $zipFileName
$stagingRoot = Join-Path $outputDirectoryPath "_staging-$shortSha"
$metadataPath = Join-Path $stagingRoot 'lens-docs-studio-local-dev-smoke.json'

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

$packagePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($rootAsset in @(
    'index.html',
    'md-mmd-renderer-v5.html',
    'service-worker.js',
    'manifest.webmanifest',
    'icon.svg',
    'docs/tool-guide.md',
    'assets/scripts/exports/export-service.js',
    'assets/scripts/exports/word-template-service.js'
)) {
    Add-PackagePath -Set $packagePaths -RelativePath $rootAsset
}

foreach ($asset in Get-ServiceWorkerLocalAssets) {
    if ($asset -eq './') {
        continue
    }
    Add-PackagePath -Set $packagePaths -RelativePath $asset
}

foreach ($asset in Get-VendorManifestAssets) {
    Add-PackagePath -Set $packagePaths -RelativePath $asset
}

foreach ($directory in @(
    'assets\scripts',
    'assets\styles',
    'assets\vendor'
)) {
    Add-PackageDirectory -Set $packagePaths -RelativeDirectory $directory
}

foreach ($relativePath in $packagePaths) {
    Assert-NoBlockedPackagePath -RelativePath $relativePath
    $sourcePath = Join-Path $repoRoot ($relativePath -replace '/', '\')
    Assert-SmokeZip (Test-Path -LiteralPath $sourcePath -PathType Leaf) "Selected package file does not exist: $relativePath"
    $targetPath = Join-Path $stagingRoot ($relativePath -replace '/', '\')
    $targetDirectory = Split-Path -Parent $targetPath
    if (-not (Test-Path -LiteralPath $targetDirectory)) {
        New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    }
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
}

$requiredPackageFiles = @(
    'index.html',
    'service-worker.js',
    'manifest.webmanifest',
    'assets/styles/app.css',
    'assets/scripts/main.js',
    'assets/scripts/exports/export-service.js',
    'assets/scripts/exports/word-template-service.js',
    'assets/vendor/manifest.json',
    'assets/vendor/fflate-0.8.2.browser.js',
    'docs/tool-guide.md'
)
foreach ($requiredFile in $requiredPackageFiles) {
    Assert-SmokeZip ($packagePaths.Contains($requiredFile)) "Required file was not selected for the local dev smoke ZIP: $requiredFile"
}

$metadata = [ordered]@{
    artifactType = 'local-dev-smoke'
    branch = $branch
    commitSha = $commitSha
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    scriptName = $scriptName
    scriptVersion = $scriptVersion
    officialRelease = $false
    releaseArtifact = $false
    intendedSmokeTarget = 'word-export-templates'
    packageFileCount = $packagePaths.Count
    dirtyWorkingTree = $isDirty
    dirtyStatus = @($dirtyStatus)
    expectedImportantFiles = $requiredPackageFiles
    excludedPathRules = @(
        '.git/',
        'node_modules/',
        'test-results/',
        'playwright-report/',
        'artifacts/',
        'artifacts/word-templates/',
        '*.docx',
        '*.dotx',
        'WebView2 runtime data',
        'IndexedDB/browser storage',
        'corporate/user-provided Word templates and extracted media'
    )
    localOnlyNotice = 'This ZIP is a local development smoke artefact for manual validation. It is not an official release, release asset, installer asset, frozen artefact, or production-readiness claim.'
}

$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $metadataPath -Encoding UTF8
$packageItems = Get-ChildItem -LiteralPath $stagingRoot -Force
Assert-SmokeZip ($packageItems.Count -gt 0) "Staging folder is empty: $stagingRoot"
Compress-Archive -LiteralPath $packageItems.FullName -DestinationPath $zipPath -Force
Assert-SmokeZip (Test-Path -LiteralPath $zipPath -PathType Leaf) "ZIP was not created: $zipPath"

if (-not $KeepStaging) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}

Write-Host ''
Write-SmokeZipLine 'PASS: local dev smoke ZIP created.'
Write-SmokeZipLine "Output ZIP path: $zipPath"
Write-SmokeZipLine "Current branch: $branch"
Write-SmokeZipLine "Current commit SHA: $commitSha"
Write-SmokeZipLine "Dirty working tree: $isDirty"
Write-SmokeZipLine "Packaged files: $($packagePaths.Count)"
Write-SmokeZipLine 'NOTE: This is a local development smoke ZIP only. It is not an official release artefact.'
