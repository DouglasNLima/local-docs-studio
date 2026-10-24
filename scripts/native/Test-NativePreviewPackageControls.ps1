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
# Target the Class BAML key while retaining the original assembly identity and all editor types.
$originalArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $PackageDirectory $zip.Name))
try {
    $input = $originalArchive.GetEntry('LensDocsStudio.Native.dll').Open(); $buffer = [IO.MemoryStream]::new()
    try { $input.CopyTo($buffer); $nativeBytes = $buffer.ToArray() } finally { $input.Dispose(); $buffer.Dispose() }
} finally { $originalArchive.Dispose() }
$resourceKey = [Text.Encoding]::Unicode.GetBytes('classvisualeditorwindow.baml')
$offset = [Convert]::ToHexString($nativeBytes).IndexOf([Convert]::ToHexString($resourceKey), [StringComparison]::Ordinal)
if ($offset -lt 0 -or $offset % 2 -ne 0) { throw 'The exact compiled Class resource key was not found for the negative control.' }
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
Reject 'missing-class-resource-with-recomputed-checksum' @{PackageDirectory=$copy} 'compiled native Class editor resource is missing'
# Retain version/resources and remove one new metadata type at a time. No package is executed.
foreach ($family in 'Xy','Radar','Sankey','Kanban','Requirement','GitGraph','Architecture','C4','Block') {
    $originalArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $PackageDirectory $zip.Name))
    try {
        $input = $originalArchive.GetEntry('LensDocsStudio.Native.dll').Open(); $buffer = [IO.MemoryStream]::new()
        try { $input.CopyTo($buffer); $nativeBytes = $buffer.ToArray() } finally { $input.Dispose(); $buffer.Dispose() }
    } finally { $originalArchive.Dispose() }
    $typeName = $family + 'VisualEditorWindow'
    $typeKey = [Text.Encoding]::UTF8.GetBytes($typeName + [char]0)
    $offset = [Convert]::ToHexString($nativeBytes).IndexOf([Convert]::ToHexString($typeKey), [StringComparison]::Ordinal)
    if ($offset -lt 0 -or $offset % 2 -ne 0) { throw "The exact compiled $family metadata type was not found for the negative control." }
    $nativeBytes[$offset / 2] = [byte][char]'Z'
    $archive = [IO.Compression.ZipFile]::Open($zip.FullName, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $archive.GetEntry('LensDocsStudio.Native.dll').Delete(); $replacement = $archive.CreateEntry('LensDocsStudio.Native.dll').Open()
        try { $replacement.Write($nativeBytes, 0, $nativeBytes.Length) } finally { $replacement.Dispose() }
    } finally { $archive.Dispose() }
    $sums = Get-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') | ForEach-Object {
        if ($_.EndsWith($zip.Name)) { "{0}  {1}" -f (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $zip.Name } else { $_ }
    }
    $sums | Set-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') -Encoding utf8NoBOM
    Reject ('missing-' + $family.ToLowerInvariant() + '-type-with-recomputed-checksum') @{PackageDirectory=$copy} ('native Mermaid editor type is missing: LensDocsStudio.Native.' + $typeName)
}
# Four independently targeted Expansion VI contracts: native type, Core type, embedded catalogue and pinned dependency manifest.
foreach ($control in @(
    @{ name='packet-native-type'; entry='LensDocsStudio.Native.dll'; needle=('PacketVisualEditorWindow' + [char]0); error='native Mermaid editor type is missing: LensDocsStudio.Native.PacketVisualEditorWindow' },
    @{ name='treeview-core-node-type'; entry='LensDocsStudio.Core.dll'; needle=('TreeViewSourceNode' + [char]0); error='Core Mermaid editor type is missing: LensDocsStudio.Core.TreeViewDiagrams.TreeViewSourceNode' },
    @{ name='treemap-embedded-catalogue'; entry='LensDocsStudio.Core.dll'; needle='mermaid/treemap'; error='compiled native treemap catalogue entry is missing or duplicated' },
    @{ name='venn-pinned-renderer-manifest'; entry='LensDocsStudio.Native.deps.json'; needle='Mermaider/0.15.1'; error='native Venn renderer manifest differs from pinned Mermaider/Sugiyama 0.15.1' }
)) {
    $originalArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $PackageDirectory $zip.Name))
    $archive = [IO.Compression.ZipFile]::Open($zip.FullName, [IO.Compression.ZipArchiveMode]::Update)
    try {
        foreach ($name in 'LensDocsStudio.Native.dll','LensDocsStudio.Core.dll','LensDocsStudio.Native.deps.json') {
            $input = $originalArchive.GetEntry($name).Open(); $buffer = [IO.MemoryStream]::new()
            try { $input.CopyTo($buffer); $bytes = $buffer.ToArray() } finally { $input.Dispose(); $buffer.Dispose() }
            if ($name -eq $control.entry) {
                if ($name.EndsWith('.json')) {
                    $value = [Text.Encoding]::UTF8.GetString($bytes)
                    if (-not $value.Contains($control.needle, [StringComparison]::Ordinal)) { throw 'The exact pinned renderer manifest identity was not found.' }
                    $bytes = [Text.Encoding]::UTF8.GetBytes($value.Replace($control.needle, 'Mermaider/0.15.2'))
                } else {
                    $key = [Text.Encoding]::UTF8.GetBytes($control.needle)
                    $offset = [Convert]::ToHexString($bytes).IndexOf([Convert]::ToHexString($key), [StringComparison]::Ordinal)
                    if ($offset -lt 0 -or $offset % 2 -ne 0) { throw "The independent $($control.name) mutation target was not found." }
                    $bytes[$offset / 2] = [byte][char]'Z'
                }
            }
            $archive.GetEntry($name).Delete(); $replacement = $archive.CreateEntry($name).Open()
            try { $replacement.Write($bytes, 0, $bytes.Length) } finally { $replacement.Dispose() }
        }
    } finally { $archive.Dispose(); $originalArchive.Dispose() }
    $sums = Get-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') | ForEach-Object {
        if ($_.EndsWith($zip.Name)) { "{0}  {1}" -f (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $zip.Name } else { $_ }
    }
    $sums | Set-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') -Encoding utf8NoBOM
    Reject ($control.name + '-with-recomputed-checksum') @{PackageDirectory=$copy} ([regex]::Escape($control.error))
}
# Restore the original bytes independently for each runtime declaration mutation.
Reject 'wrong-tree-pin' @{ExpectedSourceTree=('0' * 40)} 'source tree does not match'
foreach ($framework in 'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App') {
    Copy-Item -LiteralPath (Join-Path $PackageDirectory $zip.Name) -Destination $zip.FullName -Force
    $archive = [IO.Compression.ZipFile]::Open($zip.FullName, [IO.Compression.ZipArchiveMode]::Update)
    try {
        $entry = $archive.GetEntry('LensDocsStudio.Native.runtimeconfig.json')
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $runtime = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        $target = @($runtime.runtimeOptions.includedFrameworks | Where-Object { $_.name -ceq $framework })
        if ($target.Count -ne 1 -or $target[0].version -cne '10.0.13') { throw 'Exact runtime mutation target missing.' }
        $target[0].version = '10.0.12'
        $entry.Delete(); $writer = [IO.StreamWriter]::new($archive.CreateEntry('LensDocsStudio.Native.runtimeconfig.json').Open())
        try { $writer.Write(($runtime | ConvertTo-Json -Depth 8)) } finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
    $sums = Get-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') | ForEach-Object {
        if ($_.EndsWith($zip.Name)) { "{0}  {1}" -f (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $zip.Name } else { $_ }
    }
    $sums | Set-Content -LiteralPath (Join-Path $copy 'SHA256SUMS.txt') -Encoding utf8NoBOM
    Reject ('wrong-runtime-' + $framework) @{PackageDirectory=$copy} 'exactly the included Core/WPF 10.0.13'
}
@{ status='PASS'; checks=$checks; sourceRevision=$metadata.sourceRevision; originalPackageUnchanged=$true } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'negative-controls.json') -Encoding utf8NoBOM
"Native preview package negative controls PASS: $($checks.Count)"
