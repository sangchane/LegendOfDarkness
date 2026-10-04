#!/usr/bin/env python3
"""죽음의마을 — 99레벨 사냥터(사용자 2026-10-04). 하데스에 맵 다섯 장(입구5·죽음의마을1~4)과 괴물(5.99 팩)이 있는데
입구5에서 본 맵으로 드는 워프가 없어 들어갈 수 없던 것을 잇는다. SPEC `plans/death-village-spec-2026-10-04.md`.

  - 들어가는 길: 입구5(7~11,0) → 죽음의마을1(44,94), 99레벨 — 노바 `warp/죽음의마을.txt` 와 5.99 `VOD_Warp.txt` 가
    같은 칸·같은 도착이다(5.99 는 「Warp_죽음의마을」 이라는 사본 이름으로 보냈다).
  - 마인마을(43~48,0) → 입구5 워프 여섯 장은 99레벨로 — 5.99 `Mine_Warp.txt` 원본 값(가져올 때 1 이 됐다).
  - 월드맵 사냥터 카드 「죽음의마을」: 원작 field001 자리(142,320), 입구5 도착(11,17)은 마인마을 워프와 같은 칸,
    구역 1~4 바로 가기. 레벨은 서버가 입구5로 드는 워프 중 가장 엄한 것(99)을 건다(WorldMapRefusal).
  - 괴물: 배치·경험치·드랍은 5.99 그대로(사용자 「경험치 유지」). 체력·공격·방어만 노바 `mob/죽음의마을/` 값
    (사용자 「노바 안에서 맞춤」 — 구광산 1층이 노바 광산1층 값 그대로라 노바 값을 그대로 쓰면 둘 사이가 노바와 같다).
    노바에 없는 고사목은 5.99 에서 좀비와 벌어진 비율(체력 ×2.33)을 노바 좀비에 곱한다.
  - 빈집털이(사용자 2026-10-04 「집털이도」): 죽음의마을1 의 NPC 빈집털이·빈집털이1(5.99 스크립트, 이미 있음)이 보내는
    신죽마집안 16장과 그 너머 신죽음의마을1-1~1-6 — 맵·워프·괴물은 5.99 로 이미 있고, 수치만 같은 규칙으로 노바 집털
    (`mob/죽음의마을/` 도깨비불·블랙캣·독거미 — 체력 4만)에 맞춘다. 5.99 독거미2 = 노바 독거미, 노바에 없는 니크르·웨어랫은
    같은 방 독거미2 와의 비율.

  쓰는 법: python3 scripts/gen/world/build-death-village.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-death-village.py --쓰기     # 서버 자료에 쓴다
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from lib._world import AREAS, SERVER, WARPS, WORLDMAP, warp, world_card

NOVA = ROOT / "data" / "server-packs" / "extracted" / "novaonline"
MONSTERS = SERVER / "templates" / "monsters" / "5.99"
NAME = "죽음의마을"
LOBBY = f"{NAME}입구5"
ZONES = [f"{NAME}{n}" for n in range(1, 5)]
GATE = [(x, 0) for x in range(7, 12)]          # 입구5 위쪽 → 죽음의마을1
GATE_TO = (44, 94)
LOBBY_ARRIVAL = (11, 17)                       # 마인마을 워프 도착 칸
CARD_POINT = (142, 320)                        # 원작 field001 죽음의마을
ENTRY_LEVEL = 99
STATS = ("MaximumHP", "DmgMin", "DmgMax", "Ac")
NO_NOVA = {"고사목": "좀비", "니크르": "독거미2", "웨어랫": "독거미2"}   # 노바에 없는 괴물 → 5.99 에서 견줄 괴물
NOVA_NAME = {"독거미2": "독거미"}              # 5.99 이름 → 노바 이름
HOUSES = ("신죽마집안", "신죽음의마을")          # 빈집털이로 드는 곳


def nova_stats():
    data = json.loads((NOVA / "mobs.json").read_text(encoding="utf-8"))
    rows = data if isinstance(data, list) else next(iter(data.values()))
    return {m["이름"]: dict(zip(STATS, (int(m["fields"][k]) for k in ("체력", "최소공격력", "최대공격력", "방어력")))) for m in rows
            if "죽음의마을/" in m["출처"]}


def main():
    write = "--쓰기" in sys.argv
    ids = {}
    for path in AREAS.glob(f"{NAME}*.json"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        ids[data["Name"]] = data["Id"]
    assert all(n in ids for n in [LOBBY, *ZONES]), ids

    gate = [warp(LOBBY, ids[LOBBY], at, ZONES[0], ids[ZONES[0]], GATE_TO, ENTRY_LEVEL) for at in GATE]
    raised = []                                                   # 마인마을 → 입구5
    for path in sorted(WARPS.glob(f"warp 마인마을(*) to {LOBBY}(*).json")):
        w = json.loads(path.read_text(encoding="utf-8-sig"))
        w["LevelRequired"] = ENTRY_LEVEL
        raised.append((path.stem, w))
    assert len(raised) == 6, raised

    inside = [(p.stem, json.loads(p.read_text(encoding="utf-8-sig"))) for p in WARPS.glob(f"warp {NAME}*.json")]
    zone_ids = [ids[z] for z in ZONES]
    world = world_card(NAME, ids[LOBBY], LOBBY_ARRIVAL, CARD_POINT, zone_ids, inside + gate)

    nova = nova_stats()
    mobs = []
    paths = [*MONSTERS.glob(f"*@{NAME}[1-4].json"), *(p for h in HOUSES for p in MONSTERS.glob(f"*@{h}*.json"))]
    for path in sorted(paths):
        m = json.loads(path.read_text(encoding="utf-8-sig"))
        kind = m["Name"]
        if kind not in NO_NOVA:
            new = nova[NOVA_NAME.get(kind, kind)]
        else:                                                     # 5.99 의 비율을 노바 견줄 괴물에 곱한다
            peer = json.loads((MONSTERS / f"{NO_NOVA[kind]}@{path.stem.split('@')[1]}.json").read_text(encoding="utf-8-sig"))
            new = {k: round(nova[NOVA_NAME.get(peer["Name"], peer["Name"])][k] * m[k] / peer[k]) for k in STATS}
        mobs.append((path, {**m, **new}))

    if write:
        for n, w in gate + raised:
            (WARPS / f"{n}.json").write_text(json.dumps(w, ensure_ascii=False, indent=2), encoding="utf-8")
        WORLDMAP.write_text(json.dumps(world, ensure_ascii=False, indent=2), encoding="utf-8")
        for path, m in mobs:
            path.write_text(json.dumps(m, ensure_ascii=False, indent=2), encoding="utf-8")

    for path, m in mobs:
        print(f"  {path.stem:14} 체력 {m['MaximumHP']:>7,} · 공격 {m['DmgMin']:,}~{m['DmgMax']:,} · 방어 {m['Ac']} · 경험치 {m['Exp']:,} · {m['SpawnMax']}마리")
    print(f"들어가는 워프 {len(gate)} · 99레벨로 올린 마인마을 워프 {len(raised)} · 월드맵 카드 구역 {len(zone_ids)} · 괴물 자리 {len(mobs)}"
          + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
