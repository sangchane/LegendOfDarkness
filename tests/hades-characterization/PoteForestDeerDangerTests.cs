using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 포테의숲 사슴("사슴 몬스터 너무 강하던데" 사용자 보고, 2026-09-26) — 원래 수치(체력5200·공격력150~160·방어-45,
/// 5.99 팩 <c>mob/Fota/Fota_Monster.txt</c> 와 완전히 같다, <c>database/server/templates/monsters/5.99/사슴@포테의숲1존.json</c>)를
/// 그대로 둔 채, 입장레벨(21)과 그 다음(30) 무도가가 기본 장비(도복·화염의목걸이 — 실 캐릭터 monk4.json 그대로)로
/// 한 대에 얼마를 받는지 잰다. 식은 <c>docs/monster-behaviour.md</c>·<see cref="Pack599MonsterBlowTests"/> 와 같다 —
/// 방어를 먼저 거르고 괴물의 공격속성(늘 ×1.3)을 곱한다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class PoteForestDeerDangerTests : IDisposable
{
    private const int ForestOne = 20263;
    private const string DeerName = "사슴";

    // 5.99 warp/Suomi_Warp.txt 그대로: 수오미마을 99,24~27 을 밟으면 여기(33,47)다.
    private static readonly Tile Start = new(33, 47);
    private static readonly Tile Ahead = new(33, 46);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Theory]
    [InlineData(21)]
    [InlineData(30)]
    public async Task A_real_deer_hits_a_starter_geared_monk_within_the_packs_own_numbers(int level)
    {
        string name = $"deercheck{level}";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (ForestOne, Start.X, Start.Y));
        FixTheOneRealDeerAheadAndClearTheRest(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, name);
        MakeStarterGearedMonk(server, name, level);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);
        while (!(world.State?.Where == Start && world.Creatures.Any(c => c.Where == Ahead)))
        {
            Assert.True(DateTime.UtcNow < giveUp, "포테의숲1존 입구 앞칸에 사슴이 서지 않았습니다.");
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
        }

        // 몬스터가 때리는 배수는 공격자가 Aisling 일 때만 자세를 보므로(Sprite.BlowFacing) 내 방향은 결과에 안 걸리지만,
        // Pack599MonsterBlowTests 와 같은 자연스러운 자세로 선다.
        await world.TurnAsync(Direction.North, _deadline.Token);

        int armour = world.Vitals!.Armor;
        int expectedMin = Math.Max(1, 150 * (armour + 101) / 99) * 13 / 10;
        int expectedMax = Math.Max(1, 160 * (armour + 101) / 99) * 13 / 10;

        List<int> drops = [];
        int seen = world.Vitals!.Health;
        DateTime until = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < until)
        {
            int now = world.Vitals!.Health;
            if (now < seen)
            {
                drops.Add(seen - now);
            }

            seen = now;
            await Task.Delay(1, _deadline.Token);
        }

        Assert.True(drops.Count > 0, "15초를 서 있었는데 사슴이 한 번도 치지 않았습니다.");
        Assert.True(
            drops.All(drop => drop >= expectedMin && drop <= expectedMax),
            $"레벨 {level}, 방어 {armour}: 사슴 한 대는 {expectedMin}~{expectedMax} 여야 합니다. 실제로 줄어든 값: {string.Join(", ", drops)}");
    }

    /// <summary>실 캐릭터 <c>monk4.json</c> 의 장비를 그대로 옮긴 것 — "기본 장비"(도복·화염의목걸이, 둘 다 레벨1 요구,
    /// 무기 없음). <c>Item.Template.Name</c> 이 실제 아이템표와 같아 로그인 때 서버가 제 정의로 다시 채우고
    /// (<c>GameClient.LoadEquipment</c>) 방어 보너스를 그대로 건다(<c>Armor.Equipped</c>·<c>Necklace.Equipped</c> →
    /// <c>Item.ApplyModifers</c>).</summary>
    private const string ArmorEquipmentJson = """
    {
      "Item": {
        "X": 31, "Y": 29, "Color": 0, "Cursed": false, "DisplayImage": 32866, "Durability": 2841,
        "Identifed": false, "Image": 3, "ItemVariance": 0, "Owner": 456831163, "Slot": 1, "Stacks": 0,
        "Template": {
          "AcModifer": { "Option": 1, "Value": 10 },
          "AttackMotion": 132, "AttackSpeed": 0, "HealthRestore": 0, "ManaRestore": 0,
          "RecallArea": 0, "RecallX": 0, "RecallY": 0, "CanStack": false, "CarryWeight": 4,
          "Class": 5, "Color": 0, "ConModifer": null, "DefenseElement": 0, "DexModifer": null,
          "DisplayImage": 32866, "DmgMax": 0, "DmgMin": 0, "DmgModifer": null, "DropRate": 0.0,
          "Enchantable": false, "EquipmentSlot": 2, "Flags": 125, "Gender": 1, "HasPants": false,
          "HealthModifer": null, "HitModifer": null, "Image": 3, "IntModifer": null,
          "LevelRequired": 1, "ManaModifer": null, "MaxDurability": 3000, "MaxStack": 0,
          "MrModifer": null, "NpcKey": null, "OffenseElement": 0, "RegenModifer": null,
          "ScriptName": "Armor", "SpellOperator": null, "StageRequired": 0, "StrModifer": null,
          "Value": 850, "WeaponScript": null, "WisModifer": null, "Description": null,
          "Group": "5.99표/갑옷/무도가방어구", "Name": "도복"
        },
        "Type": null, "Upgrades": 0, "Warnings": [false, false, false], "Serial": 1404385796,
        "CurrentMapId": 20015, "_Str": 0, "_Int": 0, "_Wis": 0, "_Con": 0, "_Dex": 0, "_Dmg": 0,
        "_Hit": 0, "_Mr": 0, "_Regen": 0, "Amplified": 0, "OffenseElement": 0, "DefenseElement": 0,
        "Buffs": {}, "Debuffs": {}, "_MaximumHp": 0, "CurrentHp": 0, "_MaximumMp": 0, "CurrentMp": 0,
        "SpellReflect": false, "Direction": 0, "Immunity": false, "MajorAttribute": 0
      },
      "Slot": 2
    }
    """;

    private const string NecklaceEquipmentJson = """
    {
      "Item": {
        "X": 39, "Y": 95, "Color": 0, "Cursed": false, "DisplayImage": 32973, "Durability": 4988,
        "Identifed": false, "Image": 0, "ItemVariance": 0, "Owner": 635207243, "Slot": 3, "Stacks": 0,
        "Template": {
          "AcModifer": { "Option": 1, "Value": 1 },
          "AttackMotion": 1, "AttackSpeed": 0, "HealthRestore": 0, "ManaRestore": 0,
          "RecallArea": 0, "RecallX": 0, "RecallY": 0, "CanStack": false, "CarryWeight": 0,
          "Class": 0, "Color": 0, "ConModifer": null, "DefenseElement": 0, "DexModifer": null,
          "DisplayImage": 32973, "DmgMax": 0, "DmgMin": 0, "DmgModifer": null, "DropRate": 0.045,
          "Enchantable": false, "EquipmentSlot": 6, "Flags": 125, "Gender": 255, "HasPants": false,
          "HealthModifer": null, "HitModifer": null, "Image": 0, "IntModifer": null,
          "LevelRequired": 1, "ManaModifer": null, "MaxDurability": 5000, "MaxStack": 0,
          "MrModifer": null, "NpcKey": null, "OffenseElement": 0, "RegenModifer": null,
          "ScriptName": "Necklace", "SpellOperator": null, "StageRequired": 0, "StrModifer": null,
          "Value": 10000, "WeaponScript": null, "WisModifer": null, "Description": null,
          "Group": "5.99표/목걸이/공통목걸이", "Name": "화염의목걸이"
        },
        "Type": null, "Upgrades": 0, "Warnings": [false, false, false], "Serial": 807572243,
        "CurrentMapId": 20023, "_Str": 0, "_Int": 0, "_Wis": 0, "_Con": 0, "_Dex": 0, "_Dmg": 0,
        "_Hit": 0, "_Mr": 0, "_Regen": 0, "Amplified": 0, "OffenseElement": 0, "DefenseElement": 0,
        "Buffs": {}, "Debuffs": {}, "_MaximumHp": 0, "CurrentHp": 0, "_MaximumMp": 0, "CurrentMp": 0,
        "SpellReflect": false, "Direction": 0, "Immunity": false, "MajorAttribute": 0
      },
      "Slot": 6
    }
    """;

    private static void MakeStarterGearedMonk(IsolatedHadesServer server, string name, int level)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;

        saved["ExpLevel"] = level;
        saved["Path"] = 5; // Monk
        // 죽으면 피해 표본이 끊기므로 크게 잡는다 — 체력은 방어 식에 안 들어가 결과에 영향이 없다.
        saved["_MaximumHp"] = 100_000;
        saved["CurrentHp"] = 100_000;

        JsonNode equipment = saved["EquipmentManager"]!["Equipment"]!;
        equipment["2"] = JsonNode.Parse(ArmorEquipmentJson);
        equipment["6"] = JsonNode.Parse(NecklaceEquipmentJson);

        File.WriteAllText(path, saved.ToJsonString());
    }

    /// <summary>포테의숲1존의 사슴 정의 하나를 앞칸에 고정하고(체력·공격력·방어는 원래 값 그대로), 나머지 팜팻
    /// 정의는 젠을 꺼 표본에 섞이지 않게 한다.</summary>
    private static void FixTheOneRealDeerAheadAndClearTheRest(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex zoneOne = new($"\"AreaID\"\\s*:\\s*{ForestOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        bool foundDeer = false;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!zoneOne.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;

            if ((string?)template["Name"] == DeerName)
            {
                foundDeer = true;
                // 체력(5200)·공격력(150~160)·방어(-45)는 손대지 않는다 — 바로 그 값을 재는 시험이다.
                // SpawnRate 는 크게 둔다 — 짧게 두면 15초 표본 안에 MonolithComponent 가 같은 자리에 사슴을
                // 하나 더 세운다(넓이 2,500칸 → spread 6 → round(1×√6×0.7)=2, 원래 1이어야 할 자리가 2 —
                // PoteForestDeerSpawnCapTests 가 그 자체를 확인한다). 여기서는 한 대의 크기만 깨끗이 잰다.
                template["SpawnType"] = 4; // Defined
                template["SpawnRate"] = 100_000;
                template["DefinedX"] = Ahead.X;
                template["DefinedY"] = Ahead.Y;
                template["MoodType"] = 2; // 원래 4(스폰 때 동전 던지기) — 시험이 반반 확률에 흔들리지 않게 고정.
                template["PathQualifer"] = 2; // 원래 1(Wander) — 정해 둔 자리를 벗어나지 않게.
            }
            else
            {
                template["SpawnMax"] = 0;
            }

            File.WriteAllText(path, template.ToJsonString());
        }

        Assert.True(foundDeer, "포테의숲1존(20263) 사슴 정의를 찾지 못했습니다.");
    }
}
