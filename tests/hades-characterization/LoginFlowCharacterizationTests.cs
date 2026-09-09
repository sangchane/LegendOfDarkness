using System.Text.Json;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Records the normal 7.18 login flow of the unmodified server. Only the command order is frozen:
/// seeds, salts, hashes and serials change per run, and no real credential ever reaches this file.
/// </summary>
public sealed class LoginFlowCharacterizationTests
{
    [Fact]
    public void Login_reproduces_the_recorded_command_order()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        server.Start(TimeSpan.FromMinutes(2));

        IReadOnlyList<string> observed = LoginFlow.EnterWorld(server);

        Assert.Equal(LoadRecordedFlow(), observed);
    }

    private static List<string> LoadRecordedFlow()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "hades-718-login-flow.json");
        Flow flow = JsonSerializer.Deserialize<Flow>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"'{path}' is empty.");

        return flow.Steps.Select(step => $"{step.Direction} {step.Command}").ToList();
    }

    private sealed record Flow(string Description, IReadOnlyList<FlowStep> Steps);

    private sealed record FlowStep(string Direction, string Command, string Note);
}
