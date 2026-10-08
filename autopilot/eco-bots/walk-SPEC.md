# 생태계 봇 워프 이어 걷기 — SPEC (M, 결정 18 · ④, 2026-10-08)

## FRAME
- 누구·문제: 생태계 봇은 마을·가게·사냥터·파티장 사이를 봇 전용 순간이동(0xF1 8)으로 옮긴다. 사용자 「순간이동하듯이 하지 말고」 — 사람처럼 걸어서 워프를 밟고 월드맵을 골라 가야 한다.
- 성공: 봇의 `MoveTo` 가 같은 맵이면 걸어서, 다른 맵이면 워프를 이어 걸어서(월드맵 칸은 밟고 고른다) 도착한다. 순간이동은 길이 없거나 막혔을 때만, 그때마다 기록에 남는다.
- 안 할 것: NPC·스크립트로만 가는 곳(죽음의마을 워프 99 등 — 봇 목적지에 없음), 걸음 중 싸우기·물약, 걸은 거리로 길 고르기(워프 수가 적은 길), 앱의 맵 사이 자동 걷기.

## 확인한 것(2026-10-08, 세션 scratchpad `reach.py`)
서버 워프 자료(Map 1,393 · World 23 · 월드맵 카드·구역 134곳)만으로 마을 20041 ↔ 사냥터 130곳 · 가게 맵 모두 오간다. 그 길 위 맵 155장 모두 벽 파일(`map<번호>.txt`)이 있다. 워프 수 평균 5.3, 가장 먼 곳 32.

## 동작
1. **길 자료** `mobile/client/assets/world/links.txt` — 새 생성기 `scripts/gen/eco/build-eco-links.py`(서버 `templates/warps` · `worldmaps`):
   - `link <맵> <x> <y> <갈맵> <갈x> <갈y> <최소레벨> <최대레벨>` — Map 워프 칸 하나마다(최대 0 = 없음).
   - `gate <맵> <x> <y>` — 밟으면 월드맵이 열리는 칸(World 워프).
   - `field <갈맵> <갈x> <갈y> <최소레벨> <최대레벨>` — 월드맵에서 고를 수 있는 곳(카드 도착지 + 구역). 레벨은 서버 `WorldMapRefusal` 과 같게: 그 맵으로 드는 워프 중 가장 엄한 최소·최대, 구역은 카드의 것도 합친다.
   - 봇 프로그램에 함께 올린다(`cloud-server.sh` `bot_upload` 포함 목록).
2. **길찾기**(알맹이 `Automation/EcoRoute.cs`, 순수 함수): `EcoLinks.Read(text)` · `EcoRoute.Plan(links, from, to, level, avoid)` → 첫 걸음 `EcoLeg(Map, Tiles, ToMap, Field)`(Field 0 = 워프, 아니면 월드맵에서 고를 맵). 맵 단위 BFS(워프 수 최소), 레벨이 안 맞는 워프·월드맵 곳은 뺀다, `avoid`(이번 길에서 막힌 칸) 뺀다. 길이 없으면 null.
3. **걷기**(`EcoRunner.MoveTo`): 도착할 때까지 되풀이 —
   - 목표 맵이면 목표 칸 2칸 안으로 걷는다(벽 + 다른 생물 칸은 막힘, `TabMap.WayToAny` · 한 걸음 `Tuning.StepSeconds`).
   - 아니면 `Plan` 의 첫 걸음 칸 중 가까운 곳으로 걸어 밟는다 → 맵이 바뀌면 다음 걸음. 월드맵 칸이면 월드맵이 열린 뒤 `ChooseFieldAsync(Field)`.
   - `Tuning.EcoWalkStuck`(20초) 동안 칸·맵이 안 바뀌면 그 걸음 칸을 `avoid` 에 넣고 다시 길을 찾는다. 길이 없으면 순간이동(지금 방식)으로 넘어간다.
   - 유령이 되거나 죽음의 맵이면 그만둔다(false).
   - 맵이 바뀔 때마다 새로고침(0x38) — 순간이동 때와 같은 서버 경합 대비.
4. **기록**: 다른 맵으로 간 `MoveTo` 하나마다 `walk` 사건 `{from, to, legs, steps, seconds, teleport, why}`.

## 상수
| 이름 | 값 | 근거 |
|---|---|---|
| `EcoWalkStuck` | 20초 | 한 걸음 0.44초 — 45걸음 동안 한 칸도 못 가면 막힌 것 |
| 목표 곁 | 2칸 | 지금 `MoveTo` 의 3칸 안 판정과 상인 곁(`Merchant` 는 상인 칸으로 찾음) |

## 완료 기준(순서대로)
1. 생성기 `--쓰기` 로 `links.txt` 생성, 다시 돌려도 같은 내용 — 검증: `python3 scripts/gen/eco/build-eco-links.py --쓰기 && git diff --stat`
2. 알맹이 `EcoLinks.Read` · `EcoRoute.Plan`: 워프 이어 가기, 월드맵 칸→곳, 레벨 최소·최대로 빼기, avoid, 길 없음 null(시험) — 검증: `dotnet test mobile/tests/Lod.Mobile.Core.Tests`
3. 실제 자료로: 마을 20041 ↔ 사냥터 모두 `Plan` 이 길을 낸다(사냥터 적정 레벨로)(시험)
4. 봇 `MoveTo` 걷기 + 순간이동 대체 + `walk` 기록, 봇 빌드 오류 0 — 검증: `dotnet build mobile/bots/Lod.EcoBots`
5. 격리 서버: 봇 하나가 마을에서 다른 맵 가게·사냥터까지 걸어서 도착(`teleport=false`) — 검증: 맥 서버 + 봇 프로그램 기록
6. 클라우드 봇만 배포 뒤 30분: `walk` 중 `teleport=true` 비율, 서버 CPU(`cloud-server.sh status`), 죽음 수 — 결과를 NEXT 에

## 참고
- 지금 순간이동: `EcoRunner.cs` `MoveTo` (0xF1 8 `WorldClient.Companion.cs:85` `EcoMoveAsync`), 새로고침 까닭 주석 그대로.
- 맵 안 길: `TabMap.WayToAny`·`StepOf`(`TabMap.cs:292`·`350`), 벽 `MapWalls.For`(`Lod.CompanionBot/MapWalls.cs`).
- 자기 칸: 서버는 내 걸음을 되돌려 줄 때만 0x04 를 보낸다 — `HuntProxyRunner.cs:83~86·153` 처럼 `PositionReports` 가 바뀌면 서버 칸, 아니면 내가 센 칸.
- 월드맵: `ChooseFieldAsync`(`WorldClient.Sending.cs:320`), 열림 `Field`·`FieldShown`, 서버 `GameServerHandlers.cs` 0x3F(카드 도착지 또는 구역 번호).
