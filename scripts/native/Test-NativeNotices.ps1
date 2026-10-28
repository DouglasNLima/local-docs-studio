[CmdletBinding()]
param([string]$OutputDirectory = 'artifacts/native/notice-controls')
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([guid]::NewGuid().ToString('N'))
$cache = Join-Path $root 'packages'
$payload = Join-Path $root 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$libraries = @{}
foreach ($id in @('sample','microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')) {
    $path = Join-Path $cache "$id/1.2.3"
    New-Item -ItemType Directory -Path $path -Force | Out-Null
    $licence = if ($id -eq 'sample') { '<license type="file">terms.txt</license>' } else { '<license type="expression">MIT</license>' }
    "<package><metadata><id>$id</id><version>1.2.3</version><authors>Test fixture</authors>$licence</metadata></package>" | Set-Content -LiteralPath (Join-Path $path "$id.nuspec")
    'Synthetic redistribution notice, not an actual licence.' | Set-Content -LiteralPath (Join-Path $path 'THIRD-PARTY-NOTICES.md')
    'Synthetic licence file.' | Set-Content -LiteralPath (Join-Path $path 'terms.txt')
    if ($id -eq 'sample') { $libraries["$id/1.2.3"] = @{type='package';path="$id/1.2.3"} }
}
$assets = Join-Path $root 'assets.json'
@{ libraries=$libraries; packageFolders=@{ $cache=@{} } } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $assets
@{runtimeOptions=@{includedFrameworks=@(@{name='Microsoft.NETCore.App';version='1.2.3'},@{name='Microsoft.WindowsDesktop.App';version='1.2.3'})}} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $payload 'LensDocsStudio.Native.runtimeconfig.json')
& (Join-Path $PSScriptRoot 'Write-NativeNotices.ps1') -PayloadDirectory $payload -AssetsPath $assets
$checks = 0
foreach ($relative in @('sample/THIRD-PARTY-NOTICES.md','sample/terms.txt','microsoft.netcore.app.runtime.win-x64/THIRD-PARTY-NOTICES.md','microsoft.windowsdesktop.app.runtime.win-x64/THIRD-PARTY-NOTICES.md')) {
    $actual = Join-Path $payload "licences/$relative"
    $component, $name = $relative.Split('/')
    if ((Get-FileHash -LiteralPath $actual).Hash -ne (Get-FileHash -LiteralPath (Join-Path $cache "$component/1.2.3/$name")).Hash) { throw "Notice bytes were not preserved: $relative" }
    $checks++
}
# Remove only the known synthetic file within this new test directory.
Remove-Item -LiteralPath (Join-Path $cache 'sample/1.2.3/terms.txt')
try {
    & (Join-Path $PSScriptRoot 'Write-NativeNotices.ps1') -PayloadDirectory $payload -AssetsPath $assets
    throw 'A missing declared licence was accepted.'
} catch {
    if ($_.Exception.Message -notlike '*declared licence file*missing*') { throw }
    $checks++
}
"Native notice controls: $checks outcomes passed; synthetic packages only."
