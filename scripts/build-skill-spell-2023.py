#!/usr/bin/env python3
"""Extract the supplied 2023 skill/spell workbook into a checked JSON source.

The workbook is deliberately read with the Python standard library: it is an
OOXML zip, and this keeps refreshes reproducible without a locally installed
Excel reader.  It extracts only the two G:N tables on each class sheet; the
price calculators and their formulas at A:E/R:X are never inputs.

Usage: python3 scripts/build-skill-spell-2023.py
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parent.parent
SOURCE = next((ROOT / "docs" / "skill_spell").glob("*.xlsx"))
OUT = ROOT / "data" / "skill-spell-2023" / "skills.json"
NS = {"x": "http://schemas.openxmlformats.org/spreadsheetml/2006/main",
      "r": "http://schemas.openxmlformats.org/officeDocument/2006/relationships"}
CLASSES = {"전사": "전사", "도적": "도적", "도적 (2)": "도적", "법사": "법사", "직자": "직자", "도가": "도가"}
RANK = re.compile(r"^(?P<base>.+)\((?P<rank>Lev\d+|master)\)$")
LEVEL = re.compile(r"^Lv\.(\d+)$")
ABILITY = re.compile(r"^Ab\.\d+$")
EXPECTED = {"전사": {"ordinary": 23, "ability": 44}, "도적": {"ordinary": 32, "ability": 81},
            "법사": {"ordinary": 53, "ability": 65}, "직자": {"ordinary": 53, "ability": 53},
            "도가": {"ordinary": 32, "ability": 103}}


def col(ref: str) -> str:
    return re.match(r"[A-Z]+", ref).group(0)


def strings(zf: ZipFile) -> list[str]:
    root = ET.fromstring(zf.read("xl/sharedStrings.xml"))
    return ["".join(t.text or "" for t in si.iterfind(".//x:t", NS))
            for si in root.findall("x:si", NS)]


def workbook_sheets(zf: ZipFile, shared: list[str]):
    workbook = ET.fromstring(zf.read("xl/workbook.xml"))
    rels = ET.fromstring(zf.read("xl/_rels/workbook.xml.rels"))
    targets = {rel.attrib["Id"]: rel.attrib["Target"] for rel in rels}
    for sheet in workbook.findall("x:sheets/x:sheet", NS):
        path = "xl/" + targets[sheet.attrib["{" + NS["r"] + "}id"]]
        xml = ET.fromstring(zf.read(path))
        rows = []
        for row in xml.findall(".//x:sheetData/x:row", NS):
            values: dict[str, str] = {}
            for cell in row.findall("x:c", NS):
                value = cell.find("x:v", NS)
                text = "" if value is None else (value.text or "")
                if cell.attrib.get("t") == "s" and text:
                    text = shared[int(text)]
                elif cell.attrib.get("t") == "inlineStr":
                    text = "".join(t.text or "" for t in cell.iterfind(".//x:t", NS))
                values[col(cell.attrib["r"])] = text
            rows.append((int(row.attrib["r"]), values))
        yield sheet.attrib["name"], rows


def rank_fields(name: str) -> tuple[str, str | None]:
    match = RANK.match(name)
    return (match.group("base"), match.group("rank")) if match else (name, None)


def requirement(marker: str, inherited: dict[str, object]) -> dict[str, object]:
    if marker == "승급":
        return {"type": "ascension", "value": "승급"}
    match = LEVEL.match(marker)
    if match:
        return {"type": "level", "value": int(match.group(1))}
    return inherited


def extract_sheet(sheet: str, rows: list[tuple[int, dict[str, str]]]) -> list[dict[str, object]]:
    """Read the visible G:N tables, not the workbook's helper calculators."""
    section = None
    inherited: dict[str, object] = {"type": "unknown", "value": None}
    result = []
    for row_number, cells in rows:
        g, name = cells.get("G", "").strip(), cells.get("I", "").strip()
        if name == "Skill" and g == "Lv" and cells.get("K", "").strip() == "Exp":
            section, inherited = "ability", {"type": "ability", "value": None}
            continue
        if name == "Skill" and g == "Lv":
            section, inherited = "ordinary", {"type": "unknown", "value": None}
            continue
        if not section or not name or name == "Skill":
            continue
        if section == "ability" and not ABILITY.match(g):
            continue
        if section == "ordinary" and g and not (LEVEL.match(g) or g == "승급"):
            continue
        inherited = requirement(g, inherited) if section == "ordinary" else {
            "type": "ability", "value": int(g.split(".", 1)[1])}
        base, rank = rank_fields(name)
        is_ability = section == "ability"
        source = {"sheet": sheet, "row": row_number, "columns": "G:N"}
        record = {
            "id": f"{CLASSES[sheet]}:{section}:{row_number}",
            "class": CLASSES[sheet], "section": section, "name": name,
            "base_name": base, "rank": rank, "requirement": inherited,
            # `기본기술` and `승급기본기술` are workbook facts, not missing prices.
            "cost_text": cells.get("J", "").strip(),
            "gold": int(cells["J"]) if cells.get("J", "").isdigit() else None,
            "exp": int(cells["K"]) if is_ability and cells.get("K", "").isdigit() else None,
            "ability_exp": int(cells["L"]) if is_ability and cells.get("L", "").isdigit() else None,
            "materials": "" if is_ability else cells.get("K", "").strip(),
            "book": cells.get("M", "").strip() if is_ability else "",
            "learning_location": "" if is_ability else cells.get("N", "").strip(),
            "notes": cells.get("N", "").strip() if is_ability else "",
            "source": source,
        }
        result.append(record)
    return result


