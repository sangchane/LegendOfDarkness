# SPEC: 생태계 봇 (구현)
입력: `03-prd.md` v1.1 · `05-api-contract.md` · `08-readiness-report.md`. 결정은 `decision-log.md`.

## 배경
서버 쪽 AI 캐릭터(전사·무도가·도적)가 사람과 같은 규칙으로 1→99 성장하며 세상·경제를 채운다. 실측상 외부 헤드리스 클라이언트, 프로그램 하나에 봇 여럿(01 §4).

## 현재 상태
- 0xF1 확장 종류 0~7: `ClientFormatF1.cs`, 처리 `GameServerHandlers.cs:2025-2069`.
- 로그인: `LoginServer.Format03Handler`(`LoginServer.cs:155`), 만들기 `Format02Handler`(:109). 루프백 판정 `ProxyHunt.IsLoopback`(private, `ProxyHunt.cs:311`).
- [접속자] 길드명 꼬리 `ServerFormat36.cs:108-110`. 활동 `bot` 칸 `ActivitySession.cs:285,405`.
- 시야 병목 `ObjectComponent.CheckObjectClients`(`ObjectComponent.cs:173-193`).
- 워프 `GameClient.TransitionToMap(int, Position)`(빈 칸 찾기 포함, `GameClient.cs:1287-1333`).
- 상점: 0x43 클릭 → shop1 「삽니다」(0x0001) → 0x2F Goods(`DialogueGoods`: 값·이름·성별·수치 `ItemStats.Level/Class/Place`) · 사고팔기 `BulkTradeAsync`(상인 곁이면 대화 없이도 됨, `GameServerHandlers.cs:2080-2190`).
- 사냥 한 틱 `HuntProxyRunner.Once`(대신 사냥, 그대로 재사용) · 물약 이름표 `AutoPotion.Healing`(작은 것부터) · 능력치 `StatPlan`(앱도 씀, `WorldView.Hunt.cs:325`).
- 죽음 → 뮤레칸의방(20138) 10,9 유령 → 뮤레칸(12,5) 클릭 → 「다음」 → 레벨 맞는 마을(`MurekansRoomTests`).
- 맵 적정 레벨: 괴물 템플릿 `Level` 은 모두 1이라 못 씀 → 템플릿 `Exp` 로 「50마리에 한 레벨」(사용자 우드랜드 기준)인 레벨을 셈.

## 제안 변경
**서버(fork)**
1. `ServerConstants.EcoBots`(이름 목록, 기본 빈 목록). `Types/EcoBots.cs`: `IsEcoBot(name)`(목록에 있고 동료 봇이 아닌 이름) · `Move(client, map, x, y)`(봇 ∧ 루프백일 때만 `TransitionToMap`).
2. `ClientFormatF1.EcoMove = 8` — 몸 `[8, map u16, x u8, y u8]`. 처리기 switch 에 더함(죽은 몸은 기존 검사로 막힘).
3. 로그인·만들기: `IsEcoBot` 이름이 루프백이 아니면 거절(로그인 = 「비밀번호가 틀렸습니다」 + 실패 기록 `eco_remote`, 만들기 = 「이미 등록된 계정입니다」). `ProxyHunt.IsLoopback` → internal 로 같이 씀.
4. [접속자] 길드명 꼬리: 생태계 봇이면 `AI`. 활동 기록 `bot` 칸에 생태계 봇 포함.
5. `CheckObjectClients`: 틱마다 접속 중 이름 집합(`HashSet`, 대소문자 무시) 한 번 만들어 씀(FR-010).
6. 활동 줄에 숫자 칸 `map,x,y,level,expTotal,goldNow`(FR-013).
7. 내구도 끔 — 끝남(`Sprite.ApplyDamage`, FR-018).

