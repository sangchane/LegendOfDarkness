# SPEC — 그룹 전리품 룰렛 · 경매장
버전: v1.0 (2026-10-07) · 입력 03 v1.1 · 05 v1.1 · 08 · 등급 L · 브랜치 `feature/loot-roll-auction`(루트·서버)
진실원: 요구사항·상수는 `03-prd.md`, 패킷·파일 모양은 `05-api-contract.md`. 이 문서는 **어디를 어떻게 고치는지**와 03·05 가 남긴 구현 결정(S-1~S-12)을 정한다.
서버 경로 `S=sources/wren11/Dark-Ages-Private-Server`, `H=S/src/Hades.Server.Base`.

## 배경
그룹 사냥에서 성직자는 전리품·금화를 못 얻고, 봇은 희귀품까지 NPC 에게 판다. 룰렛·금화 나눔으로 공정하게 나누고, 사람·봇이 함께 쓰는 와우식 경매장을 연다(03 배경).

## 현재 상태 (확인한 코드)
- **드롭**: `S/database/server/scripts/Formulas/monsterexp.cs` — `GenerateGold`(:417, `Money.Create` 바닥) · Table 방식 `GenerateDrops`(:143, :209-211 `Cursed`+`AuthenticatedAislings=GetTaggedAislings()` 뒤 `Release`) · Random 방식 `DetermineRandomDrop`(:102, :121 `Release`). `Monster.GenerateRewards`(`H/Types/Monster.cs:102`)는 `Rewarded` 로 괴물당 한 번, `player` = 처치한 이.
- **줍기 보호 결함**: `H/Types/Area.cs:384` `var stale = !((now - AbandonedDate).TotalMinutes > 3);` — 뒤집혀 다음 틱에 보호가 풀린다. 줍기 검사 `H/Network/Game/GameServerHandlers.cs:414-437`(AuthenticatedAislings 가 null 이면 누구나).
- **`Item.Release(owner,pos)`**(`H/Types/Item.cs:603`): owner 가 Aisling 이면 `Cursed=false`·`AuthenticatedAislings=[]` 로 지운다 → 보호를 걸려면 owner 로 괴물을 넘긴다.
- **`Item.GiveTo`**(`Item.cs:496`): 성공/실패 bool, 실패면 아무것도 안 바뀐다(겹침은 같은 이름·한도 안 칸에 더함, 아니면 빈칸).
- **그룹**: `Aisling.GroupParty`(`Aisling.cs:138`, `ServerContext.GlobalGroupCache`) · `Party.PartyMembers`(계산 속성, `Party.cs:18`). **동료 봇이 주인 그룹에 든다**(`CompanionPairing.cs:102,121`, `Companions.IsBot`).
- **저장**: `AislingStorage.Save`(`H/Storage/AislingStorage.cs:140-161`) — 예외를 삼키고 void, `DontSavePlayers` 면 그냥 돌아감. 직렬화(`StorageManager.Serialize`)는 자물쇠 밖, 쓰기만 `SafeFile.Write`(`H/Storage/SafeFile.cs:29`, 파일별 자물쇠·원자적 교체·`.backup`). 주기 저장: `SaveComponent`(45초, `Components/SaveComponent.cs:33`) · `GameServer.AutoSave`(`GameServer.cs:161`). 패킷 처리는 소켓 스레드, 주기 저장은 게임 루프 — **겹칠 수 있다**(착수 조건 1, S-6 으로 막음).
- **새 패킷 추가 틀**(0xF3 커밋 baeeed631): `H/Network/ClientFormats/ClientFormatXX.cs` 새로 + `Undefined.cs` 빈 클래스 지움 + `GameServerHandlers` 의 `FormatXXHandler` override(스텁 3파일 `FormatStubs/*` 에는 F4 자리가 이미 있다). 0x5E 쓰기 `H/Network/ServerFormats/ServerFormat5E.cs:89-90`(종류·serial 머리).
- **본뜰 것**: 앱 일괄 거래 0xF2 핸들러 `GameServerHandlers.cs:2085-2210`(검증·`ShopPricing.Offer`·`MaxCarryGold`) · 은행 `H/Types/Bank.cs` · 주기 일 `GameServer.UpdateClients`(`GameServer.cs:95-99`, `ProxyHunt.Expire`).
- **설정**: `H/Infrastructure/ServerConstants.cs`(인터페이스 :46-80 · 구현 :248-320) · `S/src/Lorule.Config/LoruleConfig.json` · `scripts/ops/server-config/LoruleConfig.template.json`. `MaxCarryGold` = 1억.
- **장비 칸**: `ItemTemplate.EquipmentSlot`(int) = `H/Types/ItemSlots.cs` 의 `EquipSlot`.
- **알맹이**: 보냄 `mobile/src/Lod.Mobile.Core/Protocol/World/ClientOpcode.cs:31`(0xF2 옆) · 0x5E 받기 `WorldClient.Receiving.cs:199-242`(종류 switch, 모르는 종류는 무시).
- **격리 시험 틀**: `tests/hades-characterization/BankTests.cs`(`IsolatedHadesServer.Prepare(startTogether:)` · `LoginFlow.TryCreateAccount` · `CompanionCallTests.Edit` 로 캐릭터 JSON 심기 · `WorldClient` 로 패킷).
- **백업**: `scripts/ops/cloud-server.sh:374`(수동)·`:323`(cron 04시) 모두 `aislings` 만 묶는다(착수 조건 2 — 없음).

