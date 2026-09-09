using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The 7.18 protocol carries text as CP949, so Korean names travel fine even though the English client
/// cannot type them. The mobile client will send them, so any name policy must keep working for Korean.
/// </summary>
public sealed class KoreanNameTests
{
    private const string KoreanName = "한글이름";

    [Fact]
    public void A_Korean_name_is_accepted_and_saved()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        LoginFlow.TryCreateAccount(server, KoreanName);

        string expected = Path.Combine(server.ContentLocation, "aislings", $"{KoreanName}.json");

        Assert.True(File.Exists(expected), $"A Korean name was not saved as '{expected}'.");
    }
}
