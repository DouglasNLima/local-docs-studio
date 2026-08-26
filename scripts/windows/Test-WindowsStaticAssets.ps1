param(
    [switch]$NoBuild,
    [string]$Configuration = 'Debug',
    [string]$Platform = 'Any CPU',
    [string]$PreferredOutputPlatform = 'x64',
    [string]$StaticAppRoot
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$solutionPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows.sln'
$projectDirectory = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows'

function Write-AssetLine {
    param([string]$Message)
    Write-Host "[windows-static-assets] $Message"
}

function Assert-Asset {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function ConvertTo-RelativeAssetPath {
    param([string]$Asset)
    return $Asset -replace '^\./', ''
}

function Get-ServiceWorkerAssets {
    param([string]$ServiceWorkerPath)

    $source = Get-Content -LiteralPath $ServiceWorkerPath -Raw
    $match = [regex]::Match($source, 'const LOCAL_ASSETS = \[([\s\S]*?)\];')
    Assert-Asset $match.Success 'Could not find LOCAL_ASSETS in packaged service-worker.js.'

    return [regex]::Matches($match.Groups[1].Value, "'([^']+)'") | ForEach-Object {
        $_.Groups[1].Value
    }
}

function Get-LatestStaticAppRoot {
    $candidates = Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'bin') -Recurse -Directory -Filter 'StaticApp' -ErrorAction SilentlyContinue |
        Sort-Object @{ Expression = { if ($_.FullName -match "\\$PreferredOutputPlatform\\") { 0 } else { 1 } }; Ascending = $true }, @{ Expression = { if ($_.FullName -match "\\$Configuration\\") { 0 } else { 1 } }; Ascending = $true }, @{ Expression = { $_.LastWriteTimeUtc }; Descending = $true }

    Assert-Asset ($candidates -and $candidates.Count -gt 0) 'No Windows StaticApp output folder was found. Run without -NoBuild first.'
    return $candidates[0].FullName
}

function Assert-PackagedFile {
    param(
        [string]$StaticAppRoot,
        [string]$Asset
    )

    if ($Asset -eq './') {
        return
    }

    $relativePath = ConvertTo-RelativeAssetPath $Asset
    $path = Join-Path $StaticAppRoot $relativePath
    Assert-Asset (Test-Path -LiteralPath $path -PathType Leaf) "Missing packaged static app file: $relativePath"
}

function Assert-PowerShellScriptParses {
    param([string]$Path)

    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.PSParser]::Tokenize((Get-Content -LiteralPath $Path -Raw), [ref]$parseErrors) | Out-Null
    Assert-Asset (-not $parseErrors -or $parseErrors.Count -eq 0) "PowerShell script has parse errors: $Path"
}

function Invoke-DryRunScript {
    param(
        [string]$Name,
        [string]$Path
    )

    Write-AssetLine "Validating $Name dry run..."
    $output = & pwsh -NoLogo -NoProfile -File $Path -DryRun 2>&1 | ForEach-Object { $_.ToString() }
    Assert-Asset ($LASTEXITCODE -eq 0) "$Name -DryRun failed: $($output -join [Environment]::NewLine)"
    Assert-Asset (($output -join [Environment]::NewLine) -match 'DRY RUN|Dry run') "$Name -DryRun did not print dry-run operations."
}

foreach ($scriptName in @(
    'Register-WindowsFileAssociations.ps1',
    'Unregister-WindowsFileAssociations.ps1',
    'WindowsPackagePayloadHygiene.ps1',
    'Test-WindowsPackagePayloadHygiene.ps1',
    'New-WindowsManualPackagedSanityChecklist.ps1',
    'Test-WindowsManualPackagedSanityChecklist.ps1',
    'New-LocalDevSmokeZip.ps1',
    'Test-LocalDevSmokeZip.ps1',
    'New-WindowsLocalDevSmokePackage.ps1',
    'Test-WindowsLocalDevSmokePackage.ps1'
)) {
    $scriptPath = Join-Path $PSScriptRoot $scriptName
    Assert-Asset (Test-Path -LiteralPath $scriptPath -PathType Leaf) "Missing Windows validation script: $scriptName"
    Assert-PowerShellScriptParses -Path $scriptPath
}

foreach ($scriptName in @(
    'Register-WindowsFileAssociations.ps1',
    'Unregister-WindowsFileAssociations.ps1'
)) {
    $scriptPath = Join-Path $PSScriptRoot $scriptName
    Invoke-DryRunScript -Name $scriptName -Path $scriptPath
}

Write-AssetLine 'Validating Windows package payload hygiene guard...'
& pwsh -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Test-WindowsPackagePayloadHygiene.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Windows package payload hygiene guard failed with exit code $LASTEXITCODE."
}

Write-AssetLine 'Validating Windows manual packaged sanity checklist helper...'
& pwsh -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Test-WindowsManualPackagedSanityChecklist.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Windows manual packaged sanity checklist helper failed with exit code $LASTEXITCODE."
}

