#!/usr/bin/env python3
"""갈래마다 **자료가 어디서 오는지**를 세어 Obsidian vault 로 낸다.

이 볼트가 있는 이유는 하나다 — 같은 것을 또 찾지 않기 위해서. 전에는 문서에
"아이템: Hades 3 · 팩 989" 라고 적어 두었는데, 하데스의 `database/assets/MetaFiles/ItemInfo0~3`
에 **2,110개**가 있었다. 손으로 적은 표가 틀려서 그것을 근거로 또 삽질했다.

그래서 글로 적지 않고 **센다.**

자료 출처 우선순위(AGENTS.md 규약과 같다). 위에서부터 찾고, 있으면 아래를 보지 않는다:
  0. **사람이 아는 원작** — 팩이 서로 어긋날 때 이것이 정한다. 날짜와 함께 적어 두고 다시 묻지 않는다.
  1. 하데스 자기 자료 — database/server · assets/MetaFiles · server/metafile(+more·backup)
  2. 원작 아카이브 — ItemInfo0~11 · SClass1~5 · SEvent1~7 · NPCIllust · .dat 11개
  3. 참고 저장소 16개 — 원작을 관찰해 사람이 적은 것(ETDA · SleepHunter4 …)
  4. 서버팩 — 2개가 **일치할 때만** 후보. 불일치하면 버린다.

  쓰는 법: python3 scripts/build-truth-vault.py   → data/truth-vault/
"""
import json, shutil, collections
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
META = FORK / "database/assets/MetaFiles"
TPL = FORK / "database/server/templates"
ORIG = ROOT / "data/game-data"
PACKS = ROOT / "data/server-packs/extracted"
VAULT = ROOT / "data" / "truth-vault"


def rows(path):
    try:
        return json.loads(Path(path).read_text(encoding="utf-8-sig"))
    except Exception:
        return []


def names(path, key):
    return {x[key] for x in rows(path) if isinstance(x, dict) and x.get(key)}


def templates(kind):
    """서버에 실린 것. 이름과, 우리가 쓴 표가 붙었는지."""
    out = {}
    for f in sorted((TPL / kind).rglob("*.json")):
        try:
            d = json.loads(f.read_text(encoding="utf-8-sig"))
        except Exception:
            continue
        out[d.get("Name") or f.stem] = str(d.get("Group") or "")
    return out


def pack(kind, key="이름"):
    return {p.name: {x[key]: x.get("fields", {}) for x in rows(PACKS / p.name / f"{kind}.json")}
            for p in sorted(PACKS.glob("*")) if p.is_dir()}


def one(v):
    while isinstance(v, list):
        v = v[0] if v else None
    return None if v is None else str(v).strip()


def agreement(packed, fields):
    """이름이 겹치는 것 중, 검사한 칸이 **전부** 맞는 것이 몇 개인가."""
    ps = list(packed.values())
    if len(ps) < 2:
        return None
    a, b = ps[0], ps[1]
    both = sorted(set(a) & set(b))
    per, whole = collections.Counter(), 0
    for n in both:
        ok = True
        for f in fields:
            x, y = one(a[n].get(f)), one(b[n].get(f))
            if x is None or y is None:
                continue
            if x == y:
                per[f] += 1
            else:
                per[f + " ✗"] += 1
                ok = False
        whole += ok
    return {"겹침": len(both), "전부일치": whole, "칸별": per,
            "쪽수": {k: len(v) for k, v in packed.items()}}


def note(title, hades, original, now, packs, verdict, howto):
    body = ["---", f'이름: "{title}"', "갈래: 자료출처", "---", "", f"# {title}", "",
            "## 하데스에 있나", ""]
    body += [f"- {line}" for line in hades] or ["- **없다.**"]
    body += ["", "## 원작에서 뽑아 둔 것", ""]
    body += [f"- {line}" for line in original] or ["- 없다."]
    body += ["", "## 지금 서버에 실린 것", ""]
    body += [f"- {line}" for line in now]
    if packs:
        body += ["", "## 팩 (규칙 3번 — 둘이 일치할 때만 후보)", ""]
        body += [f"- {line}" for line in packs]
    body += ["", "## 판정", "", verdict, ""]
    if howto:
        body += ["## 어디를 보나", ""] + [f"- {line}" for line in howto] + [""]
    (VAULT / "갈래" / f"{title}.md").write_text("\n".join(body), encoding="utf-8")
    return title, verdict


