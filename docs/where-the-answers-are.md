# 원작이 어떻게 하는지 막혔을 때 — 어디를 보나

- 기준일: 2026-09-10
- 왜 있나: **같은 자리에서 반복해서 막혔다.** "원작은 이걸 어떻게 했지"를 서버 코드에서만 찾다가
  "이 저장소엔 없다"로 접은 적이 여러 번이다. 세 번 중 세 번 다 **다른 곳에 이미 있었다.**

---

## 1. 먼저 이 한 줄

```powershell
./scripts/find-in-sources.ps1 <찾을 말> [-Include *.cs] [-Limit 40]
```

참고 저장소 16개를 한 번에 뒤진다. `git grep` 이라 **커밋된 파일만** 보므로 `node_modules`·`bin`·`obj`
는 애초에 대상이 아니고, 열여섯 개를 다 뒤져도 1초 안쪽이다. 보통 검색이 `dark-ages-ts` 에서 멎는
이유가 그 폴더들이다.

- 정규식이다(`-E`). `.` `(` 는 `\.` `\(` 로 적는다.
- `Binary file … matches` 가 나오면 **아카이브 안에 그 이름이 들어 있다**는 뜻이다. 그러면
  `dat-extract list <그 .dat>` 로 열어 본다.

---

## 2. 무엇을 찾느냐에 따라

| 알고 싶은 것 | 어디 | 무엇이 나오나 |
|---|---|---|
| 원본 파일을 **읽는 법**(EPF·MPF·SPF·HPF·PAL·TBL·MAP) | `sources/wren11/da-lib/DALib/Drawing/` | 형식마다 클래스 하나. 머리말 순서가 여기 있다 |
| 사람을 **어떻게 겹쳐 그리나**(부위 순서·방향·염색) | `sources/FallenDev/dark-ages-ts/apps/client/src/game-objects/paper-doll/` | 부위 글자, 겹치는 차례, 방향별 앞뒤 |
| 부위가 **화면 어디에 놓이나** | `sources/FallenDev/dark-ages-ts/apps/client/public/aislings/<이름>/<이름>.atlas` | 조각마다 `bounds`(그림 자리)와 `offsets`(놓을 자리 + **바탕 크기**) |
| **원작 서버가 보내는 패킷**의 진짜 생김새 | `sources/FallenDev/dark-ages-ts/packages/network/src/packets/` | 필드 이름·크기·차례. **우리 서버(Hades)와 다르다** — 4절 |
| 우리 서버가 보내는 것 | `sources/wren11/Dark-Ages-Private-Server/src/.../Network/ServerFormats/` | 실제로 우리 클라이언트가 받는 바이트 |
| 동작 구간·몬스터 행동·색표 | `data/legend-tables/` (원본은 아래 4절) | `skill.tbl` `MobTile.tbl` `color.tbl` `color0.tbl` `itempal.tbl` … |
| **원작 게임 자료표** — 아이템·기술·퀘스트·NPC 초상 | `sources/.../database/server/metafile/` | 아래 4.6절. **.dat 이 아니라 서버 데이터베이스 폴더에 있다** |
| 그림·소리 원본 | `sources/Dark-Ages-Private-Server-master/game/*.dat` | `khan`(남) `khan2`(여) `hades`(몬스터) `roh`(효과) `seo`(타일) `ia`(아이콘) `setoa`(화면 배치) `national`(이야기) `cious`(던전) |
| 이야기 삽화·음악·규칙표 원본 | `sources/wren11/Dark-Ages-Private-Server/database/archives/legend/Legend.dat` | 13MB. `.epf` 186 · `.mp3` 165 · `.tbl` 16 |

---

## 3. 순서 — 데이터가 코드보다 정직하다

1. **원본 파일 자체**(`.dat` 안의 머리말·표)
2. **참고 구현 코드**
3. **그 저장소가 커밋해 둔 변환 산출물** — 아틀라스·표·에셋

**③이 가장 자주 답이었다.** 코드는 "무엇을 한다"만 말하고, 산출물에는 "실제 숫자"가 적혀 있다.
무기 오프셋이 그랬다 — 코드 어디에도 없었고 `.atlas` 의 `offsets:` 넷째 칸에 있었다.

