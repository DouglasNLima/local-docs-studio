param([Parameter(Mandatory)][string]$EvidenceRoot)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = (git rev-parse --show-toplevel).Trim()
$overlay = (git rev-parse HEAD).Trim()
$overlayTree = (git rev-parse 'HEAD^{tree}').Trim()
$instrumentation = Join-Path $repo 'scripts/native/diagnostics/high-contrast-colour'
$evidence = [IO.Path]::GetFullPath($EvidenceRoot)
if (Test-Path -LiteralPath $evidence) { throw 'Preserve prior evidence; output already exists.' }
New-Item -ItemType Directory -Path $evidence | Out-Null
$attempts = [Collections.Generic.List[object]]::new()
dotnet --info | Out-File (Join-Path $evidence 'dotnet-info.txt')
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,OSArchitecture | ConvertTo-Json | Out-File (Join-Path $evidence 'os.json')
Get-CimInstance Win32_Processor | Select-Object Name,NumberOfCores,NumberOfLogicalProcessors | ConvertTo-Json | Out-File (Join-Path $evidence 'cpu.json')
Get-ChildItem Env: | Where-Object Name -in @('ImageOS','ImageVersion','RUNNER_OS','RUNNER_ENVIRONMENT','GITHUB_RUN_ID','GITHUB_RUN_ATTEMPT') | ConvertTo-Json | Out-File (Join-Path $evidence 'runner.json')
dotnet build (Join-Path $instrumentation 'Observer/Observer.csproj') -c Release *> (Join-Path $evidence 'observer-build.log')
if ($LASTEXITCODE -ne 0) { throw 'Observer build failed.' }
$env:RCA_OBSERVER = Join-Path $instrumentation 'Observer/bin/Release/net10.0-windows/Observer.dll'
$env:RCA_OVERLAY = $overlay
$env:RCA_OVERLAY_TREE = $overlayTree
try {
    foreach ($source in @('ab21919a42ec8b3d17d9ba2e39b4e7e41459e206','0e80946eaf15bfd5b6dcf02a3bd16b00d5306569')) {
        $label = if ($source.StartsWith('ab219')) { 'baseline' } else { 'candidate' }
        $worktree = Join-Path $evidence ($label + '-source')
        git worktree add --detach $worktree $source *> (Join-Path $evidence ($label + '-checkout.log'))
        if ($LASTEXITCODE -ne 0) { throw 'Source checkout failed.' }
        $env:RCA_SOURCE = (git -C $worktree rev-parse HEAD).Trim()
        $env:RCA_TREE = (git -C $worktree rev-parse 'HEAD^{tree}').Trim()
        if ($env:RCA_SOURCE -ne $source) { throw 'Source identity differs.' }
        @{ source=$source; tree=$env:RCA_TREE; overlay=$overlay; overlayTree=$overlayTree } | ConvertTo-Json | Out-File (Join-Path $evidence ($label + '-identity.json'))
        $solution = Join-Path $worktree 'src/native/LensDocsStudio.Native.slnx'
        dotnet build $solution -c Release -p:RestoreLockedMode=true *> (Join-Path $evidence ($label + '-build.log'))
        if ($LASTEXITCODE -ne 0) { throw 'Exact-source build failed.' }
        $validation = Join-Path $worktree 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe'
        $product = Join-Path $worktree 'src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.exe'
        $productDll = [IO.Path]::ChangeExtension($product, '.dll')
        $before = (Get-FileHash -LiteralPath $productDll -Algorithm SHA256).Hash
        # Passive control: exact original command and code, no diagnostic overlay loaded.
        & $validation --ux-screenshots $product (Join-Path $evidence ($label + '-original-order')) $source --high-contrast *> (Join-Path $evidence ($label + '-original-order.log'))
        $attempts.Add(@{ label=$label; control='original-order'; source=$source; exitCode=$LASTEXITCODE })
        $validationSource = Join-Path $worktree 'src/native/LensDocsStudio.Native.Validation'
        Copy-Item -LiteralPath (Join-Path $instrumentation 'NativeHighContrastColourRca.cs') -Destination $validationSource
        $program = Join-Path $validationSource 'Program.cs'
        $text = [IO.File]::ReadAllText($program)
        $needle = "private static int Main(string[] args)"
        $position = $text.IndexOf($needle, [StringComparison]::Ordinal)
        if ($position -lt 0 -or $text.IndexOf($needle, $position + 1, [StringComparison]::Ordinal) -ge 0) { throw 'Diagnostic entry anchor is not unique.' }
        $brace = $text.IndexOf('{', $position)
        $text = $text.Insert($brace + 1, "`n        if (args.FirstOrDefault() == `"--hc-colour-rca`") return NativeHighContrastColourRca.Run(args[1], args[2], args[3]);`n")
        [IO.File]::WriteAllText($program, $text)
        git -C $worktree diff -- src/native/LensDocsStudio.Native.Validation | Out-File (Join-Path $evidence ($label + '-overlay.diff'))
        dotnet build (Join-Path $validationSource 'LensDocsStudio.Native.Validation.csproj') -c Release -p:RestoreLockedMode=true *> (Join-Path $evidence ($label + '-overlay-build.log'))
        if ($LASTEXITCODE -ne 0) { throw 'Diagnostic overlay build failed.' }
        if ((Get-FileHash -LiteralPath $productDll -Algorithm SHA256).Hash -ne $before) { throw 'Instrumentation changed the product assembly.' }
        & $validation --hc-colour-rca $product (Join-Path $evidence ($label + '-transition')) transition *> (Join-Path $evidence ($label + '-transition.log'))
        $attempts.Add(@{ label=$label; control='transition'; source=$source; exitCode=$LASTEXITCODE; productSha256=$before })
        # Stop after a diagnostic fault; preserve first-source attempts without spending the second half blindly.
        if ($LASTEXITCODE -ne 0) { throw 'Diagnostic control failed; inspect retained evidence before any new execution.' }
    }
}
finally {
    $attempts | ConvertTo-Json -Depth 8 | Out-File (Join-Path $evidence 'attempts.json')
    Get-ChildItem -LiteralPath $evidence -File -Recurse | Where-Object FullName -notlike '*-source\*' | ForEach-Object {
        @{ path=[IO.Path]::GetRelativePath($evidence,$_.FullName); sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; bytes=$_.Length }
    } | ConvertTo-Json | Out-File (Join-Path $evidence 'evidence-hashes.json')
}
