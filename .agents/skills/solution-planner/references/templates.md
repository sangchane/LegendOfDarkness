# Templates — 선택지 카드와 산출물 형식

## 선택지 카드 (04-proposal-cards.md 안의 반복 단위)

```yaml
decision: "시계열 데이터 저장소"          # 무엇을 결정하는가
context: "초당 1만 포인트 수집, 1년 보존, 온프레미스 가능해야 함"  # Discover에서 온 요구사항
candidates:
  - name: TimescaleDB
    evidence: "GitHub 19k stars, 최근 커밋 활발 (확인일 2026-07-06, URL)"
    fit_score: 높음
    fit_reason: "PostgreSQL 호환 → 팀 기존 역량 재활용 가능 (팀 적합성)"
    tradeoff: "초대규모 집계에서는 ClickHouse 대비 느림"
  - name: InfluxDB
    evidence: "GitHub 30k stars (확인일, URL)"
    fit_score: 중간
    fit_reason: "시계열 전용 성능은 우수하나 자체 쿼리 언어 학습 필요"
    tradeoff: "3.x 라이선스 변경 이력 → 라이선스 리스크"
recommendation: TimescaleDB
reason: "요구사항 중 온프레미스 + 기존 SQL 역량에 가중치"
question_if_needed: null   # 이 결정이 5대 질문에 올라가야 하면 질문 문구, 아니면 null
```

카드 작성 규칙: 후보 2~4개, evidence에 확인일과 URL 필수, fit_reason은 evidence-map.md의 5가지 요소 중 무엇에 근거했는지 명시.

## decision-log.md

```markdown
# Decision Log

## D-001: 시계열 데이터 저장소
- 날짜: 2026-07-06
- 결정: TimescaleDB
- 근거: 온프레미스 요구 + 팀 SQL 역량
- 버린 대안: InfluxDB (라이선스 리스크), ClickHouse (운영 복잡도)
- 상태: 사용자 확정   # 사용자 확정 | 가정(추천안 채택) | 재검토 필요
```

상태 구분이 중요하다. "가정" 상태의 결정은 이후 사용자 답변으로 뒤집힐 수 있고,
"사용자 확정"은 명시적 요청 없이 뒤집지 않는다.

## 00-seed-brief.md

```markdown
# Seed Brief
- 원문 입력: (사용자 문장 그대로)
- 솔루션 유형: (예: AI-enabled BEMS)
- 주 도메인: / 인접 도메인:
- 감지된 제약조건: (입력에서 읽어낸 것만, 추측 금지)
- 조사 시점: YYYY-MM-DD
```

## 02-assumption-pack.md

```markdown
# Assumption Pack
| # | 가정 | 근거 (현업 기본값인 이유) | 뒤집힐 경우 영향 범위 |
|---|---|---|---|
| A-01 | 초기 버전은 추천/알림까지만, 직접 제어 제외 | 안전 검증 전 제어는 업계 관례상 배제 | 09 AI 설계, 로드맵 Phase 3 |

## 확인 질문 (최대 5개)
1. (질문) — 답 안 하면 A-0N 가정 적용
```

## 03-scope-options.md

MVP / Standard / Enterprise 3단 표. 각 단에 기능 목록 + 해당 기능이 유사 솔루션 조사에서 나온 근거 표기.

## 05~07 산출물

- `05-product-blueprint.md` — PRD 초안. 문제 정의, 사용자군, 스코프(선택된 단), 기능 명세, 비기능 요구, 성공 지표. Spec Kit 사용자는 이 문서를 /specify 입력으로 쓴다.
- `06-tech-blueprint.md` — 확정 스택 + 각 선택의 decision-log 참조 번호, 아키텍처 개요, 연동 목록, 보안/규제 매핑.
- `07-roadmap.md` — Phase별 백로그. 각 항목은 "검증 가능한 완료 기준"을 갖는다.
