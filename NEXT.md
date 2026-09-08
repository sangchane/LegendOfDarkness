<!-- NEXT-ACTION:START -->
## ▶ 지금 할 일 (새 세션은 이 블록부터 — SessionStart 훅이 자동 주입)

- **[다음]** Hades(A) Phase 0 실행 검증: `docs/run-procedure.md` 10절 A단계. 클라이언트는 내려받은 7.18(`sources/DarkAges718single.exe`, gitignore)을 먼저, 관리자 계정명은 기본 `wren`. 실제 실행은 사용자 요청 후 시작 · 근거: `docs/mobile-conversion-review.md` 4절
- **[대기]** PRD `docs/mobile-test-v1-prd.md` v0.2의 결정 필요 항목(D-001 엔진, D-004 기준 기기, 지면 아이템 소멸 시간 키)은 Phase 0 결과 뒤 확정
- **[보존]** Medenia 실험은 `tmp/medenia-local-run.patch`(99줄)로만 보존. submodule은 원복(clean), 로컬 프로세스 종료됨. 재현하려면 패치 적용 + `.env` 2개
<!-- NEXT-ACTION:END -->

<!--
규칙:
- 이 마커 사이는 "지금/다음 할 일" 1~3건만. 짧게(화면 한 판).
- 완료된 항목은 여기 두지 말고 WORKLOG.md 의 ## History 로 옮긴다 (단일 출처·비대 방지).
- 훅(print_next_action.py)은 이 마커 사이만 세션에 주입한다.
-->
