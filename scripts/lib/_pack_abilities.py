"""5.99 서버팩 기술·마법 스크립트를 하데스 C# 으로 옮기는 번역기와 블록 읽기 — build-pack-abilities.py · build-pack-npcs.py · lib/_nova_effects.py 가 같이 쓴다."""
import re
from collections import Counter

from lib._paths import ROOT
from lib._io import read_source as read

PACK = ROOT / "data" / "server-packs" / "5.99-server" / "db"


HADES = ROOT / "sources" / "wren11" / "Dark-Ages-Private-Server" / "database" / "server"
OUT = HADES / "scripts" / "Pack599"


MARK = "5.99표"


HEADER = re.compile(r"^[\d,]*\s*(SKILL|SPELL|Monster)_([^\s{]+)\s*\{", re.M)


def blocks():
    """블록은 괄호 짝으로 자른다. 5.99 원본은 줄 맨 앞에 `}` 를 두기도 하고(퓨리소월루) 닫는 괄호가
    하나 더 있기도 해서(전체크래셔) 줄 모양으로는 못 자른다. 짝이 안 맞으면 다음 블록 머리에서 멈춘다."""
    return _cut(sorted((PACK / "script" / "Skill").glob("*.txt")) + [PACK / "script" / "Mob_Spell.txt"])


def _cut(paths, first=False):
    out = {}
    for path in paths:
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
            if first and (head.group(1), head.group(2)) in out:
                continue
            out[(head.group(1), head.group(2))] = (path.name, text[head.end():at - 1 if not depth else at])
    return out


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
