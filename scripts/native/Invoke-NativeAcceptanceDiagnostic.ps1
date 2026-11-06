param(
    [Parameter(Mandatory)][string]$Executable,
    [Parameter(Mandatory)][string[]]$ValidationArguments,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$StackTool,
    [Parameter(Mandatory)][string]$DumpTool
)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Native acceptance diagnostics require a disposable GitHub-hosted Windows runner.'
}
Add-Type @'
using System.IO;
using System.Text;
using System.Threading.Tasks;
public static class BoundedAcceptanceOutput {
    public static async Task<string> Read(StreamReader reader, int limit) {
        var result = new StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0) {
            var available = System.Math.Min(count, limit - result.Length);
            if (available > 0) result.Append(buffer, 0, available);
        }
        return result.ToString();
    }
}
'@
$executablePath = (Resolve-Path -LiteralPath $Executable).Path
if ([IO.Path]::GetFileName($executablePath) -ne 'LensDocsStudio.Native.Validation.exe') { throw 'Only the validation apphost may be supervised.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh diagnostic directory; retain earlier evidence.' }
New-Item -ItemType Directory -Path $output | Out-Null
$start = [Diagnostics.ProcessStartInfo]::new($executablePath)
$start.UseShellExecute = $false; $start.CreateNoWindow = $true
$start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
$start.Environment['LENS_NATIVE_ACCEPTANCE_TRACE'] = (Join-Path $output 'trace')
foreach ($argument in $ValidationArguments) { $start.ArgumentList.Add($argument) }
$child = [Diagnostics.Process]::Start($start)
$stdout = [BoundedAcceptanceOutput]::Read($child.StandardOutput,4194304)
$stderr = [BoundedAcceptanceOutput]::Read($child.StandardError,4194304)
$watch = [Diagnostics.Stopwatch]::StartNew(); $lastProgress = [Diagnostics.Stopwatch]::StartNew()
$lastPhase = ''; $blocked = $false; $result = 1; $supervisorError = $null
$captures = [Collections.Generic.List[object]]::new()
function Capture-Tool([string]$Tool, [string[]]$Arguments, [string]$Name) {
    $toolStart = [Diagnostics.ProcessStartInfo]::new((Resolve-Path -LiteralPath $Tool).Path)
    $toolStart.UseShellExecute = $false; $toolStart.CreateNoWindow = $true
    $toolStart.RedirectStandardOutput = $true; $toolStart.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $toolStart.ArgumentList.Add($argument) }
    $toolProcess = [Diagnostics.Process]::Start($toolStart)
    $text = [BoundedAcceptanceOutput]::Read($toolProcess.StandardOutput,32768)
    $errors = [BoundedAcceptanceOutput]::Read($toolProcess.StandardError,32768)
    $finished = $toolProcess.WaitForExit(15000)
    if (-not $finished) { $toolProcess.Kill($true); if (-not $toolProcess.WaitForExit(3000)) { throw 'Diagnostic helper did not terminate.' } }
    $log = $text.GetAwaiter().GetResult() + $errors.GetAwaiter().GetResult()
    [IO.File]::WriteAllText((Join-Path $output ($Name + '.txt')), $log.Substring(0, [Math]::Min($log.Length, 65536)))
    $captures.Add(@{ name=$Name; completed=$finished; exitCode=$toolProcess.ExitCode })
    $toolProcess.Dispose()
}
try {
    @{ status='RUNNING'; pid=$child.Id; arguments=$ValidationArguments; diagnosticOnly=$true } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'supervisor-start.json')
    while (-not $child.WaitForExit(200)) {
        $phasePath = Join-Path $output 'trace/phase.json'
        if (Test-Path -LiteralPath $phasePath) {
            $stream = [IO.FileStream]::new($phasePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
            $reader = [IO.StreamReader]::new($stream)
            try { $phase = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if ($phase -ne $lastPhase) { $lastPhase = $phase; $lastProgress.Restart() }
        }
        # This is a diagnostic-only fail-closed guard, never an enlarged product
        # acceptance deadline. The unchanged suites also run without this wrapper.
        if ($lastProgress.Elapsed.TotalSeconds -ge 60 -or $watch.Elapsed.TotalMinutes -ge 8) {
            $blocked = $true
            @{ status='FAIL'; reason='Diagnostic process stopped making bounded progress'; pid=$child.Id;
                elapsedMs=$watch.Elapsed.TotalMilliseconds; lastProgressMs=$lastProgress.Elapsed.TotalMilliseconds; lastPhase=$lastPhase } |
                ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'blocked.json')
            # Capture only this validation process and its own direct fixture child.
            $targets = @($child.Id)
            $targets += @(Get-CimInstance Win32_Process -Filter "ParentProcessId = $($child.Id)" |
                Where-Object Name -eq 'LensDocsStudio.Native.exe' | Select-Object -ExpandProperty ProcessId)
            foreach ($targetPid in $targets | Select-Object -First 3) {
                try {
                    Capture-Tool $StackTool @('report','--process-id',"$targetPid") "stacks-$targetPid"
                    Capture-Tool $DumpTool @('collect','--process-id',"$targetPid",'--type','Mini','--output',(Join-Path $output "threads-$targetPid.dmp")) "dump-$targetPid"
                } catch { $captures.Add(@{ pid=$targetPid; error=$_.Exception.GetType().Name }) }
            }
            if (-not $child.HasExited) { $child.Kill($true); if (-not $child.WaitForExit(3000)) { throw 'Owned validation process did not terminate.' } }
            break
        }
    }
    $result = if ($blocked) { 1 } else { $child.ExitCode }
} catch {
    $supervisorError = $_.Exception.Message
    $result = 1
} finally {
    if (-not $child.HasExited) { $child.Kill($true); $null = $child.WaitForExit(3000) }
    foreach ($entry in @(@('stdout',$stdout),@('stderr',$stderr))) {
        $text = if ($entry[1].Wait(3000)) { $entry[1].GetAwaiter().GetResult() } else { 'Output drain did not complete within its diagnostic bound.' }
        [IO.File]::WriteAllText((Join-Path $output ($entry[0] + '.txt')), $text.Substring(0,[Math]::Min($text.Length,4194304)))
    }
    @{ status=$(if ($result -ne 0) {'FAIL'} else {'PASS'}); diagnosticOnly=$true; supervisorError=$supervisorError;
        pid=$child.Id; exitCode=$result; elapsedMs=$watch.Elapsed.TotalMilliseconds; blocked=$blocked; captures=$captures } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'completion.json')
    $child.Dispose()
}
exit $result
