#!/usr/bin/env python3
"""우드랜드 서·북 — 원작 서의우드랜드(lod441~464)·북의우드랜드(lod700~723)를 노바 팩에서 새로 넣는다(사용자 2026-10-04).
SPEC `plans/woodland-west-north-spec-2026-10-04.md`. 지금 서버의 「우드랜드」(5.99 판)는 동의로 보고 건드리지 않는다.

  - 맵: 원작 = 노바 바이트까지 같다(`data/map-origins/woodland-origins.json`). 맥에서는 5.99 서버팩 `db/maps/default/maps/`
    가 같은 md5 라 거기서 복사한다(md5 대조). 서·북 정의 전부(1-1~20-1, 갈래 9-2·17-2·19-2).
    노바의 `…대기실` 과 `…입구` 는 같은 맵 파일이라 `서의·북의우드랜드입구` 한 장으로 합친다. 번호는 지금 가장 큰 번호 다음부터.
  - 워프: 노바 줄 그대로, 레벨은 노바 7·8번째 칸(최소·최대) — 우드랜드는 모두 0~99 라 제한 없음(1).
    노바에 길이 없는 구역은 NEW_LINKS 로 잇는다(사용자 2026-10-04) — 칸은 원작 맵 가장자리의 열린 칸(`edge_door`).
    입구 (11,24)(12,24) → 월드맵(노바 `warp/worldmap.txt`, build-rucesion-coast.py 의 door 처럼).
  - 월드맵 카드: 원작 field001 서 (155,171)·북 (255,96), 도착은 노바 월드맵의 대기실 칸 (10,16), 구역 바로가기.
  - 괴물: 노바 `mob/spawn.txt` 배치, 체력·공격·방어 노바 그대로(「노바 안에서 맞춤」), 경험치 ÷ 7.3(tools/pack-import
    EXPERIENCE_DIVISOR, 호러캐슬과 같은 규칙), 그림 0x4000 + 노바 이미지. 노바 자료 오류 셋만 고친다(FIX).
    노바 배치가 없는 구역은 같은 줄의 앞·뒤 구역으로 채운다(`fill`).
    드랍은 노바 목록 중 하데스 아이템에 있는 것 — 장비 칸은 그 뒤 build-gear-drops → build-drop-variety → build-drop-cap 이 단마다 한 벌로 바꾼다.

  쓰는 법: python3 scripts/gen/world/build-woodland-west-north.py            # 무엇이 바뀌는지만
           python3 scripts/gen/world/build-woodland-west-north.py --쓰기     # 서버 자료에 쓴다
"""
import hashlib
import json
import shutil
import struct
import sys
from collections import deque
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))   # scripts/ — lib/
from lib._paths import ROOT
from lib._world import AREAS, LOOT_RANDOM, MAPS, SERVER, WARPS, WORLDMAP, warp, world_card

