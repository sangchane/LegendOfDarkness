using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-11 name policy. The server used the name verbatim as a file name with no rules at all, so anything a
/// client sent became a file. Korean must keep working: see <see cref="KoreanNameTests"/>.
/// </summary>
public sealed class CharacterNamePolicyTests
{
    private static readonly string[] RefusedNames =
    [
        "a",
        "waytoolongname",
        "한글이름한글이름한글이름한글",
        "wren!",
        "two words",
        "con",
        "nul",
        "com1",
    ];

    private const string AcceptedName = "goodname";

    [Fact]
    public void Names_outside_the_policy_are_never_saved()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        foreach (string name in RefusedNames)
        {
            LoginFlow.TryCreateAccount(server, name);
        }

        // Control: if creation never saves anything, the check below would pass while proving nothing.
        LoginFlow.TryCreateAccount(server, AcceptedName);

        List<string> saved = Directory
            .EnumerateFiles(Path.Combine(server.ContentLocation, "aislings"))
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList()!;

        Assert.Equal([AcceptedName], saved);
    }
}
