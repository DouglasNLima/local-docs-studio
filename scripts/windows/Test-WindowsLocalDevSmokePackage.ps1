param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$scriptPath = Join-Path $PSScriptRoot 'New-WindowsLocalDevSmokePackage.ps1'
$scriptName = 'New-WindowsLocalDevSmokePackage.ps1'

function Write-WindowsSmokePackageTestLine {
    param([string]$Message)
    Write-Host "[windows-local-dev-smoke-package-test] $Message"
}

function Assert-WindowsSmokePackageTest {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Expand-ZipEntryNames {
    param([string]$ZipPath)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        return @($zip.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
    } finally {
        $zip.Dispose()
    }
}

Assert-WindowsSmokePackageTest (Test-Path -LiteralPath $scriptPath -PathType Leaf) "$scriptName must exist."

$tokens = $null
$parseErrors = $null
[System.Management.Automation.PSParser]::Tokenize((Get-Content -LiteralPath $scriptPath -Raw), [ref]$parseErrors) | Out-Null
Assert-WindowsSmokePackageTest (-not $parseErrors -or $parseErrors.Count -eq 0) "$scriptName has PowerShell parse errors."

$scriptSource = Get-Content -LiteralPath $scriptPath -Raw
foreach ($requiredText in @(
    'LensDocsStudio.Windows.exe',
    'StaticApp/index.html',
    'StaticApp/service-worker.js',
    'StaticApp/manifest.webmanifest',
    'StaticApp/assets/scripts/exports/export-service.js',
    'StaticApp/assets/scripts/exports/word-template-service.js',
    'lens-docs-studio-windows-local-dev-smoke.json',
    'windows-local-dev-smoke',
    'officialRelease = $false',
    'installer = $false',
    'word-export-templates',
    'packageKind = ''windows''',
    'Refusing to create Windows local dev smoke ZIP from a dirty working tree',
    '^\.git(?:/|$)',
    '^node_modules(?:/|$)',
    '^test-results(?:/|$)',
    '^playwright-report(?:/|$)',
    '^artifacts/word-templates(?:/|$)',
    '\.docx$',
    'IndexedDB',
    'corporate/user-provided Word templates'
)) {
    Assert-WindowsSmokePackageTest ($scriptSource.Contains($requiredText)) "$scriptName is missing expected packaging guard text: $requiredText"
}

$readme = Get-Content -LiteralPath (Join-Path $repoRoot 'README.md') -Raw
$toolGuide = Get-Content -LiteralPath (Join-Path $repoRoot 'docs\tool-guide.md') -Raw
$roadmap = Get-Content -LiteralPath (Join-Path $repoRoot 'docs\roadmap\lens-docs-studio-post-v010-dev4-backlog.md') -Raw
Assert-WindowsSmokePackageTest ($readme.Contains('scripts/windows/New-WindowsLocalDevSmokePackage.ps1')) 'README.md must document the Windows local dev smoke package generator.'
Assert-WindowsSmokePackageTest ($readme.Contains('LensDocsStudio.Windows-local-dev-<short-sha>-word-template-smoke.zip')) 'README.md must document the Windows local dev smoke ZIP filename pattern.'
Assert-WindowsSmokePackageTest ($readme.Contains('local Windows development smoke ZIP') -and $readme.Contains('not an official release')) 'README.md must state the Windows local smoke ZIP is not an official release.'
Assert-WindowsSmokePackageTest ($toolGuide.Contains('Windows local development smoke ZIP') -and $toolGuide.Contains('Word template')) 'Feature guide must document the Windows Word template smoke package context.'
Assert-WindowsSmokePackageTest ($roadmap.Contains('Phase 3BS') -and $roadmap.Contains('Windows local dev smoke ZIP')) 'Roadmap/backlog must mention Phase 3BS Windows local smoke package work.'

$gitignore = Get-Content -LiteralPath (Join-Path $repoRoot '.gitignore') -Raw
foreach ($ignoredText in @(
    'artifacts/',
    'artifacts/windows/local-dev-package-smoke/',
    'artifacts/windows/local-dev-package-smoke-validation/',
    'artifacts/word-templates/'
)) {
    Assert-WindowsSmokePackageTest ($gitignore.Contains($ignoredText)) ".gitignore must exclude $ignoredText"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot 'artifacts\windows\local-dev-package-smoke-validation'
}

Write-WindowsSmokePackageTestLine 'Generating validation Windows ZIP with -AllowDirty so source edits can be checked before commit...'
$output = & pwsh -NoLogo -NoProfile -File $scriptPath -OutputDirectory $OutputDirectory -AllowDirty 2>&1 | ForEach-Object { $_.ToString() }
Assert-WindowsSmokePackageTest ($LASTEXITCODE -eq 0) "$scriptName validation run failed: $($output -join [Environment]::NewLine)"

$zipPath = Get-ChildItem -LiteralPath $OutputDirectory -Filter 'LensDocsStudio.Windows-local-dev-*-word-template-smoke.zip' -File |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 |
    ForEach-Object { $_.FullName }
Assert-WindowsSmokePackageTest (-not [string]::IsNullOrWhiteSpace($zipPath)) 'Validation Windows ZIP was not created.'

$entryNames = @(Expand-ZipEntryNames -ZipPath $zipPath)
foreach ($requiredEntry in @(
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
)) {
    Assert-WindowsSmokePackageTest ($entryNames -contains $requiredEntry) "Validation Windows ZIP is missing required entry: $requiredEntry"
}

foreach ($entryName in $entryNames) {
    foreach ($blockedPattern in @(
        '^\.git(?:/|$)',
        '^node_modules(?:/|$)',
        '^test-results(?:/|$)',
        '^playwright-report(?:/|$)',
        '^artifacts(?:/|$)',
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
    )) {
        Assert-WindowsSmokePackageTest (-not [regex]::IsMatch($entryName, $blockedPattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) "Validation Windows ZIP contains blocked entry: $entryName"
    }
}

$extractRoot = Join-Path $OutputDirectory 'expanded-validation'
if (Test-Path -LiteralPath $extractRoot) {
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}
Expand-Archive -LiteralPath $zipPath -DestinationPath $extractRoot -Force
$metadataPath = Join-Path $extractRoot 'lens-docs-studio-windows-local-dev-smoke.json'
Assert-WindowsSmokePackageTest (Test-Path -LiteralPath $metadataPath -PathType Leaf) 'Metadata file was not created in the Windows ZIP.'
$metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
Assert-WindowsSmokePackageTest ($metadata.artifactType -eq 'windows-local-dev-smoke') 'Metadata must identify windows-local-dev-smoke artefact type.'
Assert-WindowsSmokePackageTest ($metadata.officialRelease -eq $false) 'Metadata must say this is not an official release.'
Assert-WindowsSmokePackageTest ($metadata.releaseArtifact -eq $false) 'Metadata must say this is not a release artefact.'
Assert-WindowsSmokePackageTest ($metadata.installer -eq $false) 'Metadata must say this is not an installer.'
Assert-WindowsSmokePackageTest ($metadata.notOfficialRelease -eq $true) 'Metadata must include an explicit not-official-release flag.'
Assert-WindowsSmokePackageTest ($metadata.notInstaller -eq $true) 'Metadata must include an explicit not-installer flag.'
Assert-WindowsSmokePackageTest ($metadata.packageKind -eq 'windows') 'Metadata must identify Windows package kind.'
Assert-WindowsSmokePackageTest ($metadata.intendedSmokeTarget -eq 'word-export-templates') 'Metadata must identify the Word export templates smoke target.'
Assert-WindowsSmokePackageTest ($metadata.expectedImportantFiles -contains 'StaticApp/assets/scripts/exports/word-template-service.js') 'Metadata must list the Word template service as an expected important file.'
Assert-WindowsSmokePackageTest ($metadata.localOnlyNotice -match 'not an official release') 'Metadata local notice must clearly exclude official release status.'

Write-WindowsSmokePackageTestLine "PASS: validated Windows local dev smoke ZIP contract and contents at $zipPath"
