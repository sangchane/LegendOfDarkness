"""기술·마법 스크립트가 보내는 연출(이펙트·소리·몸동작) 읽기 — build-ability-page-data.py 와 build-ability-operations-data.py 가 같이 쓴다."""
import re

from lib._paths import ROOT

SCRIPTS = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/server/scripts"


def scripted():
    out = {}
    for f in SCRIPTS.rglob("*.cs"):
        text = f.read_text(encoding="utf-8", errors="replace")
        for name in re.findall(r'\[Script\("([^"]+)"', text):
            out.setdefault(name, []).append(text)
    return out


# 서버가 클라이언트에 연출을 보내는 길. 이펙트는 0x29, 소리는 0x13/0x19, 몸동작은 0x1A 다
# (docs/martial-artist-skill-presentation.md 1절). 5.99 팩 스크립트는 `Pack599.Call` 을 거친다.
PACK_CALL = re.compile(r'Call\("(effect|game_sound|motion|group_hill|god_bless|group_mobsor_end|group_mobnar_end'
                       r'|hprecovery)"\s*,(.*?)\);', re.S)
#: 파티에 그림을 거는 명령은 그림 번호가 다른 자리에 있다 — `group_hill 회복량, 그림` · `god_bless 그림, 초` ·
#: `group_mob*_end 그림`(Pack599.cs 가 0x29 로 보낸다).
PARTY_PICTURE = {"group_hill": 1, "god_bless": 0, "group_mobsor_end": 0, "group_mobnar_end": 0}
#: `hprecovery` 는 스크립트에 그림이 없다 — 5.99 서버가 1초마다 그림 22 를 보낸다(Novaonline.exe 0x46e120).
REGEN_PICTURE = 22
#: 16진수로 적힌 것도 있다(beag ioc fein 의 `SendAnimation(0x04, …)`).
SEND_ANIMATION = re.compile(r'SendAnimation\((0x[0-9A-Fa-f]+|\d+)')
FORMAT_19 = re.compile(r'ServerFormat19\s*\{\s*Number\s*=\s*\(?[a-z]*\)?\s*(\d+)')
FORMAT_1A = re.compile(r'ServerFormat1A\s*\{[^}]*?Number\s*=\s*(?:\(byte\)\s*)?(0x[0-9A-Fa-f]+|\d+)', re.S)
#: 무도가 기술은 도우미를 거쳐 나간다 — 16진수로 적힌 인자가 몸동작 번호다(`MonkStrike.Afflict(…, 0x84)`).
MONK_CALL = re.compile(r'MonkStrike\.\w+\((.*?)\);', re.S)
HEX_OR_INT = re.compile(r'^\s*(?:\(byte\)\s*)?(0x[0-9A-Fa-f]+|\d+)\s*$')


def split_args(text):
    """맨 바깥 쉼표로만 가른다. 인자가 또 괄호를 품고 있어 `split(",")` 로는 안 된다."""
    out, depth, current = [], 0, ""
    for ch in text:
        if ch in "([":
            depth += 1
        elif ch in ")]":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(current.strip())
            current = ""
        else:
            current += ch
    if current.strip():
        out.append(current.strip())
    return out


def literal(arg):
    """`(V)257L` · `0x84` · `35` 처럼 **그 자리에 박힌 수**. 변수면 None."""
    inner = re.fullmatch(r'\(V\)(\d+)L', arg.strip())
    if inner:
        return int(inner.group(1))
    found = HEX_OR_INT.match(arg)
    return int(found.group(1), 0) if found else None


