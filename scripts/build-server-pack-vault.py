#!/usr/bin/env python3
"""추출한 JSON 을 Obsidian vault 로 바꾼다 — 팩마다 따로.

팩은 저마다 손댄 물건이다. 같은 "아벨마을"이라도 5.99 와 혼든이 다르고, 섞으면
어느 쪽 수치인지 알 수 없게 된다. 그래서 vault 를 팩마다 하나씩 만든다.

링크는 자료에서 그대로 나온다. 짐작으로 잇지 않는다.

  맵   ─워프→ 맵        warps
  맵   ─산다→ 괴물      mob_spawns
  맵   ─선다→ NPC       npc_spawns
  상점 ─판다→ 아이템    shops
  스크립트 ─부른다→ 맵·아이템·괴물   script 본문의 warp "..." / item_add "..."
  함정 ─걸린다→ 맵·스크립트          traps

이름이 겹치는 것은 한 장으로 합치고 `출처` 에 원본 파일을 모두 적는다.

  쓰는 법: python3 scripts/build-server-pack-vault.py
"""
import json, re, shutil
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EXTRACTED = ROOT / "data" / "server-packs" / "extracted"
VAULT_ROOT = ROOT / "data" / "server-packs" / "vault"

# 폴더이름 -> (json 파일, 사람이 읽을 이름)
CATS = [("맵", "maps"), ("괴물", "mobs"), ("아이템", "items"), ("NPC", "npcs"),
        ("마법", "spells"), ("기술", "skills"), ("상점", "shops"), ("스크립트", "scripts"),
        ("퀘스트", "quests"), ("이벤트", "events")]

# 퀘스트와 이벤트는 생김새가 다르다. 퀘스트의 이름은 스크립트 변수 이름이고
# (자료에 사람이 읽을 이름이 없다), 이벤트의 이름은 아이템을 담은 폴더 이름이다.
NAME_KEY = {"quests": "변수", "events": "묶음"}

# Obsidian 과 파일시스템이 싫어하는 글자. 이름 자체는 frontmatter 에 그대로 남긴다.
BAD = re.compile(r'[\\/:*?"<>|\[\]#^]')


def slug(name):
    s = BAD.sub("_", name).strip().strip(".")
    return s[:120] or "_이름없음"


def resolve_slugs(names):
    """이름마다 겹치지 않는 파일이름을 정한다.

    macOS 는 파일이름의 대소문자를 구분하지 않는다. 팩에는 `루어스은행A` 와
    `루어스은행a` 가 둘 다 있고 — 서로 다른 맵이다 — 그냥 쓰면 한 장이 다른 한 장을
    덮어써서 조용히 사라진다. 겹치면 뒤에 ~2 를 붙인다. 이름 순으로 정하므로
    다시 빌드해도 같은 파일에 같은 것이 간다.
    """
    out, taken = {}, {}
    for name in sorted(names):
        base = slug(name)
        cand, i = base, 1
        while cand.lower() in taken:
            i += 1
            cand = f"{base}~{i}"
        taken[cand.lower()] = name
        out[name] = cand
    return out


def load(pack, key):
    f = EXTRACTED / pack / f"{key}.json"
    return json.loads(f.read_text(encoding="utf-8")) if f.exists() else []


def yaml_str(s):
    return '"' + str(s).replace('\\', '\\\\').replace('"', '\\"') + '"'




def field_table(fields):
    rows = []
    for k, v in fields.items():
        if k == "_머리말":
            continue
        if isinstance(v, list):
            v = "<br>".join(" / ".join(x) if isinstance(x, list) else str(x) for x in v)
        rows.append(f"| {k} | {v} |")
    if not rows:
        return ""
    return "| 칸 | 값 |\n|---|---|\n" + "\n".join(rows)


