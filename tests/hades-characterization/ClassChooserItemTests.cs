using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 전직사범(<c>ClassChooser.cs</c>)이 주는 시작 장비가 모두 서버 템플릿에 있는가. 없는 이름을
/// <c>GlobalItemTemplateCache[...]</c> 로 부르면 예외로 전직 흐름(대화창 닫기·전설·귀환)이 끊긴다 —
/// 2026-09-30 영문 템플릿을 걷어낼 때 도적의 <c>Snow Secret</c> 이 그렇게 사라졌고, 마법사·전사의
/// <c>Used Boots</c> 는 그 전부터 없었다.
/// </summary>
public sealed class ClassChooserItemTests
{
    [Fact]
    public void Every_starting_item_the_class_chooser_gives_exists()
    {
        string script = File.ReadAllText(
            Path.Combine(HadesWorkspace.ServerDataDirectory, "scripts", "Mundanes", "ClassChooser.cs"));
        string[] given = [.. Regex.Matches(script, "Item\\.Create\\(client\\.Aisling, \"([^\"]+)\"\\)")
            .Select(match => match.Groups[1].Value)];

        HashSet<string> names = [.. Directory
            .EnumerateFiles(Path.Combine(HadesWorkspace.ServerDataDirectory, "templates", "items"), "*.json")
            .Select(path => (string?)JsonNode.Parse(File.ReadAllText(path))?["Name"] ?? "")];

        Assert.NotEmpty(given);
        Assert.DoesNotContain("GlobalItemTemplateCache[", script);
        Assert.Equal([], given.Where(name => !names.Contains(name)).ToArray());
    }
}
