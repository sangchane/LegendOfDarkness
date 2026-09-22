# 캐릭터 만들기 — 구현 계획

> **For agentic workers:** `superpowers:subagent-driven-development` 로 작업 단위로 실행한다.

**Goal:** 사람이 화면에서 **성별·머리 모양·머리색을 골라** 캐릭터를 만들고, 그 캐릭터로 노비스마을에 들어간다.
지금은 만들기 화면이 없고, 시험이 `0x01,0x01,0x01` 을 박아 보내 우회한다.

**Architecture:** 서버는 이미 받을 준비가 돼 있다(`ClientFormat04`). 손댈 곳은 모바일뿐이다.
알맹이에 계정·캐릭터 만들기를 보내는 길을 내고(A), 원작 배치 그대로 화면을 만들고(B),
머리 그림과 색을 원작 방식(팔레트 6칸 교체)으로 입힌다(C). 마지막에 여정 시험을 진짜 만들기로 바꾼다(D).

**Tech Stack:** C#/.NET 9 (알맹이) · Godot 4.6 + C# (화면) · 서버는 C#/.NET 5 (**안 고친다**)

**Spec:** 이 문서의 "사실" 절.

## 사실 — 고치기 전에 확인한 것 (2026-09-19)

| 무엇 | 어디 | 값 |
|---|---|---|
| 서버가 받는 것 | `sources/wren11/…/Network/ClientFormats/ClientFormat04.cs:17-20` | 세 바이트를 **HairStyle → Gender → HairColor** 차례로 읽는다 |
| 서버가 저장하는 곳 | `…/Network/Login/LoginServer.cs:166-181` (`Format04Handler`) | 그대로 넣는다. **범위 검사가 없다** — 우리가 고른 값이 그대로 박힌다 |
| 성별 | `…/Types/Gender.cs:3-7` | **1=남 · 2=여** (255=Both 는 아이템용) |
| 몸 스프라이트 | `LoginServer.cs:166-181` | `Display = (BodySprite)(Gender * 16)` — 서버가 알아서 정한다 |
| 머리 모양 | `data/character-creation/hairstyles.json` (커밋 `c7a181bc`) | **남 59가지**(1~60, 26 결번) · **여 56가지**(18·26·32·33 결번). 여자 번호는 전부 남자에도 있다 |
| 머리색 | 같은 파일 · 원작 `Legend.dat` 의 `color0.tbl` | **72가지(0~71) 전부.** `palh.tbl` 은 투구(239~396)만 다뤄 맨머리엔 제한이 없다 |
| 염색 방식 | `sources/wren11/da-lib/DALib/Drawing/Palette.cs:65-73` · `Definitions/CONSTANTS.cs:18` | 팔레트의 **98번부터 6칸**을 `color0.tbl` 의 그 번호 6색으로 덮는다 |
| 런타임 팔레트 교체 | `mobile/client/src/Palettes.cs` | 이미 있다 — 이어 붙인다 |
| 원작 만들기 창 | `docs/ui/original-451/dlgcre00.png` (640x480) · `data/original-ui/451.json:200-206` | 이름·비번 칸 · 오른쪽 위 **남/여 아이콘 둘** · 가운데 인물 미리보기 · 오른쪽 **HAIR·COLOR 를 ◀ ▶ 로 하나씩 넘김** · OK/Cancel |
| 화살표 부품 | `docs/ui/original-451/dlgcre03.png` (127x15) | HAIR·COLOR 옆 화살표 |
| 지금 모바일에 있는 머리 그림 | `mobile/client/assets/actor/` | **285번 하나뿐**(`hero-walk.png`·`hero-attack.png` 에 구워져 있다) |
| 알맹이가 아는 것 | `mobile/src/Lod.Mobile.Core/Net/HadesLoginClient.cs` | **로그인만.** 계정 만들기·캐릭터 만들기를 보내는 길이 없다 |
| 시험이 지금 하는 것 | `tests/hades-characterization/LoginFlow.cs:103-121` | `CreateAccountCommand` 로 계정, `CreateCharacterCommand` 에 **`0x01,0x01,0x01` 을 박아** 캐릭터 |
| 새 캐릭터 시작 자리 | `scripts/server-config/LoruleConfig.template.json:27-31` | 노비스마을(20373) (37,29) |

