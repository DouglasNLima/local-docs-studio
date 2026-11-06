param(
    [string]$BaselineRevision = '99349d82eb06960eb1f9785cf384db2373aed4fa',
    [string]$OutputDirectory = 'artifacts/first-layout-attribution',
    [ValidateSet('baseline-first','candidate-first')][string]$Order = 'baseline-first'
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory; never replace observations.' }
New-Item -ItemType Directory -Path $output | Out-Null
$candidate = (git rev-parse HEAD).Trim()
git diff --exit-code HEAD
if ($LASTEXITCODE -ne 0) { throw 'Profile clean, committed source.' }

# The existing measurement executable, fixtures and original acceptance script are
# immutable here. This supervisor only launches them and observes existing events.
$unchanged = @(
    'src/native/LensDocsStudio.Native.Validation/NativeContinuousPerformanceChecks.cs',
    'src/native/LensDocsStudio.Native.Validation/ContinuousPreviewFixtures.cs',
    'src/native/LensDocsStudio.Native.Validation/ContinuousHarnessEvents.cs',
    'scripts/native/Test-ContinuousPreviewPerformance.ps1',
    '.github/workflows/native-continuous-performance.yml'
)
foreach ($path in $unchanged) {
    git diff --exit-code 715bdc58c8c53e5bb15c607d1a961949ebf57c09 HEAD -- $path
    if ($LASTEXITCODE -ne 0) { throw "Original measurement contract changed: $path" }
}
dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Candidate build failed.' }
$baselineSource = Join-Path $output 'baseline-source'
git worktree add --detach $baselineSource $BaselineRevision
if ($LASTEXITCODE -ne 0) { throw 'Cannot create exact baseline checkout.' }
dotnet build "$baselineSource/src/native/LensDocsStudio.Native/LensDocsStudio.Native.csproj" -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Baseline build failed.' }
$harness = [IO.Path]::GetFullPath('src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows')
$baseline = Join-Path $output 'baseline-binaries'
Copy-Item -LiteralPath $harness -Destination $baseline -Recurse
Copy-Item -Path "$baselineSource/src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/*" -Destination $baseline -Force -Recurse
$binaries = @{ baseline=$baseline; candidate=$harness }
$harnessHash = (Get-FileHash "$harness/LensDocsStudio.Native.Validation.dll").Hash
if ($harnessHash -ne (Get-FileHash "$baseline/LensDocsStudio.Native.Validation.dll").Hash) { throw 'Harness binaries differ.' }
$toolDirectory = Join-Path $output 'tools'
dotnet tool install dotnet-trace --version 10.0.750501 --tool-path $toolDirectory
if ($LASTEXITCODE -ne 0) { throw 'Pinned profiler installation failed.' }
$traceTool = Join-Path $toolDirectory 'dotnet-trace.exe'
$traceAssembly = @(Get-ChildItem "$toolDirectory/.store/dotnet-trace/10.0.750501" -Filter Microsoft.Diagnostics.Tracing.TraceEvent.dll -Recurse)
if ($traceAssembly.Count -ne 1) { throw 'Ambiguous pinned analyser library.' }
$project = 'scripts/native/diagnostics/FirstLayoutTrace/FirstLayoutTrace.csproj'
dotnet build $project -c Release "-p:TraceEventAssembly=$($traceAssembly[0].FullName)"
if ($LASTEXITCODE -ne 0) { throw 'Attribution analyser build failed.' }
$analyser = [IO.Path]::GetFullPath('scripts/native/diagnostics/FirstLayoutTrace/bin/Release/net10.0/FirstLayoutTrace.dll')
dotnet $analyser --self-test
if ($LASTEXITCODE -ne 0) { throw 'Attribution controls failed.' }
$sides = if ($Order -eq 'baseline-first') { @('baseline','candidate') } else { @('candidate','baseline') }
[ordered]@{
    Status='DIAGNOSTIC_ONLY'; Candidate=$candidate; Baseline=$BaselineRevision; Order=$sides
    CandidateTree=(git rev-parse 'HEAD^{tree}').Trim(); HarnessSha256=$harnessHash
    TraceToolVersion='10.0.750501'; TraceLibrarySha256=(Get-FileHash $traceAssembly[0].FullName).Hash
    ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion; Runtime=(dotnet --list-runtimes)
    Processor=(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors)
    Products=@{ baseline=(Get-FileHash "$baseline/LensDocsStudio.Native.dll").Hash; candidate=(Get-FileHash "$harness/LensDocsStudio.Native.dll").Hash }
    Policy='Both products: fresh untraced process, then fresh sampled process, two complete constructions of all six unchanged fixtures per process. No prewarm, forced GC, appearance manipulation, input retries or runtime patch. All samples retained. Not the independent 66-sample acceptance protocol.'
    Bounds='120 seconds per child; 32 MiB EventPipe buffer; 256 MiB retained trace limit; analyser cardinality/event limits. These are diagnostic resource bounds, not acceptance deadlines.'
} | ConvertTo-Json -Depth 8 | Set-Content "$output/protocol.json"
$failures = [Collections.Generic.List[string]]::new()
$priorSource = $env:LENS_CONTINUOUS_SOURCE_REVISION
function Invoke-Bounded([string]$file, [string[]]$arguments, [string]$directory) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput "$directory/stdout.txt" -RedirectStandardError "$directory/stderr.txt"
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $expired = $false
    while (-not $process.WaitForExit(500)) {
        $bytes = @(Get-ChildItem -LiteralPath $directory -Filter '*.nettrace' | Where-Object Length -gt 268435456)
        if ($watch.Elapsed.TotalSeconds -ge 120 -or $bytes.Count -gt 0) {
            $expired = $true
            $process.Kill($true)
            $process.WaitForExit()
            break
        }
    }
    [ordered]@{ ProcessId=$process.Id; ExitCode=$process.ExitCode; DiagnosticBoundExceeded=$expired; ElapsedMilliseconds=$watch.Elapsed.TotalMilliseconds } |
        ConvertTo-Json | Set-Content "$directory/process.json"
    if ($expired -or $process.ExitCode -ne 0) { throw 'Diagnostic child failed; retain its partial output.' }
}
try {
    foreach ($kind in 'untraced','sampled') {
        foreach ($side in $sides) {
            $directory = Join-Path $output "$kind/$side"
            New-Item -ItemType Directory -Path $directory -Force | Out-Null
            $env:LENS_CONTINUOUS_SOURCE_REVISION = if ($side -eq 'baseline') { $BaselineRevision } else { $candidate }
            $executable = Join-Path $binaries[$side] 'LensDocsStudio.Native.Validation.exe'
            # Native command-line arguments are paths generated under this checkout;
            # quote each path for spaces without invoking an intermediate shell.
            $child = @('--continuous-performance', ('"' + "$directory/samples" + '"'), 'all', '--repeat', '2')
            try {
                if ($kind -eq 'untraced') { Invoke-Bounded $executable $child $directory }
                else {
                    $tracePath = "$directory/layout.nettrace"
                    $arguments = @('collect','--profile','dotnet-sampled-thread-time','--providers','LensDocsStudio-Native-ContinuousHarness:0xFFFFFFFF:5,LensDocsStudio-Native-PreviewConstruction:0xFFFFFFFF:5',
                        '--buffersize','32','--format','Speedscope','--output',('"'+$tracePath+'"'),'--show-child-io','--',('"'+$executable+'"')) + $child
                    Invoke-Bounded $traceTool $arguments $directory
                    dotnet $analyser $tracePath "$directory/attribution.json"
                    if ($LASTEXITCODE -ne 0) { throw 'Sampled attribution is incomplete.' }
                }
            }
            catch { $failures.Add("$kind/$side : $_"); Write-Warning $_ }
        }
    }
}
finally { $env:LENS_CONTINUOUS_SOURCE_REVISION = $priorSource }
[ordered]@{ Status=$(if($failures.Count){'FAIL'}else{'COLLECTED'}); Failures=@($failures); Acceptance='NOT EVALUATED BY THIS DIAGNOSTIC' } |
    ConvertTo-Json -Depth 4 | Set-Content "$output/completion.json"
if ($failures.Count) { throw 'One or more diagnostic executions failed; no missing evidence is a pass.' }
