#!/usr/bin/env python3
"""퀘스트와 이벤트를 스크립트에서 끄집어낸다 — 팩마다 따로.

퀘스트는 따로 표가 없다. 스크립트 안에 있고, 생김새가 일정하다.

    if(@npc$=="구피"){                              ← 누가 주나
      if(get_level(@myid)>=41){                     ← 조건
        if(#EestQuest014==1){                       ← 진행 단계
          if(item_exist(@myid,"고블린의조악한무기")>=6){  ← 요구
            item_del "고블린의조악한무기", 6;          ← 회수
            set #EestQuest014, 2;                   ← 다음 단계
            item_add "용의발톱",1;                    ← 보상

`#` 로 시작하는 변수는 캐릭터에 남는 값이다. **변수 하나가 퀘스트 하나**이고
그 정수값이 단계다. 그래서 변수를 기준으로 모으면 퀘스트가 그대로 나온다.

중괄호 깊이를 따라가며 "지금 어떤 조건 안에 있는지"를 쌓아 두고, 그 안에서 일어난
일(회수·보상·다음 단계)을 그 단계에 붙인다. 조건 밖에서 일어난 일은 붙이지 않는다.

**짐작하지 않는다.** 퀘스트 이름은 변수 이름 그대로 쓴다 — 자료에 사람이 읽을 이름이
없기 때문이다. 단계의 뜻(무엇을 해야 다음으로 가나)도 적지 않는다. 적힌 것만 옮긴다.

  쓰는 법: python3 scripts/build-server-pack-quests.py
"""
import json, re, sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PACKS_DIR = ROOT / "data" / "server-packs"
OUT_DIR = PACKS_DIR / "extracted"

SCRIPT_HEAD = re.compile(r"^(?P<head>[^\t]*)\t(?P<name>[^\t{]+?)\s*\{\s*$")
STRING = re.compile(r'"[^"]*"')


def braces_only(line):
    """중괄호를 셀 때 쓸 줄. 문자열 안의 것은 괄호가 아니다.

    대사에 색상 코드가 박혀 있다: mes 1,"...{=q강력한 {=a이란...". 이걸 그대로 세면
    깊이가 어긋나고, 한번 어긋나면 조건이 파일 끝까지 쌓여 엉뚱한 퀘스트에 보상이 붙는다.
    """
    return STRING.sub('""', line.split("//")[0])

RX = {
    "설정":   re.compile(r'set\s*#(\w+)\s*,\s*(-?\d+)'),
    "설정아무거나": re.compile(r'set\s*#(\w+)\s*,\s*([^;{}]+)'),
    "검사":   re.compile(r'#(\w+)\s*(==|!=|>=|<=|>|<)\s*(-?\d+)'),
    "쓰임":   re.compile(r'#(\w+)'),
    "NPC":    re.compile(r'@npc\$\s*==\s*"([^"]+)"'),
    "보상":   re.compile(r'item_add\s+"([^"]+)"\s*,\s*(\d+)'),
    "회수":   re.compile(r'item_del\s+"([^"]+)"\s*,\s*(\d+)'),
    "요구":   re.compile(r'item_exist\s*\([^,]+,\s*"([^"]+)"\s*\)\s*(?:>=|==|>)\s*(\d+)'),
    "경험치": re.compile(r'exp_add\s*[ (]\s*([^;,)]+)'),
    "돈":     re.compile(r'money_add\s*[ (]\s*([^;,)]+)'),
    "레벨":   re.compile(r'get_level\s*\([^)]*\)\s*(>=|==|>|<=|<)\s*(\d+)'),
    "직업":   re.compile(r'get_class\s*\([^)]*\)\s*(==|!=|>=)\s*(\d+)'),
    "승급":   re.compile(r'get_class_sub\s*\([^)]*\)\s*(==|!=|>=)\s*(\d+)'),
    "대사":   re.compile(r'\bmes\s+\d+\s*,\s*"([^"]{4,})"'),
    "워프":   re.compile(r'\bwarp\s+"([^"]+)"'),
}


def _is_int(x):
    try:
        int(x.strip())
        return True
    except ValueError:
        return False


def read(p):
    return p.read_text(encoding="utf-8", errors="replace")


