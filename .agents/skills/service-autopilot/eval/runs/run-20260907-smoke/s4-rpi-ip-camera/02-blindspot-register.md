# 블라인드스팟 레지스터 — PiCam Hub
버전: v1.0 · 2026-09-07 · 스캔: 공통 10축 + STRIDE 6범주 + 프로파일 P1(10항목) + P3(3항목) + 도메인 고유 4항목 = 33항목
모드: 오토파일럿 — 질문 배치는 작성하되 사용자에게 제시하지 않고 추천안을 `Assumed(무응답)`로 채택 (A2 프로토콜 4항).

## 제품 렌즈 (ecc:product-lens — 만들기 전 "왜")
- 문제: 소규모 사용자가 IP 카메라 여러 대를 한 화면에서 보고 조작하려면 벤더 앱(카메라마다 다름)이나 NAS·NVR(수십만 원)이 필요하다. 벤더 앱은 클라우드 경유·구독 유도, NVR은 비용. → "Pi 1대로 벤더 무관 통합 뷰 + 제어 + 상태 알림"이 채울 틈 (01-recon 유사 솔루션·상용 표).
- 대안이 이미 있는가: 객체 감지가 목적이면 **Frigate**가 정답(만들지 않는다). PiCam Hub의 차별 가치는 "가벼운 제어·상태 모니터링"이고 감지는 non-goal — 이 경계를 03에 명시.
- 성공의 정의: 카메라 4대를 브라우저 한 화면에서 1초 내 지연으로 보고, PTZ가 0.5초 내 반응하며, 카메라가 죽으면 1분 내 푸시 알림이 온다 (03 SC로 계량).

## 레지스터

