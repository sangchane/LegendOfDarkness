# NEXT — 사내 회의실 예약 (autopilot lite)
- 현재 상태: GATE 완료 — PASS (08-readiness-report.md), 03 v1.1 · 04~07 기준 03 v1.1, check_package CRITICAL 0 · HIGH 0
- 다음 할 일:
  1. 선행 조건 P1 — MRBS 데모 30분 후 자체 구축 여부 확정
  2. 선행 조건 P2 — 개인정보보호법 원문 확인 → RETENTION_DAYS 근거를 출처 URL로 (03 v1.2 + 전파)
  3. 08의 핸드오프 블록을 /service-prompt-workflow 에 붙여 넣기 → SPEC.md → 첫 작업 3개
- 재개 명령: 파이프라인은 끝났다. 개정이 생기면 03을 고치고 `python scripts/check_package.py s1-meeting-room-booking` 재실행