def sent_by(bodies, template):
    """이 기술을 쓰면 **게임이 실제로 무엇을 보내나** — 채널마다 번호 목록으로.

    화면이 세던 「연출」은 노바온라인 팩의 표를 한글 이름으로 찾은 것이라 우리 서버가 보내는 것과
    다르다. 사제 마법이 특히 딴판이다 — 프라보는 팩 표가 43·33 인데 서버는 **257** 을 쏘고,
    쿠로는 21 이 아니라 **267**, 데프레코는 18·33 이 아니라 **243** 이다(사용자, 2026-09-19).
    일음지는 팩 표 42 · 서버 276 이고, **발경은 `TargetAnimation` 이 0 이라 이펙트가 안 나간다.**

    보내는 길은 둘이다: 템플릿의 `TargetAnimation`·`Sound` 를 하데스가 쏘거나, 5.99 팩 스크립트가
    `effect`·`game_sound`·`motion` 으로 직접 쏘거나. **팩의 `effect` 는 `@대상, 쓴쪽그림, 대상그림,
    속도`** 라 둘째·셋째만 그림이다 — 넷째까지 그림으로 세면 프라보의 속도 140 이 이펙트로 둔갑한다.
    """
    out = {"이펙트": [], "소리": [], "몸동작": []}

    def add(channel, number):
        if number and number > 0 and number not in out[channel]:
            out[channel].append(number)

    for body in bodies:
        for command, raw in PACK_CALL.findall(body):
            args = split_args(raw)
            if command == "effect":
                for at in (1, 2):
                    if at < len(args):
                        add("이펙트", literal(args[at]))
            elif command in PARTY_PICTURE:
                at = PARTY_PICTURE[command]
                if at < len(args):
                    add("이펙트", literal(args[at]))
            elif command == "hprecovery":
                add("이펙트", REGEN_PICTURE)
            elif command == "game_sound" and args:
                add("소리", literal(args[0]))
            elif command == "motion" and args:
                add("몸동작", literal(args[0]))
        for number in SEND_ANIMATION.findall(body):
            add("이펙트", int(number, 0))
        for number in FORMAT_19.findall(body):
            add("소리", int(number))
        for number in FORMAT_1A.findall(body):
            add("몸동작", int(number, 0))
        for raw in MONK_CALL.findall(body):
            for arg in split_args(raw):
                # 몸동작만 16진수로 적혀 있다(`0x84`). 나머지 인자는 배율·초 같은 10진수다.
                if arg.strip().lower().startswith("0x"):
                    add("몸동작", literal(arg))

    add("이펙트", template.get("TargetAnimation") or 0)
    # 하데스 옛 마법 스크립트는 `Spell.Template.Animation` 을 대상에게 쏜다(ao cradh · armachd · deo saighead …).
    if template.get("갈래") == "spells":
        add("이펙트", template.get("Animation") or 0)
    add("소리", template.get("Sound") or 0)
    return out


ANY_CALL = re.compile(r'Call\("([a-z_]+)"')
#: 아군에게 거는 명령 — 체력을 채우거나 파티에 걸거나 잠·독을 푼다. 괴물을 재는 명령이 같이 있으면 적에게 거는 것이다.
ALLY_CALLS = {"set_vita", "group_hill", "hprecovery", "god_bless", "mobnar_end", "mobsor_end",
              "group_mobsor_end", "group_mobnar_end"}
FOE_CALLS = {"get_mobdie", "damaged"}


def effect_sides(bodies, template):
    """그림이 **누구 위에** 뜨나 — 화면이 쓴 사람·맞는 쪽 위에 그림을 따로 그리게.

    5.99 `effect @대상, 쓴쪽그림, 대상그림, 속도`(Pack599.cs 「case "effect"」) — `@get_myid` 면 대상이 쓴 사람
    자신이라 두 그림이 다 쓴 사람 위다. 하데스 `SendAnimation(그림, 맞는쪽, 쓴쪽)` 과 템플릿 `TargetAnimation` 은 맞는 쪽.
    """
    caster, target, aims, calls = [], [], set(), set()

    def add(where, number):
        if number and number > 0 and number not in where:
            where.append(number)

    for body in bodies:
        calls.update(ANY_CALL.findall(body))
        for command, raw in PACK_CALL.findall(body):
            args = split_args(raw)
            if command == "effect" and args:
                aim = args[0].strip()
                own = "myid" in aim
                aims.add("self" if own else "many" if aim.startswith("v_mob") else "one")
                if len(args) > 1:
                    add(caster, literal(args[1]))
                if len(args) > 2:
                    add(caster if own else target, literal(args[2]))
            elif command in PARTY_PICTURE:
                aims.add("party")
                at = PARTY_PICTURE[command]
                if at < len(args):
                    add(target, literal(args[at]))
            elif command == "hprecovery":
                aims.add("party")
                add(target, REGEN_PICTURE)
        for number in SEND_ANIMATION.findall(body):
            aims.add("one")
            add(target, int(number, 0))
    for number in (template.get("TargetAnimation"),
                   template.get("Animation") if template.get("갈래") == "spells" else None):
        if number:
            aims.add("one")
            add(target, number)

    skill = template.get("갈래") == "skills"
    foe = bool(calls & FOE_CALLS)
    ally = bool(calls & ALLY_CALLS) and not foe
    if "party" in aims:
        who = "파티 모두"
    elif "many" in aims:
        who = "주변 적 여럿"
    elif "one" in aims:
        # 적·아군을 가르는 명령이 없으면(벨라르모·리베라토 …) 짐작하지 않는다.
        who = "앞의 적" if skill else "고른 아군" if ally else "고른 적" if foe else "고른 대상"
    elif "self" in aims or template.get("TargetType") == 5:
        who = "자기 자신"
    else:
        who = "앞의 적" if skill else "고른 대상"
    return {"대상": who, "쓴쪽": caster, "맞는쪽": target}
