#!/usr/bin/env python3
"""원작 클라이언트 아카이브 안에 무엇이 있는지 Obsidian vault 로 남긴다.

**팩부터 뒤지지 않기 위한 도구다.** 팩(5.99)은 운영자가 손댄 사본이고, 원작 표가
`database/archives/*.dat` 안에 그대로 남아 있는 일이 잦다. 그런데 아카이브는 바이너리라
열어 보기 전에는 뭐가 든지 알 수 없어서, 매번 다시 캐게 된다. 한 번 캐서 적어 둔다.

  national.dat  _tcoord.txt      맵 번호.이름.월드맵 위치.크기
  Legend.dat    field001~010.txt 원작 월드맵 노드
  setoa.dat     _narti.txt …     아직 뜻을 모르는 표

**내용은 CP949 다.** UTF-8 로 읽으면 깨진다. 여기서 옮겨 적을 때 고친다.
**뜻을 모르는 표는 모른다고 적는다.** 짐작한 설명을 달면 다음 사람이 그것을 근거로 삼는다.

  쓰는 법: python3 scripts/build-archive-vault.py
"""
import json, re, shutil, subprocess, sys, tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ARCHIVES = ROOT / "sources/wren11/Dark-Ages-Private-Server/database/archives"
VAULT = ROOT / "data" / "archives-vault"
TOOL = ROOT / "tools" / "dat-extract"

TEXTY = (".txt", ".tbl")
BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')

# 뜻이 확인된 것만 적는다. 나머지는 "아직 모른다" 로 둔다.
KNOWN = {
    "_tcoord.txt": "맵 번호·한글명·영문명·월드맵 위치(x,y)·가로·세로. 팩의 맵 번호와 일치한다.",
    "_tncoord.txt": "_tcoord 와 같은 꼴. 대평원 계열 두 줄.",
    "mpspal.tbl": "타일 번호 구간 → 팔레트 번호. 5.99 계보 타일 색표.",
    "mpt0018.tbl": "같은 꼴의 7.18 계보 색표.",
    "mpt0027.tbl": "같은 꼴의 7.18 계보 색표.",
    "gndattr.tbl": "땅 속성(지나갈 수 있나 따위)으로 보이나 칸 뜻은 확인 못 했다.",
}
FIELD = "원작 월드맵 한 장. 첫 줄이 팔레트, 그 뒤로 `이름 그림키 x y [EX x2 y2 번호]`."


def slug(name):
    return BANNED.sub("_", name).strip()


def run(*args):
    return subprocess.run(["dotnet", "run", "--project", str(TOOL), "-c", "Release", "--", *args],
                          capture_output=True, text=True, cwd=ROOT).stdout


def decode(data):
    """CP949 가 먼저다. 그게 아니면 UTF-8, 그것도 아니면 latin-1 로 버틴다."""
    for codec in ("cp949", "utf-8", "latin-1"):
        try:
            return data.decode(codec), codec
        except UnicodeDecodeError:
            continue
    return "", "?"


def describe(name):
    if name in KNOWN:
        return KNOWN[name]
    if re.fullmatch(r"field\d+\.txt", name):
        return FIELD
    return "**아직 뜻을 모른다.** 내용을 보고 알아낸 사람이 여기를 채운다."


def main():
    if not ARCHIVES.exists():
        sys.exit(f"{ARCHIVES} 가 없다")
    if VAULT.exists():
        shutil.rmtree(VAULT)                      # 이름이 바뀌면 옛 노트가 남는다
    (VAULT / "아카이브").mkdir(parents=True)
    (VAULT / "표").mkdir(parents=True)

    index, total = [], 0
    for dat in sorted(ARCHIVES.rglob("*.dat")):
        listing = run("list", str(dat))
        kinds = dict(re.findall(r"^\s+(\.[a-z0-9]+)\s+(\d+)개", listing, re.M))
        if not kinds:
            continue

        tables = []
        with tempfile.TemporaryDirectory() as tmp:
            for ext in TEXTY:
                run("dump", str(dat), tmp, ext)
            for f in sorted(Path(tmp).rglob("*")):
                if not f.is_file() or f.suffix.lower() not in TEXTY:
                    continue
                text, codec = decode(f.read_bytes())
                lines = [l for l in text.splitlines() if l.strip()]
                note = f"{slug(dat.stem)} — {slug(f.name)}"
                (VAULT / "표" / f"{note}.md").write_text(
                    "---\n"
                    f'파일: "{f.name}"\n아카이브: "{dat.name}"\n'
                    f"줄수: {len(lines)}\n바이트: {f.stat().st_size}\n인코딩: {codec}\n"
                    "---\n\n"
                    f"# {f.name}\n\n{describe(f.name)}\n\n"
                    f"아카이브: [[아카이브/{slug(dat.stem)}|{dat.name}]]\n\n"
                    f"## 내용 (앞 40줄 / 전체 {len(lines)}줄)\n\n```\n"
                    + "\n".join(lines[:40]) + "\n```\n\n"
                    "## 꺼내는 법\n\n```bash\n"
                    f"dotnet run --project tools/dat-extract -c Release -- \\\n"
                    f"  dump {dat.relative_to(ROOT)} <폴더> {f.stem}\n"
                    f"iconv -f {codec.upper()} -t UTF-8 <폴더>/…/{f.name}\n```\n",
                    encoding="utf-8")
                tables.append((f.name, len(lines), note))
                total += 1

        rows = "\n".join(f"| [[표/{n}\\|{nm}]] | {ln} | {describe(nm).split('.')[0]} |"
                         for nm, ln, n in tables) or "| (없음) | | |"
        (VAULT / "아카이브" / f"{slug(dat.stem)}.md").write_text(
            "---\n"
            f'아카이브: "{dat.name}"\n경로: "{dat.relative_to(ROOT)}"\n'
            f"크기MB: {dat.stat().st_size // (1024*1024)}\n---\n\n"
            f"# {dat.name}\n\n## 들어 있는 것\n\n"
            + "\n".join(f"- `{k}` {v}개" for k, v in sorted(kinds.items())) + "\n\n"
            f"## 읽을 수 있는 표 ({len(tables)}개)\n\n"
            "| 표 | 줄 | 무엇 |\n|---|---|---|\n" + rows + "\n",
            encoding="utf-8")
        index.append((dat.name, len(tables), sum(int(v) for v in kinds.values())))
        print(f"  {dat.name:16} 항목 {sum(int(v) for v in kinds.values()):>6} · 표 {len(tables)}")

    (VAULT / "README.md").write_text(
        "# 원작 아카이브 — 무엇이 어디에\n\n"
        "**팩부터 뒤지지 않기 위한 것이다.** 원작 표가 여기 남아 있는 일이 잦다.\n"
        "순서: `database/server/` → **여기** → `data/game-data/` → 그래도 없으면 팩.\n\n"
        "내용은 **CP949** 다. UTF-8 로 읽으면 깨진다.\n\n"
        "| 아카이브 | 항목 | 읽을 수 있는 표 |\n|---|---|---|\n"
        + "\n".join(f"| [[아카이브/{slug(Path(n).stem)}\\|{n}]] | {e} | {t} |" for n, t, e in index)
        + f"\n\n표 {total}개. `python3 scripts/build-archive-vault.py` 로 다시 만든다.\n",
        encoding="utf-8")
    print(f"\n표 {total}개 → {VAULT.relative_to(ROOT)}/  (Obsidian 으로 연다)")


if __name__ == "__main__":
    main()
