[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'This certificate-store test runs only on the disposable Windows GitHub Actions runner.'
}
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'The disposable Windows runner must be elevated to manage its temporary machine trust anchor.'
}
Write-Output 'Preparing isolated unsigned, signed and tampered fixtures.'
$repository = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$source = Join-Path $repository 'src/native/LensDocsStudio.Native/bin/Release/net10.0-windows/LensDocsStudio.Native.exe'
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
$root = Join-Path $repository ('artifacts/native/authenticode/' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($root) | Out-Null
$sdkDirectory = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
$signTool = Get-ChildItem -LiteralPath $sdkDirectory -Filter signtool.exe -Recurse -File |
    Where-Object { $_.Directory.Name -eq 'x64' } | Sort-Object FullName -Descending | Select-Object -First 1
if (-not $signTool) { throw 'The Windows SDK signing tool is unavailable.' }
$unsigned = Join-Path $root 'unsigned-test-fixture.exe'
$signed = Join-Path $root 'signed-test-fixture.exe'
$tampered = Join-Path $root 'tampered-test-fixture.exe'
Copy-Item -LiteralPath $source -Destination $unsigned
Copy-Item -LiteralPath $source -Destination $signed
if ((Get-AuthenticodeSignature -LiteralPath $unsigned).Status -ne 'NotSigned') { throw 'The isolated fixture must start unsigned.' }
$certificate = $null
$certificateThumbprint = $null
$rootStoreOpened = $false
$publicCertificate = $null
# Current-user root changes can prompt for interactive confirmation. This root exists
# only on the elevated disposable runner; its private key remains in CurrentUser\My.
$rootStore = [Security.Cryptography.X509Certificates.X509Store]::new('Root', 'LocalMachine')
try {
    Write-Output 'Creating the one-day, non-exportable CI signing certificate.'
    $certificate = New-SelfSignedCertificate -Type CodeSigningCert -Subject ('CN=Lens Docs Studio disposable CI ' + [guid]::NewGuid().ToString('N')) `
        -CertStoreLocation 'Cert:\CurrentUser\My' -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 `
        -KeyExportPolicy NonExportable -NotBefore (Get-Date).AddMinutes(-5) -NotAfter (Get-Date).AddDays(1)
    $certificateThumbprint = $certificate.Thumbprint
    $publicCertificate = [Security.Cryptography.X509Certificates.X509CertificateLoader]::LoadCertificate($certificate.RawData)
    Write-Output 'Adding the public certificate to the disposable machine root store.'
    $rootStore.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
    $rootStoreOpened = $true
    $rootStore.Add($publicCertificate)
    Write-Output 'Signing the isolated fixture.'
    & $signTool.FullName sign /q /fd SHA256 /s My /sha1 $certificate.Thumbprint $signed
    if ($LASTEXITCODE -ne 0) { throw "Signing the isolated test fixture failed: $LASTEXITCODE." }
    Write-Output 'Verifying the isolated fixture with the Windows SDK.'
    & $signTool.FullName verify /q /pa $signed
    if ($LASTEXITCODE -ne 0) { throw "Windows SDK verification of the test fixture failed: $LASTEXITCODE." }
    $identity = @{ subject = $certificate.Subject; certificateSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificate.RawData)) }
    [IO.File]::WriteAllText((Join-Path $root 'publisher.json'), ($identity | ConvertTo-Json))
    $bytes = [IO.File]::ReadAllBytes($signed)
    if ($bytes.Length -le 8192) { throw 'The signed fixture is unexpectedly small.' }
    $bytes[4096] = $bytes[4096] -bxor 1
    [IO.File]::WriteAllBytes($tampered, $bytes)
    Push-Location $repository
    try {
        Write-Output 'Exercising the native verifier and exact publisher pinning.'
        & dotnet run --project src/native/LensDocsStudio.Native.Validation -c Release --no-build -- --authenticode $root
        if ($LASTEXITCODE -ne 0) { throw "Native Windows trust validation failed: $LASTEXITCODE." }
    } finally { Pop-Location }
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $sourceHash) { throw 'Trust validation altered the application build.' }
} finally {
    Write-Output 'Removing the disposable trust anchor and private signing key.'
    try {
        if ($rootStoreOpened -and $null -ne $publicCertificate) { $rootStore.Remove($publicCertificate) }
    } finally {
        $rootStore.Dispose()
        if ($null -ne $publicCertificate) { $publicCertificate.Dispose() }
        if ($null -ne $certificate) {
            Remove-Item -LiteralPath ('Cert:\CurrentUser\My\' + $certificate.Thumbprint) -DeleteKey -Force
            $certificate.Dispose()
        }
    }
}
if ((Test-Path -LiteralPath ('Cert:\LocalMachine\Root\' + $certificateThumbprint)) -or
    (Test-Path -LiteralPath ('Cert:\CurrentUser\Root\' + $certificateThumbprint)) -or
    (Test-Path -LiteralPath ('Cert:\CurrentUser\My\' + $certificateThumbprint))) { throw 'The disposable test certificate was not removed.' }
[IO.File]::WriteAllText((Join-Path $root 'test-certificate-cleanup.json'), (@{
    machineTestRootRemoved = $true
    currentUserSigningCertificateRemoved = $true
    sourceBuildUnchanged = $true
    productionTrustChanged = $false
    privateKeyExported = $false
} | ConvertTo-Json))
Write-Output 'Disposable Windows signature verification and certificate cleanup passed.'
