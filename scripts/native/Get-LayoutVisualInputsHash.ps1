$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$paths = @(& git -C $root ls-files src/native | Where-Object { $_ -match '\.(cs|xaml|csproj|props|targets)$' })
$paths += @(& git -C $root ls-files global.json Directory.Build.props Directory.Build.targets NuGet.Config)
[Array]::Sort($paths, [StringComparer]::Ordinal)
# Bind review to committed source, independent of Git's LF/CRLF checkout policy.
# The caller separately requires clean source and matching compiled provenance.
$blobs = @{}
foreach ($entry in (& git -C $root ls-tree -r --full-tree HEAD -- src/native global.json Directory.Build.props Directory.Build.targets NuGet.Config)) {
    if ($entry -match '^\d+ blob ([0-9a-f]+)\t(.+)$') { $blobs[$Matches[2]] = $Matches[1] }
}
$lines = foreach ($path in $paths) {
    if (-not $blobs.ContainsKey($path)) { throw "Committed visual input is missing: $path" }
    $path + ' ' + $blobs[$path]
}
$bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n") + "`n")
[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
