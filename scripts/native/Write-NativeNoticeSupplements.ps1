[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [string]$AssetsPath,
    [string]$ManifestPath,
    [string]$NoticeDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $AssetsPath) { $AssetsPath = Join-Path $repo 'src/native/LensDocsStudio.Native/obj/project.assets.json' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $repo 'docs/native/licences/native-notice-supplements.json' }
if (-not $NoticeDirectory) { $NoticeDirectory = Join-Path $repo 'docs/open-source/evidence/upstream-notices' }
$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.scope -ne 'Third-party notices only; no first-party grant or redistribution clearance') {
    throw 'Review the native notice supplement manifest before packaging.'
}
$assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json -AsHashtable
$deps = Get-Content -LiteralPath (Join-Path $PayloadDirectory 'LensDocsStudio.Native.deps.json') -Raw | ConvertFrom-Json -AsHashtable
$runtime = Get-Content -LiteralPath (Join-Path $PayloadDirectory 'LensDocsStudio.Native.runtimeconfig.json') -Raw | ConvertFrom-Json
$identities = @($deps.libraries.GetEnumerator() | Where-Object { $_.Value.type -eq 'package' } | ForEach-Object Key)
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    if ($framework.name -notin 'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App' -or $framework.version -notmatch '^\d+\.\d+\.\d+$') {
        throw 'Unexpected native runtime identity.'
    }
    $identities += $framework.name + '.Runtime.win-x64/' + $framework.version
}
$expected = @($manifest.packages.id | Sort-Object -Unique)
if ($expected.Count -ne $manifest.packages.Count -or (Compare-Object ($identities | Sort-Object -Unique) $expected)) {
    throw 'Published native dependency identities differ from the reviewed notice manifest.'
}
# Validate every input before writing. Packaging is offline; upstream files are
# reviewed immutable snapshots, never URLs fetched from dependency metadata.
$copies = [Collections.Generic.List[object]]::new()
$lines = [Collections.Generic.List[string]]::new()
$lines.Add(''); $lines.Add('## Version-bound upstream notice supplements'); $lines.Add('')
$lines.Add('The generic SPDX texts above are reference terms. The following original upstream texts supplement package metadata and package-supplied notices. All third-party rights remain separate; archive/source hashes do not establish legal permission or source-to-binary reproducibility. Shared source notices can conservatively cover companion packages without claiming every upstream tool or subcomponent is shipped.')
foreach ($package in $manifest.packages) {
    if ($package.id -notmatch '^[A-Za-z0-9_.-]+/[0-9A-Za-z.+-]+$' -or $package.archiveSha256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Invalid reviewed package identity or hash.' }
    $id, $version = $package.id.Split('/')
    $archiveRelative = ($id + '/' + $version + '/' + $id + '.' + $version + '.nupkg').ToLowerInvariant()
    $archive = $assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $archiveRelative } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $archive -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $package.archiveSha256) {
        throw "Reviewed NuGet archive hash differs or is missing: $($package.id)"
    }
    foreach ($notice in $package.supplements) {
        if ($notice.sha256 -cnotmatch '^[0-9a-f]{64}$' -or $notice.file -cne ($notice.sha256 + '.txt') -or
            $notice.sourceCommit -cnotmatch '^[0-9a-f]{40}$' -or
            -not $notice.source.StartsWith('https://raw.githubusercontent.com/', [StringComparison]::Ordinal) -or
            -not $notice.source.Contains('/' + $notice.sourceCommit + '/')) { throw 'Invalid immutable upstream notice reference.' }
        $source = Join-Path $NoticeDirectory $notice.file
        if (-not (Test-Path -LiteralPath $source -PathType Leaf) -or
            (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -cne $notice.sha256) {
            throw "Upstream notice bytes differ or are missing: $($package.id)"
        }
        $relative = 'licences/' + $id + '/upstream-' + $notice.file
        $copies.Add(@{ Source=$source; Destination=(Join-Path $PayloadDirectory $relative) })
        $lines.Add(''); $lines.Add("- $($package.id): [$($notice.upstreamPath)]($relative)")
        $lines.Add("  Source: $($notice.source); SHA-256: $($notice.sha256)")
    }
}
$noticePath = Join-Path $PayloadDirectory 'THIRD-PARTY-NOTICES.md'
if (-not (Test-Path -LiteralPath $noticePath -PathType Leaf)) { throw 'Generate the base native notices before adding supplements.' }
if ([IO.File]::ReadAllText($noticePath).Contains('## Version-bound upstream notice supplements')) { throw 'Native notice supplements have already been written.' }
foreach ($copy in $copies) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $copy.Destination) -Force | Out-Null
    Copy-Item -LiteralPath $copy.Source -Destination $copy.Destination
}
$lines | Add-Content -LiteralPath $noticePath -Encoding utf8NoBOM
Copy-Item -LiteralPath $ManifestPath -Destination (Join-Path $PayloadDirectory 'licences/native-notice-supplements.json')
"Native notice supplements: $($expected.Count) reviewed archives; $($copies.Count) original text references."