## 구현 결정 (03·05 가 남긴 것 — decision-log 에 S-1~S-12 로 옮김)
- **S-1 동료 봇 제외**: 룰렛·돌림·금화 나눔 대상 = 처치한 이의 그룹원 중 같은 맵 · 살아 있음 · 로그인 · `!Companions.IsBot(이름)`. 대상이 **2명 미만이면 false**(지금처럼 바닥). 사람 + 동료 봇 사냥은 그대로다. 생태계 봇(`EcoBots.IsEcoBot`)은 대상이다.
- **S-2 금화 나머지**: 몫 = ⌊합/n⌋, 나머지는 처치한 이(대상이 아니면 대상 목록 첫째). 대상 목록 순서 = 이름 소문자 정렬.
- **S-3 돌림 차례**: `Party` 에 `LootTurn`(int)·`LootTurnKey`(정렬한 대상 이름을 `|` 로 이은 것) 메모리 칸. 키가 바뀌면 0 부터. 받는 이 = 대상[LootTurn % n], 그 뒤 LootTurn++.
- **S-4 룰렛 같은 수**: 가장 높은 수가 여럿이면 그들끼리만 다시 굴린다. E-01 에는 **첫 굴림**의 수를 싣고 이긴 이는 최종. 사건 `roll` 의 `data.rolls` 에는 모든 굴림.
- **S-5 사건 기록 양**: `roll` 은 룰렛마다, `split` 은 **넘침(받을 것 금화)이 생길 때만** 적는다 — 처치마다 적으면 하루 수십만 줄. 평소 나눔 금화는 `GoldPoints` 설정자 → 활동 장부(FR-018 둘째 문장)로 남는다.
- **S-6 캐릭터 저장 자물쇠**(착수 조건 1): 새 `AislingStorage.TrySave → bool`(`Save` 는 그것을 부름) — 이름별 자물쇠 안에서 **직렬화 + 쓰기**를 함께 하고, 예외면 false, `DontSavePlayers` 면 false. `IStorage<T>` 는 그대로(구현 셋 중 하나만 필요). 이것으로 주기 저장이 경매 저장보다 옛 상태를 늦게 쓰는 일이 없어진다(직렬화가 자물쇠 안이라 나중에 잡은 쪽이 새 상태를 쓴다).
- **S-7 저장 실패 기록**: 내주는 쪽 저장이 실패하면 메모리를 되돌리고 사건 `{"ev":"abort","seq":N}` 한 줄 — `commit` 도 `abort` 도 없는 seq 만 끊긴 조작이다(07 R1).
- **S-8 요청 간격**: 같은 세션 0xF4 가 0.3초 안이면 **버리지 않고** `0x5E 9 ok=0 "잠시 뒤에 다시 하십시오"`(앱은 0x5E 9 까지 단추를 잠그므로 버리면 단추가 잠긴 채 남는다). 05 「넘으면 버림」을 바꾼다.
- **S-9 입찰 규칙 보충**: 지금 최고 입찰자의 다시 입찰은 거절 「이미 최고 입찰자입니다」. 입찰가 ≥ 즉시 구매가(>0)면 즉시 구매로 처리(즉시 구매가만 냄).
- **S-10 꺼낼 때**: 받을 것 물건은 `Serial` 만 새로(`Generator.GenerateNumber`, `lock(Generator.Random)`), 템플릿 사본은 그대로 — 캐릭터 파일을 한 바퀴 돈 것과 같다(다음 로그인 `GameClient.LoadInventory` 가 다시 잇는다).
- **S-11 종류 표**(착수 조건 3, `EquipmentSlot`): 무기 = 1 · 방어구 = 2 3 4 9 10 12 13 15 16 · 장신구 = 5 6 7 8 11 14 17 · `Equipable` 이 아니거나 0 = 기타.
- **S-13 경매 줄 수치 꼬리**(DL-13): 0x5E 8 보기 0·1 끝에 줄마다 0x0F 와 같은 장비 수치 — 봇의 FR-015 판단과 앱 [정보]가 쓴다.
- **S-12 경매 닫힘**: `!AuctionEnabled` → P-04~07 거절, P-08·기간 끝은 됨. `DontSavePlayers` 또는 auction.json 불러오기 실패 → **P-04~08·기간 끝 모두** 닫음(받기도 캐릭터가 저장되지 않으면 잃는다). 넘친 금화(까닭 5)도 닫혔으면 버리지 않고 메모리 받을 것에만 넣고 서버 로그.

