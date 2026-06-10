param(
    [switch]$NoBuild,
    [int]$TimeoutSeconds = 60,
    [switch]$KeepSmokeRoot
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$solutionPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows.sln'
$projectPath = Join-Path $repoRoot 'src\windows\LensDocsStudio.Windows\LensDocsStudio.Windows.csproj'
$smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("lens-docs-studio-native-smoke-{0}" -f ([guid]::NewGuid().ToString('N')))
$resultPath = Join-Path $smokeRoot 'smoke-result.json'
$expectedSingleFile = "# Windows Smoke Single File`n`nSaved by the automated native bridge smoke.`n"
$expectedSaveAs = "# Windows Smoke Save As`n`nWritten by the automated native bridge smoke.`n"
$expectedWorkspaceFile = "# Windows Smoke Workspace`n`nSaved by the automated native bridge smoke.`n"
$expectedCreatedFile = "# Windows Smoke Created File`n`nCreated by the automated native bridge smoke.`n"

function Write-SmokeLine {
    param([string]$Message)
    Write-Host "[windows-native-smoke] $Message"
}

function Assert-Smoke {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-WindowsShellExecutable {
    $projectDirectory = Split-Path -Parent $projectPath
    $candidates = Get-ChildItem -LiteralPath (Join-Path $projectDirectory 'bin') -Recurse -Filter 'LensDocsStudio.Windows.exe' -ErrorAction SilentlyContinue |
        Sort-Object @{ Expression = { if ($_.FullName -match '\\x64\\') { 0 } else { 1 } }; Ascending = $true }, @{ Expression = { $_.LastWriteTimeUtc }; Descending = $true }

    if (-not $candidates -or $candidates.Count -eq 0) {
        throw 'Windows shell executable was not found. Run without -NoBuild first.'
    }

    return $candidates[0].FullName
}

function Stop-SmokeProcess {
    param([System.Diagnostics.Process]$Process)

    if ($Process -and -not $Process.HasExited) {
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
    }
}

New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $smokeRoot 'workspace\docs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $smokeRoot 'workspace\diagrams') -Force | Out-Null

Set-Content -LiteralPath (Join-Path $smokeRoot 'single-file.md') -Value "# Smoke single file`n`nInitial fixture content." -Encoding utf8
Set-Content -LiteralPath (Join-Path $smokeRoot 'workspace\README.md') -Value "# Smoke workspace`n`nInitial workspace fixture." -Encoding utf8
Set-Content -LiteralPath (Join-Path $smokeRoot 'workspace\docs\overview.md') -Value "# Overview`n`nWorkspace overview fixture." -Encoding utf8
Set-Content -LiteralPath (Join-Path $smokeRoot 'workspace\diagrams\sample.mmd') -Value "flowchart TD`n  A-->B" -Encoding utf8

$process = $null
try {
    if (-not $NoBuild) {
        Write-SmokeLine 'Building Windows shell...'
        dotnet build $solutionPath
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
    }

    $exePath = Get-WindowsShellExecutable
    Write-SmokeLine "Launching $exePath"
    $process = Start-Process -FilePath $exePath -ArgumentList @(
        '--smoke-native-bridge',
        '--smoke-root',
        $smokeRoot,
        '--smoke-timeout-seconds',
        [string]$TimeoutSeconds
    ) -PassThru

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Path -LiteralPath $resultPath) {
            break
        }

        if ($process.HasExited -and -not (Test-Path -LiteralPath $resultPath)) {
            throw "Windows shell exited with code $($process.ExitCode) before writing smoke-result.json."
        }

        Start-Sleep -Milliseconds 250
    }

    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "Timed out after $TimeoutSeconds seconds waiting for smoke-result.json."
    }

    Wait-Process -Id $process.Id -Timeout 5 -ErrorAction SilentlyContinue
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json

    Assert-Smoke ($result.success -eq $true) 'Smoke result JSON did not report success.'
    $failedSteps = @($result.steps | Where-Object { $_.passed -ne $true })
    Assert-Smoke ($failedSteps.Count -eq 0) ("Smoke result reported failed steps: {0}" -f (($failedSteps | ForEach-Object { $_.name }) -join ', '))

    Assert-Smoke ((Get-Content -LiteralPath (Join-Path $smokeRoot 'single-file.md') -Raw) -eq $expectedSingleFile) 'Single fixture file was not saved with the expected content.'
    Assert-Smoke ((Test-Path -LiteralPath (Join-Path $smokeRoot 'single-file-copy.md'))) 'Save-as fixture file was not created.'
    Assert-Smoke ((Get-Content -LiteralPath (Join-Path $smokeRoot 'single-file-copy.md') -Raw) -eq $expectedSaveAs) 'Save-as fixture file content was not expected.'
    Assert-Smoke ((Get-Content -LiteralPath (Join-Path $smokeRoot 'workspace\README.md') -Raw) -eq $expectedWorkspaceFile) 'Workspace README was not saved with the expected content.'
    Assert-Smoke ((Test-Path -LiteralPath (Join-Path $smokeRoot 'workspace\notes\smoke-created.md'))) 'Workspace create fixture file was not written.'
    Assert-Smoke ((Get-Content -LiteralPath (Join-Path $smokeRoot 'workspace\notes\smoke-created.md') -Raw) -eq $expectedCreatedFile) 'Workspace create fixture file content was not expected.'

    $coveredSteps = @($result.steps | ForEach-Object { $_.name })
    foreach ($required in @(
        'Windows shell started',
        'WebView2 app loaded',
        'WebView2 loaded packaged static assets',
        'WebView2 did not require a local HTTP server',
        'Bridge ping returned LensDocsStudio.Windows',
        'Capabilities include diagnostics.ping',
        'Capabilities include file.open',
        'Capabilities include file.save',
        'Capabilities include file.saveAs',
        'Capabilities include workspace.openFolder',
        'Capabilities include workspace.saveFile',
        'Single fixture file opened',
        'Single fixture file saved',
        'Save-as wrote a new file',
        'Fixture workspace opened',
        'Workspace file saved',
        'Workspace file created, if capability exists',
        'Browser app did not report bridge protocol error'
    )) {
        Assert-Smoke ($coveredSteps -contains $required) "Smoke result is missing assertion: $required"
    }

    Write-SmokeLine 'PASS: Windows native bridge smoke completed successfully.'
    Write-SmokeLine "Smoke root: $smokeRoot"
    exit 0
}
catch {
    Write-SmokeLine "FAIL: $($_.Exception.Message)"
    if ($resultPath -and (Test-Path -LiteralPath $resultPath)) {
        Write-SmokeLine "Result file: $resultPath"
        Get-Content -LiteralPath $resultPath -Raw | Write-Host
    }
    exit 1
}
finally {
    Stop-SmokeProcess -Process $process
    if (-not $KeepSmokeRoot -and (Test-Path -LiteralPath $smokeRoot)) {
        Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
