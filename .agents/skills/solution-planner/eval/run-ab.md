# A/B 실행 절차 (Claude Code 전용, 맥스 요금제 사용량 내)

하네스 적용 출력과 미적용 베이스라인을 생성해 rubric.md로 비교하는 절차.

## 1. 출력 생성

시드마다 두 번 실행한다. 결과는 `eval/runs/<날짜>/` 아래에 저장.

```bash
# 베이스라인 (스킬 접근 차단 상태에서)
claude -p "다음 서비스를 기획해줘: <시드 입력>" > eval/runs/<날짜>/s1-baseline-1.md

# 하네스 적용 (스킬 설치 상태에서)
claude -p "solution-planner 스킬을 사용해 다음 서비스를 기획해줘: <시드 입력>" > eval/runs/<날짜>/s1-harness-1.md
```

주의: 베이스라인 실행 시 스킬이 트리거되지 않도록 스킬 미설치 환경이나 별도 프로젝트에서 실행한다. 시드당 각 2회 반복.

## 2. 블라인드 채점

- 파일명에서 baseline/harness를 지운 사본(a.md, b.md)을 만든다.
- 새 세션에서 rubric.md와 두 사본만 주고 채점시킨다. 어느 쪽이 하네스인지 알려주지 않는다.
- 채점 결과를 `eval/runs/<날짜>/scores.md`에 기록한다.

## 3. 판정과 기록

- rubric.md의 판정 규칙 적용.
- 결과가 하네스 개선 검증(Evolve)의 일부라면 `evolution/upgrade-log.md`에 기록.
- 하네스가 지거나 무승부인 항목은 버그 리포트다. 해당 항목을 개선 대상으로 SKILL.md나 references를 수정하고 재실험한다.
