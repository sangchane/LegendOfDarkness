# Recon — 대신 사냥
버전: v1.0 · 강도 lite(검색 3회)

## 도메인 업무 흐름
- 한국 모바일 MMORPG 의 「무접속 플레이」: 앱을 꺼도 정한 사냥터에서 정한 시간 동안 사냥. 리니지M 은 캐릭터마다 8시간, 오딘 「방치 모드」는 클라이언트를 꺼도 자동 사냥이 이어지고 오프라인 전투 8시간, 검은사막 모바일 「흑정령 모드」(2019).
  출처: https://www.inews24.com/view/1373729 · https://www.consumernews.co.kr/news/articleView.html?idxno=619450 · https://gall.dcinside.com/mgallery/board/view/?id=er&no=216905
- 공통점: 시간 상한 · 다시 접속하면 결과 요약 · 사냥 설정은 앱에서 정한 것 그대로.

## 이해관계자
- 플레이어(사용자 본인·지인 소수) — 자는 동안·다른 앱 쓰는 동안 사냥. 운영자(사용자) — 서버 부하·악용.

## 규제·표준
- 해당 없음(개인 운영 서버, 결제 없음). iOS 는 뒤로 간 앱을 멈추고 소켓이 끊기거나 먹통이 된다 — 오디오·위치 말고는 계속 돌 방법이 없다.
  출처: https://developer.apple.com/forums/thread/750136 · https://developer.apple.com/library/archive/documentation/Performance/Conceptual/EnergyGuide-iOS/WorkLessInTheBackground.html
  → 앱 안에서 이어 돌리기는 막혔다(c70e16d4 실측 실패와 같음). 사냥은 앱 밖에서 해야 한다.

## 유사 솔루션
1. 리니지M 무접속 플레이 — 서버 쪽, 8시간/캐릭터.
2. 오딘 방치 모드 — 클라이언트 꺼도 자동 사냥 유지, 8시간.
3. 이 저장소의 동료 봇 `mobile/bots/Lod.CompanionBot` — 같은 클라우드 기계에서 알맹이(`CompanionBrain`)로 접속해 움직이는 헤드리스 프로그램, systemd `lod-bot@N`. **대리 접속이 그대로 따를 본보기.**

## 스택 후보
| 후보 | 근거 | 트레이드오프 |
|---|---|---|
| A. 클라우드 대리 접속(알맹이 `AutoHunt` 그대로) — **사용자 선택** | 앱과 판단이 같다, 봇 운영 틀(배포·기록·systemd) 재사용 | 비밀번호 없이 들어갈 열쇠가 필요(로그인 서버 변경), 넘길 때 몇 초 끊김 |
| B. 게임 서버가 직접 | 끊김 없음 | 판단을 서버에 새로 써야 하고 앱과 어긋남 |

## Godot 수명 신호
- Godot 4.6 iOS 에서 `NOTIFICATION_APPLICATION_PAUSED/RESUMED/FOCUS_*` 가 SwiftUI 이전 뒤로 안 온다는 결함 보고. 출처: https://github.com/godotengine/godot/issues/115936
  → 설계는 이 신호에 **기대지 않는다**: 서버가 응답 없는 접속을 30초에 끊는 것(아래)을 넘김의 계기로 쓰고, 신호가 오면 바로 끊어 앞당길 뿐.

## 현재 시스템 감사 (서버 `S` = `sources/wren11/Dark-Ages-Private-Server/src/Hades.Server.Base`)
- 로그인: `S/Network/Login/LoginServer.cs:138` `Format03Handler` → `Passwords.Verify`(PBKDF2, `S/Security/Passwords.cs:17-60`). 서버엔 해시뿐 — 대리 프로그램이 비밀번호로 들어갈 수 없다. localhost 특례 없음. 통과하면 `ServerContext.Redirects` 표로 게임서버(`GameServerHandlers.cs:767`).
- 중복 로그인: `MultiUserLogin=false` — **새 접속이 늘 이기고** 옛 접속은 `Remove`+저장(`LoginServer.cs:182-194`, `GameServerHandlers.cs:3073-3087`). → 앱이 돌아오면 대리 접속이 저절로 밀려난다.
- 끊김: 소켓 닫힘 = 즉시 `Remove`+저장(`S/Network/Game/GameServer.cs:30`), 응답 없음 `IdleLimit` 30초(`GameServer.cs:89-107`), 서버 핑 10초.
- 확장 패킷 0xF1 종류 0~6 사용 중(`ClientFormatF1.cs`, `GameServerHandlers.cs:2013-2051`). 0xF0·0xF2·0xF3 사용 중.
- 활동 기록은 봇을 `bot` 으로 표시(`Companions.IsBot`).
- 앱: 자동 사냥 판단 `AutoHunt`(알맹이) + 연결부 `mobile/client/src/World/WorldView.Hunt.cs:144-200`(시야 모으기·행동 옮기기), 포션 `AutoPotion`, 줍기 `AutoLootGate`, 점수 `StatPlan`. 설정은 기기 파일(`user://autohunt.cfg`·포션·줍기·기술 배치).
