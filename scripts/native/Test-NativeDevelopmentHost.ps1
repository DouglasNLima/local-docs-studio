[CmdletBinding()]
param([string]$OutputDirectory, [switch]$DryRun)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeDevelopmentHostSafety.ps1')
# Host observation is read-only. Only an explicitly supplied fresh evidence directory is written.
$destination = $null
if ($OutputDirectory) {
    $repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $destination = Assert-NativeHostPath $OutputDirectory (Join-Path $repo 'artifacts/native')
    if (Test-Path -LiteralPath $destination) { throw 'Evidence already exists; choose a fresh directory.' }
}
$observation = Get-NativeHostObservation
$status = 'PASS'; $reason = 'Development tools are metadata; standard-user portable isolation is eligible.'
try { Assert-NativeHostQualification $observation } catch { $status='BLOCKED'; $reason=$_.Exception.Message }
$receipt = [ordered]@{ recordedAtUtc=[DateTime]::UtcNow.ToString('o'); status=$status; reason=$reason; observation=$observation;
    appExecuted=$false; installerExecuted=$false; screenReader='NOT RUN'; alternateDpi='NOT RUN' }
if ($OutputDirectory -and -not $DryRun) {
    [void][IO.Directory]::CreateDirectory($destination)
    Write-NativeHostReceipt (Join-Path $destination 'host-preflight.json') $receipt
}
$receipt | ConvertTo-Json -Depth 30
