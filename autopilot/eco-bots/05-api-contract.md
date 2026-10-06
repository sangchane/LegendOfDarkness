# 계약 — 생태계 봇
버전: v1.0 · 기준 03 v1.2

## 규약
HTTP API 없음. 계약은 ① 게임 패킷 확장 ② 서버 설정 키 ③ 봇 프로그램 설정 ④ 기록 형식. 패킷 확장은 기존 0xF1(우리 확장, 첫 바이트 = 종류) 규칙을 따른다. 정수는 큰 끝.

## 패킷·설정 표
| ID | 무엇 | 꼴 | 서버 동작 · 거절 |
|---|---|---|---|
| E1 | 봇 이동 요청 0xF1 종류 8 | `[8, map(2), x(1), y(1)]` | 보낸 이가 `IsEcoBot` ∧ 루프백 접속일 때만, 그 맵이 있으면 (x,y) 또는 가장 가까운 걸을 칸으로 워프. 아니면 조용히 버림(로그 Debug) |
| E2 | 서버 설정 `EcoBots` | `"EcoBots": ["이름", …]` | `IsEcoBot(name)`. `CompanionBots` 와 겹치면 동료 쪽이 이김(생태계 봇으로 안 봄) |
| E3 | 봇 계정 로그인 제한 | — | `IsEcoBot` 이름은 루프백 접속만 로그인·만들기·비밀번호 바꾸기 허락(아니면 틀린 비밀번호·이미 있는 계정과 같은 답) |
| E4 | [접속자] 0x36 길드명 꼬리 | 봇이면 길드명 자리에 `AI` | 앱 변경 없음(이미 길드명을 보여 줌) |
| E5 | 활동 기록 숫자 칸 | 기존 줄에 `map,x,y,level,expTotal,goldNow` 더함(`detail` 은 그대로) | 대시보드 호환 |
| E6 | 봇 프로그램 설정 `eco-bots.json` | `{ Host, LoginPort, MapFolder, Password, Bots:[{Name, Path, Gender}], MaxOnline, LogFile, EventFolder }` — 맵당 봇 수 등 판단 손잡이는 알맹이 `Tuning.Eco*` | 비밀번호는 클라우드 파일에만(권한 600) |
| E7 | 봇 사건 기록 | `eco/YYYY-MM-DD.jsonl`(한국 날짜), 지난 날은 `.jsonl.gz`, `EcoLogKeep` 일 | 아래 형식 |
| E8 | 학습용 내보내기 | `ml/activity/YYYY-MM-DD.jsonl.gz` · `ml/eco/` 는 E7 사본 | 아래 형식 |

## 기록 형식 (E7) — 한 줄 하나
```json
{"v":1,"at":"2026-10-06T12:00:00Z","bot":"이름","cls":1,"lvl":30,"exp":123456,"gold":2500,
 "hp":800,"mhp":1000,"mp":100,"mmp":200,"map":20015,"x":20,"y":31,"state":"hunt",
 "ev":"kill","data":{"monster":"늑대","ms":4200,"potions":12,"bag":40}}
```
| ev | data |
|---|---|
| tick(분마다) | `potions, bag, doing` |
| kill | `gain`(경험치), `ms`(지난 잡음부터) — 괴물 이름은 클라이언트가 모름 |
| death | `ground`(죽은 사냥터 맵) |
| level | `from, to` |
| sell / buy | `items:[{name,qty(,price)}]`, `goldBefore, goldAfter` |
| equip | `slot, name, level`(레벨 제한 = 서클) |
| move | `fromMap, toMap, why` |
| state | `from, to, why` |

## 학습용 사본 형식 (E8)
원본 활동 줄에서 `ip`·`detail`·`meta.install` 뺌, `player`·`meta.counterparty` → `p = hex(HMAC-SHA256(소금, 소문자 이름))[:16]`. 이름 그대로(`bot_name`)는 서버 설정의 봇 이름(EcoBots·CompanionBots)뿐 — `bot:true` 는 대신 사냥 중인 사람에게도 선다. 소금은 클라우드 `~/lod/ml-salt`(권한 600, 저장소 밖).

## 규칙·밸런스 상수 파일
값은 `03-prd.md` 상수 표가 단일 출처. 봇 프로그램 설정(E6)은 그 이름을 그대로 쓴다.

## 데이터 규칙
시각 UTC ISO8601, 파일 날짜는 한국 날짜(기존 활동 기록과 같게). 금화·경험치 정수.

## 커버리지 매핑
| FR | 담당 |
|---|---|
| FR-001 | E2 · E6 · `EcoHost` |
| FR-002 | `EcoGrounds` (guide.txt zone) |
| FR-003 | `EcoRunner` → `AutoHunt`/`AutoPotion`/`AutoLootGate` |
| FR-004 | `EcoLife` 상수 PotionLow·BagLow·TownEvery |
| FR-005 | E1 |
| FR-006 | `EcoShopping` · `BulkTradeAsync` · `UseAsync` |
| FR-007 | `StatPlan` · `Tuning.StatBuilds` 5직업 |
| FR-008 | `EcoLife.Revive` (0x43 뮤레칸 → 다음) |
| FR-009 | E2 · E3 · E4 |
| FR-010 | 서버 `ObjectComponent.CheckObjectClients` |
| FR-011 | `EcoHost` 지연 감축 |
| FR-012 | E7 |
| FR-013 | E5 |
| FR-014 | E8 |
| FR-018 | 서버 `Sprite.ApplyDamage` 내구도 깎기 지움 |
