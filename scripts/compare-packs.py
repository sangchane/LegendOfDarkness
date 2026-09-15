#!/usr/bin/env python3
"""서버팩 여러 개와 Hades 를 나란히 놓고 무엇을 믿을 수 있는지 센다.

팩 수를 고정하지 않는다 — `data/server-packs/extracted/<팩>/` 에 있는 것을 전부 읽는다.

**이름이 같다고 같은 것이 아니다.** 그래서 이름 말고 자료가 스스로 들고 있는
식별 신호(그림 번호)로 잇고, 수치는 팩끼리 얼마나 맞는지를 따로 센다.
한 갈래에서 이름은 합의해도 수치는 전혀 합의하지 않을 수 있다 — 실제로 그렇다.

  쓰는 법: python3 scripts/compare-packs.py
  산출물:  data/pack-compare/summary.json · item-korean-names.json
"""
import json, re, sys
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

# 같은 그림에 이름이 여럿인 것은 접사(신 이름·속성) 때문이다. 양쪽에 12~16개씩 붙어 있고,
# **무엇을 올려 주는가로 짝이 지어진다** — 소리로 맞추면 틀린다(칸은 Cail 이 아니라 Gramail 이다).
# 근거: 접사가 붙은 것에서 안 붙은 밑말을 빼면 달라진 칸이 하나씩 나온다 (docs/pack-comparison.md).
AFFIX = {"로오": "Luathas",   # 인트변화  ↔ IntModifer
         "이아": "Glioca",    # 위즈변화  ↔ WisModifer
         "세토아": "Ceannlaidir",  # 힘변화 ↔ StrModifer
         "메투스": "Cail",    # 콘변화    ↔ ConModifer
         "셔스": "Fiosachd",  # 덱스변화  ↔ DexModifer
         "칸": "Gramail",     # 마법방어  ↔ MrModifer
         "축복": "Blessed",   # 명중수정+5 ↔ HitModifer 5
         "풍요": "Abundance", # 공격수정+1 ↔ DmgModifer 1
         "체력": "Might",     # 체력변화+100 ↔ HealthModifer
         "마법": "Magic",     # 마력변화+100 ↔ ManaModifer
         # 아래 셋은 칸으로 못 가린 것이다 (팩에서 효과가 바뀌어 있다). 자료가 직접 말해 준다:
         # 카페 `【item】 520 아이템 속성에 관하여` 가 한↔영 접사를 12개 다 적어 두었다.
         # docs/darkages-cafe/item/520-아이템-속성에-관하여..-(-완료-).md
         "세오": "Deoch",     # 재생력 10   — 카페 520
         "뮤레칸": "Sgrios",   # 내구력 10배 — 카페 520
         "마력": "Magic",     # 카페 520 은 `마력`(DA 에서 MP 50) 이라 적었다. 팩에 둘 다 있다.
         "화염": "Fire", "대지": "Earth", "바다": "Sea", "바람": "Wind"}  # 방어속성

# 한 영문 접사에 한글 표기가 둘인 것이 있다 (Magic ← 마법·마력). 그래서 집합으로 담는다.
EN_TO_KO = {}
for _ko, _en in AFFIX.items():
    EN_TO_KO.setdefault(_en, set()).add(_ko)
EN_TO_KO["Fioschad"] = EN_TO_KO["Fiosachd"]   # 자료에 있는 오타. 접사로 잡히니 짝도 준다.

# 한글 표기가 둘일 때 **이름을 지을 때** 쓸 쪽. 짝을 찾을 때는 둘 다 받는다 (팩에 둘 다 있다).
# Magic 은 `마력의가죽각반` 으로 적는다 — 카페 520 과 게임을 해 본 사람이 같은 말을 한다.
PREFERRED_KO = {"Magic": "마력"}

