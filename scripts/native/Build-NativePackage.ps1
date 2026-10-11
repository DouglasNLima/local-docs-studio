[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+-preview\.\d+$')][string]$Version = '0.1.0-preview.1',
    [string]$IsccPath,
    [string]$SigningThumbprint,
    [string]$SignToolPath,
    [string]$TimestampUrl = 'https://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $repo "artifacts/native/packages/$Version"
if (Test-Path -LiteralPath $output) { throw "Package output already exists: $output. Use a new preview version to preserve existing evidence." }
if (-not $IsccPath) {
    $candidates = @("$env:LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe", "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe")
    $IsccPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath)) { throw 'Inno Setup 6 is required to build the installer.' }
if ($SigningThumbprint -and (-not $SignToolPath -or -not (Test-Path -LiteralPath $SignToolPath))) { throw 'Supply the Windows SDK signtool path for signing.' }
if ($SigningThumbprint -and -not $TimestampUrl.StartsWith('https://')) { throw 'Timestamping requires HTTPS.' }
$payload = Join-Path $output 'app'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Push-Location $repo
try {
    & dotnet publish src/native/LensDocsStudio.Native -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -p:Version=$Version -p:PublishSingleFile=false -p:PublishTrimmed=false -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained native publish failed.' }
    $exe = Join-Path $payload 'LensDocsStudio.Native.exe'
    if (-not (Test-Path (Join-Path $payload 'coreclr.dll'))) { throw 'The package is missing its self-contained .NET runtime.' }
    $forbidden = Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Name -match 'WebView2|chrome|node\.exe|index\.html' }
    if ($forbidden) { throw 'A browser or web runtime was found in the native package.' }
    if ($SigningThumbprint) {
        & $SignToolPath sign /sha1 $SigningThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $exe
        if ($LASTEXITCODE -ne 0) { throw 'Application signing failed.' }
    }
    $metadata = [ordered]@{ schemaVersion=1; product='LensDocsStudio.Native'; version=$Version; channel='Preview'; architecture='x64'; minimumWindowsVersion='10.0.17763'; selfContained=$true; internalPreview=$true; signingRequested=[bool]$SigningThumbprint; sourceRevision=(& git rev-parse HEAD).Trim(); sourceDirty=[bool](& git status --porcelain); builtAtUtc=[DateTimeOffset]::UtcNow.ToString('O') }
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'version.json') -Encoding utf8NoBOM
    if (Test-Path -LiteralPath (Join-Path $repo 'LICENSE')) { Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $payload }
    & (Join-Path $PSScriptRoot 'Write-NativeNotices.ps1') -PayloadDirectory $payload
    & $IsccPath "/DAppVersion=$Version" "/DPackageSource=$payload" "/DOutputDir=$output" (Join-Path $repo 'installer/inno/LensDocsStudio.Native.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Native installer compilation failed.' }
    $installer = Join-Path $output "LensDocsStudio.Native-$Version-win-x64-Setup.exe"
    if ($SigningThumbprint) {
        & $SignToolPath sign /sha1 $SigningThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $installer
        if ($LASTEXITCODE -ne 0) { throw 'Installer signing failed.' }
        & $SignToolPath verify /pa /all $installer
        if ($LASTEXITCODE -ne 0) { throw 'Installer signature verification failed.' }
    }
    Compress-Archive -Path "$payload/*" -DestinationPath (Join-Path $output "LensDocsStudio.Native-$Version-win-x64.zip")
    $checksums = Get-ChildItem -LiteralPath $output -File | Where-Object { $_.Extension -in '.exe','.zip' } | ForEach-Object { "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }
    $checksums | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt') -Encoding utf8NoBOM
    $metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'version.json') -Encoding utf8NoBOM
    & (Join-Path $PSScriptRoot 'New-NativeUpdateManifest.ps1') -PackageDirectory $output
    "Internal preview package built: $output"
} finally { Pop-Location }
