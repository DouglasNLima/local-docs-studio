param(
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$extensionMappings = @(
    @{ Extension = '.md'; ProgId = 'LensDocsStudio.Markdown' },
    @{ Extension = '.markdown'; ProgId = 'LensDocsStudio.Markdown' },
    @{ Extension = '.mmd'; ProgId = 'LensDocsStudio.Mermaid' },
    @{ Extension = '.mermaid'; ProgId = 'LensDocsStudio.Mermaid' },
    @{ Extension = '.txt'; ProgId = 'LensDocsStudio.Text' }
)

function Write-AssociationLine {
    param([string]$Message)
    Write-Host "[windows-file-associations] $Message"
}

function Get-RegistryDefaultValue {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    return (Get-Item -LiteralPath $Path).GetValue('')
}

function Remove-RegistryTree {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-AssociationLine "Already absent: $Path"
        return
    }

    if ($DryRun) {
        Write-AssociationLine "DRY RUN remove key tree '$Path'"
        return
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
    Write-AssociationLine "Removed key tree: $Path"
}

function Remove-RegistryNamedValue {
    param(
        [string]$Path,
        [string]$Name
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $property = Get-ItemProperty -LiteralPath $Path -Name $Name -ErrorAction SilentlyContinue
    if (-not $property) {
        return
    }

    if ($DryRun) {
        Write-AssociationLine "DRY RUN remove '$Path' value '$Name'"
        return
    }

    Remove-ItemProperty -LiteralPath $Path -Name $Name -Force
    Write-AssociationLine "Removed '$Name' from $Path"
}

function Clear-RegistryDefaultValueIfOwned {
    param(
        [string]$Path,
        [string]$ProgId
    )

    $current = Get-RegistryDefaultValue -Path $Path
    if ($current -ne $ProgId) {
        Write-AssociationLine "Leaving default for $Path unchanged."
        return
    }

    if ($DryRun) {
        Write-AssociationLine "DRY RUN clear default '$Path' because it is '$ProgId'"
        return
    }

    Set-Item -LiteralPath $Path -Value ''
    Write-AssociationLine "Cleared Lens Docs Studio default for $Path"
}

if ($DryRun) {
    Write-AssociationLine 'Dry run only. No registry keys will be removed.'
}

foreach ($progId in ($extensionMappings.ProgId | Select-Object -Unique)) {
    Remove-RegistryTree -Path "HKCU:\Software\Classes\$progId"
}

foreach ($mapping in $extensionMappings) {
    $extensionPath = "HKCU:\Software\Classes\$($mapping.Extension)"
    Clear-RegistryDefaultValueIfOwned -Path $extensionPath -ProgId $mapping.ProgId
    Remove-RegistryNamedValue -Path (Join-Path $extensionPath 'OpenWithProgids') -Name $mapping.ProgId
}

Write-AssociationLine 'Unregistration complete. Unrelated user defaults and Windows UserChoice keys were not removed.'
