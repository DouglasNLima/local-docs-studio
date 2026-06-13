$ErrorActionPreference = 'Stop'

$scriptPath = Join-Path $PSScriptRoot 'New-WindowsManualPackagedSanityChecklist.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("LensDocsStudio-ManualSanityTest-{0}" -f ([guid]::NewGuid().ToString('N')))
$outputDirectory = Join-Path $testRoot 'output'

function Write-ManualSanityTestLine {
    param([string]$Message)
    Write-Host "[windows-manual-sanity-test] $Message"
}

function Assert-ManualSanity {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Assert-TextContains {
    param(
        [string]$Text,
        [string]$Expected
    )

    Assert-ManualSanity ($Text.Contains($Expected)) "Checklist is missing required text: $Expected"
}

try {
    Assert-ManualSanity (Test-Path -LiteralPath $scriptPath -PathType Leaf) "Missing helper script: $scriptPath"

    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$null, [ref]$parseErrors) | Out-Null
    Assert-ManualSanity (-not $parseErrors -or $parseErrors.Count -eq 0) "Helper script has parse errors: $($parseErrors | Out-String)"

    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

    Write-ManualSanityTestLine 'Generating checklist into requested output directory...'
    $rawOutput = & pwsh -NoLogo -NoProfile -File $scriptPath `
        -OutputDirectory $outputDirectory `
        -Phase 'Phase 3BP test' `
        -ReleaseVersionUnderTest 'v0.1.0-dev.5 test candidate' `
        -PackagePath 'LensDocsStudio.Windows-test.zip' `
        -ExecutablePath 'LensDocsStudio.Windows.exe' `
        -SourceCommit 'TESTSOURCECOMMIT' `
        -PackageSha256 'TESTPACKAGESHA256' `
        -InstallerSha256 'TESTINSTALLERSHA256' 2>&1 | ForEach-Object { $_.ToString() }

    Assert-ManualSanity ($LASTEXITCODE -eq 0) "Helper script failed: $($rawOutput -join [Environment]::NewLine)"
    $jsonText = ($rawOutput | Where-Object { $_.TrimStart().StartsWith('{') -or $_.TrimStart().StartsWith('"') -or $_.TrimStart().StartsWith('}') } | Out-String).Trim()
    if ([string]::IsNullOrWhiteSpace($jsonText)) {
        $jsonText = ($rawOutput -join [Environment]::NewLine)
    }
    $result = $jsonText | ConvertFrom-Json

    Assert-ManualSanity (Test-Path -LiteralPath $result.checklistPath -PathType Leaf) "Checklist was not created: $($result.checklistPath)"
    Assert-ManualSanity ((Split-Path -Parent $result.checklistPath) -eq ([System.IO.Path]::GetFullPath($outputDirectory))) 'Checklist was not written into the requested output directory.'
    Assert-ManualSanity (Test-Path -LiteralPath $result.temporaryWorkspacePath -PathType Container) "Temporary workspace was not created: $($result.temporaryWorkspacePath)"
    Assert-ManualSanity ($result.temporaryWorkspaceBasename -match '^LensDocsStudio-ManualSanity-\d{8}-\d{6}$') "Temporary workspace basename is not neutral: $($result.temporaryWorkspaceBasename)"

    foreach ($relativeFixture in @(
        'README.md',
        'docs\overview.md',
        'diagrams\sample.mmd',
        'notes\conflict.md'
    )) {
        Assert-ManualSanity (Test-Path -LiteralPath (Join-Path $result.temporaryWorkspacePath $relativeFixture) -PathType Leaf) "Missing generated fixture: $relativeFixture"
    }

    $checklist = Get-Content -LiteralPath $result.checklistPath -Raw

    foreach ($requiredScenario in @(
        'Onboarding renders',
        'Diagnostics opens',
        'Retry bridge check passes',
        'Create support bundle preview-first flow',
        'Copy summary after preview',
        'Export local JSON after preview',
        'Open folder native picker visible',
        'Native picker select temporary workspace',
        'Native picker cancel',
        'Browser fallback inactive in packaged WebView2',
        'Clean external file change / changed-on-disk copy',
        'Dirty conflict keep app edits',
        'Dirty conflict use disk version',
        'Installer install/uninstall checks, where applicable',
        'Payload hygiene note for package/installer evidence',
        'Production readiness/go-live not claimed'
    )) {
        Assert-TextContains -Text $checklist -Expected $requiredScenario
    }

    foreach ($requiredGuardrail in @(
        'Use only the generated temporary workspace.',
        'Do not use private, customer, project, or personal documents.',
        'Do not paste document contents into evidence',
        'Do not include screenshots by default.',
        'Do not include tokens, secrets',
        'Record only safe operational observations.',
        'Mark unclear, unreachable, environment-limited, or partially observed scenarios as BLOCKED, not PASS.'
    )) {
        Assert-TextContains -Text $checklist -Expected $requiredGuardrail
    }

    foreach ($requiredField in @(
        'Release/version under test',
        'Package path',
        'Executable path',
        'Source commit',
        'Package SHA256',
        'Installer SHA256',
        'Windows version',
        'Runtime prerequisites',
        'Temporary workspace'
    )) {
        Assert-TextContains -Text $checklist -Expected $requiredField
    }

    foreach ($unsafePattern in @(
        'localStorage',
        'command-line workspace',
        'hidden bridge',
        'test-only UI route',
        'paste private document',
        'attach screenshots',
        'production readiness approved',
        'go-live approved'
    )) {
        Assert-ManualSanity (-not [regex]::IsMatch($checklist, [regex]::Escape($unsafePattern), [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) "Checklist includes unsafe or overclaiming wording: $unsafePattern"
    }

    Assert-ManualSanity ($checklist -notmatch [regex]::Escape($result.temporaryWorkspacePath)) 'Checklist recorded the full temporary workspace path.'
    Assert-ManualSanity ($checklist -notmatch 'C:\\Users\\') 'Checklist contains a private user path pattern.'
    Assert-ManualSanity ($checklist -match 'does not claim production readiness') 'Checklist must explicitly avoid production readiness claims.'
    Assert-ManualSanity ($checklist -match 'Existing prerelease assets are not changed') 'Checklist must state existing prerelease assets are unchanged.'

    Write-ManualSanityTestLine 'PASS: manual packaged sanity checklist helper generated safe checklist output.'
    exit 0
}
catch {
    Write-ManualSanityTestLine "FAIL: $($_.Exception.Message)"
    exit 1
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
