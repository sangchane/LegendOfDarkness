---
name: sk
description: |
  "무슨 스킬을 불러야 하지?"를 대신 답하는 스킬 추천기. 사용자가 원하는 것을 한 문장으로 주면
  설치된 스킬(개인·플러그인·번들) 중 맞는 것을 최대 3개 골라 이유와 실행 명령을 준다.
  사용 시점: 스킬 이름을 모를 때, "이럴 땐 뭘 써?", "/sk 회의실 예약 서비스 기획", "/sk 지난 작업 이어서".
  스킬 이름을 이미 아는 요청에는 쓰지 않는다 — 그냥 그 스킬을 부른다.
argument-hint: "[하고 싶은 것 한 문장]"
disable-model-invocation: true
allowed-tools: Read, Bash(python:*)
metadata:
  version: "0.1.0"
  updated: "2026-09-03"
---

# sk — 스킬 추천기

요청: **$ARGUMENTS**

## 절차

1. 아래 두 라우팅 표를 먼저 본다 (사람이 근거를 달아 둔 1순위 후보):
   - `~/.claude/skills/service-autopilot/references/skill-routing.md` (기획·설계 A0~GATE)
   - `~/.claude/skills/service-prompt-workflow/references/skill-routing.md` (구현 0~9)
   요청이 어느 단계에 해당하면 그 행의 스킬이 곧 답이다.
2. 표에 없으면 설치 카탈로그를 훑는다: `python ~/.claude/skills/_tools/skill_catalog.py --catalog`
   (id | 출처 | description). description은 대부분 영어이므로 **의미로** 맞춘다. 키워드 일치에 매달리지 않는다.
3. 최대 3개만 고른다. 넷째 후보가 아깝다면 요청이 두 가지 일이라는 뜻이니 그렇게 말한다.

## 출력 (이 형식 그대로, 그 외 설명 없음)

```
1. /<스킬 id> <인자 예시>   — 왜 맞는지 한 줄 (요청의 어느 부분과 대응하는지)
2. ...
3. ...
바로 실행할까요? 번호로 답하면 그 스킬을 호출합니다.
```

- 자동 트리거되는 스킬(description에 사용 시점이 있는 것)은 이름 없이 문장으로 말해도 된다고 한 줄 덧붙인다.
  예: "회의실 예약 서비스 만들고 싶어"라고만 해도 service-autopilot이 뜬다.
- 미설치 플러그인 스킬은 추천하지 않는다. 마켓에 있는 걸 알면 마지막에 "설치하면 가능: <이름>" 한 줄만.
- 후보가 없으면 "맞는 스킬 없음 — 그냥 요청하세요" 한 줄. 억지로 채우지 않는다.

## 자주 헷갈리는 것

- **지난 작업 이어서 / 지금 어디까지 했지** → 스킬이 아니다. `catch-up`이 깔아 둔 SessionStart 훅이
  세션 시작마다 `NEXT.md`의 현재 작업 블록을 자동 주입한다. 상세 이력은 `WORKLOG.md`를 읽으면 된다.
  `catch-up` 자체는 그 구조를 **처음 한 번 세팅**하는 부트스트랩이고 사용자 호출 전용이다.
- **서비스 기획** → `service-autopilot` (뭘 만들지 막연할 때) / **구현** → `service-prompt-workflow` (뭘 만들지 아는 때).