---

## 4. 함정 세 개 (전부 실제로 밟았다)

**"여기 없다"고 적힌 문서를 그대로 믿지 마라.** `Legend.dat` 은 "설치 클라이언트에만 있다"고
적어 뒀었는데 **저장소 안에 있었다**(`.../database/archives/legend/Legend.dat`, 13MB). 표 16개는
이미 `data/legend-tables/` 에 뽑혀 있어 결과는 같았지만, 그 문장 때문에 한 번 접었다.

**우리 서버의 패킷은 원작과 다르다.** `0x33`(사람 표시)에서 Hades 는 무기를 1바이트로 쓰고
(원작은 2바이트), 장신구 색·장신구 셋째·도포 색·몸 색·투명·얼굴형·무리 이름을 아예 안 보낸다.
원작 형식으로 읽으면 자리가 어긋난다. **우리 클라이언트는 Hades 기준으로 맞춘다.**

**바닥에 쌓인 물건이 확인을 망친다.** 지역 서버를 오래 켜 두면 같은 칸에 옛 물건이 겹겹이 쌓이고,
그중에는 `Format07Handler` 가 조용히 건너뛰는 것이 섞여 있다 — 눌러도 아무 일이 안 일어나 **기능이
깨진 것처럼 보인다.** 바닥 물건은 저장되지 않고 서버 메모리에만 있으므로 **다시 띄우면 비워진다**
(원작도 그렇다. 사람이 재접속하는 것은 상관없다). 줍기·떨구기를 볼 때는 **먼저 서버를 다시 띄운다.**
2026-09-11 에 이것을 모르고 바이너리·설정·좌표를 다 뒤졌다.

**`.dat` 이 두 벌 있고 서로 다르다.** 같은 이름의 아카이브가 두 곳에 있다 —
`sources/wren11/Dark-Ages-Private-Server/database/archives/`(저장소 안, 커밋돼 있다)와
`sources/Dark-Ages-Private-Server-master/`(`.gitignore`, 로컬에만 있다). **다른 클라이언트 빌드다.**
`setoa.dat` 은 731 대 733 항목이고 `gui00.pal` 의 내용이 서로 다르다 — gitignore된 쪽 색표로 구형
장비창을 그리면 노이즈가 된다. **저장소 안의 것을 써라.** 다른 PC에서 되는 쪽도 그쪽뿐이다.
(`scripts/build-client-assets.ps1` 의 기본 경로는 아직 gitignore된 쪽을 가리킨다.)

**`sources/FallenDev/DAGL` 은 근거로 쓰지 않는다.** 7.41 클라이언트를 옮긴 것처럼 보이지만
README 가 스스로 AI 로 대량 생성한 것이라고 밝히고 있고, 실제로 사람 그리는 코드에 무기가 없다.
이름만 그럴듯한 파일이 많다.

---

## 4.5 원작 자료에 **없는** 것 — 드랍표

찾아봤다. 저장소의 아카이브 **아홉 개를 전부** 열어 글자 파일을 목록까지 뽑아 봤다.

| 아카이브 | 글자 파일 | 무엇인가 |
|---|---|---|
| `Legend.dat` | `.tbl` 16 | 스킬 구간·몬스터 행동·색·아이템 팔레트·감정표현 |
| `setoa.dat` | `.txt` 130 | **화면 배치**(창·버튼 좌표) |
| `national.dat` | `.tbl` 7 · `.txt` 6 | 이야기 글·안내문 |
| `ia.dat` | `.tbl` 12 | 아이콘 팔레트·이어붙이기(`stcpal` `stcani` `stc####`) |
| `cious.dat` | `.txt` 11 | 던전 대사·설정 |
| `roh.dat` | `.tbl` 282 | 효과 재생 순서 |
| `seo.dat` | `.tbl` 8 | 타일 팔레트 |
| `npcbase.dat` | `.tbl` 1 | NPC 그림 색인 (41바이트) |
| `hades.dat` | 없음 | 몬스터 그림뿐 |

