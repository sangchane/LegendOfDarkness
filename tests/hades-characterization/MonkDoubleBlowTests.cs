using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 양의신권(2026-10-04) — 평타(0x13)는 배운 평타형 기술을 모두 쓴다. 무도가는 기본공격 + 양의신권이라 전에는 늘 두 번 쳤다.
/// 이제 양의신권은 30% 일 때만 한 번 더 친다 — 평타 마흔 번에 두 번째 타격이 가끔(늘도 아니고 없지도 않게) 나오는지 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class MonkDoubleBlowTests : IDisposable
{
    private const string Name = "monkdouble";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Yangui_singwon_adds_a_second_blow_only_sometimes()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        Waiting.MakeGameMaster(server, Name);
        PutTarget(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["Path"] = 5;
        character["ExpLevel"] = 30;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null && world.Creatures.Any(one => one.Where == Ahead),
            "앞칸에 표적이 서지 않았습니다.", _deadline.Token);

        if (!world.Skills.Any(one => one.Name.StartsWith("양의신권", StringComparison.Ordinal)))
        {
            await world.SayAsync("/skill \"양의신권\" 1", _deadline.Token);
            await Waiting.Until(() => world.Skills.Any(one => one.Name.StartsWith("양의신권", StringComparison.Ordinal)),
                "양의신권을 배우지 못했습니다.", _deadline.Token);
        }

        await world.TurnAsync(Direction.North, _deadline.Token);
        await Task.Delay(500, _deadline.Token);
        while (world.TakeFigure(out _))
        {
        }

        int presses = 40, doubles = 0, singles = 0;
        for (int press = 0; press < presses; press++)
        {
            await world.AttackAsync(_deadline.Token);
            await Task.Delay(900, _deadline.Token);

            int blows = 0;
            while (world.TakeFigure(out Figure? figure))
            {
                if (figure!.Source == world.Serial && figure.Kind == FigureKind.Damage)
                {
                    blows++;
                }
            }

            if (blows >= 2) doubles++;
            else if (blows == 1) singles++;
        }

        // 30% 에 마흔 번이면 평균 12 — 2~24 밖이면 확률이 틀렸다(늘 두 번이면 40, 없으면 0).
        Assert.True(singles + doubles >= presses * 3 / 4, $"평타가 잘 들어가지 않았습니다: 한 번 {singles} · 두 번 {doubles}");
        Assert.InRange(doubles, 2, 24);
    }

    /// <summary>
    /// 앞칸에 사람(결투 맵 아님)이 서 있어도 무도가 기술 그림이 쓴 사람 위에 그려지지 않는다 — 전에는 맞은 사람에게 0x29 를
    /// 칸 순서 거꾸로 한 번 더 보내 쓴 사람 위에도 그려졌다(사용자 2026-10-04). 사람은 결투 맵에서만 친다(원작).
    /// </summary>
    [Fact]
    public async Task A_person_ahead_does_not_draw_the_kick_on_the_kicker()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (WoodlandOneOne, Start.X, Start.Y));
        Waiting.MakeGameMaster(server, Name);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        LoginFlow.TryCreateAccount(server, "monkstander");
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["Path"] = 5;
        character["ExpLevel"] = 30;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession standerSession = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, "monkstander", LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient stander = new(standerSession);
        _ = stander.PumpAsync(_deadline.Token);
        await Waiting.Until(() => stander.State is not null, "앞에 설 사람이 들어가지 못했습니다.", _deadline.Token);

        // 둘 다 입구 칸에 선다 — 무도가가 한 칸 아래로 비켜 서서 북쪽을 보면 그 사람이 앞칸이다.
        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null, "무도가가 들어가지 못했습니다.", _deadline.Token);
        await Waiting.WalkUntil(world, Direction.South, () => world.State?.Where == new Tile(Start.X, Start.Y + 1), _deadline.Token);
        await Waiting.Until(() => world.Others.Any(one => one.Serial == stander.Serial && one.Where == Start),
            "앞칸에 사람이 보이지 않습니다.", _deadline.Token);

        await world.SayAsync("/skill \"단각\" 1", _deadline.Token);
        int? slot = null;
        await Waiting.Until(() => (slot = world.Skills.FirstOrDefault(one => one.Name.StartsWith("단각", StringComparison.Ordinal))?.Slot) is not null,
            "단각을 배우지 못했습니다.", _deadline.Token);
        await world.TurnAsync(Direction.North, _deadline.Token);
        await Task.Delay(500, _deadline.Token);
        while (world.TakeEffect(out _))
        {
        }

        await world.UseSkillAsync(slot!.Value, _deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        List<Effect> seen = [];
        while (world.TakeEffect(out Effect? effect))
        {
            seen.Add(effect!);
        }

        Assert.DoesNotContain(seen, one => one.Target == world.Serial && one.TargetAnimation > 0
                                          || one.Source == world.Serial && one.SourceAnimation > 0);
    }

    private static void PutTarget(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "양의신권시험표적";
        target["BaseName"] = "양의신권시험표적";
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = Ahead.X;
        target["DefinedY"] = Ahead.Y;
        target["MaximumHP"] = 1_000_000_000;
        target["Ac"] = 0;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "monk-double-target.json"), target.ToJsonString());
    }
}
