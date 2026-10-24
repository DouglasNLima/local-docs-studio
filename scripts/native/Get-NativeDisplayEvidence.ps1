[CmdletBinding(DefaultParameterSetName='Observe')]
param(
    [Parameter(Mandatory, ParameterSetName='Observe')][int]$NativeProcessId,
    [Parameter(Mandatory, ParameterSetName='Observe')][string]$VmIdentity,
    [Parameter(Mandatory, ParameterSetName='Observe')][string]$SnapshotIdentity,
    [Parameter(Mandatory, ParameterSetName='Observe')][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceRevision,
    [Parameter(Mandatory, ParameterSetName='Observe')][string]$OutputPath,
    [Parameter(Mandatory, ParameterSetName='Observe')][switch]$DisposableEnvironmentAuthorised,
    [Parameter(Mandatory, ParameterSetName='CompileOnly')][switch]$ValidateProbeOnly
)
$ErrorActionPreference = 'Stop'
# Read-only observation of an already running app. No launch, scale, theme, registry or awareness mutation.
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LensNativeDisplayProbe {
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AreDpiAwarenessContextsEqual(IntPtr left, IntPtr right);
    [DllImport("shcore.dll")] public static extern int GetProcessDpiAwareness(IntPtr process, out int awareness);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);
}
'@
if ($ValidateProbeOnly) {
    'DPI probe definitions compile; interactive scaling NOT RUN. No native product process inspected or launched.'
    return
}
if (-not $DisposableEnvironmentAuthorised -or -not $VmIdentity.Trim() -or -not $SnapshotIdentity.Trim()) {
    throw 'An authorised disposable environment and recorded VM/snapshot identity are required.'
}
if (Test-Path -LiteralPath $OutputPath) { throw 'Display evidence already exists; preserve it and choose a new output.' }
$app = Get-Process -Id $NativeProcessId
if ($app.ProcessName -cne 'LensDocsStudio.Native' -or $app.MainWindowHandle -eq [IntPtr]::Zero) {
    throw 'The specified process must be the actual already running native app with a top-level window.'
}
$binary = $app.MainModule.FileName
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo($binary).ProductVersion
if (-not $version.EndsWith('+' + $ExpectedSourceRevision, [StringComparison]::Ordinal)) { throw 'App source differs from the independent expected source pin.' }
$window = $app.MainWindowHandle; $monitor = [LensNativeDisplayProbe]::MonitorFromWindow($window, 2)
$windowDpi = [LensNativeDisplayProbe]::GetDpiForWindow($window)
if ($windowDpi -eq 0 -or $monitor -eq [IntPtr]::Zero) { throw 'No effective top-level window DPI/monitor could be observed.' }
[int]$awareness = -1; [uint32]$monitorX = 0; [uint32]$monitorY = 0
$awarenessResult = [LensNativeDisplayProbe]::GetProcessDpiAwareness($app.Handle, [ref]$awareness)
$monitorResult = [LensNativeDisplayProbe]::GetDpiForMonitor($monitor, 0, [ref]$monitorX, [ref]$monitorY)
if ($awarenessResult -ne 0 -or $monitorResult -ne 0) { throw "Read-only DPI query failed: awareness=$awarenessResult; monitor=$monitorResult" }
$windowContext = [LensNativeDisplayProbe]::GetWindowDpiAwarenessContext($window)
$probeContext = [LensNativeDisplayProbe]::GetThreadDpiAwarenessContext()
$windows = Get-CimInstance Win32_OperatingSystem
$registry = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
$report = [ordered]@{
    schemaVersion=1; status='OBSERVATION ONLY'; recordedAtUtc=[DateTime]::UtcNow.ToString('o')
    vmIdentity=$VmIdentity; snapshotIdentity=$SnapshotIdentity; operatorAuthorisedDisposableEnvironment=$true
    os=@{ caption=$windows.Caption; version=$windows.Version; build=$windows.BuildNumber; revision=$registry.UBR }
    app=@{ processId=$NativeProcessId; windowHandle=$window.ToInt64(); monitorHandle=$monitor.ToInt64(); productVersion=$version; sourceRevision=$ExpectedSourceRevision; binarySha256=(Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash.ToLowerInvariant() }
    dpi=@{ window=$windowDpi; monitorX=$monitorX; monitorY=$monitorY; processAwareness=$awareness; windowAwareness=[LensNativeDisplayProbe]::GetAwarenessFromDpiAwarenessContext($windowContext); perMonitorV2=[LensNativeDisplayProbe]::AreDpiAwarenessContextsEqual($windowContext, [IntPtr]::new(-4)); probeThreadAwareness=[LensNativeDisplayProbe]::GetAwarenessFromDpiAwarenessContext($probeContext) }
    limitations=@('No display scaling change was made or certified.', 'Monitor DPI may be virtualised by the recorded probe awareness; retain Windows Settings and window-DPI evidence.', 'Before/after Settings screenshots, actual pointer/control/export observations and multi-monitor protocol remain required.')
    productLaunched=$false; systemSettingsChanged=$false; interactiveScalingCertified=$false
}
$destination = [IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $destination -Encoding UTF8
'Read-only DPI observation saved. Interactive high-DPI certification remains unestablished by this probe alone.'
