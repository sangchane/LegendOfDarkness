<#
.SYNOPSIS
    원작의 자료표(metafile)를 이어 붙일 수 있는 모양으로 바꾼다.

.DESCRIPTION
    기술·마법·퀘스트·아이템·NPC 초상이 database/server/metafile/ 에 있다. 각각 zlib 한 덩어리이고,
    풀면 "줄마다 이름과 값 몇 개"인 표다. 그대로는 무엇이 무엇에 걸리는지 보이지 않으므로 여기서
    JSON 으로 바꾼다 — 그러면 선행 기술이 간선이 되고, 지식 그래프로 이을 수 있다.

    **뜻이 확인된 칸만 이름을 붙인다.** 나머지는 원문 그대로 raw 에 남긴다. 이름을 붙이려면 근거가
    있어야 하고, 없는 것을 추측해 적으면 다음 사람이 그것을 근거로 삼는다.

    근거: docs/where-the-answers-are.md 4.6절 · docs/game-data.md
#>
[CmdletBinding()]
param(
    [string] $Metafile = "$PSScriptRoot/../sources/wren11/Dark-Ages-Private-Server/database/server/metafile",
    [string] $Output = "$PSScriptRoot/../data/game-data"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$tool = Resolve-Path "$PSScriptRoot/../tools/dat-extract/bin/Debug/net8.0/dat-extract.dll" -ErrorAction SilentlyContinue

if (-not $tool) {
    throw "Build the extractor first: dotnet build tools/dat-extract/DatExtract.csproj"
}

# net8.0 output, so the shared runtime rather than the workspace SDK.
$dotnet = 'C:/Program Files/dotnet/dotnet.exe'

function Read-Table {
    param([string] $Name)

    $lines = & $dotnet $tool 'metafile' (Join-Path $Metafile $Name)

    if ($LASTEXITCODE -ne 0) {
        throw "dat-extract metafile failed: $Name"
    }

    # 줄은 "이름 <탭> 값" 이다. 탭이 없는 줄은 없다 — 값이 빈 줄이 구획 머리다.
    $lines | ForEach-Object {
        $cut = $_.IndexOf("`t")

        if ($cut -lt 0) { return }

        [pscustomobject]@{
            Key   = $_.Substring(0, $cut)
            Value = $_.Substring($cut + 1).Trim()
        }
    }
}

function Split-Fields {
    param([string] $Value)

    # "1/0/0 | 1/223/10 | 3/3/3/3/3 | Assail/1 | 0/0 |" — 끝의 빈 칸은 버린다.
    @($Value -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })
}

New-Item -ItemType Directory -Force -Path $Output | Out-Null

# ── 기술과 마법 ────────────────────────────────────────────────────────────────
# SClass1~5 는 직업 하나씩. 안에서 Skill..Skill_End 와 Spell..Spell_End 로 갈린다.
# 넷째 칸이 선행 조건 "이름/레벨" 이고, 0/0 이면 선행이 없다. 이것이 유일한 간선이다.
Write-Output 'Reading skills and spells...'

$abilities = [System.Collections.Generic.List[object]]::new()

foreach ($number in 1..5) {
    $section = $null

    foreach ($row in Read-Table "SClass$number") {
        if ($row.Value -eq '') {
            $section = switch ($row.Key) {
                'Skill' { 'skill' }
                'Spell' { 'spell' }
                default { $null }
            }

            continue
        }

        if (-not $section) { continue }

        $fields = Split-Fields $row.Value

        if ($fields.Count -lt 4) { continue }

        $needs = $fields[3] -split '/'
        # 0 은 "선행 없음", ? 는 원작이 비워 둔 칸이다(Sacrifice 가 그렇다). 둘 다 이름이 아니다.
        $requires = if ($needs.Count -eq 2 -and $needs[0] -ne '0' -and $needs[0] -ne '?') { $needs[0] } else { $null }
        $unknownPrereq = $needs.Count -eq 2 -and $needs[0] -eq '?' 
        $atLevel = if ($requires) { [int] $needs[1] } else { 0 }

        $abilities.Add([pscustomobject]@{
            name       = $row.Key
            kind       = $section
            class      = $number
            # 다섯 숫자가 능력치 요구다. 어느 자리가 어느 능력치인지는 확인하지 못했다.
            statCosts  = @($fields[2] -split '/' | ForEach-Object { [int] $_ })
            requires   = $requires
            unknownPrereq = $unknownPrereq
            atLevel    = $atLevel
            raw        = $fields
        })
    }
}