Write-AssetLine 'Validating local dev smoke ZIP helper...'
& pwsh -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'Test-LocalDevSmokeZip.ps1')
if ($LASTEXITCODE -ne 0) {
    throw "Local dev smoke ZIP helper failed with exit code $LASTEXITCODE."
}

if ($StaticAppRoot) {
    $NoBuild = $true
}

if (-not $NoBuild) {
    Write-AssetLine "Building Windows shell ($Configuration|$Platform)..."
    dotnet build $solutionPath -c $Configuration -p:Platform=$Platform
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }
}

$staticAppRoot = if ($StaticAppRoot) {
    $resolvedStaticAppRoot = Resolve-Path -LiteralPath $StaticAppRoot -ErrorAction Stop
    $resolvedStaticAppRoot.Path
} else {
    Get-LatestStaticAppRoot
}
Write-AssetLine "Inspecting $staticAppRoot"

$requiredRootAssets = @(
    './index.html',
    './assets/styles/app.css',
    './assets/scripts/main.js',
    './assets/vendor/manifest.json',
    './docs/tool-guide.md',
    './manifest.webmanifest',
    './icon.svg',
    './md-mmd-renderer-v5.html',
    './service-worker.js'
)

foreach ($asset in $requiredRootAssets) {
    Assert-PackagedFile -StaticAppRoot $staticAppRoot -Asset $asset
}

$serviceWorkerPath = Join-Path $staticAppRoot 'service-worker.js'
$serviceWorkerAssets = @(Get-ServiceWorkerAssets -ServiceWorkerPath $serviceWorkerPath)
foreach ($asset in $serviceWorkerAssets) {
    Assert-PackagedFile -StaticAppRoot $staticAppRoot -Asset $asset
}

$vendorManifestPath = Join-Path $staticAppRoot 'assets\vendor\manifest.json'
$vendorAssets = @(Get-Content -LiteralPath $vendorManifestPath -Raw | ConvertFrom-Json)
Assert-Asset ($vendorAssets.Count -gt 0) 'Packaged vendor manifest must list local vendor assets.'

$requiredVendorAssets = @(
    './assets/vendor/mermaid-11.15.0.esm.min.js',
    './assets/vendor/marked-16.4.2.esm.js',
    './assets/vendor/dompurify-3.4.5.es.js',
    './assets/vendor/highlight-11.11.1.esm.js',
    './assets/vendor/fflate-0.8.2.browser.js',
    './assets/vendor/katex-0.16.25.min.css',
    './assets/vendor/chunks/mermaid.esm.min/katex-K3KEBU37.js',
    './assets/vendor/mammoth-1.12.0.browser.min.js',
    './assets/vendor/pdfjs-5.7.284.js',
    './assets/vendor/pdfjs-5.7.284.worker.js'
)

foreach ($asset in $requiredVendorAssets) {
    Assert-Asset ($vendorAssets -contains $asset) "Packaged vendor manifest is missing $asset"
}

foreach ($asset in $vendorAssets) {
    Assert-Asset ($asset -like './assets/vendor/*') "Packaged vendor manifest asset must stay under assets/vendor: $asset"
    Assert-PackagedFile -StaticAppRoot $staticAppRoot -Asset $asset
}

$runtimeExternalPatterns = @(
    '<script\b[^>]*\bsrc=["'']https?://',
    '<link\b[^>]*\bhref=["'']https?://[^"'']+["''][^>]*\brel=["'']stylesheet["'']',
    '@import\s+(?:url\()?["'']?https?://',
    'url\(["'']?https?://',
    '\b(?:cdn\.|unpkg\.|jsdelivr\.|googleapis\.|fonts\.)'
)

$runtimeFiles = @(
    'index.html',
    'md-mmd-renderer-v5.html',
    'manifest.webmanifest',
    'service-worker.js',
    'assets/styles/app.css',
    'assets/vendor/manifest.json'
)

foreach ($asset in $serviceWorkerAssets) {
    $relativePath = ConvertTo-RelativeAssetPath $asset
    if ($relativePath -like 'assets/scripts/*.js' -or $relativePath -like 'assets/scripts/*/*.js') {
        $runtimeFiles += $relativePath
    }
}

foreach ($relativePath in ($runtimeFiles | Select-Object -Unique)) {
    $path = Join-Path $staticAppRoot $relativePath
    Assert-Asset (Test-Path -LiteralPath $path -PathType Leaf) "Runtime scan target is missing: $relativePath"
    $source = Get-Content -LiteralPath $path -Raw
    foreach ($pattern in $runtimeExternalPatterns) {
        Assert-Asset (-not [regex]::IsMatch($source, $pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) "Unexpected external runtime dependency in packaged file: $relativePath"
    }
}

Write-AssetLine "PASS: verified $($serviceWorkerAssets.Count) service-worker assets and $($vendorAssets.Count) vendor assets in packaged StaticApp."
