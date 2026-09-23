# 기능 지도 — 하데스 서버 · 모바일 클라이언트 · 원작 근거

게임 기능 39개가 **서버에서 돌아가는지**, **모바일에 화면·통신이 있는지**, **원작에 그 기능이 있었다는 근거가 어디 있는지**를 한 표에 모았다.
새 기능을 만들기 전에 여기서 "서버는 되는데 모바일만 없는 것"인지 "양쪽 다 없는 것"인지부터 본다.

- 조사: 2026-09-17. 서버 `sources/wren11/Dark-Ages-Private-Server` 포인터 `4e68244`, 모바일은 같은 날 `docs/disassembly-original-client` 브랜치.
- 방법: 읽기 전용 탐색 두 갈래(서버 / 모바일·원작) → 주장이 센 것 7개를 다시 열어 확인(문·길드·비밀번호·체력바·대화 연결·배경음악·0x32).
- **줄 번호는 코드가 바뀌면 어긋난다.** 파일은 그대로 찾아가고 줄은 근처를 본다. 다시 셀 때는 아래 "다시 확인하는 법".

## 읽는 법

| 칸 | 값 |
|---|---|
| 서버 | **돌아감** 실제 로직이 있다 · **틀만** 코드나 자리만 있고 내용·연결이 비었다 · **없음** |
| 모바일 | **됨** · **일부** 통신만 있거나 한쪽만 된다 · **없음** |

줄임말: `H/` = `sources/wren11/Dark-Ages-Private-Server/` · `HB/` = `H/src/Hades.Server.Base/` · `GSH` = `H/src/Hades.Server.Base/Network/Game/GameServerHandlers.cs` ·
`LS` = `H/src/Hades.Server.Base/Network/Login/LoginServer.cs` · `S/` = `H/database/server/scripts/` ·
`WC` = `mobile/src/Lod.Mobile.Core/World/WorldClient.cs` · `GS` = `mobile/client/src/GameScreen.cs` ·
`TS` = `sources/FallenDev/dark-ages-ts/packages/network/src/packets/op-codes.ts`

## 한눈에

- **서버는 거의 다 있다.** 39개 중 돌아감 30 · 부분 4(재접속·날씨는 밤낮만·퀘스트·소리) · 틀만 4(표지판·2차 직업·PvP·길드) · 없음 1(문).
- **모바일이 비었다.** 됨 6 · 일부 9 · 없음 22 · 해당 없음 2(저장·젠). 가장 큰 막힘은 **NPC 대화창(0x2F)이 통신만 있고 화면에 안 이어진 것** —
  상점·은행·배우기·퀘스트·전직이 전부 대화창 위에서 돈다.
- ~~**내 상태가 화면에 안 나온다.**~~ 체력·마력 막대와 레벨(위 줄)·금화(소지품 창)를 서버 값으로 보여 준다(2026-09-17). 경험치는 아직 화면에 없다.
- 서버에도 없는 것: **문**(0x32 는 빈 자리 `HB/Network/ServerFormats/ServerFormat32.cs`, 보내는 곳 없음) · **길드 관리** · **PvP 동의** · **2차 직업 승급** · **실제 날씨**.

## 기능 표

