using System.Net;
using System.Text.Json.Nodes;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 길드말 — 귓속말(0x19)의 받는 이 자리에 "!!". 길드원에게 가고, 그것으로 끝난다.
/// </summary>
/// <remarks>
/// 전에는 길드원에게 보낸 뒤 아래 귓속말 찾기로 흘러 "!!" 라는 사람을 찾다가 "!! is nowhere to be found." 까지 보냈다
/// (GameServerHandlers.Format19Handler).
/// </remarks>
[Collection(TimedCollection.Name)]
public sealed class GuildChatTests : IDisposable
{
    private const string Name = "guildtalk";
    private const string Clan = "시험길드";
    private const byte WhisperCommand = 0x19;

    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_guild_line_reaches_the_guild_and_is_not_also_looked_up_as_a_whisper()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, Name);
        Save(server, saved => saved["Clan"] = Clan);

        using WorldSession session = await HadesLoginClient.LoginAsync(
            IPAddress.Loopback, server.LoginPort, Name, LoginFlow.SyntheticSecret, progress: null, _deadline.Token);

        WorldClient world = new(session);
        _ = world.PumpAsync(_deadline.Token);
        await Waiting.Until(() => world.State is not null, "월드에 서지 못했습니다.", _deadline.Token);

        List<(byte Type, string Text)> heard = [];

        // 들어온 직후의 말은 다 비우고 시작한다.
        await Task.Delay(1000, _deadline.Token);
        Drain(world, heard);
        heard.Clear();

        byte[] body = [.. LegacyKoreanEncoding.EncodeStringA("!!"), .. LegacyKoreanEncoding.EncodeStringA("모두 안녕")];
        await session.Connection.SendAsync(
            HadesCipher.EncodeSecured(WhisperCommand, 200, body, session.Parameters), _deadline.Token);

        string guildLine = "{=o" + Name + "> {=a모두 안녕";
        await Waiting.Until(() => Drain(world, heard).Any(line => line.Text == guildLine),
            "길드말이 제게 돌아오지 않았습니다.", _deadline.Token);

        // 귓속말 찾기로 흘렀다면 곧바로 뒤따라 온다. 조금 더 듣고 본다.
        await Task.Delay(1500, _deadline.Token);
        Drain(world, heard);

        Assert.DoesNotContain(heard, line => line.Text.Contains("!!님은", StringComparison.Ordinal)
                                             || line.Text.Contains("nowhere", StringComparison.Ordinal));
        Assert.Single(heard, line => line.Text == guildLine);
    }

    private static List<(byte Type, string Text)> Drain(WorldClient world, List<(byte Type, string Text)> heard)
    {
        while (world.TakeTold(out byte type, out string text))
        {
            heard.Add((type, text));
        }

        return heard;
    }

    private static void Save(IsolatedHadesServer server, Action<JsonNode> change)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{Name}.json");
        JsonNode saved = JsonNode.Parse(File.ReadAllText(path))!;
        change(saved);
        File.WriteAllText(path, saved.ToJsonString());
    }
}