NOVA = ROOT / "data" / "server-packs" / "extracted" / "novaonline"
ORIGINS = ROOT / "data" / "map-origins" / "woodland-origins.json"
MAP_FILES = Path.home() / "Downloads" / "5.99 서버팩" / "db" / "maps" / "default" / "maps"
ITEMS = SERVER / "templates" / "items"
FLAGS, MUSIC = 106240, 173             # 하데스 기존 맵 깃발 · 지금 우드랜드 배경음(노바 정의엔 배경음이 없다)
EXPERIENCE_DIVISOR = 7.3
LOBBY_ARRIVAL = (10, 16)               # 노바 worldmap.txt 「서의·북의우드랜드 … 우드랜드대기실,10,16」
DOOR = [(11, 24), (12, 24)]            # 노바 warp/worldmap.txt 입구 → 마이소시아(월드맵)
# 쪽마다: 우리 이름 앞머리, 노바 맵 정의 파일, 노바 이름 앞머리(서는 방향이 빠졌다), 노바 대기실·입구 이름, 카드 자리
SIDES = {
    "서의우드랜드": ("west.txt", "우드랜드 ", ("우드랜드대기실", "우드랜드입구"), (155, 171)),
    "북의우드랜드": ("north.txt", "북의우드랜드 ", ("북의우드랜드대기실", "북의우드랜드입구"), (255, 96)),
}
# 노바 자료 오류만 고친다(SPEC 4): 맨티스 체력 ×1000 오타 · 녹색말벌 최소>최대 · 우드랜드보스1 방어 칸 이름이 「ㅍ」.
# 노바에 길이 없는 구역(사용자 2026-10-04 「6-1부터 13까지, 14부터 20까지 연결」) — 두 줄, 양방향:
#   줄1  … 6-1 → 9-1(노바 길) → 10-1 → 11-1 → 12-1 → 13-1. 9-2 보스방은 노바처럼 8-1 곁가지 그대로.
#   줄2  입구 → 14-1 → 15-1 → … → 20-1. 17-2·19-2 는 17-1·19-1 곁가지(노바 8-1 → 9-2 처럼 「-2」 는 곁방).
#        입구 → 14-1 은 서는 노바 길(21~22,0) 그대로, 북은 노바에 없어 서와 같은 자리로 하나 낸다(13 → 14 는 잇지 않는다).
NEW_LINKS = [("9-1", "10-1"), ("10-1", "11-1"), ("11-1", "12-1"), ("12-1", "13-1"),
             ("입구", "14-1"), ("14-1", "15-1"), ("15-1", "16-1"), ("16-1", "17-1"), ("17-1", "18-1"),
             ("17-1", "17-2"), ("18-1", "19-1"), ("19-1", "20-1"), ("19-1", "19-2")]   # 본줄이 가운데 칸을 먼저 잡게
GATE_HINT = 21                         # 입구 위 가장자리에서 14-1 로 나가는 칸 — 서 노바 길(21~22,0) 자리
SOTP = SERVER / "static" / "sotp.dat"
FIX = {"맨티스": {"체력": "6040"}, "녹색말벌": {"최소공격력": "55", "최대공격력": "60"}, "우드랜드보스1": {"방어력": "1"}}


def rows(name):
    data = json.loads((NOVA / name).read_text(encoding="utf-8"))
    return data if isinstance(data, list) else next(iter(data.values()))


def drop_names(listed):
    """노바 드롭 칸 ["5", "로오의가죽방패", …] 의 이름들."""
    return [c for c in listed or [] if isinstance(c, str) and not c.isdigit()]


def side_maps(side):
    """노바 맵 정의 — 우리 이름 → (원작 번호, 너비, 높이). 대기실·입구는 우리 `…입구` 하나로."""
    file, prefix, lobbies, _ = SIDES[side]
    out = {}
    for m in rows("maps.json"):
        f = m["fields"]
        if m["출처"].endswith(f"woodland/{file}"):
            name = f"{side}입구" if f["이름"] in lobbies else side + f["이름"][len(prefix):]
            out[name] = (int(f["번호"]), int(f["너비"]), int(f["높이"]))
    return out


def our(side, nova_name):
    file, prefix, lobbies, _ = SIDES[side]
    if nova_name in lobbies:
        return f"{side}입구"
    return side + nova_name[len(prefix):] if nova_name.startswith(prefix) else None


def walls(number, cols):
    """원작 맵의 벽 칸 — 서버와 같은 셈(WorldMapTests.Walled: 왼·오른 그림 중 sotp 가 0x0F 인 것)."""
    data, sotp = (MAP_FILES / f"lod{number}.map").read_bytes(), SOTP.read_bytes()
    return lambda x, y: any(t and sotp[t - 1] == 0x0F for t in struct.unpack_from("<hh", data, (y * cols + x) * 6 + 2))


