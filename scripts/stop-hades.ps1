# Stops every local Hades/Lorule server so ports 2610, 2615 and 2620 are free.
#
# Why this exists: the game server opens http://localhost:2620/ with a hardcoded prefix
# (Hades.Server.Base/Network/Game/GameServer.cs), so no configuration can move it. One server left
# running from an earlier session blocks every later run - including the isolated characterization
# harness, which cannot pick a different port for it.

$ports = 2610, 2615, 2620

$servers = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq 'Lorule.GameServer.exe' -or $_.CommandLine -like '*Lorule.GameServer*' }

if (-not $servers) {
    Write-Output 'No Hades server process is running.'
}
else {
    foreach ($server in $servers) {
        Stop-Process -Id $server.ProcessId -Force
        Write-Output "Stopped Hades server PID $($server.ProcessId)."
    }

    Start-Sleep -Milliseconds 800
}

$busy = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $ports -contains $_.LocalPort }

if ($busy) {
    Write-Output 'Still listening (stop these before running the harness):'
    $busy | Format-Table LocalAddress, LocalPort, OwningProcess
    exit 1
}

Write-Output "Ports $($ports -join ', ') are free."
