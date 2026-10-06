# 준비도 판정 — 생태계 봇
버전: v1.0 · 기준 03 v1.2

## 판정: CONCERNS (착수 가능)
lite — 메인 자기 점검 + `check_package.py`(CRITICAL 0 · HIGH 0, FR 17 · SC 5 · register 26행 전부 마킹).

## 자기 점검 결과
| 항목 | 결과 |
|---|---|
| 커버리지 | P0·P1 FR 14개 모두 05 매핑, SC 5개 모두 06 시나리오 |
| 모호성 | 사람 맵 피하기가 "미리 앎"처럼 적혀 있었음 → 봇은 자기 맵만 보므로 「들어가서 보이면 비킴」으로 고침(03 FR-002) |
| 불일치 | 용어 생태계 봇/동료 봇/대신 사냥 구분 유지(02 축 8) |
| 남은 걱정(MEDIUM) | ① 클라우드 코어 성능 `CloudSlowdown` 미확인 — SSH 열쇠 등록 뒤 `BotLoadTests` 를 클라우드에서 돌려 상한 확정. ② `LagHigh/LagLow` 미확인 — 스켈레톤에서 바닥 지연을 잰 뒤. ③ 실측 봇은 체력 10만이라 비용을 넉넉하게 잰 쪽(실제 봇은 덜 바쁨). ④ 99까지 걸리는 시간은 스켈레톤 기록으로 처음 앎 |

## 핸드오프 (dev:build L 경로 — 서버 로그인 인증 경계를 건드림)
```
autopilot/eco-bots/ 설계대로 구현. 입력: 03-prd.md(요구사항·상수 표) + 05-api-contract.md(E1~E8) + 07 의 첫 작업 3개.
선행: (가능하면) 클라우드에서 BotLoadTests 로 MaxEcoBots 확정.
첫 작업: ① 서버 E1·E2·E3 + EcoMoveTests ② Lod.EcoBots 최소판 한 바퀴(이동→사냥→물약 0→마을→물약 사기→돌아감) ③ 사건 기록 tick·move·buy.
보안 리뷰: E1(사람 계정 거절)·E3(밖 주소 거절)·학습용 사본(IP·이름 0건).
<model_hints>서버 권한 검사·상태기계: opus · 알맹이 순수 함수와 시험: sonnet · 설정·스크립트 배선: haiku</model_hints>
```
