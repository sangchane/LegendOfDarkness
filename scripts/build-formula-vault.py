#!/usr/bin/env python3
"""세계가 무슨 식으로 굴러가는지 — 피해·방어·성장 — 을 Obsidian vault 로 남긴다.

수치는 표에 없다. **식이 코드에 박혀 있다.** 그래서 "몬스터 공격력 표가 어디 있나" 를
물으면 답이 없고, 대신 `Creations/monsters.cs:89` 가 레벨로 체력을 만들어 내고
`Formulas/ac.cs` 가 방어를 적용한다. 그걸 매번 다시 찾지 않도록 적어 둔다.

**근거 없이 적지 않는다.** 노트마다 파일:줄과 그 줄 자체를 넣는다. 못 찾은 것은
"못 찾았다" 로 남긴다.

  쓰는 법: python3 scripts/build-formula-vault.py   → data/formula-vault/
"""
import json, re, shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
FORK = ROOT / "sources/wren11/Dark-Ages-Private-Server"
SRC, SCRIPTS = FORK / "src", FORK / "database/server/scripts"
CONFIG = FORK / "src/Lorule.Config/LoruleConfig.json"
VAULT = ROOT / "data" / "formula-vault"

# 무엇을 찾나 → (노트 제목, 어디서, 어떤 줄을 집나)
HUNTS = [
    ("피해 — 기본 공격", SCRIPTS / "Skills", r"(var |int )?dmg\s*[+*]?=|ApplyDamage"),
    ("피해 — 괴물이 때릴 때", SCRIPTS / "Formulas/damage.cs", r"diff|mod|Level|return"),
    ("방어 — AC 적용", SCRIPTS / "Formulas/ac.cs", r"armor|calculatedDmg|return"),
    ("속성 상성", SCRIPTS / "Formulas/elements.cs", r"return|case |\*"),
    ("경험치·전리품", SCRIPTS / "Formulas/monsterexp.cs", r"exp|Drops|Gold"),
    ("괴물이 만들어질 때", SCRIPTS / "Creations/monsters.cs", r"Level|_Str|_Int|_Wis|_Con|_Dex|hp\s*=|Ac"),
    ("사람이 레벨업할 때", SRC / "Hades.Server.Base/Types/Monster.cs", r"_MaximumHp|_MaximumMp|StatPoints|ExpLevel\+\+"),
    ("마법이 스크립트를 찾는 법", SRC / "Hades.Server.Base/Types/Spell.cs", r"ScriptKey|ScriptManager"),
    ("기술이 스크립트를 찾는 법", SRC / "Hades.Server.Base/Types/Skill.cs", r"ScriptName|ScriptManager"),
    ("기본 공격이 도는 길", SRC / "Hades.Server.Base/Network/Game/GameServerHandlers.cs",
     r"GetAssails|AssailIsReady|GlobalBaseSkillDelay|WeaponScripts"),
]

# 식이 읽는 설정값. 값이 바뀌면 세계가 바뀐다.
KNOBS = ["HpGainFactor", "MpGainFactor", "StatsPerLevel", "MinimumHp", "MaxHP",
         "GlobalBaseSkillDelay", "AssailsCancelSpells", "DeathHPPenalty",
         "PlayerLevelCap", "GiveAssailOnCreate", "StarterSpellOnCreate"]

BANNED = re.compile(r'[\\/:*?"<>|#\[\]^]')


def hits(where, pattern):
    """파일이든 폴더든 훑어 걸리는 줄을 근거와 함께 모은다."""
    files = sorted(where.rglob("*.cs")) if where.is_dir() else ([where] if where.exists() else [])
    rx, found = re.compile(pattern), []
    for f in files:
        for n, line in enumerate(f.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
            s = line.strip()
            if s and not s.startswith("//") and rx.search(s):
                found.append((f.relative_to(FORK).as_posix(), n, s))
    return found


def main():
    if VAULT.exists():
        shutil.rmtree(VAULT)
    (VAULT / "식").mkdir(parents=True)
    (VAULT / "설정").mkdir(parents=True)

    index = []
    for title, where, pattern in HUNTS:
        found = hits(where, pattern)
        body = [
            "---", f'이름: "{title}"', f'갈래: 식',
            f'근거수: {len(found)}', f'출처: "{where.relative_to(FORK).as_posix()}"', "---", "",
            f"# {title}", "",
        ]
        if not found:
            body += ["**못 찾았다.** 여기 있을 줄 알았는데 걸리는 줄이 없다. "
                     "찾은 사람이 이 노트를 고친다.", ""]
        else:
            body += [f"`{where.relative_to(FORK).as_posix()}` 에서 {len(found)}줄.", "", "```csharp"]
            body += [f"{p}:{n}  {s}" for p, n, s in found[:40]]
            body += ["```", ""]
            if len(found) > 40:
                body += [f"…그 밖 {len(found) - 40}줄.", ""]
        body += ["## 읽는 설정", "", "이 식이 쓰는 값은 [[설정/LoruleConfig|LoruleConfig]] 에 있다.", ""]
        (VAULT / "식" / f"{BANNED.sub('_', title)}.md").write_text("\n".join(body), encoding="utf-8")
        index.append((title, len(found)))

    raw = CONFIG.read_text(encoding="utf-8-sig")
    rows = []
    for k in KNOBS:
        m = re.search(rf'"{k}"\s*:\s*([^,\n]+)', raw)
        rows.append((k, m.group(1).strip() if m else "**없다**"))
    (VAULT / "설정" / "LoruleConfig.md").write_text(
        "---\n이름: \"LoruleConfig\"\n갈래: 설정\n---\n\n"
        "# 식이 읽는 설정값\n\n"
        "값이 바뀌면 세계가 바뀐다. 원본은 `src/Lorule.Config/LoruleConfig.json` 이고,\n"
        "**빌드할 때마다 Staging 의 사본이 이 원본으로 덮인다**(Windows 경로째로).\n\n"
        "| 값 | 지금 |\n|---|---|\n" + "\n".join(f"| `{k}` | `{v}` |" for k, v in rows) + "\n",
        encoding="utf-8")

    (VAULT / "README.md").write_text(
        "# 세계가 굴러가는 식\n\n"
        "**수치는 표에 없다. 식이 코드에 박혀 있다.**\n"
        "괴물 공격력 표를 찾아도 없는 이유가 이것이다 — 레벨 하나에서 체력·능력치·피해가 다 나온다.\n"
        "(자료로 있는 것은 서버팩의 괴물 수치뿐이고, 지금 서버는 그것을 안 쓴다. "
        "`plans/server-pack-content-port.md` 참고.)\n\n"
        "| 무엇 | 근거 줄 |\n|---|---|\n"
        + "\n".join(f"| [[식/{BANNED.sub('_', t)}\\|{t}]] | {n} |" for t, n in index)
        + "\n\n[[설정/LoruleConfig|식이 읽는 설정값]]\n\n"
          "`python3 scripts/build-formula-vault.py` 로 다시 만든다.\n",
        encoding="utf-8")
    for t, n in index:
        print(f"  {t:24} 근거 {n}줄")
    print(f"\n→ {VAULT.relative_to(ROOT)}/  (Obsidian 으로 연다)")


if __name__ == "__main__":
    main()
