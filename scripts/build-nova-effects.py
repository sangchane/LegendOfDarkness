#!/usr/bin/env python3
"""기술·마법의 이펙트(그림) 번호를 Novaonline 팩 값으로 바꾼다.

사용자 결정(2026-09-26): **"노바 것이 원작 이펙트다."** 5.99 팩과 노바 팩에 같은 이름으로 있는 기술·마법은
이펙트 번호를 노바 스크립트의 것으로 쓴다. 팩 3개가 일치할 때만 쓴다는 규칙(AGENTS.md)의 예외다.
이펙트 번호와 **이펙트 속도**(`effect` 의 넷째 인자)를 바꾼다 — 동작(motion)·소리(sound)·피해식·스크립트 짜임은
5.99 그대로다. 속도는 사용자 결정(2026-09-27): 「이펙트 속도도 노바 값으로」(예: 데빌크래셔 5.99 130 → 노바 70).

바꾸는 곳은 두 갈래다.

- `scripts/Pack599/Skills|Spells/<이름>.cs` (`build-pack-abilities.py` 가 5.99 문장을 옮긴 것) —
  `p.Call("effect", 대상, 쓴쪽그림, 대상그림, 속도)` 줄. 5.99 블록의 i 번째 `effect` 에 노바 블록의 i 번째
  (노바가 더 짧으면 마지막) 것을 짝지어 **두 그림 칸을 통째로** 옮기고, 속도는 그 짝의 속도로 바꾼다(노바 속도가
  숫자가 아니면 5.99 그대로). 짝의 대상(`@target`·`@myid` …)이
  다르면 통째로 옮기지 않고 번호끼리만 바꾼다(5.99 번호 → 같은 자리 노바 번호). **줄을 더하지 않는다** —
  노바에만 있는 `effect` 는 노바에만 있는 갈래다: 데프레코·렌토·바르도·프라보·어둠의각인의 33 은 노바가 새로 둔
  「몬스터가 마법을 피했다」(40%) 쪽 빗나감 그림이고, 데빌크래셔의 131 은 노바가 둘레 네 칸까지 치게 바꾼 쪽이다.
  그 갈래(확률·범위)는 동작이지 이펙트가 아니라 옮기지 않는다.
- `templates/skills/<이름>.json` 의 `TargetAnimation` — `Skills/Monk/<이름>.cs`(`build-monk-skills.py`)는
  이펙트를 템플릿에서 읽는다. 노바 첫 `effect` 의 대상그림을 넣고, **노바에 이펙트가 없으면 5.99 번호를 둔다**
  (0 으로 지우지 않는다 — 사용자 2026-09-27: 허공답보는 5.99 의 68 이 원작).
  템플릿에는 속도 칸이 없다 — `MonkStrike` 가 속도를 100 으로 박아 보낸다(`ServerFormat29(..., 100)`). 노바 속도는
  `--쓰기` 없이 돌리면 「무도가 속도」 줄로 보여 준다.

다시 돌려도 같다: 바꾼 줄 끝에 `// 노바 이펙트(5.99: 쓴쪽, 대상, 속도 N)` 로 원래 값을 남겨 두고 그것에서 다시
계산하며(속도가 없는 옛 꼬리표는 줄의 속도가 곧 5.99 값이다),
`build-pack-abilities.py`·`build-monk-skills.py`
가 `--쓰기` 끝에 이것을 부르므로 그쪽을 다시 돌려도 5.99 번호로 되돌아가지 않는다.

노바 팩이 읽는 파일은 `db/script/script_db.txt` 에 적힌 순서다(같은 이름이 두 번 있으면 먼저 것).

  쓰는 법: python3 scripts/build-nova-effects.py [--쓰기]
"""
import importlib.util
import json
import re
import sys
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
NOVA = ROOT / "data" / "server-packs" / "novaonline"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
PACK599 = HADES / "scripts" / "Pack599"
MONK = HADES / "scripts" / "Skills" / "Monk"
TEMPLATES = HADES / "templates" / "skills"

configure_utf8_stdio(sys.stdout, sys.stderr)

