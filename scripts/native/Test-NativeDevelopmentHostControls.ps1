[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeDevelopmentHostSafety.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $repo ('artifacts/native/host-controls/' + [guid]::NewGuid().ToString('N')) }
$root = Assert-NativeHostPath $OutputDirectory (Join-Path $repo 'artifacts')
if (Test-Path -LiteralPath $root) { throw 'Controls require a fresh output directory; failures are never overwritten.' }
[void][IO.Directory]::CreateDirectory($root)
$checks = [Collections.Generic.List[object]]::new()
function Test-Rejection([string]$Name, [scriptblock]$Action) {
    $rejected=$false
    try { & $Action | Out-Null } catch { $rejected=$true }
    if (-not $rejected) { throw "Negative control accepted: $Name" }
    $checks.Add(@{ name=$Name; status='PASS'; expected='REJECT' })
}
function Test-Acceptance([string]$Name, [scriptblock]$Action) {
    & $Action | Out-Null
    $checks.Add(@{ name=$Name; status='PASS'; expected='ACCEPT' })
}
Test-Acceptance 'Canonical synthetic descendant' { Assert-NativeHostPath (Join-Path $root 'inside') $root }
Test-Rejection 'Sibling-prefix escape' { Assert-NativeHostPath ($root + '-outside/file') $root }
Test-Rejection 'Parent is not a descendant' { Assert-NativeHostPath $root $root }
Test-Rejection 'Relative path' { Assert-NativeHostPath './relative' }
Test-Rejection 'Traversal' { Assert-NativeHostPath (Join-Path $root '../outside') $root }
Test-Rejection 'Alternate data stream' { Assert-NativeHostPath (Join-Path $root 'file:stream') $root }
Test-Rejection 'UNC/device path' { Assert-NativeHostPath '\\?\C:\unsafe' }
Test-Rejection 'Wildcard path' { Assert-NativeHostPath (Join-Path $root '*') $root }
$target=Join-Path $root 'target'; [void][IO.Directory]::CreateDirectory($target)
$junction=Join-Path $root 'junction'
New-Item -ItemType Junction -Path $junction -Target $target | Out-Null
Test-Rejection 'Junction ancestor escape' { Assert-NativeHostPath (Join-Path $junction 'file') $root }
Test-Rejection 'Junction inside manifest tree' { Get-NativeHostManifest $root }
$safe=Join-Path $root 'safe'; [void][IO.Directory]::CreateDirectory($safe)
$receipt=Join-Path $safe 'receipt.json'
Write-NativeHostReceipt $receipt @{ status='FAIL'; retained=$true }
Test-Rejection 'Existing receipt cannot be overwritten' { Write-NativeHostReceipt $receipt @{ status='PASS' } }
Test-Acceptance 'Original failure receipt remains exact' { if ((Get-Content $receipt -Raw | ConvertFrom-Json).status -cne 'FAIL') { throw 'Failure lost' } }
$revision='1'*40; $tree='2'*40
$identity=@{ schemaVersion=1; product='LensDocsStudio.Native'; sourceRevision=$revision; sourceTree=$tree; sourceDirty=$false;
    selfContained=$true; internalPreview=$true; signingRequested=$false; signingProvider='Unsigned'; architecture='x64' }
Test-Acceptance 'Exact unsigned source/tree identity' { Assert-NativeHostIdentity ([pscustomobject]$identity) $revision $tree }
Test-Rejection 'Wrong committed source' { Assert-NativeHostIdentity ([pscustomobject]$identity) ('3'*40) $tree }
Test-Rejection 'Wrong committed tree' { Assert-NativeHostIdentity ([pscustomobject]$identity) $revision ('3'*40) }
Test-Rejection 'Missing identity data' { Assert-NativeHostIdentity ([pscustomobject]@{}) $revision $tree }
$dirty=$identity.Clone(); $dirty.sourceDirty=$true
Test-Rejection 'Dirty-source package' { Assert-NativeHostIdentity ([pscustomobject]$dirty) $revision $tree }
$qualification=@{ qualification='DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION'; cleanMachineCertified=$false; elevated=$false;
    activeConsoleSession=$true; inputDesktopAccessible=$true; interactive=$true; nativeProcesses=@(); tools=@('dotnet','node','python') }
