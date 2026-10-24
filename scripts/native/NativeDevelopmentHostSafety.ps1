# Shared, fail-closed guards for personal-host observation and synthetic portable runs.
# This is a test boundary, not a security boundary against another process owned by the user.
Set-StrictMode -Version Latest

function Assert-NativeHostPath([string]$Path, [string]$Within = '') {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or $Path -notmatch '^[A-Za-z]:[\\/]' -or
        $Path.Substring(2).Contains(':') -or $Path -match '[*?]' -or $Path -match '[\\/]\.\.?([\\/]|$)') {
        throw 'An absolute local path without traversal, devices, streams or wildcards is required.'
    }
    $full = [IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($Path))
    if ($Within) {
        $parent = Assert-NativeHostPath $Within
        if (-not $full.StartsWith($parent + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Path must be strictly inside the synthetic sandbox.'
        }
    }
    $part = $full
    while ($part) {
        $existing = $null
        try { $existing = Get-Item -Force -LiteralPath $part -ErrorAction Stop }
        catch [Management.Automation.ItemNotFoundException] { }
        if ($null -ne $existing) {
            if ($existing.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Reparse points, junctions and symbolic links are forbidden in host evidence paths.'
            }
        }
        $part = [IO.Path]::GetDirectoryName($part)
    }
    return $full
}

function Get-NativeHostFiles([string]$Root) {
    $Root = Assert-NativeHostPath $Root
    if (-not (Test-Path -LiteralPath $Root)) { return }
    $pending = [Collections.Generic.Stack[string]]::new(); $pending.Push($Root)
    while ($pending.Count) {
        foreach ($entry in Get-ChildItem -Force -LiteralPath $pending.Pop()) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Reparse point found inside an observed tree; stop without following it.' }
            if ($entry.PSIsContainer) { $pending.Push($entry.FullName) } else { $entry }
        }
    }
}

function Get-NativeHostManifest([string]$Root) {
    @(Get-NativeHostFiles $Root | ForEach-Object {
        [ordered]@{ path=[IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\','/'); bytes=$_.Length;
            sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
    } | Sort-Object { $_.path })
}

function Get-NativeHostDigest($Value) {
    $json = ConvertTo-Json -InputObject (ConvertTo-NativeHostCanonical $Value) -Depth 30 -Compress
    [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json))).ToLowerInvariant()
}

function ConvertTo-NativeHostCanonical($Value) {
    if ($null -eq $Value) { return $null }
    # Pipeline commands may wrap strings with PSObject metadata. They remain
    # scalar registry names/values, rather than custom objects to enumerate.
    if ($Value -is [string]) { return [string]$Value }
    if ($Value -is [ValueType]) { return $Value }
    if ($Value -is [Collections.IDictionary] -or $Value -is [pscustomobject]) {
        $result = [ordered]@{}
        $keys = if ($Value -is [Collections.IDictionary]) { @($Value.Keys) } else { @($Value.PSObject.Properties.Name) }
        foreach ($key in $keys | Sort-Object -CaseSensitive) { $result[$key] = ConvertTo-NativeHostCanonical $Value.$key }
        return $result
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        return ,@(foreach ($item in $Value) { ConvertTo-NativeHostCanonical $item })
    }
    return $Value
}

function Write-NativeHostReceipt([string]$Path, $Value) {
    $Path = Assert-NativeHostPath $Path
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $Value -Depth 30))
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes); $stream.Flush($true) } finally { $stream.Dispose() }
}

function Assert-NativeHostIdentity($Metadata, [string]$Revision, [string]$Tree) {
    if ($Metadata.schemaVersion -ne 1 -or $Metadata.product -cne 'LensDocsStudio.Native' -or
        $Metadata.sourceRevision -cne $Revision -or $Metadata.sourceTree -cne $Tree -or
        $Metadata.sourceDirty -ne $false -or $Metadata.selfContained -ne $true -or
        $Metadata.internalPreview -ne $true -or $Metadata.signingRequested -ne $false -or
        $Metadata.signingProvider -cne 'Unsigned' -or $Metadata.architecture -cne 'x64') {
        throw 'Exact committed source/tree and unsigned self-contained native identity are required.'
    }
}

