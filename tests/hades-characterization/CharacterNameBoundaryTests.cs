using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// P0-11: a character name reaches the save path directly, so a name that walks out of the character
/// directory must never put a file anywhere else.
/// </summary>
public sealed class CharacterNameBoundaryTests
{
    private static readonly string[] EscapingNames =
    [
        "../escaped",
        "..\\escaped",
        "../../escaped",
        "sub/escaped",
        "sub\\escaped",
    ];

    [Fact]
    public void A_name_that_walks_out_of_the_character_directory_writes_nothing_outside_it()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        HashSet<string> before = SnapshotFiles(server.RunRoot);

        foreach (string name in EscapingNames)
        {
            LoginFlow.TryCreateAccount(server, name);
        }

        // Control: if creation never writes anything, the check above would pass while proving nothing.
        LoginFlow.TryCreateAccount(server, "wellformed");
        Assert.True(
            File.Exists(Path.Combine(server.ContentLocation, "aislings", "wellformed.json")),
            "A well-formed name was not saved, so this test cannot judge the escaping ones.");

        List<string> stray = SnapshotFiles(server.RunRoot)
            .Except(before)
            .Where(path => !IsWhereCharactersBelong(server, path) && !IsServerLog(server, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        Assert.Equal([], stray);
    }

    /// <summary>Characters belong directly in the character directory, never in a subdirectory of it.</summary>
    private static bool IsWhereCharactersBelong(IsolatedHadesServer server, string path) =>
        string.Equals(
            Path.GetDirectoryName(path),
            Path.GetRelativePath(server.RunRoot, Path.Combine(server.ContentLocation, "aislings")),
            StringComparison.OrdinalIgnoreCase);

    private static bool IsServerLog(IsolatedHadesServer server, string path) =>
        Path.GetDirectoryName(path)?.Length == 0
        && Path.GetFileName(path).StartsWith("Hades_", StringComparison.Ordinal);

    /// <summary>Paths are kept relative to the run root so a failure names the file, not a temp path.</summary>
    private static HashSet<string> SnapshotFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
