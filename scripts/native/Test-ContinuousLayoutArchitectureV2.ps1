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
# The Native assembly is a referenced library here. Ship only the diagnostic apphost.
$ordinaryLauncher = Join-Path $payloadDirectory 'LensDocsStudio.Native.exe'
if (Test-Path -LiteralPath $ordinaryLauncher) { Remove-Item -LiteralPath $ordinaryLauncher }
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
    for ($cold = 0; $cold -lt 3; $cold++) {
        $pair = if ($cold % 2 -eq 0) { $order } else { @($order[1],$order[0]) }
        foreach ($fixture in $fixtures) { foreach ($side in $pair) { Invoke-Sample $fixture $side "cold-$cold" } }
    }
    foreach ($side in $order) { Invoke-Sample 'all' $side 'repeated' 4 }
    foreach ($side in $order) { Invoke-Sample 'all' $side 'distant-edit' 3 -Edit }
    foreach ($fixture in $fixtures) { Invoke-Sample $fixture 'candidate' 'independent-uia' -Uia }
    foreach ($fixture in @('rich','nested-lists','table-wrap','collapsed-details','unicode-bidi')) { Invoke-Sample $fixture 'candidate' 'targeted-uia' -Uia }
    foreach ($mode in @('lifecycle','appearance','studio-checks','media-checks','parity-all')) {
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
$controlled = @($candidate | Where-Object RunGroup -notin @('independent-uia','targeted-uia'))
$rawBoundaries = @(Get-ChildItem -LiteralPath $output -Recurse -Filter timing-boundary.json | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json })
$layoutMisses = @($rawBoundaries | Where-Object Adapter -eq 'candidate' | Where-Object CompleteLayoutInputMilliseconds -gt 1000)
$presentationMisses = @($rawBoundaries | Where-Object Adapter -eq 'candidate' | Where-Object EndToEndReadinessMilliseconds -gt 1500)
$inputMisses = @($candidate | Where-Object { $_.MaximumInputGapMilliseconds -gt 100 -or $_.MaximumInteractionInputGapMilliseconds -gt 100 })
[ordered]@{
    Contract='continuous-layout-v2'; Source=$source; SourceDirty=$sourceDirty; Replication=$Replication; FirstOnly=[bool]$FirstOnly
    InitialOrder=$order; Provenance=$provenance; HarnessSha256=(Get-FileHash ([IO.Path]::ChangeExtension($exe,'.dll'))).Hash
    OfficialRuntime=& dotnet --list-runtimes; OS=[Environment]::OSVersion.ToString(); CpuAvailable=[Environment]::ProcessorCount
    ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion; Runs=$runs
} | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $output 'protocol-v2.json')
$pairChecks = @($samples | Where-Object RunGroup -notin @('independent-uia','targeted-uia') | Group-Object { "$($_.RunGroup)|$($_.Fixture)|$($_.Repetition)|$($_.Edited)" } | ForEach-Object {
    $group = $_.Group
    $mismatched = @('Width','Height','Dpi','Zoom','Appearance','Runtime','WpfFrameworkSha256','WpfFormattingSha256','ProductSha256','SourceRevision','SourceDirty','CpuAvailable','Cpu','OS','ImageOS','ImageVersion','HostMachine') | Where-Object { @($group | Select-Object -ExpandProperty $_ -Unique).Count -ne 1 }
    [ordered]@{ MismatchedControlledFields=@($mismatched); Key=$_.Name; Pass=@($mismatched).Count -eq 0 -and $group.Count -eq 2 -and @($group | Select-Object -ExpandProperty Adapter -Unique).Count -eq 2 -and @($group | Select-Object -ExpandProperty FixtureSha256 -Unique).Count -eq 1 -and @($group | Select-Object -ExpandProperty SourceSha256 -Unique).Count -eq 1 -and @($group | Select-Object -ExpandProperty HarnessSha256 -Unique).Count -eq 1 }
})
$failedInteractionInput = @(Get-ChildItem -LiteralPath $output -Filter interaction-heartbeat-failure.json -Recurse | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json } | Where-Object { @($_.InteractionInputGaps | Where-Object { $_ -gt 100 }).Count })
$coverage = Get-Content src/native/LensDocsStudio.LayoutExperiment/parity-requirements-v2.json -Raw | ConvertFrom-Json
$missing = @($coverage.Requirements | Where-Object Implementation -ne 'Implemented')
$special = @{}
foreach ($receipt in @(@('lifecycle','lifecycle.json'),@('appearance','appearance.json'),@('studio-checks','studio.json'),@('media-checks','media.json'),@('parity-all','targeted-parity.json'))) {
    $path = Join-Path $output ($receipt[0] + '/' + $receipt[1])
    $special[$receipt[0]] = if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Raw | ConvertFrom-Json } else { @{Status='MISSING'} }
}
$interactionReceipts = @(Get-ChildItem -LiteralPath $output -Filter independent-uia.json -Recurse | ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
$lifecycleBudgetMisses = $special['lifecycle'].CancellationAcceptance -ne 'PASS' -or $special['lifecycle'].WinningPreviewReadinessAcceptance -ne 'PASS'
$specialInputMisses = @($special.Values | Where-Object { $_.MaximumInputGapMilliseconds -gt 100 })
$executionFailures = @($runs | Where-Object ExitCode -ne 0)
$missingSamples = $controlled.Count -ne 66 -or @($samples | Where-Object { $_.Adapter -eq 'stock' }).Count -ne 66
$proofMissing = @($pairChecks | Where-Object Pass -ne $true).Count -gt 0 -or $FirstOnly -or $missingSamples -or $interactionReceipts.Count -ne 11 -or @($special.Values | Where-Object Status -ne 'PASS').Count -gt 0
$timingFail = $failedInteractionInput.Count -gt 0 -or $layoutMisses.Count -gt 0 -or $presentationMisses.Count -gt 0 -or $inputMisses.Count -gt 0 -or $specialInputMisses.Count -gt 0
$officialRuntimeValid = $runtimeChecks.Count -eq 2 -and @($runtimeChecks | Where-Object { $_.SignatureStatus -ne 'Valid' -or $_.Signer -notmatch 'Microsoft Corporation' }).Count -eq 0
$compiledSourceMatches = $samples.Count -gt 0 -and @($samples | Where-Object { $_.HarnessInformationalVersion -notmatch [regex]::Escape($source) }).Count -eq 0
$fullPass = $compiledSourceMatches -and -not $sourceDirty -and $officialRuntimeValid -and -not $lifecycleBudgetMisses -and -not $proofMissing -and -not $timingFail -and $missing.Count -eq 0 -and $executionFailures.Count -eq 0
[ordered]@{
    Contract='continuous-layout-v2'; CleanSourceRequired=$true; CleanSource=(-not $sourceDirty); CompiledSourceMatches=$compiledSourceMatches; OfficialRuntimeValid=$officialRuntimeValid; Status=$(if ($fullPass) {'PASS'} elseif ($timingFail -or $executionFailures.Count) {'FAIL'} else {'INCOMPLETE'})
    CandidateSamples=$candidate.Count; RequiredControlledSamples=66; RecordedControlledSamples=$controlled.Count; MissingControlledSamples=$missingSamples; RawTimedBoundaries=$rawBoundaries
    CompleteLayout=@{ BudgetMilliseconds=1000; Status=$(if($layoutMisses.Count){'FAIL'}elseif($missingSamples){'INCOMPLETE'}else{'PASS'}); Failures=$layoutMisses.Count }
    PreparationPresentation=@{ BudgetMilliseconds=1500; Status=$(if($presentationMisses.Count){'FAIL'}elseif($missingSamples){'INCOMPLETE'}else{'PASS'}); Failures=$presentationMisses.Count }
    ObservedShownWindowInput=@{ BudgetMilliseconds=100; Status=$(if($inputMisses.Count -or $specialInputMisses.Count){'FAIL'}elseif($proofMissing){'INCOMPLETE'}else{'PASS'}); SampleFailures=$inputMisses.Count; SpecialScenarioFailures=$specialInputMisses.Count }
    FunctionalSelectionClipboardSourceUiaParity=$(if($missing.Count -or $proofMissing -or @($interactionReceipts | Where-Object Status -ne 'PASS').Count){'INCOMPLETE_OR_FAIL'}else{'PASS'})
    ActualOsAppearanceLifecycle=@{ Status=$(if($lifecycleBudgetMisses -or @($special.Values | Where-Object Status -ne 'PASS').Count -or $specialInputMisses.Count){'INCOMPLETE_OR_FAIL'}else{'PASS'}); Receipts=$special }
    LifecycleBudgetMisses=$lifecycleBudgetMisses; PairIdentityChecks=$pairChecks; FailedInteractionInputReceipts=$failedInteractionInput; ParityCoverage=$coverage; MissingCapabilities=$missing; InteractionReceipts=$interactionReceipts
    Stretch250=@{ Blocking=$false; Exceeded=@($candidate | Where-Object CompleteLayoutInputMilliseconds -gt 250).Count }
    ExecutionFailures=$executionFailures; Samples=$samples
    Decision=$(if($fullPass){'VIABLE FOR A SEPARATE MIGRATION PROPOSAL: tested workloads and hosts only; no migration authorised'}else{'INSUFFICIENT EVIDENCE: required v2 timing, capabilities or validation remain unsuccessful/incomplete'})
} | ConvertTo-Json -Depth 18 | Set-Content (Join-Path $output 'acceptance-v2.json')
if (-not $fullPass) { throw 'v2 full acceptance failed or remains incomplete. Every timing sample, execution failure and capability gap is retained.' }