**드랍표는 없다.** 참고 서버(`dark-ages-ts`)의 몬스터 데이터도 이름·레벨·HP·피해뿐이다. 원작에서도
무엇이 떨어지는지는 서버가 정하고 클라이언트는 결과만 받는다 — 그래서 클라이언트 자료에 있을 이유가
없다.

지금 쓰는 드랍은 **Hades 가 만든 것**이다: 괴물 템플릿의 `Drops` → `LootTable(템플릿 이름)` →
`LootDropper`, 개수는 설정의 `LootTableStackSize`. 실제 파일·줄·숫자는 **4.7 절**에 있다.

**있는 것은 움직임·전투 성향이다** — `MobTile.tbl`(`data/legend-tables/`, 원작 개발자 한글 주석 포함).
말벌 행: `SFCnt 2 · WFCnt 2 · AFCnt 2 · fStop 0 · fChgDir 1`.

---

## 4.6 원작 게임 자료표 — 메타파일

**.dat 안이 아니라 서버 데이터베이스 폴더에 있다.** 각각 zlib 한 덩어리고, 풀면 "몇 줄, 그리고 줄마다
이름과 값 몇 개" 라는 단순한 목록이다.

```
dat-extract metafile sources/wren11/Dark-Ages-Private-Server/database/server/metafile/SClass1 Assail
→ Assail   1/0/0 | 1/223/10 | 3/3/3/3/3 | 0/0 | 0/0
```

| 파일 | 줄 수 | 무엇인가 |
|---|---|---|
| `ItemInfo0`~`ItemInfo11` | 12개 (8~11만 2,110줄) | **원작 아이템 목록** — 이름, 등급, 착용 부위, 설명(`All Lev4, Wt 1`) |
| `SClass1`~`SClass5` | 641줄 | **직업별 기술·마법** — 선행 조건(`Assail/10`), 능력치 요구, 배우는 곳 |
| `SEvent1`~`SEvent7` | 361줄 | **퀘스트** — 제목·요약·조건 (`Choosing a Class` 등) |
| `NPCIllust` | 170줄 | NPC 이름 → 초상 파일(`shaman.spf`, `npcbase.dat` 안에 있다) |
| `Light` · `NationDesc` | 34 · 6 | 맵 밝기 · 나라 설명 |

여기에도 **드랍표는 없다**(4.5절). 다만 `Shagreen Boots` 를 찾으면 0줄이 나오는 것으로
**Hades 가 만든 아이템과 원작 아이템을 가릴 수 있다.**

---

## 4.7 Hades 의 드랍·이동·전투 — 실제 파일과 숫자

4.5 가 "드랍은 Hades 가 정한다"까지 말했다. 어디서 어떻게 정하는지가 여기 있다.
**규칙은 `src/` 가 아니라 `database/server/scripts/` 의 Roslyn 스크립트에 있다** — 서버 실행 중에
컴파일된다. `src/` 만 뒤지면 못 찾는다(그래서 한 번 못 찾았다).

### 숫자를 글자로 바꾸는 표
`database/server/templates/EnumReference.txt` — 템플릿 JSON 의 모든 숫자를 여기서 읽는다.
`MoodQualifer` `PathQualifer` `LootQualifer` `SpawnQualifer` `ItemFlags` `ItemColor` `TileContent` 등.

### 괴물 템플릿 — 말벌(`templates/monsters/insight_1/bees.json`)

| 칸 | 값 | 뜻 |
|---|---|---|
| `Image` | 16385 | `0x4001` — 괴물 그림 번호 |
| `LootType` | 36 | **깃발 합** = `Gold`(32) + `Table`(4). `Random`(2) 은 꺼져 있다 |
| `Drops` | `["random"]` | 표에 넣을 후보 고르는 방법 |
| `PathQualifer` | 1 | `Wander` — 목표가 없으면 아무 데나 |
| `MoodType` | 4 | `Unpredicable` |
| `SpawnType` | 4 | `Defined` — `DefinedX/Y` (45, 33) 에 고정 |
| `MovementSpeed` / `EngagedWalkingSpeed` | 1000 / 1000 | ms. 목표가 생기면 뒤엣것으로 바뀐다 |
| `AttackSpeed` / `CastSpeed` | 1000 / 8000 | ms |
| `SpawnMax` / `SpawnRate` | 1 / 1 | 한 마리씩 |

