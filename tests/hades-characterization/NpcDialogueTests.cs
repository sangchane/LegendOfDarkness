using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The count of loaded NPC templates says nothing about whether the world has NPCs in it. An NPC that
/// loads, stands in the right place and answers when tapped is the only thing worth calling done — the
/// port's own plan says so — and that needs a client that walks in and taps one.
/// </summary>
public sealed class NpcDialogueTests : IDisposable
{
    /// <summary>
    /// A ported NPC with a line of its own and no script, and where it stands. (It was 가렌 in 밀레스마을, then 멜로린 in
    /// 노비스마을, until each got its 5.99 script — `Pack599TeacherTests`, `Pack599QuestTests`.)
    /// </summary>
    private const int NoviceTavernId = 20379;

    private const string Speaker = "미스포츈@노비스주점#5,4";
    private const string SpeakerSays = "미스포츈: 후후 어서들 오세요. 여기는 노비스 주점이랍니다~!";
    private const string Name = "npctalk";

    /// <summary>A ported shop (<c>shop1</c>) in the novice town's diner, and one of its goods the server has a template for.</summary>
    private const int NoviceDinerId = 20374;
    private const string Diner = "카르마@노비스마을식당#3,10";
    private const string SnakeMeat = "뱀고기";
    private const uint SnakeMeatPrice = 350;