**알맹이(`mobile/src/Lod.Mobile.Core/Automation`)**
8. `StatPlan`: 차례형 표 `Tuning.StatOrders` — 전사 [Con 64, Str 255] · 도적 [Wis 23, Con 41, Str 78, Dex 49, Int 20]. 차례형이 있으면 앞에서부터 목표 못 미친 첫 능력치. 무도가는 기존 「가장 먼 것」.
9. `EcoGrounds`: `eco-grounds.txt`(맵·적정 레벨·이름) 읽기 + `Pick(level, botsOn, avoid)` → 적정 레벨 ≤ 내 레벨 중 가장 높은 것, 봇 수 < `BotsPerMap`, `avoid` 제외. 없으면 그 아래 층.
10. `EcoShopping`(순수): `Sell(pack, keep)` — 물약·지금 쓰는 것 빼고 판다 · `Potions(goods, gold, have)` — `AutoPotion.Healing` 중 레벨 제한 ≤ 내 레벨인 가장 센 것을 `PotionStock` 까지(살 수 있는 만큼) · `Gear(goods, worn, path, level, gender, budget)` — 부위(`Place`)마다 직업(`Class` 0 또는 내 직업)·성별(255 또는 내 성별)·레벨 ≤ 내 레벨·값 ≤ 예산, 점수(무기 `DmgMax+Dmg+Hit`, 그 밖 `-Ac + Str+Con+Dex + Hp/10`)가 지금 것보다 클 때만, 부위당 하나.
11. `EcoLife`(순수 상태기계): `Hunt → (물약 < PotionLow ∨ 빈칸 < BagLow ∨ TownEvery 지남 ∨ 사냥 멈춤) → Town(가게 차례대로) → Hunt`, 혼수 → 죽음 기다림 → `Revive` → Town. 사람이 보이면 `YieldSeconds` 뒤 다른 사냥터. 죽음 `DeathLoop` 번 연속이면 한 층 아래.
12. `EcoLog`(사건 한 줄 JSON, 05 E7) — 순수 직렬화 + 파일 쓰기는 봇 프로그램.

**봇 프로그램 `mobile/bots/Lod.EcoBots`(새)** — `Lod.CompanionBot`·`Lod.HuntProxy` 를 참조(MapWalls·BotLogin.Describe·HuntProxyRunner 재사용).
13. `Program`(설정 `eco-bots.json`: Host·LoginPort·MapFolder·Password·Bots[{Name,Path}]·MaxOnline·LogFolder) · `EcoHost`(봇마다 Task, 2초 간격으로 들어감, 끊기면 5초~1분 재접속, 5분 요약) · `EcoRunner`(보기 → `EcoLife` → 이동 0xF1 8 / `HuntProxyRunner.Once` / 가게: 상인 칸으로 이동 → 0x43 → 「삽니다」 → Goods → `BulkTrade` 팔기·사기 → `Use` 입기).
14. 처음 접속에 계정이 없으면 `CreateCharacterAsync(path)`.

**자료·도구**
15. `scripts/gen/eco/build-eco-grounds.py` → `mobile/client/assets/world/eco-grounds.txt`(guide.txt `zone` 맵 ∩ 괴물 있는 맵, 적정 레벨 = Table[L+1] ≤ 50×괴물 Exp 중앙값인 가장 큰 L).
16. `scripts/ml/export-activity.py`(활동 → 가명 사본, 05 E8) + `tests/test_ml_export.py`(저장소 파이썬 시험 자리).
17. `scripts/ops/cloud-server.sh`: `eco`·`eco-config`·`eco-logs`, `deploy` 에 포함, systemd `lod-eco`, cron 내보내기.