function Assert-NativeHostQualification($Observation) {
    if ($Observation.qualification -cne 'DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION' -or
        $Observation.cleanMachineCertified -ne $false -or $Observation.elevated -ne $false -or
        $Observation.activeConsoleSession -ne $true -or $Observation.inputDesktopAccessible -ne $true -or
        $Observation.interactive -ne $true -or $Observation.nativeProcesses.Count -ne 0) {
        throw 'A normal active interactive console token, accessible input desktop and no existing native process are required. Clean certification cannot be relabelled.'
    }
}

function Assert-NativeHostUnchanged($Before, $After) {
    foreach ($field in 'associations','registrations','shortcuts','defaultState') {
        if ((Get-NativeHostDigest $Before.$field) -cne (Get-NativeHostDigest $After.$field)) {
            throw "Observed host $field changed. Preserve evidence; do not attempt automatic restoration."
        }
    }
}

function Assert-NativeHostPayload($Expected, [string]$Directory) {
    $actual = @(Get-NativeHostManifest $Directory)
    if ($Expected.Count -ne 528 -or $actual.Count -ne $Expected.Count) { throw 'Expected exactly 528 verified payload files.' }
    foreach ($file in $Expected) {
        $match = @($actual | Where-Object { $_.path -ceq $file.path })
        if ($match.Count -ne 1 -or $match[0].sha256 -cne $file.sha256 -or $match[0].bytes -ne $file.bytes) {
            throw 'Extracted payload differs from independently verified portable bytes.'
        }
    }
}

