#!/usr/bin/env python3
"""5.99 서버팩의 기술·마법 스크립트를 하데스에서 그대로 돌게 옮긴다.

5.99 는 기술·마법마다 `SKILL_이름 { … }`·`SPELL_이름 { … }` 블록 하나에 피해식·조건·범위·이펙트·모션·소리를
다 적어 둔다. 모양마다 손으로 옮기지 않고 **문장을 C# 으로 그대로 옮긴다** — `if`·`for`·`goto`·계산식은
스크립트에 적힌 대로 두고, 명령(`damaged`·`effect`·`get_att_damage` …)만 `Pack599.Call` 로 보낸다.
명령의 뜻은 `database/server/scripts/Pack599/Pack599.cs` 한 곳에 있다. 아직 옮기지 않은 명령은 서버를
멈추지 않고 0 을 돌려주므로, 명령을 채우는 만큼 기술이 살아난다.

템플릿은 이미 있으면 붙이는 칸만 바꾸고(기술 `ScriptName` · 마법 `ScriptKey` · 쿨다운), 없으면 만든다 —
직업은 가르치는 NPC 의 `get_class` 검사, 배우는 레벨은 `get_level` 검사에서 읽는다.

무도가 기술은 `build-monk-skills.py` 가 따로 만든 것(`Skills/Monk/`)을 그대로 둔다.

  쓰는 법: python3 scripts/build-pack-abilities.py [--쓰기] [--만 이름 …]

`--만` 을 붙이면 그 이름의 블록만 쓴다. 손본 스크립트(쿠로토·다라밀공의 무도가 몸동작 …)를 되돌리지 않고
한두 개만 새로 옮길 때 쓴다.
"""
import json
import re
import sys
from collections import Counter
from pathlib import Path

from graphify_runtime import configure_utf8_stdio

ROOT = Path(__file__).resolve().parent.parent
PACK = ROOT / "data" / "server-packs" / "5.99-server" / "db"
HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
OUT = HADES / "scripts" / "Pack599"
MONK = HADES / "scripts" / "Skills" / "Monk"
RUNTIME = OUT / "Pack599.cs"

configure_utf8_stdio(sys.stdout, sys.stderr)

MARK = "5.99표"
#: 사용자가 2026-09-16 에 뺐던 정권은 2026-09-25 에 다시 넣으라 했다("5.99 기준으로 완성") — 비어 있다.
EXCLUDED = set()

#: 옮기지 않고 하데스 스크립트를 그대로 붙이는 것. 기본공격은 하데스 `Assail` 이 바로 그것이다(사용자 확인).
ALIASES = {"기본공격": "Assail"}

#: 직업은 파일 이름 → 가르치는 NPC → 모션 순으로 정한다. 모션만으로는 못 정한다 — 파일로 직업이 나오는 130개 중
#: 모션과 맞는 것 67 · 어긋나는 것 6(마법은 여러 직업이 마법사 시전 모션 136 을 같이 쓰고, 2차 기술은 다른 직업
#: 모션을 빌린다) · 모션이 없는 것 57. `Jigja.txt` 는 NPC(성직자 7)와 모션(128·137)이 다 성직자다.
#: `공통스킬.txt` 는 공용이라 직업을 두지 않는다.
FILE_CLASS = [("전사", 1), ("Warrior", 1), ("도적", 2), ("Rogue", 2), ("법사", 3), ("Wizard", 3),
              ("성직자", 4), ("Jigja", 4), ("무도가", 5), ("Monk", 5)]

#: 원작 `skill.tbl` — 모션 번호는 0x80 + NO 이고 NO 의 그림 파일 글자가 직업이다
#: (b 성직자 · c 전사 · d 무도가 · e 도적 · f 마법사, `docs/original-sprite-animation.md` 3.2).
MOTION_CLASS = {0: 4, 9: 4, 10: 4, 1: 1, 2: 1, 11: 1, 12: 1, 13: 1, 3: 5, 4: 5, 5: 5,
                6: 2, 7: 2, 14: 2, 15: 2, 16: 2, 8: 3, 17: 3}