| # | 기능 | 서버 | 모바일 | 원작 근거 | 메모 |
|---|---|---|---|---|---|
| **접속** |||||
| 1 | 로그인·계정 만들기 | 돌아감 `LS:108-131` · 해시 PBKDF2 `HB/Security/Passwords.cs:20` | 일부 — 로그인만 `mobile/src/Lod.Mobile.Core/Net/HadesLoginClient.cs:42-103` | `H/src/Hades.Client/ClientFormats/CreateAccount.cs` | 계정 만들기 화면 없음 |
| 2 | 캐릭터 만들기·직업 고르기 | 돌아감 — 생성 `LS:166-184`가 `0x04`의 Path(1~5)를 검증해 실제 1레벨 직업 갑옷을 장비하고 노비스마을 20373 (37,29)에 저장한다 | 됨 — 생성 화면에서 전사·도적·법사·사제·무도가를 고르고 성공 뒤 곧바로 월드에 들어간다 | `database/server/templates/items/{도복,연무복}.json` · 원작 `skill.tbl` ST | `0x04`는 HairStyle·Gender·HairColor·Path 네 바이트인 모바일/서버 동시 배포 계약; 기존 NPC 전직 선택은 남아 있다 |
| 3 | 끊긴 뒤 다시 붙기 | 부분 — 끊기면 저장 후 제거 `HB/Network/Game/GameServer.cs:30-51` | 없음 | 확인 못 함 | 재로그인만 된다. 이어받기 없음 |
| 4 | 저장 | 돌아감 — 캐릭터당 JSON `HB/Storage/AislingStorage.cs:13-21`, 주기 저장 `GSH:234` | — | — | DB 가 아니라 파일 |
| **월드** |||||
| 5 | 걷기·방향 | 돌아감 `GSH:278-360` | 됨 `WC:429-442` | `TS:7` | 예측 + 서버 정정 |
| 6 | 워프·맵 이동 | 돌아감 `GSH:2469-2508` (워프 909) | 일부 — 맵 바뀜을 받긴 한다 `WC:239-246`, 워프로 확인한 적 없음 | `HB/Types/Warp.cs` | 10단계 스모크에서 확인할 것 |
| 7 | 월드맵 | 돌아감 `GSH:1841-1894` (`templates/worldmaps/`) | 없음 | `TS:37,85` (WorldMap) | 월드맵 화면 없음 |
| 8 | 문 열고 닫기 | **없음** — 0x32 빈 자리 | 없음 | `sources/FallenDev/dark-ages-ts/packages/network/src/packets/server/door-packet.ts` (`TS:89` Door=50) | **양쪽 다 새로 만들 것** |
| 9 | 게시판·편지 | 돌아감 `GSH:1634-1785` | 없음 | `HB/Types/Board.cs` · `TS:78,88` | 서버만 된다 |
| 10 | 표지판·읽는 물건 | 틀만 — 엔진 `HB/Templates/PopupTemplate.cs:144`, 내용 3개 | 없음 | `sources/FallenDev/Arbiter/Arbiter.Net/Types/WorldMessageType.cs:15` SignPost | 표지판 내용이 없다 |
| 11 | 날씨·밤낮·조명 | 밤낮 돌아감 `HB/Network/Game/Components/DayLightComponent.cs:18-35` · 날씨 틀만(Snow/Rain 은 실명 암전용 `HB/Types/MapFlags.cs:12-13`) | 없음 | 메타파일 `Light` · 원작 클라이언트 화면 효과 0xfa~0xfd(`docs/disassembly.md`) | 밤낮이 모바일에 안 온다 |
| **성장** |||||
| 12 | 경험치·레벨업 | 돌아감 `HB/Types/Monster.cs:70-204` | 일부 — 레벨은 위 줄에 보인다, 경험치는 아직 화면 없음 | `docs/monster-experience-rescale.md` | 원작 곡선 적용됨 |
| 13 | 능력치 올리기 | 돌아감 `GSH:2051-2143` | 일부 — 보내기만 `WC:598-599`, 자동 사냥에서만 씀 | `TS:42` RaiseStat | 누를 버튼 없음 |
| 14 | 2차 직업·전직 | 틀만 — `ClassStage` 에 Dedicated·Sub 있으나 승급 스크립트 없음 `HB/Types/ClassStage.cs` | 없음 | `H/database/server/areas/전직신전2.json` · `skill.tbl` 2차 직업 줄 | NEXT 의 "2차 직업 분류"와 같은 일 |
| 15 | 죽음·유령·부활 | 돌아감 `HB/Types/Aisling.cs:644-787` | 없음 — 유령 표시 안 함 `WC:741` | 5.99 서버 유령 상태(`docs/disassembly.md`) | |
| **전투** |||||
| 16 | 평타·피해식 | 돌아감 `GSH:70-138` · `HB/Types/Sprite.cs:863-875` | 됨 `WC:488-489` | `TS:17` · 5.99 공격력 식 | 처치까지 확인 |
| 17 | 괴물 AI | 돌아감 `S/Monsters/CommonMonster.cs:58-138` | 됨(표시·동작) `WC:954-997` | `docs/monster-behaviour.md` | |
| 18 | 괴물 젠 | 돌아감 `HB/Network/Game/Components/MonolithComponent.cs:35-77` (템플릿 568) | — | 팩 젠 671 | |
| 19 | 상태 이상·버프 | 돌아감 — 독·수면·빙결·실명·저주… `HB/Storage/StorageManager.cs:271-292` | 없음 — 아이콘 패킷 0x3A 안 읽음, 실명 값만 받음 `WC:925` | `HB/Types/Buff.cs:45` | |
| 20 | PvP·아레나 | 틀만 — 아레나 이동 NPC 뿐 `S/Mundanes/ArenaMaster.cs:21-50`, 동의 체계 없음 | 일부 — 사람도 때릴 수 있음 | `HB/Types/Sprite.cs:261-269` PlayerKill 맵 플래그 | 규칙부터 정해야 한다 |
| **기술·마법** |||||
| 21 | 배우기 | 돌아감 — 5.99 사범 스크립트를 그대로 돌린다(`S/Pack599/PackNpc.cs`, 밀레스마을 직업 사범 20 · 달인 3, 2026-09-17). 하데스 `LearnSkills.cs` 는 붙은 NPC 가 없다 | 됨 — 대화창으로 "다음"·메뉴를 눌러 배운다 | 팩 `Npc_Skill.txt` · 메타파일 `SClass1~5` | 기술이 73번 칸(셋째 쪽)에 들어간다 — 기술 템플릿 311 중 310 에 `Pane` 이 비었다. `get_son`(순수) 뜻 모름 → 그 선택지 11곳 막힘 |
| 22 | 쓰기·쿨다운 | 돌아감 `GSH:1787-1839` · 마법 `HB/Network/Game/GameClient.cs:1322-1350` | 일부 — 한 개씩 쓰기 `WC:492-504`, 쿨다운(0x3F) 안 읽음 | `TS` Cooldown 63 · `sources/wren11/ETDA/BotCore/Shared/Collections.cs` sCooldown | **587 중 42개만 효과가 난다**(`docs/what-hades-already-has.md`) |
| 23 | 소모품 사용 | 돌아감 `GSH:896-974` | 됨 `WC:511-512` | `TS:21` | |
| 24 | 장비 입고 벗기 | 돌아감 `HB/Network/Game/GameClient.cs:124-178` | 됨 `WC:549-550` · `mobile/client/src/GearGrid.cs` | 5.99 장착 규칙(`docs/disassembly.md`) | |
| 25 | 내구도·수리 | 돌아감 `HB/Types/EquipmentManager.cs:87-97` · 수리 `S/Mundanes/shop1.cs:113-129` | 없음 — 수치만 받음 `WC:1020-1061` | `HB/Types/Item.cs` Durability | |
| 26 | 드롭·줍기·버리기 | 돌아감 `GSH:362-602` | 됨 `WC:518-581` | `TS:8-9` | **괴물 드롭 목록이 비었다**(NEXT) |
| **NPC·경제** |||||
| 27 | NPC 대화·메뉴 | 돌아감 `GSH:1348-1438,1991-2012` | **됨(2026-09-17)** — 여덟 모양 읽기·`0x3A` 답·닫기, 화면 `mobile/client/src/TalkPanel.cs`. NPC 그림(26종)도 뽑아 누르면 열린다 | `HB/Network/ServerFormats/ServerFormat2F.cs` | NPC 템플릿 그림 번호를 0x4000 체계로 고쳤다(`tools/pack-import/import.py`) |
| 28 | 상점 | 돌아감 `S/Mundanes/shop1.cs:26-129` | 됨 — 사기 확인(`NpcDialogueTests`), 팔기·수리는 같은 창 | `HB/Network/ServerFormats/ServerFormat2F.cs:41-60` ItemSellData | 판매 목록 180종이 **모두 서버에 있다**(2026-09-17). 확성기·미션두루마리처럼 창을 띄우는 사용 스크립트 10개는 아직 못 돌려 사도 쓸 수 없다. 물약·음식·귀환 주문서 38종(`build-pack-consumables.py`)과 방패·투구·장신구·장갑·허리띠·각반·신발·장식 416종(`build-pack-equipment.py`)이 들어왔다(2026-09-17). 장식은 입은 모습으로 그리지 않는다. 문구가 영어 |
| 29 | 은행 | 돌아감 `S/Mundanes/Banker.cs:168-248` | 없음 | `HB/Network/ServerFormats/ServerFormat2F.cs:16-27` | |
| 30 | 사람끼리 교환 | 돌아감 `GSH:2146-2360` (아이템·금화·양쪽 확인) | 없음 | `HB/Network/ServerFormats/ServerFormat42.cs` · `TS` Exchange 74 | |
| 31 | 퀘스트 | 부분 — 하데스 엔진 `HB/Types/Quest.cs:27-54` 대신 5.99 NPC 스크립트를 그대로 돌린다(`S/Pack599/PackNpc.cs`, 84개, 진행값 `Aisling.PackVariables`, 2026-09-17) | 됨 — 대화창으로. 그림 없는 스크립트 NPC 는 "!" 표식(`NpcMark`) | 팩 `Npc_Quest.txt`·`Npc_Script.txt` 등 · 메타파일 `SEvent1~7` | 스크립트가 붙은 NPC 80곳(사범 23 · 그라가스·멜로린·뮤레칸·인셉션 · 그림 없는 스크립트 NPC 53). 개인 던전(`map_create`)·괴물 부르기·상점 `shop`·은행·게시판(`call_func Board`) 명령은 아직 |
| 38 | 금화 | 돌아감 — 버리기 `GSH:1009-1040`, 교환 `GSH:2272-2299` | 됨 — 버리기 `WC:534-542`, 소지품 창에 금화 | `TS:24` | |
| **함께 하기** |||||
| 32 | 채팅(말·외침·귓속말) | 돌아감 `GSH:609-684,827-864` | 일부 — 말하기만(종류 0 고정) `WC:480-481` | `TS:19` Whisper | |
| 33 | 그룹·파티 | 돌아감 — 청하기 → 묻기(0x63) → 받아들이기(0x2E 3), 제 이름을 청하면 나감, 그룹말(0x19 `!` → 0x0A 11) `GSH` `AskToGroup`·`AcceptGroup`·`Format19Handler` (2026-09-24, **라이브 서버는 다시 빌드해야 반영**) · `HB/Types/Party.cs` · 경험치는 곁의 그룹원이 같은 몫 + 인원×5% (`monsterexp.cs` `GenerateExperience`) | 됨 — 사람을 누르면 [파티 초대], 묻는 창 [거절]/[수락], 위 줄 아래 파티 목록(이름·체력·[나가기]), 대화 창 파티 탭에서 보내면 그룹말 (`World/Party.cs`, `client/src/PartyColumn.cs`) | Arbiter `ClientGroupInviteMessage`·`ServerGroupMessage` · 5.99 `Legend.exe` `GroupAskList`·`GroupAlertPane` · 5.99 서버 문구 `[그룹말]%s` | 그룹원 체력은 서버가 따로 보내지 않는다 — 보일 때 맞은 체력바(0x13)만. 모집 게시판(0x2E 4~7)은 없음 |
| 34 | 길드 | 틀만 — `Clan` 문자열뿐 `HB/Types/Aisling.cs:74-76`, 만들기·가입·계급 없음 | 없음 | `sources/FallenDev/dark-ages-ts/packages/network/src/packets/server/self-profile-packet.ts:8-16` guildName | **서버부터 만들 것** |
| 35 | 레전드 마크·프로필 | 돌아감 `GSH:1060-1080,2434-2438` | 없음 — 0x34 안 읽음 | `HB/Network/ServerFormats/ServerFormat34.cs:24-27` LegendMarks | |
| 37 | GM 명령 | 돌아감 `HB/Systems/Commander.cs:14-70` | 없음 | — | 채팅 `/` 로 쓴다 |
| **소리·설정** |||||
| 36 | 효과음·배경음악 | 부분 — 맵 배경음 `HB/Network/Game/GameClient.cs:919-929`, 피격음 `HB/Network/ServerFormats/ServerFormat13.cs` | 일부 — 효과음만 `WC:288-298` (165개) | 배경음악 `H/database/assets/MusicFiles/*.mus` **64개** | 배경음악 없음 |
| 39 | 설정·옵션 화면 | 돌아감 — 설정 토글 0x1B `GSH:866` | 없음 | `TS:20` UserOptionToggle | 서버 주소도 빌드 때 고정 |

