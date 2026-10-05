# 접속·활동 Implementation Plan

> **For agentic workers:** superpowers:executing-plans로 같은 세션에서 직접 구현한다.

**Goal:** 관리자만 웹 전송과 캐릭터 접속·게임 활동을 조회한다.
**Architecture:** nginx JSON/게임 JSONL → stdlib SQLite → 기존 인증 API/탭. 신규 패키지 없음.
**Tech Stack:** Python stdlib, nginx, C# .NET 9, 기존 vanilla JS/CSS.
**Spec:** plans/access-activity-spec-2026-10-03.md

## Global Constraints
90일 보관, KST 날짜, 비밀번호/채팅/원시 패킷 제외, 공개 docs에 원시 로그 금지. 원작 submodule 대신 기존 sangchane fork 수정. 사용자 요청대로 구현까지 진행, 추가 승인·커밋·배포 절차를 스킬만으로 만들지 않는다.

## Review Focus
HEAD 제외, 부분 전송을 설치 완료로 오인하지 않기, 로그 회전/불완전 줄/재시작 중복, 관리자 로그아웃 때 데이터 지우기, 실패 기록이 게임을 중단하지 않기.

## Task 1: 저장·집계/API
Files: scripts/ops/activity_store.py, ability-ops-service.py, tests/test_activity_store.py, test_ability_ops_service.py.
- [x] 날짜/HEAD/봇/필터/회전 중복/401 시험을 먼저 작성하고 실패 확인.
- [x] SQLite events와 offsets, collect()/report() 구현. API 인증 뒤 호출, 수집은 백그라운드 30초.
- [x] unittest를 실행하고 수정.

## Task 2: 웹 방문 수집 + 탭
Files: cloud-dashboard.sh, docs/{index.html,dashboard.js,dashboard-model.js,activity.js,activity.css}.
- [x] nginx request_id 기반 JSON 로깅·로그 회전·서비스 읽기 권한. 기존 로그 일회성 변환.
- [x] 기존 테마/탭/로그인 이벤트를 재사용해 통계·표·필터/상태 구현.
- [x] HTTP 시험과 390px/데스크톱 헤드리스 스크린샷 확인.

## Task 3: 게임 기록
Files: fork Network/Game와 새 ActivityLog.cs, tests/hades-characterization/ActivityLogTests.cs.
- [x] 기록 최소 항목/비밀 제외/봇/로그인 세션/실패 안전 시험 → 실패 확인.
- [x] 성공 로그인/연결 종료/맵 입장 및 처리한 게임 요청/상태 변화 훅.
- [x] 서버 build와 characterization 실행.

## Task 4: 리뷰·운영
- [x] diff와 SPEC만 다시 읽어 정확성·보안 리뷰 1회, 중요한 지적 수정.
- [x] 기존 어제 이후 로그 가져오기. 대시보드 배포. 게임 재시작 직전 알림 후 서버 배포.
- [x] 운영 API와 새 게임 기록 확인, 보고서·WORKLOG/NEXT 기록.

## 검증·운영 결과
Python 96/96, JS 대시보드 32/32. 별도 HEAD+이번 변경 서버 빌드 및 ActivityLog/CompanionCall 격리 시험 각 1 통과. 공유 폴더 전체 장기 시험은 동료 기능의 CompanionCall 1건 실패 후 중단(동시 변경이 섞임); 분리 빌드에서 같은 시험은 통과. 웹·서버 배포 및 실제 관리자 API/390px 화면/새 봇 로그인 5건 확인. 기존 Newtonsoft.Json NU1903 경고 유지. 상세 access-activity-results-2026-10-03.md. 커밋하지 않음.
