[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Installer,
    [ValidatePattern('^[0-9a-fA-F]{40}$')][string]$ExpectedSourceRevision,
    [ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256,
    [switch]$EnvironmentOnly
)
$ErrorActionPreference = 'Stop'
# Read-only preflight: never installs, launches, removes software or creates hosted-runner markers.
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Evidence destination already exists; retain it and choose a new directory.' }
if (-not $EnvironmentOnly -and (-not $Installer -or -not $ExpectedSourceRevision -or -not $ExpectedSha256)) {
    throw 'Package verification requires an installer and independently verified source/hash pins.'
}
$windows = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$tools = @('dotnet', 'node', 'npm', 'devenv', 'msbuild', 'code', 'python') | ForEach-Object {
    $command = Get-Command $_ -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { [pscustomobject]@{ Name = $_; Path = $command.Source } }
}
$visualStudio = @("${env:ProgramFiles}\Microsoft Visual Studio", "${env:ProgramFiles(x86)}\Microsoft Visual Studio") | Where-Object { Test-Path -LiteralPath $_ }
$sdkRoots = @("$env:ProgramFiles\dotnet\sdk", "${env:ProgramFiles(x86)}\dotnet\sdk") | Where-Object { Test-Path -LiteralPath $_ }
$nativeRegistration = Test-Path -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}_is1'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
$elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$package = @{ Status = 'NOT RUN'; Reason = 'Environment-only preflight.' }
if (-not $EnvironmentOnly) {
    $installerPath = (Resolve-Path -LiteralPath $Installer).Path
    $metadataPath = Join-Path ([IO.Path]::GetDirectoryName($installerPath)) 'version.json'
    if ((Get-Item -LiteralPath $metadataPath).Length -gt 65536) { throw 'Version metadata exceeds the validation limit.' }
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash
    if ($actualHash -ne $ExpectedSha256) { throw 'Installer hash differs from the verified CI artefact.' }
    if ($metadata.schemaVersion -ne 1 -or $metadata.product -ne 'LensDocsStudio.Native' -or
        $metadata.sourceRevision -ne $ExpectedSourceRevision -or $metadata.sourceDirty -ne $false -or
        $metadata.channel -ne 'Preview' -or $metadata.architecture -ne 'x64' -or
        $metadata.selfContained -ne $true -or $metadata.internalPreview -ne $true) {
        throw 'Package identity does not match the pinned branch-source native preview.'
    }
    $package = @{ Status = 'PASS'; Scope = 'Pinned installer bytes and adjacent metadata only; application execution not tested.';
        Installer = $installerPath; Sha256 = $actualHash; Metadata = $metadata }
}
$reasons = @()
if (@($tools).Count -gt 0 -or @($visualStudio).Count -gt 0 -or @($sdkRoots).Count -gt 0) { $reasons += 'Development tools or SDK installation detected.' }
if ($nativeRegistration) { $reasons += 'Existing native installation detected.' }
if ($elevated) { $reasons += 'Elevated process cannot establish standard-user installation acceptance.' }
$qualification = if ($reasons.Count -gt 0) { 'BLOCKED' } else { 'PARTIAL' }
$steps = @('Installation wizard', 'First launch', 'Workspace creation/opening', 'Markdown editing', 'Image insertion',
    'Mermaid preview', 'Save/reopen', 'DOCX/PDF export', 'Close/restart', 'Uninstallation', 'Preserved document verification', 'Reinstallation/state restoration') |
    ForEach-Object { @{ Requirement = $_; Status = 'NOT RUN'; Result = 'Preflight does not execute product or installer actions.' } }
$evidence = @{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('o'); Package = $package
    Environment = @{ RegistryProductLabel = $windows.ProductName; DisplayVersion = $windows.DisplayVersion;
        Build = $windows.CurrentBuildNumber; Revision = $windows.UBR; Elevated = $elevated;
        DevelopmentCommands = @($tools); VisualStudioRoots = @($visualStudio); SdkRoots = @($sdkRoots); ExistingNativeInstallation = $nativeRegistration }
    EnvironmentQualification = @{ Status = $qualification; Reasons = $reasons; CleanMachineCertified = $false;
        Remaining = @('An independently documented supported Windows 11 build', 'Disposable VM image and snapshot provenance',
            'Absence of development tools outside PATH', 'Standard-user permissions', 'Actual packaged product execution with retained artefacts') }
    Acceptance = $steps; InstallerExecuted = $false; SystemSettingsChanged = $false
}
New-Item -ItemType Directory -Path $destination | Out-Null
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $destination 'preflight.json') -Encoding UTF8
Write-Output ('Package: ' + $package.Status + '; environment qualification: ' + $qualification + '; clean-machine certification: unverified.')
