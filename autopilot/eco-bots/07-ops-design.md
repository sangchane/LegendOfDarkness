# 운영 설계 — 생태계 봇
버전: v1.0 · 기준 03 v1.2

## 배포
- 런타임: 클라우드 VM, systemd `lod-eco.service` 하나(`/opt/dotnet/dotnet ~/lod-eco/app/Lod.EcoBots.dll ~/lod-eco/eco-bots.json`). 맵 벽·guide.txt 는 동료 봇 `~/lod-bot/world` 를 같이 씀.
- `scripts/ops/cloud-server.sh` 에 `eco`(올리고 재시작) · `eco-config`(설정 파일 — 비밀번호 묻기, 클라우드에만) · `eco-logs` 를 더한다. `deploy` 에 포함.
- 순서: 서버 먼저(E1~E5) → `lod-eco` 를 `StartEcoBots` 개로 켬 → 하루 보고 `MaxEcoBots` 까지.

## 관측성
- 5분 요약 한 줄: 접속 봇 수 · 상태별 수(사냥/마을/죽음/대기) · 평균·최고 레벨 · 지난 5분 금화 들어옴/나감 · 지연.
- 서버 CPU: `cloud-server.sh status` 에 `ps -o %cpu` 한 줄 더함.
- 기록: 프로그램 로그 `~/lod-eco/logs/` 1MB×5 돌려 쓰기(동료 봇과 같음) · 사건 기록 `~/lod-eco/eco/` `EcoLogKeep` 일.

## 알림
| 조건 | 심각도 | 대응 |
|---|---|---|
| 서버 CPU > `ServerCoreBudget` 10분 | 높음 | `eco-config` 에서 `MaxOnline` 낮추고 `eco` |
| 봇 절반 이상 「대기」 30분 | 중간 | `eco-logs` — 사냥터 꽉 참/상점 실패 확인 |
(1인 운영 — 알림은 요약 줄을 볼 때 판단. 자동 알림은 만들지 않음)

## 장애·복구
| 장애 | 감지 | 복구 |
|---|---|---|
| 서버가 느려짐 | 사람이 체감·CPU | `ssh … sudo systemctl stop lod-eco` — 봇만 끊긴다 |
| 봇 판단 버그로 이상 행동 | eco-logs | `systemctl stop lod-eco`, 고쳐서 `cloud-server.sh eco` |
| 봇 캐릭터 망가짐 | 사건 기록 | 매일 4시 aislings 백업(기존 cron)에서 그 파일만 되돌림 |
| 학습용 내보내기 실패 | cron 메일 없음 → 파일 날짜 확인 | 원본 90일 안이면 스크립트 다시 돌림 |

백업: 봇 캐릭터는 기존 aislings 백업에 함께(30벌). 사건 기록·학습용 사본은 `cloud-server.sh ml-pull` 로 맥에 받아 둠(주 1회 권장).

## 착수 자산
```
mobile/bots/Lod.EcoBots/       봇 프로그램(EcoHost·EcoRunner·설정·기록)
mobile/src/Lod.Mobile.Core/Automation/Eco*.cs   순수 판단(EcoLife·EcoShopping·EcoGrounds)
mobile/tests/…/Eco*Tests.cs    알맹이 시험
tests/hades-characterization/Eco*Tests.cs · BotLoadTests.cs   격리 서버 시험·부하 실측
scripts/ml/export-activity.py  학습용 내보내기(+ test)
```
`.env.example` 대신 `eco-bots.example.json`(비밀번호 칸 비움).

첫 작업 3개(워킹 스켈레톤):
1. 서버 E1·E2·E3 + `EcoMoveTests` — 봇이 순간이동해 사냥터에 선다.
2. `Lod.EcoBots` 최소판 — 봇 1개 접속 → 이동 → `HuntProxyRunner` 판단으로 사냥 → 물약 0이면 마을 이동 → 물약 사기 → 돌아감(SC-001 의 뼈대).
3. 사건 기록 E7 tick·move·buy 세 종류 — 한 바퀴가 기록으로 보인다.