def edge_door(number, cols, rows, used, edges, hint=None):
    """가장자리에서 문 칸 둘 — 밟는 칸과 그 안쪽 칸이 모두 열려 있고, 이미 쓴 칸(다른 문)과 세 칸 넘게 떨어진 곳.
    가장자리는 edges 차례로 보고, 한 가장자리 안에서는 hint(없으면 가운데)에 가장 가까운 자리. → [(밟는 칸, 안쪽 칸)] 둘."""
    wall = walls(number, cols)
    lines = {"top": [((x, 0), (x, 1)) for x in range(cols)], "bottom": [((x, rows - 1), (x, rows - 2)) for x in range(cols)],
             "left": [((0, y), (1, y)) for y in range(rows)], "right": [((cols - 1, y), (cols - 2, y)) for y in range(rows)]}
    for edge in edges:
        cells = lines[edge]
        ok = [not wall(*t) and not wall(*i) and all(max(abs(t[0] - u[0]), abs(t[1] - u[1])) > 3 for u in used) for t, i in cells]
        spots = [k for k in range(len(cells) - 1) if ok[k] and ok[k + 1]]
        if spots:
            k = min(spots, key=lambda k: abs(k - (len(cells) // 2 if hint is None else hint)))
            return [cells[k], cells[k + 1]]
    raise SystemExit(f"lod{number}: 열린 가장자리가 없다")


def fill(side, counts, zones):
    """노바 배치가 없는 구역 — 같은 줄(1~13 · 14~20)에서 노바 배치가 있는 가장 가까운 앞·뒤 구역(갈래 「-2」 방은 빼고)의
    종마다 평균(없는 종 0, 반올림), 뒤가 없으면 앞 그대로(사용자 2026-10-04 「이웃 구역 흐름에 맞춰」). 예: 북 4-1 = 3-1(말벌·맨티스
    10) 과 5-1(맨티스·늑대 30) → 말벌 5 · 맨티스 20 · 늑대 15. → {(괴물, 구역): 마릿수} 더할 것과 근거 글."""
    number = lambda z: int(z[len(side):].split("-")[0])
    line = lambda z: number(z) >= 14
    full = {z for _, z in counts}
    anchors = [z for z in full if not z.endswith("-2")]
    added, why = {}, []
    for z in zones:
        if z in full or z.endswith("입구"):
            continue
        before = max((a for a in anchors if line(a) == line(z) and number(a) < number(z)), key=number, default=None)
        after = min((a for a in anchors if line(a) == line(z) and number(a) > number(z)), key=number, default=None)
        used = [a for a in (before, after) if a]
        for kind in sorted({k for k, a in counts if a in used}):
            added[(kind, z)] = int(sum(counts.get((kind, a), 0) for a in used) / len(used) + 0.5)
        why.append(f"{z} ← {' · '.join(used)}")
    return added, why


def monster(kind, f, count, area, have):
    f = {**f, **FIX.get(kind, {})}
    speed = int(f.get("속도") or 1500)
    drops = [d for d in drop_names(f.get("드롭아이템")) if d in have]
    return {
        "Name": kind, "BaseName": kind, "AreaID": area,
        "SpawnMax": count, "SpawnType": 2, "SpawnRate": int(f.get("젠타임") or 30), "SpawnSize": 0, "SpawnOnlyOnActiveMaps": False,
        "Image": 0x4000 + int(f["이미지"]), "ImageVarience": 0,
        "MaximumHP": int(f["체력"]), "MaximumMP": 0, "Exp": round(int(f["경험치"]) / EXPERIENCE_DIVISOR),
        "DmgMin": int(f["최소공격력"]), "DmgMax": int(f["최대공격력"]), "Ac": int(f["방어력"]),
        "Level": 1, "MovementSpeed": speed, "EngagedWalkingSpeed": speed, "AttackSpeed": 1000, "CastSpeed": 8000,
        "MoodType": 4, "PathQualifer": 1,
        "LootType": LOOT_RANDOM, "Drops": {"$values": drops},
        "ScriptName": "Common Monster", "UpdateMapWide": True, "UpdateRate": 1000.0,
        "Grow": False, "IgnoreCollision": False,
    }


def main():
    write = "--쓰기" in sys.argv
    want = {r["번호"]: r["출처"]["노바온라인"].get("md5") for r in json.loads(ORIGINS.read_text(encoding="utf-8"))["맵"]}
    existing = {}
    for path in AREAS.glob("*.json"):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        existing[data["Name"]] = data["Id"]
    next_id = max(i for i in existing.values() if i < 65536) + 1     # 맵 번호는 전선에서 16비트다
    nova_warps = [w for w in rows("warps.json") if w["출처"] == "warp/woodland.txt"]
    spawns, stats = rows("mob_spawns.json"), {m["이름"]: m["fields"] for m in rows("mobs.json")}
    have = {p.stem for p in ITEMS.glob("*.json")}

    plan, cards, warps, mobs, dropped, skipped, new_links, filled = [], [], [], [], [], [], [], []
    for side, (_, _, _, point) in SIDES.items():
        maps = side_maps(side)
        lobby = f"{side}입구"
        links = []                                                    # (출발, 칸, 도착, 칸, 레벨)
        for w in nova_warps:
            a, b = our(side, w["출발맵"]), our(side, w["도착맵"])
            if a in maps and b in maps:
                low, high = int(w["raw"][7]), int(w["raw"][8])
                assert high >= 99, w                                  # 최대 레벨 제한이 있으면 LevelMaximum 도 옮겨야 한다
                links.append((a, tuple(map(int, w["출발"])), b, tuple(map(int, w["도착"])), max(1, low)))
        used = {}                                                     # 맵마다 이미 문인 칸
        for a, at, b, to, _ in links:
            used.setdefault(a, set()).add(at)
        for near, far in NEW_LINKS:                                   # 노바에 길이 없는 구역 — 얕은 쪽 위 가장자리 ↔ 깊은 쪽 아래 가장자리
            a, b = side + near, side + far
            if any(x == a and y == b for x, _, y, _, _ in links):
                continue                                              # 서 입구 → 14-1 은 노바 길
            (na, ca, ra), (nb, cb, rb) = maps[a], maps[b]
            out = edge_door(na, ca, ra, used.get(a, set()), ["top", "right", "left", "bottom"], GATE_HINT if near == "입구" else None)
            back = edge_door(nb, cb, rb, used.get(b, set()), ["bottom", "left", "right", "top"])
            for (at, inside_a), (to, inside_b) in zip(out, back):
                links += [(a, at, b, inside_b, 1), (b, to, a, inside_a, 1)]
                used.setdefault(a, set()).add(at)
                used.setdefault(b, set()).add(to)
            new_links.append(f"{a} {out[0][0]}~{out[1][0]} ↔ {b} {back[0][0]}~{back[1][0]}")
        reached, todo = {lobby}, deque([lobby])                       # 입구에서 워프로 닿는 맵만
        while todo:
            here = todo.popleft()
            for a, _, b, _, _ in links:
                if a == here and b not in reached:
                    reached.add(b)
                    todo.append(b)
        skipped += [f"{n}(lod{maps[n][0]})" for n in sorted(maps) if n not in reached]
        order = sorted(reached - {lobby}, key=lambda n: tuple(int(x) for x in n[len(side):].split("-")))

        ids = {}
        for name in [lobby, *order]:
            number, cols, rows_ = maps[name]
            if name in existing:                                      # 이미 넣었다 — 다시 돌려도 같은 번호
                ids[name] = existing[name]
                continue
            ids[name] = next_id
            next_id += 1
            source = MAP_FILES / f"lod{number}.map"
            assert hashlib.md5(source.read_bytes()).hexdigest() == want[str(number)], f"{source} 가 원작과 다르다"
            plan.append(f"  새 맵 {ids[name]}  {name} ← lod{number} {cols}x{rows_}")
            if write:
                shutil.copyfile(source, MAPS / f"lod{ids[name]}.map")
                area = {"FilePath": f"../../database/server/maps/lod{ids[name]}.map", "Cols": cols, "ContentName": None,
                        "Flags": FLAGS, "Id": ids[name], "Music": MUSIC, "Name": name, "Rows": rows_,
                        "Blocks": [], "ScriptKey": None, "ID": ids[name]}
                (AREAS / f"{name}.json").write_text(json.dumps(area, ensure_ascii=False, indent=2), encoding="utf-8")

        side_warps = [warp(a, ids[a], at, b, ids[b], to, level) for a, at, b, to, level in links if a in reached and b in reached]
        door = f"warp {lobby} to world map"
        side_warps.append((door, {
            "ActivationMapId": ids[lobby],
            "Activations": [{"AreaID": ids[lobby], "Location": {"X": x, "Y": y}, "PortalKey": 0} for x, y in DOOR],
            "LevelRequired": 1, "To": {"AreaID": 0, "Location": None, "PortalKey": 1},
            "WarpRadius": 0, "WarpType": "World", "WorldResetWarpId": 0, "WorldTransionWarpId": 0,
            "Description": None, "Group": None, "Name": door,
        }))
        warps += side_warps
        cards.append((side, ids[lobby], point, [ids[n] for n in order], side_warps))

        prefix, counts = SIDES[side][1], {}
        for s in spawns:                                              # 같은 맵·같은 괴물 줄이 둘이면 마릿수를 합친다
            name = our(side, s["맵"]) if s["맵"].startswith(prefix) else None
            if name not in ids:
                continue
            if s["괴물"] not in stats:
                dropped.append(f"{s['괴물']}@{name}")
                continue
            counts[(s["괴물"], name)] = counts.get((s["괴물"], name), 0) + int(s["마리수"])
        added, why = fill(side, counts, order)
        counts.update(added)
        filled += why
        mobs += [(side, f"{kind}@{name}", monster(kind, stats[kind], n, ids[name], have)) for (kind, name), n in counts.items()]

    if write:
        for n, w in warps:
            (WARPS / f"{n}.json").write_text(json.dumps(w, ensure_ascii=False, indent=2), encoding="utf-8")
        for side, lobby_id, point, zones, side_warps in cards:          # world_card 가 파일에서 읽으니 하나씩 적는다
            WORLDMAP.write_text(json.dumps(world_card(side, lobby_id, LOBBY_ARRIVAL, point, zones, side_warps),
                                           ensure_ascii=False, indent=2), encoding="utf-8")
        for side, n, m in mobs:
            folder = SERVER / "templates" / "monsters" / side
            folder.mkdir(parents=True, exist_ok=True)
            (folder / f"{n}.json").write_text(json.dumps(m, ensure_ascii=False, indent=2), encoding="utf-8")

    print("\n".join(plan))
    for side, n, m in mobs:
        print(f"  {n:22} {m['SpawnMax']:>3}마리 · 체력 {m['MaximumHP']:>6,} · 공격 {m['DmgMin']}~{m['DmgMax']} · 방어 {m['Ac']}"
              f" · 경험치 {m['Exp']:,} · 그림 {m['Image'] - 0x4000} · 드랍 {', '.join(m['Drops']['$values']) or '-'}")
    for side, lobby_id, point, zones, side_warps in cards:
        print(f"카드 {side} {point} → {lobby_id}{LOBBY_ARRIVAL} · 구역 {len(zones)} · 워프 {len(side_warps)}")
    print("새 길: " + "\n       ".join(new_links))
    print(f"노바 배치가 없어 채운 구역: {' · '.join(filled)}")
    print(f"뺀 맵(입구에서 워프로 안 닿음): {' · '.join(skipped) or '없음'}")
    print(f"정의 없는 괴물(뺌): {' · '.join(sorted(set(dropped)))}")
    print(f"맵 새 {len(plan)} · 워프 {len(warps)} · 괴물 자리 {len(mobs)}" + ("" if write else "  — 미리보기, --쓰기 로 쓴다"))


if __name__ == "__main__":
    main()
