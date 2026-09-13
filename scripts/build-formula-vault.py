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

# 서버를 돌려 보고 알아낸 것. 그래서 앞의 HUNTS 와 성격이 다르다 — 여기 있는 것은 "이 줄이 있다" 가
# 아니라 "이 줄이 이런 결과를 낸다" 이고, 숫자는 실제로 재 본 값이다. 재 본 것을 지키는 시험을 함께
# 적어 둔다. 근거 줄이 안 걸리면 노트가 "못 찾았다" 로 바뀌므로, 코드가 움직이면 여기가 먼저 안다.
FINDINGS = [
    ("방어가 깎은 값을 마지막 줄이 되돌리고 있었다 — 고쳤다",
     "`ac.cs` 는 방어력 공식을 제대로 갖고 있었다. 그런데 **마지막 줄이 깎인 값을 되돌려서**, "
     "방어가 피해를 줄이는 일이 한 번도 없었다. 그 줄을 뺐다.",
     [(SCRIPTS / "Formulas/ac.cs", r"armor|calculatedDmg|return|used to end"),
      (SRC / "Hades.Server.Base/Network/Game/GameClient.cs", r"BonusAc = \(100"),
      (SCRIPTS / "Creations/monsters.cs", r"BonusAc = \(int\)\(70"),
      (SRC / "Hades.Server.Base/Types/Item.cs", r"AcModifer\.Value")],
     ["옛 코드는 `return calculatedDmg + Math.Abs(value - calculatedDmg)` 였다. 이 식은 두 값 중 "
      "**큰 쪽**이다. 그래서 배수가 1 미만이어서 피해가 줄었을 때 그 감소분이 마지막 줄에서 사라졌다 — "
      "가장 좋은 방어(-70)가 중립(-2)과 **똑같이** 맞았고, 맨몸(+100)은 3.06배를 맞았다.",
      "고친 뒤에는 배수 `(Ac+101)/99` 가 그대로 결과다: -70 → 0.31배 · -2 → 1.00배 · +100 → 2.03배. "
      "맨몸이 두 배 맞는 것은 설계다. Ac 를 아래로 내리는 것이 장비의 일이다.",
      "`Math.Abs` 도 같이 뺐다. `Sprite.Ac` 가 -70 에서 막아 주므로 한 번도 동작한 적이 없고, "
      "그 막음이 풀리면 좋은 방어를 다시 손해로 뒤집는다 — 이 버그를 가려 준 것이 바로 그 함수다.",
      "방향은 맞게 들어와 있다. 이식한 아이템 992개 중 **547개**가 `AcModifer` 를 들고 있고 전부 "
      "`Option 1`(=`Operator.Remove`)이라 걸치면 Ac 가 **내려간다**. 다만 수준만으로는 "
      "`100 - 수준/3` 이어서 99수준 맨몸도 +67 이다 — 배수가 1 아래로 내려가려면 장비가 채워야 한다.",
      "재 본 값(1수준 괴물·맨몸 1수준 사람): 괴물의 한 방이 사람에게 **10점 → 7점**. 다섯 번째 "
      "휘두름(기술 6수준)이 괴물에게 **70점 → 49점**. 등 뒤에서 때린 한 방은 105점이어서 체력 91을 "
      "한 방에 죽였는데 이제 74점이다 — 그래서 예측값 두 개가 모두 0 이 아닌 값으로 갈라진다."],
     "tests/hades-characterization/CombatSmokeTests.cs (Landed)"),

    ("기술은 휘두를 때마다 한 단계 오른다",
     "`toImprove = (int)(0.10 / LevelRate)` 가 **0** 이 되어 `Uses++ >= 0` 이 언제나 참이다. "
     "백 번 써야 오르도록 만든 값이 한 번마다 오르게 만든다.",
     [(SRC / "Hades.Server.Base/Network/Game/GameClient.cs", r"toImprove|Uses\+\+|skill\.Level\+\+"),
      (FORK / "database/server/templates/skills/Assail.json", r"LevelRate|MaxLevel")],
     ["0.10 / 0.5 = 0.2 이고 `(int)` 가 0 으로 깎는다. 그래서 Assail 은 첫 휘두름에 2수준이 되고, "
      "k번째 휘두름의 수준은 1+k 다(MaxLevel 100 에서 멎는다).",
      "수준은 피해에 들어간다 — `imp = 10 + 수준`. 그래서 **연속으로 휘두르면 매번 더 아프다**. "
      "예측값을 맞춰 보려면 몇 번째 휘두름인지를 알아야 한다.",
      "서버는 오를 때마다 `\"Assail has improved. (Lv. N)\"` 이라고 말한다. 그 말이 유일한 표시다."],
     "tests/hades-characterization/CombatSmokeTests.cs (LevelOnUse)"),

    ("괴물 템플릿의 체력은 버려진다",
     "젠할 때 `obj.Template.MaximumHP` 를 **레벨로 다시 계산해 덮어쓴다.** 팩이 적어 준 수치는 "
     "읽히지도 않는다.",
     [(SCRIPTS / "Creations/monsters.cs", r"var hp =|var mp =|Template\.MaximumHP|Template\.MaximumMP|CurrentHp ="),
      (SCRIPTS / "Formulas/damage.cs", r"obj\.Level|mod =|diff =")],
     ["`hp = (수준+1)×0.01 + 50 + 수준×(수준+40)` → 1수준이면 **91**. 서버팩 `가스` 는 17,550 을 "
      "적어 두었는데 91 로 선다. 565개 배치가 전부 1수준이므로 **전부 91** 이다.",
      "공격력도 같다. 표가 없고 `damage.cs` 가 레벨과 사람의 레벨 차이만 읽는다 — "
      "1수준 괴물의 한 방은 맨 7점이고, 사람의 방어 +100 을 지나 21, 속성 절반으로 **10점**이다.",
      "덮어쓰는 것이 **템플릿**이라 값이 젠 사이에 남는다. 그래서 `MaximumMP` 를 읽는 `CastEnabled` 는 "
      "첫 젠에서는 꺼지고 두 번째부터는 켜진다 — 같은 정의인데 결과가 다르다."],
     "tests/hades-characterization/CombatSmokeTests.cs (MonsterHealth · MonsterDamage)"),

    ("새 캐릭터는 가득 차 있지 않다",
     "150 중 60, 200 중 30 으로 깨어난다. 40% 다.",
     [(SRC / "Hades.Server.Base/Types/Aisling.cs", r"CurrentHp = 60|CurrentMp = 30|_MaximumHp = 150|_MaximumMp = 200|_Str = 10|_Dex = 5")],
     ["체력이 얼마나 깎였는지 재려면 최대 체력이 아니라 **그때 체력**을 기준으로 잡아야 한다. "
      "그리고 재생이 그 값을 계속 밀어 올린다.",
      "괴물을 죽이면 경험치가 들어와 수준이 오르고 최대 체력이 바뀐다. 시험은 그 둘을 만나면 "
      "기준만 새로 잡고 계속 본다."],
     "tests/hades-characterization/CombatSmokeTests.cs (HitBack)"),

    ("젠은 벽에 걸린 시도도 한 번으로 센다",
     "자리를 무작위로 골라 벽이면 아무것도 안 세우는데, 그 정의는 이미 `SpawnRate` 만큼 쉬러 들어간다.",
     [(SRC / "Hades.Server.Base/Templates/MonsterTemplate.cs", r"NextAvailableSpawn|ReadyToSpawn|Ready"),
      (SRC / "Hades.Server.Base/Network/Game/Components/MonolithComponent.cs", r"ReadyToSpawn|SpawnMax|Rows == 0|return;"),
      (SCRIPTS / "Creations/monsters.cs", r"IsWall|SpawnQualifer\.Random")],
     ["`ReadyToSpawn()` 이 **먼저** 다음 시각을 20초 뒤로 밀고 참을 돌려준다. 그 뒤 "
      "`FindBestMonsterMapSlot` 이 벽을 골라 `Create` 가 null 을 내면 그 20초는 그냥 날아간다.",
      "그래서 정의가 둘뿐인 방은 1분에 여섯 번만 굴리고, 집 안은 대부분 벽이라 몇 분을 기다려도 "
      "빈 방일 수 있다. 시험이 `지하수로D-2`(정의 7개, 20×20)를 쓰는 이유가 이것이다.",
      "그리고 훑는 고리가 `continue` 대신 **`return`** 이다 — 크기가 0 인 맵 하나를 만나면 그 뒤 "
      "모든 맵의 젠이 그 회차에 멎는다."],
     "tests/hades-characterization/CombatSmokeTests.cs (MonsterRoom · AnyMonster)"),

    ("헛친 것도 서버가 알려준다",
     "아무것도 닿지 않으면 **serial 0 의 체력 0** 이라는 체력 보고가 온다.",
     [(SCRIPTS / "Skills/Assail.cs", r"ServerFormat13\(0, 0")],
     ["그래서 체력 보고를 세면 몇 번 휘둘렀는지가 그대로 나온다 — 맞으면 그 놈의 체력, 헛치면 0. "
      "기술 수준이 휘두름마다 오르므로(위) 이 셈이 예측에 그대로 쓰인다.",
      "반대로, serial 0 을 괴물로 세면 '체력 0% = 한 방에 죽었다' 로 잘못 읽는다."],
     "tests/hades-characterization/CombatSmokeTests.cs (NothingWasHit)"),

    ("속성이 없는 쪽끼리는 절반",
     "때리는 쪽도 맞는 쪽도 속성이 None 이면 피해가 **0.50** 배다. 새 캐릭터와 이식한 괴물이 바로 그 짝이다.",
     [(SCRIPTS / "Formulas/elements.cs", r"None && element == ElementManager\.Element\.None|return 0\.50|return 1\.00")],
     ["맞는 쪽만 None 이고 때리는 쪽에 속성이 있으면 1.00 배다 — 속성을 붙이는 것이 두 배로 때리는 "
      "일이 된다. 이식한 괴물 565개는 `ElementType` 이 없어 전부 None 이다.",
      "`Element.Random` 은 읽을 때마다 다시 굴린다. 그런 놈이 섞이면 같은 한 방이 두 번 다르다."],
     "tests/hades-characterization/CombatSmokeTests.cs (NoElementEither)"),
]


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

    (VAULT / "확인").mkdir(parents=True)

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

    checked = []
    for title, claim, evidence, notes, pinned in FINDINGS:
        rows = [(w, p, hits(w, p)) for w, p in evidence]
        total = sum(len(f) for _, _, f in rows)
        body = [
            "---", f'이름: "{title}"', "갈래: 확인",
            f"근거수: {total}", f'지키는시험: "{pinned}"', "---", "",
            f"# {title}", "", claim, "",
        ]
        for note in notes:
            body += [note, ""]
        body += ["## 근거", ""]
        for where, pattern, found in rows:
            rel = where.relative_to(FORK).as_posix()
            if not found:
                body += [f"- `{rel}` — **못 찾았다.** 줄이 움직였다. 이 노트를 다시 본다.", ""]
                continue
            body += [f"`{rel}`", "", "```csharp"]
            body += [f"{p}:{n}  {ln}" for p, n, ln in found[:14]]
            body += ["```", ""]
            if len(found) > 14:
                body += [f"…그 밖 {len(found) - 14}줄.", ""]
        body += ["## 이걸 지키는 시험", "", f"`{pinned}`", "",
                 "식대로인지는 눈으로 못 본다. 그래서 값을 먼저 계산하고 서버에 실제로 때려 본다 —",
                 "\"체력이 좀 깎였다\" 로는 한 대에 1% 가 깎이든 90% 가 깎이든 똑같이 통과한다.", ""]
        (VAULT / "확인" / f"{BANNED.sub('_', title)}.md").write_text("\n".join(body), encoding="utf-8")
        checked.append((title, total))

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
        + "\n\n## 돌려 보고 알아낸 것\n\n"
          "아래는 \"이 줄이 있다\" 가 아니라 \"이 줄이 이런 결과를 낸다\" 다. 숫자는 실제로 재 본 값이고,\n"
          "지키는 시험이 노트마다 적혀 있다.\n\n"
          "| 무엇 | 근거 줄 |\n|---|---|\n"
        + "\n".join(f"| [[확인/{BANNED.sub('_', t)}\\|{t}]] | {n} |" for t, n in checked)
        + "\n\n[[설정/LoruleConfig|식이 읽는 설정값]]\n\n"
          "`python3 scripts/build-formula-vault.py` 로 다시 만든다.\n",
        encoding="utf-8")
    for t, n in index:
        print(f"  {t:24} 근거 {n}줄")
    print()
    for t, n in checked:
        print(f"  확인: {t:26} 근거 {n}줄")
    print(f"\n→ {VAULT.relative_to(ROOT)}/  (Obsidian 으로 연다)")


if __name__ == "__main__":
    main()
