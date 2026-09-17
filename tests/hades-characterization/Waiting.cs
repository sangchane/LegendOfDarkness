using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 5.99 시험들이 함께 쓰는 것 — 조건이 설 때까지 기다리기, 시험 캐릭터를 운영자로 만들기. 파일마다 따로 적어 두었더니
/// 기다리는 시간만 서로 달라져 있었다.
/// </summary>
internal static class Waiting
{
    /// <summary>조건이 설 때까지 50ms 마다 본다. <paramref name="within" /> 안에 서지 않으면 <paramref name="failure" /> 로 실패한다.</summary>
    public static async Task Until(Func<bool> condition, string failure, CancellationToken token, TimeSpan? within = null)
    {
        DateTime giveUp = DateTime.UtcNow + (within ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < giveUp)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, token);
        }

        throw new TimeoutException(failure);
    }

    /// <summary><c>/give</c> 같은 운영자 명령을 쓰도록 격리 서버 설정의 운영자 목록에 넣는다(서버를 켜기 전에).</summary>
    public static void MakeGameMaster(IsolatedHadesServer server, string name)
    {
        string path = Path.Combine(server.RunRoot, HadesWorkspace.ConfigFileName);
        JsonNode config = JsonNode.Parse(File.ReadAllText(path))!;
        config["ServerConfig"]!["GameMasters"] = new JsonArray(name);
        File.WriteAllText(path, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// 캐릭터 저장 파일에 5.99 스크립트 값(<c>PackVariables</c>)이 적혔는지. 서버가 쓰는 도중에 읽으면 잘린 글이 올 수 있어
    /// 읽지 못한 것은 "아직" 으로 본다.
    /// </summary>
    public static bool Saved(IsolatedHadesServer server, string name, string variable, string value)
    {
        try
        {
            string path = Path.Combine(server.ContentLocation, "aislings", $"{name}.json");
            JsonNode? saved = JsonNode.Parse(File.ReadAllText(path));
            return saved?["PackVariables"]?[variable]?.GetValue<string>() == value;
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }
}