## 제안 변경
### 서버 (`H` = Hades.Server.Base)
1. `Storage/AislingStorage.cs` — S-6 `TrySave`.
2. `Types/Area.cs:384` — `var stale = (DateTime.UtcNow - item.AbandonedDate).TotalMinutes > 3;`
3. **새 `Types/AuctionHouse.cs`** — 머리에 03 상수 표 이름 그대로 `const`(ROLL_*·AUCTION_*). 정적 클래스.
   - 상태: `Listing`(05 ERD) · `Claim`(05 ERD) 레코드 클래스, `List<Listing>`·`List<Claim>`, `long NextId`·`long LastSeq`. 물건은 `Item` 그대로 Newtonsoft(`StorageManager.Settings`)로.
   - 파일: `{ServerContext.StoragePath}/auction/auction.json`(`SafeFile.Write`, `.backup` 이 05·07 의 `.bak`) · 사건 `auction/events-yyyy-MM-dd.jsonl`(UTC, `FileStream` append + `Flush(true)`). 경로는 `Func<string> FolderSource` 로 시험에서 바꿀 수 있게(ProxyHunt 와 같은 틀).
   - 자물쇠 `Gate` 하나. 공개 메서드(모두 `(bool Ok, string Message)` 를 돌려주고 핸들러가 E-03 으로): `Post(GameClient, slot, start, buyout, hours)` · `Bid(GameClient, id, amount)` · `Buyout(GameClient, id)` · `Cancel(GameClient, id)` · `Take(GameClient, claimId)` · 읽기 `Browse(kind, sort, page, query, viewer)` · `Mine(viewer, page)` · `Claims(viewer, page)` · `ClaimCount(name)` · 넘침 `AddGoldClaim(name, gold, reason)` · `Expire(DateTime now)`(마지막 검사에서 AUCTION_TICK 안 지났으면 바로 돌아감) · `Load()`(서버 시작 때, 실패면 닫힘).
   - 조작 순서(INV-3): 검사 → `seq=++LastSeq`, 사건 줄 flush → 내주는 쪽 메모리 변경 → 내주는 쪽 저장(실패면 되돌림 + `abort` + 「저장에 실패했습니다」) → 받는 쪽 메모리 변경 → 받는 쪽 저장 → `commit`.
     | 조작 | 내주는 쪽 | 받는 쪽 |
     |---|---|---|
     | 올림 | 캐릭터: 가방 칸 비움(`Inventory.Remove(client,item)`) · 금화 − 보증금 | 경매장: 줄 추가 |
     | 입찰 | 캐릭터: 금화 − 입찰가 | 경매장: 줄 갱신 · 밀린 이 받을 것(까닭 3) |
     | 즉시 구매 | 캐릭터: 금화 − 즉시 구매가 | 경매장: 줄 지움 · 사는 이 물건(0) · 파는 이 금화(1) = 가 − 수수료 + 보증금 · 밀린 이(3) |
     | 취소 | 입찰 있으면 캐릭터: 금화 − 현재가 수수료, 없으면 없음 | 경매장: 줄 지움 · 파는 이 물건(4) · 입찰자(3) |
     | 받기 | 경매장: 받을 것 지움(가방 빈칸·금화 여유를 **먼저** 세어 들어갈 것만) | 캐릭터: `GiveTo`/금화 + (S-10). `GiveTo` 가 그래도 실패하면 그 줄을 받을 것으로 되돌려 다시 저장 |
     | 기간 끝 | 경매장만: 입찰 있으면 즉시 구매와 같은 정산(까닭 0·1), 없으면 물건 → 파는 이(2), 보증금 몰수 | — |
   - 되돌림: 올림 실패면 `item` 을 같은 칸에 `Inventory.Set(item,false)` + `ServerFormat0F` · 금화 원래대로. 금화 바꾼 뒤엔 `SendStats(StatusFlags.StructC)`.
   - 거절 검사(05 P-04~08 문구): 로그인·살아 있음(`!IsDead()`)·교환 중 아님(`Aisling.Exchange == null`)·물건 `Tradeable`·올린 수 < AUCTION_MAX_LISTINGS·값 1 ≤ 시작가 ≤ AUCTION_MAX_PRICE, 즉시 구매 0 또는 시작가 ≤ 즉시 ≤ AUCTION_MAX_PRICE·입찰가 ≤ AUCTION_MAX_PRICE·시간 ∈ {12,24,48}·계산은 long.
   - 보증금 = max(1, ⌊시작가 × 비율/100⌋) · 수수료 = ⌊가 × 5/100⌋ · 다음 최소 입찰 = 05 데이터 규칙. 금화는 손 먼저 · 모자라면 은행에서 내고, 받기는 손에 들 수 있는 만큼 · 넘는 것은 은행(DL-14).
   - 접속 중인 파는 이·밀린 이에게 E-03(ok=1, 「경매 물건이 팔렸습니다: 이름」 / 「입찰에서 밀렸습니다: 이름」).
   - 찾기: 이름(`DisplayName`, 대소문자 무시 포함) · S-11 종류 · 정렬(남은 시간 오름 / 현재가 오름 / 즉시 구매가 오름, 0 은 맨 뒤) · AUCTION_PAGE. 파는 이·입찰자 이름은 응답에 없다.
