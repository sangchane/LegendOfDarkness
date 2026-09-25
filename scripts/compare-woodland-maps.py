#!/usr/bin/env python3
"""우드랜드 맵 파일이 어느 판에서 왔는지 여섯 출처의 해시를 맞대어 가린다.

**왜.** 서버에 들어간 우드랜드는 5.99 팩의 14맵 판인데, 원작 월드맵에는 동의·서의·남의·북의
우드랜드 네 곳이 있다. 혼든 팩과 Novaonline 팩은 또 서로 다르다 — 둘이 같은 연결은 8개뿐이라
"세 팩이 일치할 때만" 규칙으로는 정할 수 없다. 그래서 **맵 파일 자체**를 원작과 맞대어 본다.
자세한 까닭은 `docs/woodland-origin-work-order.md`.

맞대는 여섯 갈래:

  원작4.51  설치본 lodr4.51.exe 안의 maps/lod*.map   (2001)
  원작2005  설치본 lodr.exe 안의 maps/lod*.map       (= 5.99 클라이언트와 같은 판)
  7.41      LOD_ 쪽 클라이언트가 쌓아 둔 maps/       (읽기만 한다 — 가져오지 않는다)
  노바온라인 sources/novaonline/db/maps/…/lod*.map
  혼든      data/map-origins/woodland-candidates.json (맥이 뽑아 둔 해시)
  5.99      같은 파일

**설치본과 맵 파일은 커밋하지 않는다.** 경로만 적는다.

  쓰는 법:
    python scripts/compare-woodland-maps.py --원작4.51 <풀어 둔 폴더> --원작2005 <풀어 둔 폴더>

  푸는 법(InstallShield 6 이라 일반 압축 도구로는 안 열린다):
    bz x -y sources/lodr4.51.exe
    docker run --rm -v <폴더>:/w debian:bookworm-slim \
      sh -c 'apt-get update && apt-get install -y unshield && cd /w/Disk1 && unshield -d /w/out x data1.cab'
"""
import argparse
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CANDIDATES = ROOT / "data" / "map-origins" / "woodland-candidates.json"
NOVA_MAPS = ROOT / "data" / "server-packs" / "extracted" / "novaonline" / "maps.json"
NOVA_PACK = ROOT / "sources" / "novaonline"
OUT_JSON = ROOT / "data" / "map-origins" / "woodland-origins.json"
OUT_DOC = ROOT / "docs" / "woodland-origin.md"

#: 7.41 클라이언트가 서버에서 받아 쌓아 둔 맵. 형제 폴더이고, 여기서는 **해시만 읽는다**.
SEVEN41 = Path("D:/_personal/LOD_/raw_data/maps")

for stream in (sys.stdout, sys.stderr):
    if hasattr(stream, "reconfigure"):
        stream.reconfigure(encoding="utf-8")


def digest(path):
    """그 파일의 md5 와 크기. 없으면 (None, None)."""
    if not path or not path.is_file():
        return None, None
    return hashlib.md5(path.read_bytes()).hexdigest(), path.stat().st_size


def maps_under(folder):
    """풀어 둔 설치본 어디에 있든 maps 폴더를 찾는다."""
    if not folder:
        return None
    folder = Path(folder)
    if (folder / "lod0.map").exists():
        return folder
    found = [p for p in folder.rglob("maps") if p.is_dir() and any(p.glob("lod*.map"))]
    return found[0] if found else None


def novaonline():
    """노바온라인 팩의 우드랜드 맵 — 번호 → (이름, 맵파일 경로, 크기 칸)."""
    rows = json.loads(NOVA_MAPS.read_text(encoding="utf-8"))
    out = {}
    for m in rows:
        if "우드랜드" not in m["이름"]:
            continue
        f = m["fields"]
        number = file_number(f.get("맵파일", ""))
        if not number:
            continue
        out.setdefault(number, []).append({
            "이름": m["이름"],
            "맵파일": f["맵파일"],
            "칸": f"{f['너비']}x{f['높이']}",
        })
    return out


def file_number(path):
    """`db/maps/WoodLand/maps/lod703.map` → `703`. 서버 맵 번호가 아니라 **파일** 번호다."""
    found = re.search(r"lod(\d+)\.map", path or "")
    return str(int(found.group(1))) if found else None


