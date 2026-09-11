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
        $requires = if ($needs.Count -eq 2 -and $needs[0] -ne '0') { $needs[0] } else { $null }
        $atLevel = if ($requires) { [int] $needs[1] } else { 0 }

        $abilities.Add([pscustomobject]@{
            name       = $row.Key
            kind       = $section
            class      = $number
            # 다섯 숫자가 능력치 요구다. 어느 자리가 어느 능력치인지는 확인하지 못했다.
            statCosts  = @($fields[2] -split '/' | ForEach-Object { [int] $_ })
            requires   = $requires
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

# 아이템은 2,110개라 한 장씩 만들면 읽을 수가 없다. 종류별로 한 장에 모은다.
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
        $lines.Add("| $($it.name) | $($it.level) | $($it.weight) | $($it.describes) |")
    }

    Write-Note (Join-Path $vault "items/$safe.md") $lines
}

Write-Output "  items/ — $(($items | Group-Object kind).Count) 종류, $($items.Count)개"

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
    "- **아이템** $($items.Count)개 — ${tick}items/${tick}, 종류 $(($items | Group-Object kind).Count)가지로 묶었다."
)

Write-Note (Join-Path $vault 'index.md') $indexLines

Write-Output ''
Write-Output "Vault at $vault"