$abilities | ConvertTo-Json -Depth 6 | Set-Content "$Output/abilities.json" -Encoding utf8
Write-Output "  abilities.json — $($abilities.Count) (skill $(($abilities | Where-Object kind -eq 'skill').Count) / spell $(($abilities | Where-Object kind -eq 'spell').Count))"

# ── 퀘스트 ────────────────────────────────────────────────────────────────────
# SEvent1~7. 줄 이름이 "01_title" 처럼 번호와 칸 이름이 붙어 있어, 번호로 묶으면 한 퀘스트다.
Write-Output 'Reading quests...'

$quests = [System.Collections.Generic.List[object]]::new()

foreach ($number in 1..7) {
    $open = @{}

    foreach ($row in Read-Table "SEvent$number") {
        if ($row.Key -notmatch '^(\d+)_(.+)$') { continue }

        $id = "$number-$($Matches[1])"
        $field = $Matches[2]

        if (-not $open.ContainsKey($id)) {
            # key 라고 부르는 것은 원문에도 "01_id" 칸이 따로 있어 부딪히기 때문이다.
            $open[$id] = [ordered]@{ key = $id; book = $number }
        }

        $open[$id][$field] = $row.Value
    }

    foreach ($one in $open.Values) {
        if ($one.Contains('title') -and $one['title']) {
            $quests.Add([pscustomobject] $one)
        }
    }
}

$quests | ConvertTo-Json -Depth 6 | Set-Content "$Output/quests.json" -Encoding utf8
Write-Output "  quests.json — $($quests.Count)"

# ── 아이템 ────────────────────────────────────────────────────────────────────
# ItemInfo8~11. "이름 <탭> 레벨 | ? | 무게 | 종류 | 설명" — 무게는 설명의 Wt 와 맞고,
# 레벨은 Lev 와 맞는다. 둘째 칸은 무엇인지 확인하지 못해 raw 로만 남긴다.
Write-Output 'Reading items...'

$items = [System.Collections.Generic.List[object]]::new()

foreach ($number in 8..11) {
    foreach ($row in Read-Table "ItemInfo$number") {
        $fields = Split-Fields $row.Value

        if ($fields.Count -lt 4) { continue }

        $items.Add([pscustomobject]@{
            name     = $row.Key
            book     = $number
            level    = [int] $fields[0]
            weight   = [int] $fields[2]
            kind     = $fields[3]
            describes = if ($fields.Count -gt 4) { $fields[4] } else { '' }
            raw      = $fields
        })
    }
}

$items | ConvertTo-Json -Depth 6 | Set-Content "$Output/items.json" -Encoding utf8
Write-Output "  items.json — $($items.Count)"

# ── NPC 초상 ──────────────────────────────────────────────────────────────────
Write-Output 'Reading NPC portraits...'

$portraits = Read-Table 'NPCIllust' | Where-Object { $_.Value } | ForEach-Object {
    [pscustomobject]@{ name = $_.Key; portrait = $_.Value }
}

$portraits | ConvertTo-Json -Depth 4 | Set-Content "$Output/npc-portraits.json" -Encoding utf8
Write-Output "  npc-portraits.json — $($portraits.Count)"

Write-Output ''
Write-Output "Wrote to $((Resolve-Path $Output).Path)"

