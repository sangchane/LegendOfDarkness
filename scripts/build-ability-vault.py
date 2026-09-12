#!/usr/bin/env python3
"""원작 기술·마법을 Obsidian vault 로 남긴다 — 선행 관계를 걸어서.

`data/game-data/abilities.json` 613개(기술 275 · 마법 338)는 Hades 의
`database/server/metafile/SClass1~5` 에서 나온 **원작 표**다. 팩의 153개보다 풍부하고,
무엇보다 **무엇을 배워야 무엇을 배우는지**가 들어 있다 — 그 관계는 표로 보면 안 보이고
그래프로 봐야 보인다.

아이콘 번호는 여기 없다. `Legend.dat` 의 `skill.tbl`·`skill_e.tbl`·`skill_i.tbl` 이
스킬 번호 → 그림 파일을 준다(`data/archives-vault`).

  쓰는 법: python3 scripts/build-ability-vault.py
"""
import json, re, shutil
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "data" / "game-data" / "abilities.json"
VAULT = ROOT / "data" / "game-data" / "vault-abilities"

BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')
CLASS = {1: "전사", 2: "도적", 3: "마법사", 4: "사제", 5: "수도사"}
STATS = ["힘", "지능", "지혜", "체력", "민첩"]      # statCosts 다섯 칸의 순서는 아직 확인 못 했다


def slug(name):
    return BANNED.sub("_", name).strip()


def main():
    rows = json.loads(SRC.read_text(encoding="utf-8-sig"))
    if VAULT.exists():
        shutil.rmtree(VAULT)
    for d in ("기술", "마법"):
        (VAULT / d).mkdir(parents=True)

    folder = {"skill": "기술", "spell": "마법"}
    by_name = {r["name"]: r for r in rows}
    unlocks = defaultdict(list)
    for r in rows:
        if r.get("requires"):
            unlocks[r["requires"]].append(r["name"])

    dangling = set()
    for r in rows:
        kind = folder.get(r["kind"], "기술")
        need = r.get("requires")
        if need and need not in by_name:
            dangling.add(need)

        costs = " · ".join(f"{s} {v}" for s, v in zip(STATS, r.get("statCosts") or []) if v)
        opens = unlocks.get(r["name"], [])

        body = [
            "---",
            f'이름: "{r["name"]}"',
            f'갈래: {kind}',
            f'직업: "{CLASS.get(r.get("class"), r.get("class"))}"',
            f'선행: "{need or ""}"',
            f'레벨: {r.get("atLevel", 0)}',
            "출처: \"Hades database/server/metafile/SClass1~5\"",
            "---", "",
            f"# {r['name']}",
            "",
            f"{kind} · {CLASS.get(r.get('class'), '?')} · 레벨 {r.get('atLevel', 0)}",
            "",
        ]
        if costs:
            body += [f"능력치 요구: {costs}", "",
                     "> 다섯 칸의 순서는 아직 확인하지 못했다. 원문은 아래 raw.", ""]
        if need:
            mark = "" if need in by_name else "  ← **이 표에 없다**"
            body += [f"## 배우려면", f"[[{folder.get(by_name.get(need, {}).get('kind', 'skill'), '기술')}/{slug(need)}|{need}]]{mark}", ""]
        if opens:
            body += ["## 이것으로 열리는 것"] + [
                f"- [[{folder.get(by_name[o]['kind'], '기술')}/{slug(o)}|{o}]]" for o in sorted(opens)] + [""]
        body += ["## 원문", "```", " / ".join(r.get("raw") or []), "```", ""]

        (VAULT / kind / f"{slug(r['name'])}.md").write_text("\n".join(body), encoding="utf-8")

    roots = [r["name"] for r in rows if not r.get("requires")]
    kinds = Counter(r["kind"] for r in rows)
    (VAULT / "README.md").write_text(
        "# 원작 기술·마법 — 무엇을 배워야 무엇을 배우나\n\n"
        f"`data/game-data/abilities.json` 에서 났다. **기술 {kinds['skill']} · 마법 {kinds['spell']}**.\n"
        "Hades 의 `database/server/metafile/SClass1~5` 가 원본이고, 팩(5.99)의 153개와는 **다른 계보**다.\n\n"
        f"- 선행이 없는 것(맨 처음 배우는 것): **{len(roots)}개**\n"
        f"- 무언가를 여는 것: **{len(unlocks)}개**\n"
        f"- 선행으로 불리는데 이 표에 없는 이름: **{len(dangling)}개** — {', '.join(sorted(dangling)) or '없음'}\n\n"
        "직업별:\n\n| 직업 | 기술 | 마법 |\n|---|---|---|\n"
        + "\n".join(
            f"| {CLASS.get(c, c)} | {sum(1 for r in rows if r.get('class') == c and r['kind'] == 'skill')} "
            f"| {sum(1 for r in rows if r.get('class') == c and r['kind'] == 'spell')} |"
            for c in sorted({r.get("class") for r in rows} - {None}))
        + "\n\n아이콘 번호는 여기 없다. `data/archives-vault` 의 `Legend — skill.tbl` 을 본다.\n"
          "\n`python3 scripts/build-ability-vault.py` 로 다시 만든다.\n",
        encoding="utf-8")
    print(f"기술 {kinds['skill']} · 마법 {kinds['spell']} · 선행 없는 것 {len(roots)} · "
          f"여는 것 {len(unlocks)} · 매달린 선행 {len(dangling)}")
    print(f"→ {VAULT.relative_to(ROOT)}/  (Obsidian 으로 연다)")


if __name__ == "__main__":
    main()
