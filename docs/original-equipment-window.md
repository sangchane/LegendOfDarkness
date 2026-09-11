# 원작 장비창 — 칸이 실제로 어디에 있었나

- 기준일: 2026-09-11
- 어디서 나왔나: `setoa.dat`(화면 배치) 안의 `lequip.txt` · `_nui_eq.txt` · `_nui_eqa.txt`
- **어느 `setoa.dat` 이냐가 중요하다.** 저장소 안의 것을 써라:

  ```
  sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat   ← 이것 (항목 731개)
  sources/Dark-Ages-Private-Server-master/game/setoa.dat                       ← 쓰지 마라 (.gitignore, 항목 733개)
  ```

  둘은 **같은 파일이 아니다.** 다른 클라이언트 빌드라 `gui00.pal` 의 내용이 다르고, gitignore된 쪽의
  색표로 구형 장비창을 그리면 픽셀이 노이즈로 깨진다. 2026-09-11 에 실제로 그렇게 한 시간을 썼다.

  ```powershell
  $dat = 'sources/wren11/Dark-Ages-Private-Server/database/archives/setoa/setoa.dat'
  dat-extract dump $dat <폴더> equip     # 배치 .txt 와 그림
  dat-extract spf  $dat _nui_eq.spf  out.png       # 신형 — 팔레트 내장
  dat-extract epf  $dat equip01 out.png 1 1 gui00.pal   # 구형 — 색표는 gui00
  ```

---

## 1. 두 가지 배치가 있다

| | 파일 | 패널 크기 | 칸 수 | 생김새 |
|---|---|---|---|---|
| 구형 | `lequip.txt` | 266 x 298 | 14 | 거의 정사각. 장비창만 있다 |
| 신형 | `_nui_eq.txt` | 599 x 306 | 18 | 가로로 넓다. 왼쪽 장비 + 오른쪽 이름·직업·초상·능력치 |
| 신형(변형) | `_nui_eqa.txt` | 599 x 306 | 18 | `_nui_eq` 를 아래로 17px 민 것. 칸 구성은 같다 |

**신형 18칸이 우리 서버가 주는 자리와 그대로 맞는다.** `WornPlace`(`World/WorldEntry.cs`)는 1~17 을
알고 Arbiter 의 `EquipmentSlot` 은 18까지 있다. 구형 14칸에는 장신구·겉투구가 없다.

| 서버 번호 | 이름 | 구형 | 신형 |
|---|---|---|---|
| 1 | 무기 | WEAPON | WEAPON |
| 2 | 갑옷 | ARMOR | ARMOR |
| 3 | 방패 | SHIELD | SHIELD |
| 4 | 투구 | HEAD | HEAD |
| 5 | 귀고리 | EAR | EAR |
| 6 | 목걸이 | NECK | NECK |
| 7 | 왼손 | LHAND | LHAND |
| 8 | 오른손 | RHAND | RHAND |
| 9 | 왼팔 | LARM | LARM |
| 10 | 오른팔 | RARM | RARM |
| 11 | 허리 | BELT | BELT |
| 12 | 다리 | LEG | LEG |
| 13 | 신발 | FOOT | FOOT |
| 14 | 장신구 | **없음** | ARMOR2 |
| 15 | 겉옷 | CAPE | CAPE |
| 16 | 겉투구 | **없음** | HEAD2 |
| 17 | 장신구2 | **없음** | CAPE2 |
| 18 | 장신구3 | **없음** | CAPE3 |

14·17·18 이 ARMOR2·CAPE2·CAPE3 에 붙는 것은 **자리 수가 맞는다는 것까지만** 확인했다. 어느 쪽이
어느 쪽인지는 서버가 실제로 그 자리에 물건을 넣어 봐야 갈린다.

---

## 2. 구형 `lequip.txt` — 266 x 298

칸은 전부 **33 x 33**. 좌표는 패널 왼쪽 위가 원점인 `<RECT> x1 y1 x2 y2`.

