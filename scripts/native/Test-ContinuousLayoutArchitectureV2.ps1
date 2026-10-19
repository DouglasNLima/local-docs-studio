param(
    [ValidateSet(1,2)][int]$Replication = 1,
    [string]$OutputDirectory = 'artifacts/layout-architecture-v2',
    [switch]$SkipBuild,
    [switch]$FirstOnly
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
$env:LENS_LAYOUT_SOURCE_DIRTY = $sourceDirty.ToString()
$payloadDirectory = Split-Path -Parent $exe
$provenance = [ordered]@{
    Label='EXPERIMENT ONLY'; Contract='continuous-layout-v2'; Source=$source; SourceDirty=$sourceDirty
    Base='a937399a3f822c4e911e1ebe5ec0e426b64c56ac'; Dependency='9a64dfa5adf79dc447db1f671ea39c94ec42de40'
    Runtime='Framework-dependent official unmodified .NET 10 Windows Desktop'
    ProductionDefault='Stock FlowDocument/PTS; experimental project outside ordinary packages'
    Payload=@(Get-ChildItem -LiteralPath $payloadDirectory -File -Recurse | Where-Object Name -ne 'provenance.json' | ForEach-Object { [ordered]@{ Path=[IO.Path]::GetRelativePath($payloadDirectory,$_.FullName); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash } })
}
$provenance | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $payloadDirectory 'provenance.json')
$runs = [Collections.Generic.List[object]]::new()
function Invoke-Sample([string]$Fixture, [string]$Adapter, [string]$Name, [int]$Repetitions = 1, [switch]$Edit, [switch]$Uia) {
    $destination = Join-Path $output "$Fixture/$Name-$Adapter"
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    $arguments = @('--contract-v2','--scenario',$Fixture,'--adapter',$Adapter,'--output',('"' + $destination + '"'),'--repetitions',$Repetitions)
    if ($Edit) { $arguments += '--edit' }
    if ($Uia) { $arguments += '--uia' }
    $env:LENS_LAYOUT_RUN_GROUP = $Name
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $destination 'stdout.txt') -RedirectStandardError (Join-Path $destination 'stderr.txt')
    if (-not $process.WaitForExit(120000)) {
        Stop-Process -Id $process.Id -Force
        @{ Status='FAIL'; Reason='External 120-second process bound' } | ConvertTo-Json | Set-Content (Join-Path $destination 'external-timeout.json')
    }
    $process.Refresh()
    $runs.Add([ordered]@{ Fixture=$Fixture; Adapter=$Adapter; Name=$Name; ExitCode=$process.ExitCode; Directory=$destination })
}
$order = if ($Replication -eq 1) { @('stock','candidate') } else { @('candidate','stock') }
$fixtures = @('list','table','quote','details','paragraphs','mixed')
# Preserve the original first combined execution and fixed fixture order in one process per side.
foreach ($side in $order) { Invoke-Sample 'all' $side 'first-combined' }
if (-not $FirstOnly) {
    foreach ($fixture in $fixtures) {
        for ($cold = 0; $cold -lt 3; $cold++) {
            $pair = if ($cold % 2 -eq 0) { $order } else { @($order[1],$order[0]) }
            foreach ($side in $pair) { Invoke-Sample $fixture $side "cold-$cold" }
        }
    }
    foreach ($side in $order) { Invoke-Sample 'all' $side 'repeated' 4 }
    foreach ($side in $order) { Invoke-Sample 'all' $side 'distant-edit' 3 -Edit }
    foreach ($fixture in $fixtures) { Invoke-Sample $fixture 'candidate' 'independent-uia' -Uia }
    foreach ($mode in @('lifecycle','appearance')) {
        $destination = Join-Path $output $mode; New-Item -ItemType Directory -Force -Path $destination | Out-Null
        $process = Start-Process -FilePath $exe -ArgumentList @("--$mode",'--output',('"' + $destination + '"')) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $destination 'stdout.txt') -RedirectStandardError (Join-Path $destination 'stderr.txt')
        if (-not $process.WaitForExit(120000)) { Stop-Process -Id $process.Id -Force }
        $process.Refresh(); $runs.Add([ordered]@{ Name=$mode; ExitCode=$process.ExitCode; Directory=$destination })
    }
}
$samples = @(Get-ChildItem -LiteralPath $output -Recurse -Filter sample.json | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$runtimeChecks = @($samples | Select-Object -First 1 | ForEach-Object {
    foreach ($path in @($_.RuntimeAssembly,$_.WpfFormattingAssembly)) {
        $signature = Get-AuthenticodeSignature -LiteralPath $path
        [ordered]@{ Path=$path; Sha256=(Get-FileHash -LiteralPath $path).Hash; SignatureStatus=$signature.Status.ToString(); Signer=$signature.SignerCertificate.Subject }
    }
})
$runtimeChecks | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'official-runtime.json')
$candidate = @($samples | Where-Object Adapter -eq 'candidate')
$layoutMisses = @($candidate | Where-Object CompleteLayoutInputMilliseconds -gt 1000)
$presentationMisses = @($candidate | Where-Object EndToEndReadinessMilliseconds -gt 1500)
$inputMisses = @($candidate | Where-Object { $_.MaximumInputGapMilliseconds -gt 100 -or $_.MaximumInteractionInputGapMilliseconds -gt 100 })
[ordered]@{
    Contract='continuous-layout-v2'; Source=$source; SourceDirty=$sourceDirty; Replication=$Replication; FirstOnly=[bool]$FirstOnly
    InitialOrder=$order; Provenance=$provenance; HarnessSha256=(Get-FileHash ([IO.Path]::ChangeExtension($exe,'.dll'))).Hash
    OfficialRuntime=& dotnet --list-runtimes; OS=[Environment]::OSVersion.ToString(); CpuAvailable=[Environment]::ProcessorCount
    ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion; Runs=$runs
} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'protocol-v2.json')
[ordered]@{
    Contract='continuous-layout-v2'; Status='INCOMPLETE'; CandidateSamples=$candidate.Count
    CompleteLayout=@{ BudgetMilliseconds=1000; Failures=$layoutMisses.Count }
    PreparationPresentation=@{ BudgetMilliseconds=1500; Failures=$presentationMisses.Count }
    ObservedShownWindowInput=@{ BudgetMilliseconds=100; Failures=$inputMisses.Count }
    FunctionalSelectionClipboardSourceUiaParity='INCOMPLETE'; ActualOsAppearanceLifecycle='INCOMPLETE'
    Stretch250=@{ Blocking=$false; Exceeded=@($candidate | Where-Object CompleteLayoutInputMilliseconds -gt 250).Count }
    ExecutionFailures=@($runs | Where-Object ExitCode -ne 0); Samples=$samples
    Decision='INSUFFICIENT EVIDENCE: continuation authorised; required functionality and validation pending'
} | ConvertTo-Json -Depth 15 | Set-Content (Join-Path $output 'acceptance-v2.json')
throw 'v2 full acceptance remains incomplete. Every timing sample and capability gap is retained.'
