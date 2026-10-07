# 작업 — 그룹 전리품 룰렛 · 경매장
SPEC: `SPEC.md`. 위에서부터. 첫 셋이 버티컬 슬라이스(경매 한 바퀴 → 룰렛 → 앱 메뉴), 그다음 위험 큰 것부터.

- [x] T1 경매 한 바퀴 — 서버 S-6 저장 bool·자물쇠 · `AuctionHouse`(불러오기·저장·사건 seq/commit/abort · 올림 · 즉시 구매 · 받기 · 찾기·내 경매·받을 것 읽기) · `ClientFormatF4` · 0x5E 8·9 · `FormatF4Handler`(S-8) · 설정 2칸 · 알맹이 `WorldClient.Auction`(보내기·8·9 읽기) + `AuctionTests`(K1·K2·K3). model: opus
- [x] T2 룰렛 — `GroupLoot.Share`·`ShareGold` · 0x5E 7 · `Area.cs` 보호 고침 · `monsterexp.cs` 세 자리 · 알맹이 7 읽기 + `GroupLootTests`(K4~K7). model: opus
- [x] T3 앱 「경매장」 단추 → 찾기 탭 목록(K8) — `GameWindow.Auction` · `AuctionPanel` 찾기 탭 · 사진 1장. model: sonnet
- [x] T4 입찰·취소·기간 끝(FR-007·009·010) + 거절 문구 · 동시 즉시 구매 20쌍 · 계약 시험(짧은 본문·범위 밖 값)(SC-004). model: opus
- [ ] T5 앱 나머지 — 올리기·내 경매·받을 것 탭, 입찰·즉시 구매 판, 룰렛 띠 `RollBanner`, 세로·가로 사진(SC-005). model: sonnet
- [ ] T6 봇 — `EcoPlan.ToAuction`·`AuctionBuys` 단위 시험 · `EcoRunner.Shop` 받기→올리기→사기 · 격리 봇 2(FR-014·015). model: sonnet
- [ ] T7 설정 끔·`DontSavePlayers`·불러오기 실패 닫힘 시험(FR-016) · `auction-revert.py`(FR-017, SC-007). model: opus
- [ ] T8 운영 — `cloud-server.sh` backup·cron `auction`, `auction-logs`, `auction-report.py`(SC-006 숫자·INV-2·끊긴 seq). model: sonnet
- [ ] T9 회귀·리뷰(정확성 1 + 보안 — 복제·금화) · 커밋 · 배포는 사용자 확인 뒤.
