#!/usr/bin/env python3
"""서버팩의 db/ 를 이어 붙일 수 있는 모양으로 바꾼다.

**매니페스트가 정답지다.** db/ 아래 *_db.txt 는 서버가 무엇을 어떤 갈래로 읽을지
직접 적어 둔 목록이다 (`npc:db/npc/npc.txt`, `spawn:db/npc/시장_spawn.txt`).
파일 이름으로 짐작하면 틀린다 — 같은 spawn 이라도 팩마다 Spawn.txt 이기도 하고
노점_spawn.txt 이기도 하다. 그래서 여기서는 매니페스트가 선언한 갈래만 믿는다.

갈래별 생김새는 네 가지뿐이다.

  {키<TAB>값} 블록   item · mob · npc · spell · skill · maps · door · worldmap
  쉼표로 끊은 줄     warp(출발→도착) · spawn(맵→괴물 또는 맵→NPC) · trap
  머리말 붙은 블록   shop(물건사기<TAB>이름<TAB>{ 추가<TAB>아이템 }) · learn
  스크립트           script — 자체 언어. 본문이 부르는 맵·아이템 이름을 끄집어낸다

간선은 자료 안에 이미 있다. 워프는 맵→맵, 젠은 맵→괴물, 상점은 상점→아이템,
스크립트는 warp "맵" 과 item_add "아이템" 으로 대상을 직접 부른다. 짐작할 것이 없다.

**뜻이 확인된 칸만 이름을 붙인다.** 나머지는 원문 그대로 fields/raw 에 남긴다.
근거 없이 이름을 붙이면 다음 사람이 그것을 근거로 삼는다.

  쓰는 법: python3 scripts/build-server-pack-data.py
"""
import json, re, sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
PACKS_DIR = ROOT / "data" / "server-packs"
OUT_DIR = PACKS_DIR / "extracted"

configure_utf8_stdio(sys.stdout, sys.stderr)

KEYS = ("items", "mobs", "npcs", "spells", "skills", "maps", "worldmaps",
        "doors", "warps", "mob_spawns", "npc_spawns", "shops", "scripts",
        "traps", "learns")
BLOCK_KINDS = {"item", "mob", "npc", "spell", "skill", "maps", "door", "worldmap"}
BUCKET = {"item": "items", "mob": "mobs", "npc": "npcs", "spell": "spells",
          "skill": "skills", "maps": "maps", "worldmap": "worldmaps",
          "door": "doors"}


def read(p):
    return p.read_text(encoding="utf-8", errors="replace")


def content_lines(text):
    """주석(//, ##, /*)과 빈 줄을 걷어낸 줄만 돌려준다."""
    for line in text.splitlines():
        s = line.strip()
        if not s or s.startswith("//") or s.startswith("##") \
           or s.startswith("/*") or s.startswith("*/"):
            continue
        yield line


def parse_blocks(text):
    """{ 키<TAB>값 } 블록. 같은 키가 되풀이되면(말하기 등) 리스트로 모은다."""
    blocks, cur = [], None
    for line in content_lines(text):
        s = line.strip()
        if s.endswith("{"):
            cur = {}
            head = s[:-1].strip()
            if head:
                cur["_머리말"] = head
            continue
        if s == "}":
            if cur is not None:
                blocks.append(cur)
            cur = None
            continue
        if cur is None:
            continue
        parts = s.split("\t")
        key = parts[0].strip()
        vals = [p for p in parts[1:] if p != ""]
        val = vals[0] if len(vals) == 1 else (vals if vals else "")
        if key in cur:
            if not isinstance(cur[key], list):
                cur[key] = [cur[key]]
            cur[key].append(val)
        else:
            cur[key] = val
    return blocks


def parse_csv(text, least):
    rows = []
    for line in content_lines(text):
        cols = [c.strip() for c in line.strip().split(",")]
        if len(cols) >= least:
            rows.append(cols)
    return rows


