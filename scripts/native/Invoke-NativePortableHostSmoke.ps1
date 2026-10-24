[CmdletBinding()]
param(
    [ValidateSet('Plan','Prepare','Launch','RunUi','Complete')][string]$Mode = 'Plan',
    [Parameter(Mandatory)][string]$SandboxRoot,
    [string]$PackageDirectory,
    [ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceRevision,
    [ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceTree,
    [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedInstallerSha256,
    [ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedZipSha256,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeDevelopmentHostSafety.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed = Join-Path $repo 'artifacts/native/development-host'
$root = Assert-NativeHostPath $SandboxRoot $allowed
if ($root -notmatch '[\\/]([0-9a-f]{40})[\\/]([A-Za-z0-9-]+)$') { throw 'Use development-host/<package-source-SHA>/<fresh-session-name>.' }
$folderRevision = $Matches[1]
$markerPath = Join-Path $root 'host-sandbox.json'
if ($DryRun -or $Mode -eq 'Plan') {
    if (Test-Path -LiteralPath $root) { throw 'Plan requires a fresh, absent sandbox; it will never reuse old evidence.' }
    @{ status='NOT RUN'; mode='DRY RUN'; qualification='DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION'; sandbox=$root;
        effects=@('Prepare writes verified payload and synthetic files only inside this new sandbox.', 'Launch opens the portable app visibly with explicit state/workspace arguments.', 'Complete observes host snapshots and writes a new receipt; no automatic cleanup.');
        installer='NOT RUN — UNSAFE SHARED REGISTRATION'; cleanMachineCertified=$false } | ConvertTo-Json -Depth 6
    return
}
if ($Mode -eq 'Prepare') {
    if (Test-Path -LiteralPath $root) { throw 'Sandbox already exists. Preserve it and choose a new session.' }
    if (-not $ExpectedSourceRevision -or -not $ExpectedSourceTree -or -not $ExpectedInstallerSha256 -or -not $ExpectedZipSha256 -or -not $PackageDirectory) {
        throw 'Independent source/tree/installer/portable pins and package directory are mandatory.'
    }
    if ($folderRevision -cne $ExpectedSourceRevision) { throw 'Evidence folder source does not match the expected package source.' }
    $package = Assert-NativeHostPath $PackageDirectory
    $metadata = Get-Content -LiteralPath (Join-Path $package 'version.json') -Raw | ConvertFrom-Json
    Assert-NativeHostIdentity $metadata $ExpectedSourceRevision $ExpectedSourceTree
    $harnessRevision = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or (& git -C $repo status --porcelain)) { throw 'Prepare requires clean committed harness source.' }
    $before = Get-NativeHostObservation
    Assert-NativeHostQualification $before
    [void][IO.Directory]::CreateDirectory($root)
    Write-NativeHostReceipt (Join-Path $root 'host-before.json') $before
    & (Join-Path $PSScriptRoot 'Test-NativePreviewPackage.ps1') -PackageDirectory $package -ExpectedVersion $metadata.version `
        -ExpectedSourceRevision $ExpectedSourceRevision -ExpectedSourceTree $ExpectedSourceTree `
        -ExpectedInstallerSha256 $ExpectedInstallerSha256 -ExpectedZipSha256 $ExpectedZipSha256 -OutputPath (Join-Path $root 'package-verification.json')
    $verified = Get-Content -LiteralPath (Join-Path $root 'package-verification.json') -Raw | ConvertFrom-Json
    if ($verified.status -cne 'PASS' -or $verified.payload.Count -ne 528) { throw 'Incomplete package inspection.' }
    $payload = Join-Path $root 'payload'; [void][IO.Directory]::CreateDirectory($payload)
    $portable = Join-Path $package ("LensDocsStudio.Native-$($metadata.version)-win-x64.zip")
    # The independent verifier has rejected traversal, duplicates, active/development/secret payloads and incorrect runtimes.
    [IO.Compression.ZipFile]::ExtractToDirectory($portable, $payload, $false)
    Assert-NativeHostPayload $verified.payload $payload
    $workspace = Join-Path $root 'workspace'; $state = Join-Path $root 'state'
    foreach ($path in $workspace,$state,(Join-Path $workspace 'assets'),(Join-Path $root 'exports')) { [void][IO.Directory]::CreateDirectory($path) }
    [IO.File]::WriteAllText((Join-Path $workspace 'acceptance-lf.md'), @'
# Café😀🚀 — synthetic acceptance

Only synthetic content belongs in this workspace.

![Synthetic orange square](assets/square.png)

[Relative document](relative.md)

## Duplicate heading with a deliberately long descriptive name for keyboard selection

## Duplicate heading with a deliberately long descriptive name for keyboard selection

| Item | Value |
| --- | --- |
| Unicode | Café😀🚀 |

- First item
- Second item

<details><summary>Details</summary>

Synthetic details body.

</details>

```csharp
public static string Greeting() => "Café😀🚀";
```

```mermaid
flowchart LR
A[Write] --> B[Review]
```
'@.Replace("`r`n","`n"), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $workspace 'relative.md'), "# Relative synthetic document`n`nCafé😀🚀`n")
    [IO.File]::WriteAllText((Join-Path $workspace 'acceptance-crlf.md'), "# CRLF Café😀🚀`r`n`r`nSynthetic bytes.`r`n")
    [IO.File]::WriteAllText((Join-Path $workspace 'standalone.mmd'), "flowchart LR`nA[Write] --> B[Review]`n")
    [IO.File]::WriteAllText((Join-Path $workspace 'invalid.mmd'), "flowchart LR`nA[unfinished`n")
    Add-Type -AssemblyName System.Drawing
    $image = [Drawing.Bitmap]::new(32,32)
    try { $graphics=[Drawing.Graphics]::FromImage($image); try { $graphics.Clear([Drawing.Color]::FromArgb(255,136,62)) } finally { $graphics.Dispose() }; $image.Save((Join-Path $workspace 'assets/square.png'), [Drawing.Imaging.ImageFormat]::Png) } finally { $image.Dispose() }
    # Prevent even an optional network update query. All library/template/history/recovery paths derive from this state root.
    Write-NativeHostReceipt (Join-Path $state 'preferences.json') @{ AutomaticUpdateChecks=$false; Theme='Light'; RecentDocuments=@(); RecentWorkspaces=@() }
    $baseline = @{ workspace=@(Get-NativeHostManifest $workspace); state=@(Get-NativeHostManifest $state) }
    Write-NativeHostReceipt (Join-Path $root 'synthetic-before.json') $baseline
    $marker = [ordered]@{ schemaVersion=1; qualification='DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION'; cleanMachineCertified=$false;
        preparedAtUtc=[DateTime]::UtcNow.ToString('o'); harnessRevision=$harnessRevision;
        sourceRevision=$ExpectedSourceRevision; sourceTree=$ExpectedSourceTree; portableSha256=$ExpectedZipSha256;
        executableSha256=(Get-FileHash -LiteralPath (Join-Path $payload 'LensDocsStudio.Native.exe')).Hash.ToLowerInvariant();
        baselineSha256=(Get-FileHash -LiteralPath (Join-Path $root 'synthetic-before.json')).Hash.ToLowerInvariant();
        packageReceiptSha256=(Get-FileHash -LiteralPath (Join-Path $root 'package-verification.json')).Hash.ToLowerInvariant();
        hostBeforeSha256=(Get-FileHash -LiteralPath (Join-Path $root 'host-before.json')).Hash.ToLowerInvariant();
        installer='NOT RUN — UNSAFE SHARED REGISTRATION'; rollback='Close only this test PID normally. Preserve this sandbox. Never restore host registry/state or kill unrelated processes.' }
    Write-NativeHostReceipt $markerPath $marker
    'PASS: verified synthetic sandbox prepared; GUI acceptance NOT RUN.'
    return
}
$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
$age = ([DateTime]::UtcNow - [DateTime]::Parse($marker.preparedAtUtc).ToUniversalTime()).TotalHours
if ($marker.schemaVersion -ne 1 -or $marker.qualification -cne 'DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION' -or
    $marker.cleanMachineCertified -ne $false -or $marker.sourceRevision -cne $folderRevision -or
    $age -lt 0 -or $age -gt 24) {
    throw 'Missing, stale or incorrectly classified host sandbox. Prepare a new source-labelled session.'
}
foreach ($binding in @(@('synthetic-before.json','baselineSha256'),@('package-verification.json','packageReceiptSha256'),@('host-before.json','hostBeforeSha256'))) {
    if ((Get-FileHash -LiteralPath (Join-Path $root $binding[0])).Hash -ine $marker.($binding[1])) { throw 'Prepared evidence binding changed.' }
}
$verified = Get-Content -LiteralPath (Join-Path $root 'package-verification.json') -Raw | ConvertFrom-Json
$before = Get-Content -LiteralPath (Join-Path $root 'host-before.json') -Raw | ConvertFrom-Json
$current = Get-NativeHostObservation
if ($Mode -eq 'Complete') { Write-NativeHostReceipt (Join-Path $root ('host-after-' + [guid]::NewGuid().ToString('N') + '.json')) $current }
Assert-NativeHostQualification $current
Assert-NativeHostUnchanged $before $current
$payload = Join-Path $root 'payload'; $state = Join-Path $root 'state'; $workspace = Join-Path $root 'workspace'
foreach ($path in $payload,$state,$workspace) { [void](Assert-NativeHostPath $path $root); @(Get-NativeHostFiles $path) | Out-Null }
Assert-NativeHostPayload $verified.payload $payload
if ($Mode -eq 'RunUi') {
    if (Test-Path -LiteralPath (Join-Path $root 'complete.json')) { throw 'Completed evidence cannot be reused.' }
    $client = Join-Path $repo 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($client).ProductVersion
    if (-not $version.EndsWith('+' + $marker.harnessRevision, [StringComparison]::Ordinal)) { throw 'UI client must be built from the clean committed preparation source.' }
    & $client --development-host-ui $root $marker.sourceRevision $marker.sourceTree $marker.executableSha256
    if ($LASTEXITCODE -ne 0) { throw 'Host UI acceptance failed. Preserve the sandbox and inspect the actual test process; no uncertain retry.' }
} elseif ($Mode -eq 'Launch') {
    if (Test-Path -LiteralPath (Join-Path $root 'complete.json')) { throw 'Completed evidence cannot be reused for another launch.' }
    $preferences = Get-Content -LiteralPath (Join-Path $state 'preferences.json') -Raw | ConvertFrom-Json
    if ($preferences.AutomaticUpdateChecks -ne $false) { throw 'Synthetic state must disable automatic update queries.' }
    $info = [Diagnostics.ProcessStartInfo]::new((Join-Path $payload 'LensDocsStudio.Native.exe'))
    $info.UseShellExecute=$false; $info.WorkingDirectory=$workspace
    foreach ($argument in '--state-directory',$state,$workspace,(Join-Path $workspace 'acceptance-lf.md')) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($info)
    Write-NativeHostReceipt (Join-Path $root ('launch-' + [guid]::NewGuid().ToString('N') + '.json')) @{
        status='NOT RUN'; scope='Process launch only; no GUI assertion'; processId=$process.Id; startTimeUtc=$process.StartTime.ToUniversalTime().ToString('o');
        sourceRevision=$marker.sourceRevision; executableSha256=$marker.executableSha256; arguments=@('--state-directory','<sandbox>/state','<sandbox>/workspace','<sandbox>/workspace/acceptance-lf.md') }
    "Portable test process started: $($process.Id). Observe its actual window; launch alone is not acceptance."
} else {
    $final = @{ status='PASS'; scope='Observed associations, registrations, shortcuts and default private-state bytes unchanged; not an exhaustive system-write trace.';
        sourceRevision=$marker.sourceRevision; sourceTree=$marker.sourceTree; portableSha256=$marker.portableSha256;
        host=$current; synthetic=@{ workspace=@(Get-NativeHostManifest $workspace); state=@(Get-NativeHostManifest $state); exports=@(Get-NativeHostManifest (Join-Path $root 'exports')) };
        guiAcceptance='Use separate source-labelled UI completion records; never infer from this receipt.'; cleanMachineCertified=$false }
    Write-NativeHostReceipt (Join-Path $root 'complete.json') $final
    'PASS: bounded host preservation checks complete. Review separate GUI outcomes and synthetic file deltas.'
}
