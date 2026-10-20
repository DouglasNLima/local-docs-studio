function Add-NativeAuthoringStateSnapshot {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Directory, [Parameter(Mandatory)][System.Collections.IDictionary]$Snapshot)
    $files = @(Get-ChildItem -LiteralPath $Directory -Recurse -File)
    if ($files.Count -eq 0) { throw 'Installed authoring produced no files to preserve across the lifecycle.' }
    foreach ($file in $files) {
        $Snapshot[$file.FullName] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
    return $files.Count
}

function Assert-NativeAuthoringStatePreserved {
    [CmdletBinding()]
    param([Parameter(Mandatory)][System.Collections.IDictionary]$Snapshot)
    foreach ($path in $Snapshot.Keys) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $Snapshot[$path]) {
            throw "Previously authored source, state or evidence changed during the installed lifecycle: $path"
        }
    }
    return $Snapshot.Count
}
