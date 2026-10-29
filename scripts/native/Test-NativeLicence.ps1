# SPDX-License-Identifier: MIT
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PortablePath,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceRevision,
    [Parameter(Mandatory)][string]$ExpectedVersion,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$InstallerSha256
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
# Preserve only the independently pinned pre-adoption diagnostic pair; missing
# source objects or missing ZIP notices must never imply a historical exemption.
$historical = @{
    '0.1.0-preview.853' = @('5a0e53a7c2a7f73861ad75ea2ac8c00c1652f55658270c7719b706aa37e6c156', '95b2c94b2dcebdd5d76f01e610f2a6ddda9b0991cf7e94e33ae0ca1e9a0db710')
    '0.1.1-preview.853' = @('fbd18dfe705d79657037047f4acb0e0b1d4365d1d53ec960fb8eaae082441650', '4a0d1d58621aed669d131cc66d2cee6e7ea53ddbfe9d7f0b2f65f6641a1ca8a3')
}
if ($ExpectedSourceRevision -ceq '358147cd6da2265efb49e4922698796f9399fcf6' -and $historical.ContainsKey($ExpectedVersion)) {
    $pins = $historical[$ExpectedVersion]
    if ((Get-FileHash -LiteralPath $PortablePath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $pins[0] -or $InstallerSha256 -cne $pins[1]) {
        throw 'Historical pre-adoption licence exception requires the exact original package hashes.'
    }
    return @{ status='HISTORICAL'; sourceGrant='NOT ADOPTED at the pinned original source' }
}
function Read-SourceText([string]$RelativePath) {
    $lines = & git -C $repo show "${ExpectedSourceRevision}:$RelativePath" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Cannot inspect scoped licence at the independently pinned source: $RelativePath" }
    return ($lines -join "`n") + "`n"
}
$expected = @{
    'LICENSE' = Read-SourceText 'LICENSE'
    'FIRST-PARTY-SCOPE.json' = Read-SourceText 'docs/open-source/configuration/mit-scope.json'
    'LICENSING.md' = (Read-SourceText 'docs/open-source/native-distribution-licensing.md').Replace('SOURCE_REVISION', $ExpectedSourceRevision)
}
$archive = [IO.Compression.ZipFile]::OpenRead($PortablePath)
try {
    foreach ($name in $expected.Keys) {
        $entries = @($archive.Entries | Where-Object { $_.FullName.Replace('\','/') -ceq $name })
        if ($entries.Count -ne 1 -or $entries[0].Length -le 0 -or $entries[0].Length -gt 1MB) {
            throw "Required scoped licence entry missing, duplicated or oversized: $name"
        }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { $actual = $reader.ReadToEnd().Replace("`r`n", "`n") } finally { $reader.Dispose() }
        if ($actual -cne $expected[$name]) { throw "Scoped licence entry differs from the independently pinned source: $name" }
    }
} finally { $archive.Dispose() }
return @{ status='PASS'; sourceRevision=$ExpectedSourceRevision; files=@($expected.Keys | Sort-Object); scope='Original first-party portions only' }
