using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 괴물이 거는 소루마(빙결)에 걸리면 아무것도 못 한다 — 물약도, 앱 자동 물약도(사용자 2026-10-04). 그리고 괴물 마법은
/// 5.99 처럼 쫓거나 때린 차례마다 `SpellChance`% 로 쓴다(Novaonline.exe 0x40a576) — 하데스의 `CastSpeed`(8초)가 아니다.
/// </summary>
/// <remarks>
/// 우드랜드1-1 입구 앞칸에 <c>Monster_소루마</c> 만 쥔 괴물 하나를 세운다. 차례는 0.3초·확률 100%, <c>CastSpeed</c> 는 1분 —
/// 하데스 식으로 돌면 시험 안에 얼지 않는다.
/// </remarks>
public sealed class FrozenTests : IDisposable
{
    private const string Name = "frozenpot";
    private const int WoodlandOneOne = 20015;
    private const int FrozenIcon = 50;
    private const string Potion = "하급체력포션";

    private static readonly Tile Start = new(2, 35);
    private static readonly Tile Ahead = new(2, 34);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(4));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_frozen_player_cannot_drink_a_potion()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, Start.X, Start.Y));
        StandOneFreezerAhead(server);
        Waiting.MakeGameMaster(server, Name);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved =>
        {
            saved["_MaximumHp"] = 100_000;
            saved["CurrentHp"] = 1_000;
        });

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await world.SayAsync($"/give \"{Potion}\" 1", _deadline.Token);
        await Until(() => world.Pack.Any(item => item.Name == Potion), $"{Potion}이 소지품에 오지 않았습니다. 서버가 한 말: {world.Said}");

        await Until(() => world.Ailments.Any(ailment => ailment.Icon == FrozenIcon),
            $"괴물이 소루마를 걸지 않았습니다(SpellChance 차례). 상태: {string.Join(", ", world.Ailments)} · 서버가 한 말: {world.Said}");

        InventoryItem potion = world.Pack.First(item => item.Name == Potion);
        await world.UseAsync(potion.Slot, _deadline.Token);
        await Task.Delay(1500, _deadline.Token);

        Assert.True(world.Ailments.Any(ailment => ailment.Icon == FrozenIcon), "물약을 쓰는 동안 빙결이 풀려 시험이 성립하지 않습니다.");
        Assert.True(world.Pack.Any(item => item.Name == Potion && item.Stacks == potion.Stacks),
            $"얼었는데 {Potion}을 마셨습니다. 서버가 한 말: {world.Said}");
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }

    private static void StandOneFreezerAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\"AreaID\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        JsonNode? freezer = null;

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!woodland.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;
            freezer ??= template.DeepClone();
            template["SpawnMax"] = 0;
            File.WriteAllText(path, template.ToJsonString());
        }

        Assert.NotNull(freezer);
        freezer["Name"] = "빙결시험괴물";
        freezer["BaseName"] = "빙결시험괴물";
        freezer["SpawnMax"] = 1;
        freezer["SpawnType"] = 4;
        freezer["SpawnRate"] = 1;
        freezer["DefinedX"] = Ahead.X;
        freezer["DefinedY"] = Ahead.Y;
        freezer["MaximumHP"] = 1_000_000;
        freezer["DmgMin"] = 1;
        freezer["DmgMax"] = 1;
        freezer["MoodType"] = 2;
        freezer["PathQualifer"] = 2;
        freezer["Grow"] = false;
        freezer["CastSpeed"] = 60_000;
        freezer["EngagedWalkingSpeed"] = 300;
        freezer["SpellChance"] = 100;
        freezer["SpellScripts"] = new JsonArray("Monster_소루마");

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(Path.Combine(testFolder, "frozen.json"), freezer.ToJsonString());
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);
}
