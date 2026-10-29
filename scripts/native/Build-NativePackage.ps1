[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+-preview\.\d+$')][string]$Version = '0.1.0-preview.1',
    [string]$IsccPath,
    [string]$SigningThumbprint,
    [string]$SignToolPath,
    [string]$SigningProviderPath,
    [string]$ExpectedPublisherSubject,
    [string]$ExpectedCertificateSha256,
    [string]$TimestampUrl = 'https://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeSigning.ps1')
$signing = New-NativeSigningConfiguration -SigningThumbprint $SigningThumbprint -SignToolPath $SignToolPath -SigningProviderPath $SigningProviderPath `
    -ExpectedPublisherSubject $ExpectedPublisherSubject -ExpectedCertificateSha256 $ExpectedCertificateSha256 -TimestampUrl $TimestampUrl
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repo
try {
    $revision = (& git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $revision -notmatch '^[0-9a-f]{40}$') { throw 'An exact Git source revision is required.' }
    $sourceTree = (& git rev-parse 'HEAD^{tree}').Trim()
    if ($LASTEXITCODE -ne 0 -or $sourceTree -notmatch '^[0-9a-f]{40}$') { throw 'An exact Git source tree is required.' }
    if (& git status --porcelain) { throw 'Preview packaging requires a clean source checkout. Commit changes before building.' }
} finally { Pop-Location }
$output = Join-Path $repo "artifacts/native/packages/$Version"
if (Test-Path -LiteralPath $output) { throw "Package output already exists: $output. Use a new preview version to preserve existing evidence." }
if (-not $IsccPath) {
    $candidates = @("$env:LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe", "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe")
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) { throw 'Inno Setup 6 is required to build the installer.' }
$payload = Join-Path $output 'app'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Push-Location $repo
try {
    # Keep the accepted preview runtime independent of the hosted SDK's latest patch.
    & dotnet publish src/native/LensDocsStudio.Native -c Release -r win-x64 --self-contained true -p:RuntimeFrameworkVersion=10.0.13 -p:RestoreLockedMode=true -p:Version=$Version -p:DebugType=None -p:DebugSymbols=false -p:PublishSingleFile=false -p:PublishTrimmed=false -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained native publish failed.' }
    $exe = Join-Path $payload 'LensDocsStudio.Native.exe'
    if (-not (Test-Path (Join-Path $payload 'coreclr.dll'))) { throw 'The package is missing its self-contained .NET runtime.' }
    $forbidden = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Name -match 'WebView2|chrome|node\.exe|index\.html|ContinuousViewer|\.pdb$|\.(pfx|pem|key)$' }
    if ($forbidden) { throw 'A browser or web runtime was found in the native package.' }
    Invoke-NativeAuthenticodeSigning -FilePath $exe -Configuration $signing
    $metadata = [ordered]@{ schemaVersion=1; product='LensDocsStudio.Native'; version=$Version; channel='Preview'; architecture='x64'; minimumWindowsVersion='10.0.17763'; selfContained=$true; internalPreview=$true; signingRequested=($signing.Mode -ne 'Unsigned'); signingProvider=$signing.Mode; sourceRevision=(& git rev-parse HEAD).Trim(); sourceDirty=[bool](& git status --porcelain); builtAtUtc=[DateTimeOffset]::UtcNow.ToString('O') }
    if ($metadata.sourceRevision -ne $revision -or $metadata.sourceDirty) { throw 'Source changed while publishing; discard this incomplete output and use a new version.' }
    $metadata.sourceTree = $sourceTree
    $mergeCheckout = $env:GITHUB_EVENT_NAME -eq 'pull_request' -and $metadata.sourceRevision -eq $env:GITHUB_SHA
    $metadata.sourceKind = if ($mergeCheckout) { 'pull-request-merge-checkout' } elseif ($env:GITHUB_ACTIONS -eq 'true') { 'branch-source' } else { 'local-source' }
    $metadata.ciRunId = $env:GITHUB_RUN_ID
    $metadata.ciRunAttempt = $env:GITHUB_RUN_ATTEMPT
    $metadata.checkoutRef = if ($env:GITHUB_EVENT_NAME -eq 'pull_request' -and -not $mergeCheckout) { 'refs/heads/' + $env:GITHUB_HEAD_REF } else { $env:GITHUB_REF }
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'version.json') -Encoding utf8NoBOM
    & (Join-Path $PSScriptRoot 'Copy-NativeLicence.ps1') -RepositoryRoot $repo -PayloadDirectory $payload -SourceRevision $revision
    & (Join-Path $PSScriptRoot 'Write-NativeNotices.ps1') -PayloadDirectory $payload
    Copy-Item -LiteralPath (Join-Path $repo 'docs/native/windows-preview-guide.md') -Destination (Join-Path $payload 'PREVIEW-GUIDE.md')
    Copy-Item -LiteralPath (Join-Path $repo 'docs/native/windows-preview-release-notes.md') -Destination (Join-Path $payload 'PREVIEW-RELEASE-NOTES.md')
    & $IsccPath "/DAppVersion=$Version" "/DPackageSource=$payload" "/DOutputDir=$output" (Join-Path $repo 'installer/inno/LensDocsStudio.Native.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Native installer compilation failed.' }
    $installer = Join-Path $output "LensDocsStudio.Native-$Version-win-x64-Setup.exe"
    Invoke-NativeAuthenticodeSigning -FilePath $installer -Configuration $signing
    Compress-Archive -Path "$payload/*" -DestinationPath (Join-Path $output "LensDocsStudio.Native-$Version-win-x64.zip")
    $checksums = Get-ChildItem -LiteralPath $output -File | Where-Object { $_.Extension -in '.exe','.zip' } | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
    $checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding utf8NoBOM
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'version.json') -Encoding utf8NoBOM
    & (Join-Path $PSScriptRoot 'New-NativeUpdateManifest.ps1') -PackageDirectory $output
    "Internal preview package built: $output"
} finally { Pop-Location }
