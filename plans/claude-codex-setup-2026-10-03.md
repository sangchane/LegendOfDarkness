# Claude 설정을 Codex에서 쓰기 — 2026-10-03

등급 M · OpenAI Docs / skill-installer · SPEC

## 목적과 완료 기준
- 현재 Claude에서 활성화한 dev 1.5.4, Superpowers 6.4.1, ECC 2.0.0-rc.1의 스킬과 개인 스킬을 Codex에 설치한다. 기존 Codex용 변형은 보존한다.
- AGENTS.md를 공통 원본으로 유지하고 폴더별 CLAUDE.md를 읽도록 한다. dev 이름과 Claude 전용 모델·도구 이름을 Codex의 실제 기능에 대응시킨다.
- NEXT-ACTION 세션 문맥과 프로젝트 명령 보호 훅은 지원 형식으로 연결하고, 로딩·동작·신뢰 상태를 확인한다.
- 설정 및 설치 목록을 백업하고 실제 Codex 스킬 목록으로 설치 결과를 검증한다.

## 범위
사용자 로컬 설정 및 스킬 설치. Claude 원본, 게임 코드, 기존 미커밋 수정, 모델·effort·MCP·권한 기본값을 보존한다. Claude 대시보드 표시와 Desktop synced 스킬은 Codex의 내장 기능과 구분해 지원 차이를 기록한다.

## 검증과 복구
TOML/JSON 파싱, 스킬 discovery와 에러 확인, 세션 문맥·명령 보호 샘플 확인, 별도 diff 리뷰. ~/.codex/backups/ 아래 원본 백업과 설치 목록으로 이번 설정만 복구할 수 있게 한다.

## 완료
272개 연결 및 실제 discovery 확인(오류 0), 공통 지침·CLAUDE.md fallback, NEXT 문맥·기존 명령 보호 훅 구성, Codex 공식 UI에서 훅 검토·활성화 완료. 상세 결과 및 이식 제한은 docs/claude-codex-setup.md. 별도 변경 리뷰 완료.
