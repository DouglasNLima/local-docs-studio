[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$UpgradeInstaller,
    [Parameter(Mandatory)][string]$ExpectedSourceRevision,
    [ValidateSet("Class", "Sequence", "Flowchart", "Gantt", "Mindmap", "Pie", "Timeline", "Journey", "Quadrant", "Xy", "Radar", "Sankey", "Kanban", "Requirement", "GitGraph", "Architecture", "C4", "Block")][string]$Family = "Class",
    [switch]$NoFileAssociations
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw "Installed $Family authoring requires a disposable GitHub-hosted Windows runner."
}
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$Installer = (Resolve-Path -LiteralPath $Installer).Path
$UpgradeInstaller = (Resolve-Path -LiteralPath $UpgradeInstaller).Path
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{3AF043CA-F7C1-4D1F-A8BC-847B4C44E23F}_is1'
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Lens Docs Studio Native Preview/Lens Docs Studio Native Preview.lnk'
if ((Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $shortcut)) { throw 'An installed native preview already exists; preserve it.' }
$root = Join-Path $repo ('artifacts/native/' + $Family.ToLowerInvariant() + '-install-smoke/' + [guid]::NewGuid().ToString('N'))
$installDirectory = Join-Path $root 'installed'
New-Item -ItemType Directory -Path $root -Force | Out-Null
. (Join-Path $PSScriptRoot 'NativeAssociationExclusion.ps1')
. (Join-Path $PSScriptRoot 'NativeAuthoringStatePreservation.ps1')
$preserved = @{}
if ($NoFileAssociations) { Assert-NativeAssociationAbsence }
$checks = [Collections.Generic.List[string]]::new()
$versions = [Collections.Generic.List[object]]::new()
$completed = $false
foreach ($package in $Installer, $UpgradeInstaller) {
    $directory = [IO.Path]::GetDirectoryName($package)
    $identity = Get-Content -LiteralPath (Join-Path $directory 'version.json') -Raw | ConvertFrom-Json
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Test-NativePreviewPackage.ps1') -PackageDirectory $directory -ExpectedVersion $identity.version -ExpectedSourceRevision $ExpectedSourceRevision -OutputPath (Join-Path $root ($identity.version + '-verification.json'))
    if ($LASTEXITCODE -ne 0) { throw "Independent package verification failed before $Family installer execution." }
    if ($identity.sourceDirty -or $identity.signingProvider -ne 'Unsigned') { throw "$Family certification expects the clean exact-source unsigned internal preview." }
}
function Assert-Payload([string]$Package, [string]$Phase) {
    $archive = [IO.Compression.ZipFile]::OpenRead(($Package -replace '-Setup\.exe$', '.zip'))
    $expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    try {
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $name = $entry.FullName.Replace('\', '/')
            if ($name.StartsWith('/') -or $name.Contains(':') -or $name.Split('/') -contains '..' -or -not $expected.Add($name)) { throw 'Unsafe or duplicate portable payload path.' }
            $installed = Join-Path $installDirectory $name
            $stream = $entry.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) } finally { $stream.Dispose() }
            if (-not (Test-Path -LiteralPath $installed -PathType Leaf) -or (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash -ne $hash) { throw "${Phase}: installed payload differs: $name" }
        }
        foreach ($file in Get-ChildItem -LiteralPath $installDirectory -Recurse -File) {
            $name = [IO.Path]::GetRelativePath($installDirectory, $file.FullName).Replace('\', '/')
            if (-not $expected.Contains($name) -and $name -notin 'unins000.exe', 'unins000.dat') { throw "Unexpected installed $Family payload: $name" }
        }
    } finally { $archive.Dispose() }
    $checks.Add("${Phase}: all $($expected.Count) installed payload files match independently verified portable bytes")
}
function Remove-Installation([string]$Phase) {
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (-not (Test-Path -LiteralPath $uninstaller)) { return }
    $process = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or (Test-Path -LiteralPath $uninstallKey) -or (Test-Path -LiteralPath $shortcut)) { throw "${Phase}: isolated $Family uninstall failed." }
    $checks.Add("${Phase}: native installation and shortcuts removed")
    $preservedCount = Assert-NativeAuthoringStatePreserved -Snapshot $preserved
    $checks.Add("${Phase}: $preservedCount previous authored/source/state/evidence files retain exact SHA-256 after uninstall")
}
try {
    foreach ($phase in 'install', 'upgrade', 'reinstall') {
        [void](Assert-NativeAuthoringStatePreserved -Snapshot $preserved)
        if ($phase -eq 'reinstall') { Remove-Installation 'before-reinstall' }
        $package = if ($phase -eq 'install') { $Installer } else { $UpgradeInstaller }
        $expected = Get-Content -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($package)) 'version.json') -Raw | ConvertFrom-Json
        $taskExclusions = if ($NoFileAssociations) { @('/TASKS=""') } else { @() }
        $process = Start-Process -FilePath $package -ArgumentList (@($taskExclusions) + @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',('/DIR="' + $installDirectory + '"'),('/LOG="' + (Join-Path $root ($phase + '.log')) + '"'))) -WindowStyle Hidden -Wait -PassThru
        if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $uninstallKey)) { throw "${phase}: isolated $Family installation failed." }
        $identity = Get-Content -LiteralPath (Join-Path $installDirectory 'version.json') -Raw | ConvertFrom-Json
        $binary = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $installDirectory 'LensDocsStudio.Native.dll')).ProductVersion.Split('+')[0]
        $registered = (Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion
        if ($identity.version -ne $expected.version -or $identity.sourceRevision -ne $ExpectedSourceRevision -or $binary -ne $expected.version -or $registered -ne $expected.version) { throw "${phase}: installed $Family provenance differs from the exact PR head." }
        if ($NoFileAssociations) { Assert-NativeAssociationAbsence; $checks.Add("${phase}: optional association tasks explicitly disabled and registrations absent") }
        Assert-Payload $package $phase
        $preservedCount = Assert-NativeAuthoringStatePreserved -Snapshot $preserved
        $checks.Add("${phase}: $preservedCount previous authored/source/state/evidence files retain exact SHA-256 after installation")
        $versions.Add(@{ phase=$phase; version=$identity.version; source=$identity.sourceRevision; binary=$binary; registered=$registered })
        Push-Location $repo
        try {
            $mode = switch ($Family) { 'Sequence' { '--sequence-ui' } 'Flowchart' { '--mermaid-ui' } { $_ -in 'Gantt','Mindmap','Pie','Timeline','Journey','Quadrant','Xy','Radar','Sankey','Kanban','Requirement','GitGraph','Architecture','C4','Block' } { '--source-editor-ui' } default { '--class-ui' } }
            $arguments = @('run', '--project', 'src/native/LensDocsStudio.Native.Validation', '-c', 'Release', '--no-build', '--', $mode, (Join-Path $installDirectory 'LensDocsStudio.Native.exe'), (Join-Path $root $phase))
            if ($Family -in 'Gantt','Mindmap','Pie','Timeline','Journey','Quadrant','Xy','Radar','Sankey','Kanban','Requirement','GitGraph','Architecture','C4','Block') { $arguments += $Family }
            $arguments += '--normal'
            if ($Family -eq 'Flowchart') { $arguments += '--polish' }
            & dotnet @arguments
            if ($LASTEXITCODE -ne 0) { throw "${phase}: installed $Family native authoring failed." }
        } finally { Pop-Location }
        $checks.Add("${phase}: real installed $Family authoring, pointers, private/document Undo, Apply, Save, restart/reopen, Cancel")
        $snapshotCount = Add-NativeAuthoringStateSnapshot -Directory (Join-Path $root $phase) -Snapshot $preserved
        $checks.Add("${phase}: $snapshotCount authored/source/state/evidence files recorded for upgrade/reinstall/uninstall preservation")
    }
    Remove-Installation 'final-uninstall'
    if ($NoFileAssociations) { Assert-NativeAssociationAbsence }
    $completed = $true
} finally {
    try { Remove-Installation 'cleanup' } finally {
        @{ status=$(if ($completed) { 'PASS' } else { 'FAIL' }); sourceRevision=$ExpectedSourceRevision; versions=$versions; checks=$checks; preservedFiles=$preserved } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root ($Family.ToLowerInvariant() + '-installer-results.json'))
    }
}
