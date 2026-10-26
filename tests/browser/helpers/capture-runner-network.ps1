param([Parameter(Mandatory)][ValidateRange(1024,65535)][int]$Port)
$ErrorActionPreference='Stop'
if($env:GITHUB_ACTIONS -cne 'true' -or $env:RUNNER_OS -cne 'Windows'){throw 'This read-only collector is restricted to a disposable Windows Actions runner.'}
$taskProperties=[Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties()
$taskConnections=@($taskProperties.GetActiveTcpConnections())
$taskStats=$taskProperties.GetTcpIPv4Statistics()
$taskProcesses=@(Get-Process -Name msedge,chrome,python,node -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,HandleCount,@{n='ThreadCount';e={$_.Threads.Count}},WorkingSet64,PrivateMemorySize64)
@{
    atUtc=[DateTime]::UtcNow.ToString('o')
    scope='Read-only aggregate TCP counters and test-process resources; no network or process changes'
    states=@($taskConnections | Group-Object State | Select-Object Name,Count)
    loopbackTestConnections=@($taskConnections | Where-Object {
        ([Net.IPAddress]::IsLoopback($_.LocalEndPoint.Address) -and $_.LocalEndPoint.Port -eq $Port) -or
        ([Net.IPAddress]::IsLoopback($_.RemoteEndPoint.Address) -and $_.RemoteEndPoint.Port -eq $Port)
    } | Group-Object State | Select-Object Name,Count)
    tcp=$taskStats
    processes=$taskProcesses
} | ConvertTo-Json -Depth 5
