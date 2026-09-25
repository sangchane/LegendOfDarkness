using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 그룹 없이 혼자 사냥하던 사람에게 귀한 물건(등급 3 이상)이 떨어질 때.
/// </summary>
/// <remarks>
/// <para>
/// <c>Formulas/monsterexp.cs</c> 의 <c>GenerateDrops</c> 는 귀한 물건이 떨어지면 그룹원 모두에게 알리려고
/// <c>aisling.GroupParty.PartyMembers</c> 를 읽었다. 그룹이 없으면 <c>GroupParty</c> 가 null 이라 거기서 멈추고,
/// 물건은 땅에 놓이지도 않았다(파티 작업 중 찾음). 지금은 혼자면 잡은 사람만 듣는다.
/// </para>
/// <para>
/// 서버 설정은 등급을 끄고 있다(<c>UseLoruleItemRarity: false</c>) — 그래서 지금 플레이에선 이 길이 잠들어 있다.
/// 켜는 날 터지지 않게, 격리 서버에서만 켜고 본다. 등급 3 이상이 뽑힐 확률이 한 마리에 몇 % 라서, 체력 1 짜리를
/// 바로바로 세워 여러 번 잡는다.
/// </para>
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class RareDropTests : IDisposable
{
    private const int MonsterRoom = 20015;

    private static readonly Tile Start = new(2, 35);

    private static readonly Tile TargetTile = new(2, 34);

    /// <summary><c>LootQualifer.Table | Gold</c> — 목록에서 뽑고 등급을 굴리는 갈래.</summary>
    private const int LootTableAndGold = (1 << 2) | (1 << 5);

    /// <summary>귀한 물건 알림(<c>monsterexp.cs</c>).</summary>
    private const string RareNotice = "귀한 물건이 떨어졌습니다";

    private const string Name = "rarealone";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(9));
    private IsolatedHadesServer? _server;

    public void Dispose()
    {
        _deadline.Dispose();
        _server?.Dispose();
    }

    [Fact]
    public async Task A_rare_drop_to_someone_without_a_group_is_announced_to_them()
    {
        WorldClient world = await Enter();
        List<string> told = [];

        for (int swings = 0; swings < 150 && !told.Any(t => t.Contains(RareNotice, StringComparison.Ordinal));)
        {
            while (world.TakeTold(out _, out string text))
            {
                told.Add(text);
            }

            if (!world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == TargetTile))
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(300, _deadline.Token);
                continue;
            }

            await world.AttackAsync(_deadline.Token);
            swings++;
            await Task.Delay(600, _deadline.Token);
        }

        while (world.TakeTold(out _, out string rest))
        {
            told.Add(rest);
        }

        Assert.True(told.Any(t => t.Contains(RareNotice, StringComparison.Ordinal)),
            $"혼자 잡아서는 귀한 물건 알림을 한 번도 못 받았습니다(그룹이 없어 멈췄을 수 있습니다). " +
            $"받은 말 {told.Count}줄: {string.Join(" | ", told.TakeLast(8))}\n" +
            Tail(_server!.ConsoleOutput));
    }

    private async Task<WorldClient> Enter()
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        _server = server;
        TurnRarityOn(server);
        StandOneAtTheDoor(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.", _deadline.Token);

        // 문 앞(북쪽)을 본다 — 괴물은 늘 그 칸에 선다.
        await world.TurnAsync(Direction.North, _deadline.Token);
        return world;
    }

    private static void TurnRarityOn(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        string text = File.ReadAllText(path);
        string on = System.Text.RegularExpressions.Regex.Replace(
            text, "\"UseLoruleItemRarity\"\\s*:\\s*false", "\"UseLoruleItemRarity\": true");

        Assert.NotEqual(text, on);
        File.WriteAllText(path, on);
    }

    /// <summary>방의 정의는 모두 재우고, 체력 1 짜리 하나를 문 앞칸에 바로바로 세운다. 목록엔 쿠룸 하나.</summary>
    private static void StandOneAtTheDoor(IsolatedHadesServer server)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        JsonNode? target = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);

            if (!text.Contains($"\"AreaID\": {MonsterRoom}", StringComparison.Ordinal))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            target ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        Assert.NotNull(target);

        target["Name"] = "귀한드랍시험표적";
        target["SpawnType"] = 4; // Defined
        target["SpawnRate"] = 1;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;
        target["PathQualifer"] = 2; // Fixed
        target["MoodType"] = 1; // Idle
        target["Grow"] = false;
        target["MaximumHP"] = 1;
        target["Exp"] = 1;
        target["LootType"] = LootTableAndGold;
        target["Drops"] = new JsonObject { ["$values"] = new JsonArray("쿠룸") };

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "rare-drop-target.json"), target.ToJsonString(indented));
    }

    private static string Tail(string console) =>
        string.Join('\n', console.Split('\n').Where(l => l.Contains("Exception", StringComparison.Ordinal)).TakeLast(5));
}
