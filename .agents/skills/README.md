# 개인 Claude Code 스킬 모음

`~/.claude/skills/`에 두고 쓰는 **개인 제작 스킬 저장소**다. 어느 PC에서든 이 저장소를
`~/.claude/skills`로 clone하면 Claude Code가 세션 시작 시 자동으로 인식한다.

스킬 이름을 몰라도 된다 — 각 스킬의 `description`을 보고 Claude가 상황에 맞게 자동 적용한다.
직접 부르고 싶으면 `/스킬이름 인자` 형태로 호출하고, **뭘 불러야 할지 모르면 `/sk <하고 싶은 것>`** 으로 물어본다.

형식은 Anthropic Agent Skills 표준(agentskills.io 스펙 + Anthropic 작성 가이드 + Claude Code 확장)을 따른다.
버전·갱신일은 각 SKILL.md 프론트매터 `metadata`에 있다.

## 스킬 간 관계 (큰 그림)

```
[아이디어만 있고 막연함]        [뭘 만들지 정해짐]              [화면이 포함됨]
service-autopilot        →   service-prompt-workflow   →   frontend-design-taste
"기획·설계 자동 생성"          "구현을 단계별로 명령"           "AI 티 안 나는 UI 강제"
A0~A7 + GATE                 0 BASE ~ 9 REFLECT               BUILD·REVIEW에서 참조
      │                            │
      └── 각 단계 진입 시 references/skill-routing.md 의 행을 읽어 설치된 전문 스킬(ecc:* 등)을 호출
      └── 서브에이전트에 맡기는 단계·작업은 references/model-routing.md 로 모델 등급(opus/sonnet/haiku/fable)을 고름
      └── 강도 spike/lite/full: 미확정이면 spike(엔진+시뮬 먼저), 아니면 A0 신호로 판정. 실측 lite 20만·full 70만 토큰(상한 25만·80만)
                                   └── PLAN 이후는 superpowers(writing-plans → TDD → verification → code-review)로 넘김
                                   └── BUILD·REVIEW 는 ponytail 결정 사다리(있으면 플러그인, 없으면 내장) 적용

[경계 규칙]  ~/.claude/CLAUDE.md — 설계는 service-autopilot, 저장소 안 구현은 superpowers, 웹 UI는 frontend-design-taste (이름 부를 필요 없음)

[세션 부트스트랩]  catch-up — 얇은 CLAUDE.md/AGENTS.md/NEXT.md 구조를 1회 세팅 (사용자 호출 전용)
[스킬 추천]        sk — "/sk 문구" 로 맞는 스킬 최대 3개 추천
[정비 도구]        _tools/skill_catalog.py — 설치 스킬 카탈로그 + 라우팅 표 정합성 검사
```

비유하면: **autopilot이 건축 설계도를 그리고, prompt-workflow가 시공 순서를 지휘하고,
design-taste가 인테리어 품질 기준을 잡는다.** 각 공정마다 어느 전문 업체(스킬)를 부를지는 라우팅 표에 적혀 있다.

---

## 1. service-autopilot — 기획·설계 오토파일럿

**한 줄 아이디어를 "구현 착수 가능한 설계 패키지"로 바꾼다.** AI가 스스로 조사하고, 사각지대를 찾아 덮고,
가정으로 못 덮는 위험한 결정만 객관식 최대 5문항으로 **딱 1번** 묻는다.

- **언제 쓰나**: 신규 서비스/기능 기획, PRD, 기술스택 추천, MVP 범위, 도메인을 모르는 상태의 착수, 위협모델·API·운영 설계.
- **파이프라인**: A0 SEED → A1 RECON(조사) → A2 INTERROGATE(사각지대 심문, 질문 1회) → A3 PRD → A4 아키텍처+위협모델(STRIDE)
  → A5 API계약+ERD → A6 테스트설계 → A7 배포/관측성 → GATE(새 컨텍스트 적대적 검토).
- **단계별 스킬 라우팅** (`references/skill-routing.md`): 단계 진입 시 설치된 전문 스킬을 호출한다.
  예: A1은 `ecc:research-ops`, A4는 `ecc:architecture-decision-records`+`ecc:security-review`, A5는 `ecc:api-design`,
  A6은 `ecc:tdd-workflow`, A7은 `ecc:deployment-patterns`. 미설치면 대체 절차. 사용 기록은 decision-log에 남는다.