EFFECT = re.compile(r"(?<![\w/])effect\s+([^;\n]+);")
LINE = re.compile(r'^(?P<pad>\s*)p\.Call\("effect", (?P<who>[^,]+), \(V\)(?P<a>\d+)L, \(V\)(?P<b>\d+)L, '
                  r'\(V\)(?P<s>\d+)L\);(?P<tail>.*)$')
#: 속도를 노바로 바꾸지 않는 것 — 사람이 정한 값이다. 쿠로토 117: 5.99·노바는 75 인데 무도가 손 들기에 맞춰
#: 「20%씩 두 번 느리게」 한 것(`KurotoTests`, 몸 동작 90 = 117 / 1.3 과 짝).
KEEP_SPEED = {"쿠로토"}
KEPT = re.compile(r"\s*// 노바 이펙트\(5\.99: (\d+), (\d+)(?:, 속도 (\d+))?\)$")


def _pack_abilities():
    spec = importlib.util.spec_from_file_location("build_pack_abilities", Path(__file__).with_name("build-pack-abilities.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def calls(body):
    """블록 안 `effect` 들 — `(대상 변수, 쓴쪽그림, 대상그림, 속도)`. 번호가 아닌 인자는 None."""
    out = []
    for args in EFFECT.findall(re.sub(r"//[^\n]*", "", body)):
        parts = [p.strip() for p in args.split(",")]
        if len(parts) < 3:
            continue
        nums = [int(p) if p.isdigit() else None for p in parts[1:4]] + [None] * (4 - len(parts))
        out.append((parts[0], *nums[:3]))
    return out


def nova_blocks(bpa):
    listing = (NOVA / "db" / "script" / "script_db.txt").read_text(encoding="utf-8", errors="replace")
    out = {}
    for rel in re.findall(r"^script:(\S.*?)\s*$", listing, re.M):
        path = NOVA / rel
        if not path.exists():
            continue
        text = bpa.read(path)
        heads = list(bpa.HEADER.finditer(text))
        for n, head in enumerate(heads):
            limit = heads[n + 1].start() if n + 1 < len(heads) else len(text)
            out.setdefault((head.group(1), head.group(2)), text[head.end():limit])
    return out


def plan(old, new):
    """5.99 `effect` 마다 바꿀 `(쓴쪽, 대상, 노바 속도 또는 None)`."""
    numbers = {}
    for k, (who, a, b, _) in enumerate(old):
        if not new:
            break
        pair = new[min(k, len(new) - 1)]
        for was, now in ((a, pair[1]), (b, pair[2])):
            if was and now:
                numbers.setdefault(was, now)
    out = []
    for k, (who, a, b, _) in enumerate(old):
        if not new:
            out.append((a, b, None))  # 노바에 이펙트가 없는 스크립트 기술은 없다. 있어도 지우지 않는다.
            continue
        pair = new[min(k, len(new) - 1)]
        if pair[0] == who and pair[1] is not None and pair[2] is not None:
            out.append((pair[1], pair[2], pair[3]))
        else:
            out.append((numbers.get(a, a) if a else a, numbers.get(b, b) if b else b, pair[3]))
    return out


def rewrite(path, old, new, keep_speed=False):
    """`.cs` 를 새 글로. 바뀐 게 없거나 못 맞추면 `(None, 까닭)`."""
    text = path.read_text(encoding="utf-8-sig")
    lines = text.split("\n")
    at = [i for i, ln in enumerate(lines) if LINE.match(ln)]
    if len(at) != len(old):
        return None, f"effect 줄 {len(at)}개 · 5.99 {len(old)}개 — 맞추지 못함"
    target = plan(old, new)
    for n, (i, (a, b, s)) in enumerate(zip(at, target)):
        m = LINE.match(lines[i])
        kept = KEPT.search(m["tail"])
        a0, b0 = (int(kept[1]), int(kept[2])) if kept else (int(m["a"]), int(m["b"]))
        s0 = int(kept[3]) if kept and kept[3] else int(m["s"])  # 옛 꼬리표엔 속도가 없다 — 그땐 줄의 속도가 5.99 값
        s = s0 if s is None or keep_speed else s
        tail = KEPT.sub("", m["tail"])
        mark = f"  // 노바 이펙트(5.99: {a0}, {b0}, 속도 {s0})" if (a, b, s) != (a0, b0, s0) else ""
        lines[i] = f'{m["pad"]}p.Call("effect", {m["who"]}, (V){a}L, (V){b}L, (V){s}L);{tail}{mark}'
    out = "\n".join(lines)
    return (out if out != text else None), None


def main():
    writing = "--쓰기" in sys.argv or "--write" in sys.argv
    bpa = _pack_abilities()
    old_blocks = {k: body for k, (_, body) in bpa.blocks().items()}
    new_blocks = nova_blocks(bpa)

    changed, skipped, monk_speed = [], [], []
    for (kind, name), body in sorted(old_blocks.items()):
        if kind not in ("SKILL", "SPELL") or (kind, name) not in new_blocks:
            continue
        old, new = calls(body), calls(new_blocks[(kind, name)])
        if kind == "SKILL" and (MONK / f"{name}.cs").exists():
            path = TEMPLATES / f"{name}.json"
            if not path.exists():
                continue
            template = json.loads(path.read_text(encoding="utf-8-sig"))
            # 노바 스크립트에 이펙트가 없으면 5.99 번호를 둔다 — 0 으로 지우지 않는다(사용자 2026-09-27: 허공답보 68 이 원작).
            want = next((c[2] for c in new if c[2]), None) or next((c[2] for c in old if c[2]), 0)
            speed = next((c[3] for c in new if c[2]), None)
            if speed is not None and speed != 100:  # 템플릿에 속도 칸이 없다 — MonkStrike 가 100 을 보낸다
                monk_speed.append((name, speed))
            if template.get("TargetAnimation") != want:
                changed.append((name, f"TargetAnimation {template.get('TargetAnimation')} → {want}", path))
                if writing:  # 그 칸만 바꾼다 — 머리표(BOM)·줄바꿈·다른 칸은 그대로 둔다
                    raw = path.read_bytes().decode("utf-8")
                    raw, n = re.subn(r'("TargetAnimation":\s*)-?\d+', lambda m: f"{m[1]}{want}", raw, count=1)
                    if not n:
                        raw = re.sub(r'(\n\s*"ScriptName":[^\n]*,)', lambda m: f'{m[1]}\n  "TargetAnimation": {want},', raw, count=1)
                    path.write_bytes(raw.encode("utf-8"))
            continue
        path = PACK599 / ("Skills" if kind == "SKILL" else "Spells") / f"{name}.cs"
        if not path.exists() or not old:
            continue
        text, why = rewrite(path, old, new, keep_speed=name in KEEP_SPEED)
        if why:
            skipped.append((name, why))
        elif text is not None:
            before = [(int(m["a"]), int(m["b"]), int(m["s"])) for m in map(LINE.match, path.read_text(encoding="utf-8-sig").split("\n")) if m]
            after = [(int(m["a"]), int(m["b"]), int(m["s"])) for m in map(LINE.match, text.split("\n")) if m]
            changed.append((name, f"{sorted(set(before))} → {sorted(set(after))}", path))
            if writing:
                path.write_text(text, encoding="utf-8-sig")

    print(f"노바 이펙트로 바꿀 것 {len(changed)}개" + ("" if writing else " (--쓰기 를 붙이면 씁니다)"))
    for name, what, path in changed:
        print(f"  {name:12} {what}")
    for name, why in skipped:
        print(f"  건너뜀 {name}: {why}")
    for name, speed in monk_speed:
        print(f"  무도가 속도 {name}: 노바 {speed} · 우리 100(MonkStrike 고정, 템플릿에 칸 없음)")
    return 0


def apply():
    """다른 생성기가 `--쓰기` 끝에 부른다."""
    saved = sys.argv
    sys.argv = [saved[0], "--쓰기"]
    try:
        return main()
    finally:
        sys.argv = saved


if __name__ == "__main__":
    raise SystemExit(main())
