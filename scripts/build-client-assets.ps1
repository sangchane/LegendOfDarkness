<#
.SYNOPSIS
    Draws the client's art out of this repository's own .dat archives.

.DESCRIPTION
    Every picture under mobile/client/assets comes from here, so nothing is hand-edited and nothing is
    carried in from a restored copy made elsewhere. Re-run after changing tools/dat-extract.

    The archives are the submodule's own, not the sources/Dark-Ages-Private-Server-master/ copy that used
    to be read here: that copy is in .gitignore, so on any other machine this script had nothing to read.
    Six of the nine archives are byte-identical between the two; hades, roh and setoa are not, and the
    submodule's are the larger ones. Everything this script draws comes out byte-identical either way —
    docs/where-the-answers-are.md 4절.

    The frame layout these sheets rely on — two drawings per action, the other two directions mirrored —
    is written down in docs/original-sprite-animation.md.
#>
[CmdletBinding()]
param(
    [string] $Archives = "$PSScriptRoot/../sources/wren11/Dark-Ages-Private-Server/database/archives",
    [string] $Server = "$PSScriptRoot/../sources/wren11/Dark-Ages-Private-Server/database/server",
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
Invoke-Extract @('map', "$Archives/seo/seo.dat", "$Server/maps/lod1.map", '30', '31', "$Output/world/safehouse.png")

# Every figure and every piece is cut on the same cell, wide enough for a weapon held out to the side.
# A piece that does not fit stops the run rather than being quietly clipped.
# 무기가 가로 114 · 세로 89 까지 뻗는다(build-client-wardrobe.py). 도구가 왼쪽 위에 맞추므로 발 자리는 그대로다.
$cell = '120x96'

Write-Output 'Stacking the wardrobe into figures...'
# Body, then what it wears, then what it wears on its head — the order the original draws them in.
Invoke-Extract @('pose', "$Archives/khan/khan.dat", 'mb00101,mi00101,MH28501', "$Output/actor/hero-walk.png", '0,1,2,3,4,5,6,7,8,9', '1', $cell)
Invoke-Extract @('pose', "$Archives/khan/khan.dat", 'mb00102,mi00102,MH28502', "$Output/actor/hero-attack.png", '0,1,2,3', '1', $cell)
Invoke-Extract @('pose', "$Archives/khan/khan.dat", 'mb00101,MU06101,MH28501', "$Output/actor/npc-walk.png", '0,1,2,3,4,5,6,7,8,9', '1', $cell)

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
    $archive = if ($gender -eq 'm') { "$Archives/khan/khan.dat" } else { "$Archives/khan2/khan2.dat" }

    foreach ($piece in $wardrobe) {
        $name = "$gender$piece"

        # Not every piece is drawn for both genders, and a missing one is not a failure.
        # 'marker': 염색되는 자리를 표시색으로 남긴다. 클라이언트가 실행 중에 진짜 색으로 갈아 끼운다.
        & $dotnet $tool @('pose', $archive, "${name}01", "$Output/actor/parts/$name.png",
            '0,1,2,3,4,5,6,7,8,9', '1', $cell, 'marker') | Out-Null

        if ($LASTEXITCODE -ne 0) {
            Write-Output "  ${name}: 없음"
            continue
        }

        # 평타는 파일이 따로다(끝 02). 네 칸뿐이고 걷기와 이어지지 않는다 — 3.3절.
        & $dotnet $tool @('pose', $archive, "${name}02", "$Output/actor/parts/${name}02.png",
            '0,1,2,3', '1', $cell, 'marker') | Out-Null

        if ($LASTEXITCODE -ne 0) {
            Write-Output "  ${name}02: 없음(평타 그림 없음)"
        }
    }
}

$ErrorActionPreference = 'Stop'

# 표시색이 무엇인지, 그리고 번호마다 무슨 색인지 — 클라이언트가 둘 다 읽어야 갈아 끼울 수 있다.
Invoke-Extract @('dyeslots', "$Output/actor/parts/dye-slots.txt")
Copy-Item "$PSScriptRoot/../data/legend-tables/color0.tbl" "$Output/actor/parts/dye-colours.txt" -Force

