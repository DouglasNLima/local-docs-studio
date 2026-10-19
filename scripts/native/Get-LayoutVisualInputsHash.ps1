$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$paths = @(& git -C $root ls-files src/native | Where-Object { $_ -match '\.(cs|xaml|csproj|props|targets)$' })
$paths += @(& git -C $root ls-files global.json Directory.Build.props Directory.Build.targets NuGet.Config)
[Array]::Sort($paths, [StringComparer]::Ordinal)
$lines = foreach ($path in $paths) { $path + ' ' + (Get-FileHash -LiteralPath (Join-Path $root $path) -Algorithm SHA256).Hash }
$bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n") + "`n")
[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
