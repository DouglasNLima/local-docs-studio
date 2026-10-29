# SPDX-License-Identifier: MIT
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [Parameter(Mandatory)][string]$PayloadDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string]$SourceRevision
)
$ErrorActionPreference = 'Stop'
# Fail before copying if any required part of the scoped grant is missing.
$files = @('LICENSE', 'docs/open-source/configuration/mit-scope.json', 'docs/open-source/native-distribution-licensing.md')
foreach ($relative in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $RepositoryRoot $relative) -PathType Leaf)) {
        throw "Missing required scoped licence material: $relative"
    }
}
$scope = Get-Content -LiteralPath (Join-Path $RepositoryRoot $files[1]) -Raw | ConvertFrom-Json
if ($scope.scope -ne 'Original first-party portions only; embedded R3-R7 content excluded' -or
    ($scope.excludedBoundaries -join ',') -ne 'R3,R4,R5,R6,R7') { throw 'Invalid first-party licence scope.' }
$disclosure = [IO.File]::ReadAllText((Join-Path $RepositoryRoot $files[2]))
if (-not $disclosure.Contains('SOURCE_REVISION')) { throw 'Missing immutable source reference.' }
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'LICENSE') -Destination (Join-Path $PayloadDirectory 'LICENSE')
Copy-Item -LiteralPath (Join-Path $RepositoryRoot $files[1]) -Destination (Join-Path $PayloadDirectory 'FIRST-PARTY-SCOPE.json')
[IO.File]::WriteAllText((Join-Path $PayloadDirectory 'LICENSING.md'),
    $disclosure.Replace('SOURCE_REVISION', $SourceRevision), [Text.UTF8Encoding]::new($false))
