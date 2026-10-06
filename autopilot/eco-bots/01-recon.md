# Recon — 생태계 봇
버전: v1.0

## 1. 업계는 어떻게 하나 (출처)

| 사례 | 구조 | 규모·비용 손잡이 | 출처 |
|---|---|---|---|
| AzerothCore **mod-playerbots** | 서버 **안** 가짜 세션 — `WorldSession(…, socket=nullptr)` 로 Player 를 직접 구동 | 작성자 측정 5000봇 / 6코어 / 20GB. `BotActiveAlone=10`(사람 근처 없는 봇은 10%만 활동) · `botActiveAloneSmartScale`(서버 틱 50~200ms 보고 활동 봇 자동 감축) · `DisabledWithoutRealPlayer`(사람 없으면 봇 로그아웃) · `RandomBotsPerInterval=60`/20초 · 접속 600~28800초 무작위 순환 · 레벨 맞는 사냥터로 **순간이동**(1~5시간) | https://github.com/mod-playerbots/mod-playerbots/wiki/Playerbot-Configuration · https://raw.githubusercontent.com/mod-playerbots/mod-playerbots/master/conf/playerbots.conf.dist · https://raw.githubusercontent.com/mod-playerbots/mod-playerbots/master/src/Bot/PlayerbotMgr.cpp |
| cmangos playerbots(ike3) | 같은 구조(소켓 없음) | 기본 200~300봇, "코어에 무거운 부하" | https://talk.trinitycore.org/t/mangos-bot-former-playerbot-ai/29731 |
| Metaplay **BotClient** | **외부 헤드리스 클라이언트**(부하 시험·경제 시뮬) | 가벼운 게임 CPU 1개당 ~1000 동시 접속 기대 | https://docs.metaplay.io/feature-cookbooks/automated-testing/botclient-testing |
| OpenKore(라그나로크) | 외부 클라이언트, 프로토콜 직접 구현 | 문서 수치 없음 | https://github.com/OpenKore/openkore |
| 제2의 나라 등 국내 모바일 | 비접속 유저 캐릭터를 서버 AI 가 대신 움직임(우리 「대신 사냥」과 같은 결) | 구현 비공개 | https://www.dailian.co.kr/news/view/998278/ |
| mod-ahbot | 경매장 전용 봇 — 희귀도별 수량 쿼터로 공급 제한 | — | https://github.com/azerothcore/mod-ah-bot |
| S.T.A.L.K.E.R. A-Life · KCD2 | **시뮬레이션 LOD** — 사람 근처만 상세, 나머지는 통계로 진행 | KCD2 NPC ~2400 | https://gamedeveloper.com/design/a-life-an-insight-into-ambitious-ai · https://schedule.gdconf.com/session/supporting-thousands-of-npcs-in-kingdom-come-deliverance-kingdom-come-deliverance-ii/915120 |