## 패킷 골격

### 클라이언트 → 서버 (서버가 받는 행동 51종)

`모바일` 칸은 모바일이 그 패킷을 보내는지다(`WC`·`mobile/src/Lod.Mobile.Core/Protocol/Login/Hades718LoginProtocol.cs`).

| 코드 | 행동 | 서버 처리 | 판정 | 모바일 |
|---|---|---|---|---|
| 0x00 | 버전 확인 | `LS:65` | 돌아감 | 보냄 |
| 0x02 | 계정 만들기 1 | `LS:91` | 돌아감 | — |
| 0x03 | 로그인 | `LS:108` | 돌아감 | 보냄 |
| 0x04 | 계정 만들기 2(성별·머리·직업) | `LS:166` | 보냄 — 본문 `HairStyle, Gender, HairColor, Path(1..5)` | 모바일/서버 동시 배포 계약 |
| 0x05 | 지도 요청 | `GSH:266` | 돌아감 | — |
| 0x06 | 걷기 | `GSH:278` | 돌아감 | 보냄 |
| 0x07 | 줍기 | `GSH:362` | 돌아감 | 보냄 |
| 0x08 | 버리기 | `GSH:479` | 돌아감 | 보냄 |
| 0x0B | 나가기 | `GSH:604` | 돌아감 | — |
| 0x0E | 말하기·외치기·GM 명령 | `GSH:609` | 돌아감 | 보냄(말하기만) |
| 0x0F | 마법 | `GSH:686` | 돌아감 | 보냄 |
| 0x10 | 월드 입장 | `GSH:738` | 돌아감 | 보냄 |
| 0x11 | 방향 | `GSH:766` | 돌아감 | 보냄 |
| 0x13 | 평타 | `GSH:779` | 돌아감 | 보냄 |
| 0x18 | 접속자 목록 | `GSH:809` | 돌아감 | — |
| 0x19 | 귓속말 | `GSH:827` | 돌아감 | — |
| 0x1B | 설정 토글 | `GSH:866` | 돌아감 | — |
| 0x1C | 아이템 사용 | `GSH:896` | 돌아감 | 보냄 |
| 0x1D | 감정표현 | `GSH:976` | 돌아감 | — |
| 0x24 | 금화 버리기 | `GSH:1009` | 돌아감 | 보냄 |
| 0x26 | 비밀번호 바꾸기 | `LS:201` | 돌아감 | — |
| 0x29 | 교환창에 올리기 | `GSH:1042` | 돌아감 | — |
| 0x2A | (모름) | `GSH:1056` | 틀만 | — |
| 0x2D | 내 프로필 열기 | `GSH:1060` | 돌아감 | — |
| 0x2E | 파티 청하기(2)·받아들이기(3) | `GSH` `Format2EHandler` | 돌아감 | 보냄 |
| 0x2F | 그룹 받기 켜고 끄기(끄면 나감) | `GSH:1124` | 돌아감 | — |
| 0x30 | 칸 바꾸기 | `GSH:1151` | 돌아감 | 보냄 |
| 0x32 | (모름) | `GSH:1322` | 틀만 | — |
| 0x38 | 새로고침 | `GSH:1327` | 돌아감 | 보냄 |
| 0x39 | 대화 답(번호) | `GSH:1348` | 돌아감 | — |
| 0x3A | 대화 답(글·앞뒤) | `GSH:1440` | 돌아감 | 보냄(대화창) |
| 0x3B | 게시판·편지 | `GSH:1634` | 돌아감 | — |
| 0x3E | 기술 | `GSH:1787` | 돌아감 | 보냄 |
| 0x3F | 월드맵 목적지 | `GSH:1841` | 돌아감 | — |
| 0x43 | 탭(대상·물건) | `GSH:1896` | 돌아감 | 보냄 |
| 0x44 | 장비 벗기 | `GSH:2017` | 돌아감 | 보냄 |
| 0x45 | 핑 | `GSH:2045` | 돌아감 | — |
| 0x47 | 능력치 올리기 | `GSH:2051` | 돌아감 | 보냄 |
| 0x4A | 교환 | `GSH:2146` | 돌아감 | — |
| 0x4B | 로그인 공지 | `LS:229` | 돌아감 | — |
| 0x4D | 시전 줄 수 | `GSH:2363` | 돌아감 | — |
| 0x4E | 시전 대사 | `GSH:2400` | 돌아감 | — |
| 0x4F | 자기소개 | `GSH:2434` | 돌아감 | — |
| 0x57 | 서버 목록 | `LS:239` | 돌아감 | — |
| 0x66 | (모름) | 없음(빈 stub) | 없음 | — |
| 0x68 | 로그인 메타데이터 | `LS:267` | 돌아감 | — |
| 0x75 | 틱 | `GSH:2440` | 돌아감 | — |
| 0x79 | 자리 비움 | `GSH:2445` | 돌아감 | — |
| 0x7B | 메타파일 요청 | `GSH:2453` · `LS:272` | 돌아감 | — |
| 0x89 | 표시 마스크 | 없음(빈 stub) | 없음 | — |

