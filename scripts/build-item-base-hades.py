#!/usr/bin/env python3
"""하데스가 싣는 **영문 아이템** 989장을 꺼낸다 — `origin/Zolian` 브랜치.

`origin/master` 는 아이템 템플릿을 3장만 싣는다(`Luathas_Bronze_Shield` 등). 그것만 보고
"하데스에 아이템 자료가 없다" 고 판단해 팩 989개를 베이스로 삼은 일이 있었다. 실제로는
`origin/Zolian` 브랜치에 **989장이 완전한 스펙으로** 있다:

  Band of Destruction — Image 46145 · EquipmentSlot 6 · LevelRequired 1
                        AcModifer 내림 50 · ScriptName Necklace · Flags 1151

**이미지 번호가 여기 있다.** 클라이언트가 받는 메타파일(`ItemInfo`)에는 이미지가 없다 —
서버가 보내는 값이라 들어갈 이유가 없다. 그래서 한글 이름을 이미지로 맞추려면 이 자료가
필요하다.

  쓰는 법:
    git -C sources/wren11/Dark-Ages-Private-Server archive origin/Zolian \\
        Data/LoruleData/templates/Items | tar -x -C <임시폴더>
    ZOLIAN_ITEMS=<임시폴더>/Data/LoruleData/templates/Items \\
        python3 scripts/build-item-base-hades.py --write
"""
import collections, glob, json, os, sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "data/game-data/items-hades.json"

KEEP = ("Name", "Image", "DisplayImage", "EquipmentSlot", "Class", "Gender",
        "LevelRequired", "ScriptName", "Flags", "CanStack", "MaxStack", "Value",
        "Weight", "DmgMin", "DmgMax", "MaxDurability", "DropRate", "Enchantable",
        "AcModifer", "StrModifer", "IntModifer", "WisModifer", "ConModifer",
        "DexModifer", "HealthModifer", "ManaModifer", "MrModifer", "HitModifer", "DmgModifer")


def main():
    where = os.environ.get("ZOLIAN_ITEMS", "")
    files = sorted(glob.glob(str(Path(where) / "*.json"))) if where else []

    if not files:
        print("풀어 둔 Zolian 아이템 폴더를 ZOLIAN_ITEMS 로 알려 준다 (위 주석 참고).")
        return

    items, slots, withac = [], collections.Counter(), 0
    for f in files:
        try:
            d = json.loads(Path(f).read_text(encoding="utf-8-sig"))
        except Exception:
            continue
        row = {k: d[k] for k in KEEP if k in d and d[k] is not None}
        row.setdefault("Name", Path(f).stem)
        items.append(row)
        slots[row.get("EquipmentSlot")] += 1
        if row.get("AcModifer"):
            withac += 1

    print(f"하데스(Zolian) 아이템 **{len(items)}장**")
    print(f"  이미지가 있는 것 {sum(1 for r in items if r.get('Image'))} · 방어력이 있는 것 {withac}")
    print("  착용 자리별: " + " · ".join(
        f"{k} {v}" for k, v in sorted(slots.items(), key=lambda x: (x[0] is None, x[0]))))

    if "--write" in sys.argv:
        OUT.write_text(json.dumps(items, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"\n→ {OUT.relative_to(ROOT)}  ({len(items)}장)")
    else:
        print("\n(예행 연습이다. 쓰려면 --write)")


if __name__ == "__main__":
    main()
