#!/usr/bin/env python3
"""Build and validate the skill/spell data-standard manifest.

This is deliberately a manifest, not a merged ability table. The Hades SClass
facts (613 English records), supplied 2023 workbook facts (539 Korean records),
and current server templates describe different things. Only an exact name
match to one server template is recorded; it never creates an English↔Korean
identity mapping or copies facts across lineages.

Usage:
  python3 scripts/build-ability-standard.py
  python3 scripts/build-ability-standard.py --check
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
HADES = ROOT / "data" / "game-data" / "abilities.json"
WORKBOOK = ROOT / "data" / "skill-spell-2023" / "skills.json"
TEMPLATES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "templates"
SCRIPTS = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server" / "scripts"
PAGE = ROOT / "docs" / "abilities-data.js"
OUT = ROOT / "data" / "game-data" / "ability-standard.json"

CLASS = {1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "수도사"}
WORKBOOK_COUNTS = {
    "전사": {"ordinary": 23, "ability": 44}, "도적": {"ordinary": 32, "ability": 81},
    "법사": {"ordinary": 53, "ability": 65}, "직자": {"ordinary": 53, "ability": 53},
    "도가": {"ordinary": 32, "ability": 103},
}


def relative(path: Path) -> str:
    return str(path.relative_to(ROOT))


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load(path: Path, encoding="utf-8"):
    return json.loads(path.read_text(encoding=encoding))


def script_keys() -> set[str]:
    found = set()
    for path in SCRIPTS.rglob("*.cs"):
        found.update(re.findall(r'\[Script\("([^"]+)"', path.read_text(encoding="utf-8", errors="replace")))
    return found


def runtime_templates(keys: set[str]):
    by_name = defaultdict(list)
    rows = []
    for kind, field in (("skill", "ScriptName"), ("spell", "ScriptKey")):
        for path in sorted((TEMPLATES / f"{kind}s").glob("*.json")):
            try:
                value = load(path, "utf-8-sig")
            except (OSError, json.JSONDecodeError):
                continue
            name = value.get("Name")
            if not name:
                continue
            script = value.get(field) or ""
            row = {
                "id": f"runtime:{kind}:{name}:{relative(path)}", "name": name, "kind": kind,
                "source": relative(path), "script_field": field, "script": script,
                "script_exists": script in keys, "target_animation": value.get("TargetAnimation") or 0,
                "sound": value.get("Sound") or 0,
            }
            rows.append(row)
            by_name[name].append(row)
    return rows, by_name


def exact_binding(record_id: str, name: str, candidates: dict[str, list[dict]]):
    matches = candidates.get(name, [])
    if len(matches) == 1:
        return {"record": record_id, "runtime": matches[0]["id"], "rule": "exact_template_name"}
    if len(matches) > 1:
        return {"record": record_id, "runtime": [m["id"] for m in matches], "rule": "ambiguous_exact_template_name"}
    return None


def build():
    if not HADES.exists() or not WORKBOOK.exists():
        raise SystemExit("기술·마법 정본 입력이 없다. Hades 613 표와 2023 워크북 JSON을 먼저 만든다")
    hades = load(HADES, "utf-8-sig")
    workbook = load(WORKBOOK)
    records = workbook.get("records", [])
    if len(hades) != 613 or Counter(r.get("kind") for r in hades) != {"skill": 275, "spell": 338}:
        raise SystemExit("Hades SClass 613개 구조 검증 실패")
    hades_ids = [f"hades-sclass:{r['class']}:{r['kind']}:{r['name']}" for r in hades]
    if len(hades_ids) != len(set(hades_ids)):
        raise SystemExit("Hades SClass 신원(class/kind/name)이 중복된다")
    if any(r.get("class") not in CLASS or not r.get("name") for r in hades):
        raise SystemExit("Hades SClass에 직업 또는 이름 없는 행이 있다")
    if workbook.get("schema_version") != 2 or len(records) != 539:
        raise SystemExit("2023 워크북 JSON 스키마 또는 행 수 검증 실패")
    if workbook.get("source", {}).get("sha256") != digest(ROOT / workbook["source"]["path"]):
        raise SystemExit("2023 워크북 원본 해시가 JSON과 다르다. 추출기를 다시 돌린다")
    counts = {klass: dict(Counter(r["section"] for r in records if r["class"] == klass)) for klass in WORKBOOK_COUNTS}
    if counts != WORKBOOK_COUNTS or len({r.get("id") for r in records}) != len(records):
        raise SystemExit("2023 워크북 직업별 행 수 또는 행 ID 검증 실패")

    keys = script_keys()
    runtime, by_name = runtime_templates(keys)
    audits = {}
    for label, source, identities in (
        ("hades_to_runtime", hades, hades_ids),
        ("workbook_2023_to_runtime", records, [r["id"] for r in records]),
    ):
        exact, ambiguous, unmatched = [], [], []
        for record, identity in zip(source, identities):
            result = exact_binding(identity, record["name"], by_name)
            if result is None:
                unmatched.append(identity)
            elif result["rule"].startswith("ambiguous"):
                ambiguous.append(result)
            else:
                exact.append(result)
        audits[label] = {"exact": exact, "ambiguous": ambiguous, "unmatched": unmatched}
    if not PAGE.exists():
        raise SystemExit("관리 페이지 데이터가 없다. build-ability-page-data.py를 먼저 돌린다")

    return {
        "schema_version": 1,
        "purpose": "기술·마법의 계보별 정본·실행 자료·매칭 한계를 명시하는 표준 manifest; 행을 병합하지 않는다.",
        "precedence": [
            {"scope": "Hades SClass 구조·영문 신원·선행", "source": relative(HADES), "records": len(hades)},
            {"scope": "사용자 제공 2023 표의 한글 이름·일반/어빌리티·비용·재료·습득 위치", "source": relative(WORKBOOK), "records": len(records)},
            {"scope": "현재 서버가 실제 보낼 수 있는 스크립트·템플릿·기본 이펙트/소리", "source": relative(TEMPLATES), "records": len(runtime)},
            {"scope": "관리 페이지 표시용 파생물(사실 원본 아님)", "source": relative(PAGE), "records": None},
        ],
        "identity_rules": {
            "hades_sclass": "class + kind + English name", "workbook_2023": "class + section + source worksheet row",
            "runtime": "template kind + Name + template path",
            "cross_lineage": "Hades와 2023 표 사이에는 자동 신원이 없다. 값·선행·이름을 복사하거나 번역으로 추정하지 않는다.",
            "runtime_binding": "동일 문자열 Name이 서버 템플릿 한 장에만 있을 때만 exact_template_name으로 기록한다. 이 바인딩은 실행 점검용이며 계보 병합이 아니다.",
        },
        "sources": {
            "hades_sclass": {"path": relative(HADES), "sha256": digest(HADES), "counts": {"total": len(hades), "skill": 275, "spell": 338}},
            "workbook_2023": {"path": relative(WORKBOOK), "workbook": workbook["source"], "counts": workbook["counts"]},
            "server_runtime": {"templates": relative(TEMPLATES), "scripts": relative(SCRIPTS), "page_derivative": relative(PAGE)},
        },
        "runtime_templates": runtime,
        "bindings": {key: value["exact"] for key, value in audits.items()},
        "audit": {
            **audits,
            "cross_lineage": {"automatic_matches": 0, "reason": "영문 SClass와 한글 워크북에는 신원 키가 공통으로 없다. 이름·레벨·아이콘 유사성으로 짝짓지 않는다."},
        },
    }


def main():
    payload = build()
    rendered = json.dumps(payload, ensure_ascii=False, indent=2) + "\n"
    if sys.argv[1:] == ["--check"]:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != rendered:
            raise SystemExit("ability-standard.json 이 낡았다. build-ability-standard.py를 다시 돌린다")
        print("기술·마법 표준 manifest 검증 통과")
        return
    if sys.argv[1:]:
        raise SystemExit("옵션은 --check 하나뿐이다")
    OUT.write_text(rendered, encoding="utf-8")
    audit = payload["audit"]
    print(f"기술·마법 표준 — Hades 613 · 2023 표 539 · 런타임 템플릿 {len(payload['runtime_templates'])}")
    print(f"  Hades 정확 바인딩 {len(audit['hades_to_runtime']['exact'])} · 2023 정확 바인딩 {len(audit['workbook_2023_to_runtime']['exact'])}")
    print(f"  미해결 — Hades {len(audit['hades_to_runtime']['unmatched'])} · 2023 {len(audit['workbook_2023_to_runtime']['unmatched'])}")


if __name__ == "__main__":
    main()
