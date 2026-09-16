#!/usr/bin/env python3
"""Hades 기술·마법과 세 서버팩의 한글 이름이 안전하게 합의되는 항목만 고른다.

구조와 정렬의 기준은 언제나 Hades ``abilities.json`` 이다. 서버팩은 번역 후보일 뿐이며,
같은 갈래·같은 아이콘에서 **이름을 댄 팩들이 하나의 같은 이름으로 모일 때만** 채택한다.
Hades 쪽도 그 아이콘에 영문 이름이 하나뿐이어야 한다(직업별 중복 행은 한 이름으로 센다).

**침묵은 반대가 아니다.** 셋이 다 말할 것을 요구하면 「2개 일치」 16건이 버려지는데, 그중 진짜로
다른 이름을 댄 것은 1건뿐이고 나머지 15건은 한 팩에 그 이름이 아예 없는 경우다. 그래서 말한
팩들만 놓고 보되, 그 안에서 갈리면 버린다. 갈린 하나는 `Stab and Twist` 이고 5.99·Novaonline 이
`찌르기` 인데 혼든만 `찔러휘비기` 다 — 혼자 다른 쪽이 손댄 쪽이라는 뜻이라 사유에 적어 둔다.
"""
import json
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
HADES = ROOT / "data/game-data/abilities.json"
PACKS = {
    "5.99-server": ROOT / "data/server-packs/extracted/5.99-server",
    "honden-community": ROOT / "data/server-packs/extracted/honden-community",
    "novaonline": ROOT / "data/server-packs/extracted/novaonline",
}
KINDS = {"skill": "skills", "spell": "spells"}
LABELS = {"skill": "기술", "spell": "마법"}


def hades_icon(row):
    try:
        return int(str(row["raw"][1]).split("/", 1)[0])
    except (KeyError, IndexError, TypeError, ValueError):
        return None


def pack_icon(row):
    try:
        value = row["fields"]["이미지"]
        return int(value) if str(value).strip().isdigit() else None
    except (KeyError, TypeError, ValueError):
        return None


def build_consensus(hades_rows, pack_rows):
    """``(영문 이름 → 합의 정보, 전체 대조 감사행)`` 을 돌려준다."""
    accepted, audit = {}, []
    for kind in KINDS:
        hades_by_icon = defaultdict(set)
        for row in hades_rows:
            icon = hades_icon(row)
            if row.get("kind") == kind and icon is not None:
                hades_by_icon[icon].add(row["name"])

        names_by_pack = {}
        for pack in PACKS:
            by_icon = defaultdict(set)
            for row in pack_rows[pack][kind]:
                icon = pack_icon(row)
                name = str(row.get("이름") or "").strip()
                if icon is not None and name:
                    by_icon[icon].add(name)
            names_by_pack[pack] = by_icon

        icons = sorted(set(hades_by_icon).union(*(set(rows) for rows in names_by_pack.values())))
        for icon in icons:
            english = sorted(hades_by_icon.get(icon, set()))
            candidates = {pack: sorted(by_icon.get(icon, set())) for pack, by_icon in names_by_pack.items()}
            # **침묵은 반대가 아니다.** 예전에는 세 팩이 모두 같은 이름을 댈 때만 채택했는데, 걸러진
            # 것을 세어 보니 「2개 일치」 16건 중 **진짜로 다른 이름을 댄 것은 1건뿐**이고 나머지
            # 15건은 한 팩에 그 이름이 아예 없는 경우였다(Novaonline 10 · 5.99 4 · 혼든 1).
            # 없는 것을 불일치로 세면 멀쩡한 이름 열다섯을 버리게 된다.
            #
            # 그래서 이름을 댄 팩들만 놓고 본다. 그 안에서 갈리면 버린다 — 실제로 갈린 하나가
            # `Stab and Twist` 이고, 5.99·Novaonline 이 `찌르기` 인데 **혼든만 `찔러휘비기`** 다.
            # 둘이 같고 셋째가 다르면 그 셋째가 손댄 쪽이라는 뜻이라, 버리면서 어느 팩인지 남긴다.
            spoke = {pack: values[0] for pack, values in candidates.items() if len(values) == 1}
            said = sorted(set(spoke.values()))
            okay = len(english) == 1 and len(spoke) >= 2 and len(said) == 1

            if okay:
                agreed = "서버팩 3개 일치" if len(spoke) == len(PACKS) else "서버팩 2개 일치·나머지 침묵"
                reason = "채택"
            elif len(english) != 1:
                agreed, reason = None, "Hades 영문 없음/다중"
            elif len(spoke) < 2:
                agreed, reason = None, "이름을 댄 팩이 하나 이하"
            else:
                agreed = None
                odd = [pack for pack, name in spoke.items() if list(spoke.values()).count(name) == 1]
                reason = ("팩 이름 불일치 — 혼자 다른 쪽: " + ", ".join(sorted(odd))) if odd \
                    else "팩 이름 불일치"

            audit.append({"kind": kind, "icon": icon, "english": english,
                          **candidates, "accepted": okay, "reason": reason})
            if okay:
                accepted[english[0]] = {
                    "korean": said[0], "kind": kind, "icon": icon,
                    "source": agreed,
                }
    return accepted, audit


def load_consensus():
    hades_rows = json.loads(HADES.read_text(encoding="utf-8-sig"))
    pack_rows = {
        pack: {
            kind: json.loads((folder / f"{stem}.json").read_text(encoding="utf-8-sig"))
            for kind, stem in KINDS.items()
        }
        for pack, folder in PACKS.items()
    }
    return build_consensus(hades_rows, pack_rows)
