using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

public sealed class IsolatedHadesServerTests
{
    private const int ManualLoginPort = 2610;
    private const int ManualGamePort = 2615;

    [Fact]
    public void Run_root_is_outside_the_repository()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        Assert.False(
            server.RunRoot.StartsWith(HadesWorkspace.RepositoryRoot, StringComparison.OrdinalIgnoreCase),
            $"The run root '{server.RunRoot}' must not live inside the repository.");
    }

    [Fact]
    public void Workspace_points_at_the_built_server_output()
    {
        string entryPoint = Path.Combine(HadesWorkspace.StagingDirectory, "Lorule.GameServer.dll");

        Assert.True(File.Exists(entryPoint), $"'{entryPoint}' is missing. Build the server first.");
    }

    [Fact]
    public void Content_location_points_into_the_run_root()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        string contentLocation = ReadConfiguredContentLocation(server);

        Assert.StartsWith(server.RunRoot, contentLocation, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(contentLocation), $"'{contentLocation}' was not copied.");
    }

    [Fact]
    public void Ports_differ_from_the_manual_server_ports()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        JsonNode config = ReadConfig(server);
        JsonNode serverConfig = config["ServerConfig"]!;

        Assert.Equal(server.LoginPort, (int)serverConfig["LOGIN_PORT"]!);
        Assert.Equal(server.GamePort, (int)serverConfig["SERVER_PORT"]!);
        Assert.NotEqual(ManualLoginPort, server.LoginPort);
        Assert.NotEqual(ManualGamePort, server.GamePort);
    }

    [Fact]
    public void Run_root_carries_no_characters_from_the_workspace()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        string aislings = Path.Combine(ReadConfiguredContentLocation(server), "aislings");

        Assert.True(Directory.Exists(aislings), $"'{aislings}' must exist for the server to save into.");
        Assert.Empty(Directory.EnumerateFileSystemEntries(aislings));
    }

    [Fact]
    public void Dispose_removes_the_run_root()
    {
        IsolatedHadesServer server = IsolatedHadesServer.Prepare();
        string runRoot = server.RunRoot;

        server.Dispose();

        Assert.False(Directory.Exists(runRoot), $"'{runRoot}' was left behind.");
    }

    [Fact]
    public void Started_server_accepts_connections_on_the_isolated_ports()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        server.Start(TimeSpan.FromMinutes(2));

        AssertAcceptsConnection(server.LoginPort);
        AssertAcceptsConnection(server.GamePort);
    }

    [Fact]
    public void Start_fails_immediately_when_the_hardcoded_object_server_port_is_taken()
    {
        using TcpListener squatter = new(IPAddress.Loopback, HadesWorkspace.ObjectServerPort);
        squatter.Start();

        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => server.Start(TimeSpan.FromSeconds(10)));

        Assert.Contains(
            HadesWorkspace.ObjectServerPort.ToString(),
            error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Running_the_server_leaves_the_read_only_sources_untouched()
    {
        // Compared before and after rather than against a clean tree: from S1 on, the fork legitimately
        // carries work in progress. What must never change is that a run adds nothing of its own.
        string before = GitStatus(HadesWorkspace.HadesRoot);

        using (IsolatedHadesServer server = IsolatedHadesServer.Prepare())
        {
            server.Start(TimeSpan.FromMinutes(2));
        }

        Assert.Equal(before, GitStatus(HadesWorkspace.HadesRoot));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            Path.Combine(HadesWorkspace.ServerDataDirectory, "aislings")));
    }

    private static string GitStatus(string workingDirectory)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("status");
        startInfo.ArgumentList.Add("--porcelain");

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("git did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git status failed in '{workingDirectory}': {error}");
        }

        return output.Trim();
    }

    private static void AssertAcceptsConnection(int port)
    {
        using TcpClient client = new();
        client.Connect(IPAddress.Loopback, port);

        Assert.True(client.Connected, $"Nothing accepted a connection on port {port}.");
    }

    [Fact]
    public void Redirect_table_points_at_the_isolated_login_port()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        XDocument table = XDocument.Load(Path.Combine(server.RunRoot, "MServerTable.xml"));

        Assert.Equal(server.LoginPort.ToString(), table.Descendants("Port").Single().Value);
    }

    private static string ReadConfiguredContentLocation(IsolatedHadesServer server) =>
        (string)ReadConfig(server)["Content"]!["Location"]!;

    private static JsonNode ReadConfig(IsolatedHadesServer server)
    {
        string path = Path.Combine(server.RunRoot, "LoruleConfig.json");
        JsonDocumentOptions options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        return JsonNode.Parse(File.ReadAllText(path), documentOptions: options)
            ?? throw new InvalidOperationException($"'{path}' is empty.");
    }
}
