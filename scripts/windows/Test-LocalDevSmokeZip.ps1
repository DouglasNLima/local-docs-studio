param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptPath = Join-Path $PSScriptRoot 'New-LocalDevSmokeZip.ps1'
$scriptName = 'New-LocalDevSmokeZip.ps1'

function Write-SmokeZipTestLine {
    param([string]$Message)
    Write-Host "[local-dev-smoke-zip-test] $Message"
}

function Assert-SmokeZipTest {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Expand-SmokeZipEntryNames {
    param([string]$ZipPath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
    } finally {
        $zip.Dispose()
    }
}

Assert-SmokeZipTest (Test-Path -LiteralPath $scriptPath -PathType Leaf) "$scriptName must exist."

$tokens = $null
$parseErrors = $null
[System.Management.Automation.PSParser]::Tokenize((Get-Content -LiteralPath $scriptPath -Raw), [ref]$parseErrors) | Out-Null
Assert-SmokeZipTest (-not $parseErrors -or $parseErrors.Count -eq 0) "$scriptName has PowerShell parse errors."

$scriptSource = Get-Content -LiteralPath $scriptPath -Raw
foreach ($requiredText in @(
    'assets/scripts/exports/word-template-service.js',
    'assets/scripts/exports/export-service.js',
    'lens-docs-studio-local-dev-smoke.json',
    'local-dev-smoke',
    'officialRelease = $false',
    'word-export-templates',
    'Refusing to create local dev smoke ZIP from a dirty working tree',
    '^\.git(?:/|$)',
    '^node_modules(?:/|$)',
    '^test-results(?:/|$)',
    '^playwright-report(?:/|$)',
    '^artifacts/word-templates(?:/|$)',
    '\.docx$'
)) {
    Assert-SmokeZipTest ($scriptSource.Contains($requiredText)) "$scriptName is missing expected packaging guard text: $requiredText"
}

$readme = Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw
$toolGuide = Get-Content -LiteralPath (Join-Path $repoRoot 'docs\tool-guide.md') -Raw
$roadmap = Get-Content -LiteralPath (Join-Path $repoRoot 'docs\roadmap\lens-docs-studio-post-v010-dev4-backlog.md') -Raw
Assert-SmokeZipTest ($readme.Contains('scripts/windows/New-LocalDevSmokeZip.ps1')) 'README.md must document the local dev smoke ZIP generator.'
Assert-SmokeZipTest ($readme.Contains('LensDocsStudio-local-dev-<short-sha>-word-template-smoke.zip')) 'README.md must document the local dev smoke ZIP filename pattern.'
Assert-SmokeZipTest ($readme.Contains('local development smoke ZIP') -and $readme.Contains('not an official release')) 'README.md must state the local smoke ZIP is not an official release.'
Assert-SmokeZipTest ($toolGuide.Contains('Word template') -and $toolGuide.Contains('local')) 'Feature guide must keep Word template smoke context discoverable.'
Assert-SmokeZipTest ($roadmap.Contains('Phase 3BR') -and $roadmap.Contains('local dev smoke ZIP')) 'Roadmap/backlog must mention Phase 3BR local smoke ZIP work.'

$gitignore = Get-Content -LiteralPath (Join-Path $repoRoot '.gitignore') -Raw
foreach ($ignoredText in @(
    'artifacts/',
    'artifacts/windows/local-dev-smoke/',
    'artifacts/windows/local-dev-smoke-validation/',
    'artifacts/word-templates/'
)) {
    Assert-SmokeZipTest ($gitignore.Contains($ignoredText)) ".gitignore must exclude $ignoredText"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\local-dev-smoke-validation'
}

Write-SmokeZipTestLine 'Generating validation ZIP with -AllowDirty so source edits can be checked before commit...'
$output = & pwsh -NoLogo -NoProfile -File $scriptPath -OutputDirectory $OutputDirectory -AllowDirty 2>&1 | ForEach-Object { $_.ToString() }
Assert-SmokeZipTest ($LASTEXITCODE -eq 0) "$scriptName validation run failed: $($output -join [Environment]::NewLine)"

$zipPath = Get-ChildItem -LiteralPath $OutputDirectory -Filter 'LensDocsStudio-local-dev-*-word-template-smoke.zip' -File |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 |
    ForEach-Object { $_.FullName }
Assert-SmokeZipTest (-not [string]::IsNullOrWhiteSpace($zipPath)) 'Validation ZIP was not created.'

$entryNames = @(Expand-SmokeZipEntryNames -ZipPath $zipPath)
foreach ($requiredEntry in @(
    'index.html',
    'service-worker.js',
    'manifest.webmanifest',
    'assets/styles/app.css',
    'assets/scripts/main.js',
    'assets/scripts/exports/export-service.js',
    'assets/scripts/exports/word-template-service.js',
    'assets/vendor/manifest.json',
    'assets/vendor/fflate-0.8.2.browser.js',
    'docs/tool-guide.md',
    'lens-docs-studio-local-dev-smoke.json'
)) {
    Assert-SmokeZipTest ($entryNames -contains $requiredEntry) "Validation ZIP is missing required entry: $requiredEntry"
}

foreach ($entryName in $entryNames) {
    foreach ($blockedPattern in @(
        '^\.git(?:/|$)',
        '^node_modules(?:/|$)',
        '^test-results(?:/|$)',
        '^playwright-report(?:/|$)',
        '^artifacts(?:/|$)',
        '^artifacts/word-templates(?:/|$)',
        '\.docx$',
        '\.dotx$',
        'WebView2',
        'IndexedDB'
    )) {
        Assert-SmokeZipTest (-not [regex]::IsMatch($entryName, $blockedPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) "Validation ZIP contains blocked entry: $entryName"
    }
}

$extractRoot = Join-Path $OutputDirectory 'expanded-validation'
if (Test-Path -LiteralPath $extractRoot) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force
$metadataPath = Join-Path $extractRoot 'lens-docs-studio-local-dev-smoke.json'
Assert-SmokeZipTest (Test-Path -LiteralPath $metadataPath -PathType Leaf) 'Metadata file was not created in the ZIP.'
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
Assert-SmokeZipTest ($metadata.artifactType -eq 'local-dev-smoke') 'Metadata must identify local-dev-smoke artefact type.'
Assert-SmokeZipTest ($metadata.officialRelease -eq $false) 'Metadata must say this is not an official release.'
Assert-SmokeZipTest ($metadata.releaseArtifact -eq $false) 'Metadata must say this is not a release artefact.'
Assert-SmokeZipTest ($metadata.intendedSmokeTarget -eq 'word-export-templates') 'Metadata must identify the Word export templates smoke target.'
Assert-SmokeZipTest ($metadata.expectedImportantFiles -contains 'assets/scripts/exports/word-template-service.js') 'Metadata must list the Word template service as an expected important file.'
Assert-SmokeZipTest ($metadata.localOnlyNotice -match 'not an official release') 'Metadata local notice must clearly exclude official release status.'

Write-SmokeZipTestLine "PASS: validated local dev smoke ZIP contract and contents at $zipPath"
