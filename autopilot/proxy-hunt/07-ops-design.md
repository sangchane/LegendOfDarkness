# 배포·운영 설계 — 대신 사냥
버전: v1.0 · 기준 03 v1.1

## 배포
- 대리 프로그램 `mobile/bots/Lod.HuntProxy`(.NET, 서버와 같은 런타임) → 클라우드 `~/lod-proxy/app/`, systemd `lod-proxy.service`(하나, Restart=always). 설정 `~/lod-proxy/hunt-proxy.json`(Host·LoginPort·JobFolder·MapFolder — 비밀 없음).
- `scripts/ops/cloud-server.sh` 에 `proxy`(올리고 다시 켬)·`proxy-logs` 추가, `deploy` 가 함께 올림(봇과 같은 틀). 작업 폴더 `~/lod-proxy/jobs`(0700) — 서버 설정 `ProxyJobFolder` 로 같은 경로.
- 앱: 맡김·수명 처리·설정 줄 → `scripts/ops/ios-build.sh install`.
- 순서: 서버(옛 앱 호환) → 대리 → 앱.

## 관측성
- 대리 기록 `~/lod-proxy/logs/hunt-proxy.log`(1MB×5 돌림, `BotLog` 그대로): 작업 집음·접속·5분 요약(캐릭터·맵·체력·잡은 수)·끝(까닭·분·경험치).
- 서버 기록: 넘김 발급·열쇠 로그인 성공/거절(까닭)·서버 강제 끊김 — 열쇠 값은 안 씀.
- 활동 대시보드: kind `proxy` 세션 수·시간(관리자 활동 화면 기존 표에 한 줄).

## 알림
없음(1인 운영). 확인은 `proxy-logs`.

## 장애·복구
| 장애 | 감지 | 영향 | 복구 |
|---|---|---|---|
| 대리 프로그램 죽음 | systemd 재시작, 기록 | 진행 중 대리 끊김(서버가 정리), 새 넘김은 재시작 뒤 | `cloud-server.sh proxy` |
| 작업 폴더 권한·경로 틀림 | 서버 기록 「넘김 쓰기 실패」 | 대리 없음(지금과 같음) | 폴더 만들고 0700 |
| 대리가 엉뚱하게 사냥(벽·출구) | 대리 5분 요약 | 캐릭터 죽음 | 앱 「대신 사냥」 끔, 대리 기록 보고 고침 |
- 백업: 새로 남는 데이터는 `Aisling.ProxyReport` 한 줄뿐 — 기존 캐릭터 백업(`cloud-server.sh backup`)에 포함.

## 착수 자산
- `mobile/bots/Lod.HuntProxy/` — Program(작업 폴더 보기·작업마다 Task) · HuntProxyRunner · ProxyConfig.
- `mobile/src/Lod.Mobile.Core/Automation/HuntDriver.cs` — 시야 모으기(앱·대리 공용).
- 서버 `Types/HandoffTokens.cs` · `ClientFormatF1` 종류 7 · `LoginServer.Format03Handler` 열쇠 길 · `GameServer.ClientDisconnected` 넘김.
- 첫 작업 3개(워킹 스켈레톤): ① 서버 `HandoffTokens` + 열쇠 로그인 + SC-003 단위 시험 ② 0xF1 7 맡김 → 끊김 → 작업 파일 ③ 대리 프로그램이 작업 파일로 들어가 `HuntDriver` 로 평타(SC-001).