def main() -> None:
    if not SOURCE.exists():
        sys.exit("docs/skill_spell 아래에 .xlsx 원본이 없다")
    with ZipFile(SOURCE) as zf:
        sheets = dict(workbook_sheets(zf, strings(zf)))
    if set(sheets) != {"도적 (2)", "전사", "도적", "법사", "직자", "도가"}:
        sys.exit(f"예상한 6개 시트와 다르다: {', '.join(sheets)}")

    # '도적 (2)' is a competing version, not a sixth class.  The ordinary
    # '도적' sheet has one additional actual row; retain a factual audit in JSON.
    duplicate = extract_sheet("도적 (2)", sheets["도적 (2)"])
    canonical = extract_sheet("도적", sheets["도적"])
    duplicate_names, canonical_names = {r["name"] for r in duplicate}, {r["name"] for r in canonical}
    if canonical_names - duplicate_names != {"설치형트랩"}:
        sys.exit("도적 시트 대체본 검증 실패: 예상한 설치형트랩 차이가 아니다")
    records = []
    for sheet in ("전사", "도적", "법사", "직자", "도가"):
        records.extend(extract_sheet(sheet, sheets[sheet]))
    ids = [r["id"] for r in records]
    if len(ids) != len(set(ids)):
        sys.exit("중복 ID가 있다")
    canonical_classes = ("전사", "도적", "법사", "직자", "도가")
    if len(records) < 400 or any(not any(r["class"] == c for r in records) for c in canonical_classes):
        sys.exit("추출 범위가 비정상이다")
    counts = {c: dict(Counter(r["section"] for r in records if r["class"] == c)) for c in canonical_classes}
    if counts != EXPECTED:
        sys.exit(f"행 수 검증 실패: {counts}")
    covered = {r["source"]["sheet"] for r in records}
    if covered != set(canonical_classes):
        sys.exit(f"정본 시트 출처 범위가 비정상이다: {covered}")
    payload = {
        "schema_version": 2,
        "lineage": "2023 workbook (separate from Hades SClass1~5 613-ability lineage)",
        "source": {"path": str(SOURCE.relative_to(ROOT)), "sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
                   "sheets_inspected": list(sheets), "canonical_sheets": ["전사", "도적", "법사", "직자", "도가"]},
        "duplicate_sheet_audit": {"alternative_sheet": "도적 (2)", "canonical_sheet": "도적",
            "alternative_rows": len(duplicate), "canonical_rows": len(canonical),
            "only_in_canonical": sorted(canonical_names - duplicate_names),
            "only_in_alternative": sorted(duplicate_names - canonical_names),
            "note": "같은 직업의 대체 표다. 더 완전한 도적 시트를 정본으로 썼고, 값 충돌은 합치거나 추정하지 않았다."},
        "counts": {"records": len(records), "by_class_section": counts}, "records": records,
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"2023 기술·마법 표 {len(records)}행 -> {OUT.relative_to(ROOT)}")
    for klass, value in counts.items(): print(f"  {klass}: 일반 {value.get('ordinary', 0)} · 어빌리티 {value.get('ability', 0)}")


if __name__ == "__main__":
    main()
