# Watchlist — 감시 대상과 탐지 쿼리

## 기존 채택·참조 기법의 원천 (릴리스 노트 확인)

| 대상 | 확인 위치 | 우리와의 관계 |
|---|---|---|
| GitHub Spec Kit | github/spec-kit 릴리스, 공식 블로그 | 산출물이 /specify 입력으로 흘러감. constitution 방식 도전자 후보 |
| BMAD-METHOD | bmad-code-org/BMAD-METHOD 릴리스 | Phase 1(Analyst) 구조, 게이트 판정 방식 참조 |
| Claude Code 스킬/커맨드 사양 | Anthropic 공식 문서 (docs.claude.com) | 하네스의 실행 기반. 사양 변경 시 구조 영향 |
| EARS 표기법 | 관련 표준 문서 | 규칙 6의 요구사항 문장 형식 |

## 신규 진입자 탐지 쿼리

Evolve 실행 시 웹 검색으로 확인한다.
- "spec-driven development" 최신 비교 글 (최근 3개월)
- GitHub topic: spec-driven-development 스타 급상승 저장소
- "AI planning framework" / "requirements elicitation AI" 신규 도구
- Claude Code 커뮤니티에서 언급 빈도가 급증한 워크플로우

탐지 기준: 스타 수 자체보다 (1) 최근 90일 성장 속도, (2) 독립 소스 2개 이상의 실사용 후기, (3) 우리 루브릭 항목과 연결되는 구체 기법 보유 여부.

## 점검 주기

권장 월 1회, 수동 실행 (A안). 마지막 점검일은 upgrade-log.md 최상단에 기록.
