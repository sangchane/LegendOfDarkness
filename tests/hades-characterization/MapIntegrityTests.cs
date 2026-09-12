using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// A map file is its area's dimensions times six bytes, exactly. Anything shorter used to load: the reader
/// filled the tiles it ran out of data for with wall, reported success, and the area cached and counted
/// towards "Map Templates Loaded" — so a map that is one sealed room looked exactly like a map that worked.
/// This matters for the content port, where maps arrive from an archive a few hundred at a time.
/// </summary>
public sealed class MapIntegrityTests
{
    /// <summary>Refugee Camp: 70x70, so 29,400 bytes.</summary>
    private const int RefugeeCampId = 2;

    private const int AreasShipped = 4;

    [Fact]
    public void A_map_that_is_not_its_declared_size_is_refused_and_named()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        string map = Path.Combine(server.ContentLocation, "maps", $"lod{RefugeeCampId}.map");

        Assert.True(File.Exists(map), $"{map} is not where this test expects the map files to be.");

        File.WriteAllBytes(map, []);

        server.Start(TimeSpan.FromMinutes(2));

        // Named, so whoever brought the file in can tell which one and what was wrong with it.
        Assert.Contains($"Map {RefugeeCampId}", server.ConsoleOutput);
        Assert.Contains("expected 29400 for 70x70", server.ConsoleOutput);
        Assert.Contains("Not loaded", server.ConsoleOutput);

        // And left out of the world rather than cached as a room made of wall.
        Assert.Contains($"Map Templates Loaded: {AreasShipped - 1}", server.ConsoleOutput);
    }

    [Fact]
    public void The_maps_that_ship_are_all_their_declared_size()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        Assert.DoesNotContain("Not loaded", server.ConsoleOutput);
        Assert.Contains($"Map Templates Loaded: {AreasShipped}", server.ConsoleOutput);
    }
}
