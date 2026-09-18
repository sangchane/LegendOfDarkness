#!/usr/bin/env python3
"""원작 4.51 UI 자료와 그것으로 정한 테마 규칙을 Obsidian vault 로 남긴다.

**화면을 만들 때마다 규칙을 다시 유추하지 않기 위한 것이다.** 원작 UI 는 그림 92장인데
어느 그림이 무슨 창인지, 어느 색을 어디에 쓰기로 했는지, 그 결정의 근거가 무엇인지가
흩어지면 다음 사람이 다시 캔다. 한 번 캐서 이어 놓는다.

  규칙 → 어느 재질·색·치수를 쓰나 → 그 재질이 어느 원작 그림에서 나왔나 → 어느 코드가 그것을 쓸 차례인가

단일 출처는 `data/original-ui/451.json` 이다. 여기서는 **아무것도 새로 판단하지 않는다** —
JSON 에 없는 것은 볼트에도 없다. 뜻을 모르는 화면은 모른다고 적는다.

  쓰는 법: python scripts/build-ui-vault.py
"""
import json
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FACTS = ROOT / "data" / "original-ui" / "451.json"
VAULT = ROOT / "data" / "ui-vault"
MOCKUPS = "docs/ui/mockups-451/index.html"

BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')

for stream in (sys.stdout, sys.stderr):
    if hasattr(stream, "reconfigure"):
        stream.reconfigure(encoding="utf-8")


def slug(name):
    """Obsidian 이 파일 이름으로 못 쓰는 글자를 뺀다. `Greybox.cs` 처럼 점이 든 것은 그대로 둔다."""
    return BANNED.sub("_", str(name)).strip()


def link(folder, name, shown=None):
    return f"[[{folder}/{slug(name)}|{shown or name}]]"


def front(**pairs):
    lines = ["---"]
    for key, value in pairs.items():
        if value is None:
            continue
        if isinstance(value, bool):
            lines.append(f"{key}: {str(value).lower()}")
        elif isinstance(value, int):
            lines.append(f"{key}: {value}")
        else:
            lines.append(f'{key}: "{value}"')
    lines.append("---\n")
    return "\n".join(lines)


def write(folder, name, body):
    path = VAULT / folder / f"{slug(name)}.md"
    path.write_text(body, encoding="utf-8")
    return path


def versions(facts):
    """판 — 4.51 · 2005 · 5.01. 어느 것을 채택했고 UI 방식이 어떻게 갈리나."""
    for v in facts["판"]:
        archives = [a for a in facts["아카이브"] if a["판"] == v["id"]]
        body = (
            front(판=v["이름"], 번호=v["id"], 날짜=v["날짜"], 채택=v.get("채택", False))
            + f"# {v['이름']}\n\n"
            + ("**이 판의 테마를 쓴다.**\n\n" if v.get("채택") else "")
            + f"- 설치본: `{v['설치본']}`\n"
            + (f"- 실행 파일: `{v['실행파일']}` {v['크기']:,} 바이트 · md5 `{v['md5']}`\n"
               if v.get("실행파일") else "")
            + (f"- `version.nfo`: `{v['version.nfo']}`\n" if v.get("version.nfo") else "")
            + f"\n## UI 를 만드는 방식\n\n{v['UI 방식']}\n"
            + f"\n## 메모\n\n{v['메모']}\n"
        )
        if archives:
            body += "\n## 아카이브\n\n" + "\n".join(
                f"- {link('아카이브', a['이름'])} — {a['메모']}" for a in archives) + "\n"
        write("판", v["이름"], body)
    return len(facts["판"])