### 서버 → 클라이언트

**모바일이 읽는 것(22):** 0x02 로그인 거절 · 0x03 서버 옮기기 · 0x04 내 위치 · 0x05 내 번호 · 0x07 괴물·상인·바닥 물건 · 0x08 내 수치 ·
0x0A 말 · 0x0C 남 걷기 · 0x0E 사라짐 · 0x0F·0x10 소지품 넣고 빼기 · 0x11 남 돌기 · 0x13 체력바·소리 · 0x15 지도 정보 ·
0x17·0x18·0x2C·0x2D 기술·마법 목록 · 0x19 소리 · 0x1A 몸 동작 · 0x29 이펙트 · 0x2F 대화창 · 0x30 창 닫기 · 0x33 사람 · 0x37·0x38 장비. 줄 번호는 `WC:239-415`.

**서버가 보내는데 모바일이 안 읽는 것(하데스 `ServerFormats/`):** 0x0B(걷기 확인) · 0x0D(떠 있는 글) · 0x20(밝기 `Shade` — 밤낮) · 0x2E ·
0x30(입력 대화) · 0x31(게시판) · 0x34(레전드 마크·프로필) · 0x36(접속자 목록) · 0x39(내 프로필) · 0x3A(상태 이상 아이콘) · 0x3B(핑) ·
0x3C(지도 줄) · 0x3F(쿨다운) · 0x42(교환) · 0x4B · 0x56(서버 표) · 0x60(공지) · 0x66·0x67 · 0x6F(메타파일) · 0x73 · 0x7E(인사).
0x32(문)는 서버에 자리만 있고 보내지 않는다.