Write-Output 'Drawing creatures...'
# 서버가 부르는 번호로 둔다. 서버는 16385 라고 하고 그림은 MNS001 이다 — 0x4000 을 뺀다.
# 어느 괴물을 뽑을지는 서버의 몬스터 템플릿이 정한다(아이템 아이콘과 같은 방식). 목록을 손으로 적어
# 두면 서버가 괴물을 늘릴 때 조용히 어긋난다.
#
# 'strip': 정사각 칸 한 줄로 뽑고 구간을 적은 .txt 를 옆에 남긴다. 둘 다 필요하다 — 칸이 정사각이라야
# 클라이언트가 시트만 보고 프레임 크기를 알고, 구간이 있어야 그 괴물의 걷기·공격 프레임을 안다.
# 사람 번호로 재생하면 없는 프레임을 달라고 해 빈 화면이 나온다 (docs/original-sprite-animation.md 4절).
# 'transparent' rather than the Korean spelling: an argument in Hangul does not survive PowerShell's
# hand-off to a native executable on this machine, and the sheet comes out with its background filled in.
New-Item -ItemType Directory -Force -Path "$Output/actor/creature" | Out-Null

# JSON 으로 읽지 않는다. 이 템플릿들은 서버의 너그러운 파서에 맞춰 쓰여 있어서 — 목록 끝에 남은
# 쉼표(Spider 5s), 따옴표 없는 16진수(0x40C5) — PowerShell 의 ConvertFrom-Json 이 거부한다.
# 필요한 것은 Image 한 값뿐이므로 그 줄만 집는다.
$creatures = Get-ChildItem -Path "$Server/templates/monsters" -Filter *.json -Recurse |
    ForEach-Object {
        $found = [regex]::Match((Get-Content $_.FullName -Raw), '"Image"\s*:\s*"?(0x[0-9A-Fa-f]+|\d+)"?')

        if (-not $found.Success) { return }

        $written = $found.Groups[1].Value

        if ($written.StartsWith('0x')) { [Convert]::ToInt32($written, 16) } else { [int] $written }
    } |
    ForEach-Object { $_ - 0x4000 } |
    Where-Object { $_ -gt 0 } |
    Sort-Object -Unique

foreach ($number in $creatures) {
    $name = 'MNS{0:000}' -f $number
    Invoke-Extract @('mpf', "$Archives/hades/hades.dat", "$name.MPF",
        "$Output/actor/creature/$($name.ToLower()).png", '1', 'transparent', 'strip')
}

Write-Output "  $($creatures.Count) creatures: $($creatures -join ', ')"

Write-Output 'Drawing the empty equipment places...'
# 원작 신형 장비창의 빈 칸 그림 열넷. 'tight': 칸 사이를 띄우지 않아야 클라이언트가 frame*32 로 자른다.
# 어느 자리가 어느 칸을 쓰는지는 GearLayout 이 안다 (원작 _nui_eq.txt 가 자리마다 적어 둔 번호).
New-Item -ItemType Directory -Force -Path "$Output/ui" | Out-Null
Invoke-Extract @('spf', "$Archives/setoa/setoa.dat", '_nui_eqi', "$Output/ui/gear-slots.png", '14', '1', 'tight')

Write-Output 'Drawing item icons...'
# 서버는 아이템마다 DisplayImage 한 개를 준다. 0x8000 을 빼면 1부터 세는 칸 번호이고,
# 그 칸은 Legend.dat 의 item###.epf 안에 있다 (한 파일에 266칸). 서버가 가진 템플릿만 뽑는다.
New-Item -ItemType Directory -Force -Path "$Output/item" | Out-Null

Get-ChildItem -Path "$Server/templates/items" -Filter *.json | ForEach-Object {
    $display = (Get-Content $_.FullName -Raw | ConvertFrom-Json).DisplayImage

    if (-not $display) {
        Write-Output "  $($_.Name): no DisplayImage, skipped"
        return
    }

    Invoke-Extract @('icon', "$Archives/legend/Legend.dat", "$display", "$Output/item/$display.png", '1')
}

# 돈도 같은 번호 체계다 — Money.Image = MoneySprites + 0x8000 (Types/Money.cs:38).
# 템플릿이 아니라 enum 이라 여섯 개를 그대로 적는다: 금·은·동 낱개와 무더기.
foreach ($coin in 32905, 32906, 32907, 32908, 32909, 32910) {
    Invoke-Extract @('icon', "$Archives/legend/Legend.dat", "$coin", "$Output/item/$coin.png", '1')
}

Get-ChildItem -Path $Output -Recurse -Filter *.png | ForEach-Object {
    Write-Output ("  {0}  {1:N0} bytes" -f $_.FullName.Substring($_.FullName.IndexOf('assets')), $_.Length)
}
