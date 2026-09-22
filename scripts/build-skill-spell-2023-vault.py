#!/usr/bin/env python3
"""Render the checked 2023 skill/spell JSON as a standalone Obsidian vault."""
import json
import re
import shutil
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FACTS = ROOT / "data" / "skill-spell-2023" / "skills.json"
VAULT = ROOT / "data" / "skill-spell-2023-vault"
BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')


def slug(value): return BANNED.sub("_", str(value)).strip()
def link(folder, name, label=None): return f"[[{folder}/{slug(name)}|{label or name}]]"
def note_name(record): return slug(record["id"].replace(":", "--"))


def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def requirement(record):
    item = record["requirement"]
    if item["type"] == "level": return f"레벨 {item['value']}"
    if item["type"] == "ability": return f"어빌리티 {item['value']}"
    if item["type"] == "ascension": return "승급"
    return "표에 없음"


def main():
    facts = json.loads(FACTS.read_text(encoding="utf-8")); records = facts["records"]
    if VAULT.exists(): shutil.rmtree(VAULT)
    by_class = defaultdict(list)
    for record in records: by_class[record["class"]].append(record)
    targets = set()
    for record in records:
        filename = note_name(record); targets.add(f"능력/{filename}")
        source = record["source"]
        body = ["---", f'id: "{record["id"]}"', f'직업: "{record["class"]}"', f'구분: "{record["section"]}"',
                f'원본시트: "{source["sheet"]}"', f'원본행: {source["row"]}', "---", "",
                f'# {record["name"]}', "", f"{link('직업', record['class'])} · {record['section']} · {requirement(record)}", "",
                "## 표 값", "", f"- 비용: {record.get('cost_text') or '표에 없음'}"]
        if record["section"] == "ability": body += [f"- EXP: {record['exp'] if record['exp'] is not None else '표에 없음'}", f"- A.EXP: {record['ability_exp'] if record['ability_exp'] is not None else '표에 없음'}"]
        if record["materials"]: body += [f"- 준비물: {record['materials']}"]
        if record["book"]: body += [f"- 기술/마법서: {record['book']}"]
        if record["learning_location"]: body += [f"- 배우는 장소: {record['learning_location']}"]
        if record["notes"]: body += [f"- 비고: {record['notes']}"]
        body += ["", "## 출처", "", f"`{facts['source']['path']}` · `{source['sheet']}`!G{source['row']}:N{source['row']}", ""]
        write(VAULT / "능력" / f"{filename}.md", "\n".join(body))
    for klass, rows in by_class.items():
        lines = [f"# {klass}", "", "| 구분 | 이름 | 요구 | 원본 |", "|---|---|---|---|"]
        for r in rows: lines.append(f"| {r['section']} | {link('능력', note_name(r), r['name'])} | {requirement(r)} | {r['source']['sheet']} {r['source']['row']}행 |")
        write(VAULT / "직업" / f"{slug(klass)}.md", "\n".join(lines) + "\n")
        targets.add(f"직업/{slug(klass)}")
    count_rows = "\n".join(f"| {link('직업', c)} | {v.get('ordinary', 0)} | {v.get('ability', 0)} |" for c, v in facts["counts"]["by_class_section"].items())
    readme = "# 2023 기술·마법 표\n\n"
    readme += "사용자가 준 2023-01-03 워크북에서 뽑은 별도 계보다. **Hades SClass1~5의 613개 표와 섞지 않는다.**\n\n"
    readme += "| 직업 | 일반 | 어빌리티 |\n|---|---:|---:|\n" + count_rows + "\n\n"
    readme += f"총 **{len(records)}행**. 계산기/수식 보조 칸은 뽑지 않았다.\n\n"
    readme += "## 도적 시트\n\n`도적 (2)`와 `도적`은 서로 다른 대체본이다. 정본 `도적`에는 전자에 없는 `설치형트랩` 1행이 있어 정본으로 썼다. 값 충돌은 추정하거나 합치지 않았다. 상세 감사는 JSON `duplicate_sheet_audit`.\n\n"
    readme += "## 다시 만들기\n\n```bash\npython3 scripts/build-skill-spell-2023.py\npython3 scripts/build-skill-spell-2023-vault.py\npython3 scripts/build-skill-spell-2023-graph.py\n```\n"
    write(VAULT / "README.md", readme)
    # Every emitted wikilink must resolve inside this vault.
    broken = []
    for path in VAULT.rglob("*.md"):
        for target in re.findall(r"\[\[([^|\]]+)", path.read_text(encoding="utf-8")):
            if target not in targets: broken.append((path, target))
    if broken: raise SystemExit(f"깨진 wikilink: {broken[:3]}")
    print(f"2023 기술·마법 볼트 노트 {sum(1 for _ in VAULT.rglob('*.md'))}장 -> {VAULT.relative_to(ROOT)}")


if __name__ == "__main__": main()
