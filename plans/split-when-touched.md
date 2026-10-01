# 고칠 때 나누기 — 큰 파일·섞인 폴더 정리 지도

2026-10-02 코드 리뷰(함수 하나에 기능 하나 · 폴더 구조 · 예외 처리 · 주석)에서 나온 것 가운데 **기능은 그대로이고 구조만 바꾸는 일**을 모았다.
~~고칠 때 하나씩~~ → **사용자 결정(2026-10-02): 지금 한다.** 큰 파일은 최근 14일 중 8~10일을 고치는 파일이라 미룰 이유가 없다.
순서: `GameScreen` → `WorldClient` → `WorldView` → `Main` → 기술 수치 빼기 → 나머지. 파일 하나 = 커밋 하나(동작 그대로). 나눈 뒤 이 표에서 그 줄을 지운다.

## 규칙
- 나누기 커밋은 동작을 바꾸지 않는다 — 시험이 그대로 통과해야 한다(`dotnet test mobile/tests/...`, 서버는 `tests/hades-characterization`).
- 처음에는 `partial class` 로 파일만 나눠도 된다. 공개 이름(`world.Pack` 같은 것)은 그대로 두어 화면 쪽이 안 바뀌게.
- Godot 스크립트를 옮기면 `.tscn` 의 `res://src/…` 경로와 `.cs.uid` 파일도 같이 옮긴다.
- 주석은 새로 쓰거나 고치는 것부터 한국어로. 변경 이력은 주석 말고 커밋·WORKLOG 에.

## 알맹이 `mobile/src/Lod.Mobile.Core`

| 파일 | 지금 | 나누는 경계 |
|---|---|---|
| `World/WorldClient.cs` (2280줄) | 신호 번호 표·상태 40여 개·받기 루프(`Listen` 470줄)·보내기·해석 함수(약 770줄)를 한 클래스가 다 한다 | `WorldPackets`(정적 `Read*` 전부) · `WorldState`(보이는 것·내 것) · `CompanionState`(`_master`~`_companionKitCount`) · `ChatLog`(`_said` `_told` `_heard`) · `PresentationQueues`(동작·이펙트·소리·음악·숫자 큐) · `WorldClient`(받기·갈래·보내기만). `Listen` 의 갈래는 `OnXxx(body)` 메서드로, 해독은 switch 앞에서 한 번 |
| 같은 파일 24-163줄 | 같은 번호가 방향만 달리 이름 여러 개(0x11 Turn/Turned …) | `ClientOpcode` · `ServerOpcode` 두 정적 클래스 |
| `World/Companion.cs` (664줄) | 패킷 해석·기록 타입·주문표·판단이 한 파일 | 패킷은 Protocol 쪽, `CompanionSpells`·`CompanionBrain` 각자 파일. `Next`(112줄)는 `AutoHunt.Next` 처럼 `Emergency() ?? Recover() ?? Maintain() ?? Follow()` |
| `World/AutoHunt.cs` `Fight`(100줄) | 대상 바꾸기·공격·다가가기 | `Strike(prey)` · `Approach(prey)` |
| 거리·체력%·`Never` | `AutoHunt`·`Companion`·`CompanionRunner` 세 곳에 따로 | 한 곳에 |
| `World/WorldEntry.cs` (426줄) | 기록 타입 30여 개 | 맵·캐릭터·수치·아이템·기술·대화 단위 파일 |
| `World/` 폴더 (40개) | 해석·화면 규칙·자동화·저장이 섞임 | `Protocol/World/` · `Ui/`(`LongPress` `Paging` `AbilityFan` `GearLayout` `OneWindow` …) · `Automation/`(`AutoHunt` `AutoPotion` `AutoLootGate` `CompanionBrain` `BotKit` `StatPlan`) · `Model/`. 네임스페이스가 바뀌니 Godot `using` 과 한 번에 |
| `Art/KeyboardFit.cs` `MessageToastLayout.cs` `SideColumn.cs` | 그림이 아니라 배치 규칙 | `Ui/` |

## 화면 `mobile/client/src`

| 파일 | 지금 | 나누는 경계 |
|---|---|---|
| `GameScreen.cs` (2578줄) | `_Process` 283줄에 위 판·월드맵 판단·시험 코드·가방 배치. 필드 110개 | `HudTopBar`(위 판) · `ControlCluster`(아래 조작) · `WindowHost`(`SetWindow` `Cover` 164줄) · `PartyHud` · `NoticeRouter`(`Route` `Listen`) · `GameRehearsal`(`Rehearse*`·`GREYBOX_` 출력·`LayoutCheck.Pretend*`) |
| `WorldView.cs` (2143줄) | 바닥·캐릭터·이펙트·사냥 판단이 섞임, 셰이더 155줄이 문자열 | `FloorLayer`(셰이더는 `.gdshader` 로) · `ActorRoster` · `EffectPlayer` · `HuntDriver` |
| `WorldView.cs` `HuntOnItsOwn` vs `AutoHuntTick` | **사냥 판단이 두 벌** — 옛 `--hunt` 봇은 화면에서 직접, 새 것은 알맹이 `AutoHunt` | 옛 봇을 `AutoHunt` 로 합치거나 지운다(지울 때는 사용자에게 먼저) |
| `CreateScreen.cs` (1096줄) | 세로·가로 폼이 거의 같은 조립을 반복 | `LookPicker`(머리·색 격자) · `CharacterPreview`. `StarterArmor` 표는 알맹이 자료로 |
| `Main.cs` (1074줄) | 실행 인자 45개·`user://*.cfg` 읽기쓰기 6벌·글꼴·화면 전환 | `LaunchFlags`(+`Has("--x")`) · `DeviceSettings`(공용 `ReadLines`/`WriteLines`) · `ScreenRouter` |
| `PackPanel.cs` `BuildAction`(135줄)·`Fill`(96줄) | 동작 줄+버릴 수량, 칸 만들기 | `PackActionRow` · `Cell(item)` |
| `AbilityBar.cs` | `_drawn[i] switch { LearnedSkill => Slot … }` 네 번 반복, 시험 코드 섞임 | `SlotOf(object?)` 하나 · 시험은 `GameRehearsal` |
| 게임 규칙이 화면에 있는 곳 | 월드맵 다시 띄우기(`GameScreen` 76-82·1324-1353) · 0.2초 돌기/걷기 · 만·억 표기 `GoldText` | 알맹이(`WorldMapGate` · `GoldFormat`)로 옮겨 시험 가능하게 |
| `Flash.cs` | 이펙트 첫 사용 때 메인 스레드에서 `GetPixel` 전체 훑기(끊김) | 바닥 줄·색을 생성기가 `effects.txt` 에 미리 |
| `src/` 평평한 48개 | 화면·위젯·시험 도구가 섞임 | `App/` `Screens/` `World/` `Hud/` `Windows/` `Widgets/` `Diagnostics/`(`LayoutCheck` `Screenshot` `GameRehearsal`) |
| `WindowFrame.cs` · `PartyColumn.cs` | 파일 하나에 타입 여럿 | `Glyph.cs` · `GridTile.cs` |
| 쓰이지 않음 | `PercentWheel.cs` 전체 · `WindowFrame.cs` `DiamondButton` | 지울지 사용자에게 |

