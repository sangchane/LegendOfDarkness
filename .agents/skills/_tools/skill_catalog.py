#!/usr/bin/env python3
"""설치된 스킬 카탈로그 + 라우팅 표 정합성 검사.

용도
  python _tools/skill_catalog.py            # 요약 + 라우팅 검사 (기본)
  python _tools/skill_catalog.py --catalog  # 설치 스킬 전체 목록 (id | 출처 | description 앞부분)
  python _tools/skill_catalog.py --unassigned  # 라우팅 표 어디에도 안 물린 설치 스킬 전체
  python _tools/skill_catalog.py --available   # 마켓플레이스에 있지만 미설치인 플러그인 이름

스킬 id 표기 규약 (라우팅 표에서 백틱으로 감싸 쓴다)
  ecc:api-design      플러그인 스킬  <플러그인명>:<스킬명>
  /code-review        Claude Code 번들 스킬 (앞에 슬래시)
  frontend-design-taste  개인/프로젝트 스킬 (디렉토리명 그대로)

설치 여부의 진실원은 ~/.claude/plugins/installed_plugins.json 의 installPath 다.
marketplaces/ 는 "설치 가능" 목록일 뿐이라 여기서 스킬을 세지 않는다.
"""
import json
import os
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")

CLAUDE_DIR = Path(os.environ.get("CLAUDE_CONFIG_DIR", Path.home() / ".claude"))
SKILLS_DIR = Path(__file__).resolve().parent.parent
PROJECT_SKILLS = Path.cwd() / ".claude" / "skills"
ROUTING_FILES = sorted(SKILLS_DIR.glob("*/references/skill-routing.md"))

# 디스크에 없어서 스캔으로 못 찾는 Claude Code 번들 스킬. 버전에 따라 늘 수 있다.
# 출처: code.claude.com/docs/en/skills "Bundled skills" (2026-09 확인).
BUNDLED = {
    "code-review", "simplify", "verify", "run", "debug", "loop", "batch", "doctor",
    "claude-api", "init", "security-review", "update-config", "schedule",
    "workflow-authoring", "run-skill-generator", "fewer-permission-prompts",
}


def frontmatter(path: Path) -> dict:
    text = path.read_text(encoding="utf-8", errors="ignore")
    m = re.match(r"---\s*\n(.*?)\n---", text, re.S)
    if not m:  # 프론트매터 없는 command/agent 파일
        return {"name": path.stem, "description": ""}
    fm = m.group(1)
    name = re.search(r"^name:\s*(.+)$", fm, re.M)
    desc = re.search(r"^description:\s*(.*?)(?=^\w[\w-]*:|\Z)", fm, re.M | re.S)
    desc = re.sub(r"\s+", " ", desc.group(1)).strip().strip("|>").strip() if desc else ""
    return {"name": name.group(1).strip() if name else path.parent.name, "description": desc}


def scan_dir(root: Path, source: str, prefix: str = "") -> dict:
    out = {}
    if not root.is_dir():
        return out
    for skill_md in sorted(root.glob("*/SKILL.md")):
        d = skill_md.parent.name
        if d.startswith("_") or d.lower() == "synced":
            continue
        fm = frontmatter(skill_md)
        out[f"{prefix}{d}"] = {"source": source, "path": skill_md, **fm}
    return out


def installed_plugins() -> dict:
    """installed_plugins.json → {플러그인명: {installPath, scope, projectPath}}"""
    p = CLAUDE_DIR / "plugins" / "installed_plugins.json"
    if not p.is_file():
        return {}
    data = json.loads(p.read_text(encoding="utf-8"))
    out = {}
    for key, entries in data.get("plugins", {}).items():
        name = key.split("@", 1)[0]
        for e in entries:
            scope = e.get("scope", "user")
            if scope == "project" and Path(e.get("projectPath", "")).resolve() != Path.cwd().resolve():
                scope = f"project({Path(e['projectPath']).name}) — 이 프로젝트에선 비활성"
            out[name] = {"installPath": Path(e["installPath"]), "scope": scope, "version": e.get("version")}
    return out


