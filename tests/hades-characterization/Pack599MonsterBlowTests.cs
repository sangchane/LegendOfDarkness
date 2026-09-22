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
/// 괴물 평타 한 대가 5.99 와 같은 순서로 셈해지는가. 5.99 서버(Novaonline.exe 0x4258a4)는 굴린 공격력을 사람
/// 방어로 먼저 거르고(0x425d6e → 0x415173) 그 뒤 괴물의 공격속성으로 ×1.3 한다(0x425dc3 → 0x415cff). 속성이
/// 안 적힌 괴물에게도 생길 때 속성을 붙이므로(0x422bc5) 괴물 평타는 늘 ×1.3 이다.
/// </summary>
/// <remarks>
/// 굴림이 한 값만 나오도록 최소·최대 공격력을 같게 적은 괴물 하나를 앞칸에 세우고, 내 체력(0x08, 점수)이
/// 줄어드는 것을 본다. 우드랜드1-1 은 넓어서 정의 하나가 세 마리까지 서므로(<c>MonolithComponent</c>) 한 틱에
/// 두 마리가 함께 치면 두 대가 한 번에 보인다 — 그래서 줄어든 값이 모두 한 대의 배수이고 한 대 그대로인 것이
/// 적어도 하나 있는지를 본다.
/// </remarks>
public sealed class Pack599MonsterBlowTests : IDisposable
{
    private const string Name = "blowcheck";
    private const int WoodlandOneOne = 20015;
    private const int Blow = 20;

    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_monster_blow_is_multiplied_by_its_attack_element_after_our_armour()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneStrikerAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        MakeSturdy(server);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);
        while (!(world.State?.Where == Start && world.Creatures.Any(c => c.Where == Ahead)))
        {
            Assert.True(DateTime.UtcNow < giveUp, "우드랜드1-1 입구 앞칸에 괴물이 서지 않았습니다.");
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
        }

        // 괴물을 마주 본다. 괴물이 나와 같은 쪽을 보면(= 등 뒤) 평타가 두 배라 그 경우를 피한다.
        await world.TurnAsync(Direction.North, _deadline.Token);

        int armour = world.Vitals!.Armor;
        int armoured = Math.Max(1, Blow * (armour + 101) / 99);
        int expected = armoured * 13 / 10;

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

        Assert.True(drops.Count > 0, "15초를 서 있었는데 괴물이 한 번도 치지 않았습니다.");
        Assert.True(
            drops.All(drop => drop % expected == 0) && drops.Contains(expected),
            $"공격력 {Blow} 의 한 대가 방어 {armour} 를 거쳐 {armoured}, ×1.3 해서 {expected} 이어야 합니다. " +
            $"줄어든 값: {string.Join(", ", drops)}");
    }

    private static void MakeSturdy(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        // 여러 대를 맞아도 쓰러지지 않게. 괴물의 한 방은 내 체력을 읽지 않는다.
        saved["_MaximumHp"] = 100_000;
        saved["CurrentHp"] = 100_000;
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void StandOneStrikerAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonNode? striker = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!woodland.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            striker ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString());
        }

        Assert.NotNull(striker);
        striker["Name"] = "평타배수시험괴물";
        striker["BaseName"] = "평타배수시험괴물";
        striker["SpawnMax"] = 1;
        striker["SpawnType"] = 4;
        striker["SpawnRate"] = 1;
        striker["DefinedX"] = Ahead.X;
        striker["DefinedY"] = Ahead.Y;
        striker["MaximumHP"] = 1_000_000;
        striker["DmgMin"] = Blow;
        striker["DmgMax"] = Blow;
        striker["MoodType"] = 2;
        striker["PathQualifer"] = 2;
        striker["Grow"] = false;
        striker["SpellScripts"] = null;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "pack599-monster-blow.json"), striker.ToJsonString());
    }
}