- **단계 진입 사전조사**: A3~A7은 각 단계의 결정에 필요한 근거를 정량(숫자+출처+확인일)·정성(실무자 인용)·사용자 영향 3줄로 산출물 상단에 남긴다.
- **단계별 모델 라우팅** (`references/model-routing.md`): 메인 세션 모델은 못 바꾸므로 서브에이전트 `model`로 고른다 —
  A1 조사는 `sonnet`, A4 독립 검토·GATE 검토관은 `opus`(생성 모델 이상), 판단 단계(A2~A5)는 위임하지 않는다.
  08 핸드오프에 구현 작업 클래스별 `<model_hints>`를 붙인다.
- **강도 spike/lite/full**: 플랫폼·범위·핵심 루프가 미확정이면 spike(엔진+시뮬 워킹 스켈레톤 먼저, 10만 토큰). 아니면 A0에서 신호(돈·안전·법·민감정보, 외부 연동 2개↑, 사용자 100명↑, 하드웨어, 팀 2명↑, "납품")로 lite/full 판정.
  lite는 질문 0·검색 ≤5·위협모델 조건부·GATE 자기 점검(실측 20만, 상한 25만). full은 상한 80만(서브에이전트 포함). `/service-autopilot spike|lite|full …`로 강제 가능.
- **자기 선언 검증**: GATE 전에 `python scripts/check_package.py autopilot/<slug>` — FR→05 커버리지, SC→06 시나리오, 미결정, register 마킹,
  버전 전파를 스크립트가 센다. CRITICAL이면 GATE 진입 금지.
- **개정 전파·상수 표**: 산출물마다 `버전:`, 04~07은 `기준 03 v`. 숫자는 03 상수 표에만.
- **착수 자산**: A3 화면 스케치 1장, A7 디렉터리 구조·`.env.example`(값 없음)·첫 작업 3개(워킹 스켈레톤).
- **예산·재개**: GATE 재실행 1회, 끊기면 없는 첫 파일의 단계부터 재개, 런 끝에 비용 기록.
- **산출물**: `00-seed.md` ~ `08-readiness-report.md` + `decision-log.md` (9개 파일).
- **평가**: `eval/PROTOCOL.md`(블라인드 pairwise A/B, 동결 시드 10개) + `evals/evals.json`(skill-creator 호환).

```
> 회의실 예약 서비스 만들고 싶어           ← 자동 발동
> /service-autopilot 라즈베리파이로 IP 카메라 모니터링 서비스
```

---

## 2. service-prompt-workflow — 구현 실행 워크플로우

**"뭘 만들지 아는" 순간부터 배포까지, AI 코딩 에이전트에게 낭비 없이 명령하는 9단계 실행 하네스.**
각 단계에 하드 게이트가 있어 못 넘으면 다음 단계로 안 간다.

- **파이프라인**: 0 BASE → 1 FRAME → 2 EXPLORE → 3 SPEC → 4 PLAN → 5 BUILD → 6 VERIFY → 7 REVIEW → 8 SHIP → 9 REFLECT.
- **라우터 내장**: "구현해" → BUILD, "리뷰해줘" → REVIEW, "커밋해" → SHIP.
- **작업 클래스별 모델 라우팅** (`references/model-routing.md`): PLAN에서 tasks.md에 `model:` 태그
  (불변식·동시성·인증·마이그레이션=`opus`, CRUD·화면·RED 테스트·설정=`sonnet`, 리네임·문구·포맷=`haiku`),
  BUILD 위임 시 그대로 적용, REVIEW 정확성은 `opus` fresh. VERIFY 2회 실패 시 한 등급 승급, 그래도 실패면 SPEC으로.