HEADER = re.compile(r"^[\d,]*\s*(SKILL|SPELL|Monster)_([^\s{]+)\s*\{", re.M)


def read(path):
    for encoding in ("utf-8", "cp949"):
        try:
            return path.read_text(encoding=encoding)
        except (UnicodeDecodeError, ValueError):
            continue
    return ""


# ── 읽기 ─────────────────────────────────────────────────────────────────────

def blocks():
    """블록은 괄호 짝으로 자른다. 5.99 원본은 줄 맨 앞에 `}` 를 두기도 하고(퓨리소월루) 닫는 괄호가
    하나 더 있기도 해서(전체크래셔) 줄 모양으로는 못 자른다. 짝이 안 맞으면 다음 블록 머리에서 멈춘다."""
    out = {}
    for path in sorted((PACK / "script" / "Skill").glob("*.txt")) + [PACK / "script" / "Mob_Spell.txt"]:
        text = read(path)
        heads = list(HEADER.finditer(text))
        for n, head in enumerate(heads):
            limit = heads[n + 1].start() if n + 1 < len(heads) else len(text)
            depth, at, quoted = 1, head.end(), False
            while at < limit and depth:
                c = text[at]
                if c == '"' and text[at - 1] != "\\":
                    quoted = not quoted
                elif not quoted and text.startswith("//", at):
                    at = text.find("\n", at)
                    at = limit if at < 0 else at
                    continue
                elif not quoted:
                    depth += c == "{"
                    depth -= c == "}"
                at += 1
            out[(head.group(1), head.group(2))] = (path.name, text[head.end():at - 1 if not depth else at])
    return out


def definitions(path):
    """`Skill.txt`·`spell.txt` 의 `{ 이름 … }` 묶음. 이름 → 칸."""
    out = {}
    for chunk in re.findall(r"\{(.*?)\}", read(path), re.S):
        fields = dict(line.split("\t", 1) for line in chunk.strip().splitlines() if "\t" in line)
        if "이름" in fields:
            out[fields["이름"].strip()] = {k.strip(): v.strip() for k, v in fields.items()}
    return out


def teachers():
    """NPC 가 가르치는 것마다 `(직업, 레벨)`. `skill_add`·`spell_add` 앞의 검사를 읽는다."""
    text = read(PACK / "script" / "Npc" / "Npc_Skill.txt")
    out = {}
    for found in re.finditer(r'(?:skill_add2?|spell_add)\s+"([^"]+)"', text):
        before = text[:found.start()]
        npc = before[max(before.rfind("\n0,0,0"), 0):]
        cls = re.findall(r"get_class\(@myid\)\s*!=\s*(\d+)", npc) or re.findall(r"get_class\(@myid\)\s*==\s*(\d+)", npc)
        branch = before[max(0, found.start() - 900):]
        branch = branch[branch.rfind("if(@select"):] if "if(@select" in branch else branch
        level = re.findall(r"get_level\(@myid\)\s*<\s*(\d+)", branch)
        out.setdefault(found.group(1), (int(cls[-1]) if cls else None, int(level[-1]) if level else None))
    return out


def class_of(name, source, body, taught):
    if "공통" in source:
        return None
    for key, cls in FILE_CLASS:
        if key in source:
            return cls
    if taught.get(name, (None, None))[0]:
        return taught[name][0]
    guessed = {MOTION_CLASS.get(int(m) - 0x80) for m in re.findall(r"\bmotion\s+(\d+)", body)} - {None}
    return guessed.pop() if len(guessed) == 1 else None


# ── 옮기기: 5.99 스크립트 → C# ────────────────────────────────────────────────

