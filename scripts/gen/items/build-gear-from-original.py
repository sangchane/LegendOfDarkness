#!/usr/bin/env python3
"""5.99 팩에서 들여온 입는 물건(무기·갑옷·장신구·방패·투구·장갑·팔찌·각반·벨트·신발·장식)의 값을 **원작 도감** 값으로 되돌린다.

  python3 scripts/gen/items/build-gear-from-original.py            # 무엇이 바뀌는지만 본다
  python3 scripts/gen/items/build-gear-from-original.py --쓰기      # 아이템 템플릿에 적는다

사용자 결정(2026-09-23): **아이템 값의 정본은 원작 도감이다.** 5.99 팩은 같은 이름의 물건에 값을 다시
매긴 갈래다 — 갑옷 값을 계단으로 뭉갰고(전부 300 / 3,000 / 7,000 …), 방어를 깎았고, 레벨 칸을 한 칸씩
올렸다(도감 1·11·41·71·99 → 팩 1·21·51·81·99). 도감에 값이 적힌 칸은 도감대로 되돌린다.

**왜 `build-pack-equipment.py` 안에 넣지 않았나.** 그것은 아이템 json 을 통째로 다시 쓴다
(`path.write_text(...)`). 그래서 다른 생성기가 나중에 얹은 칸을 말없이 지운다 — 지금 설단검·레더튜닉에
붙어 있는 `DropRate` 는 `scripts/gen/items/build-novice-drops.py` 가 얹은 것이라 거기서 같이 돌리면 노비스 드롭
29마리분이 사라진다. 이 생성기는 `build-novice-drops.py` 와 같은 꼴이다: **json 을 읽고, 정한 칸만
고치고, 나머지는 읽은 그대로 다시 쓴다.**

**무엇을 고르나.** `Group` 이 `5.99표/` 로 시작하는 입는 물건(`EquipmentSlot` > 0) 중 이름이 도감 수치표에 있는 것.
팩이 새로 만든 것(도감에 이름이 없는 것)과 하데스표·원작표(이미 도감과 같다)는 손대지 않는다.
처음(2026-09-23)에는 무기·갑옷 169장만 되돌렸다 — 아래 「장신구도」 참조.

**고치는 칸** (괄호는 도감 열 번호):
  무게(3) → CarryWeight · 내구력(9) → MaxDurability
  직업제한(20) → Class · 레벨제한(22) → LevelRequired · 공격력(25) → DmgMin/DmgMax
  방어력(10)·명중수정(11)·공격수정(12)·체력변화(13)·마력변화(14)·힘/덱스/인트/위즈/콘변화(15~19) → *Modifer

**안 고치는 칸** — 도감에 없으니 팩 값이 유일한 근거다:
  Image·DisplayImage(그림) · EquipmentSlot·ScriptName(장비 자리) · Gender·StageRequired
  AttackMotion·AttackSpeed(평타 몸 동작 — 시험이 값을 박아 쓴다) · Flags · MrModifer(마법방어)
  DropRate(`build-novice-drops.py` 것) · Group · CanStack·MaxStack
  도감 열26 **마법공격력**은 `ItemTemplate` 에 둘 칸이 없어 자료로만 남는다.

**도감 값이 0 인 칸은 칸을 뺀다.** 이 템플릿 꼴에서 없는 칸이 곧 0 이다. 그래야 팩이 자릿수를 늘려 둔
것이 풀린다 — 다크크로어 체력·마력 +100,000(도감 0·0) · 페이로브아머 마력 +50,000(도감 −200).

**되돌리지 않는 것: 지팡이 10종**(`KEEP` 참조). 도감의 해당 줄이 **글자 하나까지 똑같아서**
(공격 4m20 · 마법공격 6m30 · 레벨 11) 되돌리면 마법사·성직자 무기 사다리 다섯 칸이 레벨 11 한 칸으로
뭉개진다. 도감이 지팡이 등급을 나누기 전 시절의 표다. 아래에서 그 「똑같음」을 실제로 확인하고,
확인이 깨지면 멈춘다 — 이유가 주석이 아니라 코드에 있어야 한다.

**장신구도 — 사용자 결정(2026-10-09)**: 「아이템 스펙은 이 데이터에 있는걸 기준으로 해야해」 · 「어둠템 값에 맞춰
장비스펙은」 · 「체력이 1000이상 오르는건 승급이후에나 나오고 잘 있지도 않아」. 09-23 에는 장신구·방패·투구·장갑·각반·
벨트·신발·장식을 묶음째 되돌리면 값이 한꺼번에 움직인다고 판매가격 다섯 장만 따라가게 했다. 그 사이 팩 값이 그대로
남아(윙부츠 Lv1 체·마 10,000 — 도감 200 · 산호귀걸이 체·마 100 능력+6 — 도감 인트 2) 사냥터 드랍·상점이 그 값을
기준으로 짜였다. 이제 묶음을 5.99표 입는 물건 전부로 넓힌다(`autopilot/item-specs/SPEC.md`). **값(Value)은 되돌리지 않는다** —
사용자가 말한 것은 수치이고, 값은 10-08 결정(서클 상한 · 상점에서만 파는 치장은 금화를 빨아들이게 그대로,
`build-price-cap.py`)이 정한다. 도감대로 되돌리면 블랙팜 치장이 7천만 → 10 이 된다. 레벨이 도감(1·11·41·71·99)으로 돌아오니 상점 서클 나눔
(`build-circle-gear-shops.py`)·드랍 생성기·`build-client-guide.py` 를 다시 돌린다.
"""
import argparse
import json
import sys

