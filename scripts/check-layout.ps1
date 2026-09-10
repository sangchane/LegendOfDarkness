<#
.SYNOPSIS
    화면이 여러 크기에서 여전히 들어맞는지 확인한다.

.DESCRIPTION
    레이아웃은 조용히 깨진다 — 줄 하나가 화면 밖으로 밀려도 아무 소리가 나지 않는다. 실제로 인벤토리
    패널에 최소 높이를 박았더니 상태 막대와 방향판이 창 밖으로 나갔고, 스크린샷을 볼 때까지 몰랐다.

    그래서 시안(`docs/mobile-test-v1-wireframes.md` 2.1·2.3절)이 정한 기준 크기와 그 양옆의 화면비를
    한 번씩 띄워 보고, 각 줄의 자리를 찍은 뒤 화면을 벗어나거나 서로 겹치면 실패로 끝낸다.

    인벤토리를 연 상태로도 한 번씩 본다 — 그게 화면을 넘치게 만들었던 것이다.
#>
[CmdletBinding()]
param(
    [string] $Godot = "$PSScriptRoot/../.tools/godot-4.6-mono/Godot_v4.6-stable_mono_win64/Godot_v4.6-stable_mono_win64_console.exe"
)

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$godot = Resolve-Path $Godot
$client = Resolve-Path "$PSScriptRoot/../mobile/client"

# 시안이 정한 두 기준, 그리고 그 양옆 — 16:9 에서 20:9 까지.
$screens = @(
    @{ Name = '세로 기준 360x780'; Size = '360x780'; Orient = 'portrait' }
    @{ Name = '세로 좁고 낮음 360x640'; Size = '360x640'; Orient = 'portrait' }
    @{ Name = '세로 20:9  360x800'; Size = '360x800'; Orient = 'portrait' }
    @{ Name = '가로 기준 800x360'; Size = '800x360'; Orient = 'landscape' }
    @{ Name = '가로 16:9  640x360'; Size = '640x360'; Orient = 'landscape' }
    @{ Name = '가로 21:9  840x360'; Size = '840x360'; Orient = 'landscape' }
)

$failed = 0

foreach ($screen in $screens) {
    foreach ($pack in @($false, $true)) {
        $label = "$($screen.Name)$(if ($pack) { ' + 인벤토리' })"
        $arguments = @('--path', $client, '--', '--screen', 'game', '--layout',
            '--size', $screen.Size, '--orient', $screen.Orient)

        if ($pack) { $arguments += '--pack' }

        $output = & $godot @arguments 2>&1
        $bad = $output | Select-String 'GREYBOX_LAYOUT_BAD'

        if ($bad) {
            $failed++
            Write-Output "실패  $label"
            $bad | ForEach-Object { Write-Output "      $_" }
            $output | Select-String 'GREYBOX_LAYOUT ' | ForEach-Object { Write-Output "      $_" }
        }
        else {
            Write-Output "통과  $label"
        }
    }
}

Write-Output ''
Write-Output "$failed 개 화면에서 어긋났습니다."
exit $failed
