param(
    [string]$Version = '0.1.0-dev',
    [string]$Tag = 'v0.1.0-dev',
    [string]$TargetCommit = 'HEAD',
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$NoBuild,
    [switch]$SkipPrepare,
    [switch]$AllowDirtyWorktree,
    [switch]$AllowNonDevelopBranch
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$expectedRepo = 'DouglasNLima/local-docs-studio'
$safeVersion = $Version -replace '[^\w.-]', '-'
$releaseRoot = Join-Path $repoRoot (Join-Path 'artifacts\releases' $Tag)
$packageFileName = "LensDocsStudio.Windows-$safeVersion.zip"
$checksumFileName = "$packageFileName.sha256"
$rcReportFileName = "LensDocsStudio.Windows-$safeVersion-rc-report.md"
$releaseNotesFileName = "LensDocsStudio.Windows-$safeVersion-release-notes.md"
$commandFileName = "LensDocsStudio.Windows-$safeVersion-gh-release-command.ps1.txt"
$reviewReportFileName = "LensDocsStudio.Windows-$safeVersion-release-review.md"

$zipPath = Join-Path $releaseRoot $packageFileName
$checksumPath = Join-Path $releaseRoot $checksumFileName
$rcReportPath = Join-Path $releaseRoot $rcReportFileName
$releaseNotesPath = Join-Path $releaseRoot $releaseNotesFileName
$commandPath = Join-Path $releaseRoot $commandFileName
$reviewReportPath = Join-Path $releaseRoot $reviewReportFileName

function Write-GateLine {
    param([string]$Message)
    Write-Host "[windows-github-release-dry-run] $Message"
}

function ConvertTo-RelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $relativePath = [System.IO.Path]::GetRelativePath($repoRoot, $fullPath)
    return ($relativePath -replace '\\', '/')
}

function Get-GitText {
    param([string[]]$Arguments)

    $value = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return (($value | Out-String).Trim())
}

function Test-TextIncludesAll {
    param(
        [string]$Text,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Text -notmatch $pattern) {
            return $false
        }
    }

    return $true
}

function Add-Check {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $script:checks += [pscustomobject]@{
        name = $Name
        passed = $Passed
        detail = $Detail
    }
}

function Add-FailureCheck {
    param(
        [string]$Name,
        [string]$Detail
    )

    Add-Check -Name $Name -Passed $false -Detail $Detail
}

function ConvertTo-MarkdownTable {
    param([object[]]$Rows)

    $lines = @('| Check | Result | Detail |', '| --- | --- | --- |')
    foreach ($row in $Rows) {
        $detail = ([string]$row.detail) -replace '\|', '\|' -replace "`r?`n", '<br>'
        $lines += "| $($row.name) | $(if ($row.passed) { 'PASS' } else { 'FAIL' }) | $detail |"
    }

    return ($lines -join [Environment]::NewLine)
}

