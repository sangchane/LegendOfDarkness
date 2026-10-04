"""기술·마법의 이펙트 번호·속도를 Novaonline 팩 값으로 바꾸는 일 — 설명은 build-nova-effects.py 머리에. build-pack-abilities.py·build-monk-skills.py 가 `--쓰기` 끝에 `apply()` 를 부른다."""
import json
import re
import sys

from lib import _pack_abilities
from lib._paths import ROOT

NOVA = ROOT / "data" / "server-packs" / "novaonline"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
PACK599 = HADES / "scripts" / "Pack599"
MONK = HADES / "scripts" / "Skills" / "Monk"
TEMPLATES = HADES / "templates" / "skills"


EFFECT = re.compile(r"(?<![\w/])effect\s+([^;\n]+);")
LINE = re.compile(r'^(?P<pad>\s*)p\.Call\("effect", (?P<who>[^,]+), \(V\)(?P<a>\d+)L, \(V\)(?P<b>\d+)L, '
                  r'\(V\)(?P<s>\d+)L\);(?P<tail>.*)$')
# 노바·5.99 번호가 원작 그림과 어긋나는 것 — 사용자가 준 옛 이펙트 번호 목록(`data/이펙트번호-옛목록.tsv`,
# 2026-09-30)과 아카이브 그림으로 확인한 것만. {이름: {지금 번호: 바른 번호}} 또는 {이름: (쓴쪽, 대상)} 통째로.
# 노바 값을 계산한 뒤에 덮는다.
#  나르콜리: 33 은 「Miss」 글자 그림이다 — 28 이 「z z」 잠.  콘푸지오: 118 은 보라 소용돌이(바투) — 25 가 빨간 소용돌이 「콘푸지오(구)」
#  (사용자 2026-09-30: 머리 위 별(208)은 구버전이 아니다).
#  수페라에나르마·에나르마: 167 구버전이 맞다(사용자 2026-09-30 "에나르마도 167번이 구버전이 맞아").
#  에나르마는 노바가 두 칸 다 195(옛 목록 「카운터」)라 5.99 꼴(쓴쪽 칸 하나)로 통째로 둔다.
#  나머지는 목록대로(사용자 2026-09-30: 노바로 바꾼 뒤 이펙트가 달라져 이 목록으로 되돌리기로) — 찌르기 119 · 숏블레이드 26 ·
#  마구때리기 188 · 무영신공 110(무도가 템플릿 TargetAnimation) · 딜루메니 42 · 연막(괴물) 57.
OLD_LIST_FIX = {"나르콜리": {33: 28}, "콘푸지오": {118: 25}, "수페라에나르마": {271: 167}, "에나르마": (167, 0),
                "찌르기": {26: 119}, "숏블레이드": {166: 26}, "마구때리기": {69: 188}, "무영신공": {273: 110},
                "딜루메니": {276: 42}, "연막": {276: 57},
                # 무도가 발차기: 노바 69(흰 번쩍임) → 27 노란 고리(사용자 2026-09-30 "오리지널은 회색이 아니라 노란색, 레스큐랑 비슷").
                "단각": {69: 27}, "붕각": {69: 27}, "선풍각": {69: 27},
                # 디나르콜리·디소루마도 단각과 같은 노란 고리(사용자 2026-10-04) — 노바 200 대신 27.
                "디나르콜리": {200: 27}, "디소루마": {200: 27}}


# 5.99 대본에 이펙트 줄이 아예 없는데 옛 목록에 그림이 있는 것 — 그 줄 뒤에 한 줄 더한다(이미 있으면 그대로).
# 리젠: 옛 목록 187 노란 반짝이(사용자 2026-09-30 "리젠 아까 구버전 맞는거 같던데").
# 바투: 주황 소용돌이 · 소루마(괴물): 파란 소용돌이(사용자 2026-09-30 "소루마 파란 소용돌이 맞고 바투도 주황 맞아").
OLD_LIST_ADD = {"리젠(Lev1)": ('p.Call("hprecovery"', "v_target", 187, 100),
                "바투": ('p.Call("magic", (V)6L', "v_target", 41, 100),
                "소루마": ('p.Call("mobsor_delay"', "v_myid", 40, 100)}