function Get-NativeHostObservation {
    if (-not ('LensDevelopmentHostProbe' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LensDevelopmentHostProbe {
    [DllImport("kernel32.dll")] public static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("user32.dll")] public static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] public static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern bool GetUserObjectInformation(IntPtr handle, int index, IntPtr buffer, uint length, out uint needed);
}
'@
    }
    $desktop = [LensDevelopmentHostProbe]::OpenInputDesktop(0, $false, 1)
    $accessible = $false
    if ($desktop -ne [IntPtr]::Zero) {
        $buffer = [Runtime.InteropServices.Marshal]::AllocHGlobal(1024)
        try {
            [uint32]$needed = 0
            if ([LensDevelopmentHostProbe]::GetUserObjectInformation($desktop, 2, $buffer, 1024, [ref]$needed)) {
                $accessible = [Runtime.InteropServices.Marshal]::PtrToStringUni($buffer) -ceq 'Default'
            }
        } finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($buffer); [void][LensDevelopmentHostProbe]::CloseDesktop($desktop) }
    }
    $os = Get-CimInstance Win32_OperatingSystem
    $windows = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    $associations = @()
    foreach ($extension in '.md','.markdown','.mmd','.mermaid') {
        foreach ($key in "HKCU:\Software\Classes\$extension", "HKLM:\Software\Classes\$extension",
            "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\$extension") {
            $associations += Get-NativeHostRegistryTree $key
        }
    }
    foreach ($key in 'HKCU:\Software\Classes\LensDocsStudio.Native.Document','HKCU:\Software\Classes\LensDocsStudio.Document',
        'HKLM:\Software\Classes\LensDocsStudio.Native.Document','HKLM:\Software\Classes\LensDocsStudio.Document') {
        $associations += Get-NativeHostRegistryTree $key
    }
    $registrations = @()
    foreach ($parent in 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall','HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
        if (Test-Path -LiteralPath $parent) {
            foreach ($key in Get-ChildItem -LiteralPath $parent) {
                $entry = Get-ItemProperty -LiteralPath $key.PSPath
                if ($entry.PSObject.Properties['DisplayName'] -and $entry.DisplayName -match 'Lens.*Docs|Markdown.*Mermaid') {
                    $registrations += Get-NativeHostRegistryTree $key.PSPath
                }
            }
        }
    }
    $shortcuts = @()
    foreach ($folder in 'Programs','CommonPrograms','DesktopDirectory','CommonDesktopDirectory','Startup','CommonStartup') {
        $path = [Environment]::GetFolderPath($folder)
        if ($path -and (Test-Path -LiteralPath $path)) {
            # Enumerate shortcut names only. Never resolve or open an unrelated shortcut target.
            $shortcuts += @(Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*Lens*.lnk' -ErrorAction Stop | ForEach-Object {
                @{ location=$folder; name=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName).Hash }
            })
        }
    }
    $tools = @('dotnet','node','npm','npm.cmd','devenv','msbuild','code','python','git') | ForEach-Object {
        @{ name=$_; present=[bool](Get-Command $_ -ErrorAction SilentlyContinue | Select-Object -First 1) }
    }
    $state = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'LensDocsStudio.Native'
    $sdk = Join-Path $env:ProgramFiles 'dotnet/sdk'
    [ordered]@{
        schemaVersion=1; qualification='DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION'; cleanMachineCertified=$false
        os=@{ caption=$os.Caption; build=$os.BuildNumber; ubr=$windows.UBR; version=$os.Version; architecture=$os.OSArchitecture }
        elevated=$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        interactive=[Environment]::UserInteractive; sessionId=[Diagnostics.Process]::GetCurrentProcess().SessionId
        activeConsoleSession=([Diagnostics.Process]::GetCurrentProcess().SessionId -eq [LensDevelopmentHostProbe]::WTSGetActiveConsoleSessionId())
        inputDesktopAccessible=$accessible; tools=@($tools)
        sdkVersions=@(if (Test-Path -LiteralPath $sdk) { Get-ChildItem -LiteralPath $sdk -Directory | Select-Object -ExpandProperty Name })
        nativeProcesses=@(Get-Process -Name 'LensDocsStudio.Native','LensDocsStudio' -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,SessionId)
        associations=$associations; registrations=$registrations; shortcuts=$shortcuts
        defaultState=@{ exists=(Test-Path -LiteralPath $state); files=@(Get-NativeHostManifest $state) }
        locations=@{ privateState='%LOCALAPPDATA%/LensDocsStudio.Native'; defaultInstall='%LOCALAPPDATA%/Programs/Lens Docs Studio Native' }
        policy=@{ execution=@(Get-ExecutionPolicy -List | Select-Object Scope,ExecutionPolicy); appLockerPolicyPresent=(Test-Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\SrpV2'); sandboxExecutablePresent=[bool](Get-Command WindowsSandbox.exe -ErrorAction SilentlyContinue) }
        installer=@{ status='NOT RUN'; reason='UNSAFE SHARED REGISTRATION: fixed AppId, uninstall key and Start Menu group; no personal-host install permitted.' }
        systemSettingsChanged=$false
    }
}

function Get-NativeHostRegistryTree([string]$Root) {
    if (-not (Test-Path -LiteralPath $Root)) { return @{ key=$Root; exists=$false } }
    $keys = @((Get-Item -LiteralPath $Root)) + @(Get-ChildItem -LiteralPath $Root -Recurse)
    if ($keys.Count -gt 256) { throw 'Registry observation exceeds its bounded scope.' }
    foreach ($key in $keys) {
        $values = @(foreach ($name in $key.GetValueNames() | Sort-Object) {
            @{ name=$name; kind=$key.GetValueKind($name).ToString(); value=$key.GetValue($name, $null, 'DoNotExpandEnvironmentNames') }
        })
        @{ key=$key.Name; exists=$true; values=$values }
    }
}

function Assert-NativeHostWindow([int]$NativeProcessId, [long]$WindowHandle, [string]$Executable, [string]$Sha256, [string]$Revision) {
    if (-not ('LensHostWindowIdentity' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LensHostWindowIdentity {
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
}
'@
    }
    if ($WindowHandle -le 0 -or $NativeProcessId -le 0) { throw 'A real PID and HWND are required.' }
    $process = Get-Process -Id $NativeProcessId
    $expected = Assert-NativeHostPath $Executable
    [uint32]$owner = 0
    [void][LensHostWindowIdentity]::GetWindowThreadProcessId([IntPtr]::new($WindowHandle), [ref]$owner)
    if ($owner -ne $NativeProcessId -or $process.ProcessName -cne 'LensDocsStudio.Native' -or
        -not [LensHostWindowIdentity]::IsWindowVisible([IntPtr]::new($WindowHandle)) -or
        $process.SessionId -ne [Diagnostics.Process]::GetCurrentProcess().SessionId -or
        $process.MainModule.FileName -ine $expected -or
        (Get-FileHash -LiteralPath $expected).Hash -ine $Sha256 -or
        -not ([Diagnostics.FileVersionInfo]::GetVersionInfo($expected).ProductVersion.EndsWith('+' + $Revision, [StringComparison]::Ordinal))) {
        throw 'PID/HWND/session, executable bytes or exact source identity differs from the independent pins.'
    }
    return $process
}
