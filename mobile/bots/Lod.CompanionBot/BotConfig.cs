using System.Text.Json;
using Lod.Mobile.Core.World;

namespace Lod.CompanionBot;

/// <summary>
/// 봇의 설정 파일(<c>companion-bot.json</c>). 비밀번호가 들어 있어 커밋하지 않는다 — 예시(<c>companion-bot.example.json</c>)만
/// 저장소에 있다. 계정 이름은 서버 설정 <c>ServerConfig.CompanionBots</c> 에 적힌 이름과 같아야 한다.
/// </summary>
public sealed record BotConfig
{
    public string Host { get; init; } = "127.0.0.1";
    public int LoginPort { get; init; } = 2610;
    public string Name { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    /// <summary>맵 벽 파일(<c>map번호.txt</c>, 앱의 <c>mobile/client/assets/world</c>) 폴더. 설정 파일 자리에서 센다. 없으면 벽을 모른 채 걷는다.</summary>
    public string MapFolder { get; init; } = string.Empty;

    public int HealOwnerPercent { get; init; } = 70;
    public int HealSelfPercent { get; init; } = 50;
    public int PotionHealthPercent { get; init; } = 40;
    public int PotionManaPercent { get; init; } = 30;

    public CompanionSettings Settings => new(HealOwnerPercent, HealSelfPercent,
        PotionHealthPercent: PotionHealthPercent, PotionManaPercent: PotionManaPercent);

    public static BotConfig Load(string path)
    {
        BotConfig config = JsonSerializer.Deserialize<BotConfig>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"'{path}' 가 비어 있습니다.");

        if (config.Name.Length == 0 || config.Password.Length == 0)
        {
            throw new InvalidDataException($"'{path}' 에 Name 과 Password 를 적어 주세요.");
        }

        string folder = config.MapFolder.Length == 0
            ? string.Empty
            : Path.GetFullPath(config.MapFolder, Path.GetDirectoryName(Path.GetFullPath(path))!);

        return config with { MapFolder = folder };
    }
}