def scripts_in(path):
    """머리말<TAB>이름<TAB>{ 본문 } — 본문을 그대로 돌려준다."""
    out, lines, i = [], read(path).splitlines(), 0
    while i < len(lines):
        m = SCRIPT_HEAD.match(lines[i].rstrip())
        if not m:
            i += 1
            continue
        name = m.group("name").strip()
        depth, body, i = 1, [], i + 1
        while i < len(lines) and depth > 0:
            b = braces_only(lines[i])
            depth += b.count("{") - b.count("}")
            if depth > 0:
                body.append(lines[i])
            i += 1
        out.append((name, body))
    return out


def walk(body):
    """줄마다 '지금 어떤 조건 안에 있나'를 알려준다.

    여는 중괄호마다 그 줄의 if 조건을 붙여 쌓고, 닫는 중괄호에서 그 깊이의 틀을 버린다.
    깊이를 글자 단위로 세야 한다 — 줄 단위로 세면 `}else{` 나 한 줄짜리 if 에서 어긋나고,
    한번 어긋나면 조건이 파일 끝까지 쌓여 엉뚱한 퀘스트에 보상이 붙는다.
    """
    depth, stack = 0, []          # stack: [(그 틀이 열린 깊이, {변수:값들}, {npc})]

    def conds_of(line):
        cv = defaultdict(set)
        for v, op, n in RX["검사"].findall(line):
            if op == "==":
                cv[v].add(int(n))
        return dict(cv), set(RX["NPC"].findall(line))

    for line in body:
        s_ = line.strip()
        cvars, cnpcs = conds_of(s_)

        scope_vars, scope_npcs = defaultdict(set), set()
        for _, vs, ns in stack:
            for v, vals in vs.items():
                scope_vars[v] |= vals
            scope_npcs |= ns
        for v, vals in cvars.items():
            scope_vars[v] |= vals
        scope_npcs |= cnpcs

        yield s_, scope_vars, scope_npcs

        pending_vars, pending_npcs = cvars, cnpcs
        for ch in braces_only(s_):
            if ch == "{":
                depth += 1
                stack.append((depth, pending_vars, pending_npcs))
                pending_vars, pending_npcs = {}, set()   # 한 줄에 여러 개면 첫 것만 조건을 갖는다
            elif ch == "}":
                if stack and stack[-1][0] == depth:
                    stack.pop()
                depth -= 1


def collect(db, npc_names=()):
    """스크립트 전부를 훑어 변수별로 모은다.

    NPC 를 알아내는 길이 팩마다 다르다. 혼든은 본문에서 @npc$=="구피" 로 확인하고,
    5.99 는 그런 검사 없이 스크립트 이름 자체가 NPC 이름이다(초보자도우미1).
    이름이 같다는 것은 확인할 수 있는 사실이므로, npc 표에 있는 이름과 같을 때만 잇는다.
    """
    quests = defaultdict(lambda: {
        "출처": set(), "스크립트": set(), "NPC": set(), "단계": set(),
        "전이": set(), "보상아이템": set(), "회수아이템": set(), "요구아이템": set(),
        "보상경험치": set(), "보상돈": set(), "조건": defaultdict(set),
        "워프": set(), "대사": [], "정수아닌값": set(),
    })
    for path in sorted((db / "script").rglob("*.txt")):
        if path.name.endswith("_db.txt"):
            continue
        rel = path.relative_to(db).as_posix()
        for sname, body in scripts_in(path):
            for s, svars, snpcs in walk(body):
                # 이 줄이 건드리는 변수 = 조건으로 들어와 있는 것 + 이 줄에서 set 하는 것
                sets = RX["설정"].findall(s)
                other = [(v, e.strip()) for v, e in RX["설정아무거나"].findall(s)
                         if not _is_int(e)]
                # 값이 정수가 아닌 것도 있다: set #EestQuest006B, localtime(3).
                # 단계는 알 수 없지만 변수가 있다는 것은 사실이라 기록한다.
                touched = set(svars) | {v for v, _ in sets} | {v for v, _ in other} \
                    | set(RX["쓰임"].findall(s))
                if not touched:
                    continue
                for v in touched:
                    q = quests[v]
                    q["출처"].add(rel)
                    q["스크립트"].add(sname)
                    q["NPC"] |= snpcs
                    if sname in npc_names:
                        q["NPC"].add(sname)
                    q["단계"] |= svars.get(v, set())
                    for tv, expr in other:
                        if tv == v:
                            q["정수아닌값"].add(expr.strip())
                    for tv, n in sets:
                        if tv == v:
                            q["단계"].add(int(n))
                            for cur in (svars.get(v) or {None}):
                                q["전이"].add((cur, int(n)))
                    for name, cnt in RX["보상"].findall(s):
                        q["보상아이템"].add((name, int(cnt)))
                    for name, cnt in RX["회수"].findall(s):
                        q["회수아이템"].add((name, int(cnt)))
                    for name, cnt in RX["요구"].findall(s):
                        q["요구아이템"].add((name, int(cnt)))
                    for e in RX["경험치"].findall(s):
                        q["보상경험치"].add(e.strip().strip('"'))
                    for g in RX["돈"].findall(s):
                        q["보상돈"].add(g.strip().strip('"'))
                    for key in ("레벨", "직업", "승급"):
                        for op, n in RX[key].findall(s):
                            q["조건"][key].add(f"{op}{n}")
                    q["워프"] |= set(RX["워프"].findall(s))
                    for line in RX["대사"].findall(s):
                        if len(q["대사"]) < 12:
                            q["대사"].append(line)
    return quests


