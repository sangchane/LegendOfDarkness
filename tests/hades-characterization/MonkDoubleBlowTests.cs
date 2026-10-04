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