def archives(facts):
    for a in facts["아카이브"]:
        version = next(v for v in facts["판"] if v["id"] == a["판"])
        inside = "\n".join(f"- `{k}` {n}개" for k, n in sorted(a["들어있는것"].items(),
                                                              key=lambda kv: -kv[1]))
        # 이 아카이브에서 나온 재질
        made = [m for m in facts["재질"] if m["원본"].removesuffix(".epf") in
                {s["파일"].removesuffix(".epf") for s in facts["화면"]} and a["이름"] == "Legend.dat"]
        body = (
            front(아카이브=a["이름"], 판=version["이름"], 크기MB=a["크기"] // (1024 * 1024))
            + f"# {a['이름']}\n\n{a['메모']}\n\n"
            + f"판: {link('판', version['이름'])}\n\n## 들어 있는 것\n\n{inside}\n"
        )
        if made:
            body += "\n## 여기서 뽑은 재질\n\n" + "\n".join(
                f"- {link('재질', m['id'])} — `{m['원본']}`" for m in made) + "\n"
        write("아카이브", a["이름"], body)
    return len(facts["아카이브"])


def screens(facts):
    known = [s for s in facts["화면"] if s["확인"] == "그림으로 확인"]
    for s in facts["화면"]:
        stem = s["파일"].removesuffix(".epf")
        used_by = [m for m in facts["재질"] if m["원본"] == s["파일"]]
        body = (
            front(파일=s["파일"], 무엇=s["무엇"], 크기=s["크기"], 확인=s["확인"])
            + f"# {s['파일']}\n\n**{s['무엇']}** — {s['메모']}\n\n"
            + f"- 그림: `{s['그림']}` ({s['크기']})\n"
            + f"- 확인: {s['확인']}\n"
            + f"- 아카이브: {link('아카이브', 'Legend.dat')}\n"
        )
        if used_by:
            body += "\n## 시안이 이 그림을 쓴다\n\n" + "\n".join(
                f"- {link('재질', m['id'])} — {', '.join(m['쓰는곳'])}" for m in used_by) + "\n"
        body += ("\n## 다시 그리는 법\n\n```bash\n"
                 f"{facts['뽑는법']['그림그리기'].replace('<이름>', stem)}\n```\n")
        write("화면", s["파일"], body)
    return len(facts["화면"]), len(known)


def materials(facts):
    for m in facts["재질"]:
        rules = [r for r in facts["규칙"] if m["id"] in r.get("재질", [])]
        source = next((s for s in facts["화면"] if s["파일"] == m["원본"]), None)
        drawn = link("화면", m["원본"]) if source else f"`{m['원본']}`"
        body = (
            front(재질=m["id"], 원본=m["원본"], 평균색=m["평균색"])
            + f"# {m['id']}\n\n"
            + f"- 원작 그림: {drawn}\n"
        )
        body += (
            f"- 크기: {m['크기']} · 평균색 `{m['평균색']}`\n"
            f"- 시안 파일: `{m['시안파일']}`\n"
            f"- 쓰는 곳: {', '.join(m['쓰는곳'])}\n"
            f"- 그 위의 글자: {m['글자']}\n"
            f"\n## 왜 그렇게 정했나\n\n{m['근거']}\n"
        )
        if rules:
            body += "\n## 이 재질이 걸린 규칙\n\n" + "\n".join(
                f"- {link('규칙', r['id'])} — {r['말']}" for r in rules) + "\n"
        write("재질", m["id"], body)
    return len(facts["재질"])


def colours(facts):
    for c in facts["색"]:
        rules = [r for r in facts["규칙"] if c["id"] in r.get("색", [])]
        body = (
            front(색=c["id"], 값=c["값"], 무엇=c["무엇"])
            + f"# {c['id']} `{c['값']}`\n\n**{c['무엇']}**\n\n출처: {c['출처']}\n"
        )
        if rules:
            body += "\n## 이 색이 걸린 규칙\n\n" + "\n".join(
                f"- {link('규칙', r['id'])} — {r['말']}" for r in rules) + "\n"
        write("색", c["id"], body)
    return len(facts["색"])


def sizes(facts):
    rows = "\n".join(f"| `{s['id']}` | `{s['값']}` | {s['무엇']} |" for s in facts["치수"])
    write("치수", "치수", front(무엇="치수 한 벌")
          + "# 치수\n\n| 이름 | 값 | 무엇 |\n|---|---|---|\n" + rows + "\n")
    return len(facts["치수"])


def rules(facts):
    for r in facts["규칙"]:
        body = (
            front(규칙=r["id"])
            + f"# {r['id']}\n\n## 규칙\n\n**{r['말']}**\n\n## 왜\n\n{r['왜']}\n\n## 근거\n\n{r['근거']}\n"
        )
        for key, folder in (("재질", "재질"), ("색", "색")):
            if r.get(key):
                body += f"\n## {key}\n\n" + "\n".join(
                    f"- {link(folder, one)}" for one in r[key]) + "\n"
        if r.get("구현대상"):
            body += "\n## 이 규칙이 닿는 코드\n\n" + "\n".join(
                f"- {link('구현대상', Path(f).name, f)}" for f in r["구현대상"]) + "\n"
        write("규칙", r["id"], body)
    return len(facts["규칙"])


def drafts(facts):
    for d in facts["시안"]:
        body = (
            front(시안=d["id"], 이름=d["이름"], 채택=d["채택"])
            + f"# {d['id']} · {d['이름']}\n\n"
            + ("**채택됐다.**\n\n" if d["채택"] else "**채택되지 않았다.**\n\n")
            + f"## 무엇\n\n{d['무엇']}\n\n## 판정\n\n{d['판정']}\n\n"
            + f"눌러볼 것: `{MOCKUPS}`" + (" · 3안 비교 `three-ways.html`\n" if not d["채택"] else "\n")
        )
        write("시안", d["id"], body)

    for s in facts["화면시안"]:
        code = s["구현대상"]
        body = (
            front(화면=s["id"], 구현대상=code)
            + f"# {s['id']}\n\n- 그린 상태: {', '.join(s['상태'])}\n"
            + f"- 코드: {link('구현대상', Path(code).name, code)}\n"
            + f"- 눌러볼 것: `{MOCKUPS}`\n"
        )
        write("화면시안", s["id"], body)
    return len(facts["시안"]), len(facts["화면시안"])


def targets(facts):
    for t in facts["구현대상"]:
        name = Path(t["파일"]).name
        rules_here = [r for r in facts["규칙"] if t["파일"] in r.get("구현대상", [])]
        screens_here = [s for s in facts["화면시안"] if s["구현대상"] == t["파일"]]
        body = (
            front(파일=t["파일"], 상태=t["상태"])
            + f"# {name}\n\n**{t['무엇']}**\n\n- 경로: `{t['파일']}`\n- 상태: {t['상태']}\n"
        )
        if screens_here:
            body += "\n## 이 파일이 그리는 화면\n\n" + "\n".join(
                f"- {link('화면시안', s['id'])}" for s in screens_here) + "\n"
        if rules_here:
            body += "\n## 지켜야 할 규칙\n\n" + "\n".join(
                f"- {link('규칙', r['id'])} — {r['말']}" for r in rules_here) + "\n"
        write("구현대상", name, body)
    return len(facts["구현대상"])


def readme(facts, counts):
    adopted = next(d for d in facts["시안"] if d["채택"])
    version = next(v for v in facts["판"] if v.get("채택"))
    rule_rows = "\n".join(f"| {link('규칙', r['id'])} | {r['말']} |" for r in facts["규칙"])
    colour_rows = "\n".join(f"| {link('색', c['id'])} | `{c['값']}` | {c['무엇']} |" for c in facts["색"])
    target_rows = "\n".join(
        f"| {link('구현대상', Path(t['파일']).name, t['파일'])} | {t['무엇']} | {t['상태']} |"
        for t in facts["구현대상"])
    (VAULT / "README.md").write_text(
        "# 모바일 UI 테마 — 원작 4.51 을 쓴다\n\n"
        f"사용자가 고른 것은 **5.01 이전 판**이고, 우리가 가진 그 판은 {link('판', version['이름'])} 하나다.\n"
        f"시안 셋을 그려 **{adopted['id']} · {adopted['이름']}** 을 채택했다.\n\n"
        f"**화면을 만들거나 고치기 전에 규칙부터 읽는다.** 눌러볼 시안: `{MOCKUPS}`\n\n"
        "## 규칙\n\n| 규칙 | 말 |\n|---|---|\n" + rule_rows + "\n\n"
        "## 색\n\n| 이름 | 값 | 무엇 |\n|---|---|---|\n" + colour_rows + "\n\n"
        f"치수: {link('치수', '치수')}\n\n"
        "## 손댈 코드\n\n| 파일 | 무엇 | 상태 |\n|---|---|---|\n" + target_rows + "\n\n"
        "## 원작 자료\n\n"
        f"- 화면 **{counts['화면']}장** (`docs/ui/original-451/`) — 쓰임까지 확인된 것 {counts['확인']}장, "
        f"나머지는 모양만 봤다\n"
        f"- 재질 {counts['재질']} · 아카이브 {counts['아카이브']} · 판 {counts['판']}\n"
        f"- 글꼴: " + " · ".join(f"`{f['파일']}`({f['무엇']})" for f in facts["글꼴"]) + "\n\n"
        "## 함정\n\n" + "\n".join(f"- {t}" for t in facts["뽑는법"]["함정"]) + "\n\n"
        "---\n\n"
        "단일 출처는 `data/original-ui/451.json` 이다. 이 볼트는 그것을 옮겨 적은 것이므로,\n"
        "새로 알게 된 것은 **JSON 에 적고** `python scripts/build-ui-vault.py` 로 다시 만든다.\n"
        "그래프는 `python scripts/build-ui-graph.py`.\n",
        encoding="utf-8")


def main():
    if not FACTS.exists():
        sys.exit(f"{FACTS.relative_to(ROOT)} 가 없다")
    facts = json.loads(FACTS.read_text(encoding="utf-8"))

    if VAULT.exists():
        shutil.rmtree(VAULT)                 # 이름이 바뀌면 옛 노트가 남는다
    for folder in ("판", "아카이브", "화면", "재질", "색", "치수", "규칙", "시안", "화면시안", "구현대상"):
        (VAULT / folder).mkdir(parents=True)

    counts = {}
    counts["판"] = versions(facts)
    counts["아카이브"] = archives(facts)
    counts["화면"], counts["확인"] = screens(facts)
    counts["재질"] = materials(facts)
    counts["색"] = colours(facts)
    counts["치수"] = sizes(facts)
    counts["규칙"] = rules(facts)
    counts["시안"], counts["화면시안"] = drafts(facts)
    counts["구현대상"] = targets(facts)
    readme(facts, counts)

    notes = sum(1 for _ in VAULT.rglob("*.md"))
    print("UI 테마 볼트")
    for key in ("판", "아카이브", "화면", "재질", "색", "규칙", "시안", "화면시안", "구현대상"):
        print(f"  {key:8} {counts[key]}")
    print(f"\n노트 {notes}장 -> {VAULT.relative_to(ROOT)}/  (Obsidian 으로 연다)")


if __name__ == "__main__":
    main()
