[CmdletBinding()]
param([string]$MatrixPath = 'docs/native/evidence/goal-vii/ux-matrix.json')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$matrix = Get-Content -LiteralPath (Join-Path $root $MatrixPath) -Raw | ConvertFrom-Json
$expected = @('Flowchart','Sequence','State','Entity relationship (ER)','Class','Gantt','Mindmap','Pie','Timeline','Journey','Quadrant','XY Chart','Radar','Sankey','Kanban','Requirement','GitGraph','Architecture','C4','Block','Packet','TreeView','Treemap','Venn')
if ($matrix.schemaVersion -ne 1 -or $matrix.rows.Count -ne 24 -or
    ($matrix.rows.family -join '|') -cne ($expected -join '|') -or
    $matrix.baseline.revision -cne '33a751c60886a364102c45cb43e36af1e7f13517' -or
    $matrix.baseline.tree -cne '51912afe438f085bcfbebd33b52e90eb91a7ba33') {
    throw 'The audit must retain all 24 distinct bounded families and exact accepted baseline.'
}
$specialist = 0; $expanded = 0
foreach ($row in $matrix.rows) {
    foreach ($path in @($row.window, $row.protectionGuide, $row.keyboardOccurrenceAccess.evidence)) {
        if (-not $path -or $path.Contains('..') -or [IO.Path]::IsPathRooted($path) -or -not (Test-Path -LiteralPath (Join-Path $root $path) -PathType Leaf)) {
            throw "Missing or unsafe audit source reference for $($row.family): $path"
        }
    }
    if ($row.implementation -ceq 'specialist') { $specialist++ } else { $expanded++ }
    if ($row.sourceProtections.Length -lt 40 -or $row.entryPoints.Count -ne 4 -or
        $row.appearance.baselineSource -cne $matrix.baseline.revision -or
        ($row.appearance.sizes | ConvertTo-Json -Compress) -cne '[{"width":880,"height":600},{"width":1280,"height":800}]' -or
        $row.interactiveHighDpi -cne 'NOT RUN' -or $row.physicalScreenReader -cne 'NOT RUN' -or
        $row.cleanSdkFreeWindows -cne 'BLOCKED' -or $row.installed.historicalUpgrade -cne 'NOT CERTIFIED') {
        throw "Incomplete audit boundaries for $($row.family). Update guards only with independent certification evidence."
    }
}
if ($specialist -ne 5 -or $expanded -ne 19) { throw 'The original five and 38 expanded appearances must stay distinct.' }
foreach ($name in 'current-windows-readiness.md','goal-vii-ux-audit.md','goal-vii-certification-runbooks.md') {
    if (-not (Test-Path -LiteralPath (Join-Path $root "docs/native/$name"))) { throw "Missing readiness document: $name" }
}
'Native UX audit integrity PASS: 24 bounded families, five specialist windows, 19 shared-shell families; certification gaps remain explicit.'
