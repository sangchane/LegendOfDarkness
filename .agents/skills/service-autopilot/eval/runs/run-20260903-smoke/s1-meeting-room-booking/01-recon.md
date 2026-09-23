# Recon — 사내 회의실 웹 예약 (RoomBook)

조사 기준일: 2026-09-03 · 방법: WebSearch(11회) + 공식 문서 우선 (`evidence-map.md` 절차)
증거 경계 표기 (ecc:research-ops): **[사실]** 출처 있는 사실 · **[입력]** 사용자 제공 · **[추론]** 사실에서 도출 · **[추천]** 판단

## 도메인 업무 흐름 — 회의실 예약은 실제로 어떻게 돌아가는가

- **[사실]** 기업용 표준 흐름은 "회의실 = 캘린더 자원(resource)"이다. Microsoft 365는 회의실을 room mailbox로 만들고 Outlook 일정에서 초대하면 가용 시 자동 수락(AutomateProcessing=AutoAccept)한다. 예약 가능자·승인 필요자를 분리 설정할 수 있다.
  - https://learn.microsoft.com/en-us/exchange/recipients/room-mailboxes (확인 2026-09-03)
  - https://learn.microsoft.com/en-us/answers/questions/4667220/automatic-process-of-room-reservation-requests-in (확인 2026-09-03)
- **[사실]** Google Workspace는 Admin Console > Directory > Buildings and resources 에서 회의실을 자원으로 등록(수용 인원·층·설비)하며, 모든 유료 플랜에 포함이다. free/busy만 공개하고 상세는 숨기는 설정이 있다.
  - https://support.google.com/a/answer/9025584 (확인 2026-09-03)
  - https://knowledge.workspace.google.com/admin/calendar/allow-free-busy-google-calendar-room-booking (확인 2026-09-03)
- **[사실]** 예약 시스템 도입 후 가장 큰 운영 문제는 "고스트 미팅(예약 후 미사용)"이다. 워크플레이스 분석사 자료로 주당 예약 회의실의 30~45%가 미사용, 체크인 없는 조직은 50% 초과. 표준 대책은 체크인 + 유예시간(10~15분) 후 자동 해제.
  - https://myseat.io/meeting-room-analytics-utilization-tips/ (확인 2026-09-03)
  - https://www.deskbird.com/blog/ghost-meetings (확인 2026-09-03)
  - https://www.worklytics.co/resources/stop-ghost-meetings-microsoft-teams-check-in-auto-release-reduce-no-shows (확인 2026-09-03)
- **[입력]** 현재는 화이트보드 수기. → **[추론]** 현 상태의 실패 모드는 (a) 자리를 떠나야 현황을 알 수 있음, (b) 중복 기입·지우개 분쟁, (c) 변경·취소 이력 없음, (d) 노쇼여도 칸이 남아 있음. 시스템의 첫 가치는 "어디서나 현황 확인 + 중복 예약 원천 차단"이다.

## 이해관계자 — 누가 쓰고, 누가 책임지는가

| 이해관계자 | 관심사 | 실패 시 영향 | 근거 |
|---|---|---|---|
| 일반 직원 (예약자) | 빈 방을 10초 안에 찾고 예약, 내 예약 변경·취소 | 화이트보드보다 느리면 이탈 → 도입 실패 | [추론] + 자동 해제/체크인 관행 (deskbird) |
| 회의 참석자 | 어느 방인지 알림 | 방 못 찾아 회의 지연 | [추론] |
| 총무/시설 담당 (관리자) | 회의실 마스터 관리, 노쇼·점유율 파악, 분쟁 중재 | 운영 부담 증가 | myseat.io 지표(점유율·노쇼율) |
| IT 담당 | 인증 연동, 배포·백업, 개인정보 | 보안 사고·복구 불능 | P2 프로파일 |
| 비용 결정자 (경영지원) | 상용 SaaS 대비 구축 비용 | 과잉 투자 | 아래 유사 솔루션 가격 |

