# GATE 리뷰어 B 판정 (santa-method 독립 리뷰어 1/2) — 부모 에이전트 중단 후 메인 세션이 저장

> 리뷰 시점 파일 mtime: 03-prd 14:26:48 / 04 14:14 / 05 14:17 / 06 14:18 / 07 14:21.
> 이후 04(14:30)·05(14:31)·06(14:33)이 갱신됐으므로 아래 지적 중 "03 v1.1 미전파" 항목은 현재 파일로 재확인할 것.

## 판정: FAIL
재실행 대상: A4·A5·A6·A7 전체(03-prd v1.1 개정이 04~07에 전파되지 않음, decision-log는 "#24~"를 참조하나 #23에서 끝남) + A3 부분 패치(FR-011 촬영범위·촬영시간, FR-019 사업장 게이트).

## 발견 (심각도순)

| # | 심각도 | 문서:섹션 | 문제 | 권장 수정 |
|---|---|---|---|---|
| 1 | CRITICAL | 03 v1.1 ↔ 04·05·06·07, decision-log | 03 개정 미전파로 정면 충돌: (a) E-12 03 "415 codec-unsupported" vs 06 "201 + codec=h265 + 경고" (b) 동시 시청 상한 03 INV-8 "세션당 ≤4, 전역 ≤16" vs 04 "≤4" vs 06 "5번째 409" vs 05 G-2 "max_viewers 1~8" — 4·8·16 공존 (c) 이벤트 03 FR-013 "마지막 모션+20초 연장(상한 120초)" vs 04/06/02 "쿨다운 30s" (d) 클립 03 FR-014 "메인스트림 ≤4Mbps…종료까지" vs 06 "28~32s" (e) SC-003 03 "시청자 4명(전역 16세션)" vs 06 "4대+시청 2". decision-log #24~ 부재 | 03 v1.1 입력으로 A4~A7 재생성, decision-log #24~ 기록, 02 반영 기록 동기화 |
| 2 | CRITICAL | 05 커버리지 매핑, 06 변환표 | 05에 FR-026(P1) 없음인데 "P0·P1 매핑 0건: 없음" 기재. 06에 SC-009, SC-001b, SC-013, FR-026, E-13, E-14 없음인데 "누락 0" 기재. 실제 P0는 16건 | 커버리지 표를 ID 전수 대조로 재작성, 카운트는 계산값으로 |
| 3 | HIGH | 05 S-3 | `GET /cameras/{id}/mse` (WebSocket) + `X-Stream-Token` 헤더 — 브라우저 WebSocket API는 커스텀 헤더 불가 → FR-004(P0) MSE 폴백 구현 불가 | 토큰을 쿼리 파라미터·Sec-WebSocket-Protocol·쿠키 중 하나로 |
| 4 | HIGH | 04 시퀀스 ↔ 05 엔드포인트 | 04 `/streams/{cam}/whep`·`/streams/{cam}/mse`·`/ptz/move` vs 05 `/cameras/{id}/whep`·`/cameras/{id}/mse`·`/ptz/moves` 세 경로 불일치 | 04를 05 경로로 통일 |
| 5 | HIGH | 03 INV-5/FR-003 ↔ 04 TB2 ↔ 07 compose | go2rtc 평문 자격증명 노출 방지 설계 부재, 04 대책 문장 파손("프록시 URL 금지 → 대신…"), recorder `network_mode: host`로 localhost 1984 도달 | go2rtc 전용 netns + api만 연결, go2rtc.yaml 자격증명 처리 명시 |
| 6 | HIGH | 03 FR-014 ↔ 04 데이터 저장 ↔ 02 Q4 | 링버퍼 04 "10s×3, 4대×30s×2Mbps≈30MB, tmpfs 64MB" vs 03 "40초, ≤4Mbps" → 80MB > 64MB ENOSPC. 02 Q4 NVMe 45GB/30일 산정도 v1.1(최대 130s·4Mbps ≈ 390GB)로 무효 | tmpfs·NVMe 재산정, 링버퍼 길이 단일화 |
| 7 | HIGH | 05 인덱스·제약 ↔ 03 INV-3·SC-005 | BEFORE UPDATE/DELETE RAISE(ABORT) 트리거와 "아카이브 후 삭제 잡" 자기모순. INV-3 해시 체인·SC-005 검증 명령이 05 ERD·07에 없음 | 파기 경로(트리거 해제 절차/파티션) + prev_hash 열 + 검증 CLI |
| 8 | HIGH | 03 FR-011·FR-021·US-3 ↔ 05 C-3·ERD·P-1 | 안내판 5항목 중 촬영범위·촬영시간이 FR-011·스키마에 없어 G-3 생성 불가. PTZ 전 범위 허용, 목적 범위 강제 장치 없음 | FR-011에 촬영범위·촬영시간 추가, 카메라별 PTZ 허용 범위/자동 복귀 FR 승격 |
| 9 | HIGH | 03 FR-019·INV-6 ↔ 02 Q1·01 규제 | 사업장 전제(Q1 Assumed)에서 녹음 금지는 예외 없음인데 FR-019가 경고 후 오디오 활성 경로 제공. INV-6 "코드 경로 부재로 강제"와 모순 | 사업장 모드에서 오디오 활성 경로 제거(Eliminate) |
| 10 | HIGH | 04 위협모델 ②·③·DFD | 근거 없는 "해당없음" 6셀(TB1-8555 E, /data S·R·E, TB4 R·E). 누락 경계: 백업 대상(master.key·DB 평문 복사), 알림 채널, 외부 tailnet 프로브. TB3-E 대책이 Pi가 먼저 연 연결로 들어오는 페이로드(ffmpeg·go2rtc·zeep XXE) 못 막음. recorder가 /data/db rw | 사유 기입, 경계 추가, 파서 격리·XXE 비활성 명시 |
| 11 | HIGH | 07 알림·SLO ↔ 03 non-goals·05 | 07 "이메일/텔레그램 선택", "외부 tailnet 프로브"가 03 non-goal·05 엔드포인트 없음·04 컴포넌트 없음과 충돌. "선택"은 미결정인데 03 "미결정 0건" | 알림을 FR로 승격하거나 07을 앱 내 배너만으로 축소 |
| 12 | HIGH | 04 컴포넌트·시퀀스 3 ↔ 07 compose | E→R record() 프로세스 내 호출인데 07은 recorder 별도 컨테이너, IPC 미정의. FR-013 v1.1 연장 신호 필요 | api↔recorder IPC(유닉스 소켓/DB 아웃박스) ADR 추가 |
| 13 | MEDIUM | 02 P1 자가복구 ↔ 07 | "헬스 실패 3회 시 재시작"이나 Docker healthcheck는 재시작 안 함(autoheal 없음). interval 30s×retries 3=90s. 비루트 1001로 :443 바인드 cap 없음 | systemd WatchdogSec 또는 autoheal, 시간값 정정, NET_BIND_SERVICE |
| 14 | MEDIUM | 03 FR-012·E-11 ↔ 05·06 | "복구 불가 파기" vs 05 unlink+fsync(복구 가능). E-11 "보수적(늦게)" 정의 없음 → NTP 미동기 시 영구 미파기 | 파기 방식 정의(FDE+키 폐기 등), 미동기 클립 상한(예: 90일) |
| 15 | MEDIUM | 03 엣지케이스 ID ↔ 05 ID 체계·enum·감사 | 05 E-1/E-2가 엔드포인트 ID와 엣지케이스 ID로 이중 사용. ptz_fault가 status enum vs 플래그. FR-010 v1.1 감사 대상 4종이 05 감사 액션에 없음 | 엔드포인트 접두 EV-, ptz_fault 별도 열, 감사 액션 4종 추가 |

## 루브릭
1 커버리지 공백 FAIL · 2 절대규칙 위반 FAIL(v1.1 수치 근거 줄 없음, decision-log #24~ 부재) · 3 모호성 CONCERNS · 4 불일치 FAIL · 5 사실 정확성 PASS(검증 권고 3건: go2rtc api.local_auth, Tailscale 무료 티어 사용자 수, 비루트 443) · 6 위협모델 FAIL · 7 운영 게이트 CONCERNS · 8 중복·과잉 CONCERNS · 9 법적 리스크 FAIL

## 거짓 양성 가능성 (리뷰어 자진 표기)
- #13 443 바인드: 베이스 이미지·sysctl로 이미 해결됐을 수 있음. 헬스체크 미재시작은 확신 높음.
- #5: go2rtc `api.local_auth` 옵션이 있으면 "무조건 통과" 전제가 깨짐. 평문 자격증명·파손 문장은 별개.
- #9: 자택 사적 이용이면 강도 낮음. 단 Q1이 사업장을 전제로 채택했으므로 그 안에서는 모순 유지.
- #8 PTZ 범위 제한: 법이 기술적 제한을 명시 요구하진 않음. 촬영범위·시간 필드 누락은 확신 높음.
- #12: SQLite 폴링을 암묵적 IPC로 의도했을 수 있음. 문서에 정의 없음은 유지.
- #14: "복구 불가"가 단순 삭제를 포함하는지 해석 여지.
