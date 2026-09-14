#!/usr/bin/env python3
"""서버팩 여러 개와 Hades 를 나란히 놓고 무엇을 믿을 수 있는지 센다.

팩 수를 고정하지 않는다 — `data/server-packs/extracted/<팩>/` 에 있는 것을 전부 읽는다.

**이름이 같다고 같은 것이 아니다.** 그래서 이름 말고 자료가 스스로 들고 있는
식별 신호(그림 번호)로 잇고, 수치는 팩끼리 얼마나 맞는지를 따로 센다.
한 갈래에서 이름은 합의해도 수치는 전혀 합의하지 않을 수 있다 — 실제로 그렇다.

  쓰는 법: python3 scripts/compare-packs.py
  산출물:  data/pack-compare/summary.json · item-korean-names.json
"""
import json, sys
from collections import defaultdict
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
EXTRACTED = ROOT / "data" / "server-packs" / "extracted"
HADES_ITEMS = ROOT / "data" / "game-data" / "items-hades.json"
OUT = ROOT / "data" / "pack-compare"

configure_utf8_stdio(sys.stdout, sys.stderr)

# 갈래마다 "같은 것인지" 를 가리는 신호. 이름은 어느 갈래에서나 약한 신호다.
IMAGE_FIELD = "이미지"
MOB_STATS = ("체력", "최소공격력", "최대공격력", "경험치", "방어")

# 5.99 와 novaonline 은 배포 계보를 공유할 수 있다 (docs/server-packs/novaonline.md).
# 둘만의 일치는 "독립된 두 표의 합의" 로 세지 않는다.
SUSPECTED_SAME_LINEAGE = frozenset({"5.99-server", "novaonline"})


def packs():
    return sorted(d.name for d in EXTRACTED.iterdir() if d.is_dir())


def load(pack, key):
    f = EXTRACTED / pack / f"{key}.json"
    return json.loads(f.read_text(encoding="utf-8")) if f.exists() else []


def is_placeholder(name):
    """이름이 숫자뿐인 줄은 아이템이 아니다.

    novaonline 의 `item/아이템이미지(작성).txt` 100줄은 운영자가 그림 번호를 적어 둔
    참고표다 — 이름 자리에 그림 번호가 그대로 들어 있다. 한글 이름 후보로 세면 안 된다.
    """
    return name.strip().isdigit()


def grade(by_pack):
    """어느 등급으로 믿을 것인가. by_pack: {팩: [이름...]}

    NEXT.md 의 등급을 그대로 쓴다 — VERIFIED 는 Hades·원작 자료의 몫이라 여기서 나오지 않는다.
    """
    names = {n for v in by_pack.values() for n in v}
    if not names:
        return "NONE"
    if len(names) > 1:
        return "CONFLICT"
    agreeing = set(by_pack)
    if len(agreeing) < 2:
        return "EXTRACTED_SINGLE"
    if agreeing <= SUSPECTED_SAME_LINEAGE:
        return "SAME_LINEAGE"          # 합의로 세지 않는다 — 계보를 먼저 확인해야 한다
    return "PACK_CONSENSUS"


def names_by_image(key):
    """그림 번호 → {팩: [이름...]}. 팩이 스스로 들고 있는 유일한 공통 식별자다."""
    out = defaultdict(lambda: defaultdict(set))
    for pack in packs():
        for e in load(pack, key):
            img = str(e.get("fields", {}).get(IMAGE_FIELD, "")).strip()
            if img.isdigit() and not is_placeholder(e["이름"]):
                out[int(img)][pack].add(e["이름"])
    return {img: {p: sorted(v) for p, v in d.items()} for img, d in out.items()}


def overlap(key):
    """이름이 겹치는 수와, 그중 그림까지 맞는 수."""
    per = {}
    for pack in packs():
        per[pack] = {e["이름"]: str(e.get("fields", {}).get(IMAGE_FIELD, "")) for e in load(pack, key)}
    common = set.intersection(*(set(v) for v in per.values())) if per else set()
    same_image = {n for n in common if len({per[p][n] for p in per}) == 1 and per[list(per)[0]][n]}
    return {"팩별": {p: len(v) for p, v in per.items()},
            "이름이 모두 겹친다": len(common),
            "그림까지 같다": len(same_image),
            "이름만 같고 그림이 다르다": sorted(common - same_image)[:20]}


