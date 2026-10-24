[CmdletBinding()]
param(
    [Parameter(Mandatory)][int]$NativeProcessId,
    [Parameter(Mandatory)][long]$WindowHandle,
    [Parameter(Mandatory)][string]$SandboxRoot,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceRevision,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$ExpectedSourceTree,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedExecutableSha256,
    [Parameter(Mandatory)][string]$OutputPath,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeDevelopmentHostSafety.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$root = Assert-NativeHostPath $SandboxRoot (Join-Path $repo 'artifacts/native/development-host')
$destination = Assert-NativeHostPath $OutputPath $root
if (Test-Path -LiteralPath $destination) { throw 'Display evidence exists; choose a new output.' }
$marker = Get-Content -LiteralPath (Join-Path $root 'host-sandbox.json') -Raw | ConvertFrom-Json
if ($marker.sourceRevision -cne $ExpectedSourceRevision -or $marker.sourceTree -cne $ExpectedSourceTree -or
    $marker.executableSha256 -cne $ExpectedExecutableSha256 -or $marker.cleanMachineCertified -ne $false -or
    $marker.qualification -cne 'DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION') { throw 'Independent package pins or host qualification differ.' }
$exe = Join-Path $root 'payload/LensDocsStudio.Native.exe'
$app = Assert-NativeHostWindow $NativeProcessId $WindowHandle $exe $ExpectedExecutableSha256 $ExpectedSourceRevision
$metadata = Get-Content -LiteralPath (Join-Path $root 'payload/version.json') -Raw | ConvertFrom-Json
Assert-NativeHostIdentity $metadata $ExpectedSourceRevision $ExpectedSourceTree
$observation = Get-NativeHostObservation
if (-not $observation.activeConsoleSession -or -not $observation.inputDesktopAccessible -or $observation.elevated) { throw 'An active normal console observation is required.' }
if (-not ('LensDevelopmentDisplayProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LensDevelopmentDisplayProbe {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct MonitorInfo { public uint Size; public Rect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetThreadDpiAwarenessContext();
    [DllImport("user32.dll")] public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int kind, out uint x, out uint y);
}
'@
}
$window = [IntPtr]::new($WindowHandle)
$monitor = [LensDevelopmentDisplayProbe]::MonitorFromWindow($window, 0)
$dpi = [LensDevelopmentDisplayProbe]::GetDpiForWindow($window)
$rect = [LensDevelopmentDisplayProbe+Rect]::new()
$info = [LensDevelopmentDisplayProbe+MonitorInfo]::new(); $info.Size=[Runtime.InteropServices.Marshal]::SizeOf($info)
[uint32]$x=0; [uint32]$y=0
$monitorResult = [LensDevelopmentDisplayProbe]::GetDpiForMonitor($monitor,0,[ref]$x,[ref]$y)
if (-not $dpi -or $monitor -eq [IntPtr]::Zero -or -not [LensDevelopmentDisplayProbe]::GetWindowRect($window,[ref]$rect) -or
    -not [LensDevelopmentDisplayProbe]::GetMonitorInfo($monitor,[ref]$info) -or $monitorResult -ne 0) { throw 'Incomplete live HWND/monitor DPI observation.' }
$receipt = [ordered]@{ schemaVersion=1; status='PASS'; scope='DEVELOPMENT_HOST_OBSERVATION'; recordedAtUtc=[DateTime]::UtcNow.ToString('o');
    sourceRevision=$ExpectedSourceRevision; sourceTree=$ExpectedSourceTree; executableSha256=$ExpectedExecutableSha256;
    processId=$NativeProcessId; processStartUtc=$app.StartTime.ToUniversalTime().ToString('o'); windowHandle=$WindowHandle;
    monitorHandle=$monitor.ToInt64(); os=$observation.os;
    dpi=@{ window=$dpi; effectiveScalePercent=($dpi/96.0*100); monitorX=$x; monitorY=$y;
        windowAwareness=[LensDevelopmentDisplayProbe]::GetAwarenessFromDpiAwarenessContext([LensDevelopmentDisplayProbe]::GetWindowDpiAwarenessContext($window));
        observerAwareness=[LensDevelopmentDisplayProbe]::GetAwarenessFromDpiAwarenessContext([LensDevelopmentDisplayProbe]::GetThreadDpiAwarenessContext()) };
    windowRect=$rect; monitorRect=$info.Monitor; logicalSize=@{ width=($rect.Right-$rect.Left)*96.0/$dpi; height=($rect.Bottom-$rect.Top)*96.0/$dpi };
    systemSettingsChanged=$false; productLaunched=$false; cleanMachineCertified=$false; interactiveScalingCertified=$false;
    limitations=@('GetDpiForMonitor/rectangles can be virtualised by observer awareness; HWND DPI is reported separately.', 'No scale change, multi-monitor transition, audible reader or alternate-scale acceptance is inferred.') }
if (-not $DryRun) { Write-NativeHostReceipt $destination $receipt }
$receipt | ConvertTo-Json -Depth 10
