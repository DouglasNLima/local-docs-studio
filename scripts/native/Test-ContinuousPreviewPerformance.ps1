param(
    [string]$BaselineRevision = '9a64dfa5adf79dc447db1f671ea39c94ec42de40',
    [string]$OutputDirectory = 'artifacts/continuous-performance',
    [ValidateSet(1,2)][int]$Replication = 1
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory; previous samples are never replaced.' }
New-Item -ItemType Directory -Path $output | Out-Null
$candidateRevision = (git rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify candidate source.' }
git diff --exit-code HEAD
if ($LASTEXITCODE -ne 0) { throw 'The controlled comparison requires clean tracked source.' }
$baselineSource = Join-Path $output 'baseline-source'
git worktree add --detach $baselineSource $BaselineRevision
if ($LASTEXITCODE -ne 0) { throw 'Cannot create immutable baseline checkout.' }
dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Candidate compilation failed.' }
dotnet build (Join-Path $baselineSource 'src/native/LensDocsStudio.Native/LensDocsStudio.Native.csproj') -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Baseline compilation failed.' }
$harness = [IO.Path]::GetFullPath('src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows')
$baselineBinary = Join-Path $output 'baseline-binaries'
Copy-Item -LiteralPath $harness -Destination $baselineBinary -Recurse
# Both sides execute the identical compiled validation harness. Only the product
# assemblies/resources come from the baseline. Package versions are identical.
Copy-Item -Path (Join-Path $baselineSource 'src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/*') -Destination $baselineBinary -Force -Recurse
$executables = @{
    baseline = Join-Path $baselineBinary 'LensDocsStudio.Native.Validation.exe'
    candidate = Join-Path $harness 'LensDocsStudio.Native.Validation.exe'
}
$hashes = @{}
foreach ($side in 'baseline','candidate') {
    $hashes[$side] = (Get-FileHash -LiteralPath (Join-Path (Split-Path $executables[$side]) 'LensDocsStudio.Native.Validation.dll') -Algorithm SHA256).Hash
}
if ($hashes.baseline -ne $hashes.candidate) { throw 'Harness binary mismatch.' }
[ordered]@{
    BaselineRevision=$BaselineRevision; CandidateRevision=$candidateRevision; HarnessSha256=$hashes.candidate
    Replication=$Replication; ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion
    Processor=(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors)
    OS=[Environment]::OSVersion.ToString(); Runtime=(dotnet --list-runtimes)
    Policy='No prewarm/forced GC. First execution retained. Three cold processes per fixture/side; alternate pair order. Four repeated complete constructions and three fresh distant-source revisions. Same fixed six-fixture order. Replication 2 reverses initial order. Independent correctness follows every timed sample.'
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'protocol.json')
$runs = [Collections.Generic.List[object]]::new()
function Invoke-Sample([string]$side, [string]$group, [string]$scenario, [string[]]$extra = @()) {
    $directory = Join-Path $output "samples/$group/$side"
    $env:LENS_CONTINUOUS_SOURCE_REVISION = if ($side -eq 'baseline') { $BaselineRevision } else { $candidateRevision }
    & $executables[$side] --continuous-performance $directory $scenario @extra
    $code = $LASTEXITCODE
    $runs.Add(@{Side=$side;Group=$group;ExitCode=$code})
    if ($code -ne 0) { throw "Correctness/harness failure in $group/$side. Retain all original output." }
}
$sides = if ($Replication -eq 1) { @('baseline','candidate') } else { @('candidate','baseline') }
foreach ($side in $sides) { Invoke-Sample $side 'first-execution' 'all' }
for ($iteration=0; $iteration -lt 3; $iteration++) {
    $order = if (($iteration + $Replication) % 2 -eq 0) { @('candidate','baseline') } else { @('baseline','candidate') }
    foreach ($fixture in 'list','table','quote','details','paragraphs','mixed') {
        foreach ($side in $order) { Invoke-Sample $side "cold-$iteration-$fixture" $fixture }
    }
}
foreach ($side in $sides) { Invoke-Sample $side 'repeated' 'all' @('--repeat','4') }
foreach ($side in $sides) { Invoke-Sample $side 'fresh-source' 'all' @('--repeat','3','--edited') }
$pairs = [Collections.Generic.List[object]]::new()
$candidateSamples = [Collections.Generic.List[object]]::new()
foreach ($group in Get-ChildItem -LiteralPath (Join-Path $output 'samples') -Directory) {
    $baselineDirectory = Join-Path $group.FullName 'baseline'
    foreach ($file in Get-ChildItem -LiteralPath $baselineDirectory -Filter '*.json' -Recurse | Where-Object Name -notin @('acceptance.json','lifecycle-observation.json')) {
        $relative = [IO.Path]::GetRelativePath($baselineDirectory,$file.FullName)
        $candidateFile = Join-Path $group.FullName "candidate/$relative"
        $before = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $after = Get-Content -LiteralPath $candidateFile -Raw | ConvertFrom-Json
        foreach ($field in 'FixtureSha256','SourceSha256','HarnessSha256','LogicalTextSha256','SourceElementCount') {
            if ($before.$field -ne $after.$field) { throw "Independent $field comparison failed: $($group.Name)/$relative" }
        }
        if (($before.Structure | ConvertTo-Json -Compress) -ne ($after.Structure | ConvertTo-Json -Compress) -or
            $before.Correctness -ne 'PASS' -or $after.Correctness -ne 'PASS') { throw 'Independent structure/native navigation correctness failed.' }
        $beforePng = Join-Path $file.DirectoryName ($file.BaseName + '-initial.png')
        $afterPng = Join-Path (Split-Path $candidateFile) ($file.BaseName + '-initial.png')
        $visualEqual = (Get-FileHash -LiteralPath $beforePng).Hash -eq (Get-FileHash -LiteralPath $afterPng).Hash
        $pairs.Add(@{Group=$group.Name;Sample=$relative;VisualEqual=$visualEqual;Before=$before;After=$after})
        $candidateSamples.Add($after)
    }
}
$failed = @($candidateSamples | Where-Object { $_.FirstLayoutAcceptance -ne 'PASS' -or $_.InputAcceptance -ne 'PASS' })
[ordered]@{
    PerformanceAcceptance= $(if ($failed.Count) { 'FAIL' } else { 'PASS' })
    ThresholdMilliseconds=250; Correctness='PASS'; CandidateSampleCount=$candidateSamples.Count
    FailedSamples=$failed.Count; Pairs=$pairs; ProcessRuns=$runs
} | ConvertTo-Json -Depth 35 | Set-Content -LiteralPath (Join-Path $output 'comparison.json')
if ($failed.Count) { throw "$($failed.Count) candidate samples fail unchanged 250 ms acceptance. Implementation is bounded; this performance goal is INCOMPLETE." }
