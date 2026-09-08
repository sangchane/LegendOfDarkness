# WORKLOG — LOD

세션/에이전트 간 핸드오프 로그. **"다음 할 일"은 여기 쓰지 않는다 → `NEXT.md`.**
긴 로그는 붙이지 말고 결과만 요약한다.

## Current State
- Status: doing
- Focus: Hades Phase 0 실행 검증 **통과**. 다음은 PRD 미결정 항목(D-001 엔진, D-004 기준 기기) 확정
- Last updated: 2026-09-09

## History (append; 최신이 위)
- 2026-09-09 — **`src/Hades.Client` 재사용 검토(spike): 불가 판정.** 클라이언트가 아니라 미완성 접속 테스트 도구다 — `Program.cs`의 Main이 `//TODO: Implement client that has been written.` + 스레드 대기이고, UI는 공격 패킷 1개를 보내는 버튼 하나뿐, 코드에 `Thread.Sleep(5000)`이 박혀 있다. 프로토콜 커버리지는 로그인까지: 서버가 처리하는 클라 패킷이 로그인 11종 + 월드 41종인데 `Client.cs`가 읽는 서버 패킷은 5개(0x7E·0x00·0x02·0x03·0x05)뿐이고 월드 진입 후 패킷은 하나도 파싱하지 않는다. 이식성도 없다 — .NET Framework 4.6.1(Windows 전용), `Hades.sln`에 미포함, 실제 빌드 시 참조 196개 미해결로 실패(`..\packages` 부재; C# 문법 오류는 0개). Windows 전용 의존은 `Forms/ClientForm*.cs`에만 있어 통신부 자체는 깨끗하지만 가져올 알맹이가 없다
- 2026-09-09 — 위 검토의 대안(모바일 클라이언트 재료): **프로토콜 사양**은 서버 핸들러 52종이 유일한 정본, **암호화**는 `Hades.Server.Base/Security/SecurityProvider.cs`(`Hades.Client` 쪽 237줄과 거의 동일한 중복이므로 서버본을 쓴다), **`.dat` 자료 읽기**는 `Hades.Client.Base`(netstandard2.0 — 모바일 이식 가능, `Archive`·`PaletteCollection`·`Map`·`Tile`; 솔루션 빌드에 포함돼 오류 없이 빌드됨)
- 2026-09-09 — **Hades Phase 0 실행 검증 통과**(`docs/run-procedure.md` 10절 A단계 1~9). 로그인→맵 입장까지 완주: 서버 로그 `wren : Welcome to Lorule`, 게임서버 2615 ESTABLISHED, `aislings/wren.json`(`GameMaster=True`, `CurrentMapId=1`, `4,4`), stderr 0바이트, 10초 자동 저장 동작. 절차서와 달랐던 점 3가지를 문서에 반영: ① 빌드 출력이 `Staging/net5.0/`(SDK 8이 TFM 폴더 추가) ② `game/`에 `Legend.dat`·`cious.dat`가 없어 클라이언트가 `main data file not found`로 종료 — 같은 저장소 `database/archives/`에서 복사해야 함(`LOD_`의 7.41 자료 불필요) ③ 서버가 `Content.Location` 아래 `areas/*.json`을 자기 경로로 덮어쓰므로 `database/server`를 `tmp/hades-run/`로 복사해 가리켜야 submodule이 clean 유지. 클라이언트 비밀번호 칸은 합성 키 입력을 거부해 사람이 직접 입력해야 함(SendInput·WM_CHAR 모두 무시)
- 2026-09-09 — 원본 실행으로 확인한 데이터 공백(PRD 픽스처 근거): 시작 시 로드가 Item 3 · Monster 3 · **Mundane 0** · Spell 0 · Skill 1 · Map 4 · Warp 4 · Popup 3 · Script 135. NPC 상호작용 검증에 필요한 Mundane 템플릿이 0개
- 2026-09-09 — PRD 미결정 2건 해소: **자동 저장 주기** = `LoruleConfig.json` `ServerConfig.SaveRate: 10.0`(초). **지면 아이템 소멸** = 소멸 코드가 없다(아이템은 안 사라짐). `Area.cs:253`의 3분은 소멸이 아니라 남이 떨군 아이템의 소유권 보호(`Cursed`)가 풀리는 시간이며 설정 키 없이 하드코딩
- 2026-09-09 — 로컬 Hades 자료 확인: `sources/Dark-Ages-Private-Server-master`는 중첩 복사본까지 조사했으나 NPC 템플릿·YAML 메뉴·시작맵 연계 몬스터/드롭 공백이 기존 submodule과 동일. `sources/DarkAges718single.exe`(SHA-256 `1E34B83A81E5F844AA62DE496A689706590334E3F3D01D8E1330B1907CBADAF7`) 확보 확인, 실행하지 않음
- 2026-09-09 — PRD `docs/mobile-test-v1-prd.md` v0.2 마무리(Codex 검토 지적 반영: 맵 입장 실패 AC-013, 화면비 AC-014, 확정 드롭 조건, 반복 실행 초기화, 자동 저장 주기 반영, NPC 메뉴 경로 `interactive/Menus` vs `Scripts/Menus` 미확정 기록). Medenia 실험 원복: 패치 99줄 `tmp/medenia-local-run.patch` 보존 후 submodule clean, 로컬 프로세스 종료. DungMunkey 모듈 캐시 `.git/modules` 정리. `sources/`의 로컬 다운로드(7.18 exe, Hades zip)와 `.playwright-mcp/`를 gitignore
- 2026-09-08 — 모바일 전환 기준선 결정: Hades 서버·JSON 데이터·게임 규칙을 유지하고 모바일 클라이언트를 신규 제작한다. Medenia는 실행 대상에서 제외하되 참고 자료와 미커밋 실험은 보존한다. `DungMunkey/Dark-Ages`는 네트워크 없는 오프라인 재현물이어서 submodule에서 제거(원격 저장소는 삭제하지 않음)
- 2026-09-08 — Medenia 후속 조사 기록: 최초 `ServerTableRequest id=0` 실패 뒤 미커밋 `client-crypto.ts` 오프셋 수정으로 `id=1`과 redirect 송신까지 확인. 다만 `ServerTableEntry.port`가 `NaN`이어서 로그인 완주는 미확인. `gateway-listener.ts` 디버그 변경과 실행 프로세스(8081·8082·5173)는 유지하고 이번 커밋에서 제외
- 2026-09-08 — Medenia B단계 실행(Sonnet 에이전트): 서버 8082·자산 8081·클라 5173 기동, SQLite 자동 생성, WS 핸드셰이크 OK. 실패: 서버 `gateway-listener.ts:35` `entry.ip` undefined(ServerTableRequest id=0) → 로그인 폼 미표시. 임시 패치 `tmp/medenia-local-run.patch`(58줄, 미커밋), 보고서 `tmp/medenia-run-report.md`. 레포 밖 변경: 시스템 Python에 setuptools 설치
- 2026-09-08 — 실행 절차·체크리스트 초안 `docs/run-procedure.md` 작성, catch-up 구조 세팅
- 2026-09-08 — 현행 프로젝트 분석서 `docs/current-system-analysis/` 01~09 작성, Graphite 작업 규칙 추가 (dfe0ff2, c74b2db)
