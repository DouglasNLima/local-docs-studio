[CmdletBinding()]
param([Parameter(Mandatory)][string]$Installer, [string]$UpgradeInstaller)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Installer validation requires a disposable GitHub-hosted Windows runner.'
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Installer = (Resolve-Path -LiteralPath $Installer).Path
if ($UpgradeInstaller) { $UpgradeInstaller = (Resolve-Path -LiteralPath $UpgradeInstaller).Path }
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}_is1'
if (Test-Path -LiteralPath $uninstallKey) { throw 'A native installation already exists. This test will not replace it.' }
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Lens Docs Studio Native Preview/Lens Docs Studio Native Preview.lnk'
$desktopShortcut = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Lens Docs Studio Native Preview.lnk'
if ((Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath $desktopShortcut)) { throw 'Native shortcuts already exist; preserve them and use a disposable environment.' }
$root = Join-Path $repo ('artifacts/native/install-smoke/' + [guid]::NewGuid().ToString('N'))
$installDirectory = Join-Path $root 'installed'
New-Item -ItemType Directory -Path $root -Force | Out-Null
$checks = [Collections.Generic.List[string]]::new()
$installedVersions = [Collections.Generic.List[object]]::new()
$shortcutIdentities = [Collections.Generic.List[object]]::new()
$preservedStateRoot = $null
$preservedStateSnapshot = $null
$defaultBefore = (Get-ItemProperty 'HKCU:\Software\Classes\.md' -ErrorAction SilentlyContinue).'(default)'
$legacyBefore = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{E5D86651-4B27-4C59-B7D4-2EA98F2F8D26}_is1' -ErrorAction SilentlyContinue | ConvertTo-Json -Compress
$workspace = Join-Path $root 'workspace'; New-Item -ItemType Directory -Path $workspace | Out-Null
$document = Join-Path $workspace 'preserve.md'; [IO.File]::WriteAllText($document, "# Installer preservation fixture`n`nCafé remains portable.")
$documentHash = (Get-FileHash -LiteralPath $document).Hash
function Get-PackageIdentity([string]$Path) {
    $metadataPath = Join-Path ([IO.Path]::GetDirectoryName($Path)) 'version.json'
    if ((Get-Item -LiteralPath $metadataPath).Length -gt 65536) { throw 'Package version metadata exceeds the validation limit.' }
    $identity = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    if ($identity.schemaVersion -ne 1 -or $identity.product -ne 'LensDocsStudio.Native' -or
        $identity.version -notmatch '^\d+\.\d+\.\d+-preview\.\d+$') { throw 'Package version metadata does not identify a native preview.' }
    return $identity
}
function Get-StateSnapshot([string]$Path) {
    $snapshot = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in Get-ChildItem -LiteralPath $Path -Recurse -File) {
        $snapshot.Add([IO.Path]::GetRelativePath($Path, $file.FullName), (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash)
    }
    return $snapshot
}
function Assert-PreservedState([string]$Phase) {
    if ($null -eq $preservedStateSnapshot) { return }
    $current = Get-StateSnapshot $preservedStateRoot
    if ($current.Count -ne $preservedStateSnapshot.Count) { throw "${Phase}: native state file inventory changed." }
    foreach ($entry in $preservedStateSnapshot.GetEnumerator()) {
        if (-not $current.ContainsKey($entry.Key) -or $current[$entry.Key] -ne $entry.Value) { throw "${Phase}: native state bytes changed: $($entry.Key)." }
    }
    if ((Get-FileHash -LiteralPath $document -Algorithm SHA256).Hash -ne $documentHash) { throw "${Phase}: workspace bytes changed." }
    $checks.Add("${Phase}: preserved $($current.Count) existing native state files and workspace source byte-for-byte")
}
$initialIdentity = Get-PackageIdentity $Installer
if ($UpgradeInstaller) {
    $upgradeIdentity = Get-PackageIdentity $UpgradeInstaller
    if ($upgradeIdentity.version -eq $initialIdentity.version) { throw 'The simulated upgrade requires distinct package versions.' }
}
function Run-Installer([string]$Path, [string]$Phase) {
    $expectedIdentity = Get-PackageIdentity $Path
    $process = Start-Process -FilePath $Path -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="' + $installDirectory + '"'),'/TASKS="fileassoc,desktopicon"',('/LOG="' + (Join-Path $root ($Phase + '.log')) + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$Phase failed with exit code $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $uninstallKey)) { throw 'The native uninstall registration is missing.' }
    if (-not (Test-Path -LiteralPath $shortcut) -or -not (Test-Path -LiteralPath $desktopShortcut)) { throw 'Installer shortcuts are missing.' }
    $applicationPath = Join-Path $installDirectory 'LensDocsStudio.Native.exe'
    $shortcutReader = New-Object -ComObject WScript.Shell
    try {
        foreach ($shortcutPath in $shortcut, $desktopShortcut) {
            $link = $shortcutReader.CreateShortcut($shortcutPath)
            try {
                $iconParts = $link.IconLocation.Split(',')
                $iconPath = [Environment]::ExpandEnvironmentVariables($iconParts[0].Trim('"'))
                if ($link.TargetPath -ne $applicationPath -or ($iconPath -and $iconPath -ne $applicationPath) -or
                    ($iconParts.Count -gt 1 -and $iconParts[-1].Trim() -ne '0')) { throw "${Phase}: shortcut target or icon does not use the verified native application." }
                $shortcutIdentities.Add(@{ phase=$Phase; shortcut=$shortcutPath; target=$link.TargetPath; icon=$link.IconLocation; verifiedApplicationIcon=$true })
            } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) }
        }
    } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcutReader) }
    $association = (Get-ItemProperty 'HKCU:\Software\Classes\LensDocsStudio.Native.Document\shell\open\command').'(default)'
    if (-not $association.Contains($installDirectory)) { throw 'Open with does not target the isolated native installation.' }
    $installedIdentity = Get-Content -LiteralPath (Join-Path $installDirectory 'version.json') -Raw | ConvertFrom-Json
    $registeredVersion = (Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion
    $binaryVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $installDirectory 'LensDocsStudio.Native.dll')).ProductVersion.Split('+')[0]
    if ($installedIdentity.version -ne $expectedIdentity.version -or $installedIdentity.sourceRevision -ne $expectedIdentity.sourceRevision -or
        $registeredVersion -ne $expectedIdentity.version -or $binaryVersion -ne $expectedIdentity.version) {
        throw "${Phase}: installed metadata, registration or binary does not match the requested package version."
    }
    $installedVersions.Add(@{ phase=$Phase; expected=$expectedIdentity.version; metadata=$installedIdentity.version; registration=$registeredVersion; binary=$binaryVersion })
    $checks.Add("${Phase}: installed self-contained application, shortcuts and Open with registration")
    Assert-PreservedState $Phase
}
function Run-NativeUi([string]$Phase) {
    Push-Location $repo
    try {
        & dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- --ui (Join-Path $installDirectory 'LensDocsStudio.Native.exe') (Join-Path $root $Phase) --require-bundled-runtime
        if ($LASTEXITCODE -ne 0) { throw "Installed application UI validation failed during $Phase." }
    } finally { Pop-Location }
    $checks.Add("${Phase}: installed application saved and restored UTF-8 source")
}
function Restore-PreservedSession([string]$Phase) {
    & dotnet run --project (Join-Path $repo 'src/native/LensDocsStudio.Native.Validation') -c Release --no-build -- --restore-installed-session (Join-Path $installDirectory 'LensDocsStudio.Native.exe') $preservedStateRoot (Join-Path $root $Phase)
    if ($LASTEXITCODE -ne 0) { throw "The installed application could not restore the preserved session during $Phase." }
    # The application has verified semantic continuity and all other file hashes.
    # Its normal shutdown can rotate session/preferences backups and save viewport positions.
    $script:preservedStateSnapshot = Get-StateSnapshot $preservedStateRoot
    $checks.Add("${Phase}: installed application restored the actual previous tabs, text, settings and retained state")
}
function Uninstall-Native([string]$Phase) {
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $root ($Phase + '.log')) + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstall failed with exit code $($process.ExitCode)." }
    if ((Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath $desktopShortcut)) { throw 'Uninstall left native registration or shortcuts.' }
    if ((Test-Path -LiteralPath 'HKCU:\Software\Classes\LensDocsStudio.Native.Document')) { throw 'Uninstall left its native association registration.' }
    if ((Get-FileHash -LiteralPath $document).Hash -ne $documentHash) { throw 'Installer lifecycle changed a workspace document.' }
    $checks.Add("${Phase}: removed native installation and preserved workspace source")
    Assert-PreservedState $Phase
}
try {
    Run-Installer $Installer 'install'
    Run-NativeUi 'first-launch'
    $firstLaunchRoots = @(Get-ChildItem -LiteralPath (Join-Path $root 'first-launch') -Directory -Filter 'ui-*')
    if ($firstLaunchRoots.Count -ne 1) { throw 'The initial UI run did not produce exactly one isolated state directory.' }
    $preservedStateRoot = Join-Path $firstLaunchRoots[0].FullName 'state'
    $preservedStateSnapshot = Get-StateSnapshot $preservedStateRoot
    if ($preservedStateSnapshot.Count -eq 0 -or -not $preservedStateSnapshot.ContainsKey('preferences.json')) { throw 'The initial UI run did not retain native preferences for preservation checks.' }
    if ($UpgradeInstaller) { Run-Installer $UpgradeInstaller 'upgrade'; Restore-PreservedSession 'restored-after-upgrade'; Run-NativeUi 'after-upgrade' }
    Uninstall-Native 'uninstall'
    Run-Installer $(if ($UpgradeInstaller) { $UpgradeInstaller } else { $Installer }) 'reinstall'
    Restore-PreservedSession 'restored-after-reinstall'
    Run-NativeUi 'after-reinstall'
    & dotnet run --project (Join-Path $repo 'src/native/LensDocsStudio.Native.Validation') -c Release --no-build -- --interrupted-save (Join-Path $installDirectory 'LensDocsStudio.Native.exe') (Join-Path $root 'interrupted-save')
    if ($LASTEXITCODE -ne 0) { throw 'The installed application failed interrupted-save recovery.' }
    $checks.Add('reinstall: interrupted failed save restored exact draft and baseline in a new application process')
    Uninstall-Native 'final-uninstall'
    $defaultAfter = (Get-ItemProperty 'HKCU:\Software\Classes\.md' -ErrorAction SilentlyContinue).'(default)'
    $legacyAfter = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{E5D86651-4B27-4C59-B7D4-2EA98F2F8D26}_is1' -ErrorAction SilentlyContinue | ConvertTo-Json -Compress
    if ($defaultBefore -ne $defaultAfter -or $legacyBefore -ne $legacyAfter) { throw 'The legacy registration or Markdown default changed.' }
    $checks.Add('Existing shell registration and Markdown default preserved')
    @{ passed=$true; checks=$checks; installedVersions=$installedVersions; shortcutIdentities=$shortcutIdentities; preservedStateFiles=$preservedStateSnapshot.Count; evidence=$root;
        cleanMachineCertification=$false; upgradeTested=[bool]$UpgradeInstaller; productionSignedUpgrade=$false; sessionRestorationTested=$true;
        sameRevisionUpgrade=[bool]($UpgradeInstaller -and $upgradeIdentity.sourceRevision -eq $initialIdentity.sourceRevision);
        upgradeLimitation='Preview package replacement and preservation only; production signing, historical schema migration and SDK-free clean-machine certification remain separate.' } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'installer-results.json')
    "Native installer lifecycle passed. Evidence: $root"
} catch {
    @{ passed=$false; checks=$checks; installedVersions=$installedVersions; error=$_.Exception.Message; evidence=$root } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'installer-results.json')
    throw
}