돈을 내는 주체는 회사(내부 도구, 과금 없음). **[추론]** 결제·구독 축은 해당 없음.

## 규제·표준 — 반드시 준수해야 하는 것

- **[사실] 개인정보보호법(국내)**: 직원 이름·이메일·예약 이력은 개인정보. 최소 수집, 목적 달성 시 지체 없는 파기, 취급자 최소 지정이 원칙. 사내 도구라도 적용된다.
  - https://www.privacy.go.kr/front/contents/cntntsView.do?contsNo=121 (확인 2026-09-03)
  - https://www.catchsecu.com/archives/13133 (보유기간 해설, 확인 2026-09-03)
- **[추론]** 결제·의료·금융 규제 해당 없음(내부 도구, 과금 없음, 민감정보 없음). GDPR은 EU 거주 직원이 없다는 가정 하에 해당 없음 → register `Assumed`.
- **[사실] 인증 표준**: SSO 연동 시 OIDC(OpenID Connect)가 사실상 표준이며 M365(Entra ID)·Google 모두 OIDC IdP다. 초기엔 설계만 열어둔다(P2 기본값).
  - https://openid.net/specs/openid-connect-core-1_0.html (확인 2026-09-03)
- **[사실] 시간 표현**: 예약은 시간 구간이므로 저장은 UTC·교환은 ISO 8601, DB는 range 타입이 표준 관행 (아래 스택 근거).

## 유사 솔루션 — 실제 기능 범위 (오픈소스 + 상용)

| 솔루션 | 유형/라이선스 | 핵심 기능 | 비용 | 우리 fit | 출처 |
|---|---|---|---|---|---|
| **M365 room mailbox** | 상용 번들(기존 구독에 포함) | 자동 수락, 예약 가능자 제한, 승인 정책, Outlook/Teams 통합 | 추가 비용 0 (M365 보유 시) | **높음 — 조직이 M365면 "만들지 말고 설정"** | learn.microsoft.com (위) |
| **Google Calendar 자원** | 상용 번들 | 회의실 자원, 층·설비, free/busy 공개, 자동 방 추천 | 추가 비용 0 (Workspace 보유 시) | **높음 — 조직이 Google이면 설정만** | support.google.com (위) |
| **MRBS** | 오픈소스 GPLv2, PHP+MySQL/PostgreSQL | 회의실/구역 관리, 반복 예약, 승인, 다국어. v1.12.2 (2026-05-26) 활발 유지 | 호스팅만 | 중간 — 기능은 충분하나 UI 노후·PHP 운영 역량 필요 | https://github.com/meeting-room-booking-system/mrbs-code · https://www.vizitorapp.com/blog/free-and-open-source-meeting-room-booking-systems-the-2026-edition/ (확인 2026-09-03) |
| **Cal.com** | 오픈소스 AGPLv3 (Cal.diy 포크는 MIT) | 사람 일정 예약(Calendly 대체), 팀 라운드로빈. 회의실 자원 예약은 주 목적 아님 | 호스팅만 (상용 라이선스는 30석 이상) | 낮음 — 사람 예약이 목적, 회의실 자원 모델 없음 | https://cal.com/blog/cal-diy-open-source-to-closed-source (확인 2026-09-03) |
| **Skedda** | 상용 SaaS | 공간 예약, 반복, 캘린더 뷰, 셀프서비스 | Starter $99/월(15 공간)~Premier $199/월(25 공간) | 중간 — 즉시 사용 가능하나 월 고정비, 국내 SSO·데이터 위치 검토 필요 | https://www.capterra.com/p/132372/Skedda-Bookings/pricing/ (확인 2026-09-03) |
| **Robin** | 상용 SaaS | 회의실+데스크, 체크인/자동 해제, 분석 | 자원당 약 $4~6/월, 연 $15,000~ 플랜 | 낮음 — 소규모 사내 도구엔 과잉 | https://www.vendr.com/marketplace/robin (확인 2026-09-03) |

