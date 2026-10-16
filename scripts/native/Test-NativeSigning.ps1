[CmdletBinding()]
param([string]$OutputDirectory = 'artifacts/native/signing-adapters')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'NativeSigning.ps1')
$root = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('adapter-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$provider = Join-Path $root 'provider [fixture].ps1'
$file = Join-Path $root 'unsigned [fixture]; sample.exe'
[IO.File]::WriteAllText($file, 'Inert adapter test bytes; never executed.')
[IO.File]::WriteAllText($provider, @'
param([string]$FilePath, [string]$TimestampUrl)
[IO.File]::WriteAllText($FilePath + '.provider.json', (@{file=$FilePath; timestamp=$TimestampUrl} | ConvertTo-Json))
'@)
$script:certificateBytes = [Text.Encoding]::UTF8.GetBytes('Mock certificate data for adapter policy tests only.')
$fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($certificateBytes))
$subject = 'CN=Mock adapter test identity'
$script:signatureStatus = 'Valid'; $script:signatureSubject = $subject
$script:hasTimestamp = $true; $script:verificationCalls = 0
# Only this test process replaces the OS query. Real Windows trust has a separate CI certificate suite.
function Get-AuthenticodeSignature {
    param([string]$LiteralPath)
    $script:verificationCalls++
    if ($LiteralPath -ne $file) { throw 'The verifier received a different path.' }
    [pscustomobject]@{
        Status=$script:signatureStatus
        SignerCertificate=[pscustomobject]@{ Subject=$script:signatureSubject; RawData=$script:certificateBytes }
        TimeStamperCertificate=$(if ($script:hasTimestamp) { 'Mock timestamp certificate' } else { $null })
    }
}
$defaults = @{ SigningProviderPath=$provider; ExpectedPublisherSubject=$subject; ExpectedCertificateSha256=$fingerprint }
$checks = [Collections.Generic.List[string]]::new()
function Reject([string]$Name, [scriptblock]$Action, [string]$Message) {
    try { & $Action | Out-Null }
    catch { if ($_.Exception.Message -notlike "*$Message*") { throw }; $checks.Add($Name); return }
    throw "The adapter accepted a rejected configuration: $Name"
}
$unsigned = New-NativeSigningConfiguration
Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $unsigned
if ($unsigned.Mode -ne 'Unsigned' -or $verificationCalls -ne 0 -or (Test-Path -LiteralPath ($file + '.provider.json'))) { throw 'Unsigned mode performed signing work.' }
$checks.Add('Unconfigured signing retains unsigned mode without a provider or verification call')
Reject 'Conflicting signers are rejected' { New-NativeSigningConfiguration @defaults -SigningThumbprint ('A' * 40) } 'Choose either'
Reject 'Missing reviewed identity is rejected' { New-NativeSigningConfiguration -SigningProviderPath $provider } 'reviewed publisher'
Reject 'HTTP timestamping is rejected' { New-NativeSigningConfiguration @defaults -TimestampUrl 'http://example.test/timestamp' } 'absolute HTTPS'
Reject 'Timestamp credentials are rejected' { New-NativeSigningConfiguration @defaults -TimestampUrl 'https://user:password@example.test/timestamp' } 'without credentials'
Reject 'Missing provider is rejected' { New-NativeSigningConfiguration -SigningProviderPath (Join-Path $root 'missing.ps1') -ExpectedPublisherSubject $subject -ExpectedCertificateSha256 $fingerprint } 'provisioned local'
Reject 'Incomplete certificate-store configuration is rejected' { New-NativeSigningConfiguration -SigningThumbprint ('A' * 40) -ExpectedPublisherSubject $subject -ExpectedCertificateSha256 $fingerprint } 'signtool path'
$configuration = New-NativeSigningConfiguration @defaults
Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $configuration
$record = Get-Content -LiteralPath ($file + '.provider.json') -Raw | ConvertFrom-Json
if ($record.file -cne $file -or $record.timestamp -cne $configuration.TimestampUrl -or $verificationCalls -ne 1) { throw 'Provider arguments or verification changed.' }
$checks.Add('The provider receives literal paths and its output is independently checked against the reviewed identity')
$script:signatureStatus = 'NotSigned'
Reject 'Successful provider exit cannot approve unsigned output' { Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $configuration } 'Windows rejected'
$script:signatureStatus = 'HashMismatch'
Reject 'Invalid signature output is rejected' { Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $configuration } 'Windows rejected'
$script:signatureStatus = 'Valid'; $script:signatureSubject = 'CN=Different mock identity'
Reject 'A different publisher is rejected' { Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $configuration } 'reviewed publisher identity'
$script:signatureSubject = $subject
$wrongPin = New-NativeSigningConfiguration -SigningProviderPath $provider -ExpectedPublisherSubject $subject -ExpectedCertificateSha256 ('0' * 64)
Reject 'A different certificate fingerprint is rejected' { Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $wrongPin } 'reviewed publisher identity'
$script:hasTimestamp = $false
Reject 'Missing timestamp is rejected' { Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $configuration } 'timestamp certificate'
$script:hasTimestamp = $true
$failingProvider = Join-Path $root 'failed-provider.ps1'; [IO.File]::WriteAllText($failingProvider, 'exit 7')
$failed = New-NativeSigningConfiguration -SigningProviderPath $failingProvider -ExpectedPublisherSubject $subject -ExpectedCertificateSha256 $fingerprint
$before = $verificationCalls
Reject 'Provider failure stops before signature acceptance' { Invoke-NativeAuthenticodeSigning -FilePath $file -Configuration $failed } 'provider failed'
if ($verificationCalls -ne $before) { throw 'A failed provider reached signature acceptance.' }
@{ passed=$true; checks=$checks; count=$checks.Count; mockedWindowsVerification=$true; certificateStoreChanged=$false; executableLaunched=$false } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'signing-adapters.json') -Encoding utf8NoBOM
"Native signing adapter policy: $($checks.Count) outcomes passed. No actual signing or certificate-store access."
