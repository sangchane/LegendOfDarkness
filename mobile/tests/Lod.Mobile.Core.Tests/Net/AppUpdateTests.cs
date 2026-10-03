using Lod.Mobile.Core.Ui;

namespace Lod.Mobile.Core.Tests.Net;

public sealed class AppUpdateTests
{
    [Theory]
    [InlineData("202610030900", "202610031200\n", true)]
    [InlineData("202610030900", "202610030900", false)]
    [InlineData("202610031200", "202610030900", false)]
    [InlineData(null, "202610031200", false)]
    [InlineData("202610030900", "<html>404</html>", false)]
    public void Only_a_newer_published_number_asks_for_a_new_app(string? mine, string published, bool outdated) =>
        Assert.Equal(outdated, AppUpdate.Outdated(mine, published));
}
