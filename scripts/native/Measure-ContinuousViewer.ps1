[CmdletBinding()]
param(
    [string]$OutputDirectory = 'acceptance/continuous/comparison',
    [ValidateRange(2, 10)][int]$Samples = 3,
    [string[]]$Fixtures = @('list', 'table', 'paragraphs', 'quote', 'details', 'mixed', 'large-4mb', 'rich', 'oversized')
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory, $repo)
if (Test-Path -LiteralPath $output) { throw 'Measurement output already exists. Use a new directory to retain original observations.' }
New-Item -ItemType Directory -Path $output | Out-Null
$binary = Join-Path $repo 'src/native/spikes/ContinuousViewer/bin/Release/net10.0-windows/ContinuousViewer.dll'
if (-not (Test-Path -LiteralPath $binary)) { throw 'Build the isolated spike before measuring.' }
$environment = [ordered]@{
    sourceRevision = (& git -C $repo rev-parse HEAD).Trim()
    dirtySource = [bool](& git -C $repo status --porcelain)
    cpu = (Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name) -join ', '
    physicalMemoryBytes = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory
    os = [Environment]::OSVersion.ToString()
    viewport = '640 x 300'; inputBudgetMilliseconds = 250
    samples = $Samples; fixtures = $Fixtures
    methodology = 'Sequential independent processes; cold and warm samples remain separate. Same source, native DLL, machine and viewport. No tracing or test suites alongside measurement.'
}
$environment | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'environment.json') -Encoding utf8NoBOM
Push-Location $repo
try {
    for ($sample = 1; $sample -le $Samples; $sample++) {
        foreach ($fixture in $Fixtures) {
            foreach ($engine in @('baseline', 'prototype')) {
                $destination = Join-Path $output "$fixture/$engine-$sample"
                New-Item -ItemType Directory -Path $destination -Force | Out-Null
                & dotnet $binary --measure $engine $fixture $destination *> (Join-Path $destination 'process.log')
                if ($LASTEXITCODE -ne 0) { throw "The $engine $fixture observation process failed. Original log retained." }
                if (-not (Test-Path -LiteralPath (Join-Path $destination 'cold.json'))) { throw 'The observation process omitted its cold evidence.' }
                Get-Content -LiteralPath (Join-Path $destination 'process.log')
            }
        }
    }
    $rows = Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object { $_.Name -in @('cold.json', 'warm.json') } | ForEach-Object {
        $item = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
        [pscustomobject]@{
            fixture = $item.fixture; engine = $item.engine; scenario = $item.scenario; completed = $item.completed
            firstLayoutInputMilliseconds = $item.firstLayoutInputMilliseconds; worstDispatcherInputMilliseconds = $item.worstDispatcherInputMilliseconds
            firstUsefulViewportMilliseconds = $item.firstUsefulViewportMilliseconds; navigationMilliseconds = $item.navigationMilliseconds
            inputAcceptance = $item.inputAcceptance; peakWorkingSetBytes = $item.peakWorkingSetBytes; allocatedBytes = $item.allocatedBytes
            peakRealised = $item.peakRealised; logicalPieces = $item.logicalPieces; evidence = $_.FullName.Substring($output.Length + 1)
        }
    }
    $rows | Export-Csv -LiteralPath (Join-Path $output 'observations.csv') -NoTypeInformation -Encoding utf8NoBOM
    $rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'observations.json') -Encoding utf8NoBOM
    # Recording a failed budget is successful evidence collection, never performance certification.
    $rows | Group-Object fixture,engine,scenario | ForEach-Object {
        $values = @($_.Group.firstLayoutInputMilliseconds | Where-Object { $null -ne $_ } | Sort-Object)
        if ($values.Count -gt 0) {
            $median = if ($values.Count % 2) { $values[[int][Math]::Floor($values.Count / 2)] } else { ($values[$values.Count / 2 - 1] + $values[$values.Count / 2]) / 2 }
            [pscustomobject]@{ group = $_.Name; samples = $_.Count; medianInputMs = $median; worstInputMs = $values[-1]; failedOrIncomplete = @($_.Group | Where-Object { $_.inputAcceptance -ne 'PASS' -or -not $_.completed }).Count }
        }
    } | Export-Csv -LiteralPath (Join-Path $output 'summary.csv') -NoTypeInformation -Encoding utf8NoBOM
} finally { Pop-Location }