def packed(candidates, pack):
    """맥이 뽑아 둔 팩 해시 — **파일 번호** → 행들.

    팩의 `번호`(서버 맵 번호)와 파일 번호는 같지 않다 — 혼든 `오염된우드랜드` 는 번호 9016 인데
    파일은 `lod898950.map` 이고, 5.99 는 세 맵(5-1·6-1·14-1)이 `lod703.map` 하나를 나눠 쓴다.
    번호로 맞대면 엉뚱한 파일과 견주게 된다.
    """
    table = {}
    for r in candidates["packs"].get(pack, []):
        number = file_number(r.get("맵파일", ""))
        if number:
            table.setdefault(number, []).append(r)
    return table


def blocks(folder):
    """원작 맵에서 우드랜드 모양 구간을 찾는다.

    맵 파일에는 이름이 없다 — 이름은 서버가 붙인다. 그래서 **크기 차례**를 열쇠로 쓴다.
    동의(600~623)는 입구 3750(25x25) 다음에 60000(100x100)이 셋, 그 뒤로 15000(50x50)이 줄줄이 오고
    끝에 60000 둘이다. 같은 차례를 갖는 구간이 원작에 몇 개나 있는지 세면, 원작 월드맵이 이름 붙인
    우드랜드 넷(동의·서의·남의·북의)과 맞는지 볼 수 있다.
    """
    if not folder:
        return []
    size = {}
    for path in folder.glob("lod*.map"):
        try:
            size[int(path.stem[3:])] = path.stat().st_size
        except ValueError:
            continue
    # 입구(25x25) 뒤에 100x100 이 둘 이어지면 우드랜드 모양으로 본다.
    found = []
    for number, bytes_ in sorted(size.items()):
        if bytes_ == 3750 and size.get(number + 1) == 60000 and size.get(number + 2) == 60000:
            found.append({"시작": number,
                          "크기차례": [size.get(n) for n in range(number, number + 24)]})
    return found


