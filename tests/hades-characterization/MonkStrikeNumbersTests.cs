using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 무도가 한 방(`MonkStrike.Use`)의 배율은 기술 템플릿에 있다. 템플릿에서 빠지면 서버는 아무 말 없이 피해 0 으로 때리므로,
/// 그 기술을 쓰는 스크립트마다 템플릿에 공격력 배율이 있는지 본다.
/// </summary>
public sealed class MonkStrikeNumbersTests
{
    [Fact]
    public void Every_monk_strike_has_its_multipliers_in_the_template()
    {
        string scripts = Path.Combine(HadesWorkspace.ServerDataDirectory, "scripts", "Skills", "Monk");
        Dictionary<string, JsonNode> templates = Directory
            .GetFiles(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "skills"), "*.json")
            .Select(path => JsonNode.Parse(File.ReadAllText(path))!)
            .Where(template => template["ScriptName"] is not null)
            .GroupBy(template => (string)template["ScriptName"]!)
            .ToDictionary(group => group.Key, group => group.First());

        List<string> strikes = [];
        foreach (string path in Directory.GetFiles(scripts, "*.cs"))
        {
            string code = File.ReadAllText(path);
            if (!code.Contains("MonkStrike.Use(", StringComparison.Ordinal) || Path.GetFileName(path) == "MonkStrike.cs")
                continue;

            string name = Regex.Match(code, "\\[Script\\(\"([^\"]+)\"").Groups[1].Value;
            strikes.Add(name);
            Assert.True(templates.TryGetValue(name, out JsonNode? template), $"{name}: 템플릿이 없다");
            Assert.True((int?)template!["AttackPercent"] > 0, $"{name}: 템플릿에 AttackPercent 가 없다");
            Assert.True((int?)template["EndurancePercent"] >= 0, $"{name}: 템플릿에 EndurancePercent 가 없다");
        }

        Assert.Equal(14, strikes.Count);
    }
}
