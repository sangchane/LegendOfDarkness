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
/// 체력이 0 이 되면 — 5.99·Novaonline <c>__SCRIPT_DEAD__</c>(<c>data/server-packs/novaonline/db/script/script.txt</c>):
/// 무리가 있으면(<c>group_exist</c>) 혼수(<c>set_coma</c> · <c>coma_delay 15</c>) — 그 사이 무리가 살릴 수 있다.
/// 무리가 없으면 혼수 없이 바로 뮤레칸의방 10,9 로(<c>warp "뮤레칸의방", 10, 9</c> · <c>char_dead</c>).
/// 혼수 동안에는 괴물이 치지 않고 아무 피해도 들어가지 않는다(원작 규칙, 사용자 확인 2026-09-24).
/// 오솔길 함정의 혼수는 죽음이 아니라 그대로다 — <see cref="PoteDungeonTests" />.
/// </summary>
/// <remarks>
/// 앞칸(2,34)에 한 방에 쓰러뜨리는 비선공 괴물 하나를 세우고, 체력 100 인 사람이 먼저 친다. 비선공은 맞으면 그 자리에서 선공이 된다.
/// 선공이면 무리를 짓기 전에 쓰러뜨려 버리므로 비선공으로 둔다.
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class ComaTests : IDisposable
{
    private const int WoodlandOneOne = 20015;
    private const int MurekansRoom = 20138;
    private const int ComaIcon = 89;

    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);
    private static readonly Tile Beside = new(3, 35);

    /// <summary>무리 친구가 서 있는 곳 — 괴물 앞칸이 아니다. 혼수인 사람을 건너뛰면 괴물은 이쪽을 노린다.</summary>
    private static readonly Tile Aside = new(2, 38);

    /// <summary>하데스 빈사 문구(LoruleConfig ReapMessage) — 이 가운데 하나가 뜨면 혼수다.</summary>
    private static readonly string[] Dying =
        ["죽어 가고 있습니다.", "움직일 수도, 팔을 들 수도 없습니다.", "바론이 당신의 영혼을 거두러 옵니다.", "모든 것에는 끝이 있는 법입니다."];

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Killed_alone_goes_straight_to_murekan_without_a_coma()
    {
        const string who = "comasolo";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneKillerAhead(server);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, who);
        Save(server, who, saved =>
        {
            saved["_MaximumHp"] = 100;
            saved["CurrentHp"] = 100;
        });

        using WorldSession session = await Login(server, who);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await ArriveBeforeKiller(world);

        bool comaIconSeen = false;
        await world.TurnAsync(Direction.North, _deadline.Token);
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (world.State?.Map.Id != MurekansRoom && DateTime.UtcNow < giveUp)
        {
            comaIconSeen |= world.Ailments.Any(ailment => ailment.Icon == ComaIcon && ailment.Left > 0);
            if (world.State?.Map.Id == WoodlandOneOne)
                await world.AttackAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        Assert.True(world.State?.Map.Id == MurekansRoom,
            $"무리 없이 쓰러졌는데 뮤레칸의방으로 가지 않았습니다. 마지막: {world.State} · 서버가 한 말: {world.Said}");
        await Waiting.Until(() => world.State?.Where == new Tile(10, 9), $"뮤레칸의방 10,9 에 서지 않았습니다: {world.State}", _deadline.Token);
        Assert.False(comaIconSeen, "무리 없이 쓰러졌는데 혼수 아이콘(89)이 떴습니다.");
        Assert.DoesNotContain(Dying, line => world.Said.Contains(line));
        Assert.Contains("죽었습니다.", world.Said);
    }

    [Fact]
    public async Task Killed_in_a_group_falls_into_a_fifteen_second_coma_nobody_can_hurt_then_dies()
    {
        const string victim = "comavictim";
        const string friend = "comafriend";
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneKillerAhead(server, nibblerBeside: true);
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, victim);
        LoginFlow.TryCreateAccount(server, friend);
        Save(server, victim, saved =>
        {
            saved["_MaximumHp"] = 100;
            saved["CurrentHp"] = 100;
        });
        Save(server, friend, saved =>
        {
            saved["X"] = Aside.X;
            saved["Y"] = Aside.Y;
            saved["_MaximumHp"] = 1_000_000;
            saved["CurrentHp"] = 1_000_000;
        });

        using WorldSession victimSession = await Login(server, victim);
        WorldClient hurt = new(victimSession);
        _ = hurt.PumpAsync(_deadline.Token);
        using WorldSession friendSession = await Login(server, friend);
        WorldClient mate = new(friendSession);
        _ = mate.PumpAsync(_deadline.Token);

        await ArriveBeforeKiller(hurt);
        await Waiting.Until(() => mate.State?.Where == Aside, $"친구가 {Aside} 에 서지 않았습니다: {mate.State}", _deadline.Token);

        // 무리를 짓는다(PartyTests 와 같은 흐름). 들어온 직후엔 서버가 청을 버리므로 물어 올 때까지 다시 청한다.
        string? asker = null;
        for (int tries = 0; tries < 15 && asker is null; tries++)
        {
            await mate.AskToGroupAsync(victim, _deadline.Token);
            try
            {
                await Waiting.Until(() => hurt.TakeAsk(out asker), "0x63", _deadline.Token, TimeSpan.FromSeconds(1));
            }
            catch (TimeoutException)
            {
            }
        }

        Assert.Equal(friend, asker);
        await hurt.AcceptGroupAsync(friend, _deadline.Token);
        int rosterBefore = hurt.RosterCount;
        await hurt.AskProfileAsync(_deadline.Token);
        await Waiting.Until(() => hurt.RosterCount > rosterBefore && hurt.Roster.Grouped, "무리가 지어지지 않았습니다.", _deadline.Token);

        // 먼저 쳐서 괴물을 깨운다 — 한 방에 쓰러진다.
        await hurt.TurnAsync(Direction.North, _deadline.Token);
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (!InComa(hurt) && DateTime.UtcNow < giveUp)
        {
            await hurt.AttackAsync(_deadline.Token);
            await Task.Delay(300, _deadline.Token);
        }

        Assert.True(InComa(hurt), $"무리 안에서 쓰러졌는데 혼수에 빠지지 않았습니다. 마지막: {hurt.State} · 체력 {hurt.Vitals?.Health} · 서버가 한 말: {hurt.Said}");
        DateTime comaAt = DateTime.UtcNow;
        Assert.Equal(WoodlandOneOne, hurt.State?.Map.Id);

        // 혼수 동안: 체력이 그대로이고 나를 향한 피해 숫자(0x5D)가 오지 않는다. 곁의 물기 괴물은 혼수인 사람을 두고 무리 친구를 노린다.
        await Task.Delay(500, _deadline.Token);
        while (hurt.TakeFigure(out _))
        {
        }

        int comaHealth = hurt.Vitals!.Health;
        List<Figure> onMe = [];
        List<int> healths = [];
        while (DateTime.UtcNow - comaAt < TimeSpan.FromSeconds(11))
        {
            while (hurt.TakeFigure(out Figure? figure))
            {
                if (figure.Target == hurt.Serial && figure.Kind == FigureKind.Damage)
                    onMe.Add(figure);
            }

            healths.Add(hurt.Vitals!.Health);
            await Task.Delay(100, _deadline.Token);
        }

        Assert.Empty(onMe);
        Assert.All(healths, health => Assert.Equal(comaHealth, health));
        Assert.Equal(WoodlandOneOne, hurt.State?.Map.Id);

        // 15초(coma_delay 15)가 지나면 죽어 뮤레칸의방으로 간다.
        await Waiting.Until(() => hurt.State?.Map.Id == MurekansRoom,
            $"혼수가 끝나도 뮤레칸의방으로 가지 않았습니다. 마지막: {hurt.State} · 서버가 한 말: {hurt.Said}", _deadline.Token, TimeSpan.FromSeconds(20));
        double comaSeconds = (DateTime.UtcNow - comaAt).TotalSeconds;
        Assert.True(comaSeconds is > 12 and < 19, $"무리 혼수 15초가 {comaSeconds:0.0}초 걸렸습니다.");
        await Waiting.Until(() => hurt.State?.Where == new Tile(10, 9), $"뮤레칸의방 10,9 에 서지 않았습니다: {hurt.State}", _deadline.Token);
    }

    private static bool InComa(WorldClient world) =>
        world.Ailments.Any(ailment => ailment.Icon == ComaIcon && ailment.Left > 0) || Dying.Any(line => world.Said.Contains(line));

    private async Task<WorldSession> Login(IsolatedHadesServer server, string who) =>
        await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

    private async Task ArriveBeforeKiller(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(100);
        while (!(world.State?.Where == Start && world.Creatures.Any(c => c.Where == Ahead)))
        {
            Assert.True(DateTime.UtcNow < giveUp, "우드랜드1-1 입구 앞칸에 괴물이 서지 않았습니다.");
            await world.RefreshAsync(_deadline.Token);
            await Task.Delay(400, _deadline.Token);
        }
    }

    private static void Save(IsolatedHadesServer server, string who, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{who}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    /// <summary>우드랜드1-1 괴물을 모두 내리고, 한 방에 쓰러뜨리는 비선공 괴물 하나만 앞칸에 세운다(Pack599MonsterBlowTests 와 같은 자리).</summary>
    /// <remarks>
    /// <paramref name="nibblerBeside" /> 는 옆칸(3,35)에 한 대 1 짜리 선공 괴물을 하나 더 세운다. 혼수인 사람 곁에 붙어 서서 계속 노리는 괴물이라,
    /// 혼수 동안 피해가 들어가지 않는지(무적)와 괴물이 혼수인 사람을 목표에서 빼는지(표적)를 함께 본다 — 쓰러뜨린 괴물은 죽인 뒤 떠나 버린다.
    /// </remarks>
    internal static void StandOneKillerAhead(IsolatedHadesServer server, bool nibblerBeside = false)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonNode? killer = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!woodland.IsMatch(text))
                continue;

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            killer ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString());
        }

        Assert.NotNull(killer);
        killer["Name"] = "혼수시험괴물";
        killer["BaseName"] = "혼수시험괴물";
        killer["SpawnMax"] = 1;
        killer["SpawnType"] = 4;
        killer["SpawnRate"] = 1;
        killer["DefinedX"] = Ahead.X;
        killer["DefinedY"] = Ahead.Y;
        killer["MaximumHP"] = 1_000_000;
        killer["DmgMin"] = 1000;
        killer["DmgMax"] = 1000;
        // 비선공 · 제자리 고정(2)은 맞아도 걷기 차례가 꺼진 채라 되받아치지 않는다(CommonMonster.Walk 에서만 친다).
        // 그래서 떠돌이(1)로 두되 평소 걸음을 10분으로 늘려 앞칸에 붙여 두고, 맞은 뒤(교전 걸음)에만 움직이게 한다.
        killer["MoodType"] = 1;
        killer["PathQualifer"] = 1;
        killer["MovementSpeed"] = 600_000;
        killer["EngagedWalkingSpeed"] = 500;
        killer["Grow"] = false;
        killer["SpellScripts"] = null;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "coma-killer.json"), killer.ToJsonString());

        if (!nibblerBeside)
            return;

        JsonNode nibbler = killer.DeepClone();
        nibbler["Name"] = "혼수시험물기";
        nibbler["BaseName"] = "혼수시험물기";
        nibbler["DefinedX"] = Beside.X;
        nibbler["DefinedY"] = Beside.Y;
        nibbler["DmgMin"] = 1;
        nibbler["DmgMax"] = 1;
        nibbler["MoodType"] = 2;
        nibbler["PathQualifer"] = 2;
        nibbler["MovementSpeed"] = 500;
        File.WriteAllText(Path.Combine(testFolder, "coma-nibbler.json"), nibbler.ToJsonString());
    }
}
