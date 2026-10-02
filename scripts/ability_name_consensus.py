#!/usr/bin/env python3
"""Hades 기술·마법과 세 서버팩의 한글 이름이 안전하게 합의되는 항목만 고른다.

구조와 정렬의 기준은 언제나 Hades ``abilities.json`` 이다. 서버팩은 번역 후보일 뿐이며,
같은 갈래·같은 아이콘에서 **세 팩이 각각 하나의 같은 이름을 낼 때만** 채택한다.
Hades 쪽도 그 아이콘에 영문 이름이 하나뿐이어야 한다(직업별 중복 행은 한 이름으로 센다).

빈 후보나 다중 후보는 합의가 아니다. AGENTS.md 자료 규칙에 따라 미확정으로 남긴다.
"""
import json
from collections import defaultdict

from lib._paths import ROOT
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
            spoke = {pack: values[0] for pack, values in candidates.items() if len(values) == 1}
            said = sorted(set(spoke.values()))
            okay = len(english) == 1 and len(spoke) == len(PACKS) and len(said) == 1

            if okay:
                agreed = "서버팩 3개 일치"
                reason = "채택"
            elif len(english) != 1:
                agreed, reason = None, "Hades 영문 없음/다중"
            elif any(len(values) > 1 for values in candidates.values()):
                agreed, reason = None, "팩 이름 다중 후보"
            elif len(spoke) < len(PACKS):
                agreed, reason = None, "세 팩 이름 미충족"
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
