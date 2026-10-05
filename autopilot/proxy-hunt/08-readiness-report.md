# 준비도 판정 — 대신 사냥
버전: v1.0 · 기준 03 v1.1 · lite GATE(check_package + 자기 점검)

## 판정: CONCERNS (구현 착수 가능, 아래 확인을 첫 작업에서)
check_package: CRITICAL 0 · HIGH 0 · FR 12 → 05 12 · SC 5 → 06 5 · register 17/17.

## 자기 점검 지적
- MEDIUM 로그인 서버와 게임 서버가 한 프로세스라 `HandoffTokens` 를 공유한다는 가정 — `ServerContext.Redirects` 공유로 미루어 봄. 첫 작업 ①에서 확인, 아니면 표를 로그인 쪽에만 두고 게임 서버는 파일로 알림.
- MEDIUM 대리 세션이 밀려날 때(ClientDisconnected) 다시 넘김을 만들면 안 된다 — 대리는 0xF1 7 을 보내지 않으므로 맡김이 없다. 시험 SC-004 에 「두 번째 작업 파일 없음」 단언을 넣는다.
- MEDIUM Godot 4.6 iOS 수명 신호가 안 오면 FR-009 의 「바로 닫기」가 없고 넘김이 30초 늦다 — 기능은 그대로(서버 끊김이 계기). 폰 기록 `app_lifecycle` 로 확인.
- LOW 루프백 판정은 클라우드가 프록시 없이 직접 받는다는 전제(04 ④).

## 핸드오프 → dev:build L 변경 경로(인증 변경: SPEC + 보안 리뷰)
입력: `03-prd.md`(요구사항·상수) + `05-api-contract.md`(계약) + 07 첫 작업 3개. 04·06 은 경로만.
```
dev:build L — 대신 사냥. SPEC 은 autopilot/proxy-hunt/03-prd.md·05-api-contract.md 를 그대로 쓴다.
첫 작업: ① 서버 HandoffTokens + 로그인 열쇠 길 + SC-003 6경우 단위 시험 ② 0xF1 7 맡김 → 끊김 → 작업 파일(격리 서버) ③ Lod.HuntProxy 가 작업 파일로 접속해 HuntDriver 로 사냥(SC-001). 그 뒤 앱(맡김 송신·수명·설정 줄), 결과 요약, 배포. 보안 리뷰는 ①② diff.
```
<model_hints>서버 열쇠·로그인(opus) · HuntDriver 옮기기·대리 실행기(opus) · 앱 설정 줄·배포 스크립트(sonnet) · 문구(haiku)</model_hints>
