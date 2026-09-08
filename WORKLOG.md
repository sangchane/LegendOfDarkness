# WORKLOG — LOD

세션/에이전트 간 핸드오프 로그. **"다음 할 일"은 여기 쓰지 않는다 → `NEXT.md`.**
긴 로그는 붙이지 말고 결과만 요약한다.

## Current State
- Status: decision-recorded
- Focus: 모바일 전환 기준선은 Hades(C#). Medenia는 참고 자료만 유지하고, DungMunkey/Dark-Ages는 작업공간에서 제외
- Last updated: 2026-09-08

## History (append; 최신이 위)
- 2026-09-08 — 모바일 전환 기준선 결정: Hades 서버·JSON 데이터·게임 규칙을 유지하고 모바일 클라이언트를 신규 제작한다. Medenia는 실행 대상에서 제외하되 참고 자료와 미커밋 실험은 보존한다. `DungMunkey/Dark-Ages`는 네트워크 없는 오프라인 재현물이어서 submodule에서 제거(원격 저장소는 삭제하지 않음)
- 2026-09-08 — Medenia 후속 조사 기록: 최초 `ServerTableRequest id=0` 실패 뒤 미커밋 `client-crypto.ts` 오프셋 수정으로 `id=1`과 redirect 송신까지 확인. 다만 `ServerTableEntry.port`가 `NaN`이어서 로그인 완주는 미확인. `gateway-listener.ts` 디버그 변경과 실행 프로세스(8081·8082·5173)는 유지하고 이번 커밋에서 제외
- 2026-09-08 — Medenia B단계 실행(Sonnet 에이전트): 서버 8082·자산 8081·클라 5173 기동, SQLite 자동 생성, WS 핸드셰이크 OK. 실패: 서버 `gateway-listener.ts:35` `entry.ip` undefined(ServerTableRequest id=0) → 로그인 폼 미표시. 임시 패치 `tmp/medenia-local-run.patch`(58줄, 미커밋), 보고서 `tmp/medenia-run-report.md`. 레포 밖 변경: 시스템 Python에 setuptools 설치
- 2026-09-08 — 실행 절차·체크리스트 초안 `docs/run-procedure.md` 작성, catch-up 구조 세팅
- 2026-09-08 — 현행 프로젝트 분석서 `docs/current-system-analysis/` 01~09 작성, Graphite 작업 규칙 추가 (dfe0ff2, c74b2db)
