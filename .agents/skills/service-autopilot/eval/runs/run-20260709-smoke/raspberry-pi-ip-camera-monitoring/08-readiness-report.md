# Readiness Report - Raspberry Pi IP Camera Control & Monitoring

판정: CONCERNS

## 게이트 점검

| 항목 | 결과 | 근거 |
|---|---|---|
| FR -> API/event 커버리지 | PASS | P0/P1 FR 매핑 누락 0건 |
| SC -> 테스트 시나리오 커버리지 | PASS | SC-001~SC-008 모두 06-test-design에 매핑 |
| 모호한 미해결 질문 | PASS | 사용자 질문 금지 조건에 따라 추천안 자동 채택, 미해결 0건 |
| STRIDE 6범주 검토 | PASS | 모든 trust boundary에서 S/T/R/I/D/E 검토 |
| 운영 설계 | PASS | 로그/백업/복구/알림/SLO-lite 포함 |
| 근거 없는 추천 | PASS | 주요 선택에 ONVIF, Raspberry Pi, SRE, API guideline 출처 포함 |
| 구현 착수 리스크 | CONCERNS | 실제 카메라 제조사별 ONVIF 편차와 Pi 성능은 현장 장비 smoke 없이는 확정 불가 |

## 발견사항

1. HIGH - ONVIF 호환성은 문서만으로 보장되지 않는다. 구현 전 최소 3개 제조사 카메라로 discovery, RTSP, PTZ, preset smoke matrix가 필요하다.
2. HIGH - Raspberry Pi에서 4개 1080p WebRTC 동시 처리는 transcoding 여부에 따라 크게 달라진다. MVP는 passthrough 우선이고 성능 테스트 전 동시 스트림 수를 계약값으로 고정하면 안 된다.
3. MEDIUM - 폐쇄망 OTA는 인증서/서명/rollback 저장소 운영까지 포함하므로 MVP에서는 수동 이미지 업데이트 + signed package 검증으로 축소할 수 있다.
4. MEDIUM - 스냅샷도 개인정보에 해당할 수 있으므로 기본 보존 기간과 반출 정책을 사이트별 설정으로 노출해야 한다.

## 준비도 판단

- PRD/API/아키텍처/테스트/운영 설계는 구현 SPEC 작성에 충분하다.
- 단, 성능 수치와 카메라 호환 범위는 “초기 목표”이며 실제 하드웨어 smoke 결과로 조정해야 한다.
- 따라서 PASS가 아니라 CONCERNS로 판정한다.

## 구현 핸드오프

`service-prompt-workflow` SPEC 입력:

```text
/service-prompt-workflow 로 다음을 실행:
<inputs>
autopilot/raspberry-pi-ip-camera-monitoring/03-prd.md
autopilot/raspberry-pi-ip-camera-monitoring/04-architecture.md
autopilot/raspberry-pi-ip-camera-monitoring/05-api-contract.md
autopilot/raspberry-pi-ip-camera-monitoring/06-test-design.md
autopilot/raspberry-pi-ip-camera-monitoring/07-ops-design.md
</inputs>
<first_task>
SPEC.md 작성 후, MVP 1차 구현을 gateway enrollment + camera discovery + live stream session skeleton + 권한/감사 기반으로 시작할 수 있는지 평가한다.
</first_task>
```
