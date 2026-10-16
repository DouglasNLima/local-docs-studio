[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^0\.1\.1-preview\.\d+$')][string]$Version
)
$ErrorActionPreference = 'Stop'
$catalogue = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../tests/fixtures/native-historical-previews.json') -Raw | ConvertFrom-Json
$pins = @($catalogue.previews | Where-Object { $_.version -eq $Version })
if ($pins.Count -ne 1) { throw 'Historical package has no unique original provenance pin.' }
$pin = $pins[0]
$metadata = Get-Content -LiteralPath (Join-Path $PackageDirectory 'version.json') -Raw | ConvertFrom-Json
if ($metadata.product -ne 'LensDocsStudio.Native' -or $metadata.version -ne $pin.version -or
    $metadata.sourceRevision -ne $pin.sourceRevision -or $metadata.sourceKind -ne 'branch-source' -or
    $metadata.sourceDirty -ne $false -or $metadata.ciRunId -ne $pin.runId -or $metadata.ciRunAttempt -ne '1' -or
    $metadata.selfContained -ne $true -or $metadata.internalPreview -ne $true -or $metadata.signingProvider -ne 'Unsigned') {
    throw 'Historical metadata differs from its original branch-source provenance.'
}
$prefix = "LensDocsStudio.Native-$Version-win-x64"
if ((Get-FileHash -LiteralPath (Join-Path $PackageDirectory "$prefix-Setup.exe") -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin.installerSha256 -or
    (Get-FileHash -LiteralPath (Join-Path $PackageDirectory "$prefix.zip") -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pin.zipSha256) {
    throw 'Historical installer or portable bytes differ from the independently recorded original.'
}
Write-Output "Original $Version branch-source installer and ZIP match recorded SHA-256 pins."