# 소리(`game_sound`)도 노바로 — 사용자 2026-09-30 「노바로 바꿔봐 내가 플레이해보고 다시 돌릴 수 있게」.
# **되돌리기: 아래를 "5.99" 로 바꾸고 `python3 scripts/gen/ability/build-nova-effects.py --쓰기`.** 대본 줄에는
# `// 노바 소리(5.99: N)` 꼬리표로 원래 번호를 남기고, 무도가 템플릿 `Sound` 는 5.99 블록에서 다시 계산한다.
# 짝짓기: 5.99·노바 블록에 나오는 소리 번호를 나온 차례(겹친 것 빼고)로 i 번째끼리. 노바에 소리가 없으면 그대로,
# 노바에만 있는 갈래(데빌크래셔의 둘레 치기 18)는 더하지 않는다.
SOUND_FROM = "노바"
# 무도가 템플릿 소리를 다른 기술과 같게 — 백보신권은 노바에 없어 단각 소리로(사용자 2026-09-30 "단각이랑 같은 사운드로").
# SOUND_FROM 과 상관없이 늘 건다.
# 선풍각은 붕각과 이펙트·소리가 같고 범위만 다르다(사용자 2026-10-04) — 노바 소리 18 대신 붕각 소리.
SOUND_SAME_AS = {"백보신권": "단각", "선풍각": "붕각"}
SOUND = re.compile(r'^(?P<head>\s*p\.Call\("game_sound", \(V\))(?P<n>\d+)(?P<rest>L, .*?;)(?P<tail>\s*// 노바 소리\(5\.99: (?P<was>\d+)\))?\s*$')
SOUND_IN_PACK = re.compile(r"\b(?:game_sound|sound)\s+(\d+)")


def sound_map(old_body, new_body):
    old = list(dict.fromkeys(int(n) for n in SOUND_IN_PACK.findall(old_body)))
    new = list(dict.fromkeys(int(n) for n in SOUND_IN_PACK.findall(new_body)))
    if not new:
        return {}
    return {was: new[min(i, len(new) - 1)] for i, was in enumerate(old)}


def resound(path, swap):
    """대본의 game_sound 줄을 SOUND_FROM 쪽 번호로. 바뀐 글이나 None."""
    text = path.read_text(encoding="utf-8-sig")
    lines = text.split("\n")
    for i, ln in enumerate(lines):
        m = SOUND.match(ln)
        if not m:
            continue
        was = int(m["was"] or m["n"])
        want = swap.get(was, was) if SOUND_FROM == "노바" else was
        tail = f"  // 노바 소리(5.99: {was})" if want != was else ""
        lines[i] = f'{m["head"]}{want}{m["rest"]}{tail}'
    out = "\n".join(lines)
    return out if out != text else None


def fixed(name, number):
    swap = OLD_LIST_FIX.get(name, {})
    return swap.get(number, number) if isinstance(swap, dict) else number


def fixed_pair(name, a, b):
    swap = OLD_LIST_FIX.get(name)
    return swap if isinstance(swap, tuple) else (fixed(name, a), fixed(name, b))


KEPT = re.compile(r"\s*// 노바 이펙트\(5\.99: (\d+), (\d+)(?:, 속도 (\d+))?\)$")


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


def rewrite(path, old, new):
    """`.cs` 를 새 글로. 바뀐 게 없거나 못 맞추면 `(None, 까닭)`."""
    text = path.read_text(encoding="utf-8-sig")
    lines = text.split("\n")
    at = [i for i, ln in enumerate(lines) if LINE.match(ln)]
    if len(at) != len(old):
        return None, f"effect 줄 {len(at)}개 · 5.99 {len(old)}개 — 맞추지 못함"
    target = [(*fixed_pair(path.stem, a, b), s) for a, b, s in plan(old, new)]
    for n, (i, (a, b, s)) in enumerate(zip(at, target)):
        m = LINE.match(lines[i])
        kept = KEPT.search(m["tail"])
        a0, b0 = (int(kept[1]), int(kept[2])) if kept else (int(m["a"]), int(m["b"]))
        s0 = int(kept[3]) if kept and kept[3] else int(m["s"])  # 옛 꼬리표엔 속도가 없다 — 그땐 줄의 속도가 5.99 값
        s = s0 if s is None else s
        tail = KEPT.sub("", m["tail"])
        mark = f"  // 노바 이펙트(5.99: {a0}, {b0}, 속도 {s0})" if (a, b, s) != (a0, b0, s0) else ""
        lines[i] = f'{m["pad"]}p.Call("effect", {m["who"]}, (V){a}L, (V){b}L, (V){s}L);{tail}{mark}'
    out = "\n".join(lines)
    return (out if out != text else None), None


