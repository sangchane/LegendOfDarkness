using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Darkages.Network;
using Darkages.Security;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;
using Xunit;
namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Proves the mobile client's own protocol code against the real server rather than against a recorded
/// fixture. The fixture says what we believe the bytes are; these say the server agrees.
/// </summary>
public sealed class MobileClientProtocolTests
{
    private const string MobileName = "lodmobile";
    private const string OtherName = "lodfriend";
    private const int WoodlandOneOne = 20015;
    private static readonly Tile MonkStart = new(2, 35);
    private static readonly Tile MonkTarget = new(2, 34);
    private static readonly Tile MonkLanding = new(2, 33);

    /// <summary>
    /// A stalled exchange should fail the test rather than hold the run. One per test, not one for the
    /// class: xunit builds the class again for each test, and a shared clock would have already run down.
    /// </summary>
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(2));

    [Fact]
    public void Mobile_cipher_agrees_with_the_server_cipher()
    {
        EncryptionParameters parameters = new(0, "NexonInc."u8.ToArray(), 0);

        // Around the salt length, because the mixing changes every nine bytes, and at both ordinal ends.
        foreach (byte ordinal in new byte[] { 0, 1, 2, 9, 42, 255 })
        {
            foreach (int length in new[] { 1, 8, 9, 10, 17, 18, 19, 100 })
            {
                byte[] body = new byte[length];

                for (int index = 0; index < length; index++)
                {
                    body[index] = (byte)((index * 7) + 3);
                }

                byte[] expected = ServerTransform(ordinal, body);
                byte[] actual = (byte[])body.Clone();

                HadesCipher.Transform(actual, parameters, ordinal);

                Assert.Equal(expected, actual);
            }
        }
    }

    [Fact]
    public async Task Mobile_client_logs_in_and_enters_the_world()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        List<string> reported = [];

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            MobileName,
            LoginFlow.SyntheticSecret,
            new Progress<string>(reported.Add),
            _deadline.Token);

        Assert.Equal(MobileName, session.Character.CharacterName);
        Assert.Equal(server.GamePort, session.Character.Port);
        Assert.Equal(HadesCipher.SupportedSeed, session.Parameters.Seed);

        // The server logs this line only once the character is standing in a map.
        LoginFlow.WaitForLog(server, LoginFlow.WelcomeMessage(MobileName), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// 아이폰 테더링(SKT)은 IPv6 뿐이라 폰이 맥에 IPv6 로만 닿는다(2026-09-24). 서버는 IPv6 로도 듣고, 앱은
    /// 넘겨받는 주소(IPv4 4바이트뿐)가 아니라 처음 붙은 주소로 로비·게임에 따라간다.
    /// </summary>
    [Fact]
    public async Task Mobile_client_logs_in_over_ipv6()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.IPv6Loopback, server.LoginPort, MobileName, LoginFlow.SyntheticSecret, null, _deadline.Token);

        Assert.Equal(server.GamePort, session.Character.Port);
        LoginFlow.WaitForLog(server, LoginFlow.WelcomeMessage(MobileName), TimeSpan.FromSeconds(30));
    }

    [Theory]
    [InlineData(1, "튜닉", 2)]
    [InlineData(2, "꼬뜨", 4)]
    [InlineData(3, "매직스커트", 6)]
    [InlineData(4, "로브", 5)]
    [InlineData(5, "연무복", 3)]
    public async Task Mobile_client_creates_the_selected_class_at_novice_town_in_its_level_one_outfit(
        byte selectedPath, string outfit, int armour)
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        // Three different values, none of them the placeholder 0x01,0x01,0x01 the old bypass sent, so a
        // swapped HairStyle/Gender/HairColor order in the client would show up as a value in the wrong field.
        const byte hairStyle = 0x0C;
        const byte gender = 0x02;
        const byte hairColor = 0x47;

        using WorldSession session = await HadesLoginClient.CreateCharacterAsync(
            IPAddress.Loopback,
            server.LoginPort,
            MobileName,
            LoginFlow.SyntheticSecret,
            hairStyle,
            gender,
            hairColor,
            selectedPath,
            progress: null,
            _deadline.Token);

        Assert.Equal(MobileName, session.Character.CharacterName);

        // The server logs this line only once the character is standing in a map.
        LoginFlow.WaitForLog(server, LoginFlow.WelcomeMessage(MobileName), TimeSpan.FromSeconds(30));

        string path = Path.Combine(server.ContentLocation, "aislings", $"{MobileName}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;

        // Gender saves as its enum name, not the wire byte; HairStyle and HairColor save as the plain numbers.
        Assert.Equal(hairStyle, (byte)saved["HairStyle"]!.GetValue<int>());
        Assert.Equal(((Darkages.Types.Gender)gender).ToString(), saved["Gender"]!.GetValue<string>());
        Assert.Equal(hairColor, (byte)saved["HairColor"]!.GetValue<int>());
        Assert.Equal(((Darkages.Types.Class)selectedPath).ToString(), saved["Path"]!.GetValue<string>());
        Assert.Equal(outfit, saved["EquipmentManager"]!["Equipment"]!["2"]!["Item"]!["Template"]!["Name"]!.GetValue<string>());
        Assert.Equal(20373, saved["CurrentMapId"]!.GetValue<int>());
        Assert.Equal(37, saved["X"]!.GetValue<int>());
        Assert.Equal(29, saved["Y"]!.GetValue<int>());

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        Character appeared = await Dressed(world, wearing => wearing.Armor == armour);
        Assert.Equal(armour, appeared.Wearing!.Armor);
    }

    [Fact]
    public async Task A_new_monk_keeps_exactly_the_requested_starters_across_relogin_and_can_use_them()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(
            startTogether: (WoodlandOneOne, MonkStart.X, MonkStart.Y));
        PutMonkStarterTargetAhead(server);
        server.Start(TimeSpan.FromMinutes(2));

        const byte monk = 5;
        int leapSlot, kickSlot;

        using (WorldSession session = await HadesLoginClient.CreateCharacterAsync(
                   IPAddress.Loopback, server.LoginPort, MobileName, LoginFlow.SyntheticSecret,
                   hairStyle: 12, gender: 1, hairColor: 40, path: monk, progress: null, _deadline.Token))
        {
            WorldClient world = new(session);
            _ = world.PumpAsync(_deadline.Token);

            await Settled(world, seen => seen is { Map.Id: WoodlandOneOne, Where: var where } && where == MonkStart);
            await StarterSkillsArrive(world);
            await Until(() => world.Creatures.Any(creature => creature.Where == MonkTarget),
                "새 무도가 앞에 기술 시험 표적이 나타나지 않았습니다.");

            LearnedSkill kick = world.Skills.Single(skill => skill.Name.StartsWith("단각 (", StringComparison.Ordinal));
            LearnedSkill leap = world.Skills.Single(skill => skill.Name.StartsWith("이형환위 (", StringComparison.Ordinal));
            Assert.All(new[] { kick, leap }, skill => Assert.Equal(1, ParseSkillLevel(skill.Name)));

            // Skill.GiveTo picks the pane slots; which numbers it picks is the server's business, but the two
            // must land in different slots and the same ones must come back after a relogin (checked below).
            (leapSlot, kickSlot) = (leap.Slot, kick.Slot);
            Assert.NotEqual(leapSlot, kickSlot);
            Assert.All(new[] { leapSlot, kickSlot }, slot => Assert.True(slot > 0, $"기술이 칸을 받지 못했습니다: {slot}"));

            // 단각 is the project's one implementation of the Kick animation; a separate English Kick must
            // not be added beside it.  Its real Monk script returns motion 131, not the generic Assail motion.
            Assert.DoesNotContain(world.Skills, skill => skill.Name.StartsWith("Kick (", StringComparison.Ordinal));
            await world.UseSkillAsync(kick.Slot, _deadline.Token);
            await MovedBody(world, 131);

            // 이형환위's script requires a target, then steps over it and turns toward it.
            await world.UseSkillAsync(leap.Slot, _deadline.Token);
            await Until(() => world.State?.Where == MonkLanding && world.Self?.Facing == Direction.South,
                "이형환위가 표적을 넘어가 표적 방향을 보지 않았습니다.");
        }

        // Re-enter instead of just reading the freshly written JSON: LoadSkillBook must restore both scripts
        // into the skill pane for a later login too.
        using WorldSession relogged = await LoginAsync(server);
        WorldClient afterRelogin = new(relogged);
        _ = afterRelogin.PumpAsync(_deadline.Token);
        await Settled(afterRelogin, seen => seen is not null);
        await StarterSkillsArrive(afterRelogin);

        Assert.Equal(leapSlot, afterRelogin.Skills
            .Single(skill => skill.Name.StartsWith("이형환위 (", StringComparison.Ordinal)).Slot);
        Assert.Equal(kickSlot, afterRelogin.Skills
            .Single(skill => skill.Name.StartsWith("단각 (", StringComparison.Ordinal)).Slot);

        string savedPath = Path.Combine(server.ContentLocation, "aislings", $"{MobileName}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(savedPath))!;
        List<JsonNode> skills = saved["SkillBook"]!["Skills"]!.AsObject()
            .Select(pair => pair.Value)
            .Where(skill => skill is not null)
            .Select(skill => skill!)
            .ToList();
        // Exactly these and nothing else (user, 2026-09-24): the three Monk techniques beside the base attack the
        // attack button swings (0x13 runs only Assail-type skills), and 쿠로토 instead of beag ioc fein.
        Assert.Equal(new[] { "Assail", "단각", "붕각", "이형환위" },
            skills.Select(skill => (string)skill["Template"]!["Name"]!).Order(StringComparer.Ordinal));
        Assert.All(skills.Where(skill => (string?)skill!["Template"]?["Name"] is "이형환위" or "붕각" or "단각"),
            skill => Assert.Equal(1, (int?)skill!["Level"]));
        List<string> spells = saved["SpellBook"]!["Spells"]!.AsObject()
            .Select(pair => pair.Value)
            .Where(spell => spell is not null)
            .Select(spell => (string)spell!["Template"]!["Name"]!)
            .ToList();
        Assert.Equal(new[] { "쿠로토" }, spells);

        // The exact script keys are the authoritative backing for the pane entries, not a display-name alias.
        Assert.Equal("이형환위", ReadSkillTemplate(server, "이형환위")["ScriptName"]!.GetValue<string>());
        Assert.Equal("단각", ReadSkillTemplate(server, "단각")["ScriptName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Mobile_client_rejects_an_invalid_creation_class_without_saving_a_character()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        await Assert.ThrowsAsync<ProtocolException>(() => HadesLoginClient.CreateCharacterAsync(
            IPAddress.Loopback, server.LoginPort, MobileName, LoginFlow.SyntheticSecret,
            hairStyle: 12, gender: 2, hairColor: 40, path: 0, progress: null, _deadline.Token));

        Assert.False(File.Exists(Path.Combine(server.ContentLocation, "aislings", $"{MobileName}.json")));
    }

    [Fact]
    public async Task World_says_which_map_the_character_is_on_and_where()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry entry = await Settled(world, seen => seen is not null);

        Assert.Equal(new MapInfo(20373, 70, 70, "노비스마을"), entry.Map);
        Assert.Equal(new Tile(37, 29), entry.Where);
    }

    [Fact]
    public async Task Mobile_client_receives_and_uses_the_starter_skill_and_a_real_spell()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);
        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen is not null);

        LearnedSkill skill = await Learned(world, "Assail");
        LearnedSpell spell = await LearnedSpell(world, "beag ioc fein");

        Assert.Equal(1, skill.Icon);
        Assert.Equal(21, spell.Icon);
        Assert.Equal(SpellTargetType.NoTarget, spell.TargetType);

        // Assail executes its real script and broadcasts our body motion back through the same client.
        await world.UseSkillAsync(skill.Slot, _deadline.Token);
        await MovedBody(world);

        // The healing spell executes its real script and confirms the cast in a server message.
        await world.UseSpellAsync(spell.Slot, 0, _deadline.Token);
        await ServerSaid(world, "beag ioc fein을(를) 외웠습니다");
    }

    [Fact]
    public async Task Wearing_something_changes_how_the_server_describes_us()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        // 새 계정의 소지품은 비어 있다. 채팅 명령 `/give` 는 운영자만 쓸 수 있고 인용부호까지
        // 얽히므로, 서버가 접속할 때 읽는 파일에 직접 넣는다 — 문서가 말하는 방법 그대로다.
        PutBootsInThePack(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen is not null);

        // Boot.cs sets Aisling.Boots to the item template's Image, and this template's is 1 — so bare feet
        // are 0 and these boots are 1, which is what each wait below is waiting for.
        Character before = await Dressed(world, seen => seen.Boots == 0);

        InventoryItem boots = await Carried(world, "Shagreen Boots");

        await world.UseAsync(boots.Slot, _deadline.Token);

        // The server answers a piece of clothing by describing us again — that is what redraws the figure.
        Character after = await Dressed(world, seen => seen.Boots == 1);

        Assert.NotEqual(before.Wearing, after.Wearing);
        Assert.Equal(0, before.Wearing!.Boots);
        Assert.Equal(1, after.Wearing!.Boots);
    }

    [Fact]
    public async Task Taking_something_off_puts_it_back_in_the_pack()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);
        PutBootsInThePack(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen is not null);

        await Dressed(world, seen => seen.Boots == 0);
        InventoryItem boots = await Carried(world, "Shagreen Boots");

        await world.UseAsync(boots.Slot, _deadline.Token);

        Character dressed = await Dressed(world, seen => seen.Boots == 1);
        Assert.Equal(1, dressed.Wearing!.Boots);

        // 서버는 걸친 자리를 0x37 로 따로 알려준다. 어느 자리에 갔는지는 그것으로만 안다.
        WornItem worn = await WornIn(world, "Shagreen Boots");

        await world.TakeOffAsync(worn.Slot, _deadline.Token);

        // 벗는 것도 입는 것과 같다 — 서버가 우리를 다시 묘사하는 것이 그림을 고쳐 그리게 한다.
        Character bare = await Dressed(world, seen => seen.Boots == 0);

        Assert.Equal(0, bare.Wearing!.Boots);
        Assert.DoesNotContain(world.Worn, one => one.Slot == worn.Slot);

        // 벗은 것은 사라지지 않고 소지품으로 돌아온다.
        await Carried(world, "Shagreen Boots");
    }

    [Fact]
    public async Task Tidying_closes_the_gaps_and_throwing_something_away_empties_its_slot()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        // 세 칸 건너뛰어 넣어 둔다. 정렬이 할 일이 있어야 한다.
        PutBootsInThePack(server, MobileName, slot: 3);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry standing = await Settled(world, seen => seen is not null);

        InventoryItem away = await Carried(world, "Shagreen Boots");

        Assert.Equal(3, away.Slot);

        // 입장 직후에는 서버가 칸 옮기기를 조용히 버린다 — Format30Handler 가 IsRefreshing 이면
        // 아무 말 없이 돌아선다. 한 번 보내고 기다렸다 다시 보내면 받아 준다(2026-09-11 확인).
        await world.MoveAsync(away.Slot, 1, _deadline.Token);
        await Task.Delay(2000, _deadline.Token);
        await world.MoveAsync(away.Slot, 1, _deadline.Token);

        InventoryItem moved = await Slotted(world, 1);

        Assert.Equal("Shagreen Boots", moved.Name);

        await world.DropAsync(moved.Slot, 1, standing.Where, _deadline.Token);

        await Emptied(world);
    }

    [Fact]
    public async Task What_is_thrown_down_lies_on_that_tile_and_can_be_picked_back_up()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);
        PutBootsInThePack(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry standing = await Settled(world, seen => seen is not null);

        InventoryItem boots = await Carried(world, "Shagreen Boots");

        await world.DropAsync(boots.Slot, 1, standing.Where, _deadline.Token);
        await Emptied(world);

        // 바닥에 놓인 것은 서버가 사람·괴물과 같은 형식(0x07)으로 보내되 지나갈 수 있다고 말한다.
        Creature lying = await Lying(world, standing.Where);

        await world.PickUpAsync(standing.Where, _deadline.Token);

        // 주우면 소지품으로 돌아오고, 그 칸에서는 사라진다.
        await Carried(world, "Shagreen Boots");
        await Gone(world, lying.Serial);
    }

    [Fact]
    public async Task The_world_tells_us_our_own_name()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen is not null);

        // The HUD has no other source for it — anything else it shows is invented.
        Character? self = await Mine(world);

        Assert.Equal(MobileName, self!.Name);
    }

    [Fact]
    public async Task Walking_moves_the_character_on_the_server()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        WorldEntry start = await Settled(world, seen => seen is not null);

        // The server drops a walk while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);
        await world.WalkAsync(Direction.East, _deadline.Token);

        // It tells the people nearby rather than the walker, so ask it where we are.
        await world.RefreshAsync(_deadline.Token);

        Tile expected = new(start.Where.X + 1, start.Where.Y);
        WorldEntry after = await Settled(world, seen => seen?.Where == expected);

        Assert.Equal(expected, after.Where);
        Assert.Equal(start.Map, after.Map);
    }

    [Fact]
    public async Task Walking_faster_than_the_server_allows_pushes_the_character_back()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        using WorldSession session = await LoginAsync(server);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);

        await Settled(world, seen => seen is not null);

        // The server drops a walk while the client is still settling into the map.
        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);

        // The control: a step the server is happy with is answered with silence. This is why a client has
        // to move its own figure rather than wait to be told.
        int told = world.PositionReports;

        await world.WalkAsync(Direction.East, _deadline.Token);
        await Task.Delay(TimeSpan.FromSeconds(3), _deadline.Token);

        Assert.True(world.PositionReports == told, "서버가 정상적인 걸음에도 위치를 보내왔습니다.");

        // Now the same direction as fast as the socket will take it. The server watches how quickly a
        // client claims to move and puts it back where it really is, so a client that ignores this will
        // rubber-band on every hurried step.
        for (int step = 0; step < 8; step++)
        {
            await world.WalkAsync(Direction.East, _deadline.Token);
        }

        WorldEntry after = await Settled(world, _ => world.PositionReports > told, TimeSpan.FromSeconds(15));

        // Wherever the server puts it back, it is a tile of the map the character is standing on.
        Assert.InRange(after.Where.X, 0, after.Map.Columns - 1);
        Assert.InRange(after.Where.Y, 0, after.Map.Rows - 1);
    }

    [Fact]
    public async Task A_second_character_is_seen_and_seen_to_move()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);
        LoginFlow.TryCreateAccount(server, OtherName);

        using WorldSession watcher = await LoginAsync(server, MobileName);
        WorldClient watching = new(watcher);
        _ = watching.PumpAsync(_deadline.Token);

        await Settled(watching, seen => seen is not null);

        using WorldSession walker = await LoginAsync(server, OtherName);
        WorldClient walking = new(walker);
        _ = walking.PumpAsync(_deadline.Token);

        WorldEntry theirStart = await Settled(walking, seen => seen is not null);

        // The number the login server handed over opened the door; the world has its own for a character,
        // and the server tells each client its own.
        uint theirSerial = walking.Serial;

        Assert.NotEqual(0u, theirSerial);

        // Both start on the same entry tile, so the watcher should be shown somebody standing on it.
        Character standing = await Sees(watching, theirSerial);

        Assert.Equal(theirStart.Where, standing.Where);

        // The name sits past the wardrobe, so reading it back proves every offset in between.
        Assert.Equal(OtherName, standing.Name, ignoreCase: true);
        Assert.NotNull(standing.Wearing);

        await Task.Delay(TimeSpan.FromSeconds(1), _deadline.Token);
        await walking.WalkAsync(Direction.East, _deadline.Token);

        Character stepped = await Sees(watching, theirSerial, one => one.Where != standing.Where);

        Assert.Equal(new Tile(standing.Where.X + 1, standing.Where.Y), stepped.Where);
        Assert.Equal(Direction.East, stepped.Facing);

        // A step is announced without a wardrobe, and walking must not undress anybody.
        Assert.Equal(standing.Wearing, stepped.Wearing);
        Assert.Equal(standing.Name, stepped.Name);

        // And we are never in our own list of other people.
        Assert.DoesNotContain(watching.Others, one => one.Serial == watching.Serial);

        // Leaving takes them off the screen again, which the server says with its own packet.
        walker.Dispose();

        await Gone(watching, theirSerial);
    }

    /// <summary>Waits for the watcher to be told somebody left.</summary>
    private async Task Gone(WorldClient world, uint serial)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Others.All(one => one.Serial != serial))
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 {serial} 가 떠났다고 알려주지 않았습니다.");
    }

    /// <summary>
    /// Puts one pair of boots in a freshly created character's first pack slot, and raises the character
    /// to the level the boots ask for. The server fills in the rest of the template by name when it loads
    /// the character, so only the name and the slot matter.
    /// </summary>
    private static void PutBootsInThePack(IsolatedHadesServer server, string name, int slot = 1)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;

        // 이 신발은 33레벨을 요구한다(GameClient.CheckReqs). 새 계정은 1레벨이라 그대로면 못 신는다.
        saved["ExpLevel"] = 33;

        saved["Inventory"]!["Items"]![slot.ToString()] = new JsonObject
        {
            ["Template"] = new JsonObject { ["Name"] = "Shagreen Boots" },
            ["Slot"] = slot,
            ["Image"] = 1,
            ["DisplayImage"] = 32882,
            ["Color"] = 1,
            ["Stacks"] = 1,
            ["Durability"] = 100,
        };

        File.WriteAllText(path, saved.ToJsonString());
    }

    private async Task<LearnedSkill> Learned(WorldClient world, string name)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            LearnedSkill? found = world.Skills.FirstOrDefault(one => one.Name.StartsWith(name, StringComparison.Ordinal));

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"기술 창에 {name} 이 오지 않았습니다.");
    }

    private async Task<LearnedSpell> LearnedSpell(WorldClient world, string name)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            LearnedSpell? found = world.Spells.FirstOrDefault(one => one.Name.StartsWith(name, StringComparison.Ordinal));

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"마법 창에 {name} 이 오지 않았습니다.");
    }

    private async Task MovedBody(WorldClient world, int? expectedMotion = null)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.TakeMotion(out Motion? motion)
                && motion.Serial == world.Serial
                && (expectedMotion is null || motion.Number == expectedMotion))
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"기술 뒤 서버가 기대한 몸동작({expectedMotion?.ToString() ?? "임의"})을 돌려주지 않았습니다.");
    }

    private async Task StarterSkillsArrive(WorldClient world)
    {
        await Learned(world, "이형환위");
        await Learned(world, "붕각");
        await Learned(world, "단각");

        Assert.Single(world.Skills, skill => skill.Name.StartsWith("이형환위 (", StringComparison.Ordinal));
        Assert.Single(world.Skills, skill => skill.Name.StartsWith("붕각 (", StringComparison.Ordinal));
        Assert.Single(world.Skills, skill => skill.Name.StartsWith("단각 (", StringComparison.Ordinal));
    }

    private static int ParseSkillLevel(string name)
    {
        Match match = Regex.Match(name, @"\(Lev:(\d+)/");
        Assert.True(match.Success, $"기술 이름에 레벨이 없습니다: {name}");
        return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static JsonNode ReadSkillTemplate(IsolatedHadesServer server, string name) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(server.ContentLocation, "templates", "skills", $"{name}.json")))!;

    private static void PutMonkStarterTargetAhead(IsolatedHadesServer server)
    {
        string folder = Path.Combine(server.ContentLocation, "templates", "monsters");
        Regex woodland = new($"\\\"AreaID\\\"\\s*:\\s*{WoodlandOneOne}\\b");
        JsonDocumentOptions options = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        };
        string source = Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
            .First(path => woodland.IsMatch(File.ReadAllText(path)));
        JsonNode target = JsonNode.Parse(File.ReadAllText(source), documentOptions: options)!;

        target["Name"] = "무도가시작기술표적";
        target["BaseName"] = "무도가시작기술표적";
        target["AreaID"] = WoodlandOneOne;
        target["SpawnMax"] = 1;
        target["SpawnType"] = 4;
        target["SpawnRate"] = 1;
        target["DefinedX"] = MonkTarget.X;
        target["DefinedY"] = MonkTarget.Y;
        target["MaximumHP"] = 1_000_000;
        target["Ac"] = 0;
        target["MoodType"] = 1;
        target["PathQualifer"] = 2;
        target["Grow"] = false;

        string testFolder = Path.Combine(folder, "characterization");
        Directory.CreateDirectory(testFolder);
        File.WriteAllText(
            Path.Combine(testFolder, "monk-starter-target.json"),
            target.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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

    private async Task ServerSaid(WorldClient world, string words)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Said.Contains(words, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 '{words}'라고 말하지 않았습니다. 마지막 말: {world.Said}");
    }

    /// <summary>Waits until one particular slot holds something.</summary>
    private async Task<InventoryItem> Slotted(WorldClient world, int slot)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            InventoryItem? found = world.Pack.FirstOrDefault(item => item.Slot == slot);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"{slot}번 칸이 차지 않았습니다. 든 것: {Listed(world)}");
    }

    /// <summary>Waits until the pack has nothing left in it.</summary>
    private async Task Emptied(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Pack.Count == 0)
            {
                return;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"소지품이 비지 않았습니다. 남은 것: {Listed(world)}");
    }

    private static string Listed(WorldClient world) =>
        string.Join(", ", world.Pack.Select(item => $"{item.Slot}:{item.Name}"));

    /// <summary>Waits until something by that name is in the pack.</summary>
    private async Task<InventoryItem> Carried(WorldClient world, string called)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            InventoryItem? found = world.Pack.FirstOrDefault(item => item.Name == called);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"소지품에 {called} 가 들어오지 않았습니다. 든 것: {world.Pack.Count}가지. 서버가 한 말({world.SaidCount}번): {world.Said}");
    }

    /// <summary>Waits until something by that name is worn, and says which place it went to.</summary>
    private async Task<WornItem> WornIn(WorldClient world, string called)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            WornItem? found = world.Worn.FirstOrDefault(one => one.Called == called || one.Name == called);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(
            $"{called} 를 걸친 것으로 알려주지 않았습니다. 걸친 것: "
            + string.Join(", ", world.Worn.Select(one => $"{one.Slot}:{one.Called}")));
    }

    /// <summary>Waits until something passable is lying on that tile, and says what it is.</summary>
    private async Task<Creature> Lying(WorldClient world, Tile where)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            Creature? found = world.Creatures.FirstOrDefault(one =>
                one.Kind == CreatureKind.Passable && one.Where == where);

            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException(
            $"{where} 에 놓인 것이 보이지 않았습니다. 보이는 것: "
            + string.Join(", ", world.Creatures.Select(one => $"{one.Serial}:{one.Kind}@{one.Where}")));
    }

    /// <summary>Waits until the server describes us as wearing something other than this.</summary>
    /// <summary>Waits until the server describes us wearing something that answers <paramref name="wanted" />.</summary>
    /// <remarks>
    /// Waiting for "anything different" is not enough, and that was a flake for a while. The description we
    /// are given first can arrive with no wardrobe at all, and then the plain unbooted one counts as the
    /// change and the test measures that instead of the boots. Waiting for the thing we came to see cannot
    /// be fooled that way.
    /// </remarks>
    private async Task<Character> Dressed(WorldClient world, Func<Appearance, bool> wanted)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Self is { Wearing: not null } now && wanted(now.Wearing))
            {
                return now;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 찾던 겉모습을 알려주지 않았습니다. 마지막: {world.Self?.Wearing}");
    }

    /// <summary>Waits until the server has said which character is ours.</summary>
    private async Task<Character?> Mine(WorldClient world)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Self is not null)
            {
                return world.Self;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException("서버가 우리 자신을 알려주지 않았습니다.");
    }

    /// <summary>Waits for the watcher to be told about a particular character.</summary>
    private async Task<Character> Sees(WorldClient world, uint serial, Func<Character, bool>? wanted = null)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            Character? one = world.Others.FirstOrDefault(other => other.Serial == serial);

            if (one is not null && (wanted is null || wanted(one)))
            {
                return one;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 {serial} 를 알려주지 않았습니다. 본 사람: {watchList(world)}");

        static string watchList(WorldClient world) =>
            string.Join(", ", world.Others.Select(one => $"{one.Serial}@{one.Where}"));
    }

    /// <summary>Waits for the pump to report a state the test is looking for.</summary>
    private async Task<WorldEntry> Settled(
        WorldClient world,
        Func<WorldEntry?, bool> wanted,
        TimeSpan? within = null)
    {
        DateTime giveUp = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < giveUp)
        {
            WorldEntry? seen = world.State;

            if (wanted(seen))
            {
                return seen!;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 기다리는 상태를 알려주지 않았습니다. 마지막으로 본 것: {world.State}");
    }

    private Task<WorldSession> LoginAsync(IsolatedHadesServer server, string? name = null) =>
        HadesLoginClient.LoginAsync(
            IPAddress.Loopback,
            server.LoginPort,
            name ?? MobileName,
            LoginFlow.SyntheticSecret,
            progress: null,
            _deadline.Token);

    [Fact]
    public async Task Mobile_client_reports_the_server_refusal_instead_of_hanging()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, MobileName);

        ProtocolException refused = await Assert.ThrowsAsync<ProtocolException>(
            async () => await HadesLoginClient.LoginAsync(
                IPAddress.Loopback,
                server.LoginPort,
                MobileName,
                "definitely-the-wrong-secret",
                progress: null,
                _deadline.Token));

        // The server's own words, decrypted — not a timeout of our own making, and never the secret we sent.
        Assert.Contains("비밀번호", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("definitely-the-wrong-secret", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>The server's own cipher, used here as the oracle our implementation must match.</summary>
    private static byte[] ServerTransform(byte ordinal, byte[] body)
    {
        byte[] packet = [0x57, ordinal, .. body];
        NetworkPacket network = new(packet, packet.Length);

        new SecurityProvider().Transform(network);

        return network.Data;
    }
}
