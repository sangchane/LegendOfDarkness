using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Freezes the character state the unmodified server writes for a fresh synthetic account, so a later
/// stabilization change cannot quietly move a starting value.
/// </summary>
public sealed class CharacterStateCharacterizationTests
{
    [Fact]
    public void Freshly_created_character_matches_the_recorded_state()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.EnterWorld(server);

        JsonNode saved = ReadSavedCharacter(server);
        Dictionary<string, string> expected = LoadRecordedState();
        Dictionary<string, string> actual = expected.Keys.ToDictionary(
            field => field,
            field => saved[field]?.ToJsonString() ?? "<missing>");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Repeated_runs_change_only_the_explained_fields()
    {
        JsonObject first = CaptureFreshCharacter();
        JsonObject second = CaptureFreshCharacter();

        Assert.Equal(LoadVolatileFields(), FieldsThatDiffer(first, second));
    }

    private static JsonObject CaptureFreshCharacter()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.EnterWorld(server);

        return ReadSavedCharacter(server).AsObject();
    }

    private static List<string> FieldsThatDiffer(JsonObject first, JsonObject second) =>
        first.Select(field => field.Key)
            .Union(second.Select(field => field.Key))
            .Where(key => Render(first, key) != Render(second, key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

    private static string Render(JsonObject document, string key) =>
        document[key]?.ToJsonString() ?? "<missing>";

    /// <summary>Fields allowed to differ between runs. The fixture carries the reason for each one.</summary>
    private static List<string> LoadVolatileFields() =>
        ReadFixture()["volatileFields"]!.AsObject()
            .Select(field => field.Key)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToList();

    private static JsonNode ReadSavedCharacter(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.ContentLocation, "aislings", $"{LoginFlow.SyntheticName}.json");

        Assert.True(File.Exists(path), $"The server never saved '{path}'.");

        return JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"'{path}' is empty.");
    }

    private static Dictionary<string, string> LoadRecordedState()
    {
        return ReadFixture()["stableFields"]!.AsObject()
            .ToDictionary(field => field.Key, field => field.Value!.ToJsonString());
    }

    private static JsonNode ReadFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hades-718-character.json");

        return JsonNode.Parse(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"'{path}' is empty.");
    }
}
