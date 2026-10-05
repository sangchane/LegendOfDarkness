# 테스트 설계 — 대신 사냥
버전: v1.0 · 기준 03 v1.1 · lite: P0 FR + SC 전부

## 수용 기준 → 시나리오
| ID | 시나리오 | 레이어 | 데이터 |
|---|---|---|---|
| SC-001 / FR-003·005 | Given 격리 서버 + 대리 프로그램(작업 폴더 임시), 자동 사냥 맡김을 보낸 시험 캐릭터 When 앱 접속 소켓을 닫음 Then `HandoffDelayMax` 안에 그 캐릭터가 월드에 있고 0x13(괴물 체력) 변화가 보인다 | 통합(격리 서버, `tests/hades-characterization`) | nov·우드랜드 |
| SC-002 / FR-006·011 | Given 맡김 시간을 1분으로 줄인 시험 설정 When 대리 사냥 Then 1분 뒤 대리가 닫음 / Given 대리 프로세스를 죽임 Then 서버가 끝 + `ProxyGrace` 에 세션 Remove | 통합 | 시험용 시간 손잡이 |
| SC-003 / FR-004 | 표 시험 6줄: ①루프백·일치·미사용·시간 안·미접속 → 성공 ②원격 주소 → 거절 ③다른 이름 → 거절 ④두 번째 사용 → 거절 ⑤`TokenTtl` 지남 → 거절 ⑥이미 접속 중 → 거절(앱 안 밀림) | 단위(`HandoffTokens`) + 통합 ①⑥ | 가짜 시계 |
| SC-004 / FR-007·008 | Given 대리 접속 중 When 비밀번호로 로그인 Then 앱 세션이 월드에, 대리 끊김, 시스템 메시지에 「대신 사냥」 한 줄, 두 번째 로그인엔 없음 | 통합 | |
| SC-005 / FR-005 | Given 같은 `HuntSight` When 앱 경로(`HuntDriver.Sight` 사용)와 대리 경로 Then 같은 `HuntStep` | 단위(알맹이 `Lod.Mobile.Core.Tests`) | 지어낸 시야 |
| FR-001 | Given 자동 사냥 켬 Then 0xF1 7 JSON 이 나간다 / 끔 Then 길이 0 | 단위(알맹이 `WorldClient` 패킷 쓰기) | |
| FR-002 | Given 2049바이트 JSON Then 서버가 버리고 맡김 없음 → 끊겨도 넘김 없음 | 통합 | |
| FR-003 경계 | Given hours=10 Then until = 발급 + `ProxyHoursMax` | 단위 | |

## 계약 테스트
0xF1 7 쓰기/읽기 왕복(알맹이 쓰기 ↔ 서버 `ClientFormatF1` 읽기), 작업 파일 JSON 왕복(서버 쓰기 ↔ 대리 읽기, 같은 예시 파일).

## E2E 후보
폰 실기: 자동 사냥 켬 → 잠금 5분 → 열기 → 레벨·경험치 변화와 요약 한 줄(사용자 확인, 스크린샷 한 장).

## 리스크 기반 커버리지
열쇠 로그인(SC-003) 6경우 전부 필수 · 서버 강제 끊기(SC-002 후반) 필수 · 나머지는 대표 경로 하나.
