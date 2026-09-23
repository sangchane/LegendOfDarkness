---
name: solution-planner
description: "DEPRECATED (2026-07-09) — service-autopilot 스킬로 대체됨. 새 기획·설계 요청에는 이 스킬을 트리거하지 말고 service-autopilot을 사용하라. 이 파일은 과거 산출물(solution-planning/ 디렉토리, ICT inspection-run-ux 등)을 해석할 때만 참조한다."
---

> **⚠️ DEPRECATED**: 이 스킬은 2026-07-09에 `service-autopilot`으로 대체되었다.
> 대체 이유: ① 심문형 블라인드스팟 질문 부재 ② 설계 산출물 공백(위협모델·API계약/ERD·IaC·관측성)
> ③ 단계별 개입 과다. 재사용 가치가 있는 참조(evidence-map, quality-decomposition, ux-principles-kr)는
> service-autopilot/references/로 복사됨. 아래 본문은 과거 산출물 해석용으로만 유지.

# Solution Planner

도메인 지식이 없는 사용자를 위한 근거 기반 기획 하네스.
사용자가 빈칸을 채우는 것이 아니라, **AI가 조사하고 선택지를 가져오면 사용자는 선택만 한다.**

산출물은 GitHub Spec Kit(/specify)이나 BMAD(PRD)의 입력으로 바로 쓸 수 있는 형태로 만든다.

## 절대 규칙

1. **근거 없는 추천 금지.** 모든 추천(기술, 표준, 범위)에는 근거 소스를 명시한다. 근거 소스 선택은 `references/evidence-map.md`의 매핑 테이블을 따른다. 반드시 웹 검색으로 현재 시점 데이터를 확인하고, 학습 지식만으로 스타 수·버전·라이선스를 단정하지 않는다.
2. **인기 ≠ 적합.** fit_score는 GitHub 스타 순위가 아니라 사용자 요구사항(팀 역량, 운영 환경, 규모, 예산)과의 매칭으로 매긴다. 스타 수는 근거 중 하나일 뿐이다.
3. **질문은 루프당 최대 5개.** 그 외 모든 빈칸은 현업 기본값으로 가정하고, 가정임을 명시한다. 질문은 "가정으로 대체할 수 없는 방향성 결정"만 묻는다.
4. **모든 선택은 decision-log에 기록.** 무엇을, 왜 선택했고, 어떤 대안을 왜 버렸는지 남긴다. 이 로그가 다음 루프의 입력 상태가 된다.
5. **모르면 조사하고, 조사해도 모르면 가정하고, 가정이 위험하면 질문한다.** 이 우선순위를 뒤집지 않는다.
6. **검증 기준 없는 질적 표현 금지.** "직관적인", "간편한", "토스처럼" 같은 표현이 기획 산출물에 등장하면 반드시 측정 가능한 기준으로 변환한다. 변환 절차는 `references/quality-decomposition.md`를 따른다.
7. **하네스 자신의 변경도 A/B 게이트 통과 없이 채택 금지.** 새 방법론·기법의 발견과 채택은 `evolution/EVOLVE.md` 절차를 따르고, 채택·기각 모두 `evolution/upgrade-log.md`에 기록한다.

## 루프: Seed → Discover → Propose → Select → Generate

### 1. Seed
사용자의 한두 문장 입력을 받아 `00-seed-brief.md`로 정규화한다.
분류: 솔루션 유형, 주 도메인, 인접 도메인, 감지된 제약조건(있다면).
사용자에게 되묻지 않는다. 부족한 정보는 다음 단계에서 채운다.

### 2. Discover
`references/evidence-map.md`의 매핑에 따라 조사한다. 최소 조사 항목:
- 도메인 개념과 업무 흐름 (이 산업이 실제로 어떻게 돌아가는가)
- 이해관계자/사용자군 (누가 쓰고, 누가 돈을 내는가)
- 공식 표준·규제 (반드시 준수해야 하는 것)
- 유사 솔루션 3~5개 (오픈소스 + 상용, 실제 기능 범위 파악용)

