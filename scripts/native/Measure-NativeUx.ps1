param(
    [string]$BaselineRevision = '99349d82eb06960eb1f9785cf384db2373aed4fa',
    [string]$CandidateRevision = '75de42474514af0e7a6e521235a6517f4acad14b',
    [ValidateSet('normal','high-contrast')][string]$Appearance = 'normal',
    [ValidateSet('baseline-first','candidate-first')][string]$Order = 'baseline-first',
    [ValidateRange(1,300)][int]$Processes = 3,
    [ValidateRange(1,100)][int]$Iterations = 21,
    [string]$Fixtures = 'small,technical,long,table,nested-list,mermaid,mixed',
    [string]$OutputDirectory = 'artifacts/native-ux'
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Real foreground input and OS appearance changes require a disposable hosted Windows runner.'
}
if ($BaselineRevision -notmatch '^[0-9a-f]{40}$' -or $CandidateRevision -notmatch '^[0-9a-f]{40}$') { throw 'Exact source SHAs are required.' }
$fixtureRoot = [IO.Path]::GetFullPath('tests/fixtures/native-ux')
$manifest = Get-Content "$fixtureRoot/manifest.json" -Raw | ConvertFrom-Json
$selected = $Fixtures.Split(',')
if (@($selected | Select-Object -Unique).Count -ne $selected.Count -or @($selected | Where-Object { $_ -notin $manifest.fixtures.id }).Count) { throw 'Unknown or repeated fixture.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh evidence directory; retained runs are never overwritten.' }
New-Item -ItemType Directory -Path $output | Out-Null
git diff --exit-code HEAD
if ($LASTEXITCODE) { throw 'Tracked harness source must be clean.' }
$harnessRevision = (git rev-parse HEAD).Trim()
$harnessTree = (git rev-parse 'HEAD^{tree}').Trim()
dotnet build src/native/LensDocsStudio.Native.UxProbe -c Release -p:RestoreLockedMode=true "-p:SourceRevisionId=$harnessRevision"
if ($LASTEXITCODE) { throw 'UX probe build failed.' }
$harness = [IO.Path]::GetFullPath('src/native/LensDocsStudio.Native.UxProbe/bin/Release/net10.0-windows')
$harnessHash = (Get-FileHash -LiteralPath (Join-Path $harness 'LensDocsStudio.Native.UxProbe.dll')).Hash
$products = @{}
foreach ($side in 'baseline','candidate') {
    $revision = if ($side -eq 'baseline') { $BaselineRevision } else { $CandidateRevision }
    $checkout = Join-Path $output "$side-source"
    git worktree add --detach $checkout $revision
    if ($LASTEXITCODE) { throw "Cannot check out $side exact source." }
    dotnet build "$checkout/src/native/LensDocsStudio.Native/LensDocsStudio.Native.csproj" -c Release -p:RestoreLockedMode=true "-p:SourceRevisionId=$revision"
    if ($LASTEXITCODE) { throw "Cannot build $side product." }
    $binary = Join-Path $output "$side-binaries"
    Copy-Item -LiteralPath $harness -Destination $binary -Recurse
    Copy-Item "$checkout/src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/*" $binary -Recurse -Force
    if ((Get-FileHash "$binary/LensDocsStudio.Native.UxProbe.dll").Hash -ne $harnessHash) { throw 'Harness bytes differ.' }
    $products[$side] = @{revision=$revision;tree=(git -C $checkout rev-parse 'HEAD^{tree}').Trim();binary=$binary;
        productSha256=(Get-FileHash "$binary/LensDocsStudio.Native.dll").Hash;coreSha256=(Get-FileHash "$binary/LensDocsStudio.Core.dll").Hash}
}
$protocol = [ordered]@{schema=1;harnessRevision=$harnessRevision;harnessTree=$harnessTree;harnessSha256=$harnessHash;products=$products;
    appearance=$Appearance;order=$Order;processes=$Processes;iterations=$Iterations;fixtures=$selected;
    transitionTrace=($env:LENS_NATIVE_TRANSITION_TRACE -eq '1');purpose='Correctness investigation; no performance policy activation';
    fixtureIdentities=@($manifest.fixtures | Where-Object id -in $selected);
    image=$env:ImageVersion;processor=(Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors);
    os=[Environment]::OSVersion.ToString();runtimes=(dotnet --list-runtimes);fixtureManifestSha256=(Get-FileHash "$fixtureRoot/manifest.json").Hash;
    acceptance='PENDING; proposed policy only';historical='FAIL / LEGACY QUALIFICATION; original required gate remains blocking';
    bounds=@{childSeconds=240;privateBytes=2147483648;logBytes=16777216};
    temperature='Fresh process for each fixture and repetition; first render retained, subsequent actual interactions labelled warm. OS caches are not reset. No prewarm or forced GC.'}
$protocol | ConvertTo-Json -Depth 10 | Set-Content "$output/protocol.json" -Encoding utf8NoBOM
$runs = [Collections.Generic.List[object]]::new()
for ($process=0; $process -lt $Processes; $process++) {
    $sides = if (($Order -eq 'baseline-first') -xor ($process % 2 -eq 1)) { @('baseline','candidate') } else { @('candidate','baseline') }
    foreach ($fixture in $selected) {
        foreach ($side in $sides) {
            $directory = Join-Path $output "samples/$process/$fixture/$side"
            $parent = Split-Path $directory
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
            $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $products[$side].binary 'LensDocsStudio.Native.UxProbe.exe'))
            $info.UseShellExecute = $false; $info.CreateNoWindow = $true
            $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
            foreach ($argument in @($fixtureRoot,$directory,$fixture,"$Iterations",$Appearance,$products[$side].revision)) { $info.ArgumentList.Add($argument) }
            $child = [Diagnostics.Process]::Start($info)
            $stdoutFile = [IO.File]::Create("$parent/$side-stdout.txt")
            $stderrFile = [IO.File]::Create("$parent/$side-stderr.txt")
            $stdout = $child.StandardOutput.BaseStream.CopyToAsync($stdoutFile)
            $stderr = $child.StandardError.BaseStream.CopyToAsync($stderrFile)
            $watch = [Diagnostics.Stopwatch]::StartNew(); $bounded = $null
            while (!$child.WaitForExit(250)) {
                $child.Refresh()
                if ($watch.Elapsed.TotalSeconds -gt 240) { $bounded = '240-second child supervision limit' }
                if ($child.PrivateMemorySize64 -gt 2147483648) { $bounded = '2 GiB child private-memory supervision limit' }
                if ($stdoutFile.Length + $stderrFile.Length -gt 16777216) { $bounded = '16 MiB child log supervision limit' }
                if ($bounded) { $child.Kill($true); $child.WaitForExit(); break }
            }
            try { $stdout.GetAwaiter().GetResult(); $stderr.GetAwaiter().GetResult() }
            finally { $stdoutFile.Dispose(); $stderrFile.Dispose() }
            $runs.Add(@{process=$process;fixture=$fixture;side=$side;exitCode=$child.ExitCode;supervisionFailure=$bounded})
            $runs | ConvertTo-Json -Depth 5 | Set-Content "$output/processes.json" -Encoding utf8NoBOM
            # Retain every failed observation; continue independent cells, never retry it into PASS.
        }
    }
}
node scripts/native/ux/report.mjs $output
if ($LASTEXITCODE) { throw 'Collection or evidence-integrity failure. Proposed performance acceptance is separate.' }