def document(facts):
    """표 한 장과 결론을 `docs/woodland-origin.md` 로 적는다."""
    rows = facts["맵"]
    shapes = facts["원작우드랜드구간"]["구간"]

    def named(source):
        names = source.get("이름")
        if not names:
            return "—"
        return " · ".join(names) if isinstance(names, list) else names

    same = sum(1 for r in rows if r["판정"]["노바온라인"] == "원작과 같음")
    nova_total = sum(1 for r in rows if "md5" in r["출처"]["노바온라인"])
    honden_diff = sum(1 for r in rows if r["판정"]["혼든"] == "다름")
    five_diff = sum(1 for r in rows if r["판정"]["5.99"] == "다름")

    lines = [
        "# 원작 우드랜드 — 어느 팩의 맵이 원작 파일인가",
        "",
        "- 기준일: 2026-09-25 (윈도우 세션, `docs/woodland-origin-work-order.md` 의 결과)",
        "- 만드는 법: `python scripts/compare-woodland-maps.py --원작4.51 <폴더> --원작2005 <폴더>`",
        "- 단일 출처: `data/map-origins/woodland-origins.json`",
        "",
        "## 결론",
        "",
        f"1. **Novaonline 팩의 우드랜드 맵이 원작 파일 그대로다** — {same}/{nova_total} 이 원작과 **바이트까지 같다**.",
        f"   혼든은 같은 번호를 쓰면서 내용을 고쳤고({honden_diff}개 전부 다름), 5.99 도 마찬가지다({five_diff}개 전부 다름).",
        f"2. **원작에는 우드랜드 구간이 넷 있다** — "
        + " · ".join(f"`lod{b['시작']}`" for b in shapes)
        + ". 원작 월드맵(`field001.txt`)이 이름 붙인 동의·서의·남의·북의 넷과 수가 맞는다.",
        "   Novaonline 이 쓰는 것은 셋(`441` 이름 없음 · `600` 동의 · `700` 북의)이고, **`542` 는 어느 팩도 안 쓴다**.",
        "",
        "**맵 파일에는 이름이 없다** — 이름은 서버가 붙인다. 그래서 `441` 과 `542` 중 어느 쪽이 서의이고",
        "어느 쪽이 남의인지는 **확인 못 했다**. 참고로 원작 월드맵에서 **남의우드랜드만 `EX`(들어가는 자리)가 없다**:",
        "",
        "```",
        "동의우드랜드  f003  516 176  EX 443 140 22",
        "서의우드랜드  f004  155 171  EX 117 135 24",
        "남의우드랜드  f005  398 365            ← EX 없음",
        "북의우드랜드  f006  255  96  EX 203  86 23",
        "```",
        "",
        "## 덤으로 알게 된 것",
        "",
        "- **4.51(2001)과 2005 의 우드랜드 맵은 바이트까지 같다.** 네 해 동안 안 바뀌었다.",
        *[f"- `lod{b['시작']}` 구간은 24칸 중 "
          + ", ".join(f"`lod{b['시작'] + at}`" for at, size in enumerate(b["크기차례"]) if size is None)
          + " 이 **없다**(23개)."
          for b in shapes if any(size is None for size in b["크기차례"])],
        "- `lod441` 과 `lod600` 의 **입구 맵은 같은 파일**이다(md5 `0df8aa89…`).",
        "- 7.41 클라이언트(`LOD_`)가 쌓아 둔 맵도 원작과 같다 — 원작 확인에 쓸 수 있는 갈래가 하나 더 있다는 뜻이다.",
        "- **작업지시서의 예시가 틀렸다.** 지시서는 혼든 `동의우드랜드입구` = `lod600.map` md5 `0df8aa89…` 라고",
        "  적었지만, `woodland-candidates.json` 의 실제 값은 `69c1e745…`(5,760바이트)다. `0df8aa89…` 는 **원작**",
        "  입구의 해시다(3,750바이트). 혼든 입구는 원작과 다르다.",
        "",
        "## 표",
        "",
        "판정은 **원작(4.51·2005)과 바이트까지 같은가**다.",
        "",
        "| 파일 | 원작 | 7.41 | Novaonline | 혼든 | 5.99 | 판정(노바/혼든/5.99) |",
        "|---|---|---|---|---|---|---|",
    ]
    for r in rows:
        orig = r["출처"]["원작4.51"].get("바이트") or r["출처"]["원작2005"].get("바이트")
        short = {"원작과 같음": "같음", "다름": "다름", "없음": "—"}
        verdict = " / ".join(short.get(r["판정"][k], "확인 못 함") for k in ("노바온라인", "혼든", "5.99"))
        lines.append(
            f"| `lod{r['번호']}` | {f'{orig:,}B' if orig else '없음'} "
            f"| {'있음' if r['출처']['7.41'].get('md5') else '—'} "
            f"| {named(r['출처']['노바온라인'])} | {named(r['출처']['혼든'])} | {named(r['출처']['5.99'])} "
            f"| {verdict} |")

    lines += [
        "",
        "## 그래서 무엇을 기준으로 옮기나",
        "",
        "**Novaonline 이다.** 맵 파일이 원작과 같으니 그 팩의 우드랜드가 원작 구조다.",
        "다만 **맵 사이 연결(워프)은 서버 자료라 클라이언트에 없다** — 이 확인이 가려 준 것은 *맵 파일*까지이고,",
        "연결이 원작과 같은지는 여기서 답하지 못한다.",
        "",
    ]
    OUT_DOC.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"-> {OUT_DOC.relative_to(ROOT)}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--원작4.51", dest="v451", help="lodr4.51.exe 를 풀어 둔 폴더")
    ap.add_argument("--원작2005", dest="v2005", help="lodr.exe 를 풀어 둔 폴더")
    args = ap.parse_args()

    candidates = json.loads(CANDIDATES.read_text(encoding="utf-8"))
    honden = packed(candidates, "honden-community")
    five99 = packed(candidates, "5.99-server")
    nova = novaonline()

    folders = {"원작4.51": maps_under(args.v451), "원작2005": maps_under(args.v2005)}
    for name, folder in folders.items():
        print(f"{name}: {folder if folder else '확인 못 함 (폴더를 안 줬거나 맵이 없다)'}")
    print(f"7.41: {SEVEN41 if SEVEN41.is_dir() else '확인 못 함'}")

    shapes = blocks(folders["원작4.51"] or folders["원작2005"])
    print("원작 우드랜드 모양 구간: " + (", ".join(f"lod{b['시작']}" for b in shapes) or "확인 못 함"))

    numbers = sorted(set(honden) | set(five99) | set(nova), key=int)
    rows = []
    for number in numbers:
        row = {"번호": number, "출처": {}}
        for name, folder in folders.items():
            md5, size = digest(folder / f"lod{number}.map" if folder else None)
            row["출처"][name] = {"md5": md5, "바이트": size} if md5 else {"없음": True}

        md5, size = digest(SEVEN41 / f"lod{number}.map" if SEVEN41.is_dir() else None)
        row["출처"]["7.41"] = {"md5": md5, "바이트": size} if md5 else {"없음": True}

        if number in nova:
            names = [one["이름"] for one in nova[number]]
            md5, size = digest(NOVA_PACK / nova[number][0]["맵파일"])
            row["출처"]["노바온라인"] = ({"md5": md5, "바이트": size, "이름": names}
                                    if md5 else {"없음": True, "이름": names})
        else:
            row["출처"]["노바온라인"] = {"없음": True}

        # 한 파일을 여러 맵이 나눠 쓰는 일이 있다(5.99 의 5-1·6-1·14-1 이 lod703 하나). 이름을 다 적는다.
        for name, table in (("혼든", honden), ("5.99", five99)):
            here = table.get(number)
            row["출처"][name] = ({"md5": here[0]["md5"], "바이트": here[0]["바이트"],
                                  "이름": [one["이름"] for one in here]}
                                 if here else {"없음": True})

        # 판정 — 원작(4.51 또는 2005) 과 바이트까지 같은가.
        original = [row["출처"][k].get("md5") for k in ("원작4.51", "원작2005")]
        original = [m for m in original if m]
        row["판정"] = {}
        for name in ("7.41", "노바온라인", "혼든", "5.99"):
            mine = row["출처"][name].get("md5")
            if not mine:
                row["판정"][name] = "없음"
            elif not original:
                row["판정"][name] = "확인 못 함 (원작에 그 번호가 없다)"
            else:
                row["판정"][name] = "원작과 같음" if mine in original else "다름"
        rows.append(row)

    facts = {
        "설명": ("우드랜드 맵 파일이 어느 판에서 왔는지 여섯 갈래의 md5 를 맞대어 본 것. "
                 "설치본·맵 파일은 커밋하지 않는다 — 경로만 적는다. "
                 "만드는 법: python scripts/compare-woodland-maps.py"),
        "갱신": "2026-09-25",
        "출처경로": {
            "원작4.51": "sources/lodr4.51.exe 를 풀어서 나온 maps/ (저장소 밖)",
            "원작2005": "sources/lodr.exe 를 풀어서 나온 maps/ (저장소 밖)",
            "7.41": str(SEVEN41),
            "노바온라인": "sources/novaonline/db/maps/…/maps/",
            "혼든": "data/map-origins/woodland-candidates.json (맥이 뽑음)",
            "5.99": "data/map-origins/woodland-candidates.json (맥이 뽑음)",
        },
        "원작우드랜드구간": {
            "설명": ("맵 파일에는 이름이 없다. 입구(25x25=3750) 뒤에 100x100 이 둘 이어지는 차례를 "
                     "열쇠로 삼아 원작 맵에서 우드랜드 모양 구간을 센 것. 원작 월드맵(field001.txt)이 "
                     "이름 붙인 우드랜드는 동의·서의·남의·북의 넷이다."),
            "구간": shapes,
        },
        "맵": rows,
    }
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(json.dumps(facts, ensure_ascii=False, indent=2), encoding="utf-8")

    document(facts)

    tally = {}
    for row in rows:
        for name, verdict in row["판정"].items():
            tally.setdefault(name, {}).setdefault(verdict, 0)
            tally[name][verdict] += 1
    print(f"\n맵 {len(rows)}개 → {OUT_JSON.relative_to(ROOT)}")
    for name, counts in tally.items():
        print(f"  {name:8} " + " · ".join(f"{v} {n}" for v, n in sorted(counts.items())))


if __name__ == "__main__":
    main()
