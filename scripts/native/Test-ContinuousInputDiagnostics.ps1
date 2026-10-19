param([ValidateSet(1,2)][int]$Replication = 1, [string]$OutputDirectory = 'artifacts/layout-input-diagnostics')
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') { throw 'Actual OS controls require an authorised disposable GitHub-hosted desktop.' }
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (Test-Path -LiteralPath $output) { throw 'Use a fresh directory; no diagnostic receipt may be overwritten.' }
New-Item -ItemType Directory -Path $output | Out-Null
& scripts/native/Build-ContinuousLayoutExperiment.ps1 -Compilation ReadyToRun -OutputDirectory artifacts/layout-input-bin
$exe = (Resolve-Path artifacts/layout-input-bin/LensDocsStudio.LayoutExperiment.exe).Path
$env:LENS_LAYOUT_SOURCE_REVISION = (git rev-parse HEAD)
$env:LENS_LAYOUT_SOURCE_DIRTY = ([bool](git status --porcelain)).ToString()
$env:LENS_LAYOUT_COMPILATION = 'ReadyToRun'
if ($env:LENS_LAYOUT_SOURCE_DIRTY -ne 'False') { throw 'Diagnostic execution requires a clean committed source.' }
Copy-Item artifacts/layout-input-bin/compilation.json -Destination $output
$runs = [Collections.Generic.List[object]]::new()
function Invoke-Control([string]$Name, [string[]]$Arguments) {
    $destination = Join-Path $output $Name
    New-Item -ItemType Directory -Path $destination | Out-Null
    [string[]]$actual = $Arguments + @('--detailed-input-trace','--output',('"' + $destination + '"'))
    $started = [DateTime]::UtcNow.ToString('o')
    $process = Start-Process -FilePath $exe -ArgumentList $actual -PassThru -WindowStyle Hidden -RedirectStandardOutput "$destination/stdout.txt" -RedirectStandardError "$destination/stderr.txt"
    if (-not $process.WaitForExit(180000)) { Stop-Process -Id $process.Id -Force; 'External 180-second diagnostic deadline' | Set-Content "$destination/deadline-failure.txt" }
    $process.Refresh()
    $identity = if (Test-Path "$destination/execution-identity.json") { Get-Content "$destination/execution-identity.json" -Raw | ConvertFrom-Json } else { $null }
    if ($identity -and (($identity.Arguments | ConvertTo-Json -Compress) -ne (($Arguments + @('--detailed-input-trace','--output',$destination)) | ConvertTo-Json -Compress))) { throw 'Child arguments differ from the requested diagnostic; retain the invalid case.' }
    $runs.Add(@{ Name=$Name; Arguments=$actual; Started=$started; Ended=[DateTime]::UtcNow.ToString('o'); ExitCode=$process.ExitCode; Directory=$destination })
}
$sides = if ($Replication -eq 1) { @('candidate','stock') } else { @('stock','candidate') }
$apartments = if ($Replication -eq 1) { @('STA','MTA') } else { @('MTA','STA') }
foreach ($apartment in $apartments) { foreach ($side in $sides) { Invoke-Control "reading-$side-$apartment" @('--reading-input-checks','--adapter',$side,'--client-apartment',$apartment) } }
Invoke-Control 'original-list-interaction' @('--scenario','list','--adapter','candidate','--uia','--contract-v2')
$modes = if ($Replication -eq 1) { @('appearance','appearance-stock-control','appearance-controls','media-checks','media-controls') } else { @('appearance-controls','appearance-stock-control','appearance','media-controls','media-checks') }
foreach ($mode in $modes) { Invoke-Control $mode @("--$mode") }
@{ DiagnosticOnly=$true; Source=(git rev-parse HEAD); SourceDirty=$env:LENS_LAYOUT_SOURCE_DIRTY; Replication=$Replication; SideOrder=$sides; ClientOrder=$apartments;
   ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion; Host=[Environment]::MachineName; Os=[Environment]::OSVersion.ToString(); CpuAvailable=[Environment]::ProcessorCount; Cpu=$env:PROCESSOR_IDENTIFIER;
   HarnessSha256=(Get-FileHash artifacts/layout-input-bin/LensDocsStudio.LayoutExperiment.dll).Hash; Sdk=(dotnet --info); Runs=$runs;
   Meaning='One bounded focused round, every result retained. This command verifies execution, not the unchanged 100 ms acceptance row.' } | ConvertTo-Json -Depth 10 | Set-Content "$output/diagnostic-round.json"
if (@($runs | Where-Object ExitCode -ne 0).Count) { throw 'A focused diagnostic failed; all attempted cases remain retained.' }
