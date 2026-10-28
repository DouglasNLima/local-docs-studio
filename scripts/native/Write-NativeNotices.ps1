[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadDirectory, [string]$AssetsPath)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $AssetsPath) { $AssetsPath = Join-Path $repo 'src/native/LensDocsStudio.Native/obj/project.assets.json' }
$assets = Get-Content -LiteralPath $AssetsPath -Raw | ConvertFrom-Json -AsHashtable
$packages = @{} + $assets.libraries
# Runtime packs are download dependencies, not ordinary libraries in project.assets.json.
# Read the published runtime versions rather than the SDK's locally installed patch.
$runtime = Get-Content -LiteralPath (Join-Path $PayloadDirectory 'LensDocsStudio.Native.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    if ($framework.name -notin 'Microsoft.NETCore.App','Microsoft.WindowsDesktop.App' -or $framework.version -notmatch '^\d+\.\d+\.\d+$') {
        throw 'Review the published runtime identity before generating redistribution notices.'
    }
    $id = $framework.name + '.Runtime.win-x64'
    $packages[$id + '/' + $framework.version] = @{ type='package'; path=($id.ToLowerInvariant() + '/' + $framework.version) }
}
$licences = Join-Path $PayloadDirectory 'licences'; New-Item -ItemType Directory -Path $licences -Force | Out-Null
Copy-Item -Path (Join-Path $repo 'docs/native/licences/*.txt') -Destination $licences
$notices = [Collections.Generic.List[string]]::new()
$notices.Add('# Third-party notices')
$notices.Add('')
$notices.Add('This native application includes the following NuGet components. Standard licence texts are in licences/. Package-provided notices and licences are copied alongside the component attribution. The self-contained .NET runtime also includes its own third-party notices.')
$notices.Add('The bounded ER adapter includes adapted Mermaider source. Attribution and its MIT licence are in licences/Mermaider-ER-adapter.md.')
Copy-Item -LiteralPath (Join-Path $repo 'src/native/LensDocsStudio.Core/Rendering/PinnedMermaider/NOTICE.md') -Destination (Join-Path $licences 'Mermaider-ER-adapter.md')
foreach ($entry in ($packages.GetEnumerator() | Sort-Object Key)) {
    if ($entry.Value.type -ne 'package') { continue }
    $packageRoot = $assets.packageFolders.Keys | ForEach-Object { Join-Path $_ $entry.Value.path } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $packageRoot) { throw "The restored package for $($entry.Key) is missing." }
    $spec = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nuspec' | Select-Object -First 1
    [xml]$xml = Get-Content -LiteralPath $spec.FullName -Raw
    $metadata = $xml.package.metadata
    $expression = $metadata.license.InnerText
    $notices.Add(''); $notices.Add("## $($metadata.id) $($metadata.version)"); $notices.Add('')
    $notices.Add("Authors: $($metadata.authors). Copyright: $($metadata.copyright)")
    $notices.Add("Licence: $expression. Project: $($metadata.projectUrl)")
    if ($metadata.license.type -eq 'expression') {
        foreach ($componentLicence in ($expression -split '\s+AND\s+')) {
            if (-not (Test-Path -LiteralPath (Join-Path $licences "$componentLicence.txt"))) {
                throw "Include the licence text for $componentLicence before packaging $($metadata.id)."
            }
        }
    }
    $packageNotices = @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object { $_.Name -match '^(licen[cs]e|notice|third[-_]?party[-_]?notices|font-licen[cs]es)(\.|$)' })
    if ($metadata.license.type -eq 'file') {
        $declared = [IO.Path]::GetFullPath((Join-Path $packageRoot $expression))
        $boundary = [IO.Path]::GetFullPath($packageRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        if (-not $declared.StartsWith($boundary, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $declared -PathType Leaf)) {
            throw "The declared licence file for $($metadata.id) is missing or outside its package."
        }
        $packageNotices += Get-Item -LiteralPath $declared
    }
    if (-not $expression) { throw "Review the missing licence metadata for $($metadata.id) before packaging." }
    $packageNotices = @($packageNotices | Sort-Object FullName -Unique)
    foreach ($notice in $packageNotices) {
        $target = Join-Path $licences ($metadata.id + '/' + [IO.Path]::GetRelativePath($packageRoot, $notice.FullName))
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $notice.FullName -Destination $target
    }
}
$notices | Set-Content -LiteralPath (Join-Path $PayloadDirectory 'THIRD-PARTY-NOTICES.md') -Encoding utf8NoBOM
