[CmdletBinding()]
param([string]$OutputPath = 'artifacts/native/editor-baseline.json')
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manifest = Get-Content -LiteralPath (Join-Path $repository 'tests/fixtures/native-mermaid/expansion-v/previous-editor-baseline.json') -Raw | ConvertFrom-Json
if ($manifest.sourceRevision -ne '06a7d868d1b58c82f29c7c655dcc972f1ca77282' -or $manifest.templates.Count -ne 17 -or $manifest.files.Count -eq 0) { throw 'The previous-editor baseline identity is invalid.' }
$checks = @()
foreach ($entry in $manifest.files) {
    if (-not $entry.path.StartsWith('tests/fixtures/', [StringComparison]::Ordinal) -or $entry.path.Contains('..')) { throw 'The baseline fixture path is outside the intended source scope.' }
    $path = Join-Path $repository $entry.path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Previous fixture is missing: $($entry.path)" }
    # Git clean filters account for the checkout's LF/CRLF policy. Record actual on-disk SHA-256 separately.
    $blob = (& git -C $repository hash-object ("--path=" + $entry.path) $path).Trim()
    if ($LASTEXITCODE -ne 0 -or $blob -ne $entry.gitBlob) { throw "Previous fixture changed: $($entry.path)" }
    $checks += @{ path=$entry.path; gitBlob=$blob; sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$absoluteOutput = [IO.Path]::GetFullPath((Join-Path $repository $OutputPath))
[IO.Directory]::CreateDirectory((Split-Path -Parent $absoluteOutput)) | Out-Null
@{ status='PASS'; baselineRevision=$manifest.sourceRevision; sourceRevision=(& git -C $repository rev-parse HEAD).Trim(); fixtures=$checks } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $absoluteOutput -Encoding utf8NoBOM
"Previous editor baseline PASS: $($checks.Count) unchanged source fixtures; all seventeen catalogue templates separately pinned in Core."
