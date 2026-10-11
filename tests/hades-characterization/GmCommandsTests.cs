using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Darkages.Systems.CLI;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 한글 운영자 명령(사용자 2026-10-11, <c>Systems/GmCommands.cs</c>)과 카카오 계정으로 운영자 되기(<c>GameMasterKakaoIds</c>).
/// 시험 캐릭터는 운영자 이름 목록에 없고 카카오 번호만 목록에 있다 — 그래도 명령이 들어야 한다.
/// </summary>
[Collection(TimedCollection.Name)]
public sealed class GmCommandsTests : IDisposable
{
    private const int Lobby = 20595;
    private const string Who = "gmkakao";
    private const string Kakao = "770077";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(6));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public void A_name_is_picked_when_exact_or_the_only_one_that_contains_what_was_typed()
    {
        string[] names = ["세줄금반지", "상급체력포션", "중급체력포션", "Dark Belt"];
        Assert.Equal(("세줄금반지", (string?)null), GmCommands.Pick(names, "세줄금반지", "아이템"));
        Assert.Equal(("세줄금반지", (string?)null), GmCommands.Pick(names, "세줄", "아이템"));
        Assert.Equal(("Dark Belt", (string?)null), GmCommands.Pick(names, "dark", "아이템"));
        (string? none, string? refusal) = GmCommands.Pick(names, "체력포션", "아이템");
        Assert.Null(none);
        Assert.Contains("후보 2개", refusal);
        Assert.Contains("없습니다", GmCommands.Pick(names, "없는물건", "아이템").Refusal);
    }

    [Fact]
    public async Task A_kakao_listed_operator_can_use_the_korean_commands_and_hide_keeps_the_body_unseen()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (Lobby, 20, 17));
        string config = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode settings = JsonNode.Parse(File.ReadAllText(config))!;
        settings["ServerConfig"]!["GameMasterKakaoIds"] = new JsonArray(Kakao);
        File.WriteAllText(config, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, Who);
        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Who}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["KakaoId"] = Kakao;
        character["ExpLevel"] = 10;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Who, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State?.Map.Id == Lobby && world.Vitals is not null, "아벨해안대기실에 들어가지 못했습니다.", _deadline.Token);

        await Say(world, "/운영자 ?", line => line.Contains("운영자 명령"));
        await Say(world, "/item 상급체력포션 30", line => line.Contains("상급체력포션 30개를 만들었습니다"));
        await Waiting.Until(() => world.Pack.Where(i => i.Name.StartsWith("상급체력포션")).Sum(i => Math.Max(1, i.Stacks)) == 30,
            $"상급체력포션 30개가 가방에 없습니다: {string.Join(", ", world.Pack.Select(i => $"{i.Name}×{i.Stacks}"))}", _deadline.Token);
        await Say(world, "/item 없는물건이름", line => line.Contains("없습니다"));

        await Say(world, "/레벨 50", line => line.Contains("레벨을 50"));
        await Waiting.Until(() => world.Vitals?.Level == 50, $"레벨이 50이 아닙니다: {world.Vitals?.Level}", _deadline.Token);
        long gold = world.Vitals!.Gold;
        await Say(world, "/돈 1000", line => line.Contains("금화"));
        await Waiting.Until(() => world.Vitals?.Gold == gold + 1000, $"금화가 1000 늘지 않았습니다: {gold} → {world.Vitals?.Gold}", _deadline.Token);

        await Say(world, "/괴물 문어 1", line => line.Contains("문어 1마리를 세웠습니다"));
        await Waiting.Until(() => world.Creatures.Any(c => c.Kind == CreatureKind.Hostile), "문어가 보이지 않습니다.", _deadline.Token);

        await Say(world, "/무적", line => line.Contains("무적 켬"));
        await Say(world, "/무적", line => line.Contains("무적 끔"));

        // 하이드 — 서버는 숨은 몸(0x50)을 보낸다. 앱은 이것으로 입던 옷을 반투명하게 둔다(Wardrobe.Unseen).
        await Say(world, "/숨기", line => line.Contains("숨기 켬"));
        await Waiting.Until(() => world.Self?.Wearing is { } worn && Wardrobe.Unseen(worn),
            $"숨었는데 숨은 몸이 아닙니다: {world.Self?.Wearing}", _deadline.Token);
        await Say(world, "/숨기", line => line.Contains("숨기 끔"));
        await Waiting.Until(() => world.Self?.Wearing is { } worn && !Wardrobe.Unseen(worn),
            $"숨기를 껐는데 숨은 몸 그대로입니다: {world.Self?.Wearing}", _deadline.Token);

        await world.SayAsync("/이동 아벨마을", _deadline.Token);
        await Waiting.Until(() => world.State?.Map.Name == "아벨마을", $"아벨마을로 가지 않았습니다: {world.State}", _deadline.Token);
    }

    /// <summary>운영자 명령을 치고, 그 답(서버 알림)이 올 때까지 기다린다.</summary>
    private async Task Say(WorldClient world, string command, Func<string, bool> answered)
    {
        while (world.TakeTold(out _, out _))
        {
        }

        await world.SayAsync(command, _deadline.Token);
        List<string> told = [];
        await Waiting.Until(() =>
        {
            while (world.TakeTold(out _, out string line))
                told.Add(line);
            return told.Any(answered);
        }, $"「{command}」에 맞는 답이 없습니다. 받은 말: {string.Join(" / ", told)}", _deadline.Token);
    }
}