# 그림 번호는 **착용 부위마다 따로 매겨진다** — 그림 113 이 Hades 에서는 투구이고 팩에서는
# 부츠다. 그래서 부위가 같을 때만 잇는다. ★ 표는 신 이름이 붙은 확실한 짝에서 자료가 직접
# 말한 것이고(각각 49·40·8쌍, 다른 값이 하나도 안 섞인다), 나머지는 양쪽 이름을 읽어 맞춘 것이다.
SLOT_TO_ATTR = {1: {"0", "11", "12"},   # 무기 (검·도끼·지팡이)
                2: {"1"},               # 옷
                3: {"2"},               # 방패 (이름에 '방패' 가 든 59개 중 56개가 속성 2다)
                4: {"3"},               # 투구·모자
                5: {"4"},               # 귀걸이
                6: {"5"},               # 목걸이      ★
                7: {"6"}, 8: {"6"},     # 반지
                9: {"7"}, 10: {"7"},    # 장갑        ★
                11: {"8"},              # 벨트
                12: {"9"},              # 각반        ★
                13: {"10"}}             # 신발

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


def wearable_pack(fields):
    """팩에서 장비는 내구력이 있다. 소모품·재료는 없다 (동전·설탕에는 없고 동각반에는 6000)."""
    v = str(fields.get("내구력", "")).strip()
    return v.isdigit() and int(v) > 0


def wearable_hades(item):
    """Hades 도 같은 선이다 — `MaxDurability` 가 장비에만 있다."""
    return bool(item.get("MaxDurability"))


def english_affixes(items):
    """영문 접사 어휘를 자료에서 뽑는다: 첫 낱말을 떼도 같은 그림에 그 이름이 남아 있으면 접사다.

    한글 짝이 없는 접사(Deoch·Sgrios)도 여기 들어온다 — 그래야 "접사가 없는 이름" 과
    헷갈리지 않고, 짝을 못 찾았다고 정직하게 말할 수 있다.
    """
    by_img = defaultdict(set)
    for h in items:
        by_img[hades_image(h)].add(h["Name"])
    found = defaultdict(set)
    for h in items:
        head, _, rest = h["Name"].partition(" ")
        if rest and rest in by_img[hades_image(h)]:
            found[head].add(rest)
    return {a for a, bases in found.items() if len(bases) >= 3}


def split_ko(name, siblings):
    """'<접사>의<밑말>' — 밑말이 같은 그림에 있거나, 앞말이 아는 접사일 때 접사로 본다.

    밑말만 보면 `로오의반지` 가 통짜 이름으로 새어 나간다 — `반지` 가 그 그림 후보에 없어서다.
    그러면 접사가 없는 영문(`Loures Signet Ring`)에 붙어 **도시 Loures 를 신 로오로** 읽는다.
    사전(AFFIX)이 12개를 다 갖춘 뒤부터는 앞말만 보고도 접사인 줄 안다.
    """
    m = re.match(r"^(.+?)의(.+)$", name)
    if not m:
        return (None, name)
    if m.group(2) in siblings or m.group(1) in AFFIX:
        return (m.group(1), m.group(2))
    return (None, name)


def split_en(name, affixes):
    """'<접사> <밑말>' — 자료에서 뽑은 접사일 때만 뗀다."""
    head, _, rest = name.partition(" ")
    return (head, rest) if rest and head in affixes else (None, name)


def narrow_by_affix(by_pack, en_name, affixes):
    """영문 접사와 같은 뜻의 한글 접사가 붙은 후보만 남긴다.

    접사가 없는 영문 이름에는 접사가 없는 한글 이름만 남는다 — `Leather Greaves` 에
    `로오의가죽각반` 을 붙이지 않기 위해서다.
    """
    siblings = {n for v in by_pack.values() for n in v}
    en_aff = split_en(en_name, affixes)[0]
    if en_aff is not None and en_aff not in EN_TO_KO:
        return {}                      # 짝을 아직 모르는 접사다 — 억지로 붙이지 않는다
    # 접사가 없는 영문 이름은 접사가 없는 한글 이름(밑말 쪽이 None)만 남긴다.
    want = EN_TO_KO[en_aff] if en_aff is not None else {None}
    out = {p: [n for n in names if split_ko(n, siblings)[0] in want] for p, names in by_pack.items()}
    return {p: v for p, v in out.items() if v}


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


def pack_attr(fields):
    return str(fields.get("속성", "")).strip()


