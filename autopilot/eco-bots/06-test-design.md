# 테스트 설계 — 생태계 봇
버전: v1.0 · 기준 03 v1.2

## 수용 기준 → 시나리오
| ID | 시나리오 | 레이어 |
|---|---|---|
| SC-001 | Given 격리 서버·새 봇 3개(전사·무도가·도적) When `EcoHost` 30분 Then 봇마다 사건 기록에 move(사냥터)·sell·buy·move(사냥터 다시)가 순서대로 있다 | 격리 서버 `EcoBotLoopTests` |
| SC-002 | Given 레벨 11·금화 넉넉 봇(직업별) When 마을 쇼핑 Then 서클 ≥2 장비 1부위 이상 equip 사건 + 캐릭터 JSON 에 입혀짐 | 격리 서버 |
| SC-003 | Given `MaxEcoBots`/2 봇, 맵당 ≤ `BotsPerMap` When 60초 Then 서버 CPU ≤ `ServerCoreBudget` | `BotLoadTests` (`LOD_BOT_LOAD`) |
| SC-004 | Given 한 맵 25봇 When FR-010 전·후 Then 후 CPU < 전 CPU | `BotLoadTests` 두 번 |
| SC-005 | Given 하루치 사건 기록·학습용 사본 When 검사 스크립트 Then 모든 줄 JSON 파싱, 사본에 `ip` 키·원래 이름 0건 | Python `scripts/ml/test_export.py` |
| FR-001 | 계정 없는 봇이 처음 접속에 만들어지고 끊기면 다시 들어온다 | 격리 서버 |
| FR-002 | 레벨 1·11·41·71·99 → 고른 사냥터 입장 레벨이 가장 높고 ≤ 레벨, 사람 있는 맵·꽉 찬 맵은 빠진다 | 알맹이 `EcoGroundsTests` |
| FR-003 | 사냥 중 단계가 `AutoHunt` 결과와 같다 | 알맹이 |
| FR-004 | 물약 4·빈칸 4·61분·살 금화 — 각각 GoTown | 알맹이 `EcoLifeTests` |
| FR-005 | 봇이 E1 → 그 맵에 섬 · 사람이 E1 → 그대로 | 격리 서버 `EcoMoveTests` |
| FR-006 | 직업·성별·서클·값 거름, 지금보다 좋을 때만, 물약 예산 남김 | 알맹이 `EcoShoppingTests` |
| FR-007 | 전사 CON 64 먼저·그 뒤 STR, 도적 WIS→CON→STR→DEX→INT 차례, 무도가 그대로 | 알맹이 `StatPlanTests` |
| FR-018 | 내구도 1 도복이 여러 대 맞아도 입혀져 있음 | 격리 서버 `Pack599ArmorTests` |
| FR-008 | 죽은 봇이 뮤레칸으로 살아나 마을에 선다 | 격리 서버 |
| FR-009 | 봇 이름을 밖 주소로 로그인 → 거절 · [접속자] 봇 줄 길드명 AI | 격리 서버(밖 주소는 루프백 아닌 인터페이스가 없으면 단위 시험으로) |
| FR-010 | 같은 맵 사람이 나가면 여전히 서로 지워진다(기존 동작) | 격리 서버 |
| FR-011 | 지연 > LagHigh 이면 접속 수가 준다 | 알맹이(가짜 지연) |
| FR-012 | 사건 종류마다 필수 칸 | 알맹이 `EcoLogTests` |
| FR-013 | 활동 줄에 숫자 칸 6개 | 격리 서버 `ActivityLogTests` 더함 |
| FR-014 | SC-005 와 같음 | Python |

## 계약 시험
E1 몸 길이 5가 아니면 버림 · E2 비어 있으면 생태계 봇 없음 · E7 `v`=1.

## 리스크 기반 커버리지
위험 큰 것: E1·E3(권한) — 사람 계정·밖 주소 거절 시험 필수. 경제 루프(FR-004·006) — 알맹이 순수 시험으로 경계값.