def parse_manifest(text):
    """`갈래:경로` 목록. 줄 끝에 CR 이 섞여 있어 strip 이 필요하다."""
    out = []
    for line in content_lines(text):
        line = line.strip()
        if line.startswith("#"):
            continue
        if ":" not in line:
            continue
        kind, path = line.split(":", 1)
        kind, path = kind.strip(), path.strip()
        if kind and path and not kind.startswith("{"):
            out.append((kind, path))
    return out


SCRIPT_HEAD = re.compile(r"^(?P<head>[^\t]*)\t(?P<name>[^\t{]+?)\s*\{\s*$")


def parse_scripts(text):
    """머리말<TAB>이름<TAB>{ 본문 }. 중괄호 깊이로 끝을 찾는다."""
    out, lines, i = [], text.splitlines(), 0
    while i < len(lines):
        m = SCRIPT_HEAD.match(lines[i].rstrip())
        if not m:
            i += 1
            continue
        name, head = m.group("name").strip(), m.group("head").strip()
        depth, body, i = 1, [], i + 1
        while i < len(lines) and depth > 0:
            depth += lines[i].count("{") - lines[i].count("}")
            if depth > 0:
                body.append(lines[i])
            i += 1
        out.append({"이름": name, "머리말": head, "본문": "\n".join(body)})
    return out


# 스크립트가 부르는 이름. @name·#name 은 변수지 명령이 아니고, 문자열·주석은 코드가 아니다.
# classify-script-names.py 와 같은 잣대다 — 느슨하게 세면 if(@type1==3) 의 type1 까지 잡힌다.
_N = r'[a-z_][a-z0-9_]{1,30}'
CALL_PAREN = re.compile(rf'(?<![@#$\w]){_N}(?=\s*\()')
CALL_STMT = re.compile(rf'(?:^|[;{{}}])\s*({_N})\s+(?=["@#\d\-])')


def called_names(body):
    code = re.sub(r'"[^"]*"', '""', body)
    code = re.sub(r'//[^\n]*', '', code)
    code = re.sub(r'/\*.*?\*/', '', code, flags=re.S)
    return sorted(set(m.group(0) for m in CALL_PAREN.finditer(code))
                  | set(CALL_STMT.findall(code)))


CALLS = {
    "맵":     re.compile(r'\bwarp\s+"([^"]+)"'),
    "아이템": re.compile(r'\b(?:item_add|item_del|item_one_check)\s*[ (]\s*"([^"]+)"'),
    "괴물":   re.compile(r'\b(?:mob_add|monster|mob_spawn)\s*[ (]\s*"([^"]+)"'),
}


def script_calls(body):
    return {k: sorted(set(rx.findall(body))) for k, rx in CALLS.items()}


