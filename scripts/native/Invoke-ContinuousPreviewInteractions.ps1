[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('--normal','--high-contrast')][string]$Appearance,
    [string]$ValidationExecutable = 'src/native/LensDocsStudio.Native.Validation/bin/Release/net10.0-windows/LensDocsStudio.Native.Validation.exe',
    [string]$OutputDirectory = 'artifacts/continuous-interactions'
)
$ErrorActionPreference = 'Stop'
$output = [IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($output) | Out-Null
$validation = (Resolve-Path -LiteralPath $ValidationExecutable).Path
$arguments = @('--continuous-interactions', ('"' + $output + '"'), $Appearance)
$process = Start-Process -FilePath $validation -ArgumentList $arguments -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'validation-stdout.txt') -RedirectStandardError (Join-Path $output 'validation-stderr.txt')
if (-not $process.WaitForExit(180000)) {
    # This independent process bound cannot be starved by WPF's dispatcher or
    # an in-process diagnostic suspension. Existing 15-second checks are intact.
    [IO.File]::WriteAllText((Join-Path $output 'external-process-bound.txt'), 'FAIL: validation process exceeded the independent 180-second hard bound. No correctness or performance acceptance is inferred.')
    $stackTool = $env:LENS_CONTINUOUS_STACK_TOOL
    if ($stackTool -and (Test-Path -LiteralPath $stackTool)) {
        $stack = Start-Process -FilePath $stackTool -ArgumentList @('report','--process-id',$process.Id) -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $output 'external-managed-stacks.txt') -RedirectStandardError (Join-Path $output 'external-stack-stderr.txt')
        if (-not $stack.WaitForExit(15000)) { Stop-Process -Id $stack.Id -Force -ErrorAction SilentlyContinue }
    }
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    throw 'Continuous native interaction validation blocked; bounded diagnostics are retained.'
}
$process.Refresh()
exit $process.ExitCode
