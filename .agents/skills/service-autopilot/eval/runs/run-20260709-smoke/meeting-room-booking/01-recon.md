# Recon - 사내 회의실 웹 예약

조사 기준일: 2026-07-09

## 도메인 업무 흐름

회의실 예약은 물리 공간을 시간 단위로 배타 점유하는 업무다. Microsoft 365는 회의실을 "room mailbox"로 만들어 Outlook 일정에서 공유 자원을 예약하게 하고, Google Workspace도 건물, 기능, 회의실 리소스를 관리 콘솔에서 정의해 Calendar에서 예약하게 한다.

- Microsoft 365 room/equipment mailbox: https://learn.microsoft.com/en-us/microsoft-365/admin/manage/room-and-equipment-mailboxes
- Exchange room mailbox/resource mailbox: https://learn.microsoft.com/en-us/exchange/recipients/room-mailboxes
- Google Workspace buildings/features/calendar resources: https://knowledge.workspace.google.com/admin/calendar/create-buildings-features-and-calendar-resources
- Google resource calendar permissions: https://knowledge.workspace.google.com/admin/calendar/share-room-and-resource-calendars

화이트보드 방식에서 웹으로 전환할 때 핵심 변화는 "누가 언제 어떤 방을 쓰는지"를 전 직원이 같은 원장으로 확인하고, 중복 예약을 시스템이 원천 차단하며, 변경/취소 이력을 감사 가능하게 남기는 것이다.

## 이해관계자

| 이해관계자 | 관심사 | 실패 시 영향 |
|---|---|---|
| 일반 직원 | 빈 회의실 빠른 검색, 즉시 예약, 내 예약 변경/취소 | 회의 준비 지연, 중복 사용 충돌 |
| 팀장/예약자 | 참석 인원과 장비 조건에 맞는 방 선택 | 큰 방 낭비 또는 장비 부족 |
| 총무/관리자 | 회의실 등록, 운영 시간, 예약 정책, 이용률 확인 | 수기 개입 증가, 정책 집행 실패 |
| IT/보안 | 인증, 권한, 로그, 백업, 장애 복구 | 개인정보/업무정보 노출, 책임 추적 불가 |

## 규제·표준·보안 고려

- 직접적인 산업 규제는 낮지만, 예약 제목/참석자/부서 정보는 업무상 개인정보 또는 민감한 일정 정보가 될 수 있다.
- 캘린더 리소스 권한은 최소한 free/busy 중심 공개를 기본값으로 둔다. Google Workspace도 리소스 캘린더 권한에서 free/busy와 상세 보기 권한을 구분한다.
- API 오류는 RFC 9457 Problem JSON 형태를 채택하고 내부 경로/스택/SQL을 노출하지 않는다. 근거: https://www.rfc-editor.org/rfc/rfc9457
- REST API 설계는 Zalando RESTful API Guidelines의 리소스 중심, 문제 상세 응답, 권한 스코프 원칙을 참고한다. 근거: https://opensource.zalando.com/restful-api-guidelines/

## 유사 제품·대안

| 제품/대안 | 관찰 기능 | 적용 판단 |
|---|---|---|
| Microsoft 365 room mailbox | 조직 계정 기반 방 예약, Outlook 일정과 공유 자원 통합 | 이미 Microsoft 365를 쓰는 조직이면 2단계 연동 후보 |
| Google Workspace Calendar resources | 건물/기능/회의실 리소스 정의, free/busy 권한 | Google Workspace 사용 조직이면 2단계 연동 후보 |
| Skedda | 회의실/공유공간 예약, 접근 제어, 예약 쿼터, 사전 예약 기간, 승인 워크플로 | 정책 엔진 요구사항의 좋은 참조 |
| Envoy Rooms | 체크인 알림, 작은 방 사용 유도, 이용률 차트 | 노쇼/공간 낭비 대응 기능 참조 |
| Robin | 공간 예약, 실시간 데이터, AI 기반 예약, 사무실 지도/분석 | 확장 단계의 지도/분석 참조 |

출처:
- Skedda room booking: https://www.skedda.com/platform/meeting-room-booking-system
- Envoy conference room scheduling: https://envoy.com/products/conference-room-scheduling-software
- Robin workplace management: https://robinpowered.com/

## 스택 후보

| 후보 | 장점 | 단점 | fit |
|---|---|---|---|
| 기존 사내 웹 스택에 모듈 추가 | 인증/권한/배포/백업 재사용, 온프레미스 가능 | 기존 제품 범위와 충돌 가능 | 높음 |
| Microsoft/Google 리소스 캘린더만 사용 | 빠른 도입, 익숙한 캘린더 UI | 화이트보드 대체용 현장 화면/정책/분석 커스터마이징 제한 | 중간 |
| 전문 SaaS 구매 | 기능 풍부, 운영 부담 낮음 | 비용, 데이터 위치, 사내 정책 제약 | 중간 |

추천: MVP는 기존 사내 웹 스택에 독립 예약 원장을 만들고, Microsoft/Google Calendar 양방향 동기화는 P2로 둔다. 이유는 입력 아이디어가 "웹에서 하고 싶다"이며 현재 수기 운영이므로, 초기 성공은 범용 캘린더 통합보다 중복 예약 차단과 전사 가시성에 있다.
