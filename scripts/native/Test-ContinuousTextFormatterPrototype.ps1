param(
    [string]$OutputDirectory = 'artifacts/continuous-textformatter-prototype',
    [ValidateSet(1,2)][int]$Replication = 1
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory; previous outcomes are retained.' }
New-Item -ItemType Directory -Path $output | Out-Null
$revision = (git rev-parse HEAD).Trim()
$tree = (git rev-parse 'HEAD^{tree}').Trim()
git diff --exit-code HEAD
if ($LASTEXITCODE -ne 0) { throw 'Commit the diagnostic before a controlled comparison.' }
$develop = '86d60322f6f1f1dec06974bf5f9e1738a3bb80c7'
git diff --exit-code $develop HEAD -- src/native scripts/native/Test-ContinuousPreviewPerformance.ps1 .github/workflows/native-continuous-performance.yml scripts/native/Invoke-ContinuousPreviewInteractions.ps1
if ($LASTEXITCODE -ne 0) { throw 'The product or protected acceptance has changed; this isolated comparison is not valid.' }
dotnet build scripts/native/experiments/ContinuousTextFormatter/ContinuousTextFormatter.csproj -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Prototype compilation failed.' }
$executable = [IO.Path]::GetFullPath('scripts/native/experiments/ContinuousTextFormatter/bin/Release/net10.0-windows/ContinuousTextFormatter.exe')
$harness = Join-Path (Split-Path $executable) 'ContinuousTextFormatter.dll'
$env:LENS_CONTINUOUS_SOURCE_REVISION = $revision
$nativeTree = (git rev-parse 'HEAD:src/native').Trim()
[ordered]@{
    SourceRevision=$revision; SourceTree=$tree; UnchangedProductBaseline=$develop; NativeSourceTree=$nativeTree
    HistoricalProtectedBaseline='0237b08390d5d1d946c0b19b81d671ccd54fb951'
    HarnessSha256=(Get-FileHash -LiteralPath $harness -Algorithm SHA256).Hash
    Replication=$Replication; ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion
    OS=[Environment]::OSVersion.ToString(); Runtime=(dotnet --list-runtimes)
    Processor=(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors)
    Policy='List-only cost diagnostic; same compiled executable/product on both engines. No prewarm or forced GC. First process; three cold processes; four repeated full constructions; three list-grammar fresh-source revisions. Replication 2 reverses initial engine order. Equal original-renderer correctness construction after every timed sample on both sides can affect later repeated-process temperature. Original protected six-fixture/66-sample protocol remains separate and unchanged.'
    Completion='All TextLines, full native marker metrics, ink extents and text bounds before queued layout Input; viewport-only drawing. Selection/copy/document UIA absent: no milestone-one or full-acceptance claim.'
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'protocol.json')
$runs = [Collections.Generic.List[object]]::new()
function Invoke-Sample([string]$engine, [string]$group, [int]$count = 1, [bool]$edited = $false) {
    $directory = Join-Path $output "samples/$group/$engine"
    $arguments = @($engine, ('"' + $directory + '"'), $count.ToString([Globalization.CultureInfo]::InvariantCulture))
    if ($edited) { $arguments += '--edited' }
    $logDirectory = Join-Path $output "process-logs/$group"
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logDirectory "$engine-stdout.txt") -RedirectStandardError (Join-Path $logDirectory "$engine-stderr.txt")
    if (-not $process.WaitForExit(120000)) {
        [IO.File]::WriteAllText((Join-Path $logDirectory "$engine-timeout.txt"), 'FAIL: diagnostic exceeded the independent 120-second process bound; no result is inferred.')
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        throw "Diagnostic process timeout: $group/$engine. Retain the original failure."
    }
    $process.Refresh()
    $code = $process.ExitCode
    Get-Content -LiteralPath (Join-Path $logDirectory "$engine-stdout.txt")
    if ($code -ne 0) { Get-Content -LiteralPath (Join-Path $logDirectory "$engine-stderr.txt") }
    $runs.Add(@{Engine=$engine;Group=$group;ExitCode=$code})
    if ($code -ne 0) { throw "Diagnostic correctness/harness failure: $group/$engine. Original evidence is retained." }
}
$order = if ($Replication -eq 1) { @('baseline','prototype') } else { @('prototype','baseline') }
foreach ($engine in $order) { Invoke-Sample $engine 'first-execution' }
for ($index=0; $index -lt 3; $index++) {
    $coldOrder = if (($index + $Replication) % 2 -eq 0) { @('prototype','baseline') } else { @('baseline','prototype') }
    foreach ($engine in $coldOrder) { Invoke-Sample $engine "cold-$index" }
}
foreach ($engine in $order) { Invoke-Sample $engine 'repeated' 4 }
foreach ($engine in $order) { Invoke-Sample $engine 'fresh-source' 3 $true }
$pairs = [Collections.Generic.List[object]]::new()
foreach ($group in Get-ChildItem -LiteralPath (Join-Path $output 'samples') -Directory) {
    foreach ($sample in Get-ChildItem -LiteralPath (Join-Path $group.FullName 'baseline') -Filter 'sample-*.json') {
        $before = Get-Content -LiteralPath $sample.FullName -Raw | ConvertFrom-Json
        $after = Get-Content -LiteralPath (Join-Path $group.FullName "prototype/$($sample.Name)") -Raw | ConvertFrom-Json
        foreach ($field in 'FixtureSha256','SourceSha256','HarnessSha256','LogicalTextSha256','Lists','Items','Paragraphs','SourceIntervals') {
            if ($before.$field -ne $after.$field) { throw "Independent paired $field differs: $($group.Name)/$($sample.Name)" }
        }
        if ($before.Correctness -ne 'PASS' -or $after.Correctness -ne 'PASS') { throw 'Independent correctness failed.' }
        $pairs.Add(@{Group=$group.Name;Sample=$sample.Name;Before=$before;After=$after})
    }
}
if ($pairs.Count -ne 11) { throw 'The diagnostic lost a prescribed sample.' }
# Deterministic adversarial/oracle checks run after every timing sample, never as prewarming.
& $executable self-check (Join-Path $output 'correctness')
if ($LASTEXITCODE -ne 0) { throw 'Independent deterministic checks failed.' }
$failed = @($pairs | Where-Object { $_.After.LayoutBudgetObservation -ne 'PASS' })
[ordered]@{
    Classification='CONTINUOUS_NATIVE_PROTOTYPE_PARTIAL'; ProtectedGateEligible=$false
    PrototypePerformanceVerified=$false; ProductIntegration='ABSENT'; ThresholdMilliseconds=250
    PrototypeSamples=$pairs.Count; FailingBudgetObservations=$failed.Count; Correctness='PASS'
    Selection='NOT IMPLEMENTED'; Copy='NOT IMPLEMENTED'; ActualUIA='NOT IMPLEMENTED / NOT TESTED'
    Pairs=$pairs; ProcessRuns=$runs
} | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath (Join-Path $output 'comparison.json')
if ($failed.Count) { throw "$($failed.Count)/11 prototype observations miss the unchanged 250 ms budget. PARTIAL diagnostic; full acceptance remains FAIL." }
