using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Warps are counted by adding whatever the loader returned to a list, with no check that it returned
/// anything — so the number in "Warp Templates Loaded" is the number of files, always, and cannot go red.
/// A file that fails to parse is worse than uncounted: there is no try/catch on the way in, so one bad
/// warp ends startup for the whole server rather than being skipped and named.
/// This matters for the content port, where warps arrive 886 at a time.
/// </summary>
public sealed class WarpIntegrityTests
{
    [Fact]
    public void A_warp_that_cannot_be_read_is_skipped_and_named_rather_than_ending_startup()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        string warps = Path.Combine(server.ContentLocation, "templates", "warps");

        Assert.True(Directory.Exists(warps), $"{warps} is not where this test expects the warps to be.");

        int shipped = Directory.GetFiles(warps, "*.json").Length;

        File.WriteAllText(Path.Combine(warps, "torn.json"), "{ this is not json");

        // The server must still come up. Start throws if it never reports itself ready.
        server.Start(TimeSpan.FromMinutes(2));

        // Named, so whoever brought the file in can tell which one.
        Assert.Contains("torn", server.ConsoleOutput);

        // And left out of the count rather than added as a null that something trips over later.
        Assert.Contains($"Warp Templates Loaded: {shipped}", server.ConsoleOutput);
    }

    [Fact]
    public void The_warps_that_ship_all_point_at_areas_that_exist()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        int shipped = Directory.GetFiles(
            Path.Combine(server.ContentLocation, "templates", "warps"), "*.json").Length;

        Assert.Contains($"Warp Templates Loaded: {shipped}", server.ConsoleOutput);
        Assert.DoesNotContain("could not be read", server.ConsoleOutput);
    }
}