def names_by_image(key, wearable=None):
    """그림 번호 → {팩: [이름...]}. 팩이 스스로 들고 있는 유일한 공통 식별자다.

    그림 번호는 장비와 소모품이 나눠 쓴다 (그림 38 이 `설탕` 이면서 어느 갑옷이기도 하다).
    그래서 `wearable` 로 한쪽만 담아야 `Cthonic Magus Robes → 설탕` 같은 짝이 안 나온다.
    """
    out = defaultdict(lambda: defaultdict(set))
    for pack in packs():
        for e in load(pack, key):
            f = e.get("fields", {})
            img = str(f.get(IMAGE_FIELD, "")).strip()
            if not img.isdigit() or is_placeholder(e["이름"]):
                continue
            if wearable is not None and wearable_pack(f) != wearable:
                continue
            out[(int(img), pack_attr(f) if wearable else None)][pack].add(e["이름"])
    return {k: {p: sorted(v) for p, v in d.items()} for k, d in out.items()}


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


# 운영자가 만지는 칸과 안 만지는 칸은 갈린다. 이름이 같은 것끼리 칸을 맞춰 보면 드러난다 —
# 정체성(그림·부위·직업·성별)은 그대로 두고 균형(공격력·방어력·스탯·가격)만 고친다.
IDENTITY = ("이미지", "착용이미지", "타입", "속성", "직업제한", "성별제한", "공격모션", "수리여부")


def field_agreement(key="items"):
    """같은 이름끼리 칸별로 몇 개나 값이 같은지. 무엇을 팩에서 가져와도 되는지가 여기서 갈린다."""
    per = {}
    for pack in packs():
        d = {}
        for e in load(pack, key):
            d.setdefault(e["이름"], e.get("fields", {}))
        per[pack] = d
    if not per:
        return {}, []
    common = sorted(set.intersection(*(set(v) for v in per.values())))
    have, same = defaultdict(int), defaultdict(int)
    pure = []
    for n in common:
        fs = [per[p][n] for p in per]
        for k in {k for f in fs for k in f} - {"이름"}:
            if all(k in f for f in fs):
                have[k] += 1
                if len({str(f[k]) for f in fs}) == 1:
                    same[k] += 1
        ident = [k for k in IDENTITY if all(k in f for f in fs)]
        if ident and all(len({str(f[k]) for f in fs}) == 1 for k in ident):
            pure.append(n)
    rate = {k: {"맞은 수": same[k], "견줄 수": have[k]} for k in have if have[k] >= 20}
    return {"이름이 모두 겹친다": len(common), "칸별": rate,
            "정체성 칸이 모두 같다": len(pure)}, sorted(pure)


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


def hades_image(h):
    """팩의 `이미지` 와 이을 번호. **`Image` 가 아니라 `DisplayImage` 다.**

    Hades 는 그림 번호를 둘 들고 있다. `Image` 는 **인벤토리 칸에 보이는 아이콘**이라 여럿이
    나눠 쓴다 — 보석 반지 12종이 전부 210 이고, 210 한 칸에 122장이 몰린다. `DisplayImage`
    가 실제로 갈리는 값이다 (Beryl 206 · Red Jade 208 · Amethyst 209 · Ruby 210 …).
    `0x8000` 이 얹혀 있어 떼고 쓴다.

    대부분은 둘이 같다 (Leather Greaves 238 = 238 · Loures Signet Ring 207 = 207). 몰리는
    자리에서만 갈린다. 실측: 팩과 이어진 것 882 → 933 장, 후보가 하나뿐인 것 258 → 322 장.
    """
    d = h.get("DisplayImage")
    return (d & 0x7FFF) if d else h.get("Image")


# 부위 이름만 남은 것은 물건 이름이 아니다 (`로오의반지` → `반지`).
BODY_PART_WORDS = frozenset({"반지", "목걸이", "귀걸이", "팔찌", "장갑", "각반", "신발", "벨트",
                             "방패", "모자", "투구", "옷", "갑옷", "무기", "검", "단검"})

KO_ELEMENT_TAIL = "수토풍화"          # 속성 변종 꼬리 (라비린스메일수·토·풍·화)