def build(pack):
    out = VAULT_ROOT / pack
    # 자기 출력만 먼저 비운다. macOS 는 파일이름의 대소문자를 보존만 하고 구분하지 않아서,
    # 지난 빌드가 만든 `프리프리lev99.md` 위에 `프리프리Lev99.md` 를 쓰면 같은 파일에 쓰이고
    # 이름은 옛 대소문자가 남는다. 그러면 링크가 가리키는 이름과 실제 파일이름이 어긋난다.
    if out.exists():
        assert out.parent == VAULT_ROOT, out      # vault/<팩> 밖은 건드리지 않는다
        shutil.rmtree(out)
    data = {key: load(pack, key) for _, key in CATS}
    for key, nk in NAME_KEY.items():
        for e in data.get(key, []):
            e["이름"] = e[nk]
            e.setdefault("출처", [])
            if isinstance(e["출처"], list):
                e["출처전체"] = e["출처"]
                e["출처"] = e["출처"][0] if e["출처"] else "(없음)"
    warps = load(pack, "warps")
    mob_spawns = load(pack, "mob_spawns")
    npc_spawns = load(pack, "npc_spawns")
    traps = load(pack, "traps")

    # ── 간선 모으기 ──
    out_warp, in_warp = defaultdict(set), defaultdict(set)
    for w in warps:
        if w["출발맵"] != w["도착맵"]:
            out_warp[w["출발맵"]].add(w["도착맵"])
            in_warp[w["도착맵"]].add(w["출발맵"])
    map_mobs, mob_maps = defaultdict(set), defaultdict(set)
    for s in mob_spawns:
        map_mobs[s["맵"]].add(s["괴물"]); mob_maps[s["괴물"]].add(s["맵"])
    map_npcs, npc_maps = defaultdict(set), defaultdict(set)
    for s in npc_spawns:
        map_npcs[s["맵"]].add(s["NPC"]); npc_maps[s["NPC"]].add(s["맵"])
    item_shops = defaultdict(set)
    for sh in data["shops"]:
        for it in sh["아이템"]:
            item_shops[it].add(sh["이름"])
    map_traps = defaultdict(set)
    for t in traps:
        map_traps[t["맵"]].add(t["스크립트"])
    npc_quests, item_quests = defaultdict(set), defaultdict(set)
    for q in data["quests"]:
        for n in q["NPC"]:
            npc_quests[n].add(q["이름"])
        for group in ("요구아이템", "회수아이템", "보상아이템"):
            for it, _cnt in q[group]:
                item_quests[it].add(q["이름"])
    event_items = defaultdict(set)
    for ev in data["events"]:
        for it in ev["아이템"]:
            event_items[it].add(ev["이름"])

    # 자료 안에서 불린 이름을 갈래별로 모은다. 정의가 없어도 불렸으면 한 장 만든다 —
    # 워프가 가리키는데 정의가 없는 맵, Spawn 에만 있고 Npc 에 없는 NPC 가 실제로 있다.
    defined = {cat: {e["이름"] for e in data[key]} for cat, key in CATS}
    referred = {cat: set() for cat, _ in CATS}
    referred["맵"] |= set(out_warp) | set(in_warp) | set(map_mobs) | set(map_npcs) | set(map_traps)
    referred["괴물"] |= set(mob_maps)
    referred["NPC"] |= set(npc_maps)
    referred["아이템"] |= set(item_shops) | set(item_quests) | set(event_items)
    referred["NPC"] |= set(npc_quests)
    # 어디서 불렸는지 거꾸로 찾아 둔다 — 정의 없는 쪽지에 적는다
    back = {cat: defaultdict(set) for cat, _ in CATS}
    for sc in data["scripts"]:
        for target, names in sc["부름"].items():
            for n in names:
                referred[target].add(n)
                back[target][n].add(("스크립트", sc["이름"]))
    for w in warps:
        back["맵"][w["도착맵"]].add(("맵", w["출발맵"]))
    for sp in mob_spawns:
        back["괴물"][sp["괴물"]].add(("맵", sp["맵"]))
    for sp in npc_spawns:
        back["NPC"][sp["NPC"]].add(("맵", sp["맵"]))
    for sh in data["shops"]:
        for it in sh["아이템"]:
            back["아이템"][it].add(("상점", sh["이름"]))
    for t in traps:
        back["스크립트"][t["스크립트"]].add(("맵", t["맵"]))
    for n, qq in npc_quests.items():
        for q in qq:
            back["NPC"][n].add(("퀘스트", q))
    for it, qq in item_quests.items():
        for q in qq:
            back["아이템"][it].add(("퀘스트", q))
    for it, ee in event_items.items():
        for e in ee:
            back["아이템"][it].add(("이벤트", e))
    known = {cat: defined[cat] | referred[cat] for cat, _ in CATS}
    slugs = {cat: resolve_slugs(known[cat]) for cat, _ in CATS}

    def maybe(cat, name):
        if name not in slugs[cat]:
            return f"`{name}`"
        return f"[[{cat}/{slugs[cat][name]}|{name}]]"

    written = 0
    for cat, key in CATS:
        merged = defaultdict(list)
        for e in data[key]:
            merged[e["이름"]].append(e)
        for n in referred[cat] - defined[cat]:
            merged.setdefault(n, [])          # 불렸지만 정의가 없는 것
        (out / cat).mkdir(parents=True, exist_ok=True)
        for name, entries in merged.items():
            body = [f"---",
                    f"이름: {yaml_str(name)}",
                    f"갈래: {cat}",
                    f"팩: {pack}",
                    f"출처: [{', '.join(yaml_str(e['출처']) for e in entries)}]"]
            if len(entries) > 1:
                body.append(f"정의수: {len(entries)}")
            if not entries:
                body.append("정의없음: true")
            body += ["---", "", f"# {name}", ""]
            if not entries:
                whence = sorted(back[cat].get(name, ()))
                body += ["> 이 이름은 자료 안에서 불리는데 **정의가 없다**. 팩에 빠진 것인지",
                         "> 오타인지는 확인되지 않았다.", ""]
                if whence:
                    body += [f"## 불린 곳 ({len(whence)})",
                             ", ".join(maybe(c, n) for c, n in whence), ""]

            if cat == "맵":
                for label, names, target in (("나가는 문", out_warp.get(name, ()), "맵"),
                                             ("들어오는 문", in_warp.get(name, ()), "맵"),
                                             ("사는 괴물", map_mobs.get(name, ()), "괴물"),
                                             ("선 NPC", map_npcs.get(name, ()), "NPC"),
                                             ("함정", map_traps.get(name, ()), "스크립트")):
                    if names:
                        body += [f"## {label} ({len(names)})",
                                 ", ".join(maybe(target, n) for n in sorted(names)), ""]
            elif cat == "괴물" and mob_maps.get(name):
                body += [f"## 나오는 맵 ({len(mob_maps[name])})",
                         ", ".join(maybe("맵", m) for m in sorted(mob_maps[name])), ""]
            elif cat == "NPC":
                for label, names, tgt in (("서 있는 맵", npc_maps.get(name, ()), "맵"),
                                          ("주는 퀘스트", npc_quests.get(name, ()), "퀘스트")):
                    if names:
                        body += [f"## {label} ({len(names)})",
                                 ", ".join(maybe(tgt, n) for n in sorted(names)), ""]
            elif cat == "아이템":
                for label, names, tgt in (("파는 곳", item_shops.get(name, ()), "상점"),
                                          ("걸린 퀘스트", item_quests.get(name, ()), "퀘스트"),
                                          ("이벤트", event_items.get(name, ()), "이벤트")):
                    if names:
                        body += [f"## {label} ({len(names)})",
                                 ", ".join(maybe(tgt, n) for n in sorted(names)), ""]
            elif cat == "퀘스트":
                q = entries[0] if entries else None
                if q:
                    if q["NPC"]:
                        body += [f"## 주는 사람 ({len(q['NPC'])})",
                                 ", ".join(maybe("NPC", n) for n in q["NPC"]), ""]
                    if q["전이"]:
                        body += [f"## 단계 ({len(q['단계'])})", "",
                                 "```", "  ".join(f"{a}→{b}" for a, b in q["전이"]), "```", ""]
                    for label, key in (("가져오라는 것", "요구아이템"),
                                       ("거두어 가는 것", "회수아이템"),
                                       ("주는 것", "보상아이템")):
                        if q[key]:
                            body += [f"## {label} ({len(q[key])})",
                                     ", ".join(f"{maybe('아이템', it)} ×{c}" for it, c in q[key]), ""]
                    extra = []
                    if q["보상경험치"]:
                        extra.append("경험치 " + ", ".join(q["보상경험치"]))
                    if q["보상돈"]:
                        extra.append("돈 " + ", ".join(q["보상돈"]))
                    if q["조건"]:
                        extra.append("조건 " + " · ".join(f"{k} {' '.join(v)}"
                                                          for k, v in q["조건"].items()))
                    if q["워프"]:
                        extra.append("보내는 곳 " + ", ".join(maybe("맵", m) for m in q["워프"]))
                    if extra:
                        body += ["## 그밖에", ""] + [f"- {x}" for x in extra] + [""]
                    if q["대사"]:
                        body += ["## 대사 (앞부분)", ""] + [f"> {d}" for d in q["대사"][:6]] + [""]
                    if q["정수아닌값"]:
                        body += ["## 정수가 아닌 값", "",
                                 "단계로 읽을 수 없는 값이 들어간다. 뜻은 확인되지 않았다.", "",
                                 ", ".join(f"`{x}`" for x in q["정수아닌값"][:10]), ""]
                    body += [f"출처: " + ", ".join(f"`db/{x}`" for x in
                                                  (q.get("출처전체") or [q["출처"]])), ""]
            elif cat == "이벤트":
                ev = entries[0] if entries else None
                if ev and ev["아이템"]:
                    body += [f"## 아이템 ({len(ev['아이템'])})",
                             ", ".join(maybe("아이템", i) for i in ev["아이템"]), ""]
            elif cat == "상점":
                goods = sorted({g for e in entries for g in e["아이템"]})
                if goods:
                    body += [f"## 파는 물건 ({len(goods)})",
                             ", ".join(maybe("아이템", g) for g in goods), ""]
            elif cat == "스크립트":
                for e in entries:
                    for target, names in e["부름"].items():
                        if names:
                            body += [f"## 부르는 {target} ({len(names)})",
                                     ", ".join(maybe(target, n) for n in names), ""]
                    if e.get("머리말"):
                        body += [f"머리말: `{e['머리말']}`", ""]
                    body += [f"줄수: {e['줄수']}", ""]

            for e in entries:
                if "fields" in e and cat not in ("퀘스트", "이벤트"):
                    t = field_table(e["fields"])
                    if t:
                        if len(entries) > 1:
                            body.append(f"### `{e['출처']}`")
                        body += [t, ""]
            if entries and cat not in ("퀘스트", "이벤트"):
                body += ["", f"원본: `db/{entries[0]['출처']}`"]
            (out / cat / f"{slugs[cat][name]}.md").write_text("\n".join(body), encoding="utf-8")
            written += 1

    counts = {cat: len(defined[cat]) for cat, _ in CATS}
    stubs = {cat: len(referred[cat] - defined[cat]) for cat, _ in CATS}
    readme = [f"# {pack} — 서버팩 자료", "",
              f"`data/server-packs/{pack}/db/` 를 읽어 만든 것. 고쳐도 다음 빌드에 지워진다 —",
              "고칠 곳은 원본 db 다. 만드는 법: `python3 scripts/build-server-pack-vault.py`", "",
              "| 갈래 | 장수 |", "|---|---|"]
    readme = readme[:-2] + ["| 갈래 | 정의된 것 | 불렸지만 정의 없음 |", "|---|---|---|"]
    readme += [f"| {c} | {n} | {stubs[c] or ''} |" for c, n in counts.items()]
    readme += ["", "## 이어진 것", "",
               f"- 워프 {len(warps)}개 — 맵과 맵을 잇는다",
               f"- 괴물 젠 {len(mob_spawns)}개 — 맵 {len(map_mobs)}곳에 괴물 {len(mob_maps)}종",
               f"- NPC 배치 {len(npc_spawns)}개 — 맵 {len(map_npcs)}곳에 NPC {len(npc_maps)}명",
               f"- 상점 {len(data['shops'])}곳이 아이템 {len(item_shops)}종을 판다",
               f"- 함정 {len(traps)}개",
               f"- 퀘스트 {len(data['quests'])}종 — NPC {len(npc_quests)}명이 주고 아이템 {len(item_quests)}종이 걸린다",
               f"- 이벤트 {len(data['events'])}묶음 — 아이템 {len(event_items)}종", "",
               "## 믿을 수 있는 만큼만", "",
               "칸 이름은 원본 db 에 적힌 그대로다. 뜻을 짐작해 붙인 이름은 없다.",
               "링크는 워프·젠·상점·스크립트 호출에서 그대로 나온 것이고, 이름이 자료에 없으면",
               "링크 대신 `글자` 로 남겼다 — 그게 오타인지 빠진 자료인지는 확인되지 않았다."]
    (out / "README.md").write_text("\n".join(readme), encoding="utf-8")
    return written, counts


def main():
    packs = sorted(d.name for d in EXTRACTED.iterdir() if d.is_dir())
    for pack in packs:
        n, counts = build(pack)
        print(f"══ {pack} — 노트 {n}장")
        print("   " + "  ".join(f"{c} {v}" for c, v in counts.items()))
    print(f"\n→ {VAULT_ROOT.relative_to(ROOT)}/<팩>/  (Obsidian 에서 각각 열면 된다)")


if __name__ == "__main__":
    main()
