[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+-preview\.\d+$')][string]$ExpectedVersion,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceRevision,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedInstallerSha256,
    [ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedZipSha256,
    [ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceTree
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeIconVerification.ps1')
. (Join-Path $PSScriptRoot 'NativeMermaidVerification.ps1')
$sourceIcon = Join-Path $PSScriptRoot '../../src/native/LensDocsStudio.Native/Resources/Brand/application.ico'
if (Test-Path -LiteralPath $OutputPath) { throw 'Verification output already exists; preserve it and choose a new path.' }
$PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$metadataText = [IO.File]::ReadAllText((Join-Path $PackageDirectory 'version.json'))
$metadata = $metadataText | ConvertFrom-Json
if ($metadata.schemaVersion -ne 1 -or $metadata.product -ne 'LensDocsStudio.Native' -or $metadata.version -ne $ExpectedVersion -or
    $metadata.sourceRevision -ne $ExpectedSourceRevision -or $metadata.sourceDirty -ne $false -or $metadata.selfContained -ne $true -or
    $metadata.internalPreview -ne $true -or $metadata.channel -ne 'Preview' -or $metadata.architecture -ne 'x64' -or
    $metadata.minimumWindowsVersion -ne '10.0.17763' -or $metadata.signingRequested -ne $false -or $metadata.signingProvider -ne 'Unsigned' -or
    $metadata.sourceKind -notin 'branch-source','pull-request-merge-checkout','local-source') { throw 'Preview version, source, target or unsigned identity does not match the expected package.' }
# Historical immutable packages predate this field; current acceptance supplies an independent tree pin.
if (($metadata.sourceTree -and $metadata.sourceTree -cnotmatch '^[0-9a-f]{40}$') -or ($ExpectedSourceTree -and $metadata.sourceTree -cne $ExpectedSourceTree)) {
    throw 'Preview source tree does not match the expected committed tree.'
}
$prefix = "LensDocsStudio.Native-$ExpectedVersion-win-x64"
$installer = Join-Path $PackageDirectory "$prefix-Setup.exe"
$portable = Join-Path $PackageDirectory "$prefix.zip"
$installerHash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
$zipHash = (Get-FileHash -LiteralPath $portable -Algorithm SHA256).Hash.ToLowerInvariant()
if (($ExpectedInstallerSha256 -and $installerHash -ne $ExpectedInstallerSha256) -or ($ExpectedZipSha256 -and $zipHash -ne $ExpectedZipSha256)) { throw 'Independent SHA-256 pin does not match the package bytes.' }
$checksumLines = @(Get-Content -LiteralPath (Join-Path $PackageDirectory 'SHA256SUMS.txt'))
if ($checksumLines.Count -ne 2 -or $checksumLines -notcontains "$installerHash  $prefix-Setup.exe" -or $checksumLines -notcontains "$zipHash  $prefix.zip") { throw 'SHA256SUMS does not identify both exact package files.' }
if ((Get-AuthenticodeSignature -LiteralPath $installer).Status -ne 'NotSigned') { throw 'This verifier expects the declared unsigned internal installer.' }
$installerVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($installer)
# Inno reserves fixed-width version-resource strings with trailing space padding.
if ($installerVersion.ProductName.TrimEnd() -ne 'Lens Docs Studio Native Preview' -or $installerVersion.ProductVersion.TrimEnd() -ne $ExpectedVersion) { throw 'Installer binary version or product identity is inconsistent.' }
$envelope = Get-Content -LiteralPath (Join-Path $PackageDirectory 'native-update.json') -Raw | ConvertFrom-Json
if ($envelope.schemaVersion -ne 1 -or $envelope.keyId -ne '' -or $envelope.signature -ne '') { throw 'Unsigned preview update envelope unexpectedly contains a signing identity.' }
$update = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.payload)) | ConvertFrom-Json
if ($update.productId -ne $metadata.product -or $update.version -ne $ExpectedVersion -or $update.architecture -ne 'x64' -or
    $update.channel -ne 'Preview' -or $update.assetName -ne "$prefix-Setup.exe" -or $update.size -ne (Get-Item -LiteralPath $installer).Length -or
    $update.sha256 -ne $installerHash -or $update.releaseTag -ne "native-v$ExpectedVersion") { throw 'Update metadata does not describe the same installer.' }
$required = @('LensDocsStudio.Native.exe','LensDocsStudio.Native.dll','LensDocsStudio.Core.dll','LensDocsStudio.Native.deps.json',
    'LensDocsStudio.Native.runtimeconfig.json','coreclr.dll','hostfxr.dll','hostpolicy.dll','PresentationFramework.dll','PresentationCore.dll',
    'WindowsBase.dll','wpfgfx_cor3.dll','version.json','PREVIEW-GUIDE.md','PREVIEW-RELEASE-NOTES.md','PRIVACY.md','THIRD-PARTY-NOTICES.md')
$files = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
$archive = [IO.Compression.ZipFile]::OpenRead($portable)
try {
    if ($archive.Entries.Count -gt 2000 -or ($archive.Entries | Measure-Object Length -Sum).Sum -gt 1GB) { throw 'Preview ZIP exceeds the package verification budget.' }
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName.EndsWith('/')) { continue }
        $name = $entry.FullName.Replace('\','/')
        if ($name.StartsWith('/') -or $name.Contains(':') -or $name.Split('/') -contains '..' -or $files.ContainsKey($name) -or
            $name -match '(?i)WebView2|ContinuousViewer|LayoutExperiment|(^|/)(spikes|tests|acceptance|obj|bin|node_modules)/|chrome|node\.exe|\.(pfx|pem|key|pdb|cs|csproj|slnx?|ps1|html)$' -or
            ($name.EndsWith('.md') -and -not $name.StartsWith('licences/', [StringComparison]::OrdinalIgnoreCase) -and $name -notin 'PREVIEW-GUIDE.md','PREVIEW-RELEASE-NOTES.md','PRIVACY.md','THIRD-PARTY-NOTICES.md','LICENSING.md')) { throw "Unexpected or unsafe preview payload entry: $name" }
        $stream = $entry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
        $files.Add($name, [ordered]@{ path=$name; bytes=$entry.Length; sha256=$hash })
    }
    foreach ($name in $required) { if (-not $files.ContainsKey($name) -or $files[$name].bytes -eq 0) { throw "Required native runtime or guidance is missing: $name" } }
    $firstPartyLicence = & (Join-Path $PSScriptRoot 'Test-NativeLicence.ps1') -PortablePath $portable -ExpectedSourceRevision $ExpectedSourceRevision -ExpectedVersion $ExpectedVersion -InstallerSha256 $installerHash
    function Read-ZipText([string]$Name) {
        $entry = $archive.Entries | Where-Object { $_.FullName.Replace('\','/') -eq $Name }
        if ($entry.Length -gt 1MB) { throw 'Package metadata exceeds its verification budget.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    if ((Read-ZipText 'version.json') -ne $metadataText) { throw 'Portable and installer-side version metadata differ.' }
    $inspection = Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath))) ('binary-inspection-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($inspection) | Out-Null
    $binaryVersions = @{}
    foreach ($name in 'LensDocsStudio.Native.dll','LensDocsStudio.Core.dll','LensDocsStudio.Native.exe') {
        $entry = $archive.Entries | Where-Object { $_.FullName.Replace('\','/') -eq $name }
        if ($entry.Length -gt 32MB) { throw 'Application identity binary exceeds the inspection budget.' }
        $input = $entry.Open(); $path = Join-Path $inspection $name; $destination = [IO.File]::Open($path, [IO.FileMode]::CreateNew)
        try { $input.CopyTo($destination) } finally { $input.Dispose(); $destination.Dispose() }
        $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion
        if ($version -ne "$ExpectedVersion+$ExpectedSourceRevision") { throw 'Application binary version/source differs from package metadata.' }
        $binaryVersions[$name] = $version
    }
    if ((Get-AuthenticodeSignature -LiteralPath (Join-Path $inspection 'LensDocsStudio.Native.exe')).Status -ne 'NotSigned') { throw 'Apphost signature differs from the declared unsigned package.' }
    $mermaidEditor = Test-NativeMermaidResources -NativeAssemblyPath (Join-Path $inspection 'LensDocsStudio.Native.dll') -CoreAssemblyPath (Join-Path $inspection 'LensDocsStudio.Core.dll')
    $stream = [IO.File]::OpenRead((Join-Path $inspection 'LensDocsStudio.Core.dll'))
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $reader = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe, [System.Reflection.Metadata.MetadataReaderOptions]::Default, $null)
            [byte[]]$resourceData = $pe.GetSectionData($pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetContent()
            $catalogueText = $null
            foreach ($handle in $reader.ManifestResources) {
                $entry = $reader.GetManifestResource($handle)
                if (-not $entry.Implementation.IsNil -or -not $reader.GetString($entry.Name).EndsWith('.built-in-catalogue.json', [StringComparison]::Ordinal)) { continue }
                $offset = [int]$entry.Offset
                if ($offset -lt 0 -or $offset -gt $resourceData.Length - 4) { throw 'Invalid native catalogue resource offset.' }
                $length = [BitConverter]::ToInt32($resourceData, $offset)
                if ($length -le 0 -or $length -gt 4MB -or [long]$offset + 4 + $length -gt $resourceData.Length) { throw 'Invalid native catalogue resource length.' }
                $catalogueText = [Text.Encoding]::UTF8.GetString($resourceData, $offset + 4, $length)
            }
            if (-not $catalogueText) { throw 'The compiled native catalogue is missing.' }
            $catalogue = $catalogueText | ConvertFrom-Json
            foreach ($family in 'packet','treeview','treemap','venn') {
                $template = @($catalogue.entries | Where-Object { $_.id -ceq "mermaid/$family" -and $_.format -ceq 'mermaid' -and $_.kind -ceq 'template' })
                if ($template.Count -ne 1) { throw "The compiled native $family catalogue entry is missing or duplicated." }
                $expectedFixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot "../../tests/fixtures/native-mermaid/expansion-vi/$family.mmd")).Replace("`r`n", "`n")
                if ($template[0].contentPattern -cne $expectedFixture) { throw "The compiled native $family catalogue source differs from the exact-source fixture." }
            }
        } finally { $pe.Dispose() }
    } finally { $stream.Dispose() }
    $dependencies = Read-ZipText 'LensDocsStudio.Native.deps.json' | ConvertFrom-Json -AsHashtable
    if (-not $dependencies.libraries.ContainsKey('Mermaider/0.15.1') -or -not $dependencies.libraries.ContainsKey('Sugiyama/0.15.1') -or @($dependencies.libraries.Keys | Where-Object { $_ -like 'Mermaider/*' }).Count -ne 1) { throw 'The native Venn renderer manifest differs from pinned Mermaider/Sugiyama 0.15.1.' }
    $applicationIcon = Test-NativeIconResources -BinaryPath (Join-Path $inspection 'LensDocsStudio.Native.exe') -SourceIcon $sourceIcon
    $installerIcon = Test-NativeIconResources -BinaryPath $installer -SourceIcon $sourceIcon
    $runtime = Read-ZipText 'LensDocsStudio.Native.runtimeconfig.json' | ConvertFrom-Json
    if ($runtime.runtimeOptions.tfm -ne 'net10.0' -or $runtime.runtimeOptions.framework -or $runtime.runtimeOptions.frameworks -or
        @($runtime.runtimeOptions.includedFrameworks).Count -ne 2 -or
        @($runtime.runtimeOptions.includedFrameworks | Where-Object { $_.name -ceq 'Microsoft.NETCore.App' -and $_.version -ceq '10.0.13' }).Count -ne 1 -or
        @($runtime.runtimeOptions.includedFrameworks | Where-Object { $_.name -ceq 'Microsoft.WindowsDesktop.App' -and $_.version -ceq '10.0.13' }).Count -ne 1) { throw 'Portable package must declare exactly the included Core/WPF 10.0.13 runtimes.' }
} finally { $archive.Dispose() }
$report = [ordered]@{ status='PASS'; version=$ExpectedVersion; sourceRevision=$ExpectedSourceRevision; sourceTree=$metadata.sourceTree; sourceKind=$metadata.sourceKind;
    ciRunId=$metadata.ciRunId; installerSha256=$installerHash; portableSha256=$zipHash; signature='NotSigned';
    trustedUpdates='BLOCKED: no production signing identity'; cleanMachineCertification='BLOCKED: separate clean image required';
    executionPerformed=$false; firstPartyLicence=$firstPartyLicence; binaryVersions=$binaryVersions; applicationIcon=$applicationIcon; installerIcon=$installerIcon; mermaidEditor=$mermaidEditor; payload=@($files.Values | Sort-Object path) }
$parent = Split-Path -Parent ([IO.Path]::GetFullPath($OutputPath)); [IO.Directory]::CreateDirectory($parent) | Out-Null
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
"Native preview package verification PASS: $ExpectedVersion ($ExpectedSourceRevision)"