## 완료 기준
- [x] FR-018 내구도 끔 — `Pack599ArmorTests` 5 통과(바꾼 시험은 고치기 전 실패 확인).
- [x] FR-007 — 알맹이 `StatPlanTests`(전사·도적·무도가).
- [x] FR-002·004·006·008·012 — 알맹이 `EcoGroundsTests`·`EcoShoppingTests`·`EcoLifeTests`·`EcoLogTests`.
- [x] FR-005·009 — 격리 서버 `EcoBotServerTests`: 봇 0xF1 8 → 그 맵에 섬 · 사람 0xF1 8 → 그대로 · 봇 이름 밖 주소 로그인 거절 · [접속자] 봇 길드명 AI.
- [x] SC-001 — 격리 서버 `EcoBotLoopTests`: 봇 3개(전사·무도가·도적) 새로 만들어 move(사냥터)→sell→buy→move(사냥터) 사건이 각각 나온다(물약 0개로 시작해 바로 마을부터).
- [x] SC-002 — 같은 시험 안에서 레벨 11 봇이 장비 1부위 이상 equip.
- [x] SC-004 — (25봇 1.01→0.66, 50봇 2.18→0.74) `BotLoadTests` 한 맵 25봇 CPU 고친 뒤 < 0.90코어.
- [x] FR-013·014·SC-005 — 활동 줄 숫자 칸 시험 + `python3 -m unittest tests/test_ml_export.py`.
- [ ] 회귀 — 알맹이 전체 · 관련 격리 서버 시험(로그인·접속자·대신 사냥·동료 봇·활동 기록).

## 테스트 계획
알맹이 순수 함수는 단위 시험(엔진 없음). 서버 권한(E1·E3)은 격리 서버로 사람·밖 주소 거절까지. 한 바퀴는 격리 서버 E2E 1개(30분 상한, 보통 수 분). 부하는 `BotLoadTests`.

## 검증 방법
`dotnet test mobile/tests/Lod.Mobile.Core.Tests` · `dotnet test tests/hades-characterization --filter "EcoBot|StatPlan|Pack599Armor|UserList|Activity|ProxyHunt|Companion|LoginFlow"` · `python3 -m unittest tests/test_ml_export.py`.

## 롤백 계획
서버: `EcoBots` 를 빈 목록으로 두면 새 기능이 꺼진다(0xF1 8 은 아무도 못 씀). 봇: `systemctl stop lod-eco`. 내구도·병목 고침은 fork 커밋 되돌림.

## 구현하며 바뀐 것
- 봇은 접속 뒤 내 프로필(0x2D)을 물어야 직업(Path)을 안다 — 안 물으면 능력치·직업 장비가 안 됐다(격리 서버).
- 순간이동 직후 상인·괴물이 늦게 오거나 안 온다(새 맵 0x07 이 0x15 보다 먼저 와 알맹이가 비움) — 이동 뒤 새로고침(0x38), 상인은 2초마다 새로고침하며 12초까지.
- 사냥터 적정 레벨 = max(경험치 50마리 레벨, 입장 레벨, 가장 센 괴물 8대를 버티는 레벨) + 노비스 사냥터 — 경험치만 보면 서의우드랜드1-1(공격 55~60 말벌)에서 2레벨 봇이 죽었다.
- 물약 가게 = 체력 물약을 가장 많이 파는 곳(음식점 엑스쿠라눔 하나짜리가 먼저 잡혔다). 쉬기 기준 80 → 50%.

## 안 할 것(이번)
- FR-011 지연 감축 — `LagHigh/LagLow` 미확인(클라우드 실측 뒤). 그때까지는 `MaxOnline` 상한으로만.
- FR-015 대시보드 봇 경제 · FR-016 맵 사이 걷기 · FR-017 통계식 성장(P2).
- 마법사·성직자 생태계 봇(사용자). 마력 물약(세 직업은 평타·기술 위주).
- 상점 「수리합니다」 메뉴 지우기(내구도가 안 닳아 쓸 일이 없을 뿐, 요청 없음).

## 참조 파일
위 「현재 상태」 의 file:line · `mobile/bots/Lod.HuntProxy/HuntProxyRunner.cs` · `tests/hades-characterization/MurekansRoomTests.cs` · `ProxyHuntTests.cs`(격리 서버에서 대리 프로그램 띄우는 법).
