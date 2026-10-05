# 운영 활동 기록 확장 결과 — 2026-10-03

## 완료
- 관리자 기존 접속·활동 탭 확장: 기기/OS/앱 버전·설치 식별자, 실패 접속/앱 오류·종료 진단, 화면/버튼/이탈, 실제 아이템·XP·금화 원장, 시간별 최대 동시접속·현재 접속·재방문·D1/7/30 잔존율.
- SQLite 기존 DB에 meta 열 추가, 기존 기록 보존. 로그인 실패 이름은 활동 캐릭터 수에서 제외. 봇 실패도 서버 설정 분류로 필터링.
- 인증된 세계 연결 0xF3, 최대2048바이트·분당60개·허용 메타만. 앱은 최대120개 큐/2초 전송. 로컬 큐도90일 만료, 초기 발생 시각 유지, 앱 자기보고 시각과 서버 수신 시각 구분. 입력 글/암호/채팅/오류 메시지/스택/광고ID/위치/Wi-Fi 이름/하드웨어ID 제외.
- 다른 계정의 인증 후 미전송 행동을 재시작/로그아웃 뒤 다른 계정으로 보내지 않음. 이전 실행의 fatal 종류는 별도 진단으로 복구. unclean_exit는 OS 종료·강제 종료·크래시를 확정하지 않음. 화면 시간은 foreground만.
- 공통 재화 setter 및 아이템 변경/패킷 거래 범위로 실제 증감과 소지품/장비/은행/교환/바닥 이동을 분리. 교환 중 명시적 종료의 금화 환불·아이템 반환이 종료 기록 전에 반영됨. 순변화와 원장 중복 합산 안 함.
- idle60초 heartbeat. 90초 지난 현재 정보는 확인 지연. 잔존율은 보관된90일 안의 첫 관측 접속 기준, 해당 날짜가 끝난 코호트만 분모. 아직 분모가 없으면 자료 부족.

## 검증
- Python 전체97 통과; 최신 활동/관리 HTTP19 통과(메타/구DB 마이그레이션/CCU/코호트/실패 계정/인증).
- 웹JS32 통과. 로컬 시험데이터1440/390px: 새 표·빈 필터·자료 부족·XSS문자열 그대로·guest401·가로넘침없음.
- 앱 전체 Core695 통과(13:38 현재코드); AppActivity5개 포함. 작업 중 동시 봇 변경으로 잠시 전체시험 컴파일 실패했으나 현행 코드에서는 해소됨.
- 격리 실제 서버 ActivityLogTests3 통과(13:40, 1분15초): 미인증/잘림/초과/허용안된메타/발생시각 형식·범위/분당상한, 실패로그인, 금화 실제 -100, 아이템취득+1·장비이동delta0, 유휴heartbeat, 명시적 교환 취소/환불·최종순변화0. 앞선 CompanionCall1 통과.
- Godot 빌드0경고0오류, 앱390px 로그인 안내 촬영. 서버 빌드 성공(기존 Newtonsoft.Json12.0.3 NU1903 경고 유지).
- fresh 정확성 리뷰3건 수정(실패 계정 집계·종료 환불 순서·실패 봇 필터), 별도 보안 리뷰 신규 위험없음. 최종 diff check 통과.

## 운영 확인(13:37~13:41)
- 관리자 https://lodgame.duckdns.org/?view=activity 최신 화면/API, 실제 기기 보고1환경·원장190행·경제14집계·현재봇5/확인정상·시간별관측최대6 확인(필터bots=1, 상세최근200행).
- 운영1440/390px 스크린샷·새 표·실제 원장/기기·잔존 자료부족·JS오류없음·가로넘침없음·비로그인401·/activity.sqlite404 확인. 수집오류없음.
- 웹수집기/JS·서버DLL·아이폰/윈도우파일은 로컬과 운영 SHA256 일치. 서버·ops active, DB600·활동폴더700. collector의 원시 게임 로그 만료를 위해 service ReadWritePaths에 활동폴더를 허용.
- 동시 봇 작업의 최신 배포(서버/앱에 이번 활동 기록 포함)를 확인했으므로 추가 서버 재시작/앱 중복 업로드를 하지 않음. 공개앱 번호 iOS202610031135, Windows202610031139.
- 아이폰15Pro는 devicectl unavailable. 재연결 시도 후에도 unavailable로 직접 설치 불가. 새 signed 앱은 내려받기 페이지에 있음. 실기 설치/플레이 확인만 사용자 기기 연결 이후 남음.
- 롤백 비공개 경로: 클라우드 ~/lod-ops/rollback-activity-expansion-20261003/ (일관 DB백업+이전 수집기/화면/서버DLL). 개인 원시자료/보고 JSON/스크린샷은 out/ 비공개 작업 산출물로 유지, 공개 docs에 올리지 않음.

## 산출물
SPEC plans/activity-expansion-spec-2026-10-03.md · tasks plans/activity-expansion-tasks-2026-10-03.md
수집 scripts/ops/activity_store.py · UI docs/activity.js/index.html · 앱 Core/Diagnostics/AppActivity.cs 및 App/Main.Activity.cs · 서버 Network/Game/ActivitySession.cs 및 ClientFormats/ClientFormatF3.cs.
