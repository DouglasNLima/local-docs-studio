param(
    [string]$OutputDirectory = 'artifacts/preview-completion',
    [ValidateSet('Continuous','Automatic','Pages')][string]$Mode = 'Continuous',
    [ValidateSet('normal','high-contrast')][string]$Appearance = 'normal'
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Real foreground input and OS appearance changes require a disposable hosted Windows runner.'
}
$evidence = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $evidence) { throw 'Use fresh evidence; never replace a failed observation.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
git diff --exit-code HEAD
if ($LASTEXITCODE) { throw 'Exact committed source is required.' }
$revision = (git rev-parse HEAD).Trim()
$binary = [IO.Path]::GetFullPath('src/native/LensDocsStudio.Native.UxProbe/bin/Release/net10.0-windows')
$fixtures = [IO.Path]::GetFullPath('tests/fixtures/native-ux')
$protocol = [ordered]@{source=$revision;tree=(git rev-parse 'HEAD^{tree}').Trim();startingHead='06073c0bf5a10da6b93c8a26d6d2a8c89b4ce41d';mode=$Mode;appearance=$Appearance;
    binaries=@('LensDocsStudio.Native.UxProbe.dll','LensDocsStudio.Native.dll','LensDocsStudio.Core.dll') | ForEach-Object { @{file=$_;sha256=(Get-FileHash (Join-Path $binary $_)).Hash} };
    fixtureSha256=(Get-FileHash "$fixtures/mixed.md").Hash;manifestSha256=(Get-FileHash "$fixtures/manifest.json").Hash;
    foregroundProcesses=11;controlledProcesses=4;fixedFramesPerBoundary=3;pixelJourneyBoundaries=7;
    observationSeconds=30;childSeconds=240;privateBytes=2147483648;logBytes=16777216;
    qualification='An actual unfinished anchor task at WPF delivery, the same uncancelled layout token, subsequent cancellation and rejection; a late arrival is INCONCLUSIVE.';
    pixelProtocol='Every boundary retains exactly three frames. First after semantic readiness and Background; second after observed natural WPF commit in traced runs; third at the next DWM capture. Active-reader Find and all-reader readiness are separate boundaries; both are retained. No pixel-dependent retries.';
    uninstrumented='No completion EventSource, property, input or compositor subscriptions. Functional and pixel evidence only; no pending-interruption qualification.';
    noProductDelay=$true;image=$env:ImageVersion;runtimes=(dotnet --list-runtimes)}
$protocol | ConvertTo-Json -Depth 8 | Set-Content "$evidence/protocol.json" -Encoding utf8NoBOM
$outcomes = [Collections.Generic.List[object]]::new()
function Invoke-BoundedCompletion($label, $arguments, $environment) {
    $directory = Join-Path $evidence $label
    New-Item -ItemType Directory -Path (Split-Path $directory) -Force | Out-Null
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $binary 'LensDocsStudio.Native.UxProbe.exe'))
    $info.UseShellExecute=$false; $info.CreateNoWindow=$true; $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument.Replace('{output}', $directory)) }
    $info.Environment['LENS_NATIVE_TRANSITION_TRACE']='0'
    foreach ($key in $environment.Keys) { $info.Environment[$key]=$environment[$key] }
    $child = [Diagnostics.Process]::Start($info)
    $stdout=[IO.File]::Create("$directory-stdout.txt"); $stderr=[IO.File]::Create("$directory-stderr.txt")
    $outTask=$child.StandardOutput.BaseStream.CopyToAsync($stdout); $errTask=$child.StandardError.BaseStream.CopyToAsync($stderr)
    $watch=[Diagnostics.Stopwatch]::StartNew(); $bounded=$null
    while (!$child.WaitForExit(250)) {
        $child.Refresh()
        if ($watch.Elapsed.TotalSeconds -gt 240) { $bounded='240-second child supervision limit' }
        if ($child.PrivateMemorySize64 -gt 2147483648) { $bounded='2 GiB child private-memory supervision limit' }
        if ($stdout.Length + $stderr.Length -gt 16777216) { $bounded='16 MiB child log supervision limit' }
        if ($bounded) { $child.Kill($true); $child.WaitForExit(); break }
    }
    try { $outTask.GetAwaiter().GetResult(); $errTask.GetAwaiter().GetResult() } finally { $stdout.Dispose(); $stderr.Dispose() }
    $outcomes.Add(@{label=$label;exitCode=$child.ExitCode;supervisionFailure=$bounded;environment=$environment})
    $outcomes | ConvertTo-Json -Depth 6 | Set-Content "$evidence/processes.json" -Encoding utf8NoBOM
    Write-Output "$label exit=$($child.ExitCode)"
}
foreach ($trace in '1','0') {
    foreach ($scenario in 'close','switch','find','pixels') {
        $readers = if ($trace -eq '1' -and $scenario -ne 'pixels') { @('primary','secondary') } else { @('primary') }
        foreach ($reader in $readers) {
            Invoke-BoundedCompletion "foreground/trace-$trace/$scenario-$reader" @($fixtures,'{output}','mixed','1',$Appearance,$revision) @{
                LENS_NATIVE_COMPLETION_JOURNEY='1';LENS_NATIVE_COMPLETION_TRACE=$trace;LENS_NATIVE_COMPLETION_SCENARIO=$scenario;
                LENS_NATIVE_COMPLETION_READER=$reader;LENS_NATIVE_COMPARISON_MODE=$Mode}
        }
    }
}
foreach ($scenario in 'pendingfind','pendingfindclose','cancelnav','findquery') {
    $arguments=@('--controlled-comparison',$fixtures,'{output}',"--mode=$Mode",'--zoom=100',"--scenario=$scenario",'--completion-trace')
    if ($Appearance -eq 'high-contrast') { $arguments+='--high-contrast' }
    Invoke-BoundedCompletion "controls/$scenario" $arguments @{LENS_NATIVE_COMPLETION_JOURNEY='1'}
}
if (@($outcomes | Where-Object exitCode -ne 0).Count) { throw 'Completion collection has failed cells; every result is retained.' }
