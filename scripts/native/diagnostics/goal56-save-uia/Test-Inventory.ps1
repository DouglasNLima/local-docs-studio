param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Use a fresh disposable inventory fixture directory.' }
New-Item -ItemType Directory -Path $Output | Out-Null
$oldFixture = Join-Path $Output original-writer-control
New-Item -ItemType Directory -Path $oldFixture | Out-Null
[IO.File]::WriteAllText((Join-Path $oldFixture input.txt), 'Synthetic control only')
[IO.File]::WriteAllText((Join-Path $oldFixture file-hashes.json), 'pre-existing manifest')
$originalError = $null
try {
    # Exact historical writer, restricted to new synthetic files. Retain its exception.
    Get-ChildItem -LiteralPath $oldFixture -Recurse -File | Get-FileHash | Select-Object Algorithm,Hash,@{N='File';E={[IO.Path]::GetRelativePath($oldFixture, $_.Path)}} | ConvertTo-Json > (Join-Path $oldFixture file-hashes.json)
} catch { $originalError = $_.ToString() }
if ($originalError -notlike '*file-hashes.json*' -or $originalError -notlike '*used by another process*') { throw 'The original synthetic sharing-violation control was not demonstrated.' }
$fixture = Join-Path $Output fixture
New-Item -ItemType Directory -Path (Join-Path $fixture nested/deeper) -Force | Out-Null
$files = [ordered]@{
    'ordinary.txt' = [Text.Encoding]::UTF8.GetBytes("Ordinary ASCII`n")
    'Unicode-Ω-日本語-😀.md' = [Text.Encoding]::UTF8.GetBytes("# Preserved Ω😀`r`n")
    'nested/binary.bin' = [byte[]](0..255)
    'nested/deeper/identical.bin' = [byte[]](0..255)
    'nested/deeper/distinct.bin' = [byte[]]@(255,0,128,13,10,42)
}
foreach ($entry in $files.GetEnumerator()) { [IO.File]::WriteAllBytes((Join-Path $fixture $entry.Key), $entry.Value) }
$manifest = Join-Path $fixture file-hashes.json
[IO.File]::WriteAllText($manifest, 'pre-existing manifest')
# Load only the actual function, never execute the historical experiment script.
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot Run-Comparison.ps1), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Inventory source did not parse.' }
$functions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Write-Inventory' }, $true))
if ($functions.Count -ne 1) { throw 'Expected exactly one inventory function.' }
Invoke-Expression $functions[0].Extent.Text
Write-Inventory $fixture
$first = [IO.File]::ReadAllBytes($manifest)
$rows = @(Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json)
if ($rows.Count -ne $files.Count) { throw 'Inventory membership count differs.' }
[string[]]$expectedPaths = @($files.Keys | ForEach-Object { $_.Replace('/', [IO.Path]::DirectorySeparatorChar) })
[Array]::Sort($expectedPaths, [StringComparer]::Ordinal)
if (($rows.File -join '|') -cne ($expectedPaths -join '|')) { throw 'Exact relative paths or deterministic ordering differ.' }
foreach ($row in $rows) {
    $bytes = $files[$row.File.Replace('\', '/')]
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([byte[]]$bytes))
    if ($row.Hash -cne $hash -or $row.Length -ne $bytes.Length -or $row.Algorithm -cne 'SHA256') { throw "Incorrect inventory row: $($row.File)" }
    if (-not [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes((Join-Path $fixture $row.File)), [byte[]]$bytes)) { throw 'Original evidence changed.' }
}
Push-Location -LiteralPath $Output
try { Write-Inventory 'fixture' } finally { Pop-Location }
if (-not [Linq.Enumerable]::SequenceEqual($first, [IO.File]::ReadAllBytes($manifest))) { throw 'Repeated inventory was not byte-stable.' }
$locked = [IO.File]::Open((Join-Path $fixture ordinary.txt), 'Open', 'ReadWrite', 'None')
$unreadableFailed = $false
try { try { Write-Inventory $fixture } catch { $unreadableFailed = $true } } finally { $locked.Dispose() }
if (-not $unreadableFailed -or -not [Linq.Enumerable]::SequenceEqual($first, [IO.File]::ReadAllBytes($manifest))) { throw 'Unreadable input did not fail before replacing the manifest.' }
[ordered]@{ Status = 'PASS'; Classification = 'INVENTORY_FUNCTION_VERIFIED'; OriginalSyntheticSharingViolation = $originalError;
    Count = $rows.Count; RepeatedBytesIdentical = $true; PreExistingManifestExcluded = $true; UnreadableInputFailed = $true;
    ManifestSHA256 = (Get-FileHash -LiteralPath $manifest).Hash; Rows = $rows; Utc = [DateTimeOffset]::UtcNow;
    Source = (git -C $PSScriptRoot rev-parse HEAD); Tree = (git -C $PSScriptRoot rev-parse 'HEAD^{tree}');
    ImageOS = $env:ImageOS; ImageVersion = $env:ImageVersion; PowerShell = $PSVersionTable.PSVersion.ToString() } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $Output inventory-result.json) -Encoding utf8NoBOM