## 서버 포크 `sources/wren11/Dark-Ages-Private-Server` (우리 코드만)

| 파일 | 지금 | 나누는 경계 |
|---|---|---|
| `Types/Companions.cs` (880줄) | 짝 맺기·돌려보내기·레벨 맞춤·옷·주기·깨우기·알림, 정적 사전 6개 | `CompanionPairing` · `CompanionKit` · `CompanionStatus`, 사전은 상태 클래스 하나로 |
| `GameServerHandlers.cs` `FormatF2Handler`(100줄) | 검증·사기·팔기·메뉴 | `HandleBuy` · `HandleSell`. `Value / 1.6` 6곳 → `ShopPricing.Offer(item)` |
| `Network/ClientFormats/Undefined.cs:766-805` | 쓰는 패킷 `ClientFormatF2` 가 빈 껍데기 파일에 | `ClientFormatF2.cs` 로(`BulkTradeLine` 같이) |
| `Types/AbilityPresentationOverrides.cs` | 설정 로더가 `Types/` 에. 깨지면 로그 없이 기본값 | `Infrastructure/` 로, catch 에 경고 한 줄 |

## 밸런스 수치 — 코드에 박힌 숫자

| 어디 | 지금 | 방향 |
|---|---|---|
| 서버 기술 스크립트 `database/server/scripts/Skills/Monk/*.cs` (26개, 기술마다 파일 하나) | 위력·고정 피해가 호출 인자로 박혀 있다 — `MonkStrike.Use(sprite, Skill, 350, 5900, 0x85)`. 쿨다운·소리·이펙트는 이미 템플릿 JSON(`templates/skills/*.json`) | 위력·고정 피해를 기술 템플릿 JSON 필드로 옮기고 스크립트는 읽기만. 그러면 한 표(운영 대시보드)에서 조절 |
| `MonkStrike.cs:72` 등 | `damage / 4 * 3` 같은 이름 없는 식 | 이름 붙은 상수로(무엇을 줄이는지 주석) |
| 알맹이 `StatPlan` · 걷기 0.44초 · 돌기 0.2초 등 | 클라이언트 상수 | 지금은 상수로 두되 한 파일(`Tuning`)에 모은다 |

## 생성기 `scripts/`

| 무엇 | 지금 | 방향 |
|---|---|---|
| 공용 코드 | `ROOT` 78곳, `read` 18곳, `.tools/dotnet-9.0.317` 9곳, `run()` 5곳, git 포인터 4곳, `ensure_graphify_python` 3곳 복사 | `scripts/lib/`(밑줄 이름 모듈) — 고치는 스크립트부터 옮겨 쓴다. `importlib` 우회도 이걸로 없어진다 |
| `drops_of` | 4개 파일, 걸러내는 조건이 서로 다름 | 한 함수로, 조건을 인자로 |
| 큰 함수 | 100줄 넘는 것 17개(`build-server-pack-vault.py` `build` 284줄 …) | 읽기 / 계산 / 쓰기로 |
| 폴더 | 103개 평평 | `lib/` `ops/` 부터(가리키는 곳 166개라 `gen/<도메인>/` 은 나중) |

## 작은 것(고치는 김에)
- 엉뚱한 멤버에 붙은 설명: `WorldClient.cs` 366·514·1105·1788-1805·1995·2098 근처, `WorldEntry.cs:112`, `Wardrobe.cs:8`, `GameScreen.cs:1002`, `WorldView.cs:700`, 서버 `Companions.cs:340`(+"1초마다"→0.5초).
- 낡은 주석: `GameScreen.cs:184,187,776`([종료] 위치) · `Main.cs:472,482`(「길」 단추) · `WorldView.cs:10-12`(그림 출처) · `WorldClient.cs:1796-1800`(바닥 물건 13바이트).
- 봇에서 끝없이 쌓이는 큐: `WorldClient` 의 `_hurts` `_motions` `_effects` `_sounds` `_figures` `_songs` 는 봇이 꺼내지 않는다 — `_told` 처럼 상한. 맵이 바뀔 때 `_health` `_struck` 도 비운다.
- 레벨업 점수: 서버 갱신이 오기 전 같은 점수에 요청이 여러 번 갈 수 있다(서버가 남은 점수로 막는지 확인).