import sys as _sys, pathlib as _pathlib  # scripts/ 를 찾게 — lib/·graphify_runtime 이 거기 있다
_sys.path.insert(0, str(_pathlib.Path(__file__).resolve().parents[2]))
from graphify_runtime import configure_utf8_stdio

from lib._paths import ROOT
from lib._gear_original import MODIFIERS, number, modifier_of, wanted, differs
SHEET = ROOT / "data" / "game-data" / "items-original-sheets.json"
ITEMS = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates" / "items"

configure_utf8_stdio(sys.stdout, sys.stderr)

#: 이 묶음의 입는 물건(`EquipmentSlot` > 0)만 건드린다.
GROUPS = ("5.99표/",)

#: 되돌리지 않는 물건 — 이름: 왜.
KEEP = {name: "도감의 지팡이 다섯 줄이 완전히 같아(4m20·레벨11) 되돌리면 무기 사다리가 한 칸으로 뭉개진다"
        for name in ("매직마르시아", "매직쥬피티아", "매직솔라", "매직루나", "매직파나",
                     "홀리머큐리아", "홀리쥬피티아", "홀리솔라", "홀리루나", "홀리파나")}

#: 위 「같음」을 실제로 확인할 칸과 값. 도감이 바뀌어 사다리가 생기면 여기서 멈춘다.
KEEP_PROOF = {"공격력": "4m20", "마법공격력": "6m30", "레벨제한": "11"}

def check_keep(rows):
    """지팡이를 두는 이유가 아직 참인지 확인한다. 아니면 멈춘다."""
    wrong = []
    for name in KEEP:
        row = rows.get(name)
        if row is None:
            wrong.append(f"{name}: 도감에 없다")
            continue
        for column, expected in KEEP_PROOF.items():
            if row.get(column) != expected:
                wrong.append(f"{name}: 도감 {column} 가 {row.get(column)} 다(같음을 보던 값 {expected})")
    if wrong:
        raise SystemExit(
            "지팡이를 두는 이유가 더는 맞지 않습니다 — 도감이 등급을 나누기 시작했다면 되돌려야 합니다:\n  "
            + "\n  ".join(wrong)
        )


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--쓰기", action="store_true", dest="writing")
    writing = parser.parse_args().writing

    sheet = json.loads(SHEET.read_text(encoding="utf-8"))
    rows = {}
    for entry in sheet["수치표"]:
        rows.setdefault(entry["이름"], entry)
    check_keep(rows)

    mine, kept, untouched = [], [], 0
    for path in sorted(ITEMS.glob("*.json")):
        item = json.loads(path.read_text(encoding="utf-8-sig"))
        name = item.get("Name")
        if not str(item.get("Group") or "").startswith(GROUPS) or not item.get("EquipmentSlot"):
            continue
        if name not in rows:
            untouched += 1  # 팩이 새로 만든 것 — 도감에 견줄 줄이 없다
            continue
        if name in KEEP:
            kept.append(name)
            continue
        mine.append((path, item, rows[name]))

    # 값(Value)은 여기서 다루지 않는다 — 서클 상한·치장 금화 빨기(`build-price-cap.py`, 사용자 2026-10-08)가 정한다.
    jobs = [(path, item, {field: value for field, value in wanted(row).items() if field != "Value"}) for path, item, row in mine]

    changed, fields, removed, lines = 0, 0, 0, []
    for path, item, want in jobs:
        moved = {field: value for field, value in want.items() if differs(item, field, value)}
        if not moved:
            continue
        changed += 1
        fields += len(moved)
        was = []
        for field, value in moved.items():
            if field in MODIFIERS.values():
                was.append(f"{field} {modifier_of(item, field)}→{0 if value is None else (value['Value'] if value['Option'] == 0 else -value['Value'])}")
            else:
                was.append(f"{field} {item.get(field)}→{value}")
            if value is None:
                item.pop(field, None)
                removed += 1
            else:
                item[field] = value
        lines.append(f"  {item['Name']}: " + " · ".join(was))
        if writing:
            path.write_text(json.dumps(item, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(f"도감에 이름이 있는 5.99 입는 물건 {len(mine) + len(kept)}장 중"
          f" {changed}장 · {fields}칸을 도감 값으로 되돌렸다"
          f" ({'적었다' if writing else '미리보기 — --쓰기 로 적는다'})")
    print(f"  칸을 뺀 것 {removed}개 — 도감이 0 인 수정치다(없는 칸이 0 이다)")
    print(f"  안 되돌린 것 {len(kept)}장(지팡이): {', '.join(sorted(kept))}")
    print(f"    까닭: {next(iter(KEEP.values()))}")
    print(f"  손대지 않은 5.99 입는 물건 {untouched}장 — 도감에 이름이 없다(팩이 새로 만든 것)")
    print("\n".join(lines))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
