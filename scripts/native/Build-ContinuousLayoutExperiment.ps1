param(
    [ValidateSet('IL','ReadyToRun')][string]$Compilation = 'ReadyToRun',
    [string]$OutputDirectory = 'artifacts/layout-experiment-bin'
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Set-Location $root
$destination = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
$artefactRoot = [IO.Path]::GetFullPath((Join-Path $root 'artifacts')) + [IO.Path]::DirectorySeparatorChar
if (-not $destination.StartsWith($artefactRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Choose an isolated output within this checkout''s artifacts directory.' }
if ((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count) { throw 'Choose a fresh diagnostic output directory; existing payloads are never overwritten.' }
& dotnet build src/native/LensDocsStudio.LayoutExperiment -c Release -p:RestoreLockedMode=true
if ($LASTEXITCODE) { throw 'Experiment IL build failed.' }
$il = (Resolve-Path src/native/LensDocsStudio.LayoutExperiment/bin/Release/net10.0-windows).Path
$excluded = @(Get-ChildItem -LiteralPath $il -Filter *.dll -Recurse | Where-Object { $_.Name -like 'Microsoft.*' -or $_.Name -like 'System.*' } | Select-Object -ExpandProperty Name -Unique)
$metadata = Join-Path (Split-Path -Parent $destination) ((Split-Path -Leaf $destination) + '-build-metadata')
New-Item -ItemType Directory -Force -Path (Join-Path $metadata 'locks') | Out-Null
if ($Compilation -eq 'ReadyToRun') {
    foreach ($name in @('LensDocsStudio.LayoutExperiment','LensDocsStudio.Native','LensDocsStudio.Core')) {
        Copy-Item -LiteralPath "src/native/$name/packages.lock.json" -Destination (Join-Path $metadata "locks/$name.packages.lock.json")
    }
    # Public SDK compilation of application assemblies. Runtime/framework files
    # are not bundled, and Microsoft/System package libraries remain unchanged.
    $excludeXml = [Security.SecurityElement]::Escape(($excluded -join ';'))
    $targets = '<Project><PropertyGroup><NuGetLockFilePath>$(MSBuildThisFileDirectory)locks/$(MSBuildProjectName).packages.lock.json</NuGetLockFilePath></PropertyGroup><ItemGroup><PublishReadyToRunExclude Include="' + $excludeXml + '" /></ItemGroup></Project>'
    $targetsPath = Join-Path $metadata 'restore.targets'; [IO.File]::WriteAllText($targetsPath,$targets)
    & dotnet publish src/native/LensDocsStudio.LayoutExperiment -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true -p:PublishReadyToRunShowWarnings=true -p:RestoreLockedMode=false "-p:CustomBeforeMicrosoftCommonTargets=$targetsPath" --output $destination
    if ($LASTEXITCODE) { throw 'Experiment ReadyToRun application compilation failed.' }
} else {
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -LiteralPath $il | Copy-Item -Destination $destination -Recurse
}
$preserved = foreach ($name in $excluded) {
    $from = Join-Path $il $name; $to = Join-Path $destination $name
    if ((Test-Path -LiteralPath $from) -and (Test-Path -LiteralPath $to)) {
        $before = (Get-FileHash -LiteralPath $from).Hash; $after = (Get-FileHash -LiteralPath $to).Hash
        if ($before -ne $after) { throw "A Microsoft/System library changed: $name" }
        @{ Name=$name; OriginalSha256=$before; PublishedSha256=$after; Unmodified=$true }
    }
}
foreach ($name in @('PresentationFramework.dll','PresentationCore.dll','WindowsBase.dll','System.Private.CoreLib.dll','coreclr.dll')) {
    if (Test-Path -LiteralPath (Join-Path $destination $name)) { throw "Unexpected bundled runtime/framework file: $name" }
}
$launcher = Join-Path $destination 'LensDocsStudio.Native.exe'
if (Test-Path -LiteralPath $launcher) { Remove-Item -LiteralPath $launcher }
$nativeHeaders = foreach ($file in Get-ChildItem -LiteralPath $destination -Filter LensDocsStudio.*.dll) {
    $stream = [IO.File]::OpenRead($file.FullName)
    $reader = [Reflection.PortableExecutable.PEReader]::new($stream)
    try { @{ Name=$file.Name; ManagedNativeHeaderBytes=$reader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size } }
    finally { $reader.Dispose(); $stream.Dispose() }
}
if ($Compilation -eq 'ReadyToRun' -and @($nativeHeaders | Where-Object ManagedNativeHeaderBytes -eq 0).Count) { throw 'An application assembly lacks its requested ReadyToRun header.' }
@{ Label='EXPERIMENT ONLY'; Source=(git rev-parse HEAD); SourceDirty=[bool](git status --porcelain); Compilation=$Compilation; Runtime='Framework-dependent official unmodified .NET 10/WPF';
   Sdk=(dotnet --version); SelfContained=$false; ApplicationNativeHeaders=@($nativeHeaders); PreservedMicrosoftSystemLibraries=@($preserved);
   DocumentPrewarm=$false; OrdinaryPackagingChanged=$false } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $destination 'compilation.json')
Write-Host "EXPERIMENT ONLY $Compilation executable: $(Join-Path $destination 'LensDocsStudio.LayoutExperiment.exe')"