TOKEN = re.compile(r"""
    (?P<ws>\s+) | (?P<comment>//[^\n]*) |
    (?P<str>"(?:[^"\\]|\\.)*") | (?P<var>[@$#][\w가-힣]+\$?) | (?P<num>0x[0-9A-Fa-f]+|\d+) |
    (?P<id>[A-Za-z_가-힣][\w가-힣]*) |
    (?P<op>&&|\|\||==|!=|<=|>=|[-+*/%<>!=(){},;:.])
""", re.X)


class Unsupported(Exception):
    pass


def tokens(text):
    out, at = [], 0
    while at < len(text):
        m = TOKEN.match(text, at)
        if not m:
            raise Unsupported(f"읽지 못한 글자 {text[at:at + 20]!r}")
        at = m.end()
        if m.lastgroup in ("ws", "comment"):
            continue
        out.append((m.lastgroup, m.group()))
    return out


class Translator:
    def __init__(self, text):
        self.t = tokens(text)
        self.i = 0
        self.vars = set()
        self.calls = Counter()
        self.labels, self.jumps = set(), set()
        # `if(…){…}else{ go: … }` — C# 은 블록 안쪽 라벨로 뛰어들지 못한다. 그런 라벨은 표시를 켜고
        # `if` 바로 앞으로 가서 곧장 else 로 들어가게 바꾼다.
        self.entries = {self.t[k + 2][1] for k in range(len(self.t) - 3)
                        if self.t[k][1] == "else" and self.t[k + 1][1] == "{" and self.t[k + 3][1] == ":"}

    # 토큰
    def peek(self, k=0):
        return self.t[self.i + k] if self.i + k < len(self.t) else (None, None)

    def take(self, value=None):
        kind, got = self.peek()
        if value is not None and got != value:
            raise Unsupported(f"{value!r} 를 기대했는데 {got!r}")
        self.i += 1
        return got

    def at(self, value):
        return self.peek()[1] == value

    # 문장
    def program(self):
        out = []
        while self.i < len(self.t):
            if self.at("}"):  # 5.99 원본의 남는 닫는 괄호
                self.take()
                continue
            out.append(self.statement(3))
        return "\n".join(line for line in out if line)

    def statement(self, depth):
        pad = "    " * depth
        kind, word = self.peek()
        if word == "{":
            self.take("{")
            inner = []
            while not self.at("}"):
                inner.append(self.statement(depth + 1))
            self.take("}")
            return f"{pad}{{\n" + "\n".join(x for x in inner if x) + f"\n{pad}}}"
        if word == ";":
            self.take()
            return ""
        if kind == "id" and word == "if":
            self.take()
            self.take("(")
            cond = self.expr()
            self.take(")")
            then = self.body(depth)
            entry = None
            if self.at("else") and self.peek(1)[1] == "{" and self.peek(3)[1] == ":":
                entry = self.peek(2)[1]
            if entry:
                cond = f"V.B(!f_{entry} && V.T({cond}))"
            out = f"{pad}if (V.T({cond}))\n{then}"
            if self.at("else"):
                self.take()
                out += f"\n{pad}else\n{self.body(depth)}"
            if entry:
                self.labels.add(entry)
                out = f"{pad}L_{entry}_enter: ;\n{out}"
            return out
        if kind == "id" and word == "for":
            self.take()
            self.take("(")
            first = self.assignment() if self.at("set") else ""
            self.take(";")
            cond = self.expr() if not self.at(";") else "1"
            self.take(";")
            step = self.assignment() if self.at("set") else ""
            self.take(")")
            return f"{pad}for ({first}; V.T({cond}); {step})\n{self.body(depth)}"
        if kind == "id" and word == "while":
            self.take()
            self.take("(")
            cond = self.expr()
            self.take(")")
            return f"{pad}while (V.T({cond}))\n{self.body(depth)}"
        if kind == "id" and word == "switch":
            return self.switch(depth)
        if kind == "id" and word == "set":
            text = self.assignment()
            self.take(";")
            return f"{pad}{text};"
        if kind == "id" and word == "del":
            while not self.at(";"):
                self.take()
            self.take(";")
            return ""
        if kind == "id" and word == "end":
            self.take()
            self.take(";")
            return f"{pad}return;"
        if kind == "id" and word == "break":
            self.take()
            self.take(";")
            return f"{pad}break;"
        if kind == "id" and word == "goto":
            self.take()
            label = self.take()
            self.take(";")
            self.jumps.add(label)
            if label in self.entries:
                return f"{pad}{{ f_{label} = true; goto L_{label}_enter; }}"
            return f"{pad}goto L_{label};"
        if kind == "id" and self.peek(1)[1] == ":":
            self.take()
            self.take(":")
            if word in self.entries:
                return f"{pad}f_{word} = false;"
            self.labels.add(word)
            return f"{pad}L_{word}: ;"
        if kind == "id":
            return pad + self.command() + ";"
        raise Unsupported(f"문장을 읽지 못함 {word!r}")

    def body(self, depth):
        if self.at("{"):
            return self.statement(depth)
        return self.statement(depth + 1)

    def switch(self, depth):
        pad = "    " * depth
        self.take("switch")
        self.take("(")
        value = self.expr()
        self.take(")")
        self.take("{")
        sections = []
        while not self.at("}"):
            if self.at("case"):
                self.take()
                sign = "-" if self.at("-") else ""
                if sign:
                    self.take()
                label = f"case {sign}{self.take()}:"
                self.take(":")
            else:
                self.take("default")
                self.take(":")
                label = "default:"
            inner = []
            while not (self.at("case") or self.at("default") or self.at("}")):
                inner.append(self.statement(depth + 2))
            inner = [x for x in inner if x]
            if not inner or not re.match(r"\s*(break|return|goto)\b", inner[-1]):
                inner.append("    " * (depth + 2) + "break;")
            sections.append(f"{pad}    {label}\n" + "\n".join(inner))
        self.take("}")
        return f"{pad}switch (({value}).Num)\n{pad}{{\n" + "\n".join(sections) + f"\n{pad}}}"

    def assignment(self):
        self.take("set")
        name = self.variable(self.take())
        self.take(",")
        return f"{name} = {self.expr()}"

    def variable(self, raw):
        if raw[0] not in "@$#":
            raise Unsupported(f"변수가 아님 {raw!r}")
        # `$`·`#` 은 캐릭터에 남는 값이다. 아직 남기지 않고 블록 안에서만 쓴다.
        # 끝의 `$` 는 글자 변수라는 표시다(`@msg$`) — 같은 이름의 수 변수와 섞이지 않게 `_s` 를 붙인다.
        name = {"@": "v_", "$": "d_", "#": "h_"}[raw[0]] + re.sub(r"\W", "_", raw[1:].replace("$", "_s"))
        self.vars.add(name)
        return name

    def command(self):
        name = self.take()
        self.calls[name] += 1
        # `name(…);` 은 부르기, `name 인자, 인자;` 는 명령. `name (식) + 1, …;` 같은 것은 명령이다.
        if self.at("("):
            depth, k = 0, 0
            while True:
                word = self.peek(k)[1]
                if word is None:
                    raise Unsupported("괄호가 닫히지 않음")
                depth += word == "("
                depth -= word == ")"
                k += 1
                if depth == 0:
                    break
            if self.peek(k)[1] == ";":
                self.take("(")
                args = self.arguments(")")
                self.take(")")
                return f'p.Call("{name}"{args})'
        args = self.arguments(";")
        return f'p.Call("{name}"{args})'

    def arguments(self, stop):
        out = []
        while not self.at(stop):
            out.append(self.expr())
            if self.at(","):
                self.take()
        return "".join(", " + a for a in out)

    # 식 — 모든 값은 V. 논리는 bool 로 풀었다가 V 로 되돌린다.
    def expr(self):
        return self.logical_or()

    def logical_or(self):
        left = self.logical_and()
        while self.at("||"):
            self.take()
            left = f"V.B(V.T({left}) || V.T({self.logical_and()}))"
        return left

    def logical_and(self):
        left = self.equality()
        while self.at("&&"):
            self.take()
            left = f"V.B(V.T({left}) && V.T({self.equality()}))"
        return left

    def binary(self, next_level, ops):
        left = next_level()
        while self.peek()[1] in ops:
            op = self.take()
            right = next_level()
            left = f"((V)({left}) {op} (V)({right}))"
        return left

    def equality(self):
        return self.binary(self.relational, {"==", "!="})

    def relational(self):
        return self.binary(self.additive, {"<", ">", "<=", ">="})

    def additive(self):
        return self.binary(self.multiplicative, {"+", "-"})

    def multiplicative(self):
        return self.binary(self.unary, {"*", "/", "%"})

    def unary(self):
        if self.at("!"):
            self.take()
            return f"V.B(!V.T({self.unary()}))"
        if self.at("-"):
            self.take()
            return f"(-(V)({self.unary()}))"
        return self.primary()

    def primary(self):
        kind, word = self.peek()
        if word == "(":
            self.take()
            inner = self.expr()
            self.take(")")
            return f"({inner})"
        if kind == "num":
            self.take()
            return f"(V){word}L"
        if kind == "str":
            self.take()
            return f"(V){word}"
        if kind == "var":
            self.take()
            return self.variable(word)
        if kind == "id":
            self.take()
            self.calls[word] += 1
            if self.at("("):
                self.take("(")
                args = self.arguments(")")
                self.take(")")
                return f'p.Call("{word}"{args})'
            return f'p.Call("{word}")'
        raise Unsupported(f"식을 읽지 못함 {word!r}")


