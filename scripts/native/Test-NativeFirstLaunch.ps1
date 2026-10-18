[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Installed first-launch diagnosis requires disposable hosted Windows.'
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$root = Join-Path $repo 'artifacts/first-launch-diagnosis'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$installer = @(Get-ChildItem (Join-Path $repo 'artifacts/original-749') -Recurse -Filter '*0.1.0-preview.749*Setup.exe')
if ($installer.Count -ne 1 -or (Get-FileHash $installer[0].FullName).Hash -ne '89494C6A963FD283773CE23AF979AA732796920E943C3A607DB413D67720CC07') { throw 'Original .749 installer identity differs.' }
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}_is1'
if (Test-Path -LiteralPath $key) { throw 'Preserve an existing native installation.' }
$install = Join-Path $root 'installed'
$samples = [Collections.Generic.List[object]]::new()
try {
    $setup = Start-Process $installer[0].FullName -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="' + $install + '"'),'/TASKS="fileassoc,desktopicon"',('/LOG="' + (Join-Path $root 'install.log') + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($setup.ExitCode -ne 0) { throw 'Original installer failed.' }
    for ($index = 0; $index -lt 7; $index++) {
        $env:LENS_NATIVE_OUTLINE_FOCUS_PROBE = if ($index -eq 5) { 'original' } elseif ($index -eq 6) { 'repaired' } else { '' }
        $output = Join-Path $root ('launch-' + $index)
        New-Item -ItemType Directory -Path $output -Force | Out-Null
        $validation = Join-Path $repo 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
        $process = Start-Process $validation -ArgumentList @('--first-launch-ui',('"' + (Join-Path $install 'LensDocsStudio.Native.exe') + '"'),('"' + $output + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'stdout.txt') -RedirectStandardError (Join-Path $output 'stderr.txt')
        $completed = $process.WaitForExit(180000)
        if (-not $completed) { $process.Kill($true); $process.WaitForExit() }
        # A fixed sequence, never a retry-until-green policy. Preserve every result.
        $samples.Add(@{ index=$index; kind=$(if ($index -lt 5) { 'independent-original' } else { $env:LENS_NATIVE_OUTLINE_FOCUS_PROBE }); completed=$completed; exitCode=$process.ExitCode })
        $process.Dispose()
        foreach ($child in Get-Process 'LensDocsStudio.Native' -ErrorAction SilentlyContinue) {
            if ($child.Path -eq (Join-Path $install 'LensDocsStudio.Native.exe')) { $child.Kill(); $child.WaitForExit() }
        }
    }
} finally {
    Remove-Item Env:LENS_NATIVE_OUTLINE_FOCUS_PROBE -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath (Join-Path $install 'unins000.exe')) {
        $cleanup = Start-Process (Join-Path $install 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $root 'uninstall.log') + '"')) -WindowStyle Hidden -PassThru -Wait
        if ($cleanup.ExitCode -ne 0) { Write-Warning 'Disposable native uninstall failed.' }
    }
    @{ originalInstallerSha256=(Get-FileHash $installer[0].FullName).Hash; independentFreshProfiles=$true; predeterminedCount=5; samples=$samples; sourceRevision=(git -C $repo rev-parse HEAD) } | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $root 'results.json')
}
if (@($samples | Where-Object { $_.kind -ne 'original' -and (-not $_.completed -or $_.exitCode -ne 0) }).Count -or $samples.Count -ne 7) { throw 'A predetermined original first launch failed; inspect its pre-cleanup evidence.' }
