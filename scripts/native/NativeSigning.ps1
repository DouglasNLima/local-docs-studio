# Packaging-only signing adapters. Providers are explicitly configured, trusted local scripts.
function New-NativeSigningConfiguration {
    [CmdletBinding()]
    param(
        [string]$SigningThumbprint, [string]$SignToolPath, [string]$SigningProviderPath,
        [string]$ExpectedPublisherSubject, [string]$ExpectedCertificateSha256,
        [string]$TimestampUrl = 'https://timestamp.digicert.com'
    )
    if ($SigningThumbprint -and $SigningProviderPath) { throw 'Choose either a certificate-store signer or a signing provider.' }
    if (-not $SigningThumbprint -and -not $SigningProviderPath) {
        if ($SignToolPath -or $ExpectedPublisherSubject -or $ExpectedCertificateSha256) { throw 'Signing options require an explicitly selected signer.' }
        return [pscustomobject]@{ Mode = 'Unsigned'; TimestampUrl = $null }
    }
    if ([string]::IsNullOrWhiteSpace($ExpectedPublisherSubject) -or $ExpectedCertificateSha256 -notmatch '^[a-fA-F0-9]{64}$') {
        throw 'Signing requires the reviewed publisher subject and SHA-256 certificate fingerprint.'
    }
    $timestamp = $null
    if (-not [Uri]::TryCreate($TimestampUrl, [UriKind]::Absolute, [ref]$timestamp) -or $timestamp.Scheme -ne 'https' -or $timestamp.UserInfo) {
        throw 'Timestamping requires an absolute HTTPS URL without credentials.'
    }
    $tool = $null; $provider = $null
    if ($SigningThumbprint) {
        if ($SigningThumbprint -notmatch '^[a-fA-F0-9]{40}$') { throw 'The certificate-store thumbprint must contain 40 hexadecimal characters.' }
        if (-not $SignToolPath -or [IO.Path]::GetExtension($SignToolPath) -ne '.exe' -or -not (Test-Path -LiteralPath $SignToolPath -PathType Leaf)) { throw 'Supply the Windows SDK signtool path for signing.' }
        $tool = (Resolve-Path -LiteralPath $SignToolPath).Path
    } else {
        if ($SignToolPath) { throw 'A custom signing provider owns its tool configuration.' }
        if ([IO.Path]::GetExtension($SigningProviderPath) -ne '.ps1' -or -not (Test-Path -LiteralPath $SigningProviderPath -PathType Leaf)) {
            throw 'The signing provider must be an explicitly provisioned local PowerShell script.'
        }
        $provider = (Resolve-Path -LiteralPath $SigningProviderPath).Path
    }
    [pscustomobject]@{
        Mode = $(if ($provider) { 'Provider' } else { 'CertificateStore' })
        Thumbprint = $SigningThumbprint; SignToolPath = $tool; ProviderPath = $provider
        ExpectedPublisherSubject = $ExpectedPublisherSubject; ExpectedCertificateSha256 = $ExpectedCertificateSha256.ToUpperInvariant()
        TimestampUrl = $timestamp.AbsoluteUri
    }
}

function Invoke-NativeAuthenticodeSigning {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$FilePath, [Parameter(Mandatory)]$Configuration)
    if ($Configuration.Mode -eq 'Unsigned') { return }
    $file = (Resolve-Path -LiteralPath $FilePath).Path
    if ([IO.Path]::GetExtension($file) -ne '.exe') { throw 'Native package signing accepts executable files only.' }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    if ($Configuration.Mode -eq 'CertificateStore') {
        $start.FileName = $Configuration.SignToolPath
        $arguments = @('sign', '/sha1', $Configuration.Thumbprint, '/fd', 'SHA256', '/tr', $Configuration.TimestampUrl, '/td', 'SHA256', $file)
    } elseif ($Configuration.Mode -eq 'Provider') {
        # Arguments are separate values, never a command expression; credentials remain provider-owned.
        $start.FileName = Join-Path $PSHOME 'pwsh.exe'
        $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-File', $Configuration.ProviderPath, '-FilePath', $file, '-TimestampUrl', $Configuration.TimestampUrl)
    } else { throw 'The configured native signing mode is unsupported.' }
    foreach ($argument in $arguments) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    try {
        if (-not $process.WaitForExit(300000)) { $process.Kill($true); throw 'The configured signing provider exceeded five minutes.' }
        if ($process.ExitCode -ne 0) { throw 'The configured signing provider failed.' }
    } finally { $process.Dispose() }

    # A successful provider exit is not evidence of a valid or authorised signature.
    $signature = Get-AuthenticodeSignature -LiteralPath $file
    if ($signature.Status.ToString() -ne 'Valid' -or -not $signature.SignerCertificate) { throw 'Windows rejected the signed package.' }
    $certificate = $signature.SignerCertificate
    $fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData))
    if (-not [string]::Equals($certificate.Subject, $Configuration.ExpectedPublisherSubject, [StringComparison]::Ordinal) -or
        $fingerprint -ne $Configuration.ExpectedCertificateSha256) { throw 'The signed package does not match the reviewed publisher identity.' }
    if (-not $signature.TimeStamperCertificate) { throw 'The signed package has no verified timestamp certificate.' }
}
