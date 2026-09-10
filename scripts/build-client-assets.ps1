<#
.SYNOPSIS
    Draws the client's art out of this repository's own .dat archives.

.DESCRIPTION
    Every picture under mobile/client/assets comes from here, so nothing is hand-edited and nothing is
    carried in from a restored copy made elsewhere. Re-run after changing tools/dat-extract.

    The frame layout these sheets rely on — two drawings per action, the other two directions mirrored —
    is written down in docs/original-sprite-animation.md.
#>
[CmdletBinding()]
param(
    [string] $Game = "$PSScriptRoot/../sources/Dark-Ages-Private-Server-master/game",
    [string] $Maps = "$PSScriptRoot/../sources/Dark-Ages-Private-Server-master/database/server/maps",
    [string] $Output = "$PSScriptRoot/../mobile/client/assets"
)

$ErrorActionPreference = 'Stop'

$tool = Resolve-Path "$PSScriptRoot/../tools/dat-extract/bin/Debug/net8.0/dat-extract.dll" -ErrorAction SilentlyContinue

if (-not $tool) {
    throw "Build the extractor first: dotnet build tools/dat-extract/DatExtract.csproj"
}

# net8.0 output, so the shared runtime rather than the workspace SDK.
$dotnet = 'C:/Program Files/dotnet/dotnet.exe'

function Invoke-Extract {
    param([string[]] $Arguments)

    & $dotnet $tool @Arguments | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw "dat-extract failed: $($Arguments -join ' ')"
    }
}

New-Item -ItemType Directory -Force -Path "$Output/world", "$Output/actor" | Out-Null

Write-Output 'Drawing the safe house floor...'
Invoke-Extract @('map', "$Game/seo.dat", "$Maps/lod1.map", '30', '31', "$Output/world/safehouse.png")

# Every figure and every piece is cut on the same cell, wide enough for a weapon held out to the side.
# A piece that does not fit stops the run rather than being quietly clipped.
$cell = '80x88'

Write-Output 'Stacking the wardrobe into figures...'
# Body, then what it wears, then what it wears on its head — the order the original draws them in.
Invoke-Extract @('pose', "$Game/khan.dat", 'mb00101,mi00101,MH28501', "$Output/actor/hero-walk.png", '0,1,2,3,4,5,6,7,8,9', '1', $cell)
Invoke-Extract @('pose', "$Game/khan.dat", 'mb00102,mi00102,MH28502', "$Output/actor/hero-attack.png", '0,1,2,3', '1', $cell)
Invoke-Extract @('pose', "$Game/khan.dat", 'mb00101,MU06101,MH28501', "$Output/actor/npc-walk.png", '0,1,2,3,4,5,6,7,8,9', '1', $cell)

Write-Output 'Cutting the wardrobe into single pieces...'
# One file per piece, so the client can dress each person in whatever the server says they are wearing.
# Every sheet uses the same cell, which is what keeps a hat on a head once they are drawn apart.
# A number that is not here is simply not drawn — add a line when the world gains the item.
$wardrobe = @('b001', 'n001') + (1..8 | ForEach-Object { 'h{0:000}' -f $_ }) +
    @(
        'l001'  # 신발 — Shagreen Boots (Image 1)
        's006'  # 방패 — Luathas Bronze Shield (Image 6)
    )

New-Item -ItemType Directory -Force -Path "$Output/actor/parts" | Out-Null

# A piece the archive does not have is a note, not a failure, so the run must survive one saying so.
$ErrorActionPreference = 'Continue'

foreach ($gender in @('m', 'w')) {
    $archive = if ($gender -eq 'm') { "$Game/khan.dat" } else { "$Game/khan2.dat" }

    foreach ($piece in $wardrobe) {
        $name = "$gender$piece"

        # Not every piece is drawn for both genders, and a missing one is not a failure.
        # 'marker': 염색되는 자리를 표시색으로 남긴다. 클라이언트가 실행 중에 진짜 색으로 갈아 끼운다.
        & $dotnet $tool @('pose', $archive, "${name}01", "$Output/actor/parts/$name.png",
            '0,1,2,3,4,5,6,7,8,9', '1', $cell, 'marker') | Out-Null

        if ($LASTEXITCODE -ne 0) {
            Write-Output "  ${name}: 없음"
        }
    }
}

$ErrorActionPreference = 'Stop'

# 표시색이 무엇인지, 그리고 번호마다 무슨 색인지 — 클라이언트가 둘 다 읽어야 갈아 끼울 수 있다.
Invoke-Extract @('dyeslots', "$Output/actor/parts/dye-slots.txt")
Copy-Item "$PSScriptRoot/../data/legend-tables/color0.tbl" "$Output/actor/parts/dye-colours.txt" -Force

Write-Output 'Drawing a creature...'
# 'transparent' rather than the Korean spelling: an argument in Hangul does not survive PowerShell's
# hand-off to a native executable on this machine, and the sheet comes out with its background filled in.
Invoke-Extract @('mpf', "$Game/hades.dat", 'MNS001.MPF', "$Output/actor/wasp.png", '1', 'transparent')

# 같은 그림을 서버가 부르는 번호로도 둔다. 서버는 16385 라고 하고, 그림은 MNS001 이다 — 0x4000 을 뺀다.
New-Item -ItemType Directory -Force -Path "$Output/actor/creature" | Out-Null
Invoke-Extract @('mpf', "$Game/hades.dat", 'MNS001.MPF', "$Output/actor/creature/mns001.png", '1', 'transparent')

Get-ChildItem -Path $Output -Recurse -Filter *.png | ForEach-Object {
    Write-Output ("  {0}  {1:N0} bytes" -f $_.FullName.Substring($_.FullName.IndexOf('assets')), $_.Length)
}