def load_pack(db):
    """매니페스트가 선언한 것만 읽는다. 무엇이 무엇을 선언했는지도 남긴다."""
    declared, manifests = {}, {}
    # Path 끼리 비교는 Windows 에서 대소문자를 구분하지 않아 줄 순서가 기계마다 달라진다 — 글자로 정렬한다
    for mf in sorted(db.rglob("*_db.txt"), key=lambda f: f.relative_to(db).as_posix()):
        rel_mf = mf.relative_to(db).as_posix()
        entries = parse_manifest(read(mf))
        manifests[rel_mf] = [{"갈래": k, "경로": p} for k, p in entries]
        for kind, path in entries:
            declared[path.removeprefix("db/")] = (kind, rel_mf)

    out = {k: [] for k in KEYS}
    missing = []

    for rel, (kind, from_mf) in sorted(declared.items()):
        f = db / rel
        if not f.exists():
            missing.append({"경로": rel, "갈래": kind, "선언한곳": from_mf})
            continue
        text, src = read(f), rel

        if kind in BLOCK_KINDS:
            for b in parse_blocks(text):
                name = b.get("이름") or b.get("대상맵")
                if isinstance(name, str) and name:
                    out[BUCKET[kind]].append({"이름": name, "출처": src, "fields": b})

        elif kind == "warp":
            for c in parse_csv(text, 7):
                out["warps"].append({"출발맵": c[1], "출발": [c[2], c[3]],
                                     "도착맵": c[4], "도착": [c[5], c[6]],
                                     "출처": src, "raw": c})

        elif kind == "spawn":
            # 선언한 매니페스트로 갈린다: mob_db → 맵,괴물,수 / npc_db → 맵,x,y,방향,이름
            if "mob" in from_mf:
                for c in parse_csv(text, 3):
                    out["mob_spawns"].append({"맵": c[0], "괴물": c[1],
                                              "마리수": c[2], "출처": src})
            else:
                for c in parse_csv(text, 5):
                    if c[4]:
                        out["npc_spawns"].append({"맵": c[0], "좌표": [c[1], c[2]],
                                                  "NPC": c[4], "출처": src, "raw": c})

        elif kind == "shop":
            for b in parse_blocks(text):
                head = [x for x in b.pop("_머리말", "").split("\t") if x.strip()]
                goods = b.get("추가", [])
                goods = goods if isinstance(goods, list) else [goods]
                out["shops"].append({"이름": head[1] if len(head) > 1 else "",
                                     "갈래": head[0] if head else "",
                                     "아이템": [g for g in goods if isinstance(g, str) and g],
                                     "출처": src})

        elif kind == "script":
            for s in parse_scripts(text):
                out["scripts"].append({"이름": s["이름"], "머리말": s["머리말"],
                                       "줄수": len(s["본문"].splitlines()),
                                       "부름": script_calls(s["본문"]),
                                       "부르는이름": called_names(s["본문"]),
                                       "출처": src})

    # 매니페스트가 가리키지 않는 db 최상위 표 — 혼든의 Learn_db / Trap_db
    for name, key in (("Learn_db.txt", "learns"), ("Trap_db.txt", "traps")):
        f = db / name
        if not f.exists():
            continue
        if key == "learns":
            for b in parse_blocks(read(f)):
                add = b.get("ADD", [])
                out["learns"].append({"ID": b.get("ID"), "TYPE": b.get("TYPE"),
                                      "ADD": add if isinstance(add, list) else [add],
                                      "출처": name})
        else:
            for c in parse_csv(read(f), 5):
                out["traps"].append({"타입": c[0], "맵": c[1], "스크립트": c[2],
                                     "출처": name, "raw": c})

    out["manifests"] = manifests
    out["선언했지만_없는파일"] = missing
    # 디스크에는 있지만 어느 매니페스트도 가리키지 않는 것 = 서버가 읽지 않는 파일.
    # 백업 폴더·크래시 로그·"새 텍스트 문서" 따위다. 버리되 무엇을 버렸는지는 남긴다.
    on_disk = {f.relative_to(db).as_posix() for f in db.rglob("*.txt")}
    out["매니페스트에없는파일"] = sorted(
        on_disk - set(declared) - {f for f in on_disk if f.endswith("_db.txt")})
    return out


def main():
    packs = [d for d in sorted(PACKS_DIR.iterdir())
             if d.is_dir() and (d / "db").exists()]
    if not packs:
        sys.exit("db/ 를 가진 팩이 없다")
    for pack in packs:
        data = load_pack(pack / "db")
        outd = OUT_DIR / pack.name
        outd.mkdir(parents=True, exist_ok=True)
        # 갈래 이름이 바뀌면 옛 파일이 남는다. 다만 지울 것은 **내가 쓴 것만** 이다 —
        # 같은 폴더에 quests.json·script-commands.json 처럼 다른 스크립트의 산출물이 있다.
        owned = set(KEYS) | {"manifests", "선언했지만_없는파일", "매니페스트에없는파일"}
        for stale in outd.glob("*.json"):
            if stale.stem in owned and stale.stem not in data:
                stale.unlink()
        for k, v in data.items():
            (outd / f"{k}.json").write_text(
                json.dumps(v, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"══ {pack.name} ══")
        for k, v in data.items():
            print(f"   {k:22s} {len(v):6d}")
    print(f"\n→ {OUT_DIR.relative_to(ROOT)}/")


if __name__ == "__main__":
    main()
