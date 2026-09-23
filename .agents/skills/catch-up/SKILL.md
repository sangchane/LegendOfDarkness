---
name: catch-up
description: |
  새 세션·다른 AI 툴이 이전 작업 맥락을 토큰 낭비 없이 바로 "따라잡게(catch up)" — 프로젝트에
  "얇은 항상-로드 + 온디맨드 스코프" 컨텍스트 구조를 1회로 세팅하는 부트스트랩 스킬.
  사용 시점: 프로젝트 시작/정리 시, "세션마다 맥락 다시 설명하기 귀찮다", "새 세션이 이전 걸
  기억 못 한다", "CLAUDE.md가 너무 커졌다", "Antigravity·Cursor에서도 별도 지시 없이 프로젝트를
  이해시키고 싶다"일 때. 결과물: 얇은 CLAUDE.md(행동규칙+@AGENTS.md) · AGENTS.md(크로스툴 단일
  원본) · NEXT.md(다음-할일)+SessionStart 훅 · 스코프별 CLAUDE.md(온디맨드) · WORKLOG(히스토리 분리).
  근거: Anthropic Progressive Disclosure / Just-in-time, Claude Code 중첩 CLAUDE.md 온디맨드
  로딩, AGENTS.md 오픈표준(Antigravity v1.20.3+ 네이티브), Memory Bank 패턴.
allowed-tools: Read, Write, Edit, Glob, Grep, Bash
disable-model-invocation: true
argument-hint: "[--tools antigravity,cursor]"
metadata:
  version: "1.0.3"
  updated: "2026-09-09"
---

# Catch-Up (세션 이어받기 부트스트랩)

새 세션이나 다른 AI 툴이 "이전에 뭘 하고 있었는지"를 매번 다시 설명하지 않고 바로 파악하게 한다.
핵심은 **항상 읽히는 층은 얇게, 상세는 필요할 때만**(Progressive Disclosure).

## 규칙

1. **비파괴.** 기존 파일을 바꾸기 전 diff/제안을 보여주고 **승인**받는다. 승인 없이 쓰지 않는다.
2. **clean 트리에서만.** 작업 트리가 dirty면 `git stash` 또는 전용 브랜치를 먼저 요구한다. 롤백은
   **스킬이 만든 커밋만 `git revert`** — `reset --hard`는 사용자 작업까지 지운다.
3. **단일 출처.** "다음 할 일"은 `NEXT.md` 마커 블록에만. 히스토리는 `WORKLOG.md`에만.
   두 곳에 히스토리를 쌓지 않는다(파일 비대의 근본 원인).
4. **항상-로드 얇게.** CLAUDE.md ≤ ~4KB, AGENTS.md ≤ ~4KB, 합 ≤ ~8KB 목표. 상세는 스코프/계획
   파일로 내려 온디맨드로 만든다.
5. **시크릿은 경로만.** `.env`/키 파일의 **내용**은 컨텍스트 파일에 넣지 않는다.

## 산출 구조 (목표)

```
루트/
├─ CLAUDE.md        [항상로드·Claude Code] 행동규칙 + `@AGENTS.md`
├─ AGENTS.md        [크로스툴 단일 원본] 프로젝트 표준 + 네비 + "세션시작=NEXT 먼저"
├─ GEMINI.md        [선택·Antigravity] "→ AGENTS.md 참조" 포인터만
├─ NEXT.md          다음-할일 마커 블록만 (히스토리 없음)
├─ WORKLOG.md       히스토리(append) — 항상로드/주입 안 함
├─ .claude/settings.json          SessionStart 훅 배선(병합)
├─ tools/hooks/print_next_action.py  마커 블록만 출력
└─ <scope>/CLAUDE.md              온디맨드(그 폴더 만질 때만)
```

## 절차 (5 페이즈 — 순서대로, 각 끝에 사용자 확인)

### 1. SCAN — 현황 파악
- Glob/Grep로 탐지: 기존 `CLAUDE.md`·`AGENTS.md`·`GEMINI.md`, `.claude/settings.json`의 SessionStart 훅,
  `NEXT.md`/`WORKLOG.md`(위치 여러 곳 가능), 상위 폴더 목록, `.env`/키 등 시크릿 패턴, `git` 저장소 여부.
- 리포트: 무엇이 이미 있고 무엇이 없나 + **기존형**(큰 CLAUDE.md 있음) vs **신규형**(거의 없음) 판정.