def ko_base(name, siblings=()):
    """한글 변종의 껍질을 벗겨 밑말만 남긴다.

    한 그림에 한글 이름이 여럿인 것은 **한 물건의 변종**이다 (그림 250 의 13개가 전부 `동각반`).
    껍질을 벗겨 모으면 그림마다 밑말이 하나로 모인다 — 실측 825 → 1147 개.
    `은팔찌` 처럼 밑말이 단독으로는 팩에 없는 것도 이렇게 얻어진다.

    속성 꼬리(수·토·풍·화)는 **벗긴 것이 같은 그림에 실제로 있을 때만** 벗긴다. 이름이 그 글자로
    끝나는 멀쩡한 아이템을 망치지 않기 위해서다.
    """
    s = re.sub(r"^(?:\[속\]|초보자)", "", name)
    m = re.match(r"^(.+?)의(.+)$", s)
    if m and m.group(1) in AFFIX:
        s = m.group(2)
    s = re.sub(r"(?:\(Lev\d+\)|\(x\)|\+\d+)$", "", s)
    if len(s) > 3 and s[-1] in KO_ELEMENT_TAIL and s[:-1] in siblings:
        s = s[:-1]
    return s


def collapse_ko_base(rows, affixes):
    """접사 없는 영문에, 그림 후보의 껍질을 벗겨 모은 밑말을 준다 — 하나로 모일 때만.

    지금까지는 접사 없는 한글 이름이 **그대로 있어야** 짝이 됐다. 그림 225 처럼 8개가 전부
    접사투성이면 밑말(`은팔찌`)이 없어 아무것도 못 정했다. 벗겨 모으면 정해진다.
    """
    won = 0
    for r in rows:
        if r["한글이름"] or split_en(r["영문"], affixes)[0]:
            continue                   # 이미 정해졌거나, 접사 붙은 영문은 여기 몫이 아니다
        names = {n for v in (r["한글후보"] or {}).values() for n in v}
        if not names:
            continue
        bases = {ko_base(n, names) for n in names}
        if len(bases) != 1:
            continue
        got = bases.pop()
        # 껍질만 남은 밑말은 쓰지 않는다. `로오의반지` 를 벗기면 `반지` 가 되는데, 그것은
        # 물건 이름이 아니라 부위 이름이다 — 후보가 전부 접사투성이일 때만 이런 일이 난다.
        if got in BODY_PART_WORDS or all(ko_base(n, names) != n for n in names) and len(got) <= 3:
            continue
        r["한글이름"], r["등급"] = got, "KO_BASE_COLLAPSED"
        won += 1
    return won


def derive_affixed(rows):
    """밑말과 접사가 다 정해졌으면 팩에 없는 변종 이름도 짓는다 — `AFFIX_DERIVED`.

    `compose_affixed` 는 팩에 있는 이름만 고른다. 그런데 한글팩은 신 변종을 거의 싣지 않는다 —
    밑말이 정해진 273장을 재어 보니 지은 이름이 팩에 있는 것이 **하나도 없었다.** 그대로 두면
    영문 이름으로 남는다.

    그래서 여기서는 짓는다. 근거는 둘 다 검증된 것이다: 밑말은 그림으로 이어 정해졌고, 접사는
    카페 520 이 적어 둔 한↔영 짝이다. 대신 조건을 둔다.

    - 한 영문 접사에 한글 표기가 둘인 것(Magic ← 마법·마력)은 **짓지 않는다.** 어느 쪽인지
      자료가 말해 주지 않는다 — 사람이 고를 자리로 남긴다.
    - 이미 다른 아이템이 쓰는 이름이면 짓지 않는다. 템플릿은 이름이 열쇠라 덮어쓰면 사라진다.

    등급을 따로 달아 두었으니 되돌리려면 이 등급만 걸러 내면 된다.
    """
    taken = {r["한글이름"] for r in rows if r["한글이름"]}
    settled = {r["영문"]: r["한글이름"] for r in rows if r["한글이름"]}
    won = 0
    for r in rows:
        if r["한글이름"] or not r.get("영문접사"):
            continue
        ko_affixes = EN_TO_KO.get(r["영문접사"], ())
        picked = PREFERRED_KO.get(r["영문접사"])
        if picked is None:
            if len(ko_affixes) != 1:
                continue               # 한글 표기가 둘인데 고를 근거가 없다
            picked = next(iter(ko_affixes))
        ko_base_name = settled.get(r["영문밑말"])
        if not ko_base_name:
            continue
        made = picked + "의" + ko_base_name
        if made in taken:
            continue
        r["한글이름"], r["등급"] = made, "AFFIX_DERIVED"
        taken.add(made)
        won += 1
    return won


