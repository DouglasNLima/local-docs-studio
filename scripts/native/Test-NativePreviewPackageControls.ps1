[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory, [Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Negative-control evidence already exists; use a new output directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$metadataText = Get-Content -LiteralPath (Join-Path $PackageDirectory 'version.json') -Raw
$metadata = $metadataText | ConvertFrom-Json
$verifier = Join-Path $PSScriptRoot 'Test-NativePreviewPackage.ps1'
$checks = [Collections.Generic.List[object]]::new()
function Reject([string]$Name, [hashtable]$Changes, [string]$ExpectedError) {
    $output = Join-Path $OutputDirectory "$Name.json"
    $arguments = @{ PackageDirectory=$PackageDirectory; ExpectedVersion=$metadata.version; ExpectedSourceRevision=$metadata.sourceRevision; OutputPath=$output }
    foreach ($key in $Changes.Keys) { $arguments[$key] = $Changes[$key] }
    $rejected = $false
    try { & $verifier @arguments } catch {
        if ($_.Exception.Message -notmatch $ExpectedError) { throw }
        $rejected = $true
    }
    if (-not $rejected -or (Test-Path -LiteralPath $output)) { throw "Negative control did not fail closed: $Name" }
    $checks.Add(@{ name=$Name; status='PASS'; rejected=$true })
}
Reject 'wrong-source' @{ExpectedSourceRevision=('0' * 40)} 'identity does not match'
Reject 'wrong-version' @{ExpectedVersion='99.99.99-preview.99999'} 'identity does not match'
Reject 'wrong-independent-hash' @{ExpectedInstallerSha256=('0' * 64)} 'SHA-256 pin'
$copy = Join-Path $OutputDirectory 'modified-package'; New-Item -ItemType Directory -Path $copy | Out-Null
Get-ChildItem -LiteralPath $PackageDirectory -File | Where-Object { $_.Name -ne 'package-verification.json' } | Copy-Item -Destination $copy
$changed = $metadataText | ConvertFrom-Json; $changed.sourceDirty = $true
$changed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $copy 'version.json') -Encoding utf8NoBOM
Reject 'dirty-source' @{PackageDirectory=$copy} 'identity does not match'
[IO.File]::WriteAllText((Join-Path $copy 'version.json'), $metadataText)
$zips = @(Get-ChildItem -LiteralPath $copy -Filter '*.zip' -File)
if ($zips.Count -ne 1) { throw 'Expected exactly one portable ZIP for negative controls.' }
$zip = $zips[0]
$archive = [IO.Compression.ZipFile]::Open($zip.FullName, [IO.Compression.ZipArchiveMode]::Update)
try {
    $writer = [IO.StreamWriter]::new($archive.CreateEntry('ContinuousViewer.dll').Open())
    try { $writer.Write('Synthetic forbidden payload; not an executable.') } finally { $writer.Dispose() }
} finally { $archive.Dispose() }
Reject 'tampered-zip' @{PackageDirectory=$copy} 'SHA256SUMS'
$sums = Get-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') | ForEach-Object {
    if ($_.EndsWith($zip.Name)) { "{0}  {1}" -f (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $zip.Name } else { $_ }
}
$sums | Set-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') -Encoding utf8NoBOM
Reject 'excluded-experiment-with-recomputed-checksum' @{PackageDirectory=$copy} 'Unexpected or unsafe preview payload'
$archive = [IO.Compression.ZipFile]::Open($zip.FullName, [IO.Compression.ZipArchiveMode]::Update)
try {
    $archive.GetEntry('ContinuousViewer.dll').Delete()
    $coreStream = $archive.GetEntry('LensDocsStudio.Core.dll').Open(); $coreBytes = [IO.MemoryStream]::new()
    try { $coreStream.CopyTo($coreBytes) } finally { $coreStream.Dispose() }
    try {
        $archive.GetEntry('LensDocsStudio.Native.dll').Delete()
        $replacement = $archive.CreateEntry('LensDocsStudio.Native.dll').Open()
        try { $coreBytes.Position = 0; $coreBytes.CopyTo($replacement) } finally { $replacement.Dispose() }
    } finally { $coreBytes.Dispose() }
} finally { $archive.Dispose() }
$sums = Get-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') | ForEach-Object {
    if ($_.EndsWith($zip.Name)) { "{0}  {1}" -f (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $zip.Name } else { $_ }
}
$sums | Set-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') -Encoding utf8NoBOM
Reject 'missing-editor-with-recomputed-checksum' @{PackageDirectory=$copy} 'native Mermaid editor type is missing'
# Target the ER BAML key while retaining the original assembly identity and all editor types.
$originalArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $PackageDirectory $zip.Name))
try {
    $input = $originalArchive.GetEntry('LensDocsStudio.Native.dll').Open(); $buffer = [IO.MemoryStream]::new()
    try { $input.CopyTo($buffer); $nativeBytes = $buffer.ToArray() } finally { $input.Dispose(); $buffer.Dispose() }
} finally { $originalArchive.Dispose() }
$resourceKey = [Text.Encoding]::Unicode.GetBytes('ervisualeditorwindow.baml')
$offset = [Convert]::ToHexString($nativeBytes).IndexOf([Convert]::ToHexString($resourceKey), [StringComparison]::Ordinal)
if ($offset -lt 0 -or $offset % 2 -ne 0) { throw 'The exact compiled ER resource key was not found for the negative control.' }
$nativeBytes[$offset / 2] = [byte][char]'z'
$archive = [IO.Compression.ZipFile]::Open($zip.FullName, [IO.Compression.ZipArchiveMode]::Update)
try {
    $archive.GetEntry('LensDocsStudio.Native.dll').Delete(); $replacement = $archive.CreateEntry('LensDocsStudio.Native.dll').Open()
    try { $replacement.Write($nativeBytes, 0, $nativeBytes.Length) } finally { $replacement.Dispose() }
} finally { $archive.Dispose() }
$sums = Get-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') | ForEach-Object {
    if ($_.EndsWith($zip.Name)) { "{0}  {1}" -f (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $zip.Name } else { $_ }
}
$sums | Set-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') -Encoding utf8NoBOM
Reject 'missing-er-resource-with-recomputed-checksum' @{PackageDirectory=$copy} 'compiled native ER editor resource is missing'
@{ status='PASS'; checks=$checks; sourceRevision=$metadata.sourceRevision; originalPackageUnchanged=$true } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'negative-controls.json') -Encoding utf8NoBOM
"Native preview package negative controls PASS: $($checks.Count)"
