$script:WindowsPackageRuntimePayloadSegments = @(
    'LensDocsStudio.Windows.exe.WebView2',
    'EBWebView',
    'Service Worker',
    'CacheStorage',
    'GPUCache',
    'Code Cache',
    'DawnCache',
    'DawnGraphiteCache',
    'DawnWebGPUCache',
    'IndexedDB',
    'Local Storage',
    'Session Storage',
    'WebStorage'
)

function ConvertTo-WindowsPackageRelativePath {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    return ([System.IO.Path]::GetRelativePath($rootFullPath, $fullPath) -replace '\\', '/')
}

function Get-WindowsPackagePayloadViolationReason {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalisedPath = ($RelativePath -replace '\\', '/').Trim('/')
    if ([string]::IsNullOrWhiteSpace($normalisedPath)) {
        return $null
    }

    $segments = @($normalisedPath -split '/')
    $insideStaticApp = $segments.Count -gt 0 -and $segments[0] -eq 'StaticApp'
    foreach ($segment in $segments) {
        if ($segment -in @('LensDocsStudio.Windows.exe.WebView2', 'EBWebView')) {
            return "blocked WebView2 user-data folder segment '$segment'"
        }

        if (-not $insideStaticApp -and $segment -in $script:WindowsPackageRuntimePayloadSegments) {
            return "blocked runtime cache/user-data folder segment '$segment'"
        }
    }

    $fileName = Split-Path -Leaf $normalisedPath
    if (-not $insideStaticApp -and $fileName -match '\.log$') {
        foreach ($segment in $segments) {
            if ($segment -in $script:WindowsPackageRuntimePayloadSegments) {
                return "blocked runtime log under '$segment'"
            }
        }
    }

    return $null
}

function Get-WindowsPackagePayloadViolations {
    param([Parameter(Mandatory = $true)][string]$RootPath)

    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) {
        throw "Package payload root was not found: $RootPath"
    }

    Get-ChildItem -LiteralPath $RootPath -Recurse -Force -ErrorAction Stop | ForEach-Object {
        $relativePath = ConvertTo-WindowsPackageRelativePath -RootPath $RootPath -Path $_.FullName
        $reason = Get-WindowsPackagePayloadViolationReason -RelativePath $relativePath
        if ($reason) {
            [pscustomobject]@{
                relativePath = $relativePath
                reason = $reason
            }
        }
    }
}

function Get-WindowsPackageZipPayloadViolations {
    param([Parameter(Mandatory = $true)][string]$ZipPath)

    if (-not (Test-Path -LiteralPath $ZipPath -PathType Leaf)) {
        throw "Package ZIP was not found: $ZipPath"
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        foreach ($entry in $archive.Entries) {
            $relativePath = ($entry.FullName -replace '\\', '/').Trim('/')
            $reason = Get-WindowsPackagePayloadViolationReason -RelativePath $relativePath
            if ($reason) {
                [pscustomobject]@{
                    relativePath = $relativePath
                    reason = $reason
                }
            }
        }
    } finally {
        $archive.Dispose()
    }
}

function Format-WindowsPackagePayloadViolationMessage {
    param(
        [Parameter(Mandatory = $true)][object[]]$Violations,
        [string]$Context = 'package payload'
    )

    $displayLimit = 20
    $sample = @($Violations | Select-Object -First $displayLimit | ForEach-Object {
        "- $($_.relativePath) ($($_.reason))"
    })
    $remaining = $Violations.Count - $sample.Count
    if ($remaining -gt 0) {
        $sample += "- ... $remaining more blocked path(s) omitted"
    }

    return "Blocked WebView2/runtime user-data found in ${Context}:`n$($sample -join [Environment]::NewLine)"
}

function Assert-WindowsPackagePayloadClean {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [string]$Context = 'package payload'
    )

    $violations = @(Get-WindowsPackagePayloadViolations -RootPath $RootPath)
    if ($violations.Count -gt 0) {
        throw (Format-WindowsPackagePayloadViolationMessage -Violations $violations -Context $Context)
    }
}

function Assert-WindowsPackageZipPayloadClean {
    param(
        [Parameter(Mandatory = $true)][string]$ZipPath,
        [string]$Context = 'package ZIP'
    )

    $violations = @(Get-WindowsPackageZipPayloadViolations -ZipPath $ZipPath)
    if ($violations.Count -gt 0) {
        throw (Format-WindowsPackagePayloadViolationMessage -Violations $violations -Context $Context)
    }
}

function Clear-WindowsPackageRuntimePayload {
    param([Parameter(Mandatory = $true)][string]$RootPath)

    if (-not (Test-Path -LiteralPath $RootPath -PathType Container)) {
        return @()
    }

    $blockedDirectories = @(Get-ChildItem -LiteralPath $RootPath -Recurse -Directory -Force -ErrorAction Stop | Where-Object {
        $relativePath = ConvertTo-WindowsPackageRelativePath -RootPath $RootPath -Path $_.FullName
        $null -ne (Get-WindowsPackagePayloadViolationReason -RelativePath $relativePath)
    } | Sort-Object { $_.FullName.Length })

    $removed = @()
    foreach ($directory in $blockedDirectories) {
        if (Test-Path -LiteralPath $directory.FullName -PathType Container) {
            $removed += ConvertTo-WindowsPackageRelativePath -RootPath $RootPath -Path $directory.FullName
            Remove-Item -LiteralPath $directory.FullName -Recurse -Force
        }
    }

    return $removed
}
