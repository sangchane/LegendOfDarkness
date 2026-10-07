#!/usr/bin/env python3
"""경매장 보고(설계 autopilot/loot-auction/ 07 관측성 · SC-006) — 사건 기록과 경매장 파일로 숫자를 낸다. 아무것도 쓰지 않는다.

  python3 scripts/ops/auction-report.py <auction 폴더> [<LoruleConfig.json>]
  클라우드: LOD_CLOUD_IP=… scripts/ops/cloud-server.sh auction-report

보는 것
  · 사건 종류별 수 — 사람·봇 나눠(봇 = 서버 설정 EcoBots·CompanionBots 이름)
  · 봇마다 걸어 둔 올림의 최대 — 5(ECO_AUCTION_MAX) 넘으면 경고(07 R3)
  · 끊긴 조작 — seq 가 있는데 commit 도 abort 도 없는 것(07 R1). 되살리기(auction-revert.py --give) 전에 auction.json 과 캐릭터 파일로
    정말 빠졌는지 먼저 본다 — 경매장 파일을 늦게 쓴 조작은 다음 저장 때 commit 이 적힌다.
  · 받기에서 캐릭터를 저장하지 못한 것(unsaved) — 다음 주기 저장에 들어갔을 수 있다, 캐릭터 파일을 확인한다
  · 금화가 기록과 맞나 — 올림·입찰·즉시 구매·취소는 goldBefore − goldAfter = gold, 받기는 goldAfter − goldBefore = gold (INV-2 대신 보는 것)
  · 지금 올린 것·받을 것, 맡긴 금화, 거래된 금화와 수수료(5%)
"""
from __future__ import annotations

import json
import os
import re
import sys
from collections import Counter

PAID = ("post", "bid", "buyout", "cancel")   # 내는 쪽 금화가 gold 만큼 준다
CUT = 5


def bots(config: str | None) -> set[str]:
    if not config or not os.path.exists(config):
        return set()
    text = open(config, encoding="utf-8-sig").read()
    names: set[str] = set()
    for key in ("EcoBots", "CompanionBots"):
        if found := re.search(rf'"{key}"\s*:\s*\[([^\]]*)\]', text):
            names |= {name.lower() for name in re.findall(r'"([^"]+)"', found.group(1))}
    return names


def lines(folder: str) -> list[dict]:
    events = []
    for name in sorted(os.listdir(folder)):
        if name.startswith("events-") and name.endswith(".jsonl"):
            with open(os.path.join(folder, name), encoding="utf-8") as file:
                for line in file:
                    try:
                        events.append(json.loads(line))
                    except json.JSONDecodeError:
                        pass   # 꺼지며 반만 쓰인 줄
    return events


def main() -> None:
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    folder, config = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None
    if not os.path.isdir(folder):
        sys.exit(f"경매장 폴더가 없습니다: {folder}")
    robots = bots(config)
    events = lines(folder)

    def kind(who: str | None) -> str:
        return "봇" if (who or "").lower() in robots else "사람"

    counts: Counter = Counter()
    ends: dict[int, str] = {}
    starts: dict[int, dict] = {}
    wrong = []
    unsaved = []
    traded = cut = 0
    seller: dict[int, str] = {}
    active: Counter = Counter()
    most: Counter = Counter()

    for event in events:
        ev, seq, who = event.get("ev"), event.get("seq", 0), event.get("who")
        if ev in ("commit", "abort", "unsaved"):
            ends[seq] = ev
            if ev == "unsaved":
                unsaved.append(event)
            continue
        counts[(ev, kind(who))] += 1
        if seq and ev != "outbid":
            starts.setdefault(seq, event)
        before, after, gold = event.get("goldBefore"), event.get("goldAfter"), event.get("gold", 0)
        if before is not None and after is not None:
            moved = before - after if ev in PAID else after - before if ev == "take" else None
            if moved is not None and moved != gold:
                wrong.append(event)
        if ev in ("buyout", "sold"):
            traded += gold
            cut += gold * CUT // 100

        # 봇마다 걸어 둔 올림 — post 로 늘고, 팔림·유찰·취소로 준다.
        listing = event.get("listing", 0)
        if ev == "post":
            seller[listing] = who
            active[who] += 1
            most[who] = max(most[who], active[who])
        elif ev in ("buyout", "sold", "expired", "cancel") and listing in seller:
            active[seller.pop(listing)] -= 1

    broken = [event for seq, event in starts.items() if seq not in ends]
    print(f"사건 {len(events)}줄 · 봇 이름 {len(robots)}개")
    for (ev, who), n in sorted(counts.items()):
        print(f"  {ev:8} {who:2} {n}")

    bot_most = {name: n for name, n in most.items() if kind(name) == "봇"}
    over = {name: n for name, n in bot_most.items() if n > 5}
    print(f"봇 올림 최대: {max(bot_most.values(), default=0)}" + (f" — 5 넘음: {over}" if over else ""))
    print(f"거래된 금화 {traded:,}전 · 수수료 {cut:,}전")
    print(f"끊긴 조작(commit·abort 없음): {len(broken)}" + "".join(f"\n  seq {e['seq']} {e.get('ev')} {e.get('who')} {e.get('item')} {e.get('gold')}" for e in broken))
    print(f"금화가 기록과 다른 줄: {len(wrong)}" + "".join(f"\n  seq {e.get('seq')} {e.get('ev')} {e.get('who')}" for e in wrong))
    print(f"받기 뒤 캐릭터 저장 못 함: {len(unsaved)}" + "".join(f"\n  seq {e.get('seq')} {e.get('who')} — 캐릭터 파일을 보고 빠졌을 때만 되살린다" for e in unsaved))
    if broken:
        print("  ↳ 끊긴 조작은 auction.json 의 Listings·Claims 와 캐릭터 파일로 정말 빠졌는지 본 뒤에만 --give 로 되살린다(07 R1)")

    book_path = os.path.join(folder, "auction.json")
    if os.path.exists(book_path):
        with open(book_path, encoding="utf-8-sig") as file:
            book = json.load(file)
        listings, claims = book.get("Listings", []), book.get("Claims", [])
        held = sum(int(one.get("Deposit") or 0) + int(one.get("Bid") or 0) for one in listings)
        owed = sum(int(one.get("Gold") or 0) for one in claims)
        print(f"지금 올린 것 {len(listings)} (봇 {sum(kind(one['Seller']) == '봇' for one in listings)}) · 받을 것 {len(claims)} · 맡긴 금화 {held:,} · 받을 금화 {owed:,}")

    if broken or wrong or over or unsaved:
        sys.exit(1)


if __name__ == "__main__":
    main()