def main():
    if VAULT.exists():
        shutil.rmtree(VAULT)
    (VAULT / "갈래").mkdir(parents=True)

    metafiles = sorted(f.name for f in META.glob("*") if f.is_file()) if META.exists() else []
    index = []

    # ── 아이템 ─────────────────────────────────────────────────────────
    orig_items = names(ORIG / "items.json", "name")
    now_items = templates("items")
    ip = pack("items")
    from_orig = len(set(now_items) & orig_items)
    from_pack = len(set(now_items) & set(next(iter(ip.values()), {})))
    ag = agreement(ip, ["레벨제한", "방어력", "직업제한", "위즈변화", "콘변화", "힘변화", "덱스변화"])
    index.append(note(
        "아이템",
        [f"**`database/assets/MetaFiles/ItemInfo0~3`** — 원본 클라이언트가 읽는 아이템 표. "
         f"뽑아 둔 것이 `data/game-data/items.json` **{len(orig_items)}개**",
         "가진 칸: 이름 · 등급(book) · 요구레벨 · 무게 · 종류 · 설명. "
         "**방어력·피해·능력치 보정은 없다** — 그건 서버 값이라 클라이언트 표에 들어갈 수 없다"],
        [f"`data/game-data/items.json` {len(orig_items)}개 (영문 이름)"],
        [f"`templates/items/` **{len(now_items)}장**",
         f"그중 원작 이름과 겹치는 것 **{from_orig}개** · 팩 이름과 겹치는 것 **{from_pack}개**"],
        [f"쪽수 {ag['쪽수']} · 이름 겹침 {ag['겹침']} · 검사한 칸이 **전부 일치 {ag['전부일치']}**",
         "칸별: " + " · ".join(f"{k} {v}" for k, v in sorted(ag["칸별"].items()))] if ag else [],
        ("**규칙 1번 위반.** 하데스에 아이템 표가 있는데 서버에는 팩 것이 실려 있다. "
         f"원작 {len(orig_items)}개와 지금 실린 것의 이름 교집합이 {from_orig}개다 — 계보가 다르다"
         f"(원작은 영문판, 팩은 한국어판)."
         if from_pack > from_orig else "규칙대로다."),
        ["`docs/game-data.md` · `data/archives-vault/` · `tools/dat-extract`"]))

    # ── 괴물 ───────────────────────────────────────────────────────────
    now_mobs = templates("monsters")
    ours = {n for n, g in now_mobs.items() if n not in ("bees", "spider", "minion")}
    mp = pack("mobs")
    mag = agreement(mp, ["체력", "최소공격력", "최대공격력", "방어력", "경험치", "이미지"])
    # 사냥터별로 팩 2개가 일치하는 수치가 있는지. "그럼 우드랜드·포테의숲 것으로 하자" 는
    # 물음에 매번 다시 세지 않도록 여기서 답을 만든다.
    hunt = []
    area_of = {}
    for f in sorted((FORK / "database/server/areas").glob("*.json")):
        try:
            d = json.loads(f.read_text(encoding="utf-8-sig"))
            area_of[d["Id"]] = d["Name"]
        except Exception:
            pass
    for keyword in ("우드랜드", "포테의숲", "노비스"):
        kinds = set()
        for f in sorted((TPL / "monsters").rglob("*.json")):
            try:
                d = json.loads(f.read_text(encoding="utf-8-sig"))
            except Exception:
                continue
            if keyword in area_of.get(d.get("AreaID"), ""):
                kinds.add(d.get("Name"))
        ps = list(mp.values())
        if len(ps) < 2:
            continue
        both2 = sorted(k for k in kinds if k in ps[0] and k in ps[1])
        same = collections.Counter()
        for k in both2:
            for fld in ("체력", "최소공격력", "최대공격력", "방어력", "경험치"):
                x, y = one(ps[0][k].get(fld)), one(ps[1][k].get(fld))
                same[fld] += x is not None and x == y
        import re as _re2
        _base = lambda n: _re2.sub(r"\d+$", "", n or "")
        real_here = sorted(k for k in kinds if _base(k) in {_base(n) for n in ps[1]})
        hunt.append(f"**{keyword}** — 괴물 {len(kinds)}종 · **뿌리가 둘 다에 있는 것(실재) "
                    f"{len(real_here)}종**: {', '.join(real_here) or '없다'}")

    # 사람이 준 판정. 팩이 어긋날 때 이것이 정한다 — 적어 두지 않으면 또 묻게 된다.
    ps2 = list(mp.values())
    verdict_lines = []
    if len(ps2) >= 2:
        five, hon = ps2[0], ps2[1]
        import re as _re
        base = lambda n: _re.sub(r"\d+$", "", n)
        roots = {base(n) for n in five}
        # **둘 다에 있어야 실재다.** 한쪽에만 있으면 창작으로 본다 — 정확한 이름이 아니라
        # 뿌리로 본다: 5.99 는 `뱀1`·`뱀2`, 혼든은 `뱀` 이라 이름만 비교하면 0 이 나온다.
        hroots = {base(n) for n in hon}
        real_roots = sorted(roots & hroots)
        made_up = [n for n in hon if base(n) not in roots]
        variants = [n for n in five if base(n) not in hroots]
        ratios = []
        for n in sorted(set(five) & set(hon)):
            try:
                x, y = int(one(five[n].get("경험치"))), int(one(hon[n].get("경험치")))
                if x and y:
                    ratios.append(x / y)
            except (TypeError, ValueError):
                pass
        ratios.sort()
        big = [n for n in hon
               if (v := one(hon[n].get("체력"))) and v.isdigit() and int(v) > 1_500_000]
        verdict_lines = [
            "**2026-09-13, 사람의 판정** (팩이 어긋나므로 이것이 정한다):",
            f"- **둘 다에 있어야 실재다.** 뿌리가 둘 다에 있는 것 **{len(real_roots)}뿌리**만 쓴다. "
            f"혼든에만 있는 {len(made_up)}종과 5.99 에만 있는 {len(variants)}종은 **창작**이다 — "
            f"체력이 150만을 넘는 괴물이 혼든에 {len(big)}종 있고 최대가 "
            f"{max((int(one(hon[n].get('체력'))) for n in big if one(hon[n].get('체력')).isdigit()), default=0):,} 이다",
            f"- **5.99 가 뻥튀기됐다.** 경험치는 혼든이 원작에 가깝다 — 겹치는 것의 비율 중간값 "
            f"{ratios[len(ratios)//2]:.1f}배, 최대 {ratios[-1]:.1f}배"
            f"(`그린팜팻` 56,000 대 2,220). 1레벨의 다음 레벨까지가 600 이니 5.99 는 한 마리에 여섯 "
            f"레벨이 오른다" if ratios else "",
            "- 방어력·공격력은 유저가 알 수 없는 값이라 사람이 판정할 수 없다. 그림 번호는 겹치는 것에서 "
            "대부분 일치하므로 **신원은 믿을 수 있고 수치만 못 믿는다**",
        ]

    index.append(note(
        "괴물",
        ["`templates/monsters/insight_1` · `minions` — **3마리**(bees · spider · minion). 그게 전부다",
         "**메타파일에 없다.** 원본 클라이언트는 괴물 수치를 받지 않는다",
         "**원작 아카이브에도 없다.** `Legend.dat`의 `MobTile.tbl` 은 프레임 수, `mns###.pal` 은 색표다",
         "`DADataViewer/MonstersForm` 도 그림만 읽는다 (`mpf` · `mns###.pal`)",
         "**수치는 식이 만든다** — `scripts/Creations/monsters.cs` 가 `Level` 에서 체력·방어·능력치를, "
         "`Formulas/damage.cs` 가 공격력을, `Formulas/monsterexp.cs` 가 경험치를 만든다"],
        ["없다. 원작 쪽에 괴물 수치 자료가 존재하지 않는다"],
        [f"`templates/monsters/` **{len(now_mobs)}장** (하데스 3 + 우리가 넣은 {len(ours)})",
         "**수치는 비어 있다** — `MaximumHP: 0` 이라 하데스 식이 레벨에서 만든다. "
         "5.99 수치를 넣었다가 되돌렸다(`444b7f5`)"],
        ([f"쪽수 {mag['쪽수']} · 이름 겹침 {mag['겹침']} · 검사한 칸이 **전부 일치 {mag['전부일치']}**",
          "칸별: " + " · ".join(f"{k} {v}" for k, v in sorted(mag["칸별"].items())),
          "", "**사냥터별로 본 것** (쓸 수 있는 수치가 있는지):"] + hunt
         + ([""] + verdict_lines if verdict_lines else [])) if mag else [],
        ("하데스에 데이터가 없다(규칙 1·2번 해당 없음). 팩은 **값이 하나도 일치하지 않는다** — "
         f"이름이 그대로 겹치는 {mag['겹침']}마리조차 검사한 칸 전부 일치가 {mag['전부일치']}개다. "
         "그래서 규칙 3번만으로는 쓸 값이 없고, **규칙 0번(사람의 판정)이 정한다**: 뿌리가 둘 다에 "
         "있는 것만 실재로 보고, 어긋나는 값은 혼든 쪽을 쓴다(5.99 가 뻥튀기됐다). 수치를 아직 넣지 "
         "않았으므로 지금 서버는 하데스 식이 레벨에서 만든 값으로 돈다"
         if mag else "팩 자료를 못 읽었다."),
        ["`data/formula-vault/` — 식이 레벨에서 무엇을 만드는지"]))

    # ── 기술·마법 ──────────────────────────────────────────────────────
    ab = rows(ORIG / "abilities.json")
    sk, sp = templates("skills"), templates("spells")
    index.append(note(
        "기술·마법",
        ["**`database/assets/MetaFiles/SClass1~5`** — 직업별 기술·마법 표. "
         f"뽑아 둔 것이 `data/game-data/abilities.json` **{len(ab)}개**",
         "`database/server/scripts/` — **기술 스크립트 26 · 마법 스크립트 40** (실제 동작)",
         "`Generic Elemental Single` · `Generic Elemental Mass` 는 **공용 스크립트**다"],
        [f"`abilities.json` {len(ab)}개 — 기술 {sum(1 for a in ab if a.get('kind')=='skill')} · "
         f"마법 {sum(1 for a in ab if a.get('kind')=='spell')}. 요구레벨·포인트·선행까지 들어 있다",
         "2차 직업(어빌리티) 능력이 " + str(sum(1 for a in ab
            if (str((a.get('raw') or [''])[0]) + '/0/0').split('/')[1] == '1')) + "개"],
        [f"`templates/skills/` {len(sk)}장 · `templates/spells/` {len(sp)}장",
         f"우리가 쓴 표(`원작표`)가 붙은 것 기술 {sum(1 for g in sk.values() if g.startswith('원작표'))} · "
         f"마법 {sum(1 for g in sp.values() if g.startswith('원작표'))}"],
        ["**팩은 안 본다.** 하데스+원작으로 끝난다 — 팩은 한글 이름이라 하데스 스크립트(영문)와 "
         "붙지도 않는다.",
         "**3번 출처가 있다**: `sources/wren11/ETDA/BotCore/Shared/Collections.cs` 가 기술·마법의 "
         "직업과 요구레벨을 코드에 갖고 있다 — `Kelberoth Strike` = Monk 23 · `Kelberoth Stance` = "
         "Monk 30 · `Dark Spear` = Monk 7. 원작 `SClass` 에서 `0/0/0` 으로 비어 있던 12개의 답이 "
         "거기 있다. `sources/FallenDev/SleepHunter4/data/Skills.xml`·`Spells.xml`·`Staves.xml` 도 같다"],
        "규칙대로다. 베이스가 하데스 스크립트 66개 + 원작 `SClass` 613개이고 팩을 보지 않는다.",
        ["`data/formula-vault/구현/기술·마법이 실제로 도는가` — 몇 개가 실제로 도는지"]))

    # ── 퀘스트 · NPC 초상 ──────────────────────────────────────────────
    q = rows(ORIG / "quests.json")
    por = rows(ORIG / "npc-portraits.json")
    index.append(note(
        "퀘스트·NPC초상",
        [f"**`MetaFiles/SEvent1~7`** — 퀘스트 표. 뽑아 둔 것이 `data/game-data/quests.json` **{len(q)}개**",
         f"**`MetaFiles/NPCIllust`** — NPC 초상. `data/game-data/npc-portraits.json` **{len(por)}개**"],
        [f"퀘스트 {len(q)} · NPC 초상 {len(por)}"],
        [f"`templates/mundanes/` {len(templates('mundanes'))}장"],
        [],
        "규칙대로다 — 퀘스트와 초상은 하데스 메타파일에서 나왔다.",
        []))

    # ── 메타파일 목록 ──────────────────────────────────────────────────
    (VAULT / "갈래" / "하데스 메타파일.md").write_text(
        "---\n이름: \"하데스 메타파일\"\n갈래: 자료출처\n---\n\n"
        "# 하데스 메타파일\n\n"
        "`database/assets/MetaFiles/`. **원본 클라이언트가 읽는 데이터**이고, 여기 있는 것은\n"
        "규칙 1번에 따라 **팩으로 덮지 않는다.** 여기를 안 보고 \"하데스에 없다\" 고 단정한 적이 있다.\n\n"
        "| 파일 | 크기 | 무엇 | 뽑아 둔 것 |\n|---|---|---|---|\n"
        + "\n".join(f"| `{n}` | {(META / n).stat().st_size:,}바이트 | "
                    + ("아이템" if n.startswith("ItemInfo") else
                       "직업별 기술·마법" if n.startswith("SClass") else
                       "퀘스트" if n.startswith("SEvent") else
                       "NPC 초상" if n == "NPCIllust" else
                       "국가 설명" if n == "NationDesc" else
                       "조명" if n == "Light" else "?")
                    + " | "
                    + ("`data/game-data/items.json`" if n.startswith("ItemInfo") else
                       "`data/game-data/abilities.json`" if n.startswith("SClass") else
                       "`data/game-data/quests.json`" if n.startswith("SEvent") else
                       "`data/game-data/npc-portraits.json`" if n == "NPCIllust" else "—")
                    + " |" for n in metafiles)
        + "\n\n꺼내는 도구: `tools/dat-extract` · `scripts/build-game-data.ps1`\n",
        encoding="utf-8")

    (VAULT / "README.md").write_text(
        "# 자료가 어디서 오는가\n\n"
        "**판단 절차**\n\n"
        "1. **하데스에 데이터가 있으면 하데스다.** 팩으로 **덮지 않는다** — 파일이든 식이든 기본값이든.\n"
        "2. **없을 때만** 팩을 본다.\n"
        "3. **그때도 팩 2개가 일치할 때만** 후보다. 불일치하면 **버린다**.\n\n"
        "손으로 적은 표는 틀린다. 전에 \"아이템: Hades 3 · 팩 989\" 라고 적어 뒀는데 하데스\n"
        "메타파일에 2,110개가 있었다. 그래서 이 볼트는 **셈을 매번 다시 한다.**\n\n"
        "| 갈래 | 판정 |\n|---|---|\n"
        + "\n".join(f"| [[갈래/{t}\\|{t}]] | {v.splitlines()[0][:90]} |" for t, v in index)
        + "\n\n[[갈래/하데스 메타파일|하데스 메타파일 — 여기부터 본다]]\n\n"
          "`python3 scripts/build-truth-vault.py` 로 다시 만든다.\n",
        encoding="utf-8")

    for t, v in index:
        print(f"  {t:14} {v.splitlines()[0][:80]}")
    print(f"\n→ {VAULT.relative_to(ROOT)}/  (Obsidian 으로 연다)")


if __name__ == "__main__":
    main()
