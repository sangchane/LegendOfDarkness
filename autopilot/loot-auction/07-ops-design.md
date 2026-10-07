# 배포·운영 설계 — 그룹 전리품 룰렛 · 경매장
버전: v1.0 · 기준 03 v1.0

## 배포
- 런타임: 지금과 같다 — 클라우드 x86 2 vCPU(`161.33.43.117`), 서버 Hades(systemd), 생태계 봇 lod-eco, 앱 iOS.
- **브랜치**: 루트·서버 `feature/loot-roll-auction`. 맥 격리 서버 시험이 모두 통과하면 이 브랜치에서 클라우드로 올린다(`LOD_CLOUD_IP=161.33.43.117 scripts/ops/cloud-server.sh deploy` — 접속자 끊김, 직전에 알림 → `… eco` → 앱은 `scripts/ops/ios-build.sh install`).
  main 에 합치는 것은 클라우드에서 SC-006 을 본 뒤 사용자 확인으로.
- 단계: 알맹이 단위 시험 → 서버 빌드(`Lorule.GameServer` 프로젝트 — 솔루션 빌드는 윈도우 도구 때문에 실패, 격리 시험은 `Staging/net9.0` 을 쓴다) → 격리 통합 시험(06) → 앱 빌드·사진 → 클라우드 배포 → 사건 기록 확인.
- 설정: `GroupLootRoll`·`AuctionEnabled` 를 `scripts/ops/server-config/LoruleConfig.template.json` 과 `src/Lorule.Config/LoruleConfig.json` 에 둘 다(기본 true). 비밀값 없음.

## 관측성
- SLI: 정확성(INV-2 위반 0) · 경매 조작 처리 시간 · 경매 파일 쓰기 시간 · 활성 올림 수(사람/봇).
- SLO: INV 위반 0건/일 · 경매 조작 서버 처리 p95 < 50ms(1천 줄).
- 골든 시그널: Latency = 조작마다 처리 ms 를 사건 기록 `data.ms` 에 · Traffic = 조작 수/시 · Errors = 거절 문구별 수 · Saturation = auction.json 크기·쓰기 ms.
- 로깅: `{StoragePath}/auction/events-YYYY-MM-DD.jsonl`(UTC 날짜별 파일, 30일 보관 — 생태계 봇 사건 기록과 같은 정리), 개인정보 없음(캐릭터 이름뿐).
- 보고: `scripts/ops/auction-report.py`(맥에서 `cloud-server.sh` 로 받은 기록을 읽어 SC-006 숫자·INV-2 검사) — `eco-logs` 옆에 `auction-logs` 명령.

## 알림
1인 운영이라 호출 알람 없음 — 운영자가 배포 뒤·하루 한 번 보고를 본다.
| 조건 | 심각도 | 수신자 | 런북 |
|---|---|---|---|
| 보고에서 INV-2 위반 ≥ 1 | 높음 | 운영자 | R1 금화 어긋남 |
| 서버 로그 "auction save failed" | 높음 | 운영자 | R2 저장 실패 |
| 봇 활성 올림 > ECO_AUCTION_MAX | 보통 | 운영자 | R3 봇 폭주 |

## 장애·복구
| 장애 | 감지 | 영향 | 복구 | RTO/RPO |
|---|---|---|---|---|
| 경매 조작 중 서버가 꺼짐 | 사건 기록 마지막 줄이 짝 없는 조작(예: buyout 뒤 저장 줄 없음) | 한 조작의 물건·금화 유실(복제는 없음, INV-3) | R1: 서버 멈춤 → 마지막 줄의 who·item·gold 로 받을 것에 한 줄 넣기(`auction-revert.py --give 이름 --gold N` 또는 물건 JSON) → 켬 | 30분 / 조작 1건 |
| auction.json 깨짐 | 서버 시작 로그 "auction load failed" — 서버는 경매만 닫고(AuctionEnabled 처럼) 게임은 뜬다 | 경매 못 씀 | R2: `auction.json.bak`(쓸 때마다 직전 본을 남김)으로 바꾸고 그 뒤 사건 기록 다시 보기 | 30분 / 마지막 조작 |
| 봇이 시장을 채움 | 보고의 봇 활성 올림 | 사람 물건이 묻힘 | R3: `Tuning.EcoAuctionMax` 낮추고 `… eco` | 10분 |
| 기능 전체 되돌리기 | 사용자 결정 | — | R4: 설정 두 개 false → 서버 재시작 → 접속자에게 받을 것 받으라고 알림 → 서버 멈춤 → `auction-revert.py`(남은 올린 것·받을 것을 주인 은행으로, `auction.json` 은 `.reverted` 로 이름만 바꿈) → 브랜치 되돌린 서버 배포 | 1시간 / 0 |

- 백업: `auction/` 폴더를 캐릭터 폴더와 같은 주기로(`cloud-server.sh backup` 에 포함 — BUILD 첫 작업에서 확인·추가), 쓸 때마다 `.bak` 1개. 복원 리허설: SC-004·SC-007 시험이 곧 리허설(배포 전마다).
- 런북 골격(R1~R4 공통): 보고 줄 → 영향 → 진단(`ssh … tail auction/events-*.jsonl`) → 해결(위 표) → 검증(`auction-report.py` 위반 0) → 롤백(R4).

## 착수 자산
- 디렉터리:
  - `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base/Types/` — `GroupLoot.cs`·`AuctionHouse.cs`(새)
  - `…/Network/ClientFormats/ClientFormatF4.cs`(새) · `…/ServerFormats/ServerFormat5E.cs`(종류 7·8·9)
  - `mobile/src/Lod.Mobile.Core/Protocol/World/WorldClient.Auction.cs`(새) · `Automation/EcoPlan.cs`(올릴 것·살 것)
  - `mobile/client/src/Windows/AuctionPanel.cs`·`RollBanner.cs`(새)
  - `mobile/bots/Lod.EcoBots/EcoRunner.cs`(Shop)
  - `scripts/ops/auction-revert.py`·`auction-report.py`(새)
  - `tests/hades-characterization/GroupLootTests.cs`·`AuctionTests.cs`(새)
- `.env.example`: 해당 없음 — 새 비밀값 없음, 설정은 LoruleConfig 두 칸.
- 첫 작업 3개(워킹 스켈레톤):
  1. **경매 한 바퀴(서버만)**: `AuctionHouse` 저장·불러오기 + P-04 올림 · P-06 즉시 구매 · P-08 받기 + 사건 기록 → 격리 통합 시험 SC-003 의 즉시 구매 경로(INV-1·2). 위험(복제)이 가장 커서 먼저.
  2. **룰렛(서버)**: `GroupLoot.Share`·`ShareGold` + E-01 + 보호 결함 고침 → SC-001·SC-002.
  3. **앱 메뉴 한 줄**: 「경매장」 단추 → 찾기 탭(P-01 → E-02) 목록 표시 → 사진 1장. 그 뒤 입찰·취소·만료·봇·되돌리기.
