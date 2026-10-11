# 뤼케시온해안 에리얼 퀘스트 — SPEC (M, 2026-10-11)

출처: 블로그 「[어둠의전설 불타는꾸다]뤼케시온던전 에리얼(4써클)」 https://m.blog.naver.com/ttykr/220041743309 (2014-06-26, 스크린샷 9장).
원작 흐름: 뤼케시온해안 **3-D**(야자수 모래사장) 동쪽 천막 → **게길**(바닷가 모래밭의 갈색 게 — 닿으면 재료를 잃는다) → **대기실** →
**에리얼**(보라 큰 조개) → 갇힌 **마레**를 누르면 **파파야방패**(71 · AC -20) + 전설 「뤼케시온 해안의 에리얼을 진정시키다」.

## FRAME
- 누구: 71레벨 이상(4서클) 손님. 문제: 뤼케시온해안은 사냥만 있고 원작 4서클 이벤트가 없다.
- 성공: 3-D 입구 NPC 에게 말하면 그룹(혼자도)이 전용 게길로 들어가 게를 피해(닿으면 재료 한 벌) 대기실·에리얼 방에 닿고, 에리얼을 잡은 뒤 마레를 누르면 각자 파파야방패와 전설을 받고 3-D 로 나온다.
- 안 할 것: 원작 5직업 5명 조건 · 달팽이 껍질 2종(서버에 달팽이 없음) · 세오의깃털 면제(아이템 없음) · 대기실 석상 · 봇이 퀘스트 하기.

## 정한 것 (사용자 10-11 · 가정 ※)
| 무엇 | 값 |
|---|---|
| 파티 | 그룹이면 그룹원 모두 함께(`group_warp`), 혼자도 됨 |
| 맵(원작 맵은 어느 자료에도 없음 — `NEXT.md` 찾은 것) | 3-D = 원작 `MapFiles/lod6625`(80×50, 새 구역 **뤼케시온해안3-D**, 상설) · 게길 = `lod6627`(70×30, 새 구역 **뤼케시온해안게길**, 사본 원본만) · 대기실·에리얼 방 = 3-D 사본 |
| 3-D 잇기 | 3-C ↔ 3-D 워프 한 쌍(걸을 수 있는 가장자리 칸, 구현 때 칸 고름) · 괴물은 3-C 와 같은 줄 ※ |
| 재료 | 새 잡템 **블루하콘무기**(그림 251 푸른 칼날) · **레드하콘다리**(그림 563 빨간 다리), 블루하콘·레드하콘이 한 마리 약 15% ※(골드아쿠아링 확률은 그대로) |
| 입장 | 말 건 사람이 재료 한 벌(둘 다 1개 이상) 또는 **킹아크퍼스의팬던트**를 가졌을 때만. 재료는 입장 때 안 줄어든다 |
| 게길 | 게(그림 = 그래브, 이름 「게」) 15마리 — 길(0~7줄) 위 정한 칸, 시작 칸에서 떨어지게(리뷰) ※ — 사람을 때리지 않는다. 게 옆 칸(1칸)에 선 사람: 팬던트 있으면 무사 · 재료 있으면 한 벌 빼앗김 · 없으면 3-D 입구로 쫓겨남. 한 사람당 3초에 한 번 ※ |
| 에리얼 | 기존 템플릿 「에리얼」(체력 145,250 · 골드아쿠아링) 1마리 |
| 마레 | 에리얼 방에 세운 NPC. 에리얼이 살아 있으면 「아직…」, 죽었으면 누른 사람에게 파파야방패 1 + 전설(이미 있으면 안 더함) → 3-D 입구로. **깰 때마다** 받는다(새로 들어가면 새 에리얼) |
| 사본 단계 | `map_create` 단계 40(던전 스크립트가 0~23 만 다룸 — 손님을 내보내지 않게) |

## 엔진에 더하는 것 (서버 `Pack599.cs`)
- `npc_spawn "이름", 맵이름, x, y` — 템플릿 「이름」을 복제해 그 맵·칸에 세운다(`Mundane.Create`). 사본은 지금 NPC 를 못 세운다(`Instances.Create` 는 NPC 를 복사하지 않음, `GameClient.cs:1579` 주석). 사본이 지워지면 `Instances.Remove` 의 `Clear<Mundane>` 가 치운다.
- `item_room "이름"` — 그 물건을 받을 빈 칸·무게가 있으면 1(마레가 방패를 주기 전에 — `item_add` 는 자리가 없으면 말없이 버린다, 리뷰).
- `legend_add 아이콘, 색, "글"` — 지금 `Unknown`(노바 대본·`Dungeon__Script.cs:52` 가 부름). `LegendBook.AddLegend`(`GameClientAPI.cs:230` 꼴), 같은 글이 있으면 안 더함.

## 완료 기준 (이 순서로 구현)
- [ ] 생성기 `scripts/gen/world/build-aerial-quest.py --쓰기`: 구역 2(areas json + maps 복사) · `static/pack599-mapfiles.tsv` 2줄 · 3-C↔3-D 워프 · 3-D 괴물 · 재료 아이템 2 · 하콘 드랍 · 게 템플릿(스크립트 「에리얼게」) · NPC 템플릿 2(입장 · 마레) — 검증: 두 번 돌려 `git diff` 가 없음(다시 돌려도 같음)
- [ ] `npc_spawn`·`legend_add` + 입장 NPC·마레 대본(손으로 쓴 `PackNpc`) + 게 스크립트 — 검증: 서버 빌드 오류 0
- [ ] 격리 서버 시험 `AerialQuestTests`: ① 재료 없이 말 걸면 못 들어감 ② 재료 3벌로 들어가 게 옆에 서면 한 벌 줄고, 0벌이면 3-D 로 쫓겨남 ③ 팬던트면 안 줄어듦 ④ 대기실 → 에리얼 방, 마레를 먼저 누르면 「아직」, 에리얼을 잡고 누르면 파파야방패·전설·3-D — 검증: `dotnet test tests/hades-characterization --filter AerialQuestTests`
- [ ] 앱: 새 구역 2장의 맵 그림(`build-client-maps.py` MAPS) · 괴물 이름표 · 게길 사진 한 장(게가 보임) — 검증: 알맹이 시험 + 헤드리스 사진
- [ ] 회귀: 포테 던전·호러캐슬·뤼케시온해안 시험 · 아이템 수치 시험 — 검증: 해당 `--filter`
- [ ] 리뷰 1회 → 커밋 → 배포(사용자에게 알린 뒤 `cloud-server.sh deploy`, 앱 `ios-build.sh install`)

## 참고
- 사본 던전 틀: `database/server/scripts/Pack599/Npcs/포테의숲오솔길입장.cs`(map_create · warp_create · group_warp · mob_spawn3), 시험 `tests/hades-characterization/PoteDungeonTests.cs`.
- 사본: `src/Hades.Server.Base/Systems/Instances.cs`(60000부터 · `ClientNumber` = 원본 번호 · 비면 1분 뒤 지움 `PackRoutine.cs:36`).
- 새 구역 들이기: `scripts/gen/world/build-old-mine.py:135-152`(맵 복사 + areas json, 이름이 같으면 번호 재사용).
- 맵 대응표: `Pack599.cs:940-958` `PackMap` ← `static/pack599-mapfiles.tsv`.
- 괴물 스크립트: `MonsterScript`(`src/Hades.Server.Base/Scripting/MonsterScript.cs`) — 사람을 때릴 때의 훅이 없어 `Update` 에서 옆 칸을 본다.
- 손으로 쓴 NPC 스크립트 예: `database/server/scripts/Mundanes/GemCrafter.cs`.
