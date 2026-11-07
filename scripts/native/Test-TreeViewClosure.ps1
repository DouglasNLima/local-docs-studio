param(
    [Parameter(Mandatory)][string]$CandidatePackageNumber,
    [ValidateSet('baseline-first','candidate-first')][string]$Order = 'baseline-first',
    [ValidateSet('0','1')][string]$Trace = '1'
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Installed lifecycle observations require a disposable hosted Windows runner.'
}
$root=[IO.Path]::GetFullPath('artifacts/native/treeview-closure')
if (Test-Path -LiteralPath $root) { throw 'Retain the original evidence; use a fresh directory.' }
New-Item -ItemType Directory -Path $root | Out-Null
$revision=(git rev-parse HEAD).Trim()
@{source=$revision;tree=(git rev-parse 'HEAD^{tree}').Trim();order=$Order;trace=$Trace;
    baseline='358d2a6f4b8028461fb535f2f71a9b7c71636760';originalRun=42391694305;originalArtifact=13582703336;
    baselinePackageNumber='1050';candidatePackageNumber=$CandidatePackageNumber;exitDeadlineSeconds=20;
    scope='Identical observer; separately verified installed product identities. Full install/upgrade/reinstall; no retries.'} |
    ConvertTo-Json | Set-Content "$root/protocol.json" -Encoding utf8NoBOM
$outcomes=[Collections.Generic.List[object]]::new()
$sides=if ($Order -eq 'baseline-first') { @('baseline','candidate') } else { @('candidate','baseline') }
$env:LENS_NATIVE_CLOSURE_TRACE=$Trace
foreach ($side in $sides) {
    $number=if ($side -eq 'baseline') { '1050' } else { $CandidatePackageNumber }
    $source=if ($side -eq 'baseline') { '358d2a6f4b8028461fb535f2f71a9b7c71636760' } else { $revision }
    $before=@(Get-ChildItem artifacts/native/treeview-install-smoke -Directory -ErrorAction SilentlyContinue | ForEach-Object FullName)
    $failure=$null
    try {
        & "$PSScriptRoot/Test-NativeClassInstaller.ps1" -Family TreeView -Installer "artifacts/treeview-$side/packages/0.1.0-preview.$number/LensDocsStudio.Native-0.1.0-preview.$number-win-x64-Setup.exe" -UpgradeInstaller "artifacts/treeview-$side/packages/0.1.1-preview.$number/LensDocsStudio.Native-0.1.1-preview.$number-win-x64-Setup.exe" -ExpectedSourceRevision $source -NoFileAssociations
    } catch { $failure=$_.ToString(); Write-Warning $failure }
    $directories=@(Get-ChildItem artifacts/native/treeview-install-smoke -Directory -ErrorAction SilentlyContinue | Where-Object FullName -notin $before | ForEach-Object FullName)
    $outcomes.Add(@{side=$side;source=$source;failure=$failure;directories=$directories})
    $outcomes | ConvertTo-Json -Depth 5 | Set-Content "$root/processes.json" -Encoding utf8NoBOM
}
if (@($outcomes | Where-Object failure).Count) { throw 'TreeView closure has retained failed lifecycle observations.' }
