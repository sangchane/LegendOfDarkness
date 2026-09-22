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
KNOBS = ["HpGainFactor", "MpGainFactor", "StatsPerLevel", "MinimumHp", "MaxHP", "RegenRate",
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

    ("괴물의 수치는 정의 파일에서 온다 — 전에는 레벨로 덮어썼다",
     "괴물 정의 파일이 적어 둔 체력·공격력·방어력·경험치를 서버가 이제 읽는다. 전에는 네 가지를 모두 "
     "`Level` 하나에서 만들고 **그 값을 파일 위에 덮어썼다.**",
     [(SRC / "Hades.Server.Base/Templates/MonsterTemplate.cs", r"int\? (Ac|DmgMin|DmgMax|Exp)"),
      (SCRIPTS / "Creations/monsters.cs", r"MaximumHP <= 0|Template\.MaximumHP|Template\.Ac|CastEnabled"),
      (SCRIPTS / "Formulas/damage.cs", r"DmgMin|DmgMax|Random\.Next"),
      (SCRIPTS / "Formulas/monsterexp.cs", r"Template\.Exp")],
     ["덮어쓰던 줄은 `obj.Template.MaximumHP = (int)hp` 였다. `obj.Template` 은 사본이 아니라 템플릿 "
      "캐시에 있는 **그 객체**라서, 한 번 젠하면 파일에 적힌 숫자가 메모리에서도 사라졌다.",
      "팩 자료 형식 문제가 아니었다. 하데스가 싣는 `spider.json` 도 `Level 5 · 체력 680` 인데 275 로 "
      "덮였다. 반대로 `bees`·`minion` 은 체력을 **0** 으로 적어 둔다 — 그것이 '레벨로 만들어라' 라는 "
      "뜻이고, 지금은 그 관례를 그대로 따른다(0 이거나 Grow 면 계산, 아니면 적힌 값).",
      "레벨은 필요 없었다. 팩 괴물 **243마리 전부**가 체력·최소공격력·최대공격력·방어력·경험치를 "
      "갖고 있다. 레벨은 하데스가 그 넷을 못 가져서 쓰는 대용품일 뿐이다. (체력에서 레벨을 역산하면 "
      "103마리가 300 이상이 된다 — 척도가 다르다. 하데스는 1~99레벨 전체가 체력 91~13,812 인데 팩 "
      "괴물의 중간값이 60,000 이다.)",
      "그 숫자들이 실제로 난이도 사다리를 만든다. 1수준 캐릭터(한 방 약 24점·체력 150) 기준으로 "
      "`(튜토리얼)팜팻`(체력 10·공격 1~2)은 한 대에 죽고 나를 2점 때린다. `노비스풀뱀`(130·1~2)은 "
      "6대. `그린팜팻`(1,950·80~88)부터 위험하고 `좀비`(70,000·1600~1650)는 나를 한 방에 죽인다. "
      "전에는 243마리가 전부 체력 91·한 방 7점이라 **모든 사냥터가 똑같이 쉬웠다.**",
      "방어는 팩도 낮을수록 좋다는 같은 규약이다(-80~0). 0 도 적어 둔 값일 수 있어서 '없음' 은 null "
      "로 가린다 — 초보 사냥터 괴물이 정확히 0 이다.",
      "마법 켜짐(`CastEnabled`)도 같이 고쳤다. 마력을 정하기 **전에** 읽고 있어서, 같은 정의인데 첫 "
      "젠은 꺼지고 두 번째부터 켜졌다."],
     "tests/hades-characterization/CombatSmokeTests.cs (KindsInTheRoom · Landings)"),

    ("스크립트 하나가 어긋나면 전부 사라지고, 서버는 아무 일 없는 듯 뜬다",
     "`scripts/` 아래 C# 은 **한 덩어리로** 컴파일된다. 한 곳이 안 되면 115장이 다 사라지는데 서버는 "
     "그대로 켜지고 포트를 연다 — 괴물도 안 서고 기술도 안 돌지만 로그 한 줄만 남는다.",
     [(SRC / "Hades.Server.Base/Scripting/ScriptManager.cs", r"Compiling all scripts|result\.Success|Diagnostic|LoadFromAssembly")],
     ["실제로 겪었다. `damage.cs` 에 쓴 변수 이름이 아래쪽 `target is Monster monster` 와 겹쳐(CS0136) "
      "스크립트가 0개가 됐고, 서버는 정상으로 보였다. 시험은 '괴물이 안 선다' 며 2분을 기다린 뒤 "
      "엉뚱한 것을 신고했다.",
      "찍히는 줄은 `Scripts Loaded and Compiled: 0` 하나다. 그래서 격리 서버 하네스가 그 줄을 보고 "
      "**바로 멈추고 컴파일러가 한 말을 보여준다** — 같은 실수가 2분이 아니라 14초에 드러났다.",
      "`tools/` 나 원본 게임 파일이 옆에 있으면 관리 어셈블리가 아닌 DLL 도 후보로 들어간다. "
      "`AssemblyName.GetAssemblyName` 으로 걸러 내는 코드가 이미 있다 — 같은 함정이 전에도 있었다."],
     "tests/hades-characterization/IsolatedHadesServer.cs (RequireCompiledScripts)"),

    ("장비는 방어를 100 에서 -70 까지 내린다 — 그래서 배수가 0.31 까지 떨어진다",
     "맨몸은 `100 - 수준/3` 이라 1수준이 +100 이고 피해를 2.03배로 받는다. 장비가 그것을 내리는 유일한 "
     "수단이고, 실을 것은 이미 실려 있다.",
     [(SRC / "Hades.Server.Base/Types/Item.cs", r"AcModifer\.Value"),
      (SRC / "Hades.Server.Base/Types/Sprite.cs", r"BonusAc < -70|public int Ac"),
      (SRC / "Hades.Server.Base/Network/Game/GameClient.cs", r"BonusAc = \(100|ExpLevel < item\.Template\.LevelRequired")],
     ["아이템의 `AcModifer` 가 `Operator.Remove` 면 걸칠 때 `BonusAc` 를 그만큼 **내린다**. 자리별 최고를 "
      "다 갖추면 -245 가 되고 `Sprite.Ac` 의 바닥 -70 에 걸린다 → 배수 `(Ac+101)/99` 가 **0.31**. "
      "맨몸 2.03배와 6.5배 차이다.",
      "**착용은 조건을 검사한다.** `GameClient.CheckReqs` 가 요구 레벨·직업·성별·내구도를 본다(GM 과 "
      "Developer 는 통과). 그래서 상위 장비를 끼려면 수준과 직업을 맞춰야 한다 — 1수준 무직이 낄 수 있는 "
      "것은 착용 가능한 757개 중 293개이고, 그것만으로도 방어가 100 → **7** 까지 내려간다.",
      "적용 순서가 맞게 되어 있다. 로그인 때 `SetAislingStartupVariables` 가 `BonusAc` 를 먼저 정하고 "
      "그 **뒤에** `LoadEquipment` 가 장비 보정을 얹는다 — 순서가 반대면 장비가 지워질 것이다."],
     "(아직 지키는 시험이 없다 — 장비를 갖춘 캐릭터로 재는 시험이 없다)"),

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
     "**전에 그랬다 — 고쳤다(2026-09-22).** 때리는 쪽도 맞는 쪽도 속성이 None 이면 피해가 0.50 배였다. "
     "새 캐릭터와 이식한 괴물이 바로 그 짝이라 사람과 괴물 사이의 거의 모든 한 방이 반이 됐다. 이제 **1.00** 배다.",
     [(SCRIPTS / "Formulas/elements.cs", r"None && element == ElementManager\.Element\.None|return 0\.50|return 1\.00")],
     ["5.99 서버(Novaonline.exe)의 피해 함수에는 상대 속성을 보는 표도, 반으로 깎는 단계도 없다 — 괴물이 "
      "맞을 때 `0x424215`, 사람이 맞을 때 `0x415341`. 반이던 때 괴물 평타는 5.99 의 약 40% 였다"
      "(독거미 16~18 · 5.99 41~44, 포테의숲 팜팻 77~85 · 5.99 198~217).",
      "양쪽이 같이 바뀌었다 — 사람이 괴물을 치는 것도 두 배가 됐다.",
      "공격력 1 이 0점이 되던 것도 함께 풀렸다. 최소 1 보정(`CompleteDamageApplication` 첫 줄)이 ×0.5 보다 "
      "앞에 있어서 1 × 0.5 가 버림에 0 이 됐었다.",
      "맞는 쪽만 None 이고 때리는 쪽에 속성이 있으면 여전히 1.00 배다. 이식한 괴물은 `ElementType` 이 "
      "없어 전부 None 이다. `Element.Random` 은 읽을 때마다 다시 굴린다."],
     "tests/hades-characterization/CombatSmokeTests.cs (NoElementEither)"),

    ("괴물 평타는 방어를 거친 뒤 ×1.3 이다",
     "5.99 는 굴린 공격력을 사람 방어로 먼저 거르고(`0x425d6e` → `0x415173`) 그 뒤 괴물의 공격속성으로 "
     "×13/10 한다(`0x425dc3` → `0x415cff`).",
     [(SCRIPTS / "Skills/Assail.cs", r"MonsterBlowElement|ApplyDamageAfterArmour"),
      (SRC / "Hades.Server.Base/Types/Sprite.cs", r"_afterArmour")],
     ["속성이 안 적힌 괴물에게도 5.99 는 생길 때 1~4 를 붙인다(`0x422bc5`). 그래서 괴물 평타는 **늘** ×1.3 이다.",
      "방어 **뒤에** 곱한다. 앞에서 곱하면 작은 한 방이 버림에 깎인다 — 니에(공격 3)가 5.99 는 6, "
      "앞에서 곱하면 5 다.",
      "괴물 마법(`char_damaged2` — 마레노·플라모)은 이 단계를 거치지 않는다. 5.99 에서 `0x415cff` 를 "
      "부르는 곳은 괴물 평타(`0x425dc3`)와 괴물이 맞을 때(`0x4243d8`) 둘뿐이다."],
     "tests/hades-characterization/Pack599MonsterBlowTests.cs"),

    ("체력·마력은 21초마다 능력치로 찬다",
     "5.99 식이다(`0x46d1a5`, 타이머 `0x4766aa` 의 21000ms). 한 번에 최대 ÷ 100 × 지구력(마력은 지혜) ÷ 4.3 "
     "을 채우고, 최대의 15% 아래면 15%, 25% 위거나 능력치 108 이상이면 25%.",
     [(SRC / "Hades.Server.Base/Network/Game/GameClient.cs", r"NaturalRecovery|hundredth|attribute >= 108")],
     ["**전에는 5초마다 최대의 10~20%** 를 채웠다(분당 120~240%). 25레벨·지구력 65·체력 3083 이면 "
      "5초에 622 — 괴물 하나가 치는 것보다 빨리 차서 죽지 않았다. 5.99 는 21초에 466 이다.",
      "능력치는 장비를 뺀 것(캐릭터 칸 +163 지구력 · +162 지혜)이고, 장비의 회복 칸 합(+152)을 그대로 "
      "더한다 — 하데스에서 그 자리는 `Regen` 이다(지금 그 칸을 가진 아이템은 없다).",
      "5.99 가 더 보는 것 셋은 옮기지 않았다: 배고픔 0 이면 안 참(하데스에 배고픔이 없다), 혼수 모습이면 "
      "안 참, 캐릭터 칸 +98 이 켜져 있으면 1.5배(무엇인지 모른다)."],
     "tests/hades-characterization/Pack599RecoveryTests.cs"),
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

    # ── 기술·마법이 실제로 도는가 ────────────────────────────────────────
    # **하데스 먼저, 원작 다음, 팩은 안 본다.** 이 함수는 팩 자료를 읽지 않는다 — 규칙을 글로만
    # 적어 두면 다음 사람이 또 팩부터 뒤진다.
    skill_scripts, spell_scripts = set(), set()
    for f in sorted(SCRIPTS.rglob("*.cs")):
        t = f.read_text(encoding="utf-8-sig", errors="replace")
        found = re.findall(r'\[Script\("([^"]+)"', t)
        if re.search(r":\s*SkillScript\b", t):
            skill_scripts.update(found)
        if re.search(r":\s*SpellScript\b", t):
            spell_scripts.update(found)

    def bound(kind, field):
        got = empty = 0
        names = skill_scripts if kind == "skills" else spell_scripts
        low = {n.lower() for n in names}
        for f in sorted((FORK / "database/server/templates" / kind).glob("*.json")):
            v = json.loads(f.read_text(encoding="utf-8-sig")).get(field)
            if v and v.lower() in low:
                got += 1
            else:
                empty += 1
        return got, empty

    skills_on, skills_off = bound("skills", "ScriptName")
    spells_on, spells_off = bound("spells", "ScriptKey")

    orig = json.loads((ROOT / "data/game-data/abilities.json").read_text(encoding="utf-8-sig"))
    matched = {"skill": 0, "spell": 0}
    total = {"skill": 0, "spell": 0}
    for a in orig:
        k = a["kind"]
        total[k] = total.get(k, 0) + 1
        pool = {n.lower() for n in (skill_scripts if k == "skill" else spell_scripts)}
        if a["name"].lower() in pool:
            matched[k] += 1

    spare_skill = sorted(s for s in skill_scripts if s.lower() not in {a["name"].lower() for a in orig})
    spare_spell = sorted(s for s in spell_scripts if s.lower() not in {a["name"].lower() for a in orig})

    (VAULT / "구현").mkdir(parents=True)
    (VAULT / "구현" / "기술·마법이 실제로 도는가.md").write_text(
        "---\n이름: \"기술·마법이 실제로 도는가\"\n갈래: 구현\n---\n\n"
        "# 기술·마법이 실제로 도는가\n\n"
        "**실린 것과 도는 것은 다르다.** 이름과 아이콘만 있으면 눌러도 아무 일이 없고, 그래도 개수는 맞는다.\n\n"
        f"| | 실린 것 | 스크립트 붙음 | 빈 껍데기 |\n|---|---|---|---|\n"
        f"| 기술 | {skills_on + skills_off} | **{skills_on}** | {skills_off} |\n"
        f"| 마법 | {spells_on + spells_off} | **{spells_on}** | {spells_off} |\n\n"
        "## 순서 — 하데스 먼저, 원작 다음, 팩은 안 본다\n\n"
        f"하데스는 기술 스크립트 **{len(skill_scripts)}개** · 마법 스크립트 **{len(spell_scripts)}개** 를 이미 갖고 있다.\n"
        "이름은 영문이고, `data/game-data/abilities.json` 의 원작 목록도 영문이다 — **같은 체계다.**\n"
        "(팩은 한글 이름이고 5.99 운영자 사본이다. 팩 이름으로 실으면 이 스크립트들과 안 붙는다.)\n\n"
        f"| | 원작 목록 | 이름이 그대로 붙는 것 |\n|---|---|---|\n"
        f"| 기술 | {total.get('skill', 0)} | **{matched['skill']}** |\n"
        f"| 마법 | {total.get('spell', 0)} | **{matched['spell']}** |\n\n"
        "## 원작 목록에 없는 하데스 스크립트\n\n"
        "이름이 안 맞는다고 쓸 수 없는 것이 아니다. **공용 스크립트**가 여기 있다 —\n"
        "`Generic Elemental Single` 과 `Generic Elemental Mass` 는 속성 공격 마법 전부를 덮을 수 있다.\n"
        "587개를 하나씩 쓰는 일이 아니라, 이미 있는 것에 **붙이는** 일이다.\n\n"
        f"- 기술 {len(spare_skill)}개: {', '.join(f'`{n}`' for n in spare_skill)}\n"
        f"- 마법 {len(spare_spell)}개: {', '.join(f'`{n}`' for n in spare_spell)}\n\n"
        "## 실제 서버에서 확인된 것\n\n"
        "`Assail` · `Kick` · `High Kick` · `Double Punch` · `Poison Punch` · `Sting` 이 피해를 주고, "
        "`beag ioc fein` 이 회복 메시지를 보내는 것까지 **7개**를 직접 확인했다.\n"
        f"붙어 있는 {skills_on + spells_on}개 중 나머지는 눌러 본 적이 없다.\n"
        "지키는 시험: `tests/hades-characterization/CombatSmokeTests.cs` ·\n"
        "`tests/hades-characterization/MobileClientProtocolTests.cs` ·\n"
        "`tests/hades-characterization/MonkLevelTenSkillTests.cs`\n",
        encoding="utf-8")

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
        + "\n\n## 실린 것과 도는 것은 다르다\n\n"
          "[[구현/기술·마법이 실제로 도는가|기술·마법이 실제로 도는가]] — 개수는 맞는데 눌러도 아무 일이\n"
          "없는 것이 대부분이다. **하데스 먼저, 원작 다음, 팩은 안 본다.**\n\n"
          "[[설정/LoruleConfig|식이 읽는 설정값]]\n\n"
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
