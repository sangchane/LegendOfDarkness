#!/usr/bin/env python3
"""경매장 되돌리기(설계 autopilot/loot-auction/ FR-017 · 07 런북 R1·R4) — **서버를 멈춘 뒤에만** 돌린다(켜 둔 서버는 파일을 덮어쓴다).

  모두 은행으로: python3 scripts/ops/auction-revert.py <저장 폴더>
    남은 경매는 수수료 없는 취소로 정리한다 — 물건·보증금은 파는 이, 맡긴 입찰금은 입찰자. 받을 것과 함께 주인 캐릭터 파일의
    은행(BankManager: 물건·금화)으로 옮기고, auction/auction.json(과 서버의 직전 사본 .backup)은 .reverted 로 이름만 바꾼다.
    고치는 캐릭터 파일마다 <이름>.json.before-revert 사본을 남긴다.
  받을 것 한 줄 넣기(R1, 크래시로 끊긴 조작 되살리기):
    python3 scripts/ops/auction-revert.py <저장 폴더> --give 이름 --gold N
    python3 scripts/ops/auction-revert.py <저장 폴더> --give 이름 --item 물건.json

<저장 폴더> 는 서버 설정 Content.Location(클라우드 ~/lod/database/server) — 그 아래 auction/ 과 aislings/ 가 있다.
"""
from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
from datetime import datetime, timezone

# 서버 Item.GetDisplayName 의 강화 머리. 은행 칸 이름은 표시 이름이다(Bank.Deposit).
UPGRADES = {4: "{=fRare", 5: "{=pEpic", 6: "{=sLegendary", 7: "{=bGodly", 8: "{=uForsaken"}

# 손으로 넣는 받을 것의 까닭 — 물건은 「취소」(4), 금화는 「밀린 입찰금」(3): 앱에 「돌려받은 것」으로 보인다.
GIVEN_ITEM, GIVEN_GOLD = 4, 3


def load(path: str):
    with open(path, encoding="utf-8-sig") as file:
        return json.load(file)


def save(path: str, data) -> None:
    """옆에 다 쓰고 바꿔 끼운다 — 중간에 끊겨도 반쪽 파일이 남지 않게(서버 SafeFile 과 같은 방식)."""
    writing = path + ".writing"
    with open(writing, "w", encoding="utf-8") as file:
        json.dump(data, file, ensure_ascii=False)
        file.flush()
        os.fsync(file.fileno())
    os.replace(writing, path)


def bank_key(item) -> str:
    name = item["Template"]["Name"]
    head = UPGRADES.get(item.get("Upgrades") or 0)
    # ponytail: 변형(ItemVariance) 이름은 빼고 강화 머리만 붙인다 — 칸 이름이 표시 이름과 조금 달라도 물건 자체는 그대로 돌아간다.
    return f"{head} {name}" if head else name


def give_outs(book) -> list[tuple[str, dict | None, int]]:
    """(주인, 물건 또는 None, 금화) — 남은 경매는 수수료 없는 취소, 그다음 받을 것."""
    outs = []
    for listing in book.get("Listings", []):
        outs.append((listing["Seller"], listing["Item"], int(listing.get("Deposit") or 0)))
        if listing.get("Bidder"):
            outs.append((listing["Bidder"], None, int(listing.get("Bid") or 0)))
    for claim in book.get("Claims", []):
        is_item = claim.get("Kind") == 0
        outs.append((claim["Owner"], claim.get("Item") if is_item else None, 0 if is_item else int(claim.get("Gold") or 0)))
    return outs


def revert(storage: str) -> None:
    book_path = os.path.join(storage, "auction", "auction.json")
    book = load(book_path)
    outs = give_outs(book)

    # 먼저 모두 읽는다 — 없는 캐릭터가 하나라도 있으면 아무것도 쓰지 않고 멈춘다.
    characters: dict[str, dict] = {}
    for owner, _, _ in outs:
        path = os.path.join(storage, "aislings", f"{owner.lower()}.json")
        if path not in characters:
            if not os.path.exists(path):
                sys.exit(f"캐릭터 파일이 없습니다: {path} — 아무것도 바꾸지 않았습니다.")
            characters[path] = load(path)

    items = gold = 0
    for owner, item, amount in outs:
        bank = characters[os.path.join(storage, "aislings", f"{owner.lower()}.json")].setdefault("BankManager", {})
        bank.setdefault("Items", {})
        bank.setdefault("Gold", 0)
        if item is not None:
            bank["Items"].setdefault(bank_key(item), []).append(item)
            items += 1
        bank["Gold"] = int(bank["Gold"]) + amount
        gold += amount

    for path, data in characters.items():
        shutil.copy2(path, path + ".before-revert")
        save(path, data)

    os.replace(book_path, book_path + ".reverted")
    # 서버는 auction.json 이 없으면 직전 사본(.backup)을 읽는다(SafeFile.Read) — 남겨 두면 옛 경매가 되살아나 은행과 두 곳에 생긴다.
    for leftover in (".backup", ".writing"):
        if os.path.exists(book_path + leftover):
            os.replace(book_path + leftover, book_path + ".reverted" + leftover)
    print(f"은행으로 옮김 — 물건 {items}개 · 금화 {gold:,}전 · 캐릭터 {len(characters)}명. auction.json → auction.json.reverted")


def give(storage: str, owner: str, amount: int | None, item_file: str | None) -> None:
    book_path = os.path.join(storage, "auction", "auction.json")
    book = load(book_path) if os.path.exists(book_path) else {"NextId": 1, "LastSeq": 0, "Listings": [], "Claims": []}
    item = load(item_file) if item_file else None
    book["Claims"].append({
        "Id": book["NextId"], "Owner": owner, "Kind": 0 if item else 1, "Item": item, "Gold": 0 if item else amount,
        "Reason": GIVEN_ITEM if item else GIVEN_GOLD, "ListingId": 0, "At": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    })
    book["NextId"] += 1
    if os.path.exists(book_path):
        shutil.copy2(book_path, book_path + ".before-give")
    save(book_path, book)
    print(f"받을 것 한 줄 — {owner}: {'물건 ' + item['Template']['Name'] if item else f'금화 {amount:,}전'}")


def main() -> None:
    parser = argparse.ArgumentParser(description="경매장 되돌리기 — 서버를 멈춘 뒤에만")
    parser.add_argument("storage", help="서버 저장 폴더(Content.Location) — auction/ 과 aislings/ 가 있는 곳")
    parser.add_argument("--give", metavar="이름", help="받을 것 한 줄을 넣을 캐릭터")
    parser.add_argument("--gold", type=int, help="넣을 금화")
    parser.add_argument("--item", metavar="물건.json", help="넣을 물건(물건 JSON 한 개)")
    args = parser.parse_args()

    if args.give:
        if (args.gold is None) == (args.item is None) or (args.gold is not None and args.gold < 1):
            parser.error("--give 에는 --gold N(1 이상) 또는 --item 파일 하나를 준다")
        give(args.storage, args.give, args.gold, args.item)
    else:
        revert(args.storage)


if __name__ == "__main__":
    main()