Test-Acceptance 'SDK presence does not reject development-host classification' { Assert-NativeHostQualification ([pscustomobject]$qualification) }
foreach ($field in 'elevated','cleanMachineCertified') {
    $invalid=$qualification.Clone(); $invalid[$field]=$true
    Test-Rejection "Reject $field" { Assert-NativeHostQualification ([pscustomobject]$invalid) }
}
foreach ($field in 'activeConsoleSession','inputDesktopAccessible','interactive') {
    $invalid=$qualification.Clone(); $invalid[$field]=$false
    Test-Rejection "Reject absent $field" { Assert-NativeHostQualification ([pscustomobject]$invalid) }
}
$invalid=$qualification.Clone(); $invalid.nativeProcesses=@(@{Id=1})
Test-Rejection 'Existing native process prevents launch' { Assert-NativeHostQualification ([pscustomobject]$invalid) }
$invalid=$qualification.Clone(); $invalid.qualification='CLEAN WINDOWS CERTIFIED'
Test-Rejection 'Cannot relabel development host as clean certification' { Assert-NativeHostQualification ([pscustomobject]$invalid) }
$before=[pscustomobject]@{ associations=@{UserChoice='initial'}; registrations=@(); shortcuts=@(); defaultState=@{exists=$false;files=@()} }
$after=$before | ConvertTo-Json -Depth 10 | ConvertFrom-Json
Test-Acceptance 'Snapshot canonicalisation survives JSON round trip' { Assert-NativeHostUnchanged $before $after }
$after.associations.UserChoice='changed'
Test-Rejection 'Default application change' { Assert-NativeHostUnchanged $before $after }
Test-Rejection 'Missing preservation snapshot data' { Assert-NativeHostUnchanged $before ([pscustomobject]@{}) }
Test-Rejection 'Fake PID and HWND' { Assert-NativeHostWindow 0 0 (Join-Path $safe 'absent.exe') ('0'*64) $revision }
Test-Rejection 'Wrong real PID/unknown HWND' { Assert-NativeHostWindow $PID 1 ([Diagnostics.Process]::GetCurrentProcess().MainModule.FileName) ('0'*64) $revision }
Test-Rejection 'Missing/incomplete payload inventory' { Assert-NativeHostPayload @() $safe }
$plan=Join-Path $repo ('artifacts/native/development-host/' + $revision + '/dry-' + [guid]::NewGuid().ToString('N'))
Test-Acceptance 'Dry-run does not create sandbox or launch app' {
    & (Join-Path $PSScriptRoot 'Invoke-NativePortableHostSmoke.ps1') -SandboxRoot $plan -DryRun
    if (Test-Path -LiteralPath $plan) { throw 'Dry-run mutated sandbox' }
}
Test-Rejection 'Missing independent package pins' { & (Join-Path $PSScriptRoot 'Invoke-NativePortableHostSmoke.ps1') -SandboxRoot $plan -Mode Prepare }
[void][IO.Directory]::CreateDirectory($plan)
Test-Rejection 'Stale/nonempty evidence cannot be reused' { & (Join-Path $PSScriptRoot 'Invoke-NativePortableHostSmoke.ps1') -SandboxRoot $plan -DryRun }
Write-NativeHostReceipt (Join-Path $plan 'host-sandbox.json') @{ schemaVersion=1; qualification='DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION'; cleanMachineCertified=$false; sourceRevision=$revision; preparedAtUtc='2000-01-01T00:00:00Z' }
Test-Rejection 'Stale launch marker cannot launch a process' { & (Join-Path $PSScriptRoot 'Invoke-NativePortableHostSmoke.ps1') -SandboxRoot $plan -Mode Launch }
foreach ($name in 'Test-NativeDevelopmentHost.ps1','Invoke-NativePortableHostSmoke.ps1','Get-NativeDevelopmentHostDisplayEvidence.ps1','NativeDevelopmentHostSafety.ps1') {
    $tokens=$null; $errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $name),[ref]$tokens,[ref]$errors)
    if ($errors.Count) { throw "PowerShell syntax error: $name" }
    $commands=$ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] },$true) | ForEach-Object { $_.GetCommandName() }
    if (@($commands | Where-Object { $_ -in 'Set-ItemProperty','New-ItemProperty','Remove-ItemProperty','Remove-Item','Stop-Process','Set-ExecutionPolicy','Import-Certificate','Install-Package' }).Count) { throw "Forbidden host mutation in $name" }
    $checks.Add(@{name="No destructive/system commands in $name";status='PASS'})
}
$installer=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Test-NativeInstaller.ps1') -Raw
if (-not $installer.Contains("`$env:RUNNER_ENVIRONMENT -ne 'github-hosted'")) { throw 'Hosted installer guard lost' }
$checks.Add(@{name='Original hosted installer guard remains mandatory';status='PASS'})
$localClient=Get-Content -LiteralPath (Join-Path $repo 'src/native/LensDocsStudio.Native.Validation/NativeDevelopmentHostUiChecks.cs') -Raw
foreach ($forbidden in 'NativeTestDisplay(', 'NativeTestHighContrast(', 'Clipboard.', 'ChangeDisplaySettings(', 'SHAppBarMessage(', 'Registry.SetValue(', '.Kill(') {
    if ($localClient.Contains($forbidden)) { throw "Local UI client contains forbidden host operation: $forbidden" }
}
$checks.Add(@{name='Local UI client cannot change display contrast clipboard registry or kill processes';status='PASS'})
Test-Rejection 'Direct local UI route refuses missing pins before launch' {
    $client=Join-Path $repo 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
    if (Test-Path -LiteralPath $client) {
        & $client --development-host-ui 2>$null
        if ($LASTEXITCODE -eq 0) { return }
        throw 'Expected missing-pin rejection'
    }
    # The static route guard also runs before the solution has been built.
    $program=Get-Content -LiteralPath (Join-Path $repo 'src/native/LensDocsStudio.Native.Validation/Program.cs') -Raw
    if ($program.Contains('if (args.Length != 5)')) { throw 'Expected mandatory-pin guard present' }
}
Write-NativeHostReceipt (Join-Path $safe 'host-controls.json') @{ status='PASS'; checks=@($checks); count=$checks.Count; qualification='DETERMINISTIC GUARD TESTS'; guiAcceptance='NOT RUN' }
"PASS: $($checks.Count) development-host guard controls. Evidence: $root"