4. **새 `Types/GroupLoot.cs`** — `Share(Aisling killer, Item item, Sprite source)` · `ShareGold(Aisling killer, int amount)` → bool. `GroupLootRoll` 꺼짐 · 그룹 없음 · 대상 < 2(S-1) → false.
   룰렛 물건(03 용어: `Equipable` · `Upgrades > 0` · `ItemVariance != None`) → 굴림(S-4) → `GiveTo(winner)`, 실패면 `item.Cursed=true; item.AuthenticatedAislings=[winner]; item.Release(source, winner.Position)` → 대상 모두에게 E-01 + `SendMessage(0x03, "{물건}: {이긴 이} ({수})")` → 사건 `roll`. 돌림 물건 → S-3 → `GiveTo`, 실패면 같은 떨굼. 금화 → S-2, 몫이 `MaxCarryGold − GoldPoints` 를 넘으면 넘는 만큼 `AuctionHouse.AddGoldClaim(이름, 넘침, 5)`, 받은 이에게 `SendStats(StructC)` + 「금전 N전을 나눠 받았습니다」.
5. `monsterexp.cs` — :425 `if (!GroupLoot.ShareGold(_player, sum)) Money.Create(...)` · :211 앞 `if (GroupLoot.Share(_player, rolledItem, _monster)) return;` · :121 앞 같은 줄.
6. `Network/ServerFormats/ServerFormat5E.cs` — 종류 `Roll=7`·`AuctionPage=8`·`AuctionDone=9` + 본문(05 E-01~03). serial 0.
7. **새 `Network/ClientFormats/ClientFormatF4.cs`** — 05 P-01~08 읽기(짧으면 `Kind=0xFF` 로 두어 핸들러가 버림). `Undefined.cs` 의 `ClientFormatF4` 빈 클래스 지움.
8. `Network/Game/GameServerHandlers.cs` — `FormatF4Handler`: 로그인 검사 → S-8 → 종류별 `AuctionHouse` 호출 → 0x5E 8 / 9. `GameClient` 에 `DateTime LastAuctionRequest`.
9. `Network/Game/GameServer.cs:99` 옆 `AuctionHouse.Expire(DateTime.UtcNow);` · 서버 시작(`ServerContext` 의 캐시 불러오기 자리)에서 `AuctionHouse.Load()`.
10. 설정 `GroupLootRoll`·`AuctionEnabled`(bool, 기본 true) — `ServerConstants.cs` 인터페이스·구현 + `LoruleConfig.json` + 템플릿.
### 알맹이·앱·봇 (첫 작업 3 이후 단계는 tasks.md)
11. `ClientOpcode.Auction = 0xF4` · 새 `Protocol/World/Auction.cs`(E-01~03 읽기, 레코드 `AuctionRow`·`AuctionClaim`·`AuctionPage`·`AuctionDone`·`LootRoll`) · `WorldClient.Auction.cs`(05 알맹이 API, 응답 `AuctionPage`·`AuctionDone`·`LastRoll` + 바뀔 때 세는 수) · `WorldClient.Receiving.cs` 0x5E switch 에 7·8·9.
12. 앱 `mobile/client/src/Screens/GameScreen.TopBar.cs` 「설정」 옆 `MenuButton("경매장")` · `OneWindow.cs` `GameWindow.Auction` · 새 `Windows/AuctionPanel.cs`(탭 4, 03 스케치) · 새 `Hud/RollBanner.cs`.
13. 봇 `mobile/src/Lod.Mobile.Core/Automation/EcoPlan.cs` `ToAuction`·`AuctionBuys` + `Tuning.cs` ECO_AUCTION_* · `mobile/bots/Lod.EcoBots/EcoRunner.cs` `Shop` 첫머리(받기 → 올리기 → 사기).
14. 운영 `scripts/ops/cloud-server.sh` backup·cron 에 `auction` 더함(`--ignore-failed-read`), `auction-logs` 명령 · 새 `scripts/ops/auction-revert.py`·`auction-report.py`.

