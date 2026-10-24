[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineVerification,
    [Parameter(Mandatory)][string]$CandidateVerification,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedBaselineRevision,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedCandidateRevision,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
# Compare complete manifests emitted by read-only Test-NativePreviewPackage, never execute payloads.
if (Test-Path -LiteralPath $OutputPath) { throw 'Payload delta evidence already exists; choose a new output.' }
function Read-Manifest([string]$Path, [string]$Revision) {
    $value = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($value.status -cne 'PASS' -or $value.sourceRevision -cne $Revision -or $value.executionPerformed -ne $false -or @($value.payload).Count -eq 0) {
        throw 'A successful read-only package verification at the pinned source is required.'
    }
    $files = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach ($entry in $value.payload) {
        if (-not $entry.path -or $entry.path.StartsWith('/') -or $entry.path.Contains('\') -or $entry.path.Contains(':') -or
            $entry.path.Split('/') -contains '..' -or $entry.sha256 -cnotmatch '^[0-9a-f]{64}$' -or $entry.bytes -lt 0 -or $files.ContainsKey($entry.path)) {
            throw 'Unsafe, duplicate or incomplete payload manifest entry.'
        }
        $files.Add($entry.path, $entry)
    }
    return @{ verification=$value; files=$files; reportSha256=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
}
$baseline = Read-Manifest $BaselineVerification $ExpectedBaselineRevision
$candidate = Read-Manifest $CandidateVerification $ExpectedCandidateRevision
$paths = [Collections.Generic.SortedSet[string]]::new([StringComparer]::Ordinal)
foreach ($path in $baseline.files.Keys) { [void]$paths.Add($path) }
foreach ($path in $candidate.files.Keys) { [void]$paths.Add($path) }
$delta = @(); $unchanged = 0
foreach ($path in $paths) {
    $before = $baseline.files[$path]; $after = $candidate.files[$path]
    if ($before -and $after -and $before.sha256 -ceq $after.sha256 -and $before.bytes -eq $after.bytes) { $unchanged++; continue }
    $kind = if (-not $before) { 'added' } elseif (-not $after) { 'removed' } else { 'changed' }
    $delta += [ordered]@{ path=$path; change=$kind; baseline=$before; candidate=$after }
}
$report = [ordered]@{
    schemaVersion=1; status='PASS'; scope='Complete payload byte delta, not behavioural equivalence or reproducible-build certification'; executionPerformed=$false
    baseline=@{ sourceRevision=$ExpectedBaselineRevision; version=$baseline.verification.version; portableSha256=$baseline.verification.portableSha256; reportSha256=$baseline.reportSha256; files=$baseline.files.Count }
    candidate=@{ sourceRevision=$ExpectedCandidateRevision; sourceTree=$candidate.verification.sourceTree; version=$candidate.verification.version; portableSha256=$candidate.verification.portableSha256; reportSha256=$candidate.reportSha256; files=$candidate.files.Count }
    unchanged=$unchanged; differences=$delta
}
$destination = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $destination -Encoding utf8NoBOM
"Complete native payload delta: $($delta.Count) changed/added/removed paths; $unchanged unchanged."
