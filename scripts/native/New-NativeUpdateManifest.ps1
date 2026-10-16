[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory, [string]$SigningKeyPath, [string]$KeyId = '')
$ErrorActionPreference = 'Stop'
$PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$metadata = Get-Content -LiteralPath (Join-Path $PackageDirectory 'version.json') -Raw | ConvertFrom-Json
if ($metadata.product -ne 'LensDocsStudio.Native' -or $metadata.channel -notin 'Stable','Preview') { throw 'The package identity is not a supported native update.' }
$assetName = "LensDocsStudio.Native-$($metadata.version)-win-$($metadata.architecture)-Setup.exe"
$installer = Get-Item -LiteralPath (Join-Path $PackageDirectory $assetName)
$manifest = [ordered]@{
    schemaVersion=1; productId=$metadata.product; version=$metadata.version; channel=$metadata.channel; architecture=$metadata.architecture
    minimumWindowsVersion=$metadata.minimumWindowsVersion; releaseTag="native-v$($metadata.version)"; assetName=$assetName
    size=$installer.Length; sha256=(Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$payload = [Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Compress))
$signature = ''
if ($SigningKeyPath) {
    if (-not $KeyId) { throw 'A pinned manifest key identifier is required when signing.' }
    $key = [Security.Cryptography.RSA]::Create()
    try {
        $key.ImportFromPem([IO.File]::ReadAllText((Resolve-Path -LiteralPath $SigningKeyPath).Path))
        $signature = [Convert]::ToBase64String($key.SignData($payload, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pss))
    } finally { $key.Dispose() }
}
$envelope = [ordered]@{ schemaVersion=1; payload=[Convert]::ToBase64String($payload); keyId=$KeyId; signature=$signature }
$envelope | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PackageDirectory 'native-update.json') -Encoding utf8NoBOM
if (-not $SigningKeyPath) { 'Unsigned internal preview metadata created; the application will not execute this update.' }
else { 'Signed native update metadata created. Publication requires a separate authorised release operation.' }
