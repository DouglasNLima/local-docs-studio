[CmdletBinding()]
param([string]$OutputDirectory = 'artifacts/native/notice-supplement-controls')
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ([guid]::NewGuid().ToString('N'))
$cache = Join-Path $root 'packages'
$payload = Join-Path $root 'payload'
$upstream = Join-Path $root 'upstream'
New-Item -ItemType Directory -Path $cache,$payload,$upstream -Force | Out-Null
$assets = Join-Path $root 'assets.json'
$manifestPath = Join-Path $root 'manifest.json'
$noticePath = Join-Path $payload 'THIRD-PARTY-NOTICES.md'
$depsPath = Join-Path $payload 'LensDocsStudio.Native.deps.json'
$runtimePath = Join-Path $payload 'LensDocsStudio.Native.runtimeconfig.json'
$originalNotice = [byte[]](0xEF,0xBB,0xBF) + [Text.Encoding]::UTF8.GetBytes("Synthetic upstream notice.`r`nCopyright fixture.`n")
$digest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($originalNotice)).ToLowerInvariant()
$source = Join-Path $upstream ($digest + '.txt')
[IO.File]::WriteAllBytes($source, $originalNotice)
$packages = @()
foreach ($id in 'sample','Microsoft.NETCore.App.Runtime.win-x64','Microsoft.WindowsDesktop.App.Runtime.win-x64') {
    $directory = Join-Path $cache ($id.ToLowerInvariant() + '/1.2.3')
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $archive = Join-Path $directory ($id.ToLowerInvariant() + '.1.2.3.nupkg')
    [IO.File]::WriteAllText($archive, 'Synthetic package bytes, not an actual NuGet archive.')
    $supplements = @()
    if ($id -eq 'sample') {
        $supplements = @(@{file=($digest + '.txt');sha256=$digest;sourceCommit=('a' * 40);source=('https://raw.githubusercontent.com/example/project/' + ('a' * 40) + '/LICENSE');upstreamPath='LICENSE'})
    }
    $packages += @{id=($id + '/1.2.3');archiveSha256=(Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant();supplements=$supplements}
}
@{packageFolders=@{$cache=@{}}} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $assets
$manifest = @{schemaVersion=1;scope='Third-party notices only; no first-party grant or redistribution clearance';packages=$packages}
$deps = @{libraries=@{'sample/1.2.3'=@{type='package'}}}
$runtime = @{runtimeOptions=@{includedFrameworks=@(@{name='Microsoft.NETCore.App';version='1.2.3'},@{name='Microsoft.WindowsDesktop.App';version='1.2.3'})}}
function Reset-Fixture {
    $manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath
    $deps | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $depsPath
    $runtime | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $runtimePath
    'Synthetic base notice.' | Set-Content -LiteralPath $noticePath
    [IO.File]::WriteAllBytes($source, $originalNotice)
}
function Invoke-Writer {
    & (Join-Path $PSScriptRoot 'Write-NativeNoticeSupplements.ps1') -PayloadDirectory $payload -AssetsPath $assets -ManifestPath $manifestPath -NoticeDirectory $upstream
}
$checks = [Collections.Generic.List[string]]::new()
function Assert-Rejected([string]$Expected, [string]$Description) {
    $before = (Get-FileHash -LiteralPath $noticePath).Hash
    try { Invoke-Writer; throw 'Negative control was accepted.' } catch {
        if ($_.Exception.Message -notlike $Expected) { throw }
    }
    if ((Get-FileHash -LiteralPath $noticePath).Hash -ne $before) { throw 'Rejected inputs changed the base notice.' }
    $checks.Add($Description)
}
Reset-Fixture
Invoke-Writer
$copied = Join-Path $payload ('licences/sample/upstream-' + $digest + '.txt')
if ((Get-FileHash -LiteralPath $copied).Hash.ToLowerInvariant() -cne $digest) { throw 'Original upstream notice bytes were changed.' }
if (-not [IO.File]::ReadAllText($noticePath).Contains('licences/sample/upstream-' + $digest + '.txt')) { throw 'The notice index lacks its local readable link.' }
$checks.Add('Original BOM and mixed line endings retained; local notice link present')
Assert-Rejected '*already been written*' 'Replay rejected without changing notices'
Reset-Fixture
[IO.File]::WriteAllText($source, 'Changed copyright text')
Assert-Rejected '*notice bytes differ*' 'Modified upstream notice rejected'
Reset-Fixture
Remove-Item -LiteralPath $source
Assert-Rejected '*notice bytes differ*' 'Missing upstream notice rejected'
Reset-Fixture
$packages[0].archiveSha256 = '0' * 64
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath
Assert-Rejected '*archive hash differs*' 'Unreviewed archive bytes rejected'
$packages[0].archiveSha256 = (Get-FileHash -LiteralPath (Join-Path $cache 'sample/1.2.3/sample.1.2.3.nupkg')).Hash.ToLowerInvariant()
Reset-Fixture
$deps.libraries['new-package/1.0.0'] = @{type='package'}
$deps | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $depsPath
Assert-Rejected '*dependency identities differ*' 'Additional runtime dependency rejected'
$deps.libraries.Remove('new-package/1.0.0')
Reset-Fixture
$manifest.packages = $packages + $packages[0]
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath
Assert-Rejected '*dependency identities differ*' 'Duplicate reviewed identity rejected'
$manifest.packages = $packages
Reset-Fixture
$packages[0].supplements[0].file = '../outside.txt'
$manifest | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $manifestPath
Assert-Rejected '*Invalid immutable*' 'Path traversal rejected'
$packages[0].supplements[0].file = $digest + '.txt'
Reset-Fixture
$runtime.runtimeOptions.includedFrameworks[0].name = 'Unexpected.Runtime'
$runtime | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $runtimePath
Assert-Rejected '*Unexpected native runtime*' 'Unexpected runtime rejected'
@{synthetic=$true;checks=$checks;count=$checks.Count} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $root 'results.json')
"Native notice supplement controls: $($checks.Count) outcomes passed. Evidence: $root"
