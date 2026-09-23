# Recon — 사내 회의실 예약
버전: v1.0 · 조사 기준일: 2026-09-07 · 강도 lite (검색 5회 / 상한 5)

증거 경계 표기: **[출처]** = 웹에서 확인한 사실 · **[사용자]** = 입력에서 읽은 것 · **[추론]** = 그로부터 도출 · **[추천]** = 선택 제안

## 도메인 업무 흐름 — 회의실 예약은 실제로 어떻게 돌아가는가
- [출처] 그룹웨어가 있는 조직은 회의실을 "자원"으로 등록해 캘린더에서 예약한다. Microsoft 365는 room mailbox를 만들고 Outlook 회의 생성 시 Room Finder로 방을 고르면 자원 사서함이 자동 수락하며, 충돌 예약은 Exchange 관리센터 설정으로 자동 거절한다. Google Workspace는 건물·자원(회의실)을 등록하고 Calendar의 "회의실"에서 선택한다.
  - https://hybo.app/en/blog/how-to-book-meeting-rooms-with-office-365-or-google-workspace/ · https://www.add-on.com/add-a-room-calendar-in-outlook/ · https://support.google.com/a/answer/9193836?hl=en (확인 2026-09-07)
- [출처] 예약 도구의 반복 문제는 "유령 회의(ghost meeting)": 예약됐지만 아무도 안 오는 방이 주당 30~40%라는 업계 수치가 있고, 표준 대책은 **시작 후 10~15분 체크인 없으면 자동 해제(auto-release)**다.
  - https://www.deskbird.com/blog/ghost-meetings · https://www.yarooms.com/guides/fix-meeting-room-no-shows · https://www.woxday.com/blog/how-to-reduce-meeting-room-no-shows-with-auto-release (확인 2026-09-07)
- [사용자] 현재는 화이트보드 수기. [추론] 화이트보드의 한계 = 자리에서 못 봄, 중복·덮어쓰기 방지 없음, 누가 언제 적었는지 이력 없음, 취소해도 지우지 않으면 방이 잠김. 웹 전환의 핵심 가치는 **원격 가시성 + 중복 차단 + 취소 즉시 반영**이며, 유령 예약은 화이트보드에서도 이미 있던 문제다.

## 이해관계자 — 누가 쓰고, 누가 비용을 내는가
| 이해관계자 | 관심사 | 실패 시 영향 | 근거 |
|---|---|---|---|
| 일반 직원 (예약자) | 빈 방을 빨리 찾고 3클릭 내 예약, 내 예약 취소 | 화이트보드로 회귀 | [추론] 화이트보드 대체 목적 |
| 총무/관리자 (1인) | 회의실 목록·운영시간 관리, 방치 예약 정리 | 유령 예약으로 방 부족 민원 | [출처] deskbird·yarooms 위 링크 |
| IT 운영 (1인 겸임) | 설치·백업·계정 관리 최소화 | 운영 부담으로 서비스 방치 | [추론] 사내 도구·1인 운영 (seed) |
비용 부담: [추론] 사내 도구이므로 총무/IT 예산. 외부 과금·결제 없음 (P2 결제 항목 해당 없음).

## 규제·표준 — 반드시 준수해야 하는 것
- [출처] 개인정보보호법: 목적에 필요한 **최소한의 개인정보만** 수집하고, 최소 수집의 입증책임은 처리자에게 있다. 임직원 정보도 예외가 아니다 (사내 시스템에서 수집하는 직원 정보에 법 적용).
  - https://www.easylaw.go.kr/CSP/CnpClsMainBtr.laf?csmSeq=1257&ccfNo=2&cciNo=1&cnpClsNo=1 · https://www.privacy.go.kr/cmm/fms/FileDown.do?atchFileId=FILE_000000000808883&fileSn=0 (확인 2026-09-07)
- [추론] 이 서비스가 다루는 개인정보는 **이름·사내 이메일(로그인 식별)** 뿐이다. 전화번호·부서 외 항목은 수집하지 않고, 예약 이력 보존 기간을 명시하면 충족한다. 그 외 산업 규제·표준(PCI·의료·금융) 해당 없음 — 결제·민감정보가 없다.

