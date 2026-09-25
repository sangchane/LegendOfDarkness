using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 떠오르는 숫자(0x5D, 우리 확장) — 괴물을 치면 뺀 만큼이, 쿠로토를 외우면 채운 만큼이 온다. 원작은 0x13 백분율뿐이다.
/// </summary>
public sealed class FloatingFigureTests : IDisposable
{
    private const string Name = "figures";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Hitting_a_monster_sends_what_the_blow_took_and_kuroto_sends_what_it_healed()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        PutTargetAhead(server);
        MakeGameMaster(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["_MaximumHp"] = 1000;
            saved["CurrentHp"] = 500;
            saved["_MaximumMp"] = 1000;
            saved["CurrentMp"] = 1000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Until(() => world.State is { Map.Id: WoodlandOneOne, Where: var where } && where == Start,
            "우드랜드1-1 입구에 서지 못했습니다.");
        await Until(() => world.Creatures.Any(creature => creature.Where == Ahead), "앞칸에 표적이 나타나지 않았습니다.");
        uint target = world.Creatures.First(creature => creature.Where == Ahead).Serial;

        List<Figure> figures = [];

        void Drain()
        {
            while (world.TakeFigure(out Figure? figure))
            {
                figures.Add(figure);
            }
        }

        // 공격 단추(0x13 → Assail). 빗나갈 수 있어 맞을 때까지 1초마다 휘두른다.
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        Figure? blow = null;

        while (blow is null && DateTime.UtcNow < giveUp)
        {
            await world.AttackAsync(_deadline.Token);
            await Task.Delay(1100, _deadline.Token);
            Drain();
            blow = figures.FirstOrDefault(figure => figure.Target == target);
        }

        Assert.NotNull(blow);
        Assert.Equal(FigureKind.Damage, blow.Kind);
        Assert.Equal(world.Serial, blow.Source);
        Assert.True(blow.Amount > 0, $"뺀 만큼이 0 입니다: {blow}");

        // 쿠로토 — 5.99 표 그대로 지혜 × 5(최대 300)를 자기에게 채운다. 체력이 반이라 다 들어간다.
        await world.SayAsync("/spell \"쿠로토\" 1", _deadline.Token);
        int slot = await Slot(() => world.Spells.FirstOrDefault(s => s.Name.StartsWith("쿠로토"))?.Slot, "쿠로토");
        await Until(() => world.Vitals is not null, "체력을 받지 못했습니다.");
        int health = world.Vitals!.Health;
        figures.Clear();

        await world.UseSpellAsync(slot, 0, _deadline.Token);

        Figure? heal = null;
        await Until(() =>
        {
            Drain();
            heal = figures.FirstOrDefault(figure => figure.Target == world.Serial && figure.Kind == FigureKind.Heal);
            return heal is not null;
        }, $"쿠로토가 채운 만큼을 보내지 않았습니다: {string.Join(", ", figures)} · 서버: {world.Said}");

        Assert.True(heal!.Amount > 0);
        // 쿠로토는 100 을 채운다(사용자 2026-09-25: "체력 100 정도 회복") — 상한에 걸리면 모자란 만큼만.
        Assert.Equal(Math.Min(100, world.Vitals!.MaximumHealth - health), heal.Amount);
        await Until(() => world.Vitals!.Health == health + heal.Amount,
            $"쿠로토 숫자 {heal.Amount} 가 실제로 오른 체력({health} → {world.Vitals!.Health})과 다릅니다.");
    }

    private static void PutTargetAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\\\"AreaID\\\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "숫자시험표적";
        target["BaseName"] = "숫자시험표적";
        target["AreaID"] = WoodlandOneOne;
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = Ahead.X;
        target["DefinedY"] = Ahead.Y;
        target["MaximumHP"] = 1_000_000;
        target["Ac"] = 0;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "figure-target.json"),
            target.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void MakeGameMaster(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(Name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private async Task<int> Slot(Func<int?> find, string name)
    {
        int? slot = null;
        await Until(() => (slot = find()) is not null, $"{name}이 창에 오지 않았습니다.");
        return slot!.Value;
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure);
    }
}
