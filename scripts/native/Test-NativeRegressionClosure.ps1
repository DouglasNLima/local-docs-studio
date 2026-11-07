param(
    [ValidateSet('normal','high-contrast')][string]$Appearance = 'normal',
    [ValidateSet('baseline-first','candidate-first')][string]$Order = 'baseline-first',
    [string]$OutputDirectory = 'artifacts/native-regression-closure'
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Foreground input and OS appearance changes require a disposable hosted Windows runner.'
}
$revision = (git rev-parse HEAD).Trim()
$root = [IO.Path]::GetFullPath($OutputDirectory)
& "$PSScriptRoot/Measure-NativeUx.ps1" -BaselineRevision '358d2a6f4b8028461fb535f2f71a9b7c71636760' -CandidateRevision $revision -Appearance $Appearance -Order $Order -Fixtures mixed -Processes 2 -Iterations 1 -OutputDirectory $root -PrepareOnly
$prepared = Get-Content "$root/protocol.json" -Raw | ConvertFrom-Json
$fixtures = [IO.Path]::GetFullPath('tests/fixtures/native-ux')
$sides = if ($Order -eq 'baseline-first') { @('baseline','candidate') } else { @('candidate','baseline') }
$outcomes = [Collections.Generic.List[object]]::new()
@{ schema=1;source=$revision;tree=(git rev-parse 'HEAD^{tree}').Trim();appearance=$Appearance;order=$Order;
    controlledProcesses=8;foregroundProcesses=4;startupSamplesSeconds=@(0,1,5,30,120,220);
    bounds=@{childSeconds=240;privateBytes=2147483648;logBytes=16777216};
    acceptance='Diagnostic collection only; no passing repeat erases an original failure. No performance policy transition.';
    pixels='Exactly three fixed frames at each existing Pages boundary. Traced cells add passive desktop-wide DWM counters. Uninstrumented cells retain the original capture path.'
} | ConvertTo-Json -Depth 6 | Set-Content "$root/closure-protocol.json" -Encoding utf8NoBOM
function Invoke-Cell($label, $side, $arguments, $environment) {
    $directory = Join-Path $root $label
    New-Item -ItemType Directory -Path (Split-Path $directory) -Force | Out-Null
    $product = $prepared.products.$side
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $product.binary 'LensDocsStudio.Native.UxProbe.exe'))
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument.Replace('{output}', $directory)) }
    $info.Environment['LENS_NATIVE_TRANSITION_TRACE']='0'
    foreach ($key in $environment.Keys) { $info.Environment[$key]=$environment[$key] }
    $started = [Diagnostics.Stopwatch]::GetTimestamp()
    $child = [Diagnostics.Process]::Start($info)
    $created = [Diagnostics.Stopwatch]::GetTimestamp()
    $stdout=[IO.File]::Create("$directory-stdout.txt"); $stderr=[IO.File]::Create("$directory-stderr.txt")
    $outTask=$child.StandardOutput.BaseStream.CopyToAsync($stdout); $errTask=$child.StandardError.BaseStream.CopyToAsync($stderr)
    $clock=[Diagnostics.Stopwatch]::StartNew(); $bounded=$null
    $samples = [Collections.Generic.List[object]]::new(); $due = [Collections.Generic.Queue[int]]::new()
    foreach ($second in @(0,1,5,30,120,220)) { $due.Enqueue($second) }
    try {
        while (!$child.WaitForExit(250)) {
            $child.Refresh()
            if ($due.Count -and $clock.Elapsed.TotalSeconds -ge $due.Peek()) {
                $target=$due.Dequeue()
                try { $samples.Add(@{targetSeconds=$target;tick=[Diagnostics.Stopwatch]::GetTimestamp();elapsedMs=$clock.Elapsed.TotalMilliseconds;
                    pid=$child.Id;startTimeUtc=$child.StartTime.ToUniversalTime().ToString('o');cpuMs=$child.TotalProcessorTime.TotalMilliseconds;
                    privateBytes=$child.PrivateMemorySize64;threads=$child.Threads.Count;handles=$child.HandleCount;session=$child.SessionId;
                    mainWindow=$child.MainWindowHandle.ToInt64()}) }
                catch { $samples.Add(@{targetSeconds=$target;tick=[Diagnostics.Stopwatch]::GetTimestamp();observationError=$_.Exception.GetType().FullName}) }
                @{started=$started;created=$created;frequency=[Diagnostics.Stopwatch]::Frequency;samples=@($samples)} |
                    ConvertTo-Json -Depth 6 | Set-Content "$directory-supervision.json" -Encoding utf8NoBOM
            }
            if ($clock.Elapsed.TotalSeconds -gt 240) { $bounded='240-second child supervision limit' }
            if ($child.PrivateMemorySize64 -gt 2147483648) { $bounded='2 GiB child private-memory supervision limit' }
            if ($stdout.Length + $stderr.Length -gt 16777216) { $bounded='16 MiB child log supervision limit' }
            if ($bounded) { $child.Kill($true); $child.WaitForExit(); break }
        }
        $outTask.GetAwaiter().GetResult(); $errTask.GetAwaiter().GetResult()
        $outcomes.Add(@{label=$label;side=$side;product=$product.revision;exitCode=$child.ExitCode;supervisionFailure=$bounded;environment=$environment;
            started=$started;created=$created;finished=[Diagnostics.Stopwatch]::GetTimestamp();elapsedMs=$clock.Elapsed.TotalMilliseconds})
        $outcomes | ConvertTo-Json -Depth 7 | Set-Content "$root/processes.json" -Encoding utf8NoBOM
        Write-Output "$label exit=$($child.ExitCode)"
    } finally { $stdout.Dispose(); $stderr.Dispose(); $child.Dispose() }
}
foreach ($trace in '1','0') {
    foreach ($repeat in 0,1) {
        foreach ($side in $sides) {
            $arguments=@('--controlled-comparison',$fixtures,'{output}','--mode=Continuous','--zoom=100','--scenario=resize','--compact')
            if ($Appearance -eq 'high-contrast') { $arguments+='--high-contrast' }
            Invoke-Cell "startup/trace-$trace/repeat-$repeat/$side" $side $arguments @{LENS_NATIVE_STARTUP_TRACE=$trace;LENS_NATIVE_TRANSITION_TRACE=$trace}
        }
    }
    foreach ($side in $sides) {
        Invoke-Cell "pixels/trace-$trace/$side" $side @($fixtures,'{output}','mixed','1',$Appearance,$prepared.products.$side.revision) @{
            LENS_NATIVE_COMPLETION_JOURNEY='1';LENS_NATIVE_COMPLETION_SCENARIO='pixels';LENS_NATIVE_COMPLETION_READER='primary';
            LENS_NATIVE_COMPARISON_MODE='Pages';LENS_NATIVE_COMPLETION_TRACE=$trace;LENS_NATIVE_COMPOSITION_TRACE=$trace}
    }
}
if ($outcomes.Count -ne 12 -or @($outcomes | Where-Object exitCode -ne 0).Count) {
    throw 'The bounded diagnostic campaign has failed or missing cells; all observations are retained.'
}