def translate(body):
    t = Translator(body)
    code = t.program()
    # 없는 라벨로 뛰는 곳이 있다(아무네지아 `go4`). 스크립트 끝으로 보낸다.
    for label in sorted(t.jumps - t.labels - t.entries):
        code += f"\n            L_{label}: ;"
    flags = [f"f_{label}" for label in sorted(t.entries & t.jumps)]
    return code, sorted(t.vars), t.calls, flags


# ── 쓰기 ─────────────────────────────────────────────────────────────────────

def klass(kind, name):
    return {"SKILL": "Skill", "SPELL": "Spell", "Monster": "Monster"}[kind] + "".join(f"{ord(c):04X}" for c in name)


def monster_spells():
    """5.99 괴물 정의의 `스킬 Monster_이름 N` — 괴물 이름 → 괴물 마법 이름."""
    out = {}
    for path in (PACK / "mob").rglob("*.txt"):
        for chunk in re.findall(r"\{(.*?)\}", read(path), re.S):
            fields = dict(line.split("\t", 1) for line in chunk.strip().splitlines() if "\t" in line)
            if "이름" in fields and "스킬" in fields:
                out[fields["이름"].strip()] = fields["스킬"].split("\t")[0].strip()
    return out


def csharp(kind, name, source, code, variables, flags):
    declare = "".join(f"            V {v} = 0;\n" for v in variables)
    declare += "".join(f"            bool {f} = false;\n" for f in flags)
    where = klass(kind, name)
    header = f"""using Darkages.Scripting;
using Darkages.Types;

namespace Darkages.Storage.locales.Scripts.Pack599
{{
    /// <summary>
    /// {name} — 5.99 `{source}` 의 {kind}_{name} 을 그대로 옮긴 것.
    /// </summary>
    /// <remarks>
    /// 손으로 고치지 말 것. `scripts/build-pack-abilities.py` 가 다시 만든다.
    /// </remarks>
    [Script("{'Monster_' + name if kind == 'Monster' else name}", "{MARK}")]
"""
    if kind == "Monster":
        return header + f"""    public class {where} : SpellScript
    {{
        public {where}(Spell spell) : base(spell)
        {{
        }}

        public override void OnFailed(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnSuccess(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnUse(Sprite sprite, Sprite target)
        {{
            var p = Pack599.ForMonster(sprite, target);
            if (!p.Ready)
                return;
{declare}
{code}
        }}
    }}
}}
"""
    if kind == "SKILL":
        return header + f"""    public class {where} : SkillScript
    {{
        public {where}(Skill skill) : base(skill)
        {{
        }}

        public override void OnFailed(Sprite sprite)
        {{
        }}

        public override void OnSuccess(Sprite sprite)
        {{
        }}

        public override void OnUse(Sprite sprite)
        {{
            if (!Skill.Ready)
                return;

            var p = new Pack599(sprite, null);
            if (!p.Ready)
                return;

            p.Train(Skill);
{declare}
{code}
        }}
    }}
}}
"""
    return header + f"""    public class {where} : SpellScript
    {{
        public {where}(Spell spell) : base(spell)
        {{
        }}

        public override void OnFailed(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnSuccess(Sprite sprite, Sprite target)
        {{
        }}

        public override void OnUse(Sprite sprite, Sprite target)
        {{
            var p = new Pack599(sprite, target);
            if (!p.Ready)
                return;
{declare}
{code}
        }}
    }}
}}
"""


