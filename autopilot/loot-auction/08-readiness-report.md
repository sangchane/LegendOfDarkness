# 준비도 보고 — 그룹 전리품 룰렛 · 경매장
버전: v1.0 · 기준 03 v1.1

## 판정: PASS (패치 1회 뒤)
- 1차 검토(dev:reviewer, opus, fresh context): CONCERNS — HIGH 5 · MEDIUM 4 · LOW 3. CRITICAL 0, FR·SC 커버리지 공백 0.
- 패치(03 v1.1, DL-11) → 재검토: 9건 모두 닫힘, 패치가 만든 MEDIUM 1 · LOW 2 → 바로 고침(까닭 5 나눔 넘침 · register 갱신 · 받기 흐름 commit).
- `check_package.py`: CRITICAL 0 · HIGH 0 · FR 18(P0 11 · P1 7) 05 참조 18 · SC 7 06 참조 7 · register 29/29 · 질문 4 · 근거 미확인 상수 0.

## 닫힌 주요 지적
| 지적 | 고친 것 |
|---|---|
| 넘친 금화는 바닥에서 주인 검사가 없음 | 상한까지 주고 넘침은 받을 것(까닭 5) |
| INV-2 에 맡긴 보증금 빠짐 | 식에 진행 중 보증금 합 |
| 캐릭터 저장이 실패를 숨겨 복제 가능 | 저장 성공 여부 → 실패면 되돌리고 거절, `DontSavePlayers` 면 경매 닫힘 |
| 되돌리기에서 입찰금·보증금 증발 | 수수료 없는 취소로 정리 후 은행 |
| 복구가 없는 "저장 줄"에 기댐 | 사건 seq + commit, 파일 lastSeq |
| 0x5E serial 4바이트 | 계약에 명시 |
| 보증금 기준 와우와 다름 | 상점가 = `ShopPricing.Offer` |
| 성직자 1억이 시장으로 | 봇 구매 상한 ECO_AUCTION_SPEND_CAP |

## 착수 조건 (선행 확인 — BUILD 첫 작업 안에서)
1. 주기 자동 저장이 경매 조작의 캐릭터 저장과 겹쳐 옛 상태로 덮지 않는지 확인(`AislingStorage`·서버 저장 주기). 겹치면 같은 자물쇠 또는 저장 순서로 막는다.
2. `cloud-server.sh backup` 이 `{StoragePath}/auction/` 을 포함하는지 확인, 없으면 추가.
3. 종류(무기·방어구·장신구) 칸 번호 표를 `ItemTemplate.EquipmentSlot` 값으로 만든다.

## 첫 작업 3개 (워킹 스켈레톤, 07)
1. 경매 한 바퀴(서버): `AuctionHouse` 저장·불러오기 + 올림 · 즉시 구매 · 받기 + 사건 seq/commit → 격리 시험 SC-003 즉시 구매 경로.
2. 룰렛(서버): `GroupLoot.Share`·`ShareGold` + 0x5E 7 + 줍기 보호 결함 → SC-001·SC-002.
3. 앱 「경매장」 단추 → 찾기 탭 목록 → 사진 1장.

## 잔여 리스크
- 크래시 사이 유실 1건은 운영자 수작업(R1)으로 되살린다.
- 임의값 상수 10/14 — 클라우드 사건 기록으로 조정.

## 구현 핸드오프 (dev:build SPEC 입력)
/dev:build 로 다음을 실행:
<inputs>autopilot/loot-auction/03-prd.md (요구사항·상수 표), 05-api-contract.md, 08-readiness-report.md (착수 조건·첫 작업 3개)</inputs>
<references>04-architecture.md, 06-test-design.md, 07-ops-design.md — 필요할 때만 읽는다</references>
<first_task>SPEC.md 작성 — 위 문서를 진실원으로, 낯선 구현자 실행 가능 수준(≥7/10)</first_task>
<then>첫 작업 3개(워킹 스켈레톤)부터. brainstorming 은 생략 — 이 프롬프트가 설계 승인이다. 브랜치 `feature/loot-roll-auction`(루트·서버).</then>
BUILD·REVIEW 에서 dev:ui dial = 기존 앱 테마(어두운 판, 회색 단추 금지) 적용.
<model_hints>
opus: AuctionHouse(INV-1~3·자물쇠·저장 순서), GroupLoot 금화 나눔·넘침, 저장 실패 되돌림, auction-revert.py
sonnet: 0xF4·0x5E 패킷 읽고 쓰기, AuctionPanel·RollBanner 화면, 봇 EcoPlan 올림·사기 판단, 격리 통합 시험 작성
haiku: 문구·용어 정리, 설정 두 칸 추가
</model_hints>
