param(
    [string]$ExecutablePath,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$packageRoot = Join-Path $repoRoot 'artifacts\windows'

$extensionMappings = @(
    @{ Extension = '.md'; ProgId = 'LensDocsStudio.Markdown'; Description = 'Lens Docs Studio Markdown document' },
    @{ Extension = '.markdown'; ProgId = 'LensDocsStudio.Markdown'; Description = 'Lens Docs Studio Markdown document' },
    @{ Extension = '.mmd'; ProgId = 'LensDocsStudio.Mermaid'; Description = 'Lens Docs Studio Mermaid document' },
    @{ Extension = '.mermaid'; ProgId = 'LensDocsStudio.Mermaid'; Description = 'Lens Docs Studio Mermaid document' },
    @{ Extension = '.txt'; ProgId = 'LensDocsStudio.Text'; Description = 'Lens Docs Studio text document' }
)

function Write-AssociationLine {
    param([string]$Message)
    Write-Host "[windows-file-associations] $Message"
}

function Get-LatestPackagedExecutable {
    if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
        return $null
    }

    $candidates = Get-ChildItem -LiteralPath $packageRoot -Recurse -Filter 'LensDocsStudio.Windows.exe' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\StaticApp\\' } |
        Sort-Object LastWriteTimeUtc -Descending

    if (-not $candidates -or $candidates.Count -eq 0) {
        return $null
    }

    return $candidates[0].FullName
}

function Set-RegistryDefaultValue {
    param(
        [string]$Path,
        [string]$Value
    )

    if ($DryRun) {
        Write-AssociationLine "DRY RUN set default '$Path' = '$Value'"
        return
    }

    New-Item -Path $Path -Force | Out-Null
    Set-Item -Path $Path -Value $Value
}

function Set-RegistryNamedValue {
    param(
        [string]$Path,
        [string]$Name,
        [object]$Value,
        [string]$PropertyType = 'String'
    )

    if ($DryRun) {
        Write-AssociationLine "DRY RUN set '$Path' value '$Name' = '$Value'"
        return
    }

    New-Item -Path $Path -Force | Out-Null
    New-ItemProperty -Path $Path -Name $Name -Value $Value -PropertyType $PropertyType -Force | Out-Null
}

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Get-LatestPackagedExecutable
}

if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    if ($DryRun) {
        $ExecutablePath = Join-Path $packageRoot 'LensDocsStudio.Windows-0.1.0-dev\LensDocsStudio.Windows.exe'
        Write-AssociationLine "No packaged executable was found. Dry run will use planned path: $ExecutablePath"
    } else {
        throw 'ExecutablePath is required when no packaged LensDocsStudio.Windows.exe exists under artifacts/windows.'
    }
}

$resolvedExecutablePath = if ($DryRun -and -not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    [System.IO.Path]::GetFullPath($ExecutablePath)
} else {
    (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).Path
}

if ($resolvedExecutablePath -notlike '*.exe') {
    throw "ExecutablePath must point to a Windows .exe file: $resolvedExecutablePath"
}

$command = '"{0}" "%1"' -f $resolvedExecutablePath
Write-AssociationLine "Registering per-user associations under HKCU for $resolvedExecutablePath"
if ($DryRun) {
    Write-AssociationLine 'Dry run only. No registry keys will be written.'
}

$progIds = $extensionMappings | Group-Object ProgId
foreach ($group in $progIds) {
    $mapping = $group.Group[0]
    $progIdPath = "HKCU:\Software\Classes\$($mapping.ProgId)"
    Set-RegistryDefaultValue -Path $progIdPath -Value $mapping.Description
    Set-RegistryDefaultValue -Path (Join-Path $progIdPath 'DefaultIcon') -Value ('"{0}",0' -f $resolvedExecutablePath)
    Set-RegistryDefaultValue -Path (Join-Path $progIdPath 'shell\open\command') -Value $command
    Write-AssociationLine "ProgId $($mapping.ProgId) -> $command"
}

foreach ($mapping in $extensionMappings) {
    $extensionPath = "HKCU:\Software\Classes\$($mapping.Extension)"
    Set-RegistryDefaultValue -Path $extensionPath -Value $mapping.ProgId
    Set-RegistryNamedValue -Path (Join-Path $extensionPath 'OpenWithProgids') -Name $mapping.ProgId -Value ([byte[]]@()) -PropertyType Binary
    Write-AssociationLine "Extension $($mapping.Extension) -> $($mapping.ProgId)"
}

Write-AssociationLine 'Registration complete. Windows may still ask the user to confirm the default app in Settings or Open with.'