| 칸 | x1 | y1 | 빈 칸 그림 (`equip07.epf`) |
|---|---|---|---|
| HEAD | 116 | 81 | 0 |
| EAR | 65 | 91 | 1 |
| NECK | 166 | 90 | 2 |
| ARMOR | 50 | 130 | 3 |
| CAPE | 183 | 130 | 4 |
| LARM | 12 | 169 | 5 |
| WEAPON | 50 | 169 | 6 |
| SHIELD | 183 | 169 | 7 |
| RARM | 221 | 169 | 8 |
| LHAND | 50 | 208 | 9 |
| RHAND | 183 | 208 | 10 |
| LEG | 65 | 247 | 11 |
| BELT | 166 | 247 | 12 |
| FOOT | 116 | 251 | 13 |

칸이 아닌 것:

| 이름 | RECT | 무엇 |
|---|---|---|
| `HumanImage` | 79 142 189 226 | **종이인형** 110 x 84 |
| `HumanState` | 93 227 173 239 | 상태 글 |
| `HumanIcon` | 93 227 104 238 | 상태 아이콘 (`emot001.epf` 0·1·7) |
| `NAME` | 124 7 239 19 | 이름 |
| `CLASSTEXT` | 27 7 111 19 | 직업 |
| `CLANTEXT` / `CLANTITLETEXT` | 57 35 141 47 / 152 35 248 47 | 무리·무리 직위 |
| `TITLETEXT` | 57 53 249 65 | 칭호 |
| `NATIONFLAG` | 9 28 48 73 | 국기 (`nation.epf`) |
| `LEGENDBTN` / `GROUPBTN` / `GROUPBTN2` | 8 80 / 208 80 / 207 108 (50 x 21) | 전설·무리 버튼 |
| `VIEWBTN` / `CLOSEBTN` | 4 258 63 288 / 209 263 259 284 | 보기·닫기 |
| 바탕 | `equip01.epf` 0 | 패널 그림 |

배치를 글로 그리면:

```
              [투구]
   [귀고리]              [목걸이]
   [갑옷]                [겉옷]
[왼팔][무기]  (종이인형)  [방패][오른팔]
   [왼손]                [오른손]
   [다리]   [신발]       [허리]
```

---

## 3. 신형 `_nui_eq.txt` — 599 x 306

칸은 전부 **32 x 32**. 왼쪽 절반(x 32~273)이 장비, 오른쪽(x 348~554)이 이름·초상·능력치다.

| 칸 | x1 | y1 |
|---|---|---|
| HEAD | 136 | 33 |
| HEAD2 | 136 | 70 |
| EAR | 85 | 43 |
| NECK | 186 | 42 |
| ARMOR2 | 32 | 82 |
| ARMOR | 70 | 82 |
| CAPE | 203 | 82 |
| CAPE2 | 241 | 82 |
| WEAPON | 70 | 121 |
| SHIELD | 203 | 121 |
| CAPE3 | 241 | 121 |
| LARM | 32 | 160 |
| LHAND | 70 | 160 |
| RHAND | 203 | 160 |
| RARM | 241 | 160 |
| LEG | 85 | 199 |
| BELT | 186 | 199 |
| FOOT | 136 | 203 |

| 이름 | RECT | 무엇 |
|---|---|---|
| `HumanImage` | 95 100 205 184 | **종이인형** 110 x 84 — 구형과 같은 크기 |
| `Portrait` / `PortraitText` | 348 229 396 285 / 416 228 554 288 | 초상과 그 설명 |
| `Nation` / `NationText` | 353 154 391 199 / 416 147 554 207 | 국기와 나라 설명 |
| `N_STR` `N_WIS` `N_DEX` `N_INT` `N_CON` `N_AC` | 70·150·234 x 257 / 70·150·228 x 276 (18 x 12) | 능력치 여섯 |
| `NAME` `CLASSTEXT` `CLANTEXT` `CLANTITLETEXT` `TITLETEXT` | 432 30 / 51 / 72 / 93 / 114 (120 x 12) | 오른쪽 글줄 다섯 |