**믿지 말 것**: `sources/FallenDev/dark-ages-ts` 의 만들기 화면은 머리 17가지·색 14가지로 박아 놨고 **둘 다 틀렸다**.
패킷도 4바이트(`skinColour` 가 더 있다)로 **차례가 다르다**. 참고만 하고 값을 베끼지 마라(`AGENTS.md` 출처 우선순위: 아카이브 2순위 > 참고 저장소 3순위).

## Global Constraints

- **서버는 고치지 않는다.** 이 계획은 모바일과 시험만 건드린다.
- **다른 세션이 같은 체크아웃에서 일한다** — `docs/*.js`·`docs/index.html`·`docs/dashboard*`·`tests/docs-dashboard.test.js`·`AGENTS.md`·`WORKLOG.md` 손대지 마라. `NEXT.md` 은 `git add -p` 로 제 조각만.
  **`git add -A`·`git add .`·`git commit -a` 금지.** 이력 조작 금지.
- **소지품(가방) 파일 금지.**
- 그냥 `git status` 금지 — `git status --short --ignore-submodules=all`.
- `dotnet` 은 PATH 에 없다 — `export PATH="$PWD/.tools/dotnet-9.0.317:$PATH"`.
- 시험·빌드·고도를 **앞에서** 돌린다(`timeout` 600000). `scripts/godot.sh` 에 `--path` 를 주지 마라.
- 화면 규칙은 `docs/original-ui-451.md` — `Greybox.Stone()`·`Main.TouchMinimum`·`Main.Gutter`. **새 치수를 만들지 마라.**
- 커밋은 Conventional Commits + 한국어 제목 + 아래 두 줄:
  `Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>` ·
  `Claude-Session: https://claude.ai/code/session_01Ummb8CUxgpbMht8WVivCvH`

---

### Task A: 알맹이가 계정과 캐릭터를 만들 줄 안다

화면보다 먼저. 이것이 되면 시험이 **고정값 대신 고른 값**으로 캐릭터를 만들 수 있다.

**Files:** Modify `mobile/src/Lod.Mobile.Core/Net/HadesLoginClient.cs` · `Protocol/Login/Hades718LoginProtocol.cs`
· Test: `tests/hades-characterization/MobileClientProtocolTests.cs` 에 더한다

**Produces:**
- `Hades718LoginProtocol.CreateAccountRequest(string name, string secret, …)`
- `Hades718LoginProtocol.CreateCharacterRequest(byte hairStyle, byte gender, byte hairColor, …)` — **차례를 틀리지 마라**
- `HadesLoginClient.CreateCharacterAsync(…)` — 계정을 만들고 캐릭터를 만든다

**어떻게 하나**: `tests/hades-characterization/LoginFlow.cs:103-121` 이 **이미 그 두 패킷을 보내고 있다**. 그 바이트 모양을 본보기로 알맹이에 옮긴다. 값만 인자로 받게 한다.

TDD: 바이트 시험 먼저(`0x01,0x02,0x47` 같은 값을 주면 그 차례로 나오는지) → 그다음 진짜 서버로 만들어 로그인되는지.

---

### Task B: 만들기 화면 (원작 배치 그대로)

**Files:** Create `mobile/client/src/CreateScreen.cs` · Modify `mobile/client/src/LoginScreen.cs`(만들기로 가는 길) · `Main.cs`(화면 전환)

**원작 배치를 따른다**(`docs/ui/original-451/dlgcre00.png`):
- 이름·비밀번호·비밀번호 확인 칸
- **남/여** 두 갈래(아이콘 둘)
- **HAIR**: `◀ 12 ▶` 꼴로 하나씩 넘긴다. 번호는 `data/character-creation/hairstyles.json` 의 그 성별 목록만 돈다(결번을 건너뛴다)
- **COLOR**: `◀ 37 ▶`, 0~71
- 가운데 **미리보기** — Task C 전에는 지금 있는 그림으로 자리만 잡는다
- 만들기 / 취소