**[추천] search-first 판정 (Adopt / Extend / Build)**
1. 조직이 **M365 또는 Google Workspace를 이미 쓰고 있다면 → Adopt(설정)**. 구축 자체가 불필요. 이 조건은 조사로 알 수 없어 A2 질문 Q1로 승격.
2. 둘 다 없거나(또는 캘린더 문화가 없어 화이트보드를 쓰는 조직) 웹 전용 도구를 원하면 → **Build (경량)**. MRBS Extend도 후보였으나 PHP/노후 UI/커스터마이즈 비용으로 "얇은 자체 구축"이 총비용에서 유리 **[추론]** — 도메인 핵심(시간 구간 중복 방지)은 DB 제약 한 줄로 해결되므로 구축 난이도가 낮다.
3. 상용 SaaS(Skedda 등)는 "월 $99~ 고정비 vs 내부 개발 1~2주"의 비교 문제 — 비용 결정자에게 선택지로 남기고, 이 패키지는 Build 경로를 설계한다.

## 스택 후보 — 후보별 근거·트레이드오프 (인기 ≠ 적합)

| 후보 | 근거 | fit | 트레이드오프 |
|---|---|---|---|
| **PostgreSQL 16+ (range + EXCLUDE 제약)** | `EXCLUDE USING gist (room_id WITH =, period WITH &&)` 로 동시 요청에서도 중복 예약을 DB 수준에서 원천 차단. btree_gist 확장 필요. PostgreSQL 18은 temporal constraint 문법 추가 | **높음** — 요구 1순위(중복 차단)를 직접 충족 | 운영자가 PostgreSQL을 알아야 함(가정: 소규모 IT라도 Docker 운영 가능) · https://www.postgresql.org/docs/current/rangetypes.html · https://dev.to/franckpachot/postgresql-exclude-constraints-for-better-concurrency-than-serializable-pob (확인 2026-09-03) |
| **Next.js 16 (React, App Router) + TypeScript** | v16.3.x Active LTS, 보안 지원 2027-10 예정. 단일 저장소에 UI+API(Route Handlers) 동거 → 소규모 팀 운영 단순 | **높음** — "웹에서" 요구 + 한 컨테이너 배포 | 프레임워크 학습 곡선, SSR 불필요하면 과잉일 수 있음 · https://nextjs.org/blog/next-16 · https://versionlog.com/nextjs/16/ (확인 2026-09-03) |
| **FastAPI + React(Vite)** (대안) | Python 팀이면 백엔드 생산성 높음 | 중간 — 팀 역량에 따라 | 저장소·배포 2개, 타입 공유 별도 작업 |
| **MRBS 채택 후 커스터마이즈** (대안) | 검증된 기능 세트, GPLv2 | 중간 | PHP, GPL 파생물 공개 의무(사내 배포면 무관), UI 노후 |
| **인증: 사내 IdP OIDC(있으면) / 없으면 이메일 매직링크** | OIDC 표준. 비밀번호 저장을 피하면 자격증명 유출 리스크 자체 제거 | 높음 | IdP 없으면 SMTP 의존 → 메일 발송 실패 = 로그인 불가 (A4 위협모델·A7 알림에 반영) |
| **배포: Docker Compose 단일 호스트** | 사내 도구 규모(가정 ≤ 300명, 회의실 ≤ 30)엔 충분 | 높음 | HA 없음 — 장애 시 화이트보드 폴백(운영 절차로 흡수) |

**[추천] 최종 후보**: Next.js 16 + TypeScript + PostgreSQL 16(range/EXCLUDE) + Docker Compose, 인증은 OIDC 우선·매직링크 폴백. fit 근거: 요구 충족(중복 차단·웹)·운영 단순성(컨테이너 1~2개)·총비용(라이선스 0). 팀 역량은 미상 → register `Assumed`(TypeScript 가능 인력 1명 이상).

## 반복 조사 필요성 (research-ops 5단계)
일회성 조사로 충분. 단, "Skedda/Robin 가격·M365 정책"은 연 1회 재확인 권고 (Build 유지 근거 재검토).
