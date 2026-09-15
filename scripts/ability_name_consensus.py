#!/usr/bin/env python3
"""Hades 기술·마법과 세 서버팩의 한글 이름이 안전하게 합의되는 항목만 고른다.

구조와 정렬의 기준은 언제나 Hades ``abilities.json`` 이다. 서버팩은 번역 후보일 뿐이며,
5.99·혼든·Novaonline이 같은 갈래·같은 아이콘에서 정확히 하나의 같은 이름을 가질 때만 채택한다.
Hades 쪽도 그 아이콘에 영문 이름이 하나뿐이어야 한다(직업별 중복 행은 한 이름으로 센다).
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
            single_pack_names = [values[0] for values in candidates.values() if len(values) == 1]
            okay = (len(english) == 1 and all(len(values) == 1 for values in candidates.values())
                    and len(set(single_pack_names)) == 1)
            if okay:
                reason = "채택"
            elif len(english) != 1:
                reason = "Hades 영문 없음/다중"
            else:
                invalid_pack = next((pack for pack, values in candidates.items() if len(values) != 1), None)
                reason = f"{invalid_pack} 이름 없음/다중" if invalid_pack else "세 팩 이름 불일치"
            audit.append({"kind": kind, "icon": icon, "english": english,
                          **candidates, "accepted": okay, "reason": reason})
            if okay:
                accepted[english[0]] = {
                    "korean": single_pack_names[0], "kind": kind, "icon": icon,
                    "source": "서버팩 3개 일치",
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