    /// <summary>카르마's picture in the pack (npc/Npc.txt 이미지 31), sent the way monsters are — 0x4000 on top.</summary>
    private const int KarmaSprite = 0x4000 + 31;
    private const string Shopper = "npcshop";

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task Tapping_a_ported_npc_opens_what_the_pack_said_it_says()
    {
        // Standing beside her. The tap carries a serial, not a direction, but being next to her is what a player
        // would do and it keeps her inside the first creature list.
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceTavernId, 5, 5));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry entry = await Settled(world, seen => seen is not null);
        Assert.Equal(NoviceTavernId, entry.Map.Id);

        Creature her = await Standing(world, new Tile(5, 4));

        await world.ClickAsync(her.Serial, _deadline.Token);

        Dialogue talk = await Answered(world);

        Assert.Equal(Speaker, talk.Who);
        // 대사가 여러 줄이라 한 창에 줄바꿈으로 이어 온다(PackSpeaker). 첫 줄로 시작하는지 본다.
        Assert.StartsWith(SpeakerSays, talk.What);
        Assert.Equal(her.Serial, talk.Serial);
    }

    /// <summary>
    /// A shop is three windows deep: its menu, the goods, and word of the sale. The goods have Korean names and the
    /// shop finds what to sell by the name that comes back, so this is also the check that the name survives the
    /// way back (0x3A, the server's code page — not 0x39, which reads ASCII).
    /// </summary>
    [Fact]
    public async Task Buying_from_a_shop_goes_through_its_windows_and_puts_the_thing_in_the_pack()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (NoviceDinerId, 3, 11));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Shopper);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Shopper}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = 1000;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Shopper, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen?.Map.Id == NoviceDinerId);
        Creature keeper = await Standing(world, new Tile(3, 10));

        // 그림 번호가 괴물과 같은 체계여야 클라이언트가 mns031 을 찾아 그리고 누를 수 있다.
        Assert.Equal(KarmaSprite, keeper.Sprite);

        await world.ClickAsync(keeper.Serial, _deadline.Token);
        Dialogue menu = await Window(world, 0, talk => talk.Options.Count > 0);

        Assert.Equal((Diner, DialogueKind.Options), (menu.Who, menu.Kind));
        // 상점 말이 한국어로 바뀌었다(shop1.cs, 커밋 f3f5727ab) — "Buy" 가 아니라 "삽니다".
        DialogueOption buy = Assert.Single(menu.Options, option => option.Text == "삽니다");

        int opened = world.TalkCount;
        await world.AnswerAsync(keeper.Serial, buy.Step, _deadline.Token);
        Dialogue goods = await Window(world, opened, talk => talk.Kind == DialogueKind.Goods);

        DialogueGoods meat = Assert.Single(goods.Goods, one => one.Name == SnakeMeat);
        Assert.Equal(SnakeMeatPrice, meat.Price);

        await world.AnswerAsync(keeper.Serial, goods.Step, meat.Name, _deadline.Token);
        await Until(() => world.Pack.Any(item => item.Name == SnakeMeat) && world.Vitals?.Gold == 1000 - SnakeMeatPrice,
            $"{SnakeMeat}을 사지 못했습니다. 소지품: {string.Join(", ", world.Pack.Select(item => item.Name))} · 금화 {world.Vitals?.Gold} · 창: {world.Talking?.What}");

        await world.ShutDialogueAsync(_deadline.Token);
        await Until(() => world.Talking is null, $"창을 닫았는데 서버가 닫지 않았습니다. 마지막 창: {world.Talking?.What}");
    }

    /// <summary>
    /// The pack places script NPCs it gives no picture — here the ferry-man in 뤼케시온마을 who takes you to 노엠마을
    /// (`Npc_Warp.txt` 노엠마을: ability 18 or more, one word, then `warp "노엠마을", 35, 27`). They come with picture number 0
    /// (0x4000) — the client draws a sign there — and their script runs like any other.
    /// </summary>
    [Fact]
    public async Task A_pictureless_pack_npc_answers_with_its_script_and_warps_you()
    {
        const int Lukesion = 20338;
        const int Noem = 20087;
        const string Traveller = "npcwarp";

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (Lukesion, 29, 13));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Traveller);

        string saved = Path.Combine(server.ContentLocation, "aislings", $"{Traveller}.json");
        JsonNode character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["AbpLevel"] = 18;
        File.WriteAllText(saved, character.ToJsonString());

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Traveller, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        Creature ferryman = await Standing(world, new Tile(29, 12));
        Assert.Equal(0x4000, ferryman.Sprite);

        await world.ClickAsync(ferryman.Serial, _deadline.Token);
        Dialogue greeting = await Window(world, 0, talk => talk.What.StartsWith("안녕하세요 노엠마을로"));
        DialogueOption next = Assert.Single(greeting.Options);

        await world.AnswerAsync(ferryman.Serial, next.Step, _deadline.Token);
        await Until(() => world.State is { } state && state.Map.Id == Noem && state.Where == new Tile(35, 27),
            $"노엠마을 35,27 로 가지 않았습니다. 마지막: {world.State}");
    }

    /// <summary>
    /// 대사 속 영어 낱말 — 5.99 팩 원문은 "1500Gold입니다"·"(요금5000GOLD)" 처럼 한국어 문장 안에 영어
    /// 낱말을 그대로 남겼다(<c>Npc_Warp.txt</c> 273줄 · <c>Npc_Script.txt</c> 157줄). 2026-09-25 에
    /// <c>scripts/build-pack-npcs.py</c> 의 <c>TEXT_PATCH</c> 로 고쳤다(개인던전입장도우미·미용사·애교·
    /// 워프할아버지·타바리마을이동·선진·리프트도우미·멜로린·신의대장장이1, <c>docs/server-messages-ko.md</c>).
    /// 대표로 둘을 격리 서버에서 열어 실제 창에도 라틴 글자가 없는지 본다.
    /// </summary>
    [Theory]
    [InlineData(20338, 29, 10, 29, 9, "npceng1", "개인던전입장도우미@뤼케시온마을#29,9", "1500골드입니다")]
    [InlineData(20321, 3, 4, 3, 3, "npceng2", "미용사@로톤미용실#3,3", "5000골드")]
    public async Task Ported_npc_dialogue_has_no_leftover_english_word(
        int map, int startX, int startY, int npcX, int npcY, string account, string who, string mustContain)
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (map, startX, startY));
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, account);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, account, LoginFlow.SyntheticSecret,
            progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        Creature keeper = await Standing(world, new Tile(npcX, npcY));

        await world.ClickAsync(keeper.Serial, _deadline.Token);
        Dialogue talk = await Answered(world);

        Assert.Equal(who, talk.Who);
        Assert.Contains(mustContain, talk.What);
        Assert.False(Regex.IsMatch(talk.What, "[A-Za-z]"),
            $"{who} 의 대사에 라틴 글자가 남아 있습니다: {talk.What}");
    }

    /// <summary>Waits for a window opened after the <paramref name="seen" />-th one that is the one wanted.</summary>
    private async Task<Dialogue> Window(WorldClient world, int seen, Func<Dialogue, bool> wanted)
    {
        await Until(() => world.TalkCount > seen && world.Talking is { } talk && wanted(talk),
            $"기다린 창이 오지 않았습니다. 마지막 창: {world.Talking?.Kind} {world.Talking?.What}");

        return world.Talking!;
    }

    private Task Until(Func<bool> condition, string failure) => Waiting.Until(condition, failure, _deadline.Token);

    private async Task<Creature> Standing(WorldClient world, Tile where)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            Creature? found = world.Creatures.FirstOrDefault(one => one.Where == where);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(
            $"{where} 에 아무도 없습니다. 본 것: " +
            string.Join(", ", world.Creatures.Select(one => $"{one.Serial}@{one.Where}")));
    }

    private async Task<Dialogue> Answered(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Talking is { } talk)
            {
                return talk;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException("눌렀는데 대화창이 오지 않았습니다.");
    }

    private async Task<WorldEntry> Settled(WorldClient world, Func<WorldEntry?, bool> wanted)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(30);

        while (DateTime.UtcNow < giveUp)
        {
            if (wanted(world.State))
            {
                return world.State!;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"세계에 들어가지 못했습니다. 마지막 상태: {world.State}");
    }
}