def map_overlap():
    """맵은 이름이 아니라 맵파일 번호가 신원이다. 팩마다 제 맵을 만들어 넣어서 겹치는 것이 적다."""
    per = defaultdict(lambda: defaultdict(set))
    for pack in packs():
        for e in load(pack, "maps"):
            digits = "".join(c for c in str(e.get("fields", {}).get("맵파일", "")) if c.isdigit())
            if digits:
                per[int(digits)][pack].add(e["이름"])
    every = [n for n, d in per.items() if len(d) == len(packs())]
    same_name = [n for n in every if len({x for v in per[n].values() for x in v}) == 1]
    return {"맵파일 번호": len(per), "팩 모두에 있다": len(every),
            "이름까지 같다": len(same_name), "보기": sorted(same_name)[:10]}


def mob_stat_agreement():
    """이름이 겹치는 괴물에서 수치가 실제로 맞는지. 팩을 수치의 근거로 쓸 수 있나를 가른다."""
    per = {p: {e["이름"]: e.get("fields", {}) for e in load(p, "mobs")} for p in packs()}
    common = sorted(set.intersection(*(set(v) for v in per.values()))) if per else []
    out = {"이름이 모두 겹치는 괴물": len(common), "칸별 일치": {}}
    for field in MOB_STATS:
        agree = sum(1 for n in common
                    if len({per[p][n].get(field) for p in per}) == 1 and per[list(per)[0]][n].get(field))
        out["칸별 일치"][field] = agree
    return out


def hades_item_korean_names():
    """Hades 의 영문 아이템에 팩의 한글 이름을 잇는다 — 그림 번호가 같으면 같은 그림이다.

    Hades 의 `Image` 와 팩의 `이미지` 는 같은 번호 체계다 (Leather Greaves 238 = 가죽각반 238).
    다만 **같은 그림에 이름이 여럿** 인 일이 흔하다 — 형용사만 다른 변종이라 사람이 고른다.
    """
    if not HADES_ITEMS.exists():
        return [], {"없음": str(HADES_ITEMS.relative_to(ROOT))}
    by_img = names_by_image("items")
    rows = []
    for h in json.loads(HADES_ITEMS.read_text(encoding="utf-8")):
        cand = by_img.get(h.get("Image"), {})
        rows.append({"영문": h["Name"], "그림": h.get("Image"),
                     "착용자리": h.get("EquipmentSlot"), "요구레벨": h.get("LevelRequired"),
                     "한글후보": cand, "등급": grade(cand)})
    tally = defaultdict(int)
    for r in rows:
        tally[r["등급"]] += 1
    return rows, dict(tally)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    summary = {"팩": packs(),
               "갈래별 겹침": {k: overlap(k) for k in ("mobs", "items", "npcs", "skills", "spells")},
               "맵 겹침": map_overlap(),
               "괴물 수치 합의": mob_stat_agreement()}
    rows, tally = hades_item_korean_names()
    summary["Hades 아이템 한글 이름"] = tally
    (OUT / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=1), encoding="utf-8")
    (OUT / "item-korean-names.json").write_text(json.dumps(rows, ensure_ascii=False, indent=1), encoding="utf-8")

    print(f"팩 {len(packs())}개: {' · '.join(packs())}")
    for kind, o in summary["갈래별 겹침"].items():
        print(f"  {kind:7s} " + " ".join(f"{n:5d}" for n in o['팩별'].values())
              + f" | 이름이 모두 겹친다 {o['이름이 모두 겹친다']:4d} · 그림까지 {o['그림까지 같다']:4d}")
    mo = summary["맵 겹침"]
    print(f"  maps    맵파일 번호 {mo['맵파일 번호']:5d} | 팩 모두에 있다 {mo['팩 모두에 있다']:4d}"
          f" · 이름까지 같다 {mo['이름까지 같다']:4d}")
    m = summary["괴물 수치 합의"]
    print(f"\n괴물 {m['이름이 모두 겹치는 괴물']}마리는 이름이 모두 겹친다. 그런데 수치는:")
    for f, n in m["칸별 일치"].items():
        print(f"  {f:8s} {n:3d}/{m['이름이 모두 겹치는 괴물']} 일치")
    print("\nHades 아이템에 붙일 한글 이름:")
    for g, n in sorted(tally.items(), key=lambda kv: -kv[1]):
        print(f"  {g:17s} {n:4d}")
    print(f"\n→ {OUT.relative_to(ROOT)}/")


if __name__ == "__main__":
    main()