- **superpowers가 실행 엔진** (설치 시): PLAN `superpowers:writing-plans`, BUILD `superpowers:subagent-driven-development`/`executing-plans`
  + `test-driven-development`, VERIFY `verification-before-completion`, 실패 시 `systematic-debugging`, REVIEW `requesting/receiving-code-review`
  + `/code-review` + `ponytail:ponytail-review`, SHIP `finishing-a-development-branch`. 이 스킬은 라우터·ETHOS·배선만 맡는다.
  **직접 부를 일은 없다** — "구현해·리뷰해줘·커밋해"에 자동으로 뜬다. 미설치 PC에서는 `prompt-templates.md` 블록이 대체.
- **PLAN 순서 규칙**: 첫 작업 3개는 워킹 스켈레톤(핵심 여정을 끝까지 얇게), 그다음 위험 큰 것부터. 로그인·화면은 보통 마지막.
- **단계별 스킬 라우팅** (`references/skill-routing.md`): FRAME은 bounded면 `superpowers:brainstorming`, 새 서비스면 `service-autopilot`.
- **ponytail 배선**: BUILD 진입 시 결정 사다리(필요한가 → 이미 있나 → 표준 라이브러리 → 네이티브 → 설치된 의존성 → 한 줄 → 최소 코드)를
  코드 작성 전에 탄다. 플러그인이 있으면 훅이 자동 주입하고, 없으면 템플릿의 `<ladder>` 블록이 같은 역할을 한다.
  REVIEW는 정확성 1회 + 과잉설계 1회를 넘기지 않는다.
- **복붙 프롬프트 템플릿**: `references/prompt-templates.md`.

```
> 이 스펙대로 구현해            ← BUILD 진입, 사다리 적용
> 이거 진짜 되는지 확인해       ← VERIFY 진입
```

---

## 3. frontend-design-taste — 프론트엔드 디자인 취향

**웹 UI에서 "AI가 만든 티(slop)"를 없애고 의도된 고급 결과를 강제하는 취향 하네스.** React·Tailwind·Zustand 특화.
3개 dial(밀도·모션·파격)과 프로파일(관제 8 / 제품 UI 5 / 랜딩 3)을 정하고, 하드룰 위반은 반려한다.
service-prompt-workflow의 BUILD·REVIEW에 프론트가 포함되면 자동 참조된다.

---

## 4. catch-up — 세션 이어받기 부트스트랩 (1회 세팅, 사용자 호출 전용)

**"지난 작업 확인" 스킬이 아니다.** 프로젝트에 얇은 `CLAUDE.md`(행동규칙 + `@AGENTS.md`) · `AGENTS.md`(크로스툴 단일 원본) ·
`NEXT.md`(다음-할일) + SessionStart 훅 · 폴더별 `CLAUDE.md` · `WORKLOG.md` 구조를 **처음 한 번** 깔아 주는 스킬이다.
그 뒤로는 세션 시작마다 훅이 `NEXT.md`의 현재 작업 블록을 자동 주입하므로, "지난 작업 이어서"는 아무 스킬도 부를 필요가 없다.
상세 이력이 필요하면 `WORKLOG.md`를 읽는다.

```
> /catch-up                                   ← diff를 먼저 보여주고 승인 후 적용
> /catch-up --tools antigravity,cursor
```

---

## 5. sk — 스킬 추천기 (사용자 호출 전용)

스킬 이름을 몰라도 되게 하는 얇은 스킬. 라우팅 표 2개(사람이 근거를 단 1순위 후보)를 먼저 보고,
없으면 설치 카탈로그를 의미로 훑어 최대 3개를 이유·실행 명령과 함께 추천한다.

```
> /sk 회의실 예약 서비스 기획 시작하고 싶어
> /sk 지난 작업 이어서            ← "스킬 필요 없음, 훅이 이미 주입함" 이라고 알려준다
```

이미 이름을 아는 요청에는 쓰지 않는다. 그냥 그 스킬을 부르는 게 빠르다.

---

## 6. solution-planner — ⚠ 폐기됨 (2026-07-09)

service-autopilot으로 대체됐다. 과거 산출물(`solution-planning/` 디렉토리) 해석용으로만 남겨둔다.

---

## _tools/ — 정비 도구

```bash
python _tools/skill_catalog.py              # 설치 스킬 요약 + 항상-로드 메타데이터 토큰 추정 + 라우팅 표 정합성 검사
python _tools/skill_catalog.py --catalog    # 설치 스킬 전체 목록 (id | 출처 | description)
python _tools/skill_catalog.py --unassigned # 설치됐지만 어느 라우팅 표에도 없는 스킬
python _tools/skill_catalog.py --available  # 마켓플레이스에 있지만 미설치인 플러그인
```

