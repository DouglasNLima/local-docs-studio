[CmdletBinding()]
param([Parameter(Mandatory)][string]$PayloadDirectory)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$assets = Get-Content -LiteralPath (Join-Path $repo 'src/native/LensDocsStudio.Native/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
$licences = Join-Path $PayloadDirectory 'licences'; New-Item -ItemType Directory -Path $licences -Force | Out-Null
Copy-Item -Path (Join-Path $repo 'docs/native/licences/*.txt') -Destination $licences
$notices = [Collections.Generic.List[string]]::new()
$notices.Add('# Third-party notices')
$notices.Add('')
$notices.Add('This native application includes the following NuGet components. Standard licence texts are in licences/. Package-provided notices and licences are copied alongside the component attribution. The self-contained .NET runtime also includes its own third-party notices.')
foreach ($entry in ($assets.libraries.GetEnumerator() | Sort-Object Key)) {
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
    $packageNotices = Get-ChildItem -LiteralPath $packageRoot -Recurse -File | Where-Object { $_.Name -match '^(licen[cs]e|notice|thirdpartynotices|font-licen[cs]es)(\.|$)' }
    foreach ($notice in $packageNotices) {
        $target = Join-Path $licences ($metadata.id + '/' + [IO.Path]::GetRelativePath($packageRoot, $notice.FullName))
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $notice.FullName -Destination $target
    }
}
$notices | Set-Content -LiteralPath (Join-Path $PayloadDirectory 'THIRD-PARTY-NOTICES.md') -Encoding utf8NoBOM