def drop_name_clashes(rows):
    """한 한글 이름을 영문 여럿이 차지하면 **전부 도로 내린다** — `NAME_CLASH`.

    템플릿은 이름이 열쇠다 (`GlobalItemTemplateCache[template.Name] = template`). 같은 이름이
    둘이면 뒤엣것이 앞엣것을 덮어 아이템이 조용히 사라진다. 게다가 셋 중 맞는 것은 하나뿐인데
    셋 다 "정해졌다" 고 적어 두면 검토하는 사람을 속인다.

    어느 것이 맞는지는 자료가 말해 주지 않으므로 고르지 않는다. 사람이 고를 자리로 되돌린다.
    """
    claim = defaultdict(list)
    for r in rows:
        if r["한글이름"]:
            claim[r["한글이름"]].append(r)
    dropped = 0
    for name, sharers in claim.items():
        if len(sharers) < 2:
            continue
        for r in sharers:
            r["한글이름"], r["등급"] = None, "NAME_CLASH"
            dropped += 1
    return dropped


def pack_item_names():
    """팩에 실제로 있는 아이템 이름 전부. 지어낸 이름을 걸러 내는 체다."""
    return {e["이름"] for pack in packs() for e in load(pack, "items")
            if not is_placeholder(e["이름"])}


def compose_affixed(rows, affixes, universe):
    """밑말이 정해진 것에 접사를 붙여 변종을 짓는다 — **지어낸 이름은 쓰지 않는다.**

    `Deoch Leather Greaves` 의 후보로 `세오의가죽각반` 과 `세오의각반` 이 같이 남는다. 접사만
    보면 둘 다 세오라 못 가린다. 그런데 `Leather Greaves → 가죽각반` 은 이미 정해져 있으니,
    `세오` + `의` + `가죽각반` 을 지어 **팩에 그 이름이 실제로 있을 때만** 고른다. 없으면 그냥
    둔다 — 팩에 없는 이름을 만들어 넣으면 템플릿이 조용히 덮인다.

    찾는 자리는 그림 후보가 아니라 팩 전체다. 변종은 밑말과 같은 물건인데 팩이 그것을 다른
    그림에 두는 일이 있어, 그림으로 막으면 거의 다 놓친다 (실측: 후보 안에서만 보면 1건).
    밑말 쪽이 이미 그림으로 검증된 짝이라 그것이 증거 노릇을 한다.

    한 영문 접사에 한글 표기가 둘인 것(Magic ← 마법·마력)은 둘 다 넣어 본다.
    """
    settled = {r["영문"]: r["한글이름"] for r in rows if r["한글이름"]}
    won = 0
    for r in rows:
        if r["한글이름"]:
            continue
        en_aff, base = split_en(r["영문"], affixes)
        ko_base = settled.get(base) if en_aff else None
        if not ko_base:
            continue
        made = [ko_aff + joiner + ko_base
                for ko_aff in EN_TO_KO.get(en_aff, ())
                for joiner in ("의", "")]
        hit = [n for n in made if n in universe]
        if len(hit) == 1:
            r["한글이름"], r["등급"] = hit[0], "AFFIX_COMPOSED"
            won += 1
    return won


