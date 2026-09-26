using System.Net;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol;

namespace Lod.CompanionBot;

/// <summary>봇의 로그인 — 계정이 없으면(처음 한 번) 성직자로 만든다. 프로그램과 시험이 같은 길을 탄다.</summary>
public static class BotLogin
{
    /// <summary>원작 직업 번호 — 하데스 <c>Class.Priest</c>.</summary>
    public const byte Priest = 4;

    public static async Task<WorldSession> EnterAsync(BotConfig config, Action<string> log, CancellationToken token)
    {
        IPAddress address = IPAddress.TryParse(config.Host, out IPAddress? parsed)
            ? parsed
            : (await Dns.GetHostAddressesAsync(config.Host, token))[0];

        try
        {
            return await HadesLoginClient.LoginAsync(address, config.LoginPort, config.Name, config.Password, null, token);
        }
        catch (ProtocolException refused) when (refused.Message.Contains("계정", StringComparison.Ordinal))
        {
            log("계정이 없어 성직자로 만듭니다.");
            return await HadesLoginClient.CreateCharacterAsync(
                address, config.LoginPort, config.Name, config.Password, hairStyle: 1, gender: 1, hairColor: 1, path: Priest,
                cancellationToken: token);
        }
    }

    /// <summary>기록에 남길 까닭 — 예외 종류와 안쪽 예외까지. 메시지가 빈 예외(끊긴 소켓 등)도 무엇인지 보이게.</summary>
    public static string Describe(Exception failed)
    {
        List<string> parts = [];

        for (Exception? at = failed; at is not null; at = at.InnerException)
        {
            parts.Add(string.IsNullOrWhiteSpace(at.Message) ? at.GetType().Name : $"{at.GetType().Name}: {at.Message}");
        }

        return string.Join(" ← ", parts);
    }
}
