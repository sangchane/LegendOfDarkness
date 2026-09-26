using Lod.CompanionBot;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.World;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 봇 프로그램의 첫 접속을 클라우드와 같은 조건으로: 계정이 없는 상태 · 이름 "동료사제"(한글 넷) · 영숫자 10자 비밀번호.
/// 로그인이 거절되면 성직자로 만들고 들어가 기다린다 — 두 번째 접속(다시 붙기)도 같은 계정으로 들어간다.
/// </summary>
public sealed class CompanionBotLoginTests : IDisposable
{
    private readonly CancellationTokenSource _deadline = new(TimeSpan.FromMinutes(3));

    public void Dispose() => _deadline.Dispose();

    [Fact]
    public async Task A_new_bot_account_is_created_as_a_priest_and_logs_in_again_later()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        BotConfig config = new() { Host = "127.0.0.1", LoginPort = server.LoginPort, Name = "동료사제", Password = "a1b2c3d4e5" };
        List<string> said = [];

        foreach (int round in new[] { 1, 2 })
        {
            WorldSession session;

            try
            {
                session = await BotLogin.EnterAsync(config, said.Add, _deadline.Token);
            }
            catch (Exception failed)
            {
                throw new Xunit.Sdk.XunitException(
                    $"{round}번째 접속 실패: {BotLogin.Describe(failed)} · 봇이 한 말: {string.Join(" | ", said)}{Environment.NewLine}{server.ConsoleOutput[^Math.Min(3000, server.ConsoleOutput.Length)..]}");
            }

            using (session)
            {
                WorldClient world = new(session);
                _ = world.PumpAsync(_deadline.Token);
                await Waiting.Until(() => world.State is not null && world.Self?.Name == config.Name,
                    $"{round}번째 접속에서 월드에 서지 못했습니다.", _deadline.Token);
                await world.LogOutAsync(_deadline.Token);
            }
        }

        Assert.Equal(["계정이 없어 성직자로 만듭니다."], said);
        System.Text.Json.Nodes.JsonNode saved = System.Text.Json.Nodes.JsonNode.Parse(
            File.ReadAllText(Path.Combine(server.ContentLocation, "aislings", "동료사제.json")))!;
        Assert.Contains(saved["Path"]!.ToJsonString().Trim('"'), new[] { "4", "Priest" });
    }
}