def swap_effects(old_blocks, new_blocks, writing):
    """이펙트 번호·속도를 노바 것으로 — 무도가는 템플릿, 나머지는 Pack599 스크립트 줄."""
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
            want = fixed(name, next((c[2] for c in new if c[2]), None) or next((c[2] for c in old if c[2]), 0))
            speed = next((c[3] for c in new if c[2]), None)
            if speed is not None:
                monk_speed.append((name, speed))
            old_speed = int(template.get("TargetAnimationSpeed") or 100)
            if template.get("TargetAnimation") != want or speed is not None and old_speed != speed:
                changed.append((name, f"TargetAnimation {template.get('TargetAnimation')} → {want}, 속도 {old_speed} → {speed or old_speed}", path))
                if writing:  # 이 두 칸만 바꾼다 — 머리표(BOM)·줄바꿈·다른 칸은 그대로 둔다
                    raw = path.read_bytes().decode("utf-8")
                    raw, n = re.subn(r'("TargetAnimation":\s*)-?\d+', lambda m: f"{m[1]}{want}", raw, count=1)
                    if not n:
                        raw = re.sub(r'(\n\s*"ScriptName":[^\n]*,)', lambda m: f'{m[1]}\n  "TargetAnimation": {want},', raw, count=1)
                    if speed is not None:
                        raw, n = re.subn(r'("TargetAnimationSpeed":\s*)-?\d+', lambda m: f"{m[1]}{speed}", raw, count=1)
                        if not n:
                            raw = re.sub(r'(\n\s*"TargetAnimation":\s*-?\d+,)',
                                         lambda m: f'{m[1]}\n  "TargetAnimationSpeed": {speed},', raw, count=1)
                    path.write_bytes(raw.encode("utf-8"))
            continue
        path = PACK599 / ("Skills" if kind == "SKILL" else "Spells") / f"{name}.cs"
        if not path.exists() or not old:
            continue
        text, why = rewrite(path, old, new)
        if why:
            skipped.append((name, why))
        elif text is not None:
            before = [(int(m["a"]), int(m["b"]), int(m["s"])) for m in map(LINE.match, path.read_text(encoding="utf-8-sig").split("\n")) if m]
            after = [(int(m["a"]), int(m["b"]), int(m["s"])) for m in map(LINE.match, text.split("\n")) if m]
            changed.append((name, f"{sorted(set(before))} → {sorted(set(after))}", path))
            if writing:
                path.write_text(text, encoding="utf-8-sig")
    return changed, skipped, monk_speed


def swap_sounds(old_blocks, new_blocks, writing, changed):
    """소리 — 노바 짝이 있는 기술·마법만."""
    # 소리 — 노바 짝이 있는 기술·마법만.
    sounds = []
    for (kind, name), body in sorted(old_blocks.items()):
        if kind not in ("SKILL", "SPELL") or (kind, name) not in new_blocks:
            continue
        swap = sound_map(body, new_blocks[(kind, name)])
        template = TEMPLATES / f"{name}.json"
        if kind == "SKILL" and (MONK / f"{name}.cs").exists() and template.exists():
            first = next(iter(swap), None)
            if first is None:
                continue
            want = swap[first] if SOUND_FROM == "노바" else first
            raw = template.read_bytes().decode("utf-8")
            m = re.search(r'"Sound":\s*(\d+)', raw)
            if m and int(m[1]) != want:
                sounds.append((name, f"Sound {m[1]} → {want}", template))
                if writing:
                    template.write_bytes(raw.replace(m[0], f'"Sound": {want}', 1).encode("utf-8"))
            continue
        path = PACK599 / ("Skills" if kind == "SKILL" else "Spells") / f"{name}.cs"
        if path.exists():
            text = resound(path, swap)
            if text is not None:
                sounds.append((name, f"소리 → {SOUND_FROM}", path))
                if writing:
                    path.write_text(text, encoding="utf-8-sig")
    for name, like in SOUND_SAME_AS.items():
        path, source = TEMPLATES / f"{name}.json", TEMPLATES / f"{like}.json"
        if not (path.exists() and source.exists()):
            continue
        want = re.search(r'"Sound":\s*(\d+)', source.read_text(encoding="utf-8-sig"))
        raw = path.read_bytes().decode("utf-8")
        m = re.search(r'"Sound":\s*(\d+)', raw)
        if want and m and m[1] != want[1]:
            sounds.append((name, f"Sound {m[1]} → {want[1]} ({like}와 같게)", path))
            if writing:
                path.write_bytes(raw.replace(m[0], f'"Sound": {want[1]}', 1).encode("utf-8"))
    changed += sounds
    return changed