def catalog() -> dict:
    cat = {}
    cat.update(scan_dir(SKILLS_DIR, "personal"))
    cat.update(scan_dir(PROJECT_SKILLS, "project"))
    for pname, info in installed_plugins().items():
        src = f"plugin:{pname} v{info['version']} [{info['scope']}]"
        cat.update(scan_dir(info["installPath"] / "skills", src, f"{pname}:"))
        # commands/ 는 Claude Code가 스킬과 동일하게 /name 으로 노출한다. agents/ 는 서브에이전트 타입.
        for sub, kind in (("commands", "command"), ("agents", "agent")):
            for md in sorted((info["installPath"] / sub).glob("*.md")):
                cat.setdefault(f"{pname}:{md.stem}", {"source": f"{src} {kind}", "path": md, **frontmatter(md)})
    return cat


def routing_refs() -> dict:
    """라우팅 표에서 참조한 스킬 id → 참조한 파일 목록"""
    refs = {}
    for f in ROUTING_FILES:
        for tok in re.findall(r"`(/?[a-z0-9][a-z0-9-]*(?::[a-z0-9][a-z0-9-]*)?)`", f.read_text(encoding="utf-8")):
            refs.setdefault(tok, set()).add(f.relative_to(SKILLS_DIR).as_posix())
    return refs


def est_tokens(text: str) -> int:
    """항상 로드되는 메타데이터 비용 추정. 영문 ≈4자/토큰, 한글·기호 ≈1.5자/토큰 (대략치)."""
    ascii_n = sum(1 for c in text if ord(c) < 128)
    return round(ascii_n / 4 + (len(text) - ascii_n) / 1.5)


def available_not_installed() -> dict:
    inst = set(installed_plugins())
    out = {}
    for mp in (CLAUDE_DIR / "plugins" / "marketplaces").glob("*/.claude-plugin/marketplace.json"):
        try:
            names = [p["name"] for p in json.loads(mp.read_text(encoding="utf-8")).get("plugins", [])]
        except (json.JSONDecodeError, KeyError):
            continue
        out[mp.parent.parent.name] = sorted(n for n in names if n not in inst)
    return out


def main(argv):
    cat = catalog()
    refs = routing_refs()

    if "--catalog" in argv:
        for sid, info in sorted(cat.items()):
            print(f"{sid} | {info['source']} | {info['description'][:110]}")
        return

    if "--available" in argv:
        for mp, names in available_not_installed().items():
            print(f"[{mp}] 미설치 {len(names)}개: {', '.join(names)}")
        return

    by_source = {}
    for info in cat.values():
        by_source[info["source"]] = by_source.get(info["source"], 0) + 1
    print("## 설치 스킬")
    for s, n in sorted(by_source.items(), key=lambda kv: -kv[1]):
        print(f"- {s}: {n}")
    meta = " ".join(f"{sid} {i['description']}" for sid, i in cat.items())
    print(f"- 항상 로드 메타데이터(name+description) 총 {len(meta):,}자 ≈ {est_tokens(meta):,} 토큰 (추정)")
    print(f"  · 상위 5개 description 길이: " + ", ".join(
        f"{sid}={len(i['description'])}" for sid, i in sorted(cat.items(), key=lambda kv: -len(kv[1]['description']))[:5]))

    print(f"\n## 라우팅 표 검사 ({len(ROUTING_FILES)}개 파일, 참조 {len(refs)}개)")
    missing = {}
    for tok, files in refs.items():
        if tok.startswith("/"):
            if tok[1:] not in BUNDLED:
                missing[tok] = files
        elif tok not in cat:
            missing[tok] = files
    if missing:
        print("- 참조했지만 미설치(또는 오타):")
        for tok, files in sorted(missing.items()):
            print(f"  · `{tok}` ← {', '.join(sorted(files))}")
    else:
        print("- 참조한 스킬 전부 설치됨 ✔")

    unassigned = sorted(sid for sid in cat if sid not in refs)
    print(f"- 설치됐지만 어느 라우팅 표에도 없는 스킬: {len(unassigned)}개"
          + ("" if "--unassigned" in argv else " (전체 목록: --unassigned)"))
    if "--unassigned" in argv:
        for sid in unassigned:
            print(f"  · {sid} — {cat[sid]['description'][:80]}")
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