### 드랍이 정해지는 순서

1. `scripts/Creations/monsters.cs:199` — `LootType` 에 `Table` 깃발이 없으면 **표를 아예 안 만든다**.
2. 같은 파일 `:203` — `Drops` 의 값이 `"random"` 이면 **레벨 차 10 이내인 아이템 템플릿 중 하나**를 뽑아 표에 넣는다.
   **아이템 이름을 그대로 적으면 레벨을 보지 않고** 그 아이템을 넣는다(`:213`).
3. 죽으면 `Monster.GenerateRewards` → 설정의 `MonsterRewardScript` → `scripts/Formulas/monsterexp.cs`.
4. `monsterexp.cs:131 GenerateDrops()` — `Table` 이면 `LootDropper.Drop(표, Random.Next(3))`.
   3 은 `LoruleConfig.json` 의 `LootTableStackSize`.
5. 뽑기 무게는 `ItemTemplate.Weight => DropRate` (`Templates/ItemTemplate.cs:112`). **`DropRate` 가 곧 가중치다.**
6. 등급은 `UpgradeTable` 8단계(Common~Forsaken)에서 따로 뽑는다.
7. `Gold` 깃발이 있으면 `GenerateGold()` 가 돈을 따로 떨군다.

`bees.json`(레벨 1, `Drops: ["random"]`)에 들어갈 수 있는 것은 **산호 귀걸이 하나뿐**이다 —
아이템 템플릿 3개의 `LevelRequired` 가 각각 8 / 31 / 33 이라 10 이내는 8 하나다.

**주의 — 안전 가옥에서 잡는 말벌은 `bees` 가 아니다.** `safehouse wasp` 라는 별도 템플릿이고,
`tmp/hades-run/database/server/templates/monsters/insight_1/safehouse_wasp.json` **에만 있다**
(저장소의 `sources/` 에는 없는 손으로 만든 시험용 콘텐츠다). `AreaID 1` · `MaximumHP 30` ·
`LootType 4`(표만, 돈 없음) · `Drops: ["Shagreen Boots"]` — 이름을 적었으니 레벨 규칙을 타지 않고
**항상 장화가 후보**다. 잡아 보면 실제로 장화가 떨어진다.

### 이동과 전투

`scripts/Monsters/CommonMonster.cs` — 템플릿의 `ScriptName: "Common Monster"` 가 이 파일이다.

- `HandleMonsterState`(`:227`) 가 시계 셋(`BashTimer` `CastTimer` `WalkTimer`)을 굴린다.
  목표가 생기면 `WalkTimer.Delay` 를 `EngagedWalkingSpeed` 로 바꾼다.
- `Walk()`(`:352`) — 목표가 **옆칸이면** 방향을 맞추고 `Bash()`, 아니면 `WalkTo`,
  길이 막히면 `Wander()`. 목표가 없으면 `Patrol`(`Waypoints` 있을 때)이거나 `Wander()`.
- 죽으면 `OnDeath`(`:106`) → `GenerateRewards` → 위 순서.

---

## 4.8 아이템 아이콘 — `ia.dat` 의 `stc` 가 아니라 `Legend.dat` 의 `item###.epf`

한동안 `ia.dat` 의 `stc#####.hpf` 19,664장을 아이템 아이콘으로 알고 찾았다. **아니다.** `stc` 는
바닥에 놓인 정적 오브젝트다. 아이템 아이콘은 `Legend.dat` 안에 있다:

| 파일 | 개수 | 무엇 |
|---|---|---|
| `item001.epf` ~ `item050.epf` | 48개 (006·023 없음) | 아이콘 그림. **한 파일에 정확히 266칸** |
| `item000.pal` ~ | 56개 | 색표 |
| `itempal.tbl` | 33줄 | 칸 번호 → 색표 번호 |

### 서버 번호에서 그림까지

서버는 아이템마다 `DisplayImage` 하나를 준다 — 소지품(`0x0F`)이든 바닥(`0x07`)이든 같은 번호다.