## 완료 기준
- [x] **K1** 캐릭터 저장이 bool, 직렬화·쓰기가 한 자물쇠(S-6) — 검증: 기존 `CharacterSaveTests` 통과 + 경매 시험 INV-3(저장 폴더 읽기 전용 → 올림 거절, 가방 그대로)
- [x] **K2** 경매 한 바퀴(FR-006·008·011·018, SC-003 즉시 구매 경로) — 검증: `AuctionTests.Post_buyout_take_keeps_items_and_gold`(올림 → 즉시 구매 → 둘 다 받기, 단계마다 INV-1·2, 파는 이 순수익 = 즉시 − ⌊즉시×5%⌋, 사건 줄 post·buyout·take·take 와 같은 seq commit)
- [x] **K3** 경매 파일 다시 불러오기(SC-004 앞부분) — 검증: `AuctionTests.Restart_keeps_listings_and_claims`
- [x] **K4** 룰렛(FR-001·002·005, SC-001) — 검증: `GroupLootTests.Roll_gives_one_member_and_tells_everyone`(4인·10회) · 가방 가득 → 발밑·남은 줍기 거절 · 그룹 없음 → 바닥
- [x] **K5** 금화 나눔(FR-004·005, SC-002) — 검증: `GroupLootTests.Gold_split_sums_to_the_drop`(3인 1,000 → 333·333·334) · 상한 근처 → 받을 것 금화 줄
- [x] **K6** 보호 결함(DL-10) — 검증: `GroupLootTests.A_full_bag_drops_at_the_winners_feet_only_for_them`(발밑에 떨어진 것을 남이 몇 순회 뒤에도 못 주움 — 고치기 전 식이면 실패 확인)
- [x] **K7** 동료 봇 제외(S-1) — 검증: 사람 1 + 동료 봇 → 바닥에 떨어짐(지금 동작)
- [x] **K8** 앱 「경매장」 → 찾기 탭 목록(FR-012·013 일부) — 검증: 맥 앱 실제 로그인 사진 1장(`shots/auction-browse-*.png`) + 알맹이 E-02 읽기 단위 시험
- [ ] **K9 이후**(tasks.md T4~): 입찰·취소·기간 끝·거절 문구·동시 즉시 구매 20쌍(SC-004) · 앱 나머지 탭·룰렛 띠(SC-005) · 봇(FR-014·015) · 설정 끔(FR-016) · 되돌리기(FR-017, SC-007) · 운영 보고(SC-006)

