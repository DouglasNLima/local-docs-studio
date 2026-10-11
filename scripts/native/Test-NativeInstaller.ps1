[CmdletBinding()]
param([Parameter(Mandatory)][string]$Installer, [string]$UpgradeInstaller)
$ErrorActionPreference = 'Stop'
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
$defaultBefore = (Get-ItemProperty 'HKCU:\Software\Classes\.md' -ErrorAction SilentlyContinue).'(default)'
$legacyBefore = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{E5D86651-4B27-4C59-B7D4-2EA98F2F8D26}_is1' -ErrorAction SilentlyContinue | ConvertTo-Json -Compress
$workspace = Join-Path $root 'workspace'; New-Item -ItemType Directory -Path $workspace | Out-Null
$document = Join-Path $workspace 'preserve.md'; [IO.File]::WriteAllText($document, "# Installer preservation fixture`n`nCafé remains portable.")
$documentHash = (Get-FileHash -LiteralPath $document).Hash
function Run-Installer([string]$Path, [string]$Phase) {
    $process = Start-Process -FilePath $Path -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="' + $installDirectory + '"'),'/TASKS="fileassoc,desktopicon"',('/LOG="' + (Join-Path $root ($Phase + '.log')) + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$Phase failed with exit code $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $uninstallKey)) { throw 'The native uninstall registration is missing.' }
    if (-not (Test-Path -LiteralPath $shortcut) -or -not (Test-Path -LiteralPath $desktopShortcut)) { throw 'Installer shortcuts are missing.' }
    $association = (Get-ItemProperty 'HKCU:\Software\Classes\LensDocsStudio.Native.Document\shell\open\command').'(default)'
    if (-not $association.Contains($installDirectory)) { throw 'Open with does not target the isolated native installation.' }
    $checks.Add("${Phase}: installed self-contained application, shortcuts and Open with registration")
}
function Run-NativeUi([string]$Phase) {
    Push-Location $repo
    try {
        & dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- --ui (Join-Path $installDirectory 'LensDocsStudio.Native.exe') (Join-Path $root $Phase)
        if ($LASTEXITCODE -ne 0) { throw "Installed application UI validation failed during $Phase." }
    } finally { Pop-Location }
    $checks.Add("${Phase}: installed application saved and restored UTF-8 source")
}
function Uninstall-Native([string]$Phase) {
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    $process = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $root ($Phase + '.log')) + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Uninstall failed with exit code $($process.ExitCode)." }
    if ((Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $shortcut) -or (Test-Path -LiteralPath $desktopShortcut)) { throw 'Uninstall left native registration or shortcuts.' }
    if ((Test-Path -LiteralPath 'HKCU:\Software\Classes\LensDocsStudio.Native.Document')) { throw 'Uninstall left its native association registration.' }
    if ((Get-FileHash -LiteralPath $document).Hash -ne $documentHash) { throw 'Installer lifecycle changed a workspace document.' }
    $checks.Add("${Phase}: removed native installation and preserved workspace source")
}
try {
    Run-Installer $Installer 'install'
    Run-NativeUi 'first-launch'
    if ($UpgradeInstaller) { Run-Installer $UpgradeInstaller 'upgrade'; Run-NativeUi 'after-upgrade' }
    Uninstall-Native 'uninstall'
    Run-Installer $(if ($UpgradeInstaller) { $UpgradeInstaller } else { $Installer }) 'reinstall'
    Run-NativeUi 'after-reinstall'
    Uninstall-Native 'final-uninstall'
    $defaultAfter = (Get-ItemProperty 'HKCU:\Software\Classes\.md' -ErrorAction SilentlyContinue).'(default)'
    $legacyAfter = Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{E5D86651-4B27-4C59-B7D4-2EA98F2F8D26}_is1' -ErrorAction SilentlyContinue | ConvertTo-Json -Compress
    if ($defaultBefore -ne $defaultAfter -or $legacyBefore -ne $legacyAfter) { throw 'The legacy registration or Markdown default changed.' }
    $checks.Add('Existing shell registration and Markdown default preserved')
    @{ passed=$true; checks=$checks; evidence=$root; cleanMachineCertification=$false; upgradeTested=[bool]$UpgradeInstaller } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'installer-results.json')
    "Native installer lifecycle passed. Evidence: $root"
} catch {
    @{ passed=$false; checks=$checks; error=$_.Exception.Message; evidence=$root } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $root 'installer-results.json')
    throw
}