def hades_item_korean_names():
    """Hades 의 영문 아이템에 팩의 한글 이름을 잇는다 — 그림 번호가 같으면 같은 그림이다.

    번호는 `hades_image()` 가 고른다 (`Image` 가 아니라 `DisplayImage`, 까닭은 그쪽 주석).
    다만 **같은 그림에 이름이 여럿** 인 일이 흔하다 — 형용사만 다른 변종이라 사람이 고른다.
    """
    if not HADES_ITEMS.exists():
        return [], {"없음": str(HADES_ITEMS.relative_to(ROOT))}
    hades = json.loads(HADES_ITEMS.read_text(encoding="utf-8"))
    affixes = english_affixes(hades)
    worn = names_by_image("items", wearable=True)
    rest = names_by_image("items", wearable=False)
    rows = []
    for h in hades:
        if wearable_hades(h):
            cand = {}
            for attr in SLOT_TO_ATTR.get(h.get("EquipmentSlot"), ()):
                for pack, names in worn.get((hades_image(h), attr), {}).items():
                    cand.setdefault(pack, []).extend(names)
        else:
            cand = rest.get((hades_image(h), None), {})
        kept = narrow_by_affix(cand, h["Name"], affixes)
        names = {n for v in kept.values() for n in v}
        # 접사까지 맞춘 뒤에도 이름이 하나여야 정해진 것이다. 여럿이면 사람이 고른다.
        settled = names.pop() if len(names) == 1 else None
        en_aff, en_base = split_en(h["Name"], affixes)
        rows.append({"영문": h["Name"], "그림": hades_image(h),
                     "영문접사": en_aff, "영문밑말": en_base,
                     "착용자리": h.get("EquipmentSlot"), "요구레벨": h.get("LevelRequired"),
                     "한글이름": settled,
                     "접사맞춤": kept, "한글후보": cand,
                     "등급": grade(kept) if settled else ("NONE" if not cand else "CONFLICT")})
    collapse_ko_base(rows, affixes)
    compose_affixed(rows, affixes, pack_item_names())
    derive_affixed(rows)
    drop_name_clashes(rows)
    tally = defaultdict(int)
    for r in rows:
        tally[r["등급"]] += 1
    tally["한글 이름이 하나로 정해졌다"] = sum(1 for r in rows if r["한글이름"])
    return rows, dict(tally)


def write_review_sheet(rows):
    """사람이 고르는 자리. 그림으로 이은 짝은 뜻이 틀릴 수 있어 **눈으로 한 번** 봐야 한다.

    `제안` 은 규칙이 하나로 좁힌 것이고, `후보` 는 같은 그림·같은 부위에 있던 다른 이름이다.
    맞으면 그대로 두고, 틀리면 `후보` 에서 골라 `제안` 자리에 적는다.
    """
    head = ["영문", "부위", "요구레벨", "제안", "후보", "등급"]
    lines = ["\t".join(head)]
    for r in sorted(rows, key=lambda x: (not x["한글이름"], str(x["착용자리"]), x["영문"])):
        if r["등급"] == "NONE":
            continue
        cand = sorted({n for v in r["한글후보"].values() for n in v} - {r["한글이름"]})
        lines.append("\t".join([r["영문"], str(r["착용자리"]), str(r["요구레벨"]),
                                r["한글이름"] or "", " · ".join(cand[:12]), r["등급"]]))
    (OUT / "한글이름-검토.tsv").write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    summary = {"팩": packs(),
               "갈래별 겹침": {k: overlap(k) for k in ("mobs", "items", "npcs", "skills", "spells")},
               "맵 겹침": map_overlap(),
               "괴물 수치 합의": mob_stat_agreement()}
    agreement, pure = field_agreement()
    summary["아이템 칸별 합의"] = agreement
    (OUT / "순정후보-아이템.json").write_text(json.dumps(pure, ensure_ascii=False, indent=1), encoding="utf-8")
    rows, tally = hades_item_korean_names()
    summary["Hades 아이템 한글 이름"] = tally
    (OUT / "summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=1), encoding="utf-8")
    (OUT / "item-korean-names.json").write_text(json.dumps(rows, ensure_ascii=False, indent=1), encoding="utf-8")
    write_review_sheet(rows)

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
    a = summary["아이템 칸별 합의"]
    print("")
    print(f"이름이 같은 아이템 {a['이름이 모두 겹친다']}개 — 칸이 실제로 맞는 비율")
    ranked = sorted(a["칸별"].items(), key=lambda kv: kv[1]["맞은 수"] / kv[1]["견줄 수"])
    for k, v in ranked[-6:][::-1]:
        print(f"   믿을 만 {k:8s} {v['맞은 수']:4d}/{v['견줄 수']:4d}")
    for k, v in ranked[:6]:
        print(f"   못 믿을 {k:8s} {v['맞은 수']:4d}/{v['견줄 수']:4d}")
    print(f"   정체성 칸이 셋 다 같은 것 {a['정체성 칸이 모두 같다']}개"
          " → data/pack-compare/순정후보-아이템.json")
    print("\nHades 아이템에 붙일 한글 이름:")
    for g, n in sorted(tally.items(), key=lambda kv: -kv[1]):
        print(f"  {g:17s} {n:4d}")
    print(f"\n→ {OUT.relative_to(ROOT)}/")


if __name__ == "__main__":
    main()