function Write-ReviewReport {
    param(
        [string]$Verdict,
        [string]$ResolvedTargetCommit,
        [string]$ZipHash,
        [string]$CommandText,
        [string]$PrepareOutputText,
        [object[]]$Checks
    )

    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

    $tick = [char]96
    $fence = "$tick$tick$tick"
    $relativeZipPath = ConvertTo-RelativePath $zipPath
    $relativeRcReportPath = ConvertTo-RelativePath $rcReportPath
    $relativeReleaseNotesPath = ConvertTo-RelativePath $releaseNotesPath
    $published = if ($PrepareOutputText -match 'DRY RUN: no GitHub release was created and no files were uploaded') {
        'No. The preparation run reported dry-run mode, so no GitHub Release was created and no files were uploaded.'
    }
    else {
        'No publish action was requested by this review gate. Dry-run confirmation was not available.'
    }

    $reportLines = @(
        '# Lens Docs Studio Windows GitHub Release Dry-run Review',
        '',
        'Product: Lens Docs Studio',
        "Version: $tick$Version$tick",
        "Tag: $tick$Tag$tick",
        "Target commit: $tick$ResolvedTargetCommit$tick",
        "Package ZIP: $tick$relativeZipPath$tick",
        "ZIP SHA256: $tick$ZipHash$tick",
        "RC report path: $tick$relativeRcReportPath$tick",
        "Release notes path: $tick$relativeReleaseNotesPath$tick",
        '',
        '## Generated gh command',
        '',
        ($fence + 'powershell'),
        $CommandText,
        $fence,
        '',
        '## Validation verdict',
        '',
        $Verdict,
        '',
        '## Validation checks',
        '',
        (ConvertTo-MarkdownTable -Rows $Checks),
        '',
        '## Manual publication checklist',
        '',
        '- Review the generated release notes for product name, version, target commit, prerequisites, known limitations, and checksum guidance.',
        "- Confirm the ZIP SHA256 in the ${tick}.sha256${tick} file matches ${tick}Get-FileHash -Algorithm SHA256${tick}.",
        '- Review the RC report and confirm the release-candidate result is PASS.',
        "- Confirm the generated command targets $tick$expectedRepo$tick, tag $tick$Tag$tick, and commit $tick$ResolvedTargetCommit$tick.",
        "- Confirm the command includes $tick--draft$tick and $tick--prerelease$tick.",
        "- Only then run the generated ${tick}gh release create${tick} command or intentionally rerun ${tick}Prepare-WindowsGitHubRelease.ps1 -Publish${tick}.",
        '',
        '## Known limitations',
        '',
        '- The ZIP is unsigned.',
        '- No MSIX package is included.',
        '- No classic installer is included.',
        '- No auto-update is included.',
        '- No WebView2 bootstrapper is included.',
        '- No machine-wide file associations are included.',
        '',
        '## Publication state',
        '',
        $published
    )

    $report = $reportLines -join [Environment]::NewLine

    $report | Set-Content -LiteralPath $reviewReportPath -Encoding utf8
}

Set-Location $repoRoot

$checks = @()
$prepareOutputText = ''
$ghCommand = ''
$zipHash = ''
$verdict = 'BLOCKED'
$resolvedTargetCommit = Get-GitText -Arguments @('rev-parse', $TargetCommit)
$headCommit = Get-GitText -Arguments @('rev-parse', 'HEAD')
$branch = Get-GitText -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
$tagBefore = Get-GitText -Arguments @('tag', '--list', $Tag)

