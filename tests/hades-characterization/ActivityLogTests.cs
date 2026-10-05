using System.Net;
using System.Text.Json;
using System.Text;
using Lod.Mobile.Core.Diagnostics;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Protocol.Login;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

[Collection(TimedCollection.Name)]
public sealed class ActivityLogTests
{
    [Fact]
    public void Telemetry_format_rejects_oversized_truncated_or_extra_wire_data()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string json = "{\"kind\":\"app_device\",\"meta\":{\"model\":\"iPhone\"}}";
        byte[] body = AppActivity.Packet(json);
        string? Parse(byte[] data)
        {
            var format = new Darkages.Network.ClientFormats.ClientFormatF3();
            var packet = new Darkages.Network.NetworkPacket([0xF3, 0, .. data], data.Length + 2);
            var reader = new Darkages.Network.NetworkPacketReader { Packet = packet, Position = 0 };
            format.Serialize(reader);
            return format.Payload;
        }
        Assert.Equal(json, Parse(body));
        Assert.Null(Parse([.. body, 0]));
        Assert.Null(Parse(body[..^1]));
        Assert.Null(Parse([8, 1, .. new byte[2049]]));
    }

    [Fact]
    public async Task Explicit_exit_refunds_exchange_currency_and_returns_items_before_closing_activity()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using var server = IsolatedHadesServer.Prepare(startTogether: (20015, 2, 35));
        Waiting.MakeGameMaster(server, "refund");
        server.Start(TimeSpan.FromMinutes(2));
        LoginFlow.TryCreateAccount(server, "refund");
        LoginFlow.TryCreateAccount(server, "peer");
        string saved = Path.Combine(server.ContentLocation, "aislings", "refund.json");
        var character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = 1000;
        File.WriteAllText(saved, character.ToJsonString());
        using var session = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort,
            "refund", LoginFlow.SyntheticSecret, null, deadline.Token);
        using var peerSession = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort,
            "peer", LoginFlow.SyntheticSecret, null, deadline.Token);
        using var client = new WorldClient(session);
        using var peer = new WorldClient(peerSession);
        _ = client.PumpAsync(deadline.Token);
        _ = peer.PumpAsync(deadline.Token);
        await Waiting.Until(() => client.State != null && peer.State != null && peer.Serial != 0,
            "교환 시험 캐릭터들이 들어오지 못했습니다.", deadline.Token);
        await client.SayAsync("/give \"도복\" 1", deadline.Token);
        await Waiting.Until(() => client.Pack.Any(item => item.Name == "도복"), "교환할 아이템이 없습니다.", deadline.Token);
        var robe = client.Pack.First(item => item.Name == "도복");
        string occurredAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        await client.SendActivityAsync(JsonSerializer.Serialize(new { kind = "app_lifecycle", meta = new { action = "resume", occurredAt } }), deadline.Token);
        foreach (string invalid in new[] { "not-a-date", DateTimeOffset.UtcNow.AddDays(-91).ToString("O"), DateTimeOffset.UtcNow.AddDays(2).ToString("O") })
            await client.SendActivityAsync(JsonSerializer.Serialize(new { kind = "app_lifecycle", meta = new { action = "resume", occurredAt = invalid } }), deadline.Token);
        byte ordinal = 50;
        Task Exchange(byte kind, byte[] extra) => session.Connection.SendAsync(HadesCipher.EncodeSecured(0x4A, ordinal++,
            [kind, (byte)(peer.Serial >> 24), (byte)(peer.Serial >> 16), (byte)(peer.Serial >> 8), (byte)peer.Serial, .. extra],
            session.Parameters), deadline.Token);
        await Exchange(0, []);
        // Original Format4A.GetCanRead requires the original trailing byte after the offered slot.
        await Exchange(1, [(byte)robe.Slot, 0]);
        await Waiting.Until(() => !client.Pack.Any(item => item.Name == "도복"), "교환 제안에 아이템을 넣지 못했습니다.", deadline.Token);
        await Exchange(3, [0, 0, 0, 100]);
        string folder = Path.Combine(server.RunRoot, "activity");
        string Text() => string.Join("\n", Directory.GetFiles(folder, "*.jsonl").SelectMany(File.ReadAllLines));
        await Waiting.Until(() => Text().Contains("\"delta\":-100"), "교환 금화 제안 원장이 없습니다.", deadline.Token);
        await client.LogOutAsync(deadline.Token);
        await Waiting.Until(() => Text().Contains("\"kind\":\"logout\",\"player\":\"refund\""), "명시적 종료 기록이 없습니다.", deadline.Token);
        var rows = Text().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            var own = rows.Where(row => row.RootElement.GetProperty("player").GetString() == "refund").ToArray();
            Assert.Contains(own, row => row.RootElement.GetProperty("kind").GetString() == "ledger"
                && row.RootElement.GetProperty("meta").GetProperty("asset").GetString() == "gold"
                && row.RootElement.GetProperty("meta").GetProperty("delta").GetInt32() == 100
                && row.RootElement.GetProperty("meta").GetProperty("balance").GetInt32() == 1000);
            Assert.Contains(own, row => row.RootElement.GetProperty("kind").GetString() == "ledger"
                && row.RootElement.GetProperty("meta").GetProperty("asset").GetString() == "item"
                && row.RootElement.GetProperty("meta").GetProperty("from").GetString() == "exchange"
                && row.RootElement.GetProperty("meta").GetProperty("to").GetString() == "inventory"
                && row.RootElement.GetProperty("meta").GetProperty("delta").GetInt32() == 0);
            var lifecycle = Assert.Single(own, row => row.RootElement.GetProperty("kind").GetString() == "app_lifecycle");
            Assert.Equal(occurredAt, lifecycle.RootElement.GetProperty("meta").GetProperty("occurredAt").GetString());
            Assert.True(DateTimeOffset.Parse(lifecycle.RootElement.GetProperty("at").GetString()!) > DateTimeOffset.Parse(occurredAt));
            Assert.Equal(0, own.Where(row => row.RootElement.GetProperty("kind").GetString() == "state")
                .Sum(row => row.RootElement.GetProperty("gold").GetInt32()));
        }
        finally { foreach (var row in rows) row.Dispose(); }
    }

    [Fact]
    public async Task Authenticated_session_records_login_requests_and_disconnect_without_packet_secrets()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare(startTogether: (20015, 2, 35));
        string config = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        var settings = JsonNode.Parse(File.ReadAllText(config))!;
        settings["ServerConfig"]!["GameMasters"] = new JsonArray("audit");
        File.WriteAllText(config, settings.ToJsonString());
        server.Start(TimeSpan.FromMinutes(2));
        using (var unauthenticated = Hades718TestClient.Connect(server.GamePort))
        {
            unauthenticated.UseEncryption(new PacketFrame(0, [0, 0, 0, 0, 0, 0, 9, .. Encoding.ASCII.GetBytes("NexonInc.")]));
            unauthenticated.SendSecured(0xF3, 0, AppActivity.Packet("{\"kind\":\"app_device\",\"meta\":{\"platform\":\"unauthenticated\"}}"));
            await Task.Delay(150, deadline.Token);
        }
        LoginFlow.TryCreateAccount(server, "audit");
        string saved = Path.Combine(server.ContentLocation, "aislings", "audit.json");
        var character = JsonNode.Parse(File.ReadAllText(saved))!;
        character["GoldPoints"] = 1000;
        character["Path"] = 5;
        File.WriteAllText(saved, character.ToJsonString());
        // A refused login is recorded by the login server, with no credential payload.
        await Assert.ThrowsAnyAsync<Exception>(() => HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort,
            "audit", "wrong-secret", null, deadline.Token));
        WorldSession session = await HadesLoginClient.LoginAsync(IPAddress.Loopback, server.LoginPort,
            "audit", LoginFlow.SyntheticSecret, null, deadline.Token);
        using WorldClient client = new(session);
        _ = client.PumpAsync(deadline.Token);
        string folder = Path.Combine(server.RunRoot, "activity");
        string Text() => Directory.Exists(folder) ? string.Join("\n", Directory.GetFiles(folder, "*.jsonl").SelectMany(File.ReadAllLines)) : "";
        await Waiting.Until(() => Text().Contains("\"kind\":\"login\""), "접속 기록이 없습니다.", deadline.Token);
        await client.SendActivityAsync("{\"kind\":\"app_device\",\"meta\":{\"platform\":\"iOS\",\"model\":\"iPhone16,1\",\"install\":\"5de69df2-14f3-43d3-8998-5cd711c0bdf1\",\"beforeLogin\":true}}", deadline.Token);
        await client.SendActivityAsync("{\"kind\":\"app_error\",\"meta\":{\"password\":\"forbidden-secret\"}}", deadline.Token);
        await client.SendActivityAsync("{\"kind\":\"app_button\",\"meta\":{\"action\":\"bad input\"}}", deadline.Token);
        for (int i = 0; i < 61; i++)
            await client.SendActivityAsync("{\"kind\":\"app_button\",\"meta\":{\"action\":\"rate_limit_check\"}}", deadline.Token);
        await client.DropGoldAsync(100, new Tile(2, 35), deadline.Token);
        await Waiting.Until(() => Text().Contains("\"kind\":\"ledger\""), "실제 금화 변경 기록이 없습니다.", deadline.Token);
        await client.SayAsync("/give \"도복\" 1", deadline.Token);
        await Waiting.Until(() => client.Pack.Any(item => item.Name == "도복"), "시험 아이템을 받지 못했습니다.", deadline.Token);
        var robe = client.Pack.First(item => item.Name == "도복");
        await client.UseAsync(robe.Slot, deadline.Token);
        await Waiting.Until(() => client.Worn.Any(item => item.Name == "도복"), "시험 아이템을 입지 못했습니다.", deadline.Token);
        await Waiting.Until(() => Text().Contains("\"to\":\"equipment\""), "장비 이동 원장이 없습니다.", deadline.Token);
        // 존재하지 않는 슬롯도 행동 요청이다. 성공한 사용으로 표시하면 안 된다.
        await client.UseSkillAsync(255, deadline.Token);
        await client.UseSkillAsync(255, deadline.Token);
        var heard = client.LastHeard;
        await client.AskProfileAsync(deadline.Token);
        await Waiting.Until(() => client.LastHeard > heard, "서버가 요청을 처리하지 않았습니다.", deadline.Token);
        // An idle authenticated connection still produces a durable 60-second heartbeat.
        await Waiting.Until(() => Text().Contains("\"kind\":\"heartbeat\""), "유휴 접속 heartbeat가 없습니다.", deadline.Token, TimeSpan.FromSeconds(70));
        session.Dispose();
        await Waiting.Until(() => Text().Contains("\"kind\":\"logout\""), "접속 종료 기록이 없습니다.", deadline.Token);
        var rows = Text().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            Assert.Single(rows, row => row.RootElement.GetProperty("kind").GetString() == "login");
            Assert.Single(rows, row => row.RootElement.GetProperty("kind").GetString() == "logout");
            var action = Assert.Single(rows, row => row.RootElement.GetProperty("kind").GetString() == "request_skill");
            Assert.Equal(2, action.RootElement.GetProperty("count").GetInt32());
            var device = Assert.Single(rows, row => row.RootElement.GetProperty("kind").GetString() == "app_device");
            Assert.True(device.RootElement.GetProperty("meta").GetProperty("reported").GetBoolean());
            Assert.Equal("audit", device.RootElement.GetProperty("player").GetString());
            var failure = Assert.Single(rows, row => row.RootElement.GetProperty("kind").GetString() == "login_failure");
            Assert.Equal("password", failure.RootElement.GetProperty("meta").GetProperty("reason").GetString());
            var ledger = Assert.Single(rows, row => row.RootElement.GetProperty("kind").GetString() == "ledger"
                && row.RootElement.GetProperty("meta").GetProperty("asset").GetString() == "gold");
            Assert.Equal(-100, ledger.RootElement.GetProperty("meta").GetProperty("delta").GetInt32());
            Assert.Equal(900, ledger.RootElement.GetProperty("meta").GetProperty("balance").GetInt32());
            Assert.Equal(0, ledger.RootElement.GetProperty("gold").GetInt32());
            var items = rows.Where(row => row.RootElement.GetProperty("kind").GetString() == "ledger"
                && row.RootElement.GetProperty("meta").GetProperty("asset").GetString() == "item").ToArray();
            Assert.Contains(items, row => row.RootElement.GetProperty("meta").GetProperty("item").GetString() == "도복"
                && row.RootElement.GetProperty("meta").GetProperty("delta").GetInt32() == 1);
            Assert.Contains(items, row => row.RootElement.GetProperty("meta").GetProperty("item").GetString() == "도복"
                && row.RootElement.GetProperty("meta").GetProperty("delta").GetInt32() == 0
                && row.RootElement.GetProperty("meta").GetProperty("from").GetString() == "inventory"
                && row.RootElement.GetProperty("meta").GetProperty("to").GetString() == "equipment");
            Assert.DoesNotContain(rows, row => row.RootElement.GetProperty("kind").GetString() == "app_error");
            Assert.Equal(59, rows.Count(row => row.RootElement.GetProperty("kind").GetString() == "app_button"));
            Assert.DoesNotContain("bad input", Text());
            Assert.DoesNotContain("unauthenticated", Text());
            Assert.DoesNotContain("wrong-secret", Text());
            Assert.DoesNotContain("forbidden-secret", Text());
            Assert.DoesNotContain(LoginFlow.SyntheticSecret, Text());
            Assert.DoesNotContain("Password", Text());
            Assert.Contains(rows, row => row.RootElement.GetProperty("ip").GetString() == "127.0.0.1");
        }
        finally { foreach (var row in rows) row.Dispose(); }
    }
}