# ── NPC 스크립트 ──────────────────────────────────────────────────────────────
# scripts/Mundanes/ 의 25개. 여기 있는 것은 NPC 의 **행동**이고, 무엇을 파는지·어느 퀘스트를 거는지는
# templates/mundanes/ 의 인스턴스가 정하도록 돼 있다 — 그런데 그 폴더가 비어 있다. 그래서 이을 수 있는
# 것은 스크립트가 이름으로 직접 부르는 것뿐이다: GlobalItemTemplateCache["Eppe"] 같은 줄.
#
# 그 이름이 원작 자료표에 있는지 없는지가 그대로 "원작인가 Hades 가 만든 것인가"를 가른다.
Write-Output ''
Write-Output 'Reading NPC scripts...'

$mundanes = Join-Path (Split-Path $Metafile) 'scripts/Mundanes'
$originalItems = @{}; $items | ForEach-Object { $originalItems[$_.name] = $true }
$originalAbilities = @{}; $abilities | ForEach-Object { $originalAbilities[$_.name.ToLower()] = $true }
$originalNpcs = @{}; $portraits | ForEach-Object { $originalNpcs[$_.name] = $true }

$npcScripts = [System.Collections.Generic.List[object]]::new()

foreach ($file in Get-ChildItem -Path $mundanes -Filter *.cs) {
    $text = Get-Content $file.FullName -Raw

    $declared = [regex]::Match($text, '\[Script\("([^"]+)"(?:\s*,\s*"([^"]+)")?\)\]')
    if (-not $declared.Success) { continue }

    $named = @{}
    foreach ($kind in 'Item', 'Skill', 'Spell') {
        $named[$kind] = @([regex]::Matches($text, "Global${kind}TemplateCache\[`"([^`"]+)`"\]") |
            ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    }

    $questKeys = @([regex]::Matches($text, '"([A-Za-z0-9_'']*[Qq]uest[A-Za-z0-9_'']*)"') |
        ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)

    # 둘째 인자가 있으면 그것이 NPC 이름이고, 없으면 스크립트 이름이 곧 NPC 이름이다.
    $npcName = if ($declared.Groups[2].Success) { $declared.Groups[2].Value } else { $declared.Groups[1].Value }

    # 스크립트 이름에는 사람 이름이 아닌 것이 붙어 있다 — "tut/Raghnall", "Erin's Script".
    # 원작 초상 목록과 맞춰 보려면 그것을 떼어 낸다.
    $bare = ($npcName -replace '^.*/', '') -replace "'s Script$", ''
    $bare = $bare.Trim()

    $npcScripts.Add([pscustomobject]@{
        file    = $file.Name
        key     = $declared.Groups[1].Value
        npc     = $npcName
        # 원작 초상 목록에 이름이 있으면 원작 NPC, 없으면 Hades 가 만든 것이다.
        inOriginal = $originalNpcs.ContainsKey($bare)
        bare       = $bare
        items   = @($named['Item']  | ForEach-Object { [pscustomobject]@{ name = $_; inOriginal = $originalItems.ContainsKey($_) } })
        skills  = @($named['Skill'] | ForEach-Object { [pscustomobject]@{ name = $_; inOriginal = $originalAbilities.ContainsKey($_.ToLower()) } })
        spells  = @($named['Spell'] | ForEach-Object { [pscustomobject]@{ name = $_; inOriginal = $originalAbilities.ContainsKey($_.ToLower()) } })
        quests  = $questKeys
    })
}

$npcScripts | ConvertTo-Json -Depth 6 | Set-Content "$Output/npc-scripts.json" -Encoding utf8
$named = ($npcScripts | Where-Object { $_.items.Count + $_.spells.Count + $_.skills.Count -gt 0 }).Count
Write-Output "  npc-scripts.json — $($npcScripts.Count) (이름을 직접 부르는 것 $named)"

# ── 맵·워프·월드맵 ────────────────────────────────────────────────────────────
# 맵은 database/server/areas/*.json 이 정의한다(이름·크기·음악·.map 파일). 맵끼리는 워프로 잇고,
# 월드맵(temuair)은 그림 위의 점에서 맵으로 들어간다. 괴물은 AreaID 로 맵에 걸린다.
#
# WarpType 이 'Map' 이면 To.AreaID 가 목적지 맵이고, 'World' 면 월드맵 화면으로 나간다(To.Location 이 null).
Write-Output ''
Write-Output 'Reading maps and warps...'

$serverRoot = Split-Path $Metafile

# 이 파일들은 "Id" 와 "ID" 를 둘 다 들고 있다. 서버의 파서는 봐주지만 PowerShell 은 같은 열쇠로 보고
# 거부한다 — 뒤에 붙은 "ID" 를 떼고 읽는다. 값은 "Id" 와 같다.
$areas = @(Get-ChildItem -Path (Join-Path $serverRoot 'areas') -Filter *.json | ForEach-Object {
    # -creplace 로 대소문자를 가린다. -replace 는 안 가려서 "Id" 까지 지운다(실제로 그랬다).
    $a = ((Get-Content $_.FullName -Raw) -creplace ',\s*"ID"\s*:\s*\d+', '') | ConvertFrom-Json

    [pscustomobject]@{
        id      = $a.Id
        name    = $a.Name
        content = $a.ContentName
        cols    = $a.Cols
        rows    = $a.Rows
        music   = $a.Music
        mapFile = Split-Path $a.FilePath -Leaf
    }
})

$areas | ConvertTo-Json -Depth 4 | Set-Content "$Output/areas.json" -Encoding utf8
Write-Output "  areas.json — $($areas.Count)"

$warps = @(Get-ChildItem -Path (Join-Path $serverRoot 'templates/warps') -Filter *.json | ForEach-Object {
    $w = Get-Content $_.FullName -Raw | ConvertFrom-Json

    [pscustomobject]@{
        name     = $w.Name
        type     = $w.WarpType
        fromArea = $w.ActivationMapId
        # 밟는 칸이 여럿일 수 있다 — 월드맵으로 나가는 문은 아홉 칸이 한 줄로 늘어서 있다.
        steppingOn = @($w.Activations | ForEach-Object { "$($_.Location.X),$($_.Location.Y)" })
        toArea   = if ($w.WarpType -eq 'Map') { $w.To.AreaID } else { $null }
        toAt     = if ($w.WarpType -eq 'Map' -and $w.To.Location) { "$($w.To.Location.X),$($w.To.Location.Y)" } else { $null }
        needsLevel = $w.LevelRequired
    }
})

$warps | ConvertTo-Json -Depth 4 | Set-Content "$Output/warps.json" -Encoding utf8
Write-Output "  warps.json — $($warps.Count)"

$worldmaps = @(Get-ChildItem -Path (Join-Path $serverRoot 'templates/worldmaps') -Filter *.json | ForEach-Object {
    $m = Get-Content $_.FullName -Raw | ConvertFrom-Json

    [pscustomobject]@{
        name    = $m.Name
        field   = $m.FieldNumber
        describes = $m.Description
        portals = @($m.Portals | ForEach-Object {
            [pscustomobject]@{
                shows  = $_.DisplayName
                toArea = $_.Destination.AreaID
                toAt   = "$($_.Destination.Location.X),$($_.Destination.Location.Y)"
                # 월드맵 그림 위의 점. field###.png 가 그 그림이다.
                atPoint = "$($_.PointX),$($_.PointY)"
            }
        })
    }
})

$worldmaps | ConvertTo-Json -Depth 5 | Set-Content "$Output/worldmaps.json" -Encoding utf8
Write-Output "  worldmaps.json — $($worldmaps.Count) (문 $(($worldmaps.portals | Measure-Object).Count)개)"

# 괴물이 어느 맵에 나오나 — 템플릿의 AreaID. JSON 으로 읽지 않는 이유는 위와 같다.
$spawns = @(Get-ChildItem -Path (Join-Path $serverRoot 'templates/monsters') -Filter *.json -Recurse | ForEach-Object {
    $text = Get-Content $_.FullName -Raw
    $name = [regex]::Match($text, '"Name"\s*:\s*"([^"]+)"')
    $area = [regex]::Match($text, '"AreaID"\s*:\s*(\d+)')

    if (-not $name.Success) { return }

    [pscustomobject]@{
        monster = $name.Groups[1].Value
        areaId  = if ($area.Success) { [int] $area.Groups[1].Value } else { $null }
    }
})

$spawns | ConvertTo-Json -Depth 3 | Set-Content "$Output/monster-spawns.json" -Encoding utf8
Write-Output "  monster-spawns.json — $($spawns.Count)"

# ── Obsidian 노트 ─────────────────────────────────────────────────────────────
# 표를 그래프로 만드는 마지막 걸음. 간선이 자료 안에 이미 또렷하게 있으므로(선행 기술, 퀘스트 글 속의
# NPC 이름) LLM 으로 짐작할 것이 없다 — 그대로 [[링크]] 로 옮긴다. 짐작이 없으니 틀릴 일도 없다.
#
# 노트는 저장소에 담지 않는다(.gitignore). 위의 JSON 이 단일 출처이고 이것은 거기서 언제든 다시 난다.
Write-Output ''
Write-Output 'Writing Obsidian notes...'

$vault = Join-Path $Output 'vault'
Remove-Item -Recurse -Force $vault -ErrorAction SilentlyContinue
foreach ($room in 'abilities', 'quests', 'npcs', 'items') {
    New-Item -ItemType Directory -Force -Path (Join-Path $vault $room) | Out-Null
}

function Get-SafeName {
    param([string] $Name)
    ($Name -replace '[\/:*?"<>|#\[\]^]', '-').Trim()
}

# PowerShell 5.1 의 -Encoding utf8 은 BOM 을 붙인다. BOM 이 앞에 오면 Obsidian 이 첫 줄의
# --- 를 frontmatter 로 못 읽는다. 그래서 직접 쓴다.
$plainUtf8 = New-Object System.Text.UTF8Encoding $false

function Write-Note {
    param([string] $Path, [string[]] $Lines)
    [System.IO.File]::WriteAllText($Path, ($Lines -join [Environment]::NewLine), $plainUtf8)
}

$className = @{ 1 = '전사'; 2 = '도적'; 3 = '마법사'; 4 = '사제'; 5 = '수도사' }

# 무엇이 무엇을 선행으로 삼는가 — 되돌아가는 간선을 미리 모은다.
$unlocks = @{}
foreach ($one in $abilities) {
    if (-not $one.requires) { continue }
    if (-not $unlocks.ContainsKey($one.requires)) { $unlocks[$one.requires] = [System.Collections.Generic.List[object]]::new() }
    $unlocks[$one.requires].Add($one)
}

foreach ($sameName in $abilities | Group-Object name) {
    $one = $sameName.Group[0]
    $safe = Get-SafeName $one.name
    $learnedBy = @($sameName.Group.class | Sort-Object -Unique | ForEach-Object { $className[$_] })
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('---')
    $lines.Add("kind: $($one.kind)")
    $lines.Add("classes: [$($learnedBy -join ', ')]")
    $lines.Add('---')
    $lines.Add("# $($one.name)")
    $lines.Add('')
    $lines.Add("$($learnedBy -join ' · ') 가 배우는 $(if ($one.kind -eq 'skill') { '기술' } else { '마법' })입니다.")
    $lines.Add('')
    if ($one.requires) {
        $lines.Add("**선행** — [[$(Get-SafeName $one.requires)]] 를 $($one.atLevel) 단계까지 올려야 배운다.")
    } elseif ($one.unknownPrereq) {
        $lines.Add('**선행** — 원작이 이 칸을 `?` 로 비워 두었다. 무엇이 필요한지 자료에 없다.')
    } else {
        $lines.Add('**선행** — 없다. 처음부터 배울 수 있다.')
    }
    $lines.Add('')
    $next = $unlocks[$one.name]
    if ($next) {
        $lines.Add('**이것이 열어 주는 것**')
        # 여러 직업이 같은 기술을 가지므로 이름이 겹친다. 한 번만 적는다.
        foreach ($n in $next | Sort-Object name, atLevel -Unique) {
            $lines.Add("- [[$(Get-SafeName $n.name)]] ($($n.atLevel) 단계부터)")
        }
        $lines.Add('')
    }
    $lines.Add("능력치 요구(원문 차례 그대로): $($one.statCosts -join '/')")
    $lines.Add('')
    $lines.Add("원문: $($one.raw -join ' | ')")

    Write-Note (Join-Path $vault "abilities/$safe.md") $lines
}

Write-Output "  abilities/ — $($abilities.Count)"

# NPC 는 초상만 있으므로 노트는 얇다. 값은 퀘스트가 걸어 주는 링크에 있다.
foreach ($who in $portraits) {
    $safe = Get-SafeName $who.name

    Write-Note (Join-Path $vault "npcs/$safe.md") @(
        '---'
        'kind: npc'
        '---'
        "# $($who.name)"
        ''
        "초상: $($who.portrait) (npcbase.dat 안에 있다)"
    )
}

Write-Output "  npcs/ — $($portraits.Count)"

# 퀘스트 글에 NPC 이름이 그대로 적혀 있다 — "Talk to Alleen in Piet". 이름이 나오면 링크로 건다.
$npcNames = $portraits.name | Sort-Object { $_.Length } -Descending

foreach ($quest in $quests) {
    $safe = Get-SafeName $quest.title
    $text = "$($quest.sum) $($quest.result) $($quest.reward)"
    $mentioned = @($npcNames | Where-Object { $text -match "\b$([regex]::Escape($_))\b" })

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('---')
    $lines.Add('kind: quest')
    $lines.Add("book: $($quest.book)")
    $lines.Add('---')
    $lines.Add("# $($quest.title)")
    $lines.Add('')
    if ($quest.sum) { $lines.Add($quest.sum); $lines.Add('') }
    if ($quest.reward) { $lines.Add("**보상** — $($quest.reward)"); $lines.Add('') }
    if ($quest.result) { $lines.Add("**결과** — $($quest.result)"); $lines.Add('') }
    if ($mentioned.Count -gt 0) {
        $lines.Add('**나오는 사람**')
        foreach ($m in $mentioned) { $lines.Add("- [[$(Get-SafeName $m)]]") }
    }

    Write-Note (Join-Path $vault "quests/$safe.md") $lines
}

Write-Output "  quests/ — $($quests.Count)"

# 아이템은 한 장씩 만든다 — NPC 스크립트와 퀘스트가 이름으로 부르므로, 한 장씩 있어야 링크가
# 실제로 이어진다. 그리고 종류마다 목차를 한 장 더 둔다(2,110장을 그냥 늘어놓으면 못 읽는다).
New-Item -ItemType Directory -Force -Path (Join-Path $vault 'items/_kinds') | Out-Null

foreach ($one in $items) {
    Write-Note (Join-Path $vault "items/$(Get-SafeName $one.name).md") @(
        '---'
        'kind: item'
        "itemKind: $($one.kind)"
        "level: $($one.level)"
        '---'
        "# $($one.name)"
        ''
        "[[$(Get-SafeName $one.kind)]] · 레벨 $($one.level) · 무게 $($one.weight)"
        ''
        $one.describes
    )
}

foreach ($group in $items | Group-Object kind) {
    $safe = Get-SafeName $group.Name
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('---')
    $lines.Add('kind: item-kind')
    $lines.Add('---')
    $lines.Add("# $($group.Name) — $($group.Count)개")
    $lines.Add('')
    $lines.Add('| 이름 | 레벨 | 무게 | 설명 |')
    $lines.Add('|---|---|---|---|')
    foreach ($it in $group.Group | Sort-Object level, name) {
        $lines.Add("| [[$(Get-SafeName $it.name)]] | $($it.level) | $($it.weight) | $($it.describes) |")
    }

    Write-Note (Join-Path $vault "items/_kinds/$safe.md") $lines
}

Write-Output "  items/ — $(($items | Group-Object kind).Count) 종류, $($items.Count)개"

# 맵 노트 — 나가는 문·들어오는 문·나오는 괴물이 한 장에 모인다.
New-Item -ItemType Directory -Force -Path (Join-Path $vault 'maps') | Out-Null

$areaName = @{}
$areas | ForEach-Object { $areaName[$_.id] = $_.name }

foreach ($area in $areas) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('---')
    $lines.Add('kind: map')
    $lines.Add("areaId: $($area.id)")
    $lines.Add('---')
    $lines.Add("# $($area.name)")
    $lines.Add('')
    $lines.Add("$($area.cols) x $($area.rows) 칸 · 음악 $($area.music) · 지도 파일 $($area.mapFile)")
    if ($area.content -ne $area.name) { $lines.Add("안에서 부르는 이름: $($area.content)") }
    $lines.Add('')

    $out = @($warps | Where-Object { $_.fromArea -eq $area.id })
    if ($out.Count -gt 0) {
        $lines.Add('**나가는 문**')
        foreach ($w in $out) {
            if ($w.type -eq 'Map' -and $areaName.ContainsKey($w.toArea)) {
                $lines.Add("- $($w.steppingOn -join ' · ') 칸 → [[$(Get-SafeName $areaName[$w.toArea])]] 의 $($w.toAt) (레벨 $($w.needsLevel)+)")
            } else {
                $lines.Add("- $($w.steppingOn -join ' · ') 칸 → 월드맵 화면 (레벨 $($w.needsLevel)+)")
            }
        }
        $lines.Add('')
    }

    $into = @($warps | Where-Object { $_.type -eq 'Map' -and $_.toArea -eq $area.id })
    if ($into.Count -gt 0) {
        $lines.Add('**들어오는 문**')
        foreach ($w in $into) {
            $lines.Add("- [[$(Get-SafeName $areaName[$w.fromArea])]] 에서 → $($w.toAt)")
        }
        $lines.Add('')
    }

    foreach ($m in $worldmaps) {
        $doors = @($m.portals | Where-Object { $_.toArea -eq $area.id })
        if ($doors.Count -eq 0) { continue }
        $lines.Add("**월드맵에서 바로** — $($m.name) 그림의 $(($doors | ForEach-Object { $_.atPoint }) -join ' · ') 점에서 $(($doors | ForEach-Object { $_.toAt }) -join ' · ') 로 들어온다.")
        $lines.Add('')
    }

    $here = @($spawns | Where-Object { $_.areaId -eq $area.id })
    if ($here.Count -gt 0) {
        $lines.Add('**나오는 괴물**')
        foreach ($one in $here) { $lines.Add("- $($one.monster)") }
        $lines.Add('')
    }

    Write-Note (Join-Path $vault "maps/$(Get-SafeName $area.name).md") $lines
}

# 어느 맵에도 걸리지 않는 괴물 — 가리키는 AreaID 가 areas/ 에 없다는 뜻이고, 그러면 뜨지 않는다.
$orphans = @($spawns | Where-Object { -not $areaName.ContainsKey($_.areaId) })

if ($orphans.Count -gt 0) {
    Write-Note (Join-Path $vault 'maps/없는 맵.md') (@(
        '---'
        'kind: map'
        '---'
        '# 없는 맵'
        ''
        ('아래 괴물들은 ' + (($orphans | ForEach-Object { $_.areaId } | Sort-Object -Unique) -join ', ') +
         ' 번 맵에 나오게 돼 있는데, `areas/` 에 그런 맵이 없다. **그래서 뜨지 않는다.**')
        ''
        '안전 가옥이 비어 있던 이유이고, 시험용으로 `safehouse_wasp`(AreaID 1)를 손으로 만들어야 했던 이유다.'
        ''
    ) + @($orphans | ForEach-Object { "- $($_.monster) (AreaID $($_.areaId))" }))
}

Write-Output "  maps/ — $($areas.Count) (맵 없는 괴물 $($orphans.Count)마리)"

# NPC 스크립트 노트 — 여기서 아이템·마법·퀘스트로 링크가 나간다.
New-Item -ItemType Directory -Force -Path (Join-Path $vault 'npc-scripts') | Out-Null

foreach ($one in $npcScripts) {
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('---')
    $lines.Add('kind: npc-script')
    $lines.Add("original: $($one.inOriginal)")
    $lines.Add('---')
    $lines.Add("# $($one.npc)")
    $lines.Add('')
    if ($one.inOriginal) {
        $lines.Add("원작 NPC 다 — 초상 목록에 [[$(Get-SafeName $one.bare)]] 로 있다.")
    } else {
        $lines.Add('**원작 초상 목록에 없다.** Hades 가 만든 NPC 이거나, 이름이 원작과 다르게 붙어 있다.')
    }
    $lines.Add('')
    $lines.Add("스크립트: ``$($one.file)``")
    $lines.Add('')

    foreach ($pair in @(
        @{ Label = '부르는 아이템'; Rows = $one.items },
        @{ Label = '부르는 기술';   Rows = $one.skills },
        @{ Label = '부르는 마법';   Rows = $one.spells })) {

        if ($pair.Rows.Count -eq 0) { continue }

        $lines.Add("**$($pair.Label)**")
        foreach ($r in $pair.Rows) {
            # 원작 자료표에 있으면 노트가 있으니 링크로, 없으면 Hades 가 만든 것이라 그대로 적는다.
            if ($r.inOriginal) {
                $lines.Add("- [[$(Get-SafeName $r.name)]]")
            } else {
                $lines.Add("- $($r.name) — 원작 자료표에 없다 (Hades 가 만든 것)")
            }
        }
        $lines.Add('')
    }

    if ($one.quests.Count -gt 0) {
        $lines.Add("**퀘스트 열쇠** — $($one.quests -join ' · ')")
        $lines.Add('')
        $lines.Add('이 열쇠는 사람이 가진 깃발 이름이다. 원작 퀘스트 제목(`quests/`)과는 이어지지 않는다.')
    }

    Write-Note (Join-Path $vault "npc-scripts/$(Get-SafeName $one.npc).md") $lines
}

Write-Output "  npc-scripts/ — $($npcScripts.Count)"

$tick = [char] 96

@(
    '# 원작 자료'
    ''
    "${tick}data/game-data/*.json${tick} 에서 났다. 고칠 것이 있으면 JSON 이 아니라 원작 자료표를 보고,"
    "다시 만들려면 ${tick}scripts/build-game-data.ps1${tick} 을 돌린다."
    ''
    "- **기술·마법** $($abilities.Count)개 — ${tick}abilities/${tick}. 선행 관계가 링크로 걸려 있다."
    "- **퀘스트** $($quests.Count)개 — ${tick}quests/${tick}. 글에 이름이 나오는 사람에게 링크가 걸린다."
    "- **NPC** $($portraits.Count)명 — ${tick}npcs/${tick}."
    "- **아이템** $($items.Count)개 — ${tick}items/${tick}, 종류 목차는 ${tick}items/_kinds/${tick} 에 $(($items | Group-Object kind).Count)장."
    "- **NPC 스크립트** $($npcScripts.Count)개 — ${tick}npc-scripts/${tick}. 부르는 아이템·마법과 퀘스트 열쇠."
    "- **맵** $($areas.Count)개 — ${tick}maps/${tick}. 나가는 문·들어오는 문·나오는 괴물."
)

Write-Note (Join-Path $vault 'index.md') $indexLines

Write-Output ''
Write-Output "Vault at $vault"