스킬 id 표기: 플러그인 `ecc:api-design`, 번들 `/code-review`, 개인 `frontend-design-taste`.
라우팅 표가 미설치 스킬을 가리키면 종료코드 1.

## 외부 스킬 흡수 기준

새 스킬을 들일 때 아래를 전부 확인하고, 통과하면 **복사하지 말고 플러그인으로 설치**한 뒤 라우팅 표에 배정한다.

1. **신호**: GitHub ★ 5만 이상 또는 Anthropic 공식(anthropics/skills, claude-plugins-official). ★는 GitHub API로 당일 확인.
2. **활성**: 마지막 push 90일 이내. 멈춘 저장소는 배정하지 않는다.
3. **라이선스**: OSI 승인(MIT·Apache-2.0 등). 스킬 본문을 복사하지 않으므로 라이선스 표기 의무는 evidence.md 한 줄로 충분.
4. **근거**: 자체 벤치마크나 평가가 있는가. 없으면 우리 스모크 회귀(시드 3개)로 직접 잰다.
5. **겹침**: `--unassigned`와 라우팅 표를 보고 이미 같은 역할을 하는 스킬이 있으면 둘 중 하나만 남긴다.
6. **컨텍스트 비용**: description이 항상 로드된다. 플러그인 하나가 수십 개 스킬을 들여오면 `--catalog`로 토큰 추정치를 보고 결정.

흡수 완료: `ponytail`(121k★, 2026-09-04), `superpowers`(281k★, 2026-09-07 — 구현 단계 엔진으로 배선). 후보(미설치): `skill-creator`(공식 마켓, 스킬 평가 도구).
플러그인 사이 경계(누가 설계하고 누가 구현하나)는 `~/.claude/CLAUDE.md`에 사용자 지시로 둔다 — superpowers가 "사용자 지시 > 스킬"이라 명시하기 때문. 새 PC에서는 이 파일도 복사한다.
Remote Control 세션에서는 `/plugin`이 막혀 있으므로 같은 PC의 터미널에서 `claude plugin marketplace add <repo>` → `claude plugin install <name>@<marketplace>`를 쓴다.

## 다른 PC에서 쓰는 법

```bash
git clone https://github.com/kimsangchan/claude-skills "$HOME/.claude/skills"
```

Windows PowerShell:

```powershell
git clone https://github.com/kimsangchan/claude-skills "$env:USERPROFILE\.claude\skills"
```

플러그인(ecc·ponytail)은 이 저장소에 포함되지 않는다. 새 PC에서는 `/plugin marketplace add` → `/plugin install`로 따로 설치하고
`python _tools/skill_catalog.py`로 라우팅 표가 가리키는 스킬이 다 있는지 확인한다.

## 평소 동기화 루틴

- 스킬을 **고친 PC에서**: `git add . && git commit -m "무엇을 왜" && git push`
- **다른 PC에서** 세션 시작 전: `git pull`
- 원칙: 원본은 GitHub 하나. 두 PC에서 동시에 같은 스킬을 고치지 않는다.
- 스킬을 고치면 회귀 평가: autopilot은 `eval/PROTOCOL.md` 스모크(시드 3개), prompt-workflow는 `eval/` 대리 A/B.

## 모델 세대 맞춤 (2026-09-08)

Fable 5.1 기준으로 Anthropic prompt-audit(번들 `claude-api` 스킬)를 적용했다. 압력 어조·사건 서술·교육 문장·중복 절차를 뺐고,
택소노미·라우팅·검사 스크립트·형식 계약·GATE는 그대로다. 검토·판정 서브에이전트는 `_tools/agents/fresh-reviewer.md`
(`~/.claude/agents/`에 복사)로 띄우며, ponytail 페르소나는 구현 서브에이전트에만 주입된다(`~/.claude/settings.json` env
`PONYTAIL_SUBAGENT_MATCHER`). 상세와 미완 A/B는 `service-autopilot/references/evidence.md`.