**성별을 바꿀 때 (정해 둠, 2026-09-19)**: 머리 18·32·33 번은 남자에만 있다. 남자로 그중 하나를 고른 뒤
여자로 바꾸면 **가장 가까운 있는 번호로 스스로 옮긴다**(위아래 둘 다 있으면 작은 쪽). 사용자에게 묻지 않고,
경고도 띄우지 않는다 — 없는 번호가 남아 캐릭터가 이상해지는 것만 막으면 된다.

**미리보기는 Task C 가 채운다.** 여기서는 자리와 넘기기가 되면 된다.

---

### Task C: 머리 그림과 색

**Files:** Create/Modify `scripts/build-client-hair.py` · `mobile/client/src/Palettes.cs` · 미리보기 부분

- **미리보기는 맨몸이다 (사용자, 2026-09-19)**: 옷·모자·신발을 **하나도 안 입은** 몸에 고른 머리만 얹는다.
  모자를 쓰고 있으면 머리가 가려져 고를 수가 없다. 지금 자리에 있는 `hero-walk.png` 는 머리 285번에 옷까지
  구워진 그림이라 **쓰면 안 된다.** 필요한 것은 ① 성별별 맨몸 ② 머리 번호별 그림 둘뿐이다.
  (서버가 몸을 정하는 식은 `Display = (BodySprite)(Gender * 16)` — `LoginServer.cs:166-181`.)
- 머리 그림을 `khan.dat`·`khan2.dat` 에서 뽑는다(남 59 · 여 56). **미리보기에 쓸 한 방향·한 프레임이면 된다** — 걷는 동작 전체를 뽑지 마라.
- 색은 **굽지 않는다.** 조합이 4천 개가 넘는다. 원작처럼 **팔레트 98번부터 6칸을 갈아 끼운다**(`Palette.Dye`). `mobile/client/src/Palettes.cs` 가 이미 런타임 교체를 한다 — 이어 붙인다.
- `color0.tbl` 을 읽어 72가지 색 6칸을 자료로 둔다(`data/character-creation/` 아래).

---

### Task D: 계정 만들기부터 포테의숲까지 — 진짜로

**Files:** Modify `tests/hades-characterization/WorldMapMenuTests.cs`(또는 `LoginFlow.cs`) · `docs/mobile-client.md` · `NEXT.md`

- 여정 시험이 **고른 값으로** 캐릭터를 만들게 바꾼다(예: 여자·머리 32번·색 40). 지금 `0x01,0x01,0x01` 을 박는 자리다.
- 만들기 화면을 **손 없이 한 번 눌러 보는** 확인용 인자(`--create` 같은 것)를 더하고 화면 한 장을 찍는다.
- 문서에서 "만들기 화면이 없다"는 기록을 고친다.

---

## 자기 점검

- **명세 덮기**: 성별(A·B) · 머리 모양(A·B·C) · 머리색(A·B·C) · 실제로 만들어져 들어가는가(A·D).
- **자리표시자**: 없음. 값은 전부 위 "사실" 절에 있다.
- **남는 위험**:
  - 머리 그림을 뽑는 도구가 100 이하 번호를 어떻게 다루는지 Task C 에서 확인해야 한다(`scripts/build-client-wardrobe.py` 가 본보기).
  - 미리보기 색을 런타임에 갈아 끼우는 것이 고도에서 얼마나 비싼지 모른다 — 72칸을 한꺼번에 그리지 말고 **고른 하나만** 그린다.
  - 여자 전용/남자 전용 번호(18·32·33) 때문에 성별을 바꾸면 고른 머리 번호가 없을 수 있다 — 그때 어떻게 할지 Task B 에서 정한다(가장 가까운 번호로 내리기 등).
