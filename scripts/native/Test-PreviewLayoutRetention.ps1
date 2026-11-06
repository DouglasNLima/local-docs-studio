param([string]$EvidenceDirectory = 'artifacts/native-ux')
$ErrorActionPreference = 'Stop'
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
$protocol = Get-Content -LiteralPath (Join-Path $evidence 'protocol.json') -Raw | ConvertFrom-Json
$output = Join-Path $evidence 'controls'
if (Test-Path -LiteralPath $output) { throw 'Use a fresh control evidence directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$outcomes = [Collections.Generic.List[object]]::new()
$sides = if ($protocol.order -eq 'candidate-first') { @('candidate', 'baseline') } else { @('baseline', 'candidate') }
foreach ($side in $sides) {
    $product = $protocol.products.$side
    foreach ($mode in 'Continuous', 'Automatic', 'Pages') {
        $directory = Join-Path $output "$side-$mode"
        $arguments = @('--controlled-layout', [IO.Path]::GetFullPath('tests/fixtures/native-ux'), $directory, "--mode=$mode")
        if ($side -eq 'baseline' -and $mode -ne 'Pages') { $arguments += '--expect-displacement' }
        if ($protocol.appearance -eq 'high-contrast') { $arguments += '--high-contrast' }
        & (Join-Path $product.binary 'LensDocsStudio.Native.UxProbe.exe') @arguments 2>&1 | Tee-Object -FilePath "$directory-terminal.txt"
        $outcomes.Add(@{side=$side;mode=$mode;revision=$product.revision;tree=$product.tree;exitCode=$LASTEXITCODE;
            expectation=if ($side -eq 'baseline' -and $mode -ne 'Pages') { 'Reproduce source-position loss; not an acceptance PASS' } else { 'Retain visible source, selection, caret and documents' }})
        $outcomes | ConvertTo-Json -Depth 5 | Set-Content "$output/results.json" -Encoding utf8NoBOM
    }
}
if (@($outcomes | Where-Object exitCode -ne 0).Count) { throw 'Controlled geometry or preservation checks failed. Every outcome is retained.' }
