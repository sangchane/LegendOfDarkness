# Readiness Report - 요양시설 낙상 감지 알림 서비스

판정: CONCERNS

## 게이트 검토

| 항목 | 결과 | 근거 |
|---|---|---|
| FR -> API 커버리지 | PASS | 05-api-contract에서 P0/P1 100% 매핑 |
| SC -> 테스트 커버리지 | PASS | 06-test-design에서 SC/FR별 시나리오 포함 |
| STRIDE 검토 | PASS | 04-architecture에서 6범주와 trust boundary 검토 |
| 블라인드스팟 등록 | PASS | 02-blindspot-register에 질문·가정·자동채택 기록 |
| 운영 로그·백업·복구 | PASS | 07-ops-design에 위치, 주기, 절차, RTO/RPO 포함 |
| 규제 포지셔닝 | CONCERNS | 한국/미국 기준을 함께 검토했으나 실제 출시국, 의료기기 해당성, 영상 사용 여부는 법무 검토 필요 |
| 감지 성능 | CONCERNS | 센서 융합을 채택했지만 실제 민감도/특이도는 파일럿 데이터 없이는 확정 불가 |
| 보호자 알림 정책 | CONCERNS | 직원 확인 우선 정책은 합리적이나 시설 계약과 보호자 동의 문구 검증 필요 |

## 발견 사항

- HIGH: 낙상 감지 성능은 제품 요구사항이 아니라 파일럿 검증 목표로 남겨야 한다. PRD의 SC-004가 이를 반영한다.
- HIGH: 영상 모듈을 추가하면 개인정보, 영상정보, 보관·열람 정책이 별도 제품 범위가 된다. MVP에서는 제외해야 한다.
- MEDIUM: 직원 알림 10초 p95는 provider와 단말 상태에 영향을 받는다. 현장 로컬 경보를 병행해야 한다.
- MEDIUM: 보호자에게 원시 센서값을 공개하면 오해와 개인정보 리스크가 커진다. guardian-safe projection을 유지해야 한다.

## 준비도

- 구현 준비도: 7/10
- 파일럿 준비도: 6/10
- 규제·법무 준비도: 4/10
- 운영 준비도: 7/10

## 구현 핸드오프

service-prompt-workflow SPEC 입력:

```text
inputs:
  - autopilot/s8-elder-fall-alert/03-prd.md
  - autopilot/s8-elder-fall-alert/04-architecture.md
  - autopilot/s8-elder-fall-alert/05-api-contract.md
  - autopilot/s8-elder-fall-alert/06-test-design.md
  - autopilot/s8-elder-fall-alert/07-ops-design.md
first_task:
  SPEC.md를 작성하고, 센서 이벤트 수신 -> 직원 확인 -> 보호자 알림의 P0 경로만 1차 구현한다.
constraints:
  - 영상 AI 제외
  - 의료 진단 표현 제외
  - 멀티테넌트 권한과 감사로그 우선
```

