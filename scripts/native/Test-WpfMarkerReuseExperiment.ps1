param([ValidateSet(1,2)][int]$Replication = 1)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'The modified-framework experiment requires disposable hosted Windows.'
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $repo 'artifacts/wpf-marker-experiment'
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$dotnetRoot = Split-Path (Get-Command dotnet.exe).Source
$desktopVersion = Get-ChildItem (Join-Path $dotnetRoot 'shared/Microsoft.WindowsDesktop.App') -Directory | Where-Object Name -match '^10\.0\.\d+$' | Sort-Object { [version]$_.Name } | Select-Object -Last 1
if (-not $desktopVersion) { throw 'The hosted .NET 10 desktop runtime is unavailable.' }
$version = $desktopVersion.Name
if (-not (Test-Path -LiteralPath (Join-Path $dotnetRoot "shared/Microsoft.NETCore.App/$version"))) { throw 'Matching core and desktop runtime versions are required.' }
$roots = @{}
foreach ($side in 'original','modified') {
    $privateRoot = Join-Path $output "runtimes/$side"
    $roots[$side] = $privateRoot
    New-Item -ItemType Directory -Path $privateRoot -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $dotnetRoot 'dotnet.exe') -Destination $privateRoot
    foreach ($relative in "host/fxr/$version", "shared/Microsoft.NETCore.App/$version", "shared/Microsoft.WindowsDesktop.App/$version") {
        $parent = Join-Path $privateRoot (Split-Path $relative)
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $dotnetRoot $relative) -Destination $parent -Recurse
    }
}
$originalFramework = Join-Path $roots.original "shared/Microsoft.WindowsDesktop.App/$version/PresentationFramework.dll"
$modifiedFramework = Join-Path $roots.modified "shared/Microsoft.WindowsDesktop.App/$version/PresentationFramework.dll"
& dotnet build scripts/native/experiments/MarkerReuse -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'The pinned experimental tool did not build.' }
$patch = Join-Path $output 'PresentationFramework.experimental.dll'
& dotnet scripts/native/experiments/MarkerReuse/bin/Release/net10.0/MarkerReuse.dll $originalFramework $patch
if ($LASTEXITCODE -ne 0) { throw 'The framework shape was not recognised; no sample is valid.' }
# The only replacement is inside the freshly created experimental workspace.
if (-not ([IO.Path]::GetFullPath($modifiedFramework)).StartsWith([IO.Path]::GetFullPath($output) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Private runtime escaped the experiment directory.' }
Copy-Item -LiteralPath $patch -Destination $modifiedFramework -Force
$frameworkHashes = @{ original=(Get-FileHash $originalFramework).Hash; modified=(Get-FileHash $modifiedFramework).Hash }
$validation = (Resolve-Path 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.dll').Path
$sourceRevision = (git rev-parse HEAD).Trim()
@{ SourceRevision=$sourceRevision; HarnessSha256=(Get-FileHash $validation).Hash; Replication=$Replication; RuntimeVersion=$version
   FrameworkHashes=$frameworkHashes; ImageOS=$env:ImageOS; ImageVersion=$env:ImageVersion
   Processor=(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfLogicalProcessors)
   ProductAcceptanceEligible=$false; Signature='Modified framework is not Microsoft-signed/trusted; no package or installer is produced'
   Policy='Identical application/harness/fixture bytes. Same 66-sample policy as the controlled path. ReadyToRun disabled equally on both private runtimes; separate experimental temperature, never pooled with stock-runtime acceptance.'
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output 'protocol.json')
function Invoke-Sample([string]$Side, [string]$Group, [string]$Scenario, [string[]]$Extra = @()) {
    $directory = Join-Path $output "samples/$Group/$Side"
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $roots[$Side] 'dotnet.exe'))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    foreach ($argument in @($validation,'--continuous-performance',$directory,$Scenario) + $Extra) { $start.ArgumentList.Add($argument) }
    $start.Environment['DOTNET_ROOT'] = $roots[$Side]; $start.Environment['DOTNET_ROOT_X64'] = $roots[$Side]
    $start.Environment['DOTNET_ReadyToRun'] = '0'; $start.Environment['COMPlus_ReadyToRun'] = '0'; $start.Environment['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $start.Environment['LENS_CONTINUOUS_RUNTIME_LAB'] = '1'; $start.Environment['LENS_CONTINUOUS_SOURCE_REVISION'] = $sourceRevision
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $completed = $process.WaitForExit(180000)
    if (-not $completed) { $process.Kill($true); $process.WaitForExit() }
    [IO.File]::WriteAllText((Join-Path $directory 'stdout.txt'), $stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $directory 'stderr.txt'), $stderr.GetAwaiter().GetResult())
    $code = $process.ExitCode; $process.Dispose()
    if (-not $completed -or $code -ne 0) { throw "Experimental process failed: $Group/$Side, completed=$completed, code=$code." }
}
$order = if ($Replication -eq 1) { @('original','modified') } else { @('modified','original') }
foreach ($side in $order) { Invoke-Sample $side 'first-execution' 'all' }
for ($iteration = 0; $iteration -lt 3; $iteration++) {
    $pair = if (($iteration + $Replication) % 2 -eq 0) { @('modified','original') } else { @('original','modified') }
    foreach ($fixture in 'list','table','quote','details','paragraphs','mixed') {
        foreach ($side in $pair) { Invoke-Sample $side "cold-$iteration-$fixture" $fixture }
    }
}
foreach ($side in $order) { Invoke-Sample $side 'repeated' 'all' @('--repeat','4') }
foreach ($side in $order) { Invoke-Sample $side 'fresh-source' 'all' @('--repeat','3','--edited') }
$pairs = [Collections.Generic.List[object]]::new(); $failed = 0
foreach ($group in Get-ChildItem (Join-Path $output 'samples') -Directory) {
    $beforeRoot = Join-Path $group.FullName 'original'
    foreach ($file in Get-ChildItem $beforeRoot -Recurse -Filter '*.json' | Where-Object Name -notin 'acceptance.json','lifecycle-observation.json') {
        $relative = [IO.Path]::GetRelativePath($beforeRoot,$file.FullName)
        $other = Join-Path $group.FullName "modified/$relative"
        $before = Get-Content $file.FullName -Raw | ConvertFrom-Json; $after = Get-Content $other -Raw | ConvertFrom-Json
        foreach ($field in 'FixtureSha256','SourceSha256','HarnessSha256','LogicalTextSha256','SourceElementCount') {
            if ($before.$field -ne $after.$field) { throw "The experimental $field differs: $relative." }
        }
        if ($before.RuntimeLab.WpfFrameworkSha256 -ne $frameworkHashes.original -or $after.RuntimeLab.WpfFrameworkSha256 -ne $frameworkHashes.modified) { throw 'The process loaded an unexpected framework.' }
        if (($before.Structure | ConvertTo-Json -Depth 6 -Compress) -ne ($after.Structure | ConvertTo-Json -Depth 6 -Compress) -or $before.Correctness -ne 'PASS' -or $after.Correctness -ne 'PASS') { throw 'Experimental native structure, styling or navigation differs.' }
        $visual = (Get-FileHash (Join-Path $file.DirectoryName ($file.BaseName + '-initial.png'))).Hash -eq (Get-FileHash (Join-Path (Split-Path $other) ($file.BaseName + '-initial.png'))).Hash
        if (-not $visual) { throw 'The experimental native raster differs.' }
        if ($after.Name -in 'list','mixed' -and $after.RuntimeLab.ReuseCount -le 0) { throw 'The experimental marker reuse was not exercised.' }
        if ($after.FirstLayoutAcceptance -ne 'PASS') { $failed++ }
        $pairs.Add(@{ Group=$group.Name; Sample=$relative; VisualEqual=$visual; Before=$before; After=$after })
    }
}
@{ Status='EXPERIMENT_ONLY'; ProductAcceptanceEligible=$false; ThresholdMilliseconds=250; Samples=$pairs.Count; FailedTimingSamples=$failed; Correctness='PASS'; Pairs=$pairs } | ConvertTo-Json -Depth 35 | Set-Content (Join-Path $output 'comparison.json')