**경제(파우셋·싱크)**: 드랍·금화 = 파우셋, 상점 구매·수리 = 싱크. 하드 싱크 없으면 인플레이션. NPC 상점가가 가격 상·하한 역할(https://www.gamedeveloper.com/design/the-f-words-of-mmos-faucets). RuneScape 는 봇이 자원을 풀어 시세를 깎음 → 거래세 싱크(https://runescape.wiki/w/Update:Blog_-_An_outline_of_the_impact_of_RuneScape_Bonds). EVE 는 파우셋·싱크를 월간 보고서로 공개 추적(https://www.eveonline.com/news/view/monthly-economic-report-april-2026).

**고지**: 사람인 척하는 봇은 신뢰 문제(AviaGames 소송 https://jipel.law.nyu.edu/angry-gamers-developer-caught-using-bots-in-disguise-of-humans/). 공개·표시된 봇이 안전.

검증 못 한 것: 봇당 CPU·RAM 공식 수치(5000봇 사례 하나뿐), mod-playerbots 레벨업 방식 원문, KCD2 세션 본문.

## 2. 현재 시스템 감사 (읽은 것만)

- **서버 게임 루프는 한 줄기** — `GameServer.UpdateServer` 가 8ms 마다 `UpdateClients` → 컴포넌트 → 모든 맵 `Update` (`src/Hades.Server.Base/Network/Game/GameServer.cs:165-183`).
- **접속자마다 매 틱 시야 갱신** — `ObjectComponent.UpdateClientObjects` 가 그 맵의 모든 물체를 훑고(`ObjectComponent.cs:134-171`), 그 맵의 다른 사람마다 **전체 접속자 목록을 이름 비교로 다시 훑는다**(`CheckObjectClients`, `ObjectComponent.cs:173-193`, `CurrentCultureIgnoreCase`). 한 맵 사람 수 A, 전체 접속자 C 이면 틱마다 A²×C — **같은 맵에 봇을 몰면 제곱으로 무거워진다**(아래 실측).
- 저장: 접속자마다 `SaveRate` 10초(`LoruleConfig.json:131`) — 캐릭터 JSON 파일.
- 기술은 서버가 레벨대로 자동으로 가르침(`Types/AutoLearn.cs`, 표 `mobile/client/assets/world/auto-learn.txt`). 단 **동료 봇 이름은 건너뜀**(`AutoLearn.cs:24`) — 생태계 봇은 동료 봇 목록(`CompanionBots`)에 넣으면 안 된다.
- 봇 표시: `Companions.IsBot`(`Types/Companions.cs:61`) — 활동 기록 `bot` 칸이 이걸로 나뉜다(`ActivitySession.cs:405`).
- 대리 접속 보호: 대신 사냥 열쇠는 **같은 기계(루프백)에서 온 것만** 받는다(`Types/ProxyHunt.cs:185,311`).
- 알맹이에 이미 있는 것: 자동 사냥 판단 `AutoHunt`(반경·회복·남이 치는 괴물 피함) · 자동 물약 `AutoPotion` · 줍기 `AutoLootGate` · 능력치 `StatPlan`(**무도가만** 계획 있음, `Tuning.cs:22-25`) · 상점 사고팔기 `BulkTradeAsync`/`ShopMenuAsync`(0xF2) · 입기/벗기 `UseAsync`/`TakeOffAsync` · 맵 안 길찾기 `TabMap.WayToAny` · 맵 출구 `MapGuide.ExitsOn` · 사냥터 표 `guide.txt` 의 `zone`(맵·입장 레벨).
- **맵 사이 길찾기는 없다** — 출구 표(guide.txt `exit` 1124줄)는 있으나 맵을 잇는 경로 계산이 없음.
- 한 프로그램 여러 접속은 이미 운영 중: `ProxyHost`(동시 `Max` 10, `mobile/bots/Lod.HuntProxy/Program.cs`).
- 활동 기록(머신러닝 재료 후보): 서버 `activity/YYYY-MM-DD.jsonl`, 90일 지우기, **IP 포함**, 수치가 `detail` 한국어 문장 안에 섞여 있음(`ActivitySession.cs:395-430`).
- 경험치 표: 99까지 누적 **238,651,254**(41까지 13,681,980 · 71까지 73,613,939) — `Types/ExperienceCurve.cs`.
- 사냥터 사다리(guide.txt `zone`): 우드랜드 1~20존(입장 1) · 서/북 우드랜드 · 포테의숲 21 · 아벨해안 51 · 뤼케시온해안 71 · 구광산·죽음의마을 99. 마을: 노비스·밀레스·아벨·수오미·마인.

## 3. 실측 — 이 PC(12스레드, Windows)에서 격리 서버

도구: `tests/hades-characterization/BotLoadTests.cs`(`LOD_BOT_LOAD=…`, `LOD_BOT_LOAD_MAPS=…`). 봇 N 개를 **한 프로그램**에서 접속시켜 대신 사냥과 같은 판단(`HuntProxyRunner`)으로 사냥, 30초 데운 뒤 60초 동안 서버 프로세스 CPU 시간을 잼. 봇은 레벨 30·체력 10만(죽어서 사망 맵에 모이지 않게).

**한 맵에 몰았을 때(최악)** — 첫 판은 봇이 죽어 사망 맵 한 곳에 모인 상태로 잼(그래도 "한 맵 N명" 비용):

| 봇 | 서버 CPU(봇 없을 때) | 서버 메모리 | 봇 프로그램 CPU | 비고 |
|---|---|---|---|---|
| 25 | 0.90코어 (0.40) | 375MB | 0.22 | |
| 50 | 2.87코어 (0.38) | 437MB | 0.30 | 25→50 에 늘어난 몫이 5배 — 제곱 |
| 100 | 1.81코어 (0.42) | 526MB | 0.19 | **4개 끊김, 경험치 0 — 루프 포화** |

**`CheckObjectClients` 고친 뒤(FR-010, 2026-10-06)** — 같은 빌드에서 고치기 전·후, 한 맵(우드랜드1-1)에 죽지 않는 봇:

| 봇 | 고치기 전 | 고친 뒤 |
|---|---|---|
| 25 | 1.01코어 | 0.66코어 |
| 50 | 2.18코어 | 0.74코어 |

**사냥터에 흩었을 때(운영 형태)** — 우드랜드·서/북 우드랜드 55맵:

| 봇 | 맵 | 서버 CPU(봇 없을 때) | 서버 메모리 | 봇 프로그램 CPU | 봇당 경험치/분 | 끊김 |
|---|---|---|---|---|---|---|
| 55 | 44 | 1.09코어 (0.36) | 395MB | 0.14 | 103,868 | 0 |
| 110 | 45 | 1.69코어 (0.36) | 441MB | 0.28 | 84,585 | 0 |
| 220 | 50 | 3.05코어 (0.35) | 532MB | 0.51 | 69,201 | 0 |

- **봇 하나 = 서버 약 0.012코어(이 PC) · 메모리 약 0.6MB · 봇 프로그램 약 0.0023코어.** 흩어 놓으면 봇 수에 **직선**으로 는다(55→220 에서 봇당 몫 0.0133→0.0123).
- 봇 없이도 서버가 0.35~0.45코어를 쓴다 — 8ms 마다 모든 맵을 도는 루프의 바닥 비용.
- 높은 존(우드랜드 16~20)에서는 체력 10만이어도 봇 일부가 죽었다(220 중 30) — 레벨 맞는 사냥터 고르기가 필수.
- 맵당 봇이 늘면 봇당 경험치가 준다(괴물 나눠 먹기) — 55맵에 220이면 맵당 4, 봇당 −33%.
- 한계: 측정 봇은 체력 10만·힘 100으로 사람보다 빨리 잡는다(행동이 많다 = 비용을 넉넉하게 잰 쪽). 서버 루프 지연(틱 시간)은 재지 못했다 — CPU 와 끊김만.

## 4. 결론 (질문에 대한 답)

1. **봇마다 프로그램을 하나씩 둘 필요 없다.** (동료 봇 프로세스 하나가 메모리 70MB — 50개를 따로 띄우면 3.5GB) 이 PC 에서 프로그램 하나가 봇 220개를 0.51코어로 돌렸다(봇당 0.0023코어). 지금 동료 봇의 「봇 하나 = 서비스 하나(lod-bot@N)」는 5개라 괜찮았던 것이고, 생태계 봇은 대신 사냥(`ProxyHost`)처럼 **프로그램 하나 · 서비스 하나 · 접속 여럿**으로 간다.
2. **서버 안에 넣는(업계 playerbots 식) 이득은 작다.** 비용의 대부분이 소켓이 아니라 서버의 접속자별 시야 갱신(01 §2)이라, 가짜 세션으로 바꿔도 이 몫은 그대로다. 봇 프로그램 몫(봇당 0.0023)만 아끼는 대신 서버 코어를 크게 고쳐야 하고, 봇 판단의 예외가 서버를 죽일 수 있다. → 외부 클라이언트 유지(04 대안 비교).
3. **클라우드에서 몇 개?** SSH 로 확인하니 오라클 VM 은 **x86_64 AMD EPYC 2 vCPU · 메모리 11GB**(운영 스크립트 주석의 「무료 ARM」은 틀림). 봇 없이(동료 봇 5만) 서버가 0.48코어 —
   이 PC 바닥(0.35~0.45)과 비슷해 코어 하나 성능도 비슷하다(×1.2로 봄). 메모리는 넉넉하다(봇 200 이어도 서버 +0.2GB). 막는 것은 CPU:
   서버에 1.2코어까지(나머지는 사람·OS·동료 봇·대리) → (1.2 − 0.48) ÷ (0.0123 × 1.2) ≈ **50개.** 시작 30, 상한 50. `CheckObjectClients` 고침(아래 5)으로 같은 맵에 몇 개 겹쳐도 덜 무겁다.
4. **업계와 같은 손잡이 세 개를 가져온다**: 한 맵 봇 상한(몰리면 제곱), 서버가 느려지면 봇을 스스로 줄이기(smartScale), 레벨 맞는 사냥터로 순간이동(playerbots 식, 맵 사이 걷기는 2단계).
5. **서버 병목 하나를 고치면 여유가 커진다**: `CheckObjectClients` 의 「같은 맵 사람마다 전체 접속자 이름 비교」를 틱마다 한 번 만든 이름 집합으로 바꾸면 A²×C 가 A² 로 준다(사람에게도 이득). 고친 뒤 같은 시험으로 다시 잰다(03 FR-010).
