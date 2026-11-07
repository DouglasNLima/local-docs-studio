param(
    [string]$OutputDirectory = 'artifacts/line-height-alternative',
    [ValidateSet('stock-first','alternative-first')][string]$Order = 'stock-first'
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh diagnostic directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$source = (git rev-parse HEAD).Trim()
git diff --exit-code HEAD
if ($LASTEXITCODE -ne 0) { throw 'Require clean source before the isolated experiment.' }
dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Stock build failed.' }
$harness = [IO.Path]::GetFullPath('src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows')
$lab = Join-Path $output 'source'
git worktree add --detach $lab $source
if ($LASTEXITCODE -ne 0) { throw 'Cannot create the isolated source copy.' }
$path = 'src/native/LensDocsStudio.Native/Rendering/NativeMarkdownRenderer.cs'
$renderer = Join-Path $lab $path
$original = [IO.File]::ReadAllText($renderer)
$needle = 'Margin = new Thickness(0, 4, 0, 10), LineHeight = 23'
if (($original.Split($needle, [StringSplitOptions]::None)).Count -ne 2) { throw 'Expected exactly one renderer property declaration.' }
# A deliberately ineligible causal experiment: automatic line height changes
# appearance. It is never copied to the working tree or packaged as the product.
[IO.File]::WriteAllText($renderer, $original.Replace($needle, 'Margin = new Thickness(0, 4, 0, 10)'), [Text.UTF8Encoding]::new($false))
git -C $lab diff -- $path | Set-Content "$output/alternative.patch"
dotnet build "$lab/src/native/LensDocsStudio.Native/LensDocsStudio.Native.csproj" -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE -ne 0) { throw 'Diagnostic alternative build failed.' }
$alternative = Join-Path $output 'alternative-binaries'
Copy-Item -LiteralPath $harness -Destination $alternative -Recurse
Copy-Item -Path "$lab/src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/*" -Destination $alternative -Recurse -Force
$hash = (Get-FileHash "$harness/LensDocsStudio.Native.Validation.dll").Hash
if ($hash -ne (Get-FileHash "$alternative/LensDocsStudio.Native.Validation.dll").Hash) { throw 'Harness binaries differ.' }
$sides = if ($Order -eq 'stock-first') { @('stock','alternative') } else { @('alternative','stock') }
[ordered]@{
    Status='EXPERIMENT_ONLY'; AcceptanceEligible=$false; Reason='Removing explicit 23-DIP line height changes required appearance. No product fix or acceptance claim.'
    StockCommit=$source; StockTree=(git rev-parse 'HEAD^{tree}').Trim(); HarnessSha256=$hash
    AlternativeSourceSha256=(Get-FileHash $renderer).Hash; PatchSha256=(Get-FileHash "$output/alternative.patch").Hash
    ProductSha256=@{ stock=(Get-FileHash "$harness/LensDocsStudio.Native.dll").Hash; alternative=(Get-FileHash "$alternative/LensDocsStudio.Native.dll").Hash }
    Order=$sides; Runtime=(dotnet --list-runtimes); ImageVersion=$env:ImageVersion
    Policy='One fresh process per side; two complete constructions of all six original fixtures. Original harness, thresholds and assertions unchanged. No prewarming or forced GC. Not the 66-sample acceptance protocol.'
} | ConvertTo-Json -Depth 6 | Set-Content "$output/protocol.json"
$failures = [Collections.Generic.List[string]]::new()
$priorSource = $env:LENS_CONTINUOUS_SOURCE_REVISION
try {
    foreach ($side in $sides) {
        $directory = Join-Path $output $side
        New-Item -ItemType Directory -Path $directory | Out-Null
        $binary = if ($side -eq 'stock') { $harness } else { $alternative }
        $env:LENS_CONTINUOUS_SOURCE_REVISION = if($side -eq 'stock'){$source}else{"$source + ineligible line-height patch"}
        $process = Start-Process -FilePath "$binary/LensDocsStudio.Native.Validation.exe" -ArgumentList @('--continuous-performance',('"'+"$directory/samples"+'"'),'all','--repeat','2') -WindowStyle Hidden -PassThru -RedirectStandardOutput "$directory/stdout.txt" -RedirectStandardError "$directory/stderr.txt"
        $expired = -not $process.WaitForExit(120000)
        if($expired){$process.Kill($true);$process.WaitForExit()}
        @{ExitCode=$process.ExitCode;DiagnosticBoundExceeded=$expired} | ConvertTo-Json | Set-Content "$directory/process.json"
        if($expired -or $process.ExitCode -ne 0){$failures.Add($side)}
    }
}
finally { $env:LENS_CONTINUOUS_SOURCE_REVISION = $priorSource }
@{Status=$(if($failures.Count){'FAIL'}else{'COLLECTED'});AcceptanceEligible=$false;FailedExecutions=@($failures)} | ConvertTo-Json | Set-Content "$output/completion.json"
if($failures.Count){throw 'A diagnostic execution failed; preserve both outcomes.'}
