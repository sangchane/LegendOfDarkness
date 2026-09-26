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
/// 코마디움 칸(원작 5.99 코마디움·엑스코마디움 — <see cref="ComaChip" />)과 쓰러진 봇 되살리기. 포테의숲1존 사슴 한 마리가 때린다
/// (<see cref="PoteForestDeerDangerTests" /> 와 같은 붙박이). 혼수는 짧게(3초) 두어 죽음까지 본다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class CompanionComaTests : IDisposable
{
    private const string OwnerName = "comaowner";
    private const int ForestOne = 20263;
    private const int MurekansRoom = 20138;
    private static readonly Tile Deer = new(33, 46);

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(5));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task In_a_coma_i_wake_myself_with_ex_comadium()
    {
        // 운영자는 혼수 규칙이 다르다(무리 없이도 혼수) — 이 시험의 주인은 보통 사람, 엑스코마디움은 저장 파일로 넣는다.
        using IsolatedHadesServer server = Ready(ownerLevel: 1, ownerHealth: 150, (33, 47), gameMaster: false);
        CompanionCallTests.Edit(server, OwnerName, saved => saved["Inventory"]!["Items"]!["1"] = new JsonObject
        {
            ["Template"] = new JsonObject { ["Name"] = "엑스코마디움" },
            ["Slot"] = 1,
            ["DisplayImage"] = 32814,
            ["Stacks"] = 2,
            ["Durability"] = 0,
        });
        WorldClient owner = await Enter(server, OwnerName, ForestOne);
        await Until(() => AutoPotion.Count(owner.Pack, "엑스코마디움") == 2, () => "엑스코마디움이 가방에 없습니다.");

        // 무리가 없으면 혼수 없이 바로 죽는다(혼수 규칙) — 봇을 불러 파티가 되게 한다.
        WorldClient bot = await Enter(server, CompanionCallTests.BotName, 20373);
        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        await Until(() => Overhead.InComa(owner.Ailments), () => $"사슴에게 맞아 혼수가 되지 않았습니다: 맵 {owner.State?.Map.Id} {owner.State?.Where} 체력 {owner.Vitals?.Health}/{owner.Vitals?.MaximumHealth} · 봇 {bot.Master} · 괴물 {string.Join(",", owner.Creatures.Select(c => c.Where))} · {owner.Said}", TimeSpan.FromSeconds(40));

        ComaChoice choice = ComaChip.Choose(Overhead.InComa(owner.Ailments), false, owner.Pack);
        Assert.Equal(ComaUse.UseOnSelf, choice.Use);
        await owner.UseAsync(choice.Slot, _deadline.Token);

        await Until(() => !Overhead.InComa(owner.Ailments) && AutoPotion.Count(owner.Pack, "엑스코마디움") == 1,
            () => $"엑스코마디움으로 깨어나지 않았습니다: 혼수 {Overhead.InComa(owner.Ailments)} · 남은 {AutoPotion.Count(owner.Pack, "엑스코마디움")} · {owner.Said}");
    }

    [Fact]
    public async Task My_comadium_wakes_the_bot_and_a_dead_bot_comes_back_when_called()
    {
        // 주인은 사슴과 대각선(맞지 않는다), 불린 봇은 주인 남쪽 — 사슴 바로 옆이라 맞는다. 봇은 1레벨(체력 150)이라 한 대에 혼수.
        using IsolatedHadesServer server = Ready(ownerLevel: 3, ownerHealth: 5000, (32, 45));
        WorldClient owner = await Enter(server, OwnerName, ForestOne);
        WorldClient bot = await Enter(server, CompanionCallTests.BotName, 20373);
        await Give(owner, "코마디움", 3);

        for (int tries = 0; tries < 10 && bot.Master is null; tries++)
        {
            await owner.CallCompanionAsync(_deadline.Token);
            await Task.Delay(1000, _deadline.Token);
        }

        await Until(() => Overhead.InComa(bot.Ailments), () => $"봇이 혼수가 되지 않았습니다: 봇 {bot.State?.Where}", TimeSpan.FromSeconds(40));
        await Until(() => owner.AilmentsOf(bot.Serial).Any(a => a.Icon == Overhead.ComaIcon), () => "주인 화면에 봇의 혼수(0x5C)가 오지 않았습니다.");
        Assert.Equal(ComaUse.WakeBot, ComaChip.Choose(false, true, owner.Pack).Use);

        await owner.WakeCompanionAsync(_deadline.Token);
        await Until(() => !Overhead.InComa(bot.Ailments) && AutoPotion.Count(owner.Pack, "코마디움") == 2,
            () => $"내 코마디움으로 봇이 깨어나지 않았습니다: 혼수 {Overhead.InComa(bot.Ailments)} · 남은 {AutoPotion.Count(owner.Pack, "코마디움")} · {owner.Said}");

        // 또 맞아 혼수 → 3초 뒤 죽어 뮤레칸의방(유령). 서버는 유령을 데려오지 않는다.
        await Until(() => bot.State?.Map.Id == MurekansRoom, () => $"봇이 죽어 뮤레칸에 가지 않았습니다: {bot.State?.Map.Id}", TimeSpan.FromSeconds(60));
        await Until(() => bot.StatusesOf(bot.Serial)?.Any(s => s.Name == "ghost") == true, () => $"유령 표시가 봇에게 오지 않았습니다: 주인 {bot.Master} · 상태 {string.Join(",", bot.StatusesOf(bot.Serial)?.Select(x => x.Name) ?? ["없음"])} · 맵 {bot.State?.Map.Id} · 안읽음 {bot.Unread} · 주인이 본 봇 상태 {string.Join(",", owner.StatusesOf(bot.Serial)?.Select(x => x.Name) ?? ["없음"])} · 주인 말 {owner.Said}");
        await Task.Delay(2000, _deadline.Token);
        Assert.Equal(MurekansRoom, bot.State?.Map.Id);

        // [봇 부르기] — 되살려 옆으로.
        await owner.CallCompanionAsync(_deadline.Token);
        await Until(() => bot.State?.Map.Id == ForestOne && bot.StatusesOf(bot.Serial)?.Any(s => s.Name == "ghost") == false,
            () => $"유령 봇이 되살아 오지 않았습니다: {bot.State?.Map.Id} · {owner.Said}");
    }

    private IsolatedHadesServer Ready(int ownerLevel, int ownerHealth, (int X, int Y) at, bool gameMaster = true)
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (ForestOne, at.X, at.Y));
        CompanionCallTests.Configure(server);

        if (gameMaster)
        {
            Waiting.MakeGameMaster(server, OwnerName);
        }

        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["SkullLength"] = 3;
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        OneDeer(server);
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, OwnerName);
        LoginFlow.TryCreateAccount(server, CompanionCallTests.BotName);
        CompanionCallTests.Edit(server, OwnerName, saved =>
        {
            saved["ExpLevel"] = ownerLevel;
            saved["_MaximumHp"] = ownerHealth;
            saved["CurrentHp"] = ownerHealth;
        });
        CompanionCallTests.Edit(server, CompanionCallTests.BotName, saved =>
        {
            saved["CurrentMapId"] = 20373;
            saved["XPos"] = 37;
            saved["YPos"] = 29;
        });
        return server;
    }

    private async Task Give(WorldClient owner, string name, int count)
    {
        await owner.SayAsync($"/give \"{name}\" {count}", _deadline.Token);
        await Until(() => AutoPotion.Count(owner.Pack, name) == count, () => $"{name} 이 오지 않았습니다: {owner.Said}");
    }

    private async Task Until(Func<bool> condition, Func<string> failure, TimeSpan? within = null)
    {
        DateTime giveUp = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(failure());
    }

    private async Task<WorldClient> Enter(IsolatedHadesServer server, string who, int map)
    {
        WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Until(() => world.State?.Map.Id == map && world.Serial != 0, () => $"{who} 가 서지 못했습니다: {world.State?.Map.Id}");
        return world;
    }

    private static void OneDeer(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex zoneOne = new($"\"AreaID\"\\s*:\\s*{ForestOne}\\b");
        JsonDocumentOptions lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        foreach (string path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            if (!zoneOne.IsMatch(text))
            {
                continue;
            }

            JsonNode template = JsonNode.Parse(text, documentOptions: lenient)!;

            if ((string?)template["Name"] == "사슴")
            {
                template["SpawnType"] = 4;
                template["SpawnRate"] = 100_000;
                template["DefinedX"] = Deer.X;
                template["DefinedY"] = Deer.Y;
                template["MoodType"] = 2;
                template["PathQualifer"] = 2;
            }
            else
            {
                template["SpawnMax"] = 0;
            }

            File.WriteAllText(path, template.ToJsonString());
        }
    }
}