## 테스트 계획
- 통합(격리 서버, 실제 패킷): `tests/hades-characterization/AuctionTests.cs`·`GroupLootTests.cs` — 06 의 시나리오 표 전부. 복제·유실(INV-1~3)이 가장 비싸 먼저.
  룰렛 시험은 괴물 드롭표를 장비 하나 100% 로 고친 격리 자료(`IsolatedHadesServer.Prepare` 뒤 몬스터 템플릿 JSON 편집 — `DeathVillageTests` 등 기존 편집 틀을 따른다).
- 단위(알맹이): E-01~03 바이트 견본 읽기 · 모르는 종류 무시 · 봇 판단(FR-014·015).
- 앱: 맥 Godot 실제 로그인 사진(`--login nov:1234`), 세로·가로.
- 모든 새 시험은 고치기 전/되돌리면 실패하는지 한 번 확인(mobile/CLAUDE.md).

## 검증 방법
1. `dotnet build $S/src/Lorule.GameServer` (솔루션 빌드는 윈도우 도구 때문에 실패 — 07)
2. `dotnet test tests/hades-characterization/Hades.Characterization.Tests.csproj --filter "FullyQualifiedName~AuctionTests|FullyQualifiedName~GroupLootTests|FullyQualifiedName~CharacterSaveTests|FullyQualifiedName~BankTests"`
3. `dotnet test mobile/tests/Lod.Mobile.Core.Tests/Lod.Mobile.Core.Tests.csproj`
4. 앱 사진(K8)

## 롤백 계획
설정 `GroupLootRoll=false`·`AuctionEnabled=false` → 서버 재시작(룰렛 즉시 꺼짐, 경매는 받기만). 완전 철거는 07 R4: `auction-revert.py` 로 맡긴 것을 주인 은행으로 → 브랜치 이전 서버 배포. 브랜치가 루트·서버 모두 따로라 main 은 그대로다.

## 안 할 것
03 범위 밖 전부(우편 · NPC 경매인 · 시세 그래프 · 필요/차비 버튼 · 실제 돈 · 1:1 거래 앱 화면) + 사건 기록 30일 정리(운영 cron 은 T 마지막에 eco 기록과 함께) + 동료 봇의 경매 사용.

## 참조 파일
`03-prd.md`(요구사항·불변식·상수) · `05-api-contract.md`(패킷·ERD·데이터 규칙) · `06-test-design.md`(시나리오) · `07-ops-design.md`(배포·런북) · `decision-log.md` · `tasks.md`.
