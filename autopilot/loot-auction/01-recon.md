# Recon — 그룹 전리품 룰렛 · 경매장
버전: v1.0 (lite — 검색 4회)

## 도메인 업무 흐름
- **그룹 전리품(와우)**: 담당자 분배(Master Loot) · 그룹 분배(필요/차비/포기 주사위 — 필요가 차비보다 앞섬) · 필요 우선(못 쓰는 물건은 자동 포기) · 개인 전리품.
  출처: https://blizzardwatch.com/2022/09/29/dragonflight-group-loot/ · https://wowwiki-archive.fandom.com/wiki/Loot · https://www.mmo-champion.com/threads/882352
- **경매장(와우)**: 올릴 때 보증금(기간 12h=판매가 15% · 24h=30% · 48h=60%), 입찰가·즉시 구매가, 입찰금은 맡겨 두고 밀리면 돌려줌,
  팔리면 낙찰가 − 수수료 5% + 보증금 반환, 유찰이면 물건만 돌아오고 보증금은 잃음, 입찰 없는 취소는 보증금만 잃음, 입찰 있는 취소는 보증금 + 현재 입찰가의 수수료.
  돈·물건은 우편으로 받음. 출처: https://warcraft.wiki.gg/wiki/Auction_House · https://thelazygoldmaker.com/a-look-at-deposit-costs
- **반면교사(디아블로3)**: 경매장이 「괴물을 잡아 좋은 물건을 얻는」 핵심 고리를 대신해 2014년 없앰. 출처: https://diablo.fandom.com/wiki/Auction_House ·
  https://www.nbcnews.com/technolog/no-money-no-problems-blizzard-axes-diablo-3-auction-house-4B11192195 → 봇이 시장을 채우는 양을 상한으로 묶는다.

## 이해관계자
사람 플레이어(폰 앱) · 생태계 봇(lod-eco, 사람과 같은 규칙) · 운영자 1명(사용자).

## 규제·표준
해당 없음 — 게임 안 금화만, 실제 돈 거래 없음(실제 돈 경매는 디아블로3 RMAH 가 없앤 길).

## 유사 솔루션
와우 경매장(위) · AzerothCore `mod-ahbot`(봇이 희귀도별 수량 상한으로 경매장을 채움, `autopilot/eco-bots/01-recon.md:13`) · 와우 그룹 분배.

## 스택 후보
기존 스택 그대로(서버 Hades C# JSON 저장 · 앱 Godot C# · 알맹이). 새 DB 없음 — 서버 전역 상태는 이미 JSON 파일(게시판 `community/boards`, 대신 사냥 `ProxyJobFolder`).

## 현재 시스템 감사 (Explore 서브에이전트 sonnet, 2026-10-07)
서버 경로는 `sources/wren11/Dark-Ages-Private-Server/` 기준, H=`src/Hades.Server.Base`.
- 드롭: 괴물 `OnDeath` → `Monster.GenerateRewards` → `Monsterexp`(`database/server/scripts/Formulas/monsterexp.cs`) — 금화 `Money.Create`(`H/Types/Money.cs:20`, 같은 칸 금화와 합침), 물건 `GenerateDrops`(:143, Table/Random 두 방식) → 바닥에 `Release`.
- 줍기 보호: Table 방식만 `Cursed = true` + `AuthenticatedAislings = GetTaggedAislings()`(:209-210). 태그는 친 사람 + 그 그룹원 전부(`H/Types/Monster.cs:81-99`).
  **결함**: `H/Types/Area.cs:386-391` 의 `stale` 식이 뒤집혀 보호가 다음 틱에 풀린다.
- **`Item.Serial` 은 안정 ID 아님** — 만들 때·떨굴 때마다 `Random.Next()`(`H/Types/Item.cs:252,610`). 경매 물건은 물건 JSON 통째(템플릿·Upgrades·ItemVariance·Durability·Stacks·Color)로 맡아야 한다.
- 물건 저장: 캐릭터 JSON `{StoragePath}/aislings/<name>.json`(`H/Storage/AislingStorage.cs`), 템플릿 통째 복사. 접두사 칸 없음 — `ItemVariance`·`Upgrades` 로 `DisplayName` 을 만든다.
- 줍기 0x07: `GameServerHandlers.cs:~395-480`(금화 먼저, `ClickLootDistance`, 주인 목록 검사 :420-436).
- 경험치: 이전 작업(결정 20)에서 같은 맵 그룹원 모두로 고침.
- 거래 0x4A(`ExchangeSession`) · 은행 `Banker.cs`(`lock(bank)`) · NPC 상점 `shop1/shop2.cs` · 앱 전용 묶음 거래 0xF2(Buy/Sell, 은행이 0x0F01·0x0F02 로 재사용, `GameServerHandlers.cs:2085`).
- 앱 전용 패킷: 0xF0 세계지도 · 0xF1 동료/생태계(0~9) · 0xF2 묶음 거래 · 0xF3 원격 측정 · 서버→앱 0x5E(1~6). 새 패킷 추가법: `ClientFormatXX` + `FormatStubs` 3파일 + `GameServerHandlers` + 알맹이 `ClientOpcode/ServerOpcode`.
- 경매·우편: 코드 없음. 게시판·우편은 엔진 기능 아님(`docs/exe-manual/04-server-systems.md:28,309-313`).
- 앱 메뉴: `mobile/client/src/Screens/GameScreen.TopBar.cs:113-160`(장비·인벤토리·설정 `MenuButton`), 창 추가 = `OneWindow.cs` 의 `GameWindow` + `GameScreen.Windows.cs:16-66` + `client/src/Windows/` 패널(`WindowFrame`). 목록 UI 바탕은 `TalkPanel`(묶음 사고팔기 줄).
- 봇 판매: `EcoRunner.Shop`(`mobile/bots/Lod.EcoBots/EcoRunner.cs:~453-501`) → `EcoShopping.ToSell(pack, keep: [])` — 체력 물약 빼고 다 판다. 줍기 `AutoHunt.Loot` + `AutoLootGate`(물건마다 0x07 한 번).