def apply_old_list(changed, writing):
    """옛 이펙트 번호 목록으로 덮는다(노바 값을 계산한 뒤)."""
    # 노바 짝이 없는 스크립트(괴물 마법·노바에 없는 마법)에도 옛 목록 바로잡기를 건다.
    done = {path for _, _, path in changed}
    for name, swap in OLD_LIST_FIX.items():
        for path in sorted(PACK599.glob(f"*/{name}.cs")):
            if path in done:
                continue
            text = path.read_text(encoding="utf-8-sig")
            lines = text.split("\n")
            for i, ln in enumerate(lines):
                m = LINE.match(ln)
                a, b = fixed_pair(name, int(m["a"]), int(m["b"])) if m else (0, 0)
                if m and (a, b) != (int(m["a"]), int(m["b"])):
                    lines[i] = (f'{m["pad"]}p.Call("effect", {m["who"]}, (V){a}L, (V){b}L, (V){m["s"]}L);{m["tail"]}'
                                f'  // 옛 이펙트 목록({m["a"]}, {m["b"]} → {a}, {b})')
            out = "\n".join(lines)
            if out != text:
                changed.append((name, f"옛 이펙트 목록 {swap}", path))
                if writing:
                    path.write_text(out, encoding="utf-8-sig")

    for name, (after, who, number, speed) in OLD_LIST_ADD.items():
        for path in sorted(PACK599.glob(f"*/{name}.cs")):
            text = path.read_text(encoding="utf-8-sig")
            if any(LINE.match(ln) for ln in text.split("\n")):
                continue
            lines = text.split("\n")
            at = next((i for i, ln in enumerate(lines) if after in ln), None)
            if at is None:
                continue
            pad = lines[at][:len(lines[at]) - len(lines[at].lstrip())]
            lines.insert(at + 1, f'{pad}p.Call("effect", {who}, (V)0L, (V){number}L, (V){speed}L);  // 옛 이펙트 목록(5.99 에 없던 줄)')
            changed.append((name, f"이펙트 줄 더함 {number}", path))
            if writing:
                path.write_text("\n".join(lines), encoding="utf-8-sig")

    # 무도가 템플릿(TargetAnimation)에도 — 노바 짝이 없어 위에서 안 거친 것(무영신공).
    for name in OLD_LIST_FIX:
        path = TEMPLATES / f"{name}.json"
        if not path.exists() or path in done or any(p == path for _, _, p in changed):
            continue
        raw = path.read_bytes().decode("utf-8")
        m = re.search(r'"TargetAnimation":\s*(\d+)', raw)
        if m and fixed(name, int(m[1])) != int(m[1]):
            want = fixed(name, int(m[1]))
            changed.append((name, f"TargetAnimation {m[1]} → {want} (옛 이펙트 목록)", path))
            if writing:
                path.write_bytes(raw.replace(m[0], f'"TargetAnimation": {want}', 1).encode("utf-8"))


def report(changed, writing, skipped, monk_speed):
    """바꿀 것·건너뛴 것·무도가 속도를 알린다."""
    print(f"노바 이펙트로 바꿀 것 {len(changed)}개" + ("" if writing else " (--쓰기 를 붙이면 씁니다)"))
    for name, what, path in changed:
        print(f"  {name:12} {what}")
    for name, why in skipped:
        print(f"  건너뜀 {name}: {why}")
    for name, speed in monk_speed:
        print(f"  무도가 속도 {name}: 노바 {speed}")


def main():
    writing = "--쓰기" in sys.argv or "--write" in sys.argv
    bpa = _pack_abilities
    old_blocks = {k: body for k, (_, body) in bpa.blocks().items()}
    new_blocks = nova_blocks(bpa)

    changed, skipped, monk_speed = swap_effects(old_blocks, new_blocks, writing)

    changed = swap_sounds(old_blocks, new_blocks, writing, changed)

    apply_old_list(changed, writing)

    report(changed, writing, skipped, monk_speed)
    return 0


def apply():
    """다른 생성기가 `--쓰기` 끝에 부른다."""
    saved = sys.argv
    sys.argv = [saved[0], "--쓰기"]
    try:
        return main()
    finally:
        sys.argv = saved