def freeze(quests):
    out = []
    for var, q in sorted(quests.items()):
        if var.isdigit() or len(var) < 2:
            continue
        out.append({
            "변수": var,
            "출처": sorted(q["출처"]),
            "스크립트": sorted(q["스크립트"]),
            "NPC": sorted(q["NPC"]),
            "단계": sorted(q["단계"]),
            "전이": sorted([list(t) for t in q["전이"]], key=lambda x: (x[0] is None, x)),
            "요구아이템": sorted([list(t) for t in q["요구아이템"]]),
            "회수아이템": sorted([list(t) for t in q["회수아이템"]]),
            "보상아이템": sorted([list(t) for t in q["보상아이템"]]),
            "보상경험치": sorted(q["보상경험치"]),
            "보상돈": sorted(q["보상돈"]),
            "조건": {k: sorted(v) for k, v in q["조건"].items() if v},
            "워프": sorted(q["워프"]),
            "정수아닌값": sorted(q["정수아닌값"]),
            "대사": q["대사"],
        })
    return out


def collect_events(db, pack_dir):
    """이벤트 — 자료가 '이벤트'라고 이름 붙인 것만 모은다. 짐작으로 넓히지 않는다."""
    groups = defaultdict(lambda: {"아이템": set(), "출처": set()})
    items = json.loads((OUT_DIR / pack_dir.name / "items.json").read_text(encoding="utf-8"))
    for it in items:
        parts = it["출처"].split("/")
        low = [p.lower() for p in parts]
        if "event" in low:
            g = parts[low.index("event") + 1] if low.index("event") + 1 < len(parts) - 1 else "(묶음없음)"
            groups[g]["아이템"].add(it["이름"])
            groups[g]["출처"].add(it["출처"])
    return [{"묶음": k, "아이템": sorted(v["아이템"]), "출처": sorted(v["출처"])}
            for k, v in sorted(groups.items())]


def main():
    packs = [d for d in sorted(PACKS_DIR.iterdir())
             if d.is_dir() and (d / "db").exists()]
    if not packs:
        sys.exit("db/ 를 가진 팩이 없다")
    for pack in packs:
        db = pack / "db"
        npc_names = {e["이름"] for e in json.loads(
            (OUT_DIR / pack.name / "npcs.json").read_text(encoding="utf-8"))}
        quests = freeze(collect(db, npc_names))
        events = collect_events(db, pack)
        outd = OUT_DIR / pack.name
        outd.mkdir(parents=True, exist_ok=True)
        (outd / "quests.json").write_text(
            json.dumps(quests, ensure_ascii=False, indent=1), encoding="utf-8")
        (outd / "events.json").write_text(
            json.dumps(events, ensure_ascii=False, indent=1), encoding="utf-8")
        staged = [q for q in quests if len(q["단계"]) > 1]
        with_npc = [q for q in quests if q["NPC"]]
        with_reward = [q for q in quests if q["보상아이템"]]
        print(f"══ {pack.name}")
        print(f"   영속변수 {len(quests)}종  (단계가 둘 이상 {len(staged)} · NPC 가 붙은 것 {len(with_npc)}"
              f" · 보상이 붙은 것 {len(with_reward)})")
        print(f"   이벤트 묶음 {len(events)}  아이템 {sum(len(e['아이템']) for e in events)}")
    print(f"\n→ {OUT_DIR.relative_to(ROOT)}/<팩>/quests.json · events.json")


if __name__ == "__main__":
    main()