```
칸번호  = DisplayImage - 0x8000        (32882 → 114)
파일    = (칸번호 - 1) / 266 + 1        (114 → item001.epf)
프레임  = (칸번호 - 1) % 266            (114 → 113번 칸, 0부터 셈)
색표    = itempal.tbl 에서 칸번호를 찾은 번호 → item###.pal (없으면 item000.pal)
```

**1부터 세는 번호다.** 0부터 세면 한 칸씩 밀려서 장화 대신 원피스가 나온다 — 실제로 그렇게 나왔다.
`DADataViewer/ItemsForm.cs:141` 이 권위이고(`(파일-1)*266 + 칸 + 1`), `DAGL/Graphics/ImageLoader.cs:80`
은 `-1` 이 없어 한 칸 어긋난다. **자료가 코드보다 정직하다**(3절)의 실례다.

확인한 세 개 — 32882 초록 장화 · 32957 청동 방패 · 33002 파란 구슬 귀걸이. 셋 다 `item001.epf`,
색표는 `item000.pal`(`itempal.tbl` 의 첫 줄이 2395부터라 2394 이하는 색표 0이다).

**돈도 같은 번호 체계다.** `Money.Image = MoneySprites + 0x8000`(`Types/Money.cs:38`) — 금·은·동
낱개가 137·138·139, 무더기가 140·141·142 이므로 32905~32910 이다. 아이템과 똑같이 `item001.epf`
의 136~141번 칸이라, 아이콘을 읽는 코드가 돈을 따로 알 필요가 없다.

뽑는 명령: `dat-extract icon <Legend.dat> <번호들> <출력.png> [배율]`.
`build-client-assets.ps1` 이 서버의 아이템 템플릿을 읽어 번호마다 한 장씩 `assets/item/<번호>.png` 로 둔다.

---

## 5. 자동으로 막아 주는 것들 (훅)

문서에 적어 두는 것만으로는 안 읽힌다. **기계가 판정할 수 있는 규칙은 훅으로 옮겼다** —
`.claude/settings.json` 에 걸려 있고 스크립트는 `tools/hooks/` 에 있다.

| 언제 | 무엇을 | 어디 |
|---|---|---|
| `git ...` 실행 전 | 잠금 없는 루트 상태 조회는 **막는다**. submodule 17개를 스캔하다 멈추면 `index.lock` 이 남아 git 전체가 마비된다. `--ignore-submodules=all` 이나 `-C sources/...` 면 통과 | `guard_shell.py` |
| `cp`·`mv`·`rsync`·PowerShell 실행 전 | `LOD_` 에서 복사해 오면 **알린다**(막지 않음) | 〃 |
| Artifact 호출 전 | 산출물은 `docs/` 마크다운으로 남기라고 **알린다** | `prefer_docs_over_artifact.py` |
| 세션이 끝날 때 | 포트 2610·2615·2620 이 열려 있으면 서버를 내리라고 **알린다** | `check_hades_ports.py` |

막는 것은 첫 줄 하나뿐이다. 나머지는 한 줄 알리고 지나간다 — 일부러 그렇게 할 때도 있기 때문이다.
훅을 보거나 끄려면 `/hooks`. 무엇을 막고 무엇을 통과시키는지는 `python tools/hooks/test_guard_shell.py`
로 확인한다.

**훅은 조용히 틀린다.** 안 막아도 아무 일이 없고, 잘못 막으면 엉뚱한 명령이 죽는다. 실제로 이 훅을
넣는 커밋이 **자기 자신에게 막혔다** — 커밋 메시지 본문에 그 두 낱말이 들어 있었기 때문이다. 그래서
지금은 토막의 맨 앞이 `git` 일 때만, 그리고 따옴표가 열리기 전까지만 명령으로 본다. 규칙을 손대면
위 시험을 돌린다.

**훅으로 옮기지 않은 것들.** "그린을 믿지 마라"·"class_name 파스에러"는 출력을 해석해야 해서 오판하기
쉽고, "쉽게 설명해라"·"작업에 맞는 모델을 써라"는 사람이 판정할 몫이다. 그런 것은 `CLAUDE.md` 와
기억에 남겨 둔다.
