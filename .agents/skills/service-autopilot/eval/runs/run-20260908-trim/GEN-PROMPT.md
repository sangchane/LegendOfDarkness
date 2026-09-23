이 작업은 service-autopilot 스킬(현재 버전)의 산출물 품질을 재는 평가 런이다. 스킬 본문의 절차를 그대로 따르고, 이 프롬프트는 환경만 정한다.

작업 디렉터리: C:/Users/dev/.claude/skills/service-autopilot/eval/runs/run-20260908-trim

1. Skill 도구로 `service-autopilot`을 다음 인자로 호출하고 그 절차대로 A0부터 GATE까지 끝까지 실행한다:
   `full 라즈베리파이로 IP 카메라를 제어하고 실시간 모니터링하는 서비스를 만들고 싶다.`
2. 산출물 디렉터리는 `autopilot/<slug>/` 대신 작업 디렉터리 아래 `s4-rpi-ip-camera/`로 한다
   (00-seed.md … 08-readiness-report.md, decision-log.md, NEXT.md 전부 그 안에).
3. 평가 런이라 사용자에게 질문할 수 없다. A2 질문 배치는 AskUserQuestion을 쓰지 말고, 각 문항의 추천안을 `Assumed(무응답)`으로
   채택해 register에 그대로 기록한다(문항과 옵션은 평소처럼 작성한다).
4. 서브에이전트: A1 조사는 `model: sonnet`. A4 독립 검토와 GATE 검토관은 `subagent_type: fresh-reviewer`, `model: fable`
   (세션 등급). GATE는 1회, 재검토 없음(예산). 검토관이 돌려준 텍스트로 메인이 08을 쓴다.
5. GATE 전에 `python C:/Users/dev/.claude/skills/service-autopilot/scripts/check_package.py <작업 디렉터리>/s4-rpi-ip-camera`를 실행하고
   출력을 검토관 입력에 넣는다.
6. 429·한도 오류로 끊기면 스킬의 재개 규칙대로 있는 파일은 다시 만들지 않고 없는 첫 파일부터 이어 간다.
7. 끝나면 보고: 강도 판정과 사유, 검색 횟수, 스킬 호출 횟수, 서브에이전트 수와 모델, 소요 시간, check_package 결과(CRITICAL/HIGH 수),
   GATE 판정과 지적 수, 알면 토큰 사용량.
