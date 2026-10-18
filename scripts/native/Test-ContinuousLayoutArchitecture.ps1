param(
    [ValidateSet(1,2)][int]$Replication = 1,
    [string]$OutputDirectory = 'artifacts/layout-architecture',
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
New-Item -ItemType Directory -Force -Path $output | Out-Null
if (-not $SkipBuild) {
    & dotnet build src/native/LensDocsStudio.LayoutExperiment -c Release -p:RestoreLockedMode=true
    if ($LASTEXITCODE -ne 0) { throw 'Experiment build failed.' }
}
$exe = (Resolve-Path src/native/LensDocsStudio.LayoutExperiment/bin/Release/net10.0-windows/LensDocsStudio.LayoutExperiment.exe).Path
$source = (& git rev-parse HEAD).Trim()
$env:LENS_LAYOUT_SOURCE_REVISION = $source
$sourceDirty = [bool](& git status --porcelain)
$payloadDirectory = Split-Path -Parent $exe
$provenance = [ordered]@{
    Label='EXPERIMENT ONLY'; Source=$source; SourceDirty=$sourceDirty; Base='a937399a3f822c4e911e1ebe5ec0e426b64c56ac'
    Runtime='Framework-dependent, official .NET 10 Windows Desktop; no framework copied or patched'
    ProductionDefault='Stock FlowDocument/PTS; no application renderer path changed'
    Payload=@(Get-ChildItem -LiteralPath $payloadDirectory -File -Recurse | Where-Object Name -ne 'provenance.json' | ForEach-Object { [ordered]@{ Path=[IO.Path]::GetRelativePath($payloadDirectory,$_.FullName); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash } })
}
$provenance | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $payloadDirectory 'provenance.json')
$results = [Collections.Generic.List[object]]::new()
function Invoke-Sample([string]$Adapter, [string]$Name, [int]$Repetitions = 1, [switch]$Edit, [switch]$Uia, [switch]$Lifecycle) {
    $destination = Join-Path $output "$Name-$Adapter"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $arguments = @('--adapter', $Adapter, '--output', ('"' + $destination + '"'), '--repetitions', $Repetitions)
    if ($Edit) { $arguments += '--edit' }
    if ($Uia) { $arguments += '--uia' }
    if ($Lifecycle) { $arguments += '--lifecycle' }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $destination 'stdout.txt') -RedirectStandardError (Join-Path $destination 'stderr.txt')
    # The external bound still fires if a native UIA/layout operation blocks its dispatcher.
    if (-not $process.WaitForExit(120000)) {
        Stop-Process -Id $process.Id -Force
        @{ Status='FAIL'; Reason='External 120-second process bound'; Name=$Name; Adapter=$Adapter } | ConvertTo-Json | Set-Content (Join-Path $destination 'external-timeout.json')
    }
    $process.Refresh()
    $results.Add([ordered]@{ Name=$Name; Adapter=$Adapter; ExitCode=$process.ExitCode; Directory=$destination })
}
# One compiled executable contains both adapters; Native/Core code is the unchanged
# verified #37 implementation. No product assembly is swapped between samples.
$initialOrder = if ($Replication -eq 1) { @('stock','candidate') } else { @('candidate','stock') }
foreach ($side in $initialOrder) { Invoke-Sample $side 'first' }
for ($cold = 0; $cold -lt 3; $cold++) {
    $order = if ($cold % 2 -eq 0) { $initialOrder } else { @($initialOrder[1],$initialOrder[0]) }
    foreach ($side in $order) { Invoke-Sample $side "cold-$cold" }
}
foreach ($side in $initialOrder) { Invoke-Sample $side 'repeated' 4 }
foreach ($side in @($initialOrder[1],$initialOrder[0])) { Invoke-Sample $side 'distant-edit' 3 -Edit }
Invoke-Sample 'candidate' 'independent-uia' -Uia
Invoke-Sample 'candidate' 'lifecycle' -Lifecycle
$manifest = [ordered]@{
    Source=$source; Base='a937399a3f822c4e911e1ebe5ec0e426b64c56ac'; Dependency='9a64dfa5adf79dc447db1f671ea39c94ec42de40'
    Replication=$Replication; InitialOrder=$initialOrder; HarnessSha256=(Get-FileHash ([IO.Path]::ChangeExtension($exe,'.dll'))).Hash
    SourceDirty=$sourceDirty; Provenance=$provenance
    OfficialRuntime=& dotnet --list-runtimes; OS=[Environment]::OSVersion.ToString(); CpuAvailable=[Environment]::ProcessorCount
    ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion; Runs=$results
    OtherFixtures='NOT IMPLEMENTED: first vertical-slice gate must pass before expansion'
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $output 'protocol.json')
$allSamples = @(Get-ChildItem -LiteralPath $output -Recurse -Filter sample.json | Where-Object { $_.FullName -notmatch 'independent-uia' } | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$runtimeChecks = @($allSamples | Select-Object -First 1 | ForEach-Object {
    foreach ($path in @($_.RuntimeAssembly,$_.WpfFormattingAssembly)) {
        if (-not $path) { continue }
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        [ordered]@{ Path=$path; Sha256=(Get-FileHash -LiteralPath $path).Hash; SignatureStatus=$signature.Status.ToString(); Signer=$signature.SignerCertificate.Subject }
    }
})
$runtimeChecks | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'official-runtime.json')
$candidate = @($allSamples | Where-Object Adapter -eq 'candidate')
$misses = @($candidate | Where-Object { $_.CompleteLayoutInputMilliseconds -gt 250 -or $_.MaximumInputGapMilliseconds -ge 250 })
$failures = @($results | Where-Object ExitCode -ne 0)
& python scripts/native/compare-layout-geometry.py (Join-Path $output 'first-stock/sample-0/geometry.json') (Join-Path $output 'first-candidate/sample-0/geometry.json') (Join-Path $output 'geometry-comparison.json')
$geometryExit = $LASTEXITCODE
[ordered]@{
    Status='FAIL'; FullAcceptance='INCOMPLETE'; CandidateSamples=$candidate.Count; CandidateTimingFailures=$misses.Count
    ExecutionFailures=$failures; GeometryExit=$geometryExit; ThresholdMilliseconds=250
    Decision=if ($misses.Count -gt 0) {'NOT VIABLE: demonstrated complete-layout timing blocker for tested design/host'} else {'INSUFFICIENT EVIDENCE: required parity remains incomplete'}
    Samples=$allSamples; Missing='All-six parity, complete clipboard parity, appearance and product integration are not certified by this list gate.'
} | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $output 'acceptance.json')
throw 'Prototype acceptance FAIL; all raw samples and missing parity are retained. Do not interpret a timing-only pass as full acceptance.'
