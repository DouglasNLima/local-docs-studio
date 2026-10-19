param([string]$OutputDirectory = 'artifacts/layout-input-profiles')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') { throw 'Native/OS profiling requires a disposable hosted desktop.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a fresh profile directory.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$wpr = Get-Command wpr.exe -ErrorAction SilentlyContinue
if (-not $wpr) { @{ Status='UNAVAILABLE'; Reason='Built-in WPR not present'; NativeAttribution='Unavailable, not inferred from managed timings' } | ConvertTo-Json | Set-Content "$OutputDirectory/availability.json"; exit 0 }
& $wpr.Source -profiles | Set-Content "$OutputDirectory/wpr-profiles.txt"
$exe = (Resolve-Path artifacts/layout-input-bin/LensDocsStudio.LayoutExperiment.exe).Path
$env:LENS_LAYOUT_SOURCE_REVISION = (git rev-parse HEAD); $env:LENS_LAYOUT_SOURCE_DIRTY = ([bool](git status --porcelain)).ToString(); $env:LENS_LAYOUT_COMPILATION = 'ReadyToRun'
$cases = @(
    @{Name='reading-candidate'; Arguments=@('--reading-input-checks','--adapter','candidate','--client-apartment','STA')},
    @{Name='reading-stock'; Arguments=@('--reading-input-checks','--adapter','stock','--client-apartment','STA')},
    @{Name='appearance-candidate'; Arguments=@('--appearance')},
    @{Name='appearance-stock'; Arguments=@('--appearance-stock-control')},
    @{Name='appearance-minimal'; Arguments=@('--appearance-controls')},
    @{Name='media-candidate'; Arguments=@('--media-checks')},
    @{Name='media-controls'; Arguments=@('--media-controls')}
)
$rows = [Collections.Generic.List[object]]::new()
foreach ($case in $cases) {
    $directory = [IO.Path]::GetFullPath((Join-Path $OutputDirectory $case.Name)); New-Item -ItemType Directory -Path $directory | Out-Null
    # WPR memory mode uses bounded circular buffers. Stop/flush is outside the
    # child operation. Never change profile intervals, executive paging or shell settings.
    & $wpr.Source -start GeneralProfile -start CPU *> "$directory/wpr-start.txt"
    $started = $LASTEXITCODE -eq 0
    if (-not $started) { $rows.Add(@{Name=$case.Name; Status='WPR_UNAVAILABLE'; ExitCode=$LASTEXITCODE}); break }
    try {
        [string[]]$arguments = $case.Arguments + @('--detailed-input-trace','--output',('"' + $directory + '/observations"'))
        $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput "$directory/stdout.txt" -RedirectStandardError "$directory/stderr.txt"
        if (-not $process.WaitForExit(120000)) { Stop-Process -Id $process.Id -Force; 'External 120-second profile bound' | Set-Content "$directory/deadline-failure.txt" }
        $process.Refresh(); $rows.Add(@{Name=$case.Name; ExitCode=$process.ExitCode; DiagnosticOnly=$true; Arguments=$arguments})
        & $wpr.Source -status collectors -details *> "$directory/wpr-status.txt"
    }
    finally { & $wpr.Source -stop "$directory/native.etl" 'PR38 bounded input diagnostic; not acceptance' -skipPdbGen -compress *> "$directory/wpr-stop.txt" }
}
@{ DiagnosticOnly=$true; Source=(git rev-parse HEAD); SourceDirty=$env:LENS_LAYOUT_SOURCE_DIRTY; Runs=$rows; Meaning='Bounded native CPU/scheduling controls; profiler overhead is not acceptance. No target retries or native setting changes.' } | ConvertTo-Json -Depth 8 | Set-Content "$OutputDirectory/profiles.json"