산출: `01-domain-discovery.md`. 모든 항목에 출처 URL을 붙인다.

### 3. Propose
결정이 필요한 지점마다 **선택지 카드**를 만든다 (형식은 `references/templates.md`).
필수 결정 지점: 스코프(MVP/Standard/Enterprise), 프론트엔드, 백엔드, 데이터 저장소, 핵심 연동, AI 적용 단계(해당 시).

각 카드는 후보 2~4개 + 근거 + 트레이드오프 + 추천 1개 + 추천 이유로 구성한다.
추천 이유는 반드시 사용자 제약조건이나 Discover 결과와 연결한다.

동시에 `02-assumption-pack.md`에 가정 목록을, 마지막에 **최대 5개 질문**을 정리한다.
질문은 사용자가 선택지 번호나 짧은 답으로 답할 수 있는 형태로 만든다.

산출: `02-assumption-pack.md`, `03-scope-options.md`, `04-proposal-cards.md`

### 4. Select
사용자의 답변과 카드 선택을 받는다.
- 선택된 항목 → `decision-log.md`에 기록 (선택 + 이유 + 버린 대안)
- 무응답 항목 → 추천안을 가정으로 채택하고 가정 표시
- 답변이 기존 가정과 충돌하면 → 영향받는 카드만 재생성 (전체 재생성 금지)

### 5. Generate
확정된 결정 + 가정을 바탕으로 기획 산출물을 생성한다.
기본 산출물: `05-product-blueprint.md` (PRD 초안), `06-tech-blueprint.md` (아키텍처/스택), `07-roadmap.md` (단계별 백로그).
사용자가 Spec Kit이나 BMAD를 쓴다면 해당 입력 형식으로 변환 제안한다.

### 루프 반복
사용자가 방향을 바꾸면 Seed부터가 아니라 **영향받는 단계부터** 재실행한다.
decision-log에 기록된 확정 사항은 사용자가 명시적으로 뒤집지 않는 한 유지한다.

## 산출물 디렉토리

```
solution-planning/<solution-name>/
├─ 00-seed-brief.md
├─ 01-domain-discovery.md
├─ 02-assumption-pack.md
├─ 03-scope-options.md
├─ 04-proposal-cards.md
├─ 05-product-blueprint.md
├─ 06-tech-blueprint.md
├─ 07-roadmap.md
└─ decision-log.md
```

단계별 산출물이 완성될 때마다 파일로 저장한다. 대화에만 남기지 않는다.

## 참조 파일

- `references/evidence-map.md` — 조사 항목별 근거 소스 매핑과 조사 방법. Discover와 Propose 단계 시작 전에 읽는다.
- `references/templates.md` — 선택지 카드, decision-log, 각 산출물의 템플릿. Propose와 Generate 단계에서 읽는다.
- `references/quality-decomposition.md` — 질적 표현의 측정 가능 기준 변환 규칙. 시드나 산출물에 검증 불가능한 형용사·참조 제품 비유가 있으면 읽는다.
- `references/ux-principles-kr.md` — UI/UX 기획 적용 원칙 (토스 컨슈머 UX 가이드 14원칙 + UX 심리학 10법칙, 측정 기준 매핑 포함). 화면·인터랙션·프론트엔드 결정이 포함된 기획이면 Propose와 quality-decomposition 변환 시 읽는다.
- `eval/` — 하네스 자체 성능 검증(베이스라인 A/B). 하네스가 효과를 내는지 확인하거나 변경을 검증할 때 읽는다.
- `evolution/` — 신규 방법론 감시와 버전 채택 절차. 사용자가 "evolve", "업데이트 점검"을 요청하면 `evolution/EVOLVE.md`를 읽고 따른다.

현재 버전: v0.3.1 (변경 이력은 `evolution/upgrade-log.md`)