def implemented():
    return set(re.findall(r'case "([^"]+)":', read(RUNTIME)))


def main():
    writing = "--쓰기" in sys.argv or "--write" in sys.argv
    only = set(sys.argv[sys.argv.index("--만") + 1:]) if "--만" in sys.argv else None
    found = blocks()
    if only is not None:
        found = {key: value for key, value in found.items() if key[1] in only}
    skills, spells = definitions(PACK / "skill" / "Skill.txt"), definitions(PACK / "spell" / "spell.txt")
    taught = teachers()
    known = implemented()

    made, failed, missing, aliased = [], [], Counter(), []
    for (kind, name), (source, body) in sorted(found.items()):
        if name in EXCLUDED or (kind == "SKILL" and (MONK / f"{name}.cs").exists()):
            continue
        if name in ALIASES:
            aliased.append((kind, name))
            continue
        try:
            code, variables, calls, flags = translate(body)
        except Unsupported as error:
            failed.append((kind, name, str(error)))
            continue
        lacking = sorted(set(calls) - known)
        for call in lacking:
            missing[call] += 1
        delay = re.search(r"\bskill_delay\s+(\d+)", body)
        made.append((kind, name, source, code, variables, flags, lacking, int(delay.group(1)) if delay else 0,
                     class_of(name, source, body, taught)))

    whole = [m for m in made if not m[6]]
    print(f"5.99 블록 {len(found)} · 옮김 {len(made)} (명령이 다 있는 것 {len(whole)}) · 못 옮김 {len(failed)}")
    for kind, name, error in failed:
        print(f"  못 옮김 {kind} {name}: {error}")
    print("아직 없는 명령(쓰는 블록 수): " + ", ".join(f"{k}({v})" for k, v in missing.most_common()))

    if not writing:
        print("\n--쓰기 를 붙이면 실제로 만듭니다.")
        return 0

    for kind, name, source, code, variables, flags, lacking, delay, cls in made:
        folder = OUT / {"SKILL": "Skills", "SPELL": "Spells", "Monster": "Monsters"}[kind]
        folder.mkdir(parents=True, exist_ok=True)
        (folder / f"{name}.cs").write_text(csharp(kind, name, source, code, variables, flags), encoding="utf-8-sig")
        if kind == "Monster":
            # 하데스 괴물 AI 는 같은 이름의 마법 템플릿이 있어야 스크립트를 불러온다(`CommonMonster.cs:292`) —
            # 없으면 말없이 건너뛴다. 가르치는 NPC 가 없으니 사람이 배울 길은 없다.
            path = HADES / "templates" / "spells" / f"Monster_{name}.json"
            path.write_text(json.dumps({"Name": f"Monster_{name}", "ScriptKey": f"Monster_{name}", "Prerequisites": {},
                                        "MaxLevel": 100, "ID": 0, "Description": None, "TargetType": 2,
                                        "Group": f"{MARK}/괴물마법"}, ensure_ascii=False, indent=2), encoding="utf-8-sig")
            continue

        define = (skills if kind == "SKILL" else spells).get(name, {})
        level = taught.get(name, (None, None))[1]
        path = HADES / "templates" / ("skills" if kind == "SKILL" else "spells") / f"{name}.json"
        if path.exists():
            template = json.loads(path.read_text(encoding="utf-8-sig"))
        else:
            template = {"Name": name, "Prerequisites": {}, "MaxLevel": 100, "ID": 0,
                        "Description": define.get("설명"), "Group": f"{MARK}/{source[:-4]}"}
            if cls:
                template["Prerequisites"]["Class_Required"] = cls
            if level:
                template["Prerequisites"]["ExpLevel_Required"] = level
            if define.get("이미지", "").isdigit():
                template["Icon"] = int(define["이미지"])
            if kind == "SPELL" and define.get("타입", "").isdigit():
                template["TargetType"] = int(define["타입"])
        # 이 생성기가 만든 템플릿은 직업을 다시 정한다(원작 템플릿은 건드리지 않는다).
        if str(template.get("Group", "")).startswith(MARK):
            template.setdefault("Prerequisites", {}).pop("Class_Required", None)
            if cls:
                template["Prerequisites"]["Class_Required"] = cls
        template["ScriptName" if kind == "SKILL" else "ScriptKey"] = name
        template["Cooldown"] = delay
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")
    for kind, name in aliased:
        folder = "skills" if kind == "SKILL" else "spells"
        (OUT / folder.capitalize() / f"{name}.cs").unlink(missing_ok=True)
        path = HADES / "templates" / folder / f"{name}.json"
        template = json.loads(path.read_text(encoding="utf-8-sig"))
        template["ScriptName" if kind == "SKILL" else "ScriptKey"] = ALIASES[name]
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")
    # 괴물 마법을 괴물 템플릿에 붙인다. 하데스 `CommonMonster` 가 `SpellScripts` 의 것을 표적에게 쓴다.
    defined = {name for kind, name, *_ in made if kind == "Monster"}
    wanted = monster_spells()
    attached, undefined = 0, Counter()
    for path in (HADES / "templates" / "monsters" / "5.99").glob("*.json"):
        template = json.loads(path.read_text(encoding="utf-8-sig"))
        spell = wanted.get(template.get("BaseName") or template.get("Name"))
        if not spell:
            continue
        if spell[len("Monster_"):] not in defined:
            undefined[spell] += 1
            continue
        template["SpellScripts"] = [spell]
        path.write_text(json.dumps(template, ensure_ascii=False, indent=2), encoding="utf-8-sig")
        attached += 1
    print(f"\n스크립트·템플릿 {len(made)}쌍을 만들었습니다. 하데스 스크립트를 붙인 것 {len(aliased)}개.")
    print(f"괴물 템플릿 {attached}장에 괴물 마법을 붙였습니다.")
    if undefined:
        print("5.99 에 정의가 없는 괴물 마법(템플릿 수): " + ", ".join(f"{k}({v})" for k, v in undefined.most_common()))
    run_nova_effects()
    return 0


def run_nova_effects():
    """이펙트 번호는 노바 것이 원작이다(사용자 결정 2026-09-26) — 옮긴 뒤 `build-nova-effects.py` 로 바꾼다."""
    import importlib.util
    spec = importlib.util.spec_from_file_location("build_nova_effects", Path(__file__).with_name("build-nova-effects.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.apply()


if __name__ == "__main__":
    raise SystemExit(main())
