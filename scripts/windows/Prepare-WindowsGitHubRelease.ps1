param(
    [string]$Version = '0.1.0-dev',
    [string]$Tag = 'v0.1.0-dev',
    [string]$TargetCommit = 'HEAD',
    [string]$Configuration = 'Release',
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$Draft = $true,
    [switch]$Prerelease = $true,
    [switch]$DryRun,
    [switch]$Publish,
    [switch]$NoBuild,
    [switch]$NoBrowserTests,
    [switch]$AllowDirtyWorktree,
    [switch]$AllowNonDevelopBranch
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$templatePath = Join-Path $repoRoot 'docs\release\templates\github-release-notes.md'
$releaseRoot = Join-Path $repoRoot (Join-Path 'artifacts\releases' $Tag)
$safeVersion = $Version -replace '[^\w.-]', '-'
$packageFileName = "LensDocsStudio.Windows-$safeVersion.zip"
$checksumFileName = "$packageFileName.sha256"
$rcReportFileName = "LensDocsStudio.Windows-$safeVersion-rc-report.md"
$releaseNotesFileName = "LensDocsStudio.Windows-$safeVersion-release-notes.md"
$releaseZipPath = Join-Path $releaseRoot $packageFileName
$checksumPath = Join-Path $releaseRoot $checksumFileName
$rcReportPath = Join-Path $releaseRoot $rcReportFileName
$releaseNotesPath = Join-Path $releaseRoot $releaseNotesFileName

function Write-ReleaseLine {
    param([string]$Message)
    Write-Host "[windows-github-release] $Message"
}

function Assert-Release {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-GitText {
    param([string[]]$Arguments)

    $value = & git @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return (($value | Out-String).Trim())
}

function ConvertTo-RelativePath {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $relativePath = [System.IO.Path]::GetRelativePath($repoRoot, $fullPath)
    return ($relativePath -replace '\\', '/')
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

function Get-MatchingRcMetadata {
    param(
        [string]$CommitSha,
        [string]$PackageVersion
    )

    $candidateRoots = @(
        (Join-Path $repoRoot 'artifacts\windows\release-candidates'),
        $releaseRoot
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }

    $jsonFiles = foreach ($candidateRoot in $candidateRoots) {
        Get-ChildItem -LiteralPath $candidateRoot -Filter '*.json' -File -ErrorAction SilentlyContinue
    }

    foreach ($jsonFile in @($jsonFiles | Sort-Object LastWriteTimeUtc -Descending)) {
        try {
            $metadata = Get-Content -LiteralPath $jsonFile.FullName -Raw | ConvertFrom-Json
        }
        catch {
            continue
        }

        if ($metadata.packageVersion -eq $PackageVersion -and
            $metadata.commitSha -eq $CommitSha -and
            $metadata.validationResult -eq 'PASS' -and
            -not [string]::IsNullOrWhiteSpace($metadata.zipPath) -and
            (Test-Path -LiteralPath $metadata.zipPath -PathType Leaf)) {
            return [pscustomobject]@{
                JsonPath = $jsonFile.FullName
                Metadata = $metadata
            }
        }
    }

    return $null
}

function Format-PowerShellArgument {
    param([string]$Value)

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    return '"' + ($Value -replace '"', '\"') + '"'
}

function New-GhReleaseCommand {
    param(
        [string]$ResolvedTag,
        [string]$ResolvedTargetCommit,
        [string]$Title,
        [string[]]$AssetPaths,
        [string]$NotesPath
    )

    $lines = @("gh release create $ResolvedTag ``")
    foreach ($assetPath in $AssetPaths) {
        $lines += "  $(Format-PowerShellArgument (ConvertTo-RelativePath $assetPath)) ``"
    }

    $lines += '  --repo DouglasNLima/local-docs-studio `'
    $lines += "  --target $ResolvedTargetCommit ``"
    $lines += "  --title $(Format-PowerShellArgument $Title) ``"
    $lines += "  --notes-file $(Format-PowerShellArgument (ConvertTo-RelativePath $NotesPath)) ``"

    if ($Draft) {
        $lines += '  --draft `'
    }

    if ($Prerelease) {
        $lines += '  --prerelease'
    }
    else {
        $lines[$lines.Count - 1] = $lines[$lines.Count - 1].TrimEnd(' ', '`')
    }

    return ($lines -join [Environment]::NewLine)
}

Set-Location $repoRoot

$branch = Get-GitText -Arguments @('rev-parse', '--abbrev-ref', 'HEAD')
$headCommit = Get-GitText -Arguments @('rev-parse', 'HEAD')
$resolvedTargetCommit = Get-GitText -Arguments @('rev-parse', $TargetCommit)
$worktreeStatus = Get-GitText -Arguments @('status', '--short')

Assert-Release ($branch -eq 'develop' -or $AllowNonDevelopBranch) "Release preparation must run on develop. Current branch: $branch. Use -AllowNonDevelopBranch only for an intentional local rehearsal."
Assert-Release ([string]::IsNullOrWhiteSpace($worktreeStatus) -or $AllowDirtyWorktree) 'Release preparation requires a clean worktree. Commit, stash, or use -AllowDirtyWorktree only for an intentional local rehearsal.'
Assert-Release ($resolvedTargetCommit -eq $headCommit) "TargetCommit resolves to $resolvedTargetCommit, but the checked-out HEAD is $headCommit. Check out the target commit before preparing the release."
Assert-Release (Test-Path -LiteralPath $templatePath -PathType Leaf) "Release notes template was not found: $templatePath"

$effectiveDryRun = (-not $Publish) -or $DryRun
$releaseDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

if ($NoBuild) {
    Write-ReleaseLine 'NoBuild supplied; reusing a matching successful RC metadata record.'
    $matchingRc = Get-MatchingRcMetadata -CommitSha $resolvedTargetCommit -PackageVersion $Version
    Assert-Release ($null -ne $matchingRc) "No successful RC metadata was found for version $Version at commit $resolvedTargetCommit."
}
else {
    Write-ReleaseLine 'Building and certifying the Windows folder/ZIP package.'
    & (Join-Path $PSScriptRoot 'Test-WindowsPackageReleaseCandidate.ps1') `
        -Configuration $Configuration `
        -Version $Version `
        -RuntimeIdentifier $RuntimeIdentifier `
        -ReportPath $rcReportPath
    if ($LASTEXITCODE -ne 0) {
        throw "Windows package RC certification failed with exit code $LASTEXITCODE."
    }

    $metadataPath = [System.IO.Path]::ChangeExtension($rcReportPath, '.json')
    Assert-Release (Test-Path -LiteralPath $metadataPath -PathType Leaf) "RC metadata JSON was not created: $metadataPath"
    $matchingRc = [pscustomobject]@{
        JsonPath = $metadataPath
        Metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    }
}

$rcMetadata = $matchingRc.Metadata
Assert-Release ($rcMetadata.packageVersion -eq $Version) "RC package version mismatch. Expected $Version; found $($rcMetadata.packageVersion)."
Assert-Release ($rcMetadata.commitSha -eq $resolvedTargetCommit) "RC commit mismatch. Expected $resolvedTargetCommit; found $($rcMetadata.commitSha)."
Assert-Release ($rcMetadata.validationResult -eq 'PASS') "RC validation did not pass. Result: $($rcMetadata.validationResult)."
Assert-Release (Test-Path -LiteralPath $rcMetadata.zipPath -PathType Leaf) "Certified ZIP was not found: $($rcMetadata.zipPath)"

if (-not (Test-Path -LiteralPath $rcReportPath -PathType Leaf)) {
    $sourceReport = [System.IO.Path]::ChangeExtension($matchingRc.JsonPath, '.md')
    Assert-Release (Test-Path -LiteralPath $sourceReport -PathType Leaf) "Matching RC report was not found: $sourceReport"
    Copy-Item -LiteralPath $sourceReport -Destination $rcReportPath -Force
}

Copy-Item -LiteralPath $rcMetadata.zipPath -Destination $releaseZipPath -Force
$zipHash = (Get-FileHash -LiteralPath $releaseZipPath -Algorithm SHA256).Hash
Assert-Release (-not [string]::IsNullOrWhiteSpace($zipHash)) "Could not compute SHA256 for $releaseZipPath."

"$zipHash  $packageFileName" | Set-Content -LiteralPath $checksumPath -Encoding ascii

$projectPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows\LensDocsStudio.Windows.csproj'
$projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
$targetPlatformMinVersion = ([string]$projectXml.Project.PropertyGroup.TargetPlatformMinVersion).Trim()
$windowsAppSdkVersion = Get-ProjectPackageVersion -Project $projectXml -PackageName 'Microsoft.WindowsAppSDK'
$webView2PackageVersion = Get-ProjectPackageVersion -Project $projectXml -PackageName 'Microsoft.Web.WebView2'

$verificationCommand = "Get-FileHash -Algorithm SHA256 -LiteralPath `"$packageFileName`""
$template = Get-Content -LiteralPath $templatePath -Raw
$releaseNotes = $template.
    Replace('{{PRODUCT_NAME}}', 'Lens Docs Studio').
    Replace('{{VERSION}}', $Version).
    Replace('{{TAG}}', $Tag).
    Replace('{{COMMIT_SHA}}', $resolvedTargetCommit).
    Replace('{{DATE}}', $releaseDate).
    Replace('{{PACKAGE_TYPE}}', 'Windows folder/ZIP').
    Replace('{{DOWNLOAD_ARTEFACT}}', $packageFileName).
    Replace('{{SHA256}}', $zipHash).
    Replace('{{RC_REPORT}}', $rcReportFileName).
    Replace('{{WINDOWS_VERSION}}', "Windows 10 version 2004 / build $targetPlatformMinVersion or newer").
    Replace('{{WINDOWS_APP_SDK_VERSION}}', $windowsAppSdkVersion).
    Replace('{{WEBVIEW2_PACKAGE_VERSION}}', $webView2PackageVersion).
    Replace('{{VERIFICATION_COMMAND}}', $verificationCommand)

$releaseNotes | Set-Content -LiteralPath $releaseNotesPath -Encoding utf8

$assetPaths = @($releaseZipPath, $checksumPath, $rcReportPath)
$releaseTitle = "Lens Docs Studio $Tag"
$ghCommand = New-GhReleaseCommand -ResolvedTag $Tag -ResolvedTargetCommit $resolvedTargetCommit -Title $releaseTitle -AssetPaths $assetPaths -NotesPath $releaseNotesPath

$commandPath = Join-Path $releaseRoot "LensDocsStudio.Windows-$safeVersion-gh-release-command.ps1.txt"
$ghCommand | Set-Content -LiteralPath $commandPath -Encoding utf8

Write-ReleaseLine "Release folder: $(ConvertTo-RelativePath $releaseRoot)"
Write-ReleaseLine "ZIP: $(ConvertTo-RelativePath $releaseZipPath)"
Write-ReleaseLine "SHA256: $zipHash"
Write-ReleaseLine "Checksum file: $(ConvertTo-RelativePath $checksumPath)"
Write-ReleaseLine "RC report: $(ConvertTo-RelativePath $rcReportPath)"
Write-ReleaseLine "Release notes: $(ConvertTo-RelativePath $releaseNotesPath)"
Write-ReleaseLine "GitHub CLI command file: $(ConvertTo-RelativePath $commandPath)"

if ($NoBrowserTests) {
    Write-ReleaseLine 'NoBrowserTests supplied; browser smoke validation is left to the caller for this release rehearsal.'
}

Write-Host ''
Write-Host $ghCommand
Write-Host ''

if ($effectiveDryRun) {
    Write-ReleaseLine 'DRY RUN: no GitHub release was created and no files were uploaded.'
    exit 0
}

Write-ReleaseLine 'Publish requested; creating the GitHub release with gh.'
$ghExecutable = Get-Command gh -ErrorAction SilentlyContinue
Assert-Release ($null -ne $ghExecutable) 'GitHub CLI (`gh`) is required when -Publish is supplied.'

$ghArgs = @(
    'release',
    'create',
    $Tag,
    (ConvertTo-RelativePath $releaseZipPath),
    (ConvertTo-RelativePath $checksumPath),
    (ConvertTo-RelativePath $rcReportPath),
    '--repo',
    'DouglasNLima/local-docs-studio',
    '--target',
    $resolvedTargetCommit,
    '--title',
    $releaseTitle,
    '--notes-file',
    (ConvertTo-RelativePath $releaseNotesPath)
)

if ($Draft) {
    $ghArgs += '--draft'
}

if ($Prerelease) {
    $ghArgs += '--prerelease'
}

& gh @ghArgs
if ($LASTEXITCODE -ne 0) {
    throw "gh release create failed with exit code $LASTEXITCODE."
}

Write-ReleaseLine 'PASS: GitHub release was created.'
