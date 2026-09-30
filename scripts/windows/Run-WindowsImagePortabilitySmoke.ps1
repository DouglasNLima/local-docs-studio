param([string]$AppExecutablePath, [int]$TimeoutSeconds = 180)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $AppExecutablePath) { $AppExecutablePath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows\bin\x64\Debug\net8.0-windows10.0.19041.0\LensDocsStudio.Windows.exe' }
$AppExecutablePath = (Resolve-Path -LiteralPath $AppExecutablePath).Path
$evidenceRoot = Join-Path $repoRoot ('artifacts\windows\image-portability-' + [guid]::NewGuid().ToString('N'))
$sourceRoot = Join-Path $evidenceRoot 'source-session'
$copyRoot = Join-Path $evidenceRoot 'copy-session'
$previousProfile = [Environment]::GetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', 'Process')
$png = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP432H3HwAHFALF2h7vpgAAAABJRU5ErkJggg==')
New-Item -ItemType Directory -Path (Join-Path $sourceRoot 'workspace\docs'),(Join-Path $sourceRoot 'workspace\assets\other'),$copyRoot -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $sourceRoot 'workspace\assets\capture.png'), $png)
[IO.File]::WriteAllBytes((Join-Path $sourceRoot 'workspace\assets\other\capture.png'), $png)
[IO.File]::WriteAllBytes((Join-Path $sourceRoot 'workspace\assets\ação (v1) # 100%.png'), $png)
[IO.File]::WriteAllText((Join-Path $sourceRoot 'workspace\README.md'), ((0..599 | ForEach-Object { "Line $_`: documentation text." }) -join "`n"))
[IO.File]::WriteAllText((Join-Path $sourceRoot 'workspace\docs\details.md'), "# Subfolder images`n`n")
$outside = Join-Path $sourceRoot 'outside'
New-Item -ItemType Directory -Path $outside -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $outside 'outside.png'), $png)
New-Item -ItemType Junction -Path (Join-Path $sourceRoot 'workspace\assets\junction') -Target $outside | Out-Null
function Invoke-ImageSmoke([string]$Root, [string]$Mode) {
    [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', (Join-Path $Root 'isolated-webview-profile'), 'Process')
    $process = Start-Process -FilePath $AppExecutablePath -ArgumentList @('--smoke-native-bridge','--smoke-root',('"'+$Root+'"'),'--smoke-images',$Mode,'--smoke-timeout-seconds',[string]$TimeoutSeconds) -WindowStyle Hidden -PassThru
    $resultPath = Join-Path $Root 'smoke-result.json'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline -and -not (Test-Path -LiteralPath $resultPath) -and -not $process.HasExited) { Start-Sleep -Milliseconds 250 }
    if (-not (Test-Path -LiteralPath $resultPath)) { if (-not $process.HasExited) { Stop-Process -Id $process.Id }; throw "Native image smoke timed out or exited early: $Root" }
    Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not $result.success) { Get-Content -LiteralPath $resultPath -Raw | Write-Host; throw "Native image smoke failed: $Mode" }
    return $result
}
try {
    $created = Invoke-ImageSmoke $sourceRoot 'create'
    $originalWorkspace = Join-Path $sourceRoot 'workspace'
    $copiedWorkspace = Join-Path $copyRoot 'workspace'
    # Copy only fixture documents and physical image bytes; exclude the escape
    # junction and both profiles. No profile data enters the copied workspace.
    New-Item -ItemType Directory -Path $copiedWorkspace -Force | Out-Null
    robocopy $originalWorkspace $copiedWorkspace /E /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "Fixture copy failed: robocopy exit $LASTEXITCODE" }
    $hashes = Get-ChildItem -LiteralPath $copiedWorkspace -Recurse -File | Where-Object Extension -Match '^\.(png|jpg|jpeg|gif|webp)$' | ForEach-Object { @{ path = [IO.Path]::GetRelativePath($copiedWorkspace, $_.FullName); bytes = $_.Length; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash } }
    if (@($hashes).Count -lt 7) { throw 'Expected physical images were not created.' }
    # Verified targets are confined to this generated fixture root.
    if (-not (Resolve-Path -LiteralPath $originalWorkspace).Path.StartsWith($evidenceRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected fixture path.' }
    Move-Item -LiteralPath $originalWorkspace -Destination (Join-Path $sourceRoot 'workspace-unavailable')
    $verified = Invoke-ImageSmoke $copyRoot 'verify'
    $report = @{ success = $true; executable = $AppExecutablePath; originalUnavailable = -not (Test-Path -LiteralPath $originalWorkspace); profilesIsolated = $true; physicalAssets = $hashes; create = $created; copy = $verified }
    $report | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $evidenceRoot 'portability-evidence.json') -Encoding utf8
    Write-Host "PASS: real Windows image persistence, copied workspace and isolated ZIP round-trip. Evidence: $evidenceRoot"
} finally { [Environment]::SetEnvironmentVariable('WEBVIEW2_USER_DATA_FOLDER', $previousProfile, 'Process') }
