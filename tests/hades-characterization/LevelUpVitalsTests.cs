using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 레벨이 오를 때 최대 체력·마력이 얼마나 느는가.
/// </summary>
/// <remarks>
/// <para>
/// 5.99 서버(<c>Novaonline.exe</c>)의 레벨업(<c>0x469a46</c>~<c>0x469a94</c>)은 기본 최대 체력(캐릭터 <c>+0xB0</c>)에
/// <b>콘(+0xA3) + 30</b>, 기본 최대 마력(<c>+0xB4</c>)에 <b>위즈(+0xA2) + 25</b> 를 더한다. 직업·무작위는 없다.
/// 혼든 팩 서버(<c>Yuki.exe</c> <c>0x45fa29</c>~<c>0x45fa46</c>)도 같은 식이다.
/// </para>
/// <para>
/// 하데스는 <c>5 × 콘 × 0.65</c>, <c>5 × 위즈 × 0.45</c> 였다 — 콘·위즈가 낮은 초반에는 원작의 절반도 안 된다
/// (콘 5 에 16, 위즈 5 에 11 — 원작은 35 · 30).
/// </para>
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class LevelUpVitalsTests : IDisposable
{
    /// <summary>우드랜드1-1 — <see cref="MonsterGoldTests" /> 와 같은 방·같은 문 앞칸.</summary>
    private const int MonsterRoom = 20015;

    private static readonly Tile Start = new(2, 35);

    private static readonly Tile TargetTile = new(2, 34);

    private const int SpawnDefined = 4;
    private const int PathFixed = 2;
    private const int MoodIdle = 1;
    private const int MostSwings = 120;

    /// <summary>하데스 시작값(10/5/5/5/5)과 다른 값으로 둬, 식이 콘·위즈를 제대로 읽는지 본다.</summary>
    private const int Con = 7;

    private const int Wis = 4;

    private const string Name = "levelup";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_level_adds_con_plus_thirty_health_and_wis_plus_twenty_five_mana()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (MonsterRoom, Start.X, Start.Y));
        StandOneAtTheDoor(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Name);

        // 한 마리로 꼭 한 레벨만 오르게: 다음 레벨까지 1, 그 뒤 목표는 3레벨이라 한 마리로는 못 닿는다.
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["ExpLevel"] = 1;
        character["ExpNext"] = 1;
        character["_Con"] = Con;
        character["_Wis"] = Wis;
        File.WriteAllText(saved, character.ToJsonString());

        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        using WorldSession held = session;

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State is not null && world.Vitals is not null, "세계에 들어가지 못했습니다.");
        await Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "문 앞에 괴물이 서지 않았습니다.");
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        Vitals before = Mine(world);
        Assert.Equal(1, before.Level);

        await SwingUntil(world, enough: () => Mine(world).Level > before.Level);

        Vitals after = Mine(world);
        Assert.True(after.Level == 2, $"{MostSwings}번 휘둘렀는데 레벨이 {after.Level} 입니다. 서버가 한 말: {world.Said}");
        Assert.Equal(Con + 30, after.MaximumHealth - before.MaximumHealth);
        Assert.Equal(Wis + 25, after.MaximumMana - before.MaximumMana);
    }

    /// <summary>방의 정의 하나만 문 앞칸에 가만히 세운다 — <see cref="MonsterGoldTests" /> 와 같은 모양.</summary>
    private static void StandOneAtTheDoor(IsolatedHadesServer server)
    {
        JsonSerializerOptions indented = new() { WriteIndented = true };
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");

        (string Path, JsonNode Template)[] room =
        [
            .. Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Select(path => (path, text: File.ReadAllText(path)))
                .Where(file => file.text.Contains($"\"AreaID\": {MonsterRoom}", StringComparison.Ordinal))
                .Select(file => (file.path, JsonNode.Parse(file.text, documentOptions: lenient)!))
        ];

        Assert.NotEmpty(room);

        JsonNode target = room.MinBy(definition => (int?)definition.Template["MaximumHP"] ?? 0).Template.DeepClone();

        foreach ((string path, JsonNode template) in room)
        {
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString(indented));
        }

        target["Name"] = "레벨업시험표적";
        target["SpawnType"] = SpawnDefined;
        target["SpawnRate"] = 60;
        target["SpawnMax"] = 1;
        target["DefinedX"] = TargetTile.X;
        target["DefinedY"] = TargetTile.Y;
        target["PathQualifer"] = PathFixed;
        target["MoodType"] = MoodIdle;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "level-up-target.json"), target.ToJsonString(indented));
    }

    private async Task SwingUntil(WorldClient world, Func<bool> enough)
    {
        for (int swings = 0; swings < MostSwings && !enough();)
        {
            if (Nearest(world) is not { } goal || world.State is not { } before)
            {
                await world.RefreshAsync(_deadline.Token);
                await Task.Delay(200, _deadline.Token);
                continue;
            }

            int dx = goal.X - before.Where.X, dy = goal.Y - before.Where.Y;
            Direction step = Math.Abs(dx) >= Math.Abs(dy)
                ? (dx >= 0 ? Direction.East : Direction.West)
                : (dy >= 0 ? Direction.South : Direction.North);

            await world.WalkAsync(step, _deadline.Token);
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(120, _deadline.Token);

            if (world.State is not { } after || !IsThere(world, Ahead(after.Where, step)))
            {
                continue;
            }

            await world.AttackAsync(_deadline.Token);
            swings++;
            await Task.Delay(600, _deadline.Token);
        }
    }

    private static Tile Ahead(Tile from, Direction facing) => facing switch
    {
        Direction.North => new Tile(from.X, from.Y - 1),
        Direction.South => new Tile(from.X, from.Y + 1),
        Direction.East => new Tile(from.X + 1, from.Y),
        _ => new Tile(from.X - 1, from.Y)
    };

    private static bool IsThere(WorldClient world, Tile tile) =>
        world.Creatures.Any(c => c.Kind == CreatureKind.Hostile && c.Where == tile);

    private static Tile? Nearest(WorldClient world)
    {
        if (world.State is not { } me)
        {
            return null;
        }

        return world.Creatures
            .Where(c => c.Kind == CreatureKind.Hostile)
            .OrderBy(c => Math.Abs(c.Where.X - me.Where.X) + Math.Abs(c.Where.Y - me.Where.Y))
            .Select(c => (Tile?)c.Where)
            .FirstOrDefault();
    }

    private static Vitals Mine(WorldClient world) =>
        world.Vitals ?? throw new InvalidOperationException("서버가 내 수치를 말하지 않았습니다.");

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