## 빈틈 — 어디서부터 막히나

1. ~~**NPC 대화창(모바일)**~~ — 됐다(2026-09-17). NPC·괴물 그림 112종도 뽑아 화면에 보이고 눌린다(`scripts/build-client-creatures.py`).
2. ~~**내 상태 표시(모바일)**~~ — 체력·마력·레벨·금화는 됐다(2026-09-17). 경험치만 남았다.
3. **콘텐츠(서버)** — 기술·마법 효과(42/587) · 괴물 드롭 · 상점 물건 · 퀘스트 연결 NPC 8개.
4. **상태 이상 아이콘·쿨다운(모바일)** — 0x3A·0x3F 를 읽으면 된다.
5. **양쪽 다 없는 것(서버부터)** — 문 · 길드 관리 · PvP 규칙 · 2차 직업 승급 · 실제 날씨.
6. **함께 하기(모바일)** — 외치기·귓속말·파티·교환·게시판·레전드 마크는 서버가 이미 한다.

## 다시 확인하는 법

- 서버 처리: `ClientFormatXX` 이름으로 `GSH`·`LS` 를 찾는다. 오버라이드가 없으면 `H/src/Hades.Server.Base/Network/FormatStubs/NetworkServerStubs.cs` 의 빈 stub 이 돈다.
- 모바일 통신: `WC` 의 `private const byte …Command = 0x..` 목록이 읽고 보내는 것 전부다.
- 모바일 화면 연결: 통신 함수 이름(`AnswerAsync`·`Talking` 등)으로 `mobile/client/src` 를 찾는다. 안 나오면 화면이 없다.
- 원작 근거: `docs/where-the-answers-are.md` 의 `scripts/find-in-sources.ps1`.
