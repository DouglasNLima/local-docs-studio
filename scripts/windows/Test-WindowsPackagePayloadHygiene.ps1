$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'WindowsPackagePayloadHygiene.ps1')

function Assert-HygieneTest {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function New-TestFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    $directory = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    'test' | Set-Content -LiteralPath $Path -Encoding ascii
}

$root = Join-Path ([System.IO.Path]::GetTempPath()) "LensDocsStudio.PayloadHygiene.$([System.Guid]::NewGuid().ToString('N'))"
try {
    New-Item -ItemType Directory -Path $root -Force | Out-Null

    $webViewPayload = Join-Path $root 'LensDocsStudio.Windows.exe.WebView2\EBWebView\Default\Preferences'
    New-TestFile -Path $webViewPayload
    $violations = @(Get-WindowsPackagePayloadViolations -RootPath $root)
    Assert-HygieneTest ($violations.Count -gt 0) 'Expected WebView2 payload violation was not reported.'
    Assert-HygieneTest ($violations[0].relativePath -like 'LensDocsStudio.Windows.exe.WebView2*') 'WebView2 violation did not include a focused relative path.'
    try {
        Assert-WindowsPackagePayloadClean -RootPath $root -Context 'fake WebView2 package'
        throw 'Expected WebView2 package assertion to fail.'
    } catch {
        Assert-HygieneTest ($_.Exception.Message -match 'LensDocsStudio.Windows.exe.WebView2') 'WebView2 assertion failure did not identify the offending relative path.'
    }

    Remove-Item -LiteralPath (Join-Path $root 'LensDocsStudio.Windows.exe.WebView2') -Recurse -Force

    $cachePayload = Join-Path $root 'SomePayload\EBWebView\Default\Service Worker\CacheStorage\cache-entry'
    New-TestFile -Path $cachePayload
    $violations = @(Get-WindowsPackagePayloadViolations -RootPath $root)
    Assert-HygieneTest (@($violations | Where-Object { $_.relativePath -like 'SomePayload/EBWebView*' }).Count -gt 0) 'Expected nested EBWebView/cache violation was not reported.'
    try {
        Assert-WindowsPackagePayloadClean -RootPath $root -Context 'fake nested cache package'
        throw 'Expected nested cache package assertion to fail.'
    } catch {
        Assert-HygieneTest ($_.Exception.Message -match 'SomePayload/EBWebView') 'Nested cache assertion failure did not identify the offending relative path.'
    }

    $zipPath = Join-Path ([System.IO.Path]::GetTempPath()) "LensDocsStudio.PayloadHygiene.$([System.Guid]::NewGuid().ToString('N')).zip"
    try {
        Compress-Archive -LiteralPath (Get-ChildItem -LiteralPath $root -Force).FullName -DestinationPath $zipPath -Force
        try {
            Assert-WindowsPackageZipPayloadClean -ZipPath $zipPath -Context 'fake package ZIP'
            throw 'Expected ZIP package assertion to fail.'
        } catch {
            Assert-HygieneTest ($_.Exception.Message -match 'SomePayload/EBWebView') 'ZIP assertion failure did not identify the offending relative path.'
        }
    } finally {
        if (Test-Path -LiteralPath $zipPath) {
            Remove-Item -LiteralPath $zipPath -Force
        }
    }

    $message = Format-WindowsPackagePayloadViolationMessage -Violations $violations -Context 'test payload'
    Assert-HygieneTest ($message -match 'SomePayload/EBWebView') 'Violation message did not include the offending relative path.'
    Assert-HygieneTest ($message.Length -lt 4000) 'Violation message should remain bounded and path-focused.'

    $removed = @(Clear-WindowsPackageRuntimePayload -RootPath $root)
    Assert-HygieneTest ($removed.Count -gt 0) 'Cleanup did not remove blocked runtime payload paths.'
    Assert-HygieneTest (-not (Test-Path -LiteralPath (Join-Path $root 'SomePayload\EBWebView'))) 'Cleanup left a blocked EBWebView folder behind.'

    New-TestFile -Path (Join-Path $root 'StaticApp\assets\scripts\CacheStorage\fixture.js')
    New-TestFile -Path (Join-Path $root 'StaticApp\assets\scripts\Local Storage\fixture.js')
    Assert-WindowsPackagePayloadClean -RootPath $root -Context 'StaticApp fixture'

    Write-Host '[windows-package-payload-hygiene] PASS: blocked WebView2/cache payloads are detected, cleaned, and StaticApp assets are allowed.'
} finally {
    if (Test-Path -LiteralPath $root) {
        Remove-Item -LiteralPath $root -Recurse -Force
    }
}
