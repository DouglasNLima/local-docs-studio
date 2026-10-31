param([Parameter(Mandatory)][ValidateSet('baseline','candidate')][string]$SourceName, [Parameter(Mandatory)][string]$Revision)
$ErrorActionPreference = 'Stop'
$protocol = Join-Path $PWD protocol
$source = Join-Path $PWD source
$diagnostic = Join-Path $PWD diagnostic
$inventory = Join-Path $PWD inventory-protocol
$evidence = Join-Path $PWD evidence
New-Item -ItemType Directory -Path $evidence | Out-Null
$outcomes = [ordered]@{}
$client = 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
$app = 'src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.exe'
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
$identity = [ordered]@{ Source = $SourceName; Revision = (git -C $source rev-parse HEAD); Tree = (git -C $source rev-parse 'HEAD^{tree}');
    HarnessRevision = (git -C $protocol rev-parse HEAD); HarnessTree = (git -C $protocol rev-parse 'HEAD^{tree}');
    DiagnosticProtocol = (git -C $inventory rev-parse HEAD); DiagnosticProtocolTree = (git -C $inventory rev-parse 'HEAD^{tree}');
    ImageOS = $env:ImageOS; ImageVersion = $env:ImageVersion; Utc = [DateTimeOffset]::UtcNow }
$identity | ConvertTo-Json > (Join-Path $evidence identity.json)
if ($identity.Revision -ne $Revision -or (git -C $diagnostic rev-parse HEAD) -ne $Revision) { throw 'Exact product source identity mismatch.' }
foreach ($copy in @($source, $diagnostic)) {
    if (git -C $copy status --porcelain=v1) { throw 'Comparison source was not clean before the equivalent harness overlay.' }
    foreach ($file in @('MermaidCreationUiChecks.cs','NativeEditorSaveReadiness.cs','MermaidCreationSessionChecks.cs')) {
        Copy-Item -LiteralPath (Join-Path $protocol "src/native/LensDocsStudio.Native.Validation/$file") -Destination (Join-Path $copy "src/native/LensDocsStudio.Native.Validation/$file")
    }
    if (@(git -C $copy diff --name-only | Where-Object { $_ -notin @('src/native/LensDocsStudio.Native.Validation/MermaidCreationUiChecks.cs','src/native/LensDocsStudio.Native.Validation/MermaidCreationSessionChecks.cs') }).Count) { throw 'Unexpected product change in harness overlay.' }
}
Get-FileHash -LiteralPath (Join-Path $source src/native/LensDocsStudio.Native.Validation/MermaidCreationUiChecks.cs),(Join-Path $source src/native/LensDocsStudio.Native.Validation/NativeEditorSaveReadiness.cs),(Join-Path $source src/native/LensDocsStudio.Native.Validation/MermaidCreationSessionChecks.cs) | ConvertTo-Json > (Join-Path $evidence harness-files.json)
git -C $source diff > (Join-Path $evidence harness.patch)
Run restore $source { dotnet restore src/native/LensDocsStudio.Native.slnx --locked-mode }
if ($outcomes.restore -eq 0) { Run build $source { dotnet build src/native/LensDocsStudio.Native.slnx -c Release --no-restore -warnaserror } }
if ($outcomes.Contains('build') -and $outcomes.build -eq 0) {
    Run sessions $source { & $client --mermaid-creation-sessions (Join-Path $evidence sessions) }
    Run high-contrast $source { & $client --mermaid-creation-ui $app (Join-Path $evidence high-contrast) --high-contrast }
    if ($SourceName -eq 'candidate') {
        Run light $source { & $client --mermaid-creation-ui $app (Join-Path $evidence light) --normal }
        Run dark $source { & $client --mermaid-creation-ui $app (Join-Path $evidence dark) --dark }
    }
}
& (Join-Path $PSScriptRoot Add-ControlDiagnostics.ps1) -Source $diagnostic -DiagnosticProtocol $inventory
git -C $diagnostic diff > (Join-Path $evidence diagnostic.patch)
Get-FileHash -LiteralPath (Join-Path $diagnostic src/native/LensDocsStudio.Core/SaveTimeline.cs),(Join-Path $diagnostic src/native/LensDocsStudio.Native/NativeSaveProbe.cs),(Join-Path $diagnostic src/native/LensDocsStudio.Native.Validation/ReadinessControls.cs) | ConvertTo-Json > (Join-Path $evidence control-files.json)
Run control-restore $diagnostic { dotnet restore src/native/LensDocsStudio.Native.slnx --locked-mode }
if ($outcomes['control-restore'] -eq 0) { Run control-build $diagnostic { dotnet build src/native/LensDocsStudio.Native.slnx -c Release --no-restore -warnaserror } }
if ($outcomes.Contains('control-build') -and $outcomes['control-build'] -eq 0) {
    foreach ($mode in @('released','never')) {
        $env:LENS_DIAGNOSTIC_DIRECTORY = Join-Path $evidence $mode
        $env:LENS_DIAGNOSTIC_SESSION = "$SourceName-$mode"
        Run "control-$mode" $diagnostic { & $client --goal56-save-readiness $app $env:LENS_DIAGNOSTIC_DIRECTORY $mode }
    }
}
# Use the separately hosted-verified writer; load only its AST function.
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $inventory scripts/native/diagnostics/goal56-save-uia/Run-Comparison.ps1), [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'Inventory writer did not parse.' }
$function = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Write-Inventory' }, $true))
if ($function.Count -ne 1) { throw 'Inventory writer missing.' }
Invoke-Expression $function[0].Extent.Text
Write-Inventory $evidence
$expected = if ($SourceName -eq 'baseline') { 8 } else { 10 }
if ($outcomes.Count -ne $expected -or @($outcomes.Values | Where-Object { $_ -ne 0 }).Count) { exit 1 }