```
                    [투구]
      [귀고리]              [목걸이]
[장신구][갑옷]              [겉옷][장신구2]
        [무기]  (종이인형)  [방패][장신구3]
  [왼팔][왼손]              [오른손][오른팔]
      [다리]    [신발]      [허리]
```

---

## 3.5 어느 자리에 놓이나 — 아이템이 정한다

클라이언트가 고르는 것이 아니다. **서버의 아이템 템플릿에 `EquipmentSlot` 이 적혀 있고**, 그 번호가
곧 `WornPlace` 이자 이 문서의 자리 번호다.

```
database/server/templates/items/Shagreen_Boots.json        "EquipmentSlot": 13   → 신발
database/server/templates/items/Luathas_Bronze_Shield.json "EquipmentSlot": 3    → 방패
database/server/templates/items/Luathas_Coral_Earrings.json "EquipmentSlot": 5   → 귀고리
```

번호의 이름표는 `Hades.Server.Base/Types/ItemSlots.cs` 의 `EquipSlot` 열거형이다(1 Weapon · 2 Armor ·
3 Shield · 4 Helmet · 5 Earring · 6 Necklace · 7·8 LHand/RHand · 9·10 LArm/RArm · 11 Waist · 12 Leg ·
13 Foot · 14 FirstAcc · 15 Trousers · 16 Coat · 17 SecondAcc).

입으면 `EquipmentManager` 가 그 자리에 넣고 **`0x37`** 로 자리 번호와 함께 알려 준다. 클라이언트는
그 번호를 `GearLayout` 에 넣어 칸을 찾을 뿐, 무엇이 어디에 가는지 스스로 판단하지 않는다 — 그래서
서버가 아이템을 새로 만들어도 화면은 고칠 것이 없다.

> 열거형의 `[Description]` 은 자리 이름과 어긋나 있다 — `Trousers = 15` 에 "Jewels", `Coat = 16` 에
> "Pants" 가 붙어 있다. 이름표가 아니라 **번호**를 믿는다.

---

## 4. 그림

두 세대가 그림도 따로 들고 있다. 둘 다 뽑아서 `docs/ui/assets/` 에 두었고 `docs/index.html` 에서 볼 수 있다.

| | 바탕 | 빈 칸 그림 | 형식 | 색표 |
|---|---|---|---|---|
| 구형 | `equip01.epf` 266 x 298 | `equip07.epf` 14장 33 x 33 | `.epf` | **`gui00.pal`** |
| 신형 | `_nui_eq.spf` 599 x 306 | `_nui_eqi.spf` 14장 32 x 32 | `.spf` | 파일 안에 들어 있다 |

- **`.spf` 는 자기 팔레트를 들고 있다.** 앞쪽에 RGB565 256색과 RGB555 256색이 연달아 오고, 앞의 것만
  쓴다. 그래서 신형은 바깥에서 색표를 고를 일이 없다 — `tools/dat-extract/Spf.cs`.
- **구형 `.epf` 의 색표는 `gui00.pal` 이다.** `setoa.dat` 의 `gui00`~`gui17` 중 하나인데 표가 없어
  열여덟 개를 다 대 보고 골랐다. 나머지 열일곱은 전부 노이즈가 된다.
  (`gui06.pal` 은 기술·마법 아이콘용이다 — `DADataViewer/SkillsForm.cs:70`.)
- 구형 `equip07.epf` 의 칸 그림 열넷은 신형 `_nui_eqi.spf` 의 열넷과 **같은 부위, 같은 차례**다.
  신형에서 늘어난 네 자리(장신구·겉투구·장신구2·3)는 전용 그림이 없다.