| 축/항목 | 상태 | 처리 | 근거·출처 |
|---|---|---|---|
| 1. 기능 범위·행동 | Partial → **Assumed** | MVP = 카메라 발견·등록 / 실시간 그리드 뷰 / PTZ·프리셋 제어 / 온·오프라인 헬스 + 알림 / 스냅샷. P2 = 모션 이벤트 클립 녹화·보존. non-goal = AI 객체 감지, 24/7 연속 녹화, 다중 Pi 클러스터, 클라우드 계정 | 시드 "제어·실시간 모니터링" + 01 fit(Extend 접근). 24/7 녹화는 43~65GB/일/대(01 수치 표)로 SD·소형 SSD 부적합 |
| 2. 도메인·데이터 모델 | Partial → **Assumed** | 엔티티: Camera / StreamProfile / PtzPreset / HealthSample / Event / Alert / Snapshot / Clip(P2) / AdminUser(단일). 식별자 UUIDv7, 시각 UTC ISO-8601, 카메라 자격증명은 암호화 저장. 볼륨: 카메라 ≤4, 헬스 샘플 25초 간격 → ≈14k행/일 → 원본 7일 보존(시간 요약은 P2) | 01 fit(SQLite WAL) · P3 이력 증가 항목 |
| 3. 상호작용·UX 플로우 | Partial → **Assumed** | 화면 3장: ① 그리드(라이브 2×2) ② 카메라 상세(단일 뷰 + PTZ 패드 + 프리셋) ③ 설정(카메라 등록·알림 채널·보관). 권한 1종(관리자). 에러는 타일 위 상태 배지 + 재시도 CTA (L-06 막다른 에러 0) | ux-principles-kr L-03·L-06·L-10, 03 화면 스케치 |
| 4. 비기능 품질 | Missing → **Assumed** | 03 상수 표로 고정: 라이브 지연 p95 ≤ 1,000ms(WebRTC 200~500ms 근거), PTZ 명령 p95 ≤ 500ms, 오프라인 감지 ≤ 60s, 알림 도달 ≤ 60s, 동시 시청 세션 ≤ 3, CPU 5분 평균 ≤ 70%, 가용성 목표 월 99%(≈7.2h 다운 허용 — 1인 운영 현실) | 01 수치 표(forasoft·mux) · Google SRE "100% 금지" |
| 5. 통합·외부 의존성 | Partial → **Assumed** | 외부: IP 카메라(ONVIF Profile S/T + RTSP), go2rtc(로컬 프로세스), ntfy(자가호스트 또는 ntfy.sh), Tailscale(원격), NTP. 카메라 장애 시: 타일 오프라인 상태 + 지수 백오프 재연결(상한 60s). ntfy 장애: 알림 큐 로컬 보관 후 재전송(상한 100건). Tailscale 장애: LAN 직접 접근은 유지 | P1 연결 끊김 기본값 · 01 fit |
| 6. 엣지케이스·실패 처리 | Missing → **Assumed** | 03 엣지케이스 표에 Given/When/Then: 카메라 자격증명 오류, RTSP URL 변경, 동일 카메라 중복 등록(MAC 유니크), PTZ 미지원 카메라, 동시 PTZ 명령 충돌(마지막 명령 승리 + 스로틀), 디스크 잔여 하한, 시계 드리프트, 브라우저 H.265 미지원 | A3 템플릿 |
| 7. 제약·트레이드오프 | Partial → **Assumed** | 1인 자가 운영, 예산 = Pi 5 4GB + USB SSD + 전원, 인터넷 가능(NAT 뒤), 폐쇄망 아님, 기한 미지정(계획 가정: MVP 4~6주 — 근거 없는 계획 수치) | 시드에 팀·예산 언급 없음 |
| 8. 용어·일관성 | Clear | 용어집(03): 카메라=ONVIF 장치 1대, 스트림 프로파일=RTSP main/sub, 프리셋=PTZ 저장 위치, 이벤트=카메라/허브가 만든 사실, 알림=이벤트가 채널로 전달된 것, 클립=이벤트 전후 영상 파일 | — |
| 9. 완료 신호 | Missing → **Assumed** | SC-001~SC-010 전부 pass/fail 계량(03). 인수 = 06 시나리오 전부 GREEN + 07 스모크 통과 | A3·A6 게이트 |
| 10. 비용·라이선스 | Partial → **Assumed** | 런타임 비용 0원(전부 자가호스트; ntfy.sh 사용 시도 무료). 라이선스: go2rtc MIT(확인), FastAPI MIT, SQLite PD, Tailscale 클라이언트 BSD-3; onvif-zeep-async MIT·ntfy Apache-2.0/GPL-2.0 이중은 **추정**(01 미확인) → 03 '검증 대기 가정' 1. GPL(ZoneMinder) 미채택 | 01 유사 솔루션·스택 표 |
| S. Spoofing | Missing → **Assumed** | 카메라→허브: 카메라별 ONVIF 자격증명(허브가 카메라를 인증). 브라우저→허브: 관리자 로그인(argon2id) + 세션 쿠키(HttpOnly·SameSite=Strict), Tailscale 밖 접근 없음. LAN 내부 기기 위장은 Accept + MAC/IP 대조 | STRIDE · P1 디바이스 신원 |
| T. Tampering | Missing → **Assumed** | 스냅샷·클립 파일 SHA-256 기록, 설정 변경 감사 로그, SQLite WAL + 일 1회 `PRAGMA integrity_check`. RTSP 평문(카메라 대부분 TLS 미지원) → LAN 내부 Accept + 사유 | 04 위협모델 |
| R. Repudiation | Missing → **Assumed** | 감사 이벤트: 로그인, PTZ 명령, 설정 변경, 클립 삭제 — 누가·언제·무엇을 append-only 테이블 | 04 |
| I. Information Disclosure | Missing → **Assumed** | 영상은 Tailscale(WireGuard) 밖으로 나가지 않음, 카메라 자격증명 AES-GCM 암호화(키 파일 0600), 로그에 자격증명·RTSP URL 마스킹, 스냅샷 URL 서명·만료 | 04 · 개인정보보호법 §25 |
| D. Denial of Service | Missing → **Assumed** | 동시 시청 세션 상한, PTZ 명령 스로틀, 디스크 잔여 하한 시 클립 생성 중단·오래된 클립 선삭제, 카메라 재연결 백오프(폭주 방지), HW watchdog | P1 자가 복구 · P3 알람 폭주 |
| E. Elevation of Privilege | Clear(해당없음 — 권한 1종이라 수직 상승 경로 없음) → 보완 **Assumed** | go2rtc 관리 API(1984 포트)는 localhost 바인딩 + 허브 경유만 허용해 우회 제어 차단 | 04 |
| P1. SD카드 마모 | Missing → **Assumed** | rootfs SD(읽기 위주) + `noatime` + swap off + log2ram(`/var/log` RAM 상주, 1h 동기화) / DB·스냅샷·클립·**docker data-root(이미지·컨테이너·json-file 로그)**는 USB SSD `/data` | Frigate 커뮤니티 tmpfs 사례(01) · blindspot 기본값 |
| P1. 전원 차단 | Missing → **Assumed** | 공식 27W 전원. overlayfs read-only rootfs는 MVP 미채택(1인 운영의 업데이트 편의) → ext4 저널 + 부팅 fsck + SQLite WAL로 버팀. UPS는 non-goal | blindspot 기본값 부분 채택, 사유 명시 |
| P1. 자가 복구 | Missing → **Assumed** | hub-api 내부 감시 태스크: 마지막 프로브 경과 > PROBE_STALE_FACTOR×HEALTH_PROBE_INTERVAL_S면 `exit(1)` → docker `restart: unless-stopped`가 되살림(docker healthcheck는 표시만, 재시작 안 함 — GATE #1) + HW watchdog `RuntimeWatchdogSec=15` (Pi 상한 15s 준수). systemd WatchdogSec는 컨테이너 프로세스에 전달되지 않아 미채택 | blindspot 기본값(15s 상한 경고) |
| P1. OTA 업데이트 | Missing → **Assumed** | MVP: `docker compose pull` + 헬스체크 실패 시 이전 이미지 태그로 롤백 절차(07). A/B 파티션·서명 검증은 기기 1대·1인 운영이라 non-goal(비용>편익) | AWS IoT Lens 표준 대비 축소, 사유 명시 |
| P1. 시계 드리프트 | Missing → **Assumed** | 인터넷 가능 → `systemd-timesyncd` NTP. 부팅 직후 NTP 동기 전에는 TLS 의존 작업(ntfy.sh·Tailscale) 재시도. RTC 모듈 non-goal | blindspot 기본값(인터넷 가능 = NTP) |
| P1. 디바이스 신원 | Missing → **Assumed** | 허브 1대·클라우드 없음 → 기기별 X.509 fleet 신원은 해당 없음. 카메라 신원 = ONVIF 자격증명 + MAC | blindspot 기본값 축소(단일 기기) |
| P1. 연결 끊김 | Missing → **Assumed** | 카메라 끊김: 재연결 백오프 1→60s, 오프라인 이벤트 1회(중복 억제 5분). 알림 채널 끊김: 로컬 큐 100건 순서 보장 재전송, 초과 시 오래된 것 폐기 + 폐기 카운트 기록 | blindspot 기본값 |
| P1. 영상 스트림 | Partial → **Assumed** | 라이브 = go2rtc WebRTC(패스스루, 허브 트랜스코딩 없음) / 폴백 MSE. 그리드는 카메라 sub-stream(≤720p, ≤1.5Mbps), 상세는 main-stream. 동시 시청 세션 ≤3. Pi 업링크 실측은 착수 첫 작업 | 01 수치 표 · go2rtc README |
| P1. 원격 접근 | Missing → **Assumed(무응답)** | Q4 추천안: Tailscale (포트포워딩 금지) | 질문 배치 |
| P1. 프로비저닝 | Missing → **Assumed** | 기기 1대: 설치 스크립트 1개(`install.sh`: docker·compose·log2ram·watchdog·tailscale) + 첫 접속 시 관리자 비밀번호 설정 화면. 카메라는 WS-Discovery로 자동 발견 후 자격증명만 입력 | blindspot 기본값 축소(단일 기기) |
| P3. 실시간성 | Missing → **Assumed** | "실시간" = glass-to-glass p95 ≤ 1,000ms(WebRTC), 상태 변경 → 화면 반영 ≤ 3s(SSE 푸시), 알림 경로는 라이브와 분리된 우선 큐 | 01 수치 표 · P3 기본값 |
| P3. 알람 폭주 | Missing → **Assumed** | 동일 카메라·동일 유형 알림 5분 중복 억제, 네트워크 전체 단절 시 "카메라 N대 오프라인" 1건으로 그룹핑, 심각도 2단계(경고/장애) | P3 기본값 · SRE |
| P3. 이력 증가 | Missing → **Assumed** | HealthSample 원본 7일(시간 요약은 P2). Event 90일. 감사 로그 무기한(INV-6). 스냅샷 30일(법 상한과 동일). 클립(P2) 기본 7일·최대 30일. 일 1회 정리 작업 | 표준 개인정보 보호지침 30일(01) |
| D1. 영상 개인정보(법) — 도메인 고유 | Missing → **Assumed(무응답)** | Q1 추천안: 소규모 사업장(공개 장소) 기준 내장 — 보관 상한 30일, 오디오 녹음 기본 off, 설정 화면에 안내판 의무·운영방침 체크리스트. 가정 사용자에게도 해가 없음 | 질문 배치 · 01 규제 |
| D2. 기준 기기(HW) — 도메인 고유 | Missing → **Assumed(무응답)** | Q2 추천안: Pi 5 4GB + USB SSD + 27W 전원. Pi 4 4GB 지원(성능 하향) | 질문 배치 · 01 fit |
| D3. 녹화 범위 — 도메인 고유 | Missing → **Assumed(무응답)** | Q3 추천안: MVP 라이브+스냅샷(P1), 이벤트 클립 P2, 24/7 녹화 non-goal | 질문 배치 |
| D4. 카메라 대수·코덱 — 도메인 고유 | Missing → **Assumed(무응답)** | Q5 추천안: 카메라 ≤4, H.264 sub-stream 라이브, H.265는 상세 뷰에서만(브라우저 지원 시) | 질문 배치 · Pi 5 H.264 HW 없음 |

집계: 33항목 전부 마킹 — Clear 1 · Assumed 27 · Asked 5(오토파일럿 무응답 → 전부 추천안 Assumed 채택).

## 질문 배치 (최대 5) — 작성 2026-09-07 10:41, 오토파일럿이라 미제시

**Q1. 카메라가 비추는 곳은 어디인가? (개인정보보호법 §25 적용 여부 — Impact: 법 / Uncertainty: 사용자 환경)**

| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 소규모 사업장(매장·사무실 등 공개 장소) 기준으로 설계 — 보관 30일 상한·오디오 off·안내판 체크리스트를 기본 내장 | 더 엄격한 쪽을 기본값으로 두면 가정 사용자에게도 손해가 없고, 사업장 사용자는 법을 지킨 상태로 시작한다 (01 규제 절, 표준 개인정보 보호지침 30일) |
| B | 가정 내부(사적 공간) 전용 — 보관 상한 없음, 오디오 허용 | 설계 단순. 단 매장에 쓰는 순간 §25·§25조의2 위반 위험 |
| C | 둘 다 지원 — 설치 시 "장소 유형" 선택으로 정책 전환 | 유연하지만 정책 분기 코드·테스트 2배. MVP 범위 초과 |

→ 답: 무응답 → 추천안 A를 `Assumed(무응답)` 채택

**Q2. 기준 하드웨어는? (Impact: 하드웨어 재구매·성능 설계 / Uncertainty: 보유 기기·예산)**

| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | Raspberry Pi 5 4GB + USB SSD(≥256GB) + 공식 27W 전원 | H.264 HW 디코더가 없지만 패스스루 설계라 무관, CPU 여유가 크다. SSD로 SD 마모 회피 (01 fit·수치 표) |
| B | Raspberry Pi 4 4GB + USB SSD | 보유 기기 활용. H.264 HW 디코더 있으나 CPU가 낮아 카메라 상한을 3대로 낮출 수 있음 |
| C | Raspberry Pi Zero 2 W + SD만 | 최저가. RAM 512MB로 go2rtc+앱+세션 동시 부담 위험, SD 마모 대책 불가 |

→ 답: 무응답 → 추천안 A를 `Assumed(무응답)` 채택 (B는 지원 대상으로 유지, C는 비지원)

**Q3. MVP에서 녹화는 어디까지? (Impact: 저장 설계·SD 마모·법 / Uncertainty: 사용 목적)**

| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | 라이브 + 수동/이벤트 스냅샷(P1), 모션 이벤트 클립(P2) — 24/7 연속 녹화는 non-goal | 시드가 "제어·실시간 모니터링"; 연속 녹화는 43~65GB/일/대로 소형 SSD·SD에 부적합 (01 수치 표) |
| B | 24/7 연속 녹화 포함 | 증거 보전 완전. 1TB SSD·보존 정책·디스크 관리가 MVP에 들어옴 → 범위 2배 |
| C | 녹화·스냅샷 전부 제외(라이브만) | 가장 단순. 알림에 "무슨 일인지" 첨부할 수 없어 알림 가치 반감 |

→ 답: 무응답 → 추천안 A를 `Assumed(무응답)` 채택

**Q4. 집 밖에서 어떻게 접속하나? (Impact: 보안 아키텍처 / Uncertainty: 네트워크 운영 의지)**

| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | Tailscale (WireGuard 메시, 포트포워딩 없음, 무료 6사용자) | 영상이 제3자 서버를 거치지 않고 E2E. 뷰어 기기마다 클라이언트 설치 필요 (01 fit) |
| B | Cloudflare Tunnel | 클라이언트 설치 불필요(브라우저만). 영상 트래픽이 Cloudflare 경유 → 민감정보 트레이드오프 |
| C | LAN 전용 (원격 non-goal) | 가장 안전·단순. "모니터링" 가치의 절반(외출 중 확인) 상실 |

→ 답: 무응답 → 추천안 A를 `Assumed(무응답)` 채택

**Q5. 카메라 몇 대, 어떤 코덱을 기준으로 잡나? (Impact: Pi 성능·대역폭 설계 / Uncertainty: 보유 카메라)**

| 옵션 | 내용 | 근거·트레이드오프 |
|---|---|---|
| A (추천) | ≤4대, 라이브는 H.264 sub-stream(≤720p·≤1.5Mbps), 상세 뷰만 main-stream | 그리드 4타일 = 6Mbps 이하로 Pi·뷰어 대역 안전. go2rtc 커뮤니티 "Pi 4에서 10개 무리 없음"(단일 출처)의 절반 이하로 보수적 (01) |
| B | ≤8대 | 그리드 3×3, 대역 12Mbps+, 동시 세션 상한을 2로 낮춰야 함 |
| C | ≤16대 | 사실상 NVR급. Pi 단일 노드 한계 — 이 경우 Frigate/상용 NVR 재검토가 맞다 |

→ 답: 무응답 → 추천안 A를 `Assumed(무응답)` 채택

## 반영 기록
- 무응답 5건 → 전부 추천안 채택 (decision-log #5~#9). 03 상수 표에 RETENTION_MAX_DAYS=30 · MAX_CAMERAS=4 · 원격 접근=Tailscale · HW 기준 Pi 5 4GB · 녹화 범위(P1 스냅샷/P2 클립)로 고정.
- 사용자가 나중에 답을 바꾸면: 1번-B → 보관 상한 해제 + 오디오 토글 / 2번-B → MAX_CAMERAS=3 / 3번-B → 07 저장 설계 재작성 / 4번-B → 04 위협모델 I·S 재검토 / 5번-B → 세션 상한·그리드 재설계. 각각 03 개정 → 하류 전파(규칙 9).
