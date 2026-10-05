# SPEC — 운영 활동 기록 확장 (L 변경)

## 요청·완료 기준
앞서 관리자 화면에서 빠졌다고 설명한 기기/OS/앱 버전, 로그인 실패/오류, 화면/버튼/이탈, 아이템·XP·금화 상세 원장, 동시 접속/재방문/잔존율을 추가한다. 기존 접속/다운로드 통계와 관리자 인증은 유지한다.

## 흐름·계약
- 게임 JSONL의 기존 필드는 유지하고 구조화 필드 `meta`(JSON 객체) 추가. SQLite는 `meta TEXT NOT NULL DEFAULT '{}'`를 추가하는 호환 마이그레이션. 원본 문자열/개인 자료는 허용 필드만 보관.
- 앱→세계 서버 0xF3: Secured=true, `ReadStringB()`로 최대 2048 UTF-8 byte JSON 객체 읽기. `{kind, meta, count}`; kind는 app_device, app_screen, app_button, app_error, app_lifecycle. authenticated Aisling/Activity만 수락, 계정·IP·시각·접속 ID는 서버가 채운다. 입력 길이/허용 키/문자 제한, 세션당 분당60개 제한, count 1..1000. payload 원시/메시지/스택/계정 암호/채팅/입력칸/하드웨어 ID 제외.
- meta 앱 허용키: install(재설치 가능한 임의 GUID), run(실행 GUID), platform, model, os, version, screen, action, error, previousScreen, seconds, beforeLogin, occurredAt. occurredAt는 앱의 UTC 자기보고이며 서버 수신 at과 구분한다. 로컬 대기 큐도 90일 만료한다. error는 예외 타입 또는 고정 코드. 화면·버튼은 고정 코드(유저 텍스트 제외). 서버에서 `reported=true` 표식; 기기 값은 앱 자기보고이며 신원 증명 아님.
- 앱 로컬 작은 영속 기록: 시작/정상 종료 표식, 직전 화면; 다음 시작에 비정상 종료 가능성 표시. OS 강제 종료와 크래시를 구분 못 하면 `unclean_exit`로 표시. 앱 오류는 고정 종류만. 로그인 전 기록은 제한 큐로 모아 다음 성공 접속 때 보내며 해당 계정의 인증 전 기록이라는 점 표시. 실패만 하고 끝낸 앱 기록은 원격에 오지 않지만 서버 로그인 실패는 남음.
- 서버 로그인 실패: 입력 계정명(64자)·IP·고정 사유(password/account/read failure)만, 비밀번호 미수집. 분당/IP 제한. 서버가 발생시킨 기록.
- 서버 세션 heartbeat 60초. 서버 연결 종료/heartbeat 구간으로 시간별 최대 동시접속과 현재 접속(90초 stale) 집계. legacy_login은 확정 세션/잔존율에 쓰지 않는다.
- 경제 원장: 실제 변동 발생 지점에서 `ledger` meta `{transaction, asset, item, quantity, delta, balance, reason, counterparty, from, to}`. asset=gold/xp/item. 원장과 기존 state 순변화를 합산해 중복하지 않는다. 아이템 이동은 소지품/장비/은행/바닥/교환의 이동과 실제 수량 변화 구분. 인벤토리와 재화 공통 변경점 및 직접 쓰기 호출을 추적해 실패한 요청은 성공으로 기록하지 않는다. reason은 고정 코드/서버 코드 경로; 아이템명/상대계정 외 사용자 텍스트 없음. 서버 플러그인 변경도 포함.
- API 응답 확장: devices, diagnostics, screens, buttons, exits, ledger(최근200), economy(전체집계), concurrency(시간별최대), retention(첫 관측 접속 코호트 D1/7/30 및 관측성숙자 분모), returning(최초/재방문 날짜별), analyticsSince. 봇·캐릭터·한국날짜 필터 적용; 분모가 성숙하지 않으면 null.
- 관리 웹 기존 탭에서 새 표/상태/설명 추가. 기기 자기보고·비정상 종료 추정·첫 관측 기준 명시. 비로그인 데이터 미노출, textContent, 수집오류·90초지난 현재통계는 stale 표시.

## 보안·보관
90일·원시/DB 비공개·600 권한 유지. 새 의존성 없음. 수집 실패는 게임 진행을 막지 않는다. 메타 허용필드와 숫자 범위검증. 앱 안내에 실제 수집 항목/보관기간 표시. Wi-Fi 이름/GPS/연락처/광고ID/채팅/암호/전체 터치좌표 수집 안 함. 이 요청은 앞서 나열한 운영 항목의 추가이며 게임 밖 활동을 추적하지 않는다.

## 검증·배포
Python: 구DB 마이그레이션/허용메타/원장중복방지/CCU겹침/날짜경계/성숙잔존율/봇필터. 서버: 로그인 실패/인증된0xF3/입력거부/실제 재화 이동 원장. 앱: 전송인코딩/한정큐/비정상종료/버튼입력제외, core tests+Godot build. 웹 실제응답390px/데스크톱·인증/XSS. 별도 diff 리뷰+보안리뷰. 배포 전 게임 재시작 알림, 웹/서버/새 앱 내려받기 및 아이폰 설치. 다른 동시 작업 변경은 보존하며 현재 완료된 봇 변경의 배포 범위를 확인한다.

## 롤백
서버/운영 서비스/앱 이전판 복원. SQLite 일관 백업과 이전 수집기/화면을 함께 복원한다. 복원 전 신규 DB도 비공개로 보존한다. 캐릭터 저장 구조는 바꾸지 않는다.