## 유사 솔루션 3개 (lite 상한 3)
| 솔루션 | 유형 | 실제 기능 범위 | 우리 상황과의 fit | 근거 |
|---|---|---|---|---|
| Microsoft 365 room mailbox / Google Calendar 자원 | 상용(기존 그룹웨어 부속) | 캘린더에서 방 선택, 자동 수락, 충돌 자동 거절. 별도 구축 0 | **회사가 이미 M365/Workspace를 쓰면 이게 정답** — 이 패키지 전체가 불필요. 화이트보드를 쓴다는 점에서 미도입 또는 미활용으로 [추론] | https://hybo.app/en/blog/how-to-book-meeting-rooms-with-office-365-or-google-workspace/ |
| MRBS (Meeting Room Booking System) | 오픈소스, GPLv2, PHP + MySQL/PostgreSQL | 다지점 회의실, 주/일 보기, 반복 예약. 캘린더 연동·모바일 UI·알림 없음 | 기능은 충족. PHP 운영 가능하면 Adopt 후보. 모바일·알림 부재, GPLv2 | https://sourceforge.net/projects/mrbs/ · https://www.vizitorapp.com/blog/free-and-open-source-meeting-room-booking-systems-the-2026-edition/ |
| Cal.com (self-hosted) | 오픈소스, 개인 일정 예약 중심 | 외부인 예약 링크, Google/M365 동기화, 모바일 UI | 개인 캘린더 예약이 본체라 "회의실 그리드" 용도엔 과잉·부적합 | https://opencals.com/blog/open-source-booking-system |

search-first 판정: **Build(최소)**. 이유 — (1) Adopt-as-is 후보(M365/Google 자원)는 그룹웨어 보유 여부에 달려 있어 register에서 가정 처리, 착수 조건으로 확인을 요구한다. (2) MRBS는 GPLv2·PHP·모바일 부재로 "Extend"하면 스택을 그대로 물려받는다. (3) 핵심(방 × 시간 그리드 + 중복 차단 + 취소)은 엔드포인트 6개 내외로 작아 커스텀 비용이 낮다. 자세한 대안 비교는 decision-log #2.

## 스택 후보 2개 (lite 상한 2)
| 후보 | 구성 | 근거 | 트레이드오프 |
|---|---|---|---|
| **S1 (추천)** | TypeScript · Next.js(App Router, 서버 액션/Route Handler) · PostgreSQL 16 (`btree_gist` + `EXCLUDE` 제약) · Docker Compose 2 컨테이너 | 중복 예약 차단을 **DB 제약**으로 보장 — `EXCLUDE USING gist (room_id WITH =, period WITH &&)`는 앱 코드·트리거보다 동시성에 안전 (https://www.postgresql.org/docs/current/rangetypes.html · https://dev.to/franckpachot/postgresql-exclude-constraints-for-better-concurrency-than-serializable-pob). 프론트+API 단일 코드베이스로 1인 운영에 맞음 (https://nextjs.org/docs/app/getting-started/deploying) | Node 런타임·빌드 파이프라인 필요. 팀이 TS에 익숙하지 않으면 S2 |
| S2 | Python · FastAPI + Jinja2/HTMX · PostgreSQL 16 (동일 EXCLUDE 제약) | 파이썬 팀이면 학습 비용 최소, 템플릿 렌더링으로 프론트 빌드 불필요 (https://fastapi.tiangolo.com/) | 화면 상호작용(그리드 드래그 등) 확장 시 JS 비중이 다시 커짐 |
공통: SQLite 대신 PostgreSQL을 고른 이유는 범위 타입 + EXCLUDE 제약이 PostgreSQL 고유 기능이기 때문 [출처 위 rangetypes 문서]. 사용자 규모(≤100명 동시)에서 성능 차이는 설계 변수가 아니다 [추론].
fit 판단은 인기가 아니라 seed 제약(1인 운영·사내·소규모) 매칭이다. 팀 언어 역량은 미기재 → register 7축 Assumed(TS).

## 현재 시스템 감사
해당 없음 — 기존 시스템은 화이트보드 (seed).
