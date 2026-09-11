using System.Net;
using System.Text.Json.Nodes;
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

        // lod1.map, the same 30x31 floor tools/dat-extract draws for the mockups, and where
        // LoruleConfig.json drops a new character.
        Assert.Equal(new MapInfo(1, 30, 31, "Safe House"), entry.Map);
        Assert.Equal(new Tile(4, 4), entry.Where);
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

        Character before = (await Mine(world))!;

        InventoryItem boots = await Carried(world, "Shagreen Boots");

        await world.UseAsync(boots.Slot, _deadline.Token);

        // The server answers a piece of clothing by describing us again — that is what redraws the figure.
        Character after = await Changed(world, before.Wearing);

        Assert.NotEqual(before.Wearing, after.Wearing);

        // Boot.cs sets Aisling.Boots to the item template's Image, and this template's is 1.
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

        Character before = (await Mine(world))!;
        InventoryItem boots = await Carried(world, "Shagreen Boots");

        await world.UseAsync(boots.Slot, _deadline.Token);

        Character dressed = await Changed(world, before.Wearing);
        Assert.Equal(1, dressed.Wearing!.Boots);

        // 서버는 걸친 자리를 0x37 로 따로 알려준다. 어느 자리에 갔는지는 그것으로만 안다.
        WornItem worn = await WornIn(world, "Shagreen Boots");

        await world.TakeOffAsync(worn.Slot, _deadline.Token);

        // 벗는 것도 입는 것과 같다 — 서버가 우리를 다시 묘사하는 것이 그림을 고쳐 그리게 한다.
        Character bare = await Changed(world, dressed.Wearing);

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

        Assert.InRange(after.Where.X, 0, 29);
        Assert.InRange(after.Where.Y, 0, 30);
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

    /// <summary>Waits until the server describes us as wearing something other than this.</summary>
    private async Task<Character> Changed(WorldClient world, Appearance? before)
    {
        DateTime giveUp = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < giveUp)
        {
            if (world.Self is { Wearing: not null } now && now.Wearing != before)
            {
                return now;
            }

            await Task.Delay(50, _deadline.Token);
        }

        throw new TimeoutException($"서버가 겉모습을 다시 알려주지 않았습니다. 마지막: {world.Self?.Wearing}");
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
        Assert.Contains("Password", refused.Message, StringComparison.OrdinalIgnoreCase);
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
