param(
    [string]$EvidenceDirectory = 'artifacts/comparison-reflow',
    [ValidateSet('Continuous','Automatic','Pages')][string]$Mode = 'Continuous'
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Foreground input and OS appearance changes require a disposable hosted Windows runner.'
}
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$protocol = Get-Content -LiteralPath (Join-Path $evidence 'protocol.json') -Raw | ConvertFrom-Json
$outcomes = [Collections.Generic.List[object]]::new()
function Run-BoundedProbe($side, $label, $arguments, $environment) {
    $directory = Join-Path $evidence $label
    if (Test-Path -LiteralPath $directory) { throw 'A failed observation must never be overwritten.' }
    New-Item -ItemType Directory -Path (Split-Path $directory) -Force | Out-Null
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $protocol.products.$side.binary 'LensDocsStudio.Native.UxProbe.exe'))
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument.Replace('{output}', $directory)) }
    foreach ($key in $environment.Keys) { $info.Environment[$key] = $environment[$key] }
    $child = [Diagnostics.Process]::Start($info)
    $out = [IO.File]::Create("$directory-stdout.txt"); $err = [IO.File]::Create("$directory-stderr.txt")
    $outTask = $child.StandardOutput.BaseStream.CopyToAsync($out); $errTask = $child.StandardError.BaseStream.CopyToAsync($err)
    $watch = [Diagnostics.Stopwatch]::StartNew(); $bounded = $null
    while (!$child.WaitForExit(250)) {
        $child.Refresh()
        if ($watch.Elapsed.TotalSeconds -gt 240) { $bounded = '240-second child supervision limit' }
        if ($child.PrivateMemorySize64 -gt 2147483648) { $bounded = '2 GiB child private-memory supervision limit' }
        if ($out.Length + $err.Length -gt 16777216) { $bounded = '16 MiB child log supervision limit' }
        if ($bounded) { $child.Kill($true); $child.WaitForExit(); break }
    }
    try { $outTask.GetAwaiter().GetResult(); $errTask.GetAwaiter().GetResult() }
    finally { $out.Dispose(); $err.Dispose() }
    $outcomes.Add(@{side=$side;label=$label;mode=$Mode;revision=$protocol.products.$side.revision;tree=$protocol.products.$side.tree;
        exitCode=$child.ExitCode;supervisionFailure=$bounded;environment=$environment})
    $outcomes | ConvertTo-Json -Depth 5 | Set-Content "$evidence/comparison-results.json" -Encoding utf8NoBOM
    Write-Output "$label exit=$($child.ExitCode)"
}
$fixtures = [IO.Path]::GetFullPath('tests/fixtures/native-ux')
foreach ($replication in 0,1) {
    $sides = if (($protocol.order -eq 'baseline-first') -xor ($replication -eq 1)) { @('baseline','candidate') } else { @('candidate','baseline') }
    foreach ($scenario in 'close','ownership','resize','pending') {
        foreach ($side in $sides) {
            $environment = @{LENS_NATIVE_COMPARISON_JOURNEY='1';LENS_NATIVE_COMPARISON_MODE=$Mode;
                LENS_NATIVE_COMPARISON_SCENARIO=$scenario;LENS_NATIVE_TRANSITION_TRACE="$replication"}
            Run-BoundedProbe $side "foreground/$replication/$scenario/$side" @($fixtures,'{output}','mixed','1',$protocol.appearance,$protocol.products.$side.revision) $environment
        }
    }
}
foreach ($side in $sides) {
    foreach ($scenario in 'close','switch','resize','switchresize','pending','zoom','selection','findchrome','pendingfind','pendingfindclose','edit','placeholder','cancelnav','findquery') {
        $zooms = if ($side -eq 'candidate' -and $Mode -eq 'Continuous' -and $scenario -in 'close','switch') { @(100,50,75,150,200) } else { @(100) }
        foreach ($zoom in $zooms) {
            $arguments = @('--controlled-comparison',$fixtures,'{output}',"--mode=$Mode","--zoom=$zoom","--scenario=$scenario")
            if ($protocol.appearance -eq 'high-contrast') { $arguments += '--high-contrast' }
            Run-BoundedProbe $side "controls/$scenario-$zoom/$side" $arguments @{}
        }
        if ($scenario -in 'resize','switchresize','zoom','selection','findchrome','pendingfind','pendingfindclose','edit','placeholder','cancelnav','findquery') {
            $arguments = @('--controlled-comparison',$fixtures,'{output}',"--mode=$Mode",'--zoom=100',"--scenario=$scenario",'--compact')
            if ($protocol.appearance -eq 'high-contrast') { $arguments += '--high-contrast' }
            Run-BoundedProbe $side "controls/$scenario-compact/$side" $arguments @{}
        }
    }
}
if (@($outcomes | Where-Object exitCode -ne 0).Count) { throw 'Comparison acceptance failed. Every independent baseline and candidate outcome is retained.' }
