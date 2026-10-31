param([Parameter(Mandatory)][ValidateSet('baseline','candidate')][string]$SourceName,
      [Parameter(Mandatory)][string]$Revision)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Shown preview diagnostics require a disposable GitHub-hosted Windows runner.'
}
$protocol = Join-Path $PWD 'protocol'
$source = Join-Path $PWD 'source'
$inventory = Join-Path $PWD 'inventory-protocol'
$evidence = Join-Path $PWD 'evidence'
New-Item -ItemType Directory -Path $evidence | Out-Null
$outcomes = [ordered]@{}
function Run([string]$Name, [scriptblock]$Action) {
    Push-Location -LiteralPath $source
    try { & $Action *>&1 | Tee-Object -FilePath (Join-Path $evidence "$Name.log"); $outcomes[$Name] = $LASTEXITCODE }
    catch { $_ | Out-String | Add-Content -LiteralPath (Join-Path $evidence "$Name.log"); $outcomes[$Name] = -1 }
    finally { Pop-Location; $outcomes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $evidence 'outcomes.json') }
}
try {
    $identity = [ordered]@{ Source = $SourceName; Revision = (git -C $source rev-parse HEAD); Tree = (git -C $source rev-parse 'HEAD^{tree}');
        DiagnosticRevision = (git -C $protocol rev-parse HEAD); DiagnosticTree = (git -C $protocol rev-parse 'HEAD^{tree}');
        ImageOS = $env:ImageOS; ImageVersion = $env:ImageVersion; Utc = [DateTimeOffset]::UtcNow }
    $identity | ConvertTo-Json | Set-Content (Join-Path $evidence 'identity.json')
    if ($identity.Revision -ne $Revision -or (git -C $source status --porcelain=v1)) { throw 'Exact clean product source identity mismatch.' }
    # Select the fixed diagnostic SDK from the workspace parent, outside every product checkout.
    '{ "sdk": { "version": "10.0.403", "rollForward": "disable" } }' | Set-Content (Join-Path $PWD 'global.json')
    Copy-Item -LiteralPath (Join-Path $PWD 'global.json') -Destination (Join-Path $evidence 'sdk-selection.json')
    Push-Location -LiteralPath $source
    try {
        if ((dotnet --version) -ne '10.0.403') { throw 'The selected SDK is not the fixed 10.0.403 diagnostic SDK.' }
        dotnet --info > (Join-Path $evidence 'dotnet-info.txt')
    } finally { Pop-Location }
    Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber | ConvertTo-Json | Set-Content (Join-Path $evidence 'windows.json')
    Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors | ConvertTo-Json | Set-Content (Join-Path $evidence 'cpu.json')
    $files = Get-ChildItem -LiteralPath $PSScriptRoot -File
    Get-FileHash -LiteralPath $files.FullName -Algorithm SHA256 | Select-Object Path,Hash | ConvertTo-Json | Set-Content (Join-Path $evidence 'diagnostic-files.json')
    Get-FileHash (Join-Path $source 'src/native/LensDocsStudio.Native.Validation/NativePreviewLoadingChecks.cs') | ConvertTo-Json | Set-Content (Join-Path $evidence 'original-test-hash.json')
    & (Join-Path $PSScriptRoot 'Apply-PreviewOverlay.ps1') -SourceRoot $source -Evidence $evidence
    $changed = @(git -C $source diff --name-only)
    if ($changed.Count -ne 1 -or $changed[0] -ne 'src/native/LensDocsStudio.Native.Validation/NativePreviewLoadingChecks.cs') { throw 'Unexpected tracked change in validation overlay.' }
    $productDiff = git -C $source diff -- src/native/LensDocsStudio.Core src/native/LensDocsStudio.Native | Out-String
    if ($LASTEXITCODE -ne 0) { throw 'Product source comparison failed.' }
    [IO.File]::WriteAllText((Join-Path $evidence 'product-source.diff'), $productDiff)
    if ($productDiff.Length -ne 0) { throw 'Product source changed.' }
    Run build { dotnet build src/native/LensDocsStudio.Native.slnx -c Release -p:RestoreLockedMode=true -warnaserror }
    if ($outcomes.build -eq 0) {
        $client = Join-Path $source 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
        Get-FileHash $client,(Join-Path $source 'src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.dll') | ConvertTo-Json | Set-Content (Join-Path $evidence 'binary-hashes.json')
        Run ordinary { & $client --preview-loading-ui (Join-Path $evidence 'ordinary') $source }
        Run high-contrast { & $client --preview-loading-ui (Join-Path $evidence 'high-contrast') $source --high-contrast }
        $env:LENS_PREVIEW_LIFECYCLE_CONTROL = 'never'
        try { Run never { & $client --preview-loading-ui (Join-Path $evidence 'never') $source } }
        finally { Remove-Item Env:LENS_PREVIEW_LIFECYCLE_CONTROL }
        $negative = Get-Content (Join-Path $evidence 'never/lifecycle-timeout-before-cleanup.json') -Raw | ConvertFrom-Json
        $boundary = @($negative.Events | Where-Object Name -eq 'timeout-before-cleanup')[-1]
        $receipt = Get-Content (Join-Path $evidence 'never/preview-loading.json') -Raw | ConvertFrom-Json
        $exception = Get-Content (Join-Path $evidence 'never.log') -Raw
        $confirmed = $outcomes.never -eq 1 -and $receipt.Status -eq 'FAIL' -and $boundary.State.GateHeldByControl -eq $true -and
            $boundary.State.GateCapacity -eq 0 -and $boundary.State.Loading.Busy -eq $true -and
            $boundary.State.View.IsVisible -eq $true -and $boundary.State.CurrentAcceptedSource -eq $false -and
            $boundary.State.Ring.IsVisible -eq $true -and $boundary.State.Ring.Visibility -eq 'Visible' -and
            $boundary.State.IsIndeterminate -eq $true -and $boundary.State.RingStatus -eq 'Busy' -and
            $boundary.State.Host.Visibility -eq 'Hidden' -and
            $boundary.State.Loading.Context.Document -eq $boundary.State.ActiveDocument -and
            $exception.Contains('System.TimeoutException: Preview lifecycle check exceeded its bounded wait.')
        @{ ExpectedNegativeControl = $confirmed; UnderlyingAcceptance = $receipt.Status; ExitCode = $outcomes.never;
            Boundary = $boundary } | ConvertTo-Json -Depth 14 | Set-Content (Join-Path $evidence 'never-control.json')
        $outcomes['never-control'] = if ($confirmed) { 0 } else { 1 }
    }
}
catch { $_ | Out-String | Set-Content (Join-Path $evidence 'protocol-error.txt'); $outcomes['protocol-error'] = 1 }
finally {
    $outcomes | ConvertTo-Json | Set-Content (Join-Path $evidence 'outcomes.json')
    # Load only the independently hosted-verified inventory function, never its experiment.
    $tokens = $null; $parseErrors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $inventory 'scripts/native/diagnostics/goal56-save-uia/Run-Comparison.ps1'), [ref]$tokens, [ref]$parseErrors)
    if ($parseErrors.Count) { throw 'Inventory source did not parse.' }
    $function = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Write-Inventory' }, $true))
    if ($function.Count -ne 1) { throw 'Expected one verified inventory function.' }
    Invoke-Expression $function[0].Extent.Text
    Write-Inventory $evidence
}
if ($outcomes.Count -ne 5 -or $outcomes.build -ne 0 -or $outcomes.ordinary -ne 0 -or
    $outcomes['high-contrast'] -ne 0 -or $outcomes['never-control'] -ne 0) { exit 1 }
exit 0
