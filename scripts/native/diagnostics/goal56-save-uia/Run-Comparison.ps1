param([Parameter(Mandatory)][string]$SourceName, [Parameter(Mandatory)][string]$Revision)
$ErrorActionPreference = 'Stop'
$evidence = Join-Path $PWD evidence
New-Item -ItemType Directory -Path $evidence | Out-Null
$protocol = Join-Path $PWD protocol
$original = Join-Path $PWD original
$diagnostic = Join-Path $PWD diagnostic
$outcomes = [ordered]@{}
function Write-Inventory([string]$Directory) {
    $Directory = (Resolve-Path -LiteralPath $Directory -ErrorAction Stop).Path
    $manifest = Join-Path $Directory 'file-hashes.json'
    # Finish enumeration and every read before opening the destination, including on repeat runs.
    [string[]]$paths = @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Force -ErrorAction Stop |
        Where-Object { -not [StringComparer]::OrdinalIgnoreCase.Equals($_.FullName, $manifest) } |
        ForEach-Object FullName)
    [Array]::Sort($paths, [StringComparer]::Ordinal)
    $rows = @(foreach ($path in $paths) {
        $inputFile = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        try {
            [ordered]@{ Algorithm = 'SHA256'; Hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($inputFile));
                File = [IO.Path]::GetRelativePath($Directory, $path); Length = $inputFile.Length }
        } finally { $inputFile.Dispose() }
    })
    ConvertTo-Json -InputObject $rows | Set-Content -LiteralPath $manifest -Encoding utf8NoBOM -ErrorAction Stop
}
function Run([string]$Name, [string]$Directory, [scriptblock]$Action) {
    Push-Location -LiteralPath $Directory
    try {
        & $Action *>&1 | Tee-Object -FilePath (Join-Path $evidence "$Name.log")
        $outcomes[$Name] = $LASTEXITCODE
    } catch { $_ | Out-String | Add-Content -LiteralPath (Join-Path $evidence "$Name.log"); $outcomes[$Name] = -1 }
    finally { Pop-Location; $outcomes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence outcomes.json) }
}
dotnet --info > (Join-Path $evidence dotnet-info.txt)
Get-ComputerInfo -Property WindowsProductName,WindowsVersion,OsBuildNumber,CsProcessors | ConvertTo-Json -Depth 6 > (Join-Path $evidence windows.json)
[ordered]@{ Source = $SourceName; Revision = (git -C $original rev-parse HEAD); Tree = (git -C $original rev-parse 'HEAD^{tree}');
    ExpectedRevision = $Revision; ImageOS = $env:ImageOS; ImageVersion = $env:ImageVersion;
    ProtocolRevision = (git -C $protocol rev-parse HEAD); ProtocolTree = (git -C $protocol rev-parse 'HEAD^{tree}') } | ConvertTo-Json > (Join-Path $evidence identity.json)
if ((git -C $original rev-parse HEAD) -ne $Revision -or (git -C $diagnostic rev-parse HEAD) -ne $Revision) { throw 'Source identity mismatch.' }
git -C $original status --porcelain=v1 > (Join-Path $evidence original-status.txt)
$filter = (Get-Content -LiteralPath (Join-Path $PSScriptRoot historical-tests.txt) | ForEach-Object { 'FullyQualifiedName=' + $_ }) -join '|'
$focused = 'FullyQualifiedName~Atomic_symbolic_rename_updates_both_arrow_forms_without_touching_properties_or_comments|FullyQualifiedName~Rename_updates_all_known_references_and_retains_namespace_notes_generic_and_annotation'
Run original-restore $original { dotnet restore src/native/LensDocsStudio.Native.slnx --locked-mode }
if ($outcomes['original-restore'] -eq 0) { Run original-build $original { dotnet build src/native/LensDocsStudio.Native.slnx -c Release --no-restore -warnaserror } }
if ($outcomes['original-build'] -eq 0 -and $outcomes.Contains('original-build')) {
    Run original-rendering $original { dotnet test src/native/LensDocsStudio.Core.Tests -c Release --no-build --no-restore --filter $filter --logger 'trx;LogFileName=historical.trx' --results-directory (Join-Path $evidence original-rendering) }
    Run original-high-contrast $original { & src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe --mermaid-creation-ui src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.exe (Join-Path $evidence original-ui) --high-contrast }
}
& (Join-Path $PSScriptRoot Add-SaveDiagnostics.ps1) -Source $diagnostic
git -C $diagnostic diff > (Join-Path $evidence diagnostic.patch)
git -C $diagnostic ls-files --others --exclude-standard | ForEach-Object { Get-FileHash -LiteralPath (Join-Path $diagnostic $_) } | ConvertTo-Json > (Join-Path $evidence diagnostic-additions.json)
Run diagnostic-restore $diagnostic { dotnet restore src/native/LensDocsStudio.Native.slnx --locked-mode }
if ($outcomes['diagnostic-restore'] -eq 0) { Run diagnostic-build $diagnostic { dotnet build src/native/LensDocsStudio.Native.slnx -c Release --no-restore -warnaserror } }
if ($outcomes['diagnostic-build'] -eq 0 -and $outcomes.Contains('diagnostic-build')) {
    $env:LENS_DIAGNOSTIC_DIRECTORY = Join-Path $evidence diagnostic-rendering
    $env:LENS_DIAGNOSTIC_SESSION = "$SourceName-rendering"
    Run diagnostic-rendering $diagnostic { dotnet test src/native/LensDocsStudio.Core.Tests -c Release --no-build --no-restore --filter $focused --logger 'trx;LogFileName=focused.trx' --results-directory $env:LENS_DIAGNOSTIC_DIRECTORY }
    $env:LENS_DIAGNOSTIC_DIRECTORY = Join-Path $evidence diagnostic-ui
    $env:LENS_DIAGNOSTIC_SESSION = "$SourceName-observed"
    Run diagnostic-high-contrast $diagnostic { & src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe --mermaid-creation-ui src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.exe $env:LENS_DIAGNOSTIC_DIRECTORY --high-contrast }
    $env:LENS_DIAGNOSTIC_DIRECTORY = Join-Path $evidence synthetic
    $env:LENS_DIAGNOSTIC_SESSION = "$SourceName-synthetic"
    Run synthetic $diagnostic { & src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe --goal56-save-race src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.exe $env:LENS_DIAGNOSTIC_DIRECTORY }
}
Write-Inventory $evidence
if ($outcomes.Count -ne 9 -or @($outcomes.Values | Where-Object { $_ -ne 0 }).Count -ne 0) { exit 1 }