### 2. CLASSIFY — 스코프·이관 계획
- **스코프 폴더 후보**를 추린다: 코드/역할 디렉토리(예: `server` `client` `tools` `data` `packages/*`).
  자동 확정하지 말고 후보를 제시해 **사용자 확인**.
- 기존 CLAUDE.md가 크면 그 **프로젝트 상세 섹션 목록**을 뽑아 "어디로 이관"할지 매핑 초안을 만든다
  (→ AGENTS.md / → 해당 스코프 CLAUDE.md). 행동규칙은 루트 CLAUDE.md의 Karpathy 가이드라인 4절(템플릿 원문 대응)만 남긴다.
  기존 CLAUDE.md에 다른 행동규칙이 있으면 프로젝트 규칙은 AGENTS.md로 옮기고, 일반 행동규칙은 이 4절로 교체를 제안한다(승인 후).

### 3. PROPOSE — 초안 제시 (아직 쓰지 않음)
`templates/`에서 초안을 만들어 **diff/신규파일 목록으로 제시**한다:
- `CLAUDE.md` ← `templates/CLAUDE.md.tmpl` (Karpathy 가이드라인 4절 원문 대응 + 상단 `@AGENTS.md`. 출처·동기화 커밋은 템플릿 머리 주석)
- `AGENTS.md` ← `templates/AGENTS.md.tmpl` (프로젝트명·스택·네비 포인터·Session start 지시·NEXT 포인터 채움 + 기존 상세 이관)
- `NEXT.md` ← `templates/NEXT.md.tmpl` (마커만). 기존 NEXT가 있으면 **진행중 1~3건만** 남기고 완료분은 WORKLOG History로.
- `WORKLOG.md` ← `templates/WORKLOG.md.tmpl` (Current State + History). 기존 히스토리를 옮길 때도 결정·제약·버린 대안·미결·정확한 이름과 숫자는 줄이지 않는다.
- 훅 `tools/hooks/print_next_action.py` ← `templates/print_next_action.py` (그대로 복사, 경로 자동탐색)
- `.claude/settings.json` ← SessionStart 배열에 훅 **병합**(command에 `print_next_action.py` 있으면 skip=재추가 안 함)
- 스코프별 `<scope>/CLAUDE.md` ← `templates/scope-CLAUDE.md.tmpl`
- (선택 `--tools antigravity,cursor`) `GEMINI.md` 포인터 / `.cursor/rules` 포인터
- **이관 무손실 체크리스트**(원본 섹션 ↔ 목적지)를 함께 제시한다.

### 4. APPLY — 승인 후에만
- 선행: `git status`로 clean 확인. dirty면 stash/브랜치 안내 후 대기.
- git 체크포인트(커밋 또는 전용 브랜치) → 파일 쓰기 → settings.json 병합.
- 롤백 안내: 스킬 커밋만 `git revert <checkpoint>`.

### 5. VERIFY — 자동 점검 리포트
- CLAUDE.md 크기·프로젝트 상세 0줄 (SC-001)
- AGENTS.md 크기·총 항상로드 ≤ ~8KB (SC-001b)
- `python3 tools/hooks/print_next_action.py` → 마커 블록만 출력 (SC-002; 다른 PC python 부재 조기감지)
- `grep -rn "NEXT-ACTION"` → 정의 1곳 (SC-006)
- git 체크포인트 존재 (SC-005)
- 이관 무손실 체크리스트 통과 (SC-009)

## 크로스툴 (AGENTS.md = 단일 원본)
- **Claude Code:** `CLAUDE.md` → `@AGENTS.md` import + SessionStart 훅이 NEXT 블록 주입.
- **Antigravity(v1.20.3+):** `AGENTS.md` 네이티브 읽음. `GEMINI.md`는 포인터만(우선순위 가로채기 방지).
- **Cursor:** `AGENTS.md` 네이티브 / `.cursor/rules` 포인터(선택).
- 훅 없는 툴은 **AGENTS.md 최상단 "세션 시작 시 NEXT.md 현재-작업 먼저 읽어라" 지시**로 대체(소프트 의존).

## 재실행(멱등)
- 자기 훅은 command에 `print_next_action.py` 있으면 재추가 안 함.
- 이미 세팅된 repo면 파괴 없이 마커/포인터 갱신만.

원칙·근거(왜 이렇게 하나)와 출처는 필요할 때만 `references/PRINCIPLES.md`를 읽는다.

버전은 프론트매터 `metadata`.