try {
    Add-Check -Name 'Branch is develop' -Passed ($branch -eq 'develop' -or $AllowNonDevelopBranch) -Detail "Current branch: $branch"
    Add-Check -Name 'Target commit matches HEAD' -Passed ($resolvedTargetCommit -eq $headCommit) -Detail "Target: $resolvedTargetCommit; HEAD: $headCommit"

    if (-not $SkipPrepare) {
        Write-GateLine 'Running release preparation in dry-run mode.'
        $prepareArgs = @(
            '-NoLogo',
            '-NoProfile',
            '-File',
            (Join-Path $PSScriptRoot 'Prepare-WindowsGitHubRelease.ps1'),
            '-Version',
            $Version,
            '-Tag',
            $Tag,
            '-TargetCommit',
            $TargetCommit,
            '-Configuration',
            $Configuration,
            '-RuntimeIdentifier',
            $RuntimeIdentifier,
            '-DryRun'
        )

        if ($NoBuild) {
            $prepareArgs += '-NoBuild'
        }

        if ($AllowDirtyWorktree) {
            $prepareArgs += '-AllowDirtyWorktree'
        }

        if ($AllowNonDevelopBranch) {
            $prepareArgs += '-AllowNonDevelopBranch'
        }

        $prepareOutput = & pwsh @prepareArgs 2>&1 | ForEach-Object { $_.ToString() }
        $prepareExitCode = if ($null -ne $LASTEXITCODE) { [int]$LASTEXITCODE } else { 0 }
        $prepareOutputText = ($prepareOutput -join [Environment]::NewLine).Trim()
        Add-Check -Name 'Preparation dry run completed' -Passed ($prepareExitCode -eq 0) -Detail "Exit code: $prepareExitCode"
    }
    else {
        Write-GateLine 'SkipPrepare supplied; validating the existing release folder.'
        $prepareOutputText = 'Preparation was skipped by operator request.'
        Add-Check -Name 'Preparation dry run completed' -Passed $true -Detail 'Skipped; existing artefacts are being reviewed.'
    }

    Add-Check -Name 'No publish action was performed' -Passed ($prepareOutputText -match 'DRY RUN: no GitHub release was created and no files were uploaded' -or $SkipPrepare) -Detail 'The gate does not pass -Publish and expects dry-run confirmation.'

    $tagAfter = Get-GitText -Arguments @('tag', '--list', $Tag)
    Add-Check -Name 'No local tag was created' -Passed ($tagBefore -eq $tagAfter) -Detail $(if ([string]::IsNullOrWhiteSpace($tagAfter)) { "Local tag $Tag is absent." } else { "Local tag $Tag existed before and after the dry run." })

    Add-Check -Name 'Artefact folder exists' -Passed (Test-Path -LiteralPath $releaseRoot -PathType Container) -Detail (ConvertTo-RelativePath $releaseRoot)
    Add-Check -Name 'ZIP exists' -Passed (Test-Path -LiteralPath $zipPath -PathType Leaf) -Detail (ConvertTo-RelativePath $zipPath)
    Add-Check -Name 'ZIP SHA256 file exists' -Passed (Test-Path -LiteralPath $checksumPath -PathType Leaf) -Detail (ConvertTo-RelativePath $checksumPath)
    Add-Check -Name 'RC report exists' -Passed (Test-Path -LiteralPath $rcReportPath -PathType Leaf) -Detail (ConvertTo-RelativePath $rcReportPath)
    Add-Check -Name 'Release notes exist' -Passed (Test-Path -LiteralPath $releaseNotesPath -PathType Leaf) -Detail (ConvertTo-RelativePath $releaseNotesPath)
    Add-Check -Name 'GitHub CLI command file exists' -Passed (Test-Path -LiteralPath $commandPath -PathType Leaf) -Detail (ConvertTo-RelativePath $commandPath)

    if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
        $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    }

    if (Test-Path -LiteralPath $checksumPath -PathType Leaf) {
        $checksumText = (Get-Content -LiteralPath $checksumPath -Raw).Trim()
        $checksumMatch = [regex]::Match($checksumText, '^([A-Fa-f0-9]{64})\s+(.+)$')
        Add-Check -Name 'ZIP SHA256 file format is valid' -Passed $checksumMatch.Success -Detail $checksumText
        if ($checksumMatch.Success) {
            Add-Check -Name 'ZIP SHA256 file names the ZIP' -Passed ($checksumMatch.Groups[2].Value -eq $packageFileName) -Detail "Expected $packageFileName; found $($checksumMatch.Groups[2].Value)."
            Add-Check -Name 'ZIP SHA256 file content matches calculated SHA256' -Passed ($checksumMatch.Groups[1].Value.ToUpperInvariant() -eq $zipHash.ToUpperInvariant()) -Detail "Calculated: $zipHash"
        }
    }
    else {
        Add-FailureCheck -Name 'ZIP SHA256 file format is valid' -Detail 'Checksum file is missing.'
        Add-FailureCheck -Name 'ZIP SHA256 file names the ZIP' -Detail 'Checksum file is missing.'
        Add-FailureCheck -Name 'ZIP SHA256 file content matches calculated SHA256' -Detail 'Checksum file is missing.'
    }

    if (Test-Path -LiteralPath $releaseNotesPath -PathType Leaf) {
        $releaseNotes = Get-Content -LiteralPath $releaseNotesPath -Raw
        Add-Check -Name 'Release notes mention package prerequisites' -Passed (Test-TextIncludesAll -Text $releaseNotes -Patterns @('(?im)^## Prerequisites', '\.NET 8 Desktop Runtime', 'Windows App SDK Runtime', 'WebView2 Runtime')) -Detail 'Prerequisites section and runtime references are expected.'
        Add-Check -Name 'Release notes mention unsupported or non-claimed features' -Passed (Test-TextIncludesAll -Text $releaseNotes -Patterns @('(?im)^## Known Limitations', 'unsigned', 'No MSIX', 'No classic installer', 'No auto-update', 'No WebView2 bootstrapper')) -Detail 'Known limitations must make deferred features explicit.'
        Add-Check -Name 'Release notes include SHA256 verification guidance' -Passed (Test-TextIncludesAll -Text $releaseNotes -Patterns @('(?im)^## Verification', 'Get-FileHash\s+-Algorithm\s+SHA256', [regex]::Escape($zipHash))) -Detail 'Verification section must include the PowerShell hash command and expected hash.'
    }
    else {
        Add-FailureCheck -Name 'Release notes mention package prerequisites' -Detail 'Release notes file is missing.'
        Add-FailureCheck -Name 'Release notes mention unsupported or non-claimed features' -Detail 'Release notes file is missing.'
        Add-FailureCheck -Name 'Release notes include SHA256 verification guidance' -Detail 'Release notes file is missing.'
    }

    if (Test-Path -LiteralPath $commandPath -PathType Leaf) {
        $ghCommand = (Get-Content -LiteralPath $commandPath -Raw).Trim()
        $normalisedCommand = $ghCommand -replace '\\', '/'
        $expectedZip = ConvertTo-RelativePath $zipPath
        $expectedChecksum = ConvertTo-RelativePath $checksumPath
        $expectedRcReport = ConvertTo-RelativePath $rcReportPath
        $expectedReleaseNotes = ConvertTo-RelativePath $releaseNotesPath

        Add-Check -Name 'Generated gh command uses expected repo' -Passed ($normalisedCommand -match '--repo\s+DouglasNLima/local-docs-studio') -Detail $expectedRepo
        Add-Check -Name 'Generated gh command uses expected tag' -Passed ($normalisedCommand -match "gh release create\s+$([regex]::Escape($Tag))\b") -Detail $Tag
        Add-Check -Name 'Generated gh command uses expected target commit' -Passed ($normalisedCommand -match "--target\s+$([regex]::Escape($resolvedTargetCommit))\b") -Detail $resolvedTargetCommit
        Add-Check -Name 'Generated gh command is draft' -Passed ($normalisedCommand -match '\s--draft\b') -Detail '--draft'
        Add-Check -Name 'Generated gh command is prerelease' -Passed ($normalisedCommand -match '\s--prerelease\b') -Detail '--prerelease'
        Add-Check -Name 'Generated gh command includes ZIP asset' -Passed ($normalisedCommand.Contains($expectedZip)) -Detail $expectedZip
        Add-Check -Name 'Generated gh command includes SHA256 asset' -Passed ($normalisedCommand.Contains($expectedChecksum)) -Detail $expectedChecksum
        Add-Check -Name 'Generated gh command includes RC report asset' -Passed ($normalisedCommand.Contains($expectedRcReport)) -Detail $expectedRcReport
        Add-Check -Name 'Generated gh command uses notes file' -Passed ($normalisedCommand.Contains("--notes-file $expectedReleaseNotes")) -Detail $expectedReleaseNotes
    }
    else {
        foreach ($missingCommandCheck in @(
            'Generated gh command uses expected repo',
            'Generated gh command uses expected tag',
            'Generated gh command uses expected target commit',
            'Generated gh command is draft',
            'Generated gh command is prerelease',
            'Generated gh command includes ZIP asset',
            'Generated gh command includes SHA256 asset',
            'Generated gh command includes RC report asset',
            'Generated gh command uses notes file'
        )) {
            Add-FailureCheck -Name $missingCommandCheck -Detail 'GitHub CLI command file is missing.'
        }
    }

    $failedChecks = @($checks | Where-Object { -not $_.passed })
    $verdict = if ($failedChecks.Count -eq 0) { 'CERTIFIED_DRAFT_RELEASE_READY' } else { 'BLOCKED' }
}
catch {
    Add-FailureCheck -Name 'Dry-run review gate completed' -Detail $_.Exception.Message
    $verdict = 'BLOCKED'
}
finally {
    if ([string]::IsNullOrWhiteSpace($zipHash) -and (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
        $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
    }

    Write-ReviewReport -Verdict $verdict -ResolvedTargetCommit $resolvedTargetCommit -ZipHash $zipHash -CommandText $ghCommand -PrepareOutputText $prepareOutputText -Checks $checks
    Write-GateLine "Review report: $(ConvertTo-RelativePath $reviewReportPath)"
    Write-GateLine "Verdict: $verdict"
    Write-Host $verdict
}

if ($verdict -eq 'BLOCKED') {
    exit 1
}

exit 0
