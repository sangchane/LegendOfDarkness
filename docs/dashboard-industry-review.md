# LOD 개발 대시보드 현업 구성 조사

> 조사일: 2026-09-10  
> 범위: AI 보조 개발, 개발 과정, 운영, 유지보수, 내부 개발자 포털  
> 결론: 지금 단계에서는 Backstage 같은 플랫폼을 설치하기보다 그 정보 모델을 `docs/index.html`에 작게 적용한다.

## 결론

AI가 코드를 더 많이 작성할수록 사람이 봐야 할 것은 코드의 양이 아니라 **의도, 책임 경계, 흐름, 변경 근거, 운영 결과**다. LOD 대시보드의 주 탐색축은 다음 질문에 답해야 한다.

1. 무엇이 어디에 있으며 누가/무엇이 책임지는가?
2. 로그인·이동·전투 같은 기능은 어떤 구성요소와 데이터를 통과하는가?
3. 변경은 어떤 테스트와 검토를 거쳐 배포되는가?
4. 장애가 나면 어떤 신호로 발견하고 어떻게 복구하는가?
5. 표시된 사실은 언제, 어떤 원문에서 확인했는가?

Backstage의 카탈로그는 서비스·라이브러리·데이터 파이프라인의 소유권과 메타데이터를 한곳에서 찾게 하고, 관계 그래프로 의존성과 생명주기를 표현한다. 중요한 점은 이 그래프가 모든 것을 긁어 모은 목록이 아니라 **사람의 시스템 mental model**이어야 하며, 카탈로그 자체를 최종 원본으로 취급하지 않는다는 것이다. LOD도 문서와 저장소를 원본으로 유지하고 대시보드는 탐색용 캐시로 취급하는 편이 맞다. [Backstage Software Catalog](https://backstage.io/docs/features/software-catalog/), [Creating the Catalog Graph](https://backstage.io/docs/features/software-catalog/creating-the-catalog-graph/)

C4 모델도 대부분의 팀에는 system context와 container 수준이면 충분하며, 코드 수준까지 항상 내려갈 필요가 없다고 설명한다. LOD에는 `시스템 지도`와 기능별 dynamic flow가 적당하고, 클래스 전체 다이어그램은 과하다. [C4 diagrams](https://c4model.com/diagrams)

## 현업 대시보드에서 반복되는 정보 구조

| 영역 | 현업 패턴 | LOD 적용 |
|---|---|---|
| 소프트웨어 카탈로그 | 구성요소, 유형, 책임, 소유, 생명주기, 의존 관계, 문서/API 링크 | Godot·프로토콜·Hades·콘텐츠·자산·검증을 7개 경계로 표시 |
| 기능 흐름 | 사용자 행동부터 클라이언트·계약·서버·데이터·운영 신호까지 추적 | 로그인, 이동, 전투, 콘텐츠 배포의 end-to-end 흐름 |
| 전달 흐름 | 작업, PR, 테스트, 빌드, 배포와 근거를 연결 | 로드맵과 Graphite 스택을 같은 화면에 배치 |
| 운영 | 사용자 중심 SLI/SLO, 로그·지표·추적, 알림, runbook, incident, postmortem | 서비스 전에는 `미계측`; 공개 서버 gate 이후 연결 |
| 유지보수 | 의존성, 취약점, 라이선스, EOL, 데이터 마이그레이션, 빌드 provenance | .NET 5 이전, SBOM, 자산 출처, 복구 리허설을 큐로 관리 |
| 문서 신뢰성 | docs-like-code, 원문과 가까운 문서, 소유/갱신일/상태 | 각 화면에 원문 링크와 근거 기준일 표시 |

GitHub Projects는 issue·PR과 동기화되는 table/board/roadmap, 사용자 정의 필드와 chart를 제공한다. 즉 로드맵의 핵심은 예쁜 타임라인보다 실제 작업 원본과의 동기화다. 현재 LOD는 정적 페이지이므로 숫자가 자동으로 갱신되는 것처럼 보이지 않게 해야 한다. [GitHub Projects](https://docs.github.com/en/issues/planning-and-tracking-with-projects), [Projects insights](https://docs.github.com/en/issues/planning-and-tracking-with-projects/viewing-insights-from-your-project/about-insights-for-projects)

DORA는 현재 전달 성능을 throughput 세 항목과 instability 두 항목으로 본다. 하지만 한 서비스의 추세를 맥락 안에서 측정해야 하며, 서비스가 배포되지 않은 현 단계에서 임의의 DORA 숫자를 만드는 것은 의미가 없다. 지금은 commit→review→test→artifact의 단계와 대기 지점을 먼저 기록하고, 배포가 시작되면 lead time·deployment frequency·failed deployment recovery time·change fail rate·deployment rework rate를 붙인다. [DORA software delivery performance metrics](https://dora.dev/guides/dora-metrics/), [DORA value stream mapping](https://dora.dev/guides/value-stream-management/)

## 기존 화면에서 부족했던 부분

- `제품 축 72%` 같은 수치에 계산식·원본·갱신 규칙이 없어 정확해 보이지만 판단 근거가 없었다.
- “서버”, “클라이언트”, “자산”은 보였지만 책임·위치·생명주기·의존 관계로 이동할 수 없었다.
- 로드맵은 작업 상태만 보여 주고 기능이 실제로 어느 계층과 파일, 테스트, 운영 신호를 통과하는지 보여 주지 못했다.
- Graphite가 별도 섬으로 있어 로드맵과 변경 전달의 관계가 끊겼다.
- 운영 준비는 체크리스트였지만 SLO, 복구 목표, incident 역할, postmortem 조치 추적, 의존성/EOL이 없었다.
- 스냅샷 값과 실시간 값의 시각적 구분, 기준일, source of truth가 충분하지 않았다.
- 게임 데이터 변경이 검증·버전·배포·롤백되는 콘텐츠 운영 흐름이 없었다.

## 불필요하거나 아직 이른 부분

- 근거 없는 전체 완성도 백분율과 출처 없는 고정 테스트 수는 제거한다.
- 배포 전 단계에서 실시간 CPU, 접속자, DORA 등 운영 차트를 흉내 내지 않는다.
- 모든 클래스와 파일을 그래프로 만들지 않는다. 시스템 지도는 사람이 기억해야 할 경계만 가진다.
- 지금 당장 Backstage, Prometheus, Grafana, OpenTelemetry backend를 운영하지 않는다. 정적 포털이 감당 못할 규모나 실제 서비스 계측 필요가 생길 때 도입한다.
- 화면 실험실은 유효하지만 개발 전체를 대표하는 최상위 상태 지표는 아니다. 별도 HTML 없이 index의 도구 영역으로 유지한다.

## 블라인드 스팟과 활성화 조건

### 개발 과정

- 요구/결정(ADR) → 변경 스택 → 테스트 → 빌드 아티팩트의 traceability
- 기능별 계약 테스트와 실패 시 영향 범위
- AI가 수정한 기능 흐름, 사람이 검토한 경계, 생성 근거
- 기다림과 재작업을 포함한 value stream

### 운영

- 사용자 관점 SLI/SLO와 오류 예산
- 로그인·월드 입장·이동·전투 trace의 상관관계
- 알림 심각도, 담당 역할, runbook, 롤백
- incident 타임라인, root cause, 후속 조치 완료 추적

Google SRE는 monitoring의 목적을 알림, 진단, 시각화, 장기 추세, 변경 전후 비교로 구분하며 대상 독자마다 다른 보기가 필요하다고 설명한다. OpenTelemetry는 traces·metrics·logs를 서로 다른 질문에 답하는 신호로 구분한다. 따라서 LOD가 실행 서비스를 갖추면 기능 흐름의 각 단계에 동일한 correlation context를 전달하는 것이 좋다. [Google SRE Monitoring](https://sre.google/workbook/monitoring/), [OpenTelemetry Signals](https://opentelemetry.io/docs/concepts/signals/), [OpenTelemetry observability primer](https://opentelemetry.io/docs/concepts/observability-primer/)

Google SRE의 postmortem 사례는 incident 역할, 상세 timeline, 영향 서비스, 심각도, 탐지 수단을 자동 수집하고 후속 조치를 중앙 이슈로 추적한다. 단순한 “장애 대응 필요” 체크박스만으로는 부족하다. [Google SRE Postmortem Culture](https://sre.google/workbook/postmortem-culture/)

### 유지보수와 공급망

- 지원 종료 런타임과 이전 목표일
- 직접·전이 의존성, 라이선스, 알려진 취약점, SBOM
- 어떤 source revision과 build process가 모바일/서버 artifact를 만들었는지
- 저장 포맷·프로토콜·자산 버전의 호환/마이그레이션 정책
- RPO/RTO, 백업 성공이 아니라 실제 복원 성공 기록

GitHub dependency graph는 manifest/lockfile에서 직접·전이 의존성, 라이선스, 취약점 경로와 SBOM을 제공한다. SLSA Build L1의 첫 실용 목표는 artifact가 어떤 입력과 build process에서 만들어졌는지 provenance를 남기는 것이다. [GitHub dependency graph](https://docs.github.com/en/code-security/concepts/supply-chain-security/dependency-graph), [SLSA Build Track](https://slsa.dev/spec/v1.2/build-track-basics)

## 단계별 적용

1. **지금 — 정적 mental model:** 시스템 지도, 네 개 기능 흐름, 근거/상태/기준일, 운영·유지보수 공백을 index에 표시한다.
2. **실행 검증 자동화 — 생성 스냅샷:** 테스트 결과, Graphite/Git 상태, dependency inventory를 스크립트가 JSON으로 생성하게 한다. 수동 숫자는 생성 시각을 표시한다.
3. **공개 테스트 서버 — 관측 연결:** 로그인·입장·이동·전투의 traces/metrics/logs와 사용자 중심 SLO를 연결한다.
4. **반복 배포 — 전달 성능:** 서비스 단위 DORA 추세, incident와 rollback, build provenance/SBOM을 연결한다.

문서는 코드와 함께 버전 관리하는 docs-like-code 방식을 유지한다. 이는 별도 위키보다 원문 변경과 함께 검토되며, 대시보드는 탐색과 요약에 집중하게 한다. [Backstage TechDocs](https://backstage.io/docs/features/techdocs/creating-and-publishing/)

## 이번 화면 변경에 반영한 판단

- 메뉴를 `대시보드 / 시스템 지도 / 기능 흐름 / 개발·Graphite / 게임 데이터 / 운영·유지보수 / 화면 실험실`로 재구성
- Graphite를 로드맵과 합쳐 “변경이 어떻게 전달되는가”라는 한 맥락으로 정리
- 제품별 임의 백분율을 제거하고 현재 gate·위험·근거 기준일로 교체
- 구성요소 7개와 로그인·이동·전투·콘텐츠 배포 흐름을 데이터 모델로 추가
- 각 기능 단계에 위치, 검증 근거, 필요한 운영 신호를 표시
- 운영 화면에 신뢰성, incident, 복구, EOL/의존성, release provenance를 추가
- 실시간 계측이 없는 값은 명시적으로 `미계측` 처리

