using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Lod.Hades.Characterization;

/// <summary>
/// Runs the unmodified Hades server from a throwaway copy so characterization tests never touch
/// the read-only submodule, the manual run directory, or real character data.
/// </summary>
public sealed class IsolatedHadesServer : IDisposable
{
    private const string CharacterDirectoryName = "aislings";
    private const string LoginOnlineSignal = "Login server is online.";
    private const string GameOnlineSignal = "Game server is online.";
    private const string RedirectTableFileName = "MServerTable.xml";

    /// <summary>How long the isolated server tolerates a frame that stopped half way.</summary>
    public const int IncompleteFrameTimeoutSeconds = 3;

    private readonly StringBuilder _console = new();
    private readonly ManualResetEventSlim _ready = new();
    private Process? _process;
    private bool _loginOnline;
    private bool _gameOnline;

    private IsolatedHadesServer(string runRoot, string contentLocation, int loginPort, int gamePort, int objectPort)
    {
        RunRoot = runRoot;
        ContentLocation = contentLocation;
        LoginPort = loginPort;
        GamePort = gamePort;
        ObjectPort = objectPort;
    }

    public string RunRoot { get; }

    /// <summary>The copied server data the run writes into, including its character directory.</summary>
    public string ContentLocation { get; }

    public int LoginPort { get; }

    public int GamePort { get; }

    /// <summary>The object server's port for this run. Its own, so two runs do not collide.</summary>
    public int ObjectPort { get; }

    public static IsolatedHadesServer Prepare()
    {
        string runRoot = Path.Combine(Path.GetTempPath(), "lod-hades-harness", Guid.NewGuid().ToString("N"));
        string contentLocation = Path.Combine(runRoot, "database", "server");

        RequireBuiltServer();
        CopyDirectory(HadesWorkspace.StagingDirectory, runRoot);
        CopyDirectory(HadesWorkspace.ServerDataDirectory, contentLocation, skipDirectory: CharacterDirectoryName);
        Directory.CreateDirectory(Path.Combine(contentLocation, CharacterDirectoryName));
        Directory.CreateDirectory(Path.Combine(runRoot, "game"));

        (int loginPort, int gamePort, int objectPort) = ReserveFreePorts();
        WriteIsolatedConfig(runRoot, contentLocation, loginPort, gamePort);
        WriteIsolatedRedirectTable(runRoot, loginPort);

        return new IsolatedHadesServer(runRoot, contentLocation, loginPort, gamePort, objectPort);
    }

    /// <summary>Launches the copied server and waits until both listeners report themselves online.</summary>
    public void Start(TimeSpan readinessTimeout)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = RunRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(HadesWorkspace.ServerEntryPoint);

        // The object server's address used to be written into the code, so a machine could hold one server
        // at a time. It is a configuration key now, and the environment overrides the file.
        startInfo.Environment["ServerConfig__ObjectServerPort"] = ObjectPort.ToString(CultureInfo.InvariantCulture);

        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => Observe(e.Data);
        process.ErrorDataReceived += (_, e) => Observe(e.Data);
        process.Exited += (_, _) => _ready.Set();

        _process = process;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _ready.Wait(readinessTimeout);

        if (!_loginOnline || !_gameOnline)
        {
            throw new InvalidOperationException(
                $"The isolated Hades server did not report both listeners online within {readinessTimeout}." +
                $"{Environment.NewLine}{ConsoleOutput}");
        }
    }

    /// <summary>Everything the server wrote to stdout and stderr so far.</summary>
    public string ConsoleOutput
    {
        get
        {
            lock (_console)
            {
                return _console.ToString();
            }
        }
    }

    /// <summary>
    /// How many OS threads the server is running. A connection that ends must leave this where it found it;
    /// a per-connection thread that outlives its connection is invisible to a socket count but ends the
    /// server just as surely, once there is no thread left to answer with.
    /// </summary>
    public int ThreadCount
    {
        get
        {
            Process? process = _process;

            if (process == null || process.HasExited)
            {
                return 0;
            }

            process.Refresh();

            return process.Threads.Count;
        }
    }

    public void Dispose()
    {
        StopProcess();

        if (Directory.Exists(RunRoot))
        {
            Directory.Delete(RunRoot, recursive: true);
        }
    }

    private void StopProcess()
    {
        if (_process is not { } process)
        {
            return;
        }

        _process = null;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit(milliseconds: 15_000);
        }
        catch (InvalidOperationException)
        {
            // The process was never started or already reaped.
        }

        process.Dispose();
    }

    private void Observe(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_console)
        {
            _console.AppendLine(line);
        }

        _loginOnline |= line.Contains(LoginOnlineSignal, StringComparison.Ordinal);
        _gameOnline |= line.Contains(GameOnlineSignal, StringComparison.Ordinal);

        if (_loginOnline && _gameOnline)
        {
            _ready.Set();
        }
    }

    private static void RequireBuiltServer()
    {
        string entryPoint = Path.Combine(HadesWorkspace.StagingDirectory, HadesWorkspace.ServerEntryPoint);

        if (!File.Exists(entryPoint))
        {
            throw new InvalidOperationException(
                $"'{entryPoint}' is missing. Build it first: " +
                "dotnet build sources/wren11/Dark-Ages-Private-Server/src/Hades.sln -c Debug");
        }
    }

    private static void WriteIsolatedConfig(string runRoot, string contentLocation, int loginPort, int gamePort)
    {
        string configPath = Path.Combine(runRoot, HadesWorkspace.ConfigFileName);
        JsonDocumentOptions options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        JsonNode config = JsonNode.Parse(File.ReadAllText(configPath), documentOptions: options)
            ?? throw new InvalidOperationException($"'{configPath}' is empty.");

        config["Content"]!["Location"] = contentLocation;
        config["Editor"]!["Location"] = Path.Combine(runRoot, "database");
        config["Editor"]!["GameLocation"] = Path.Combine(runRoot, "game");
        // Short so the suite does not sit through the production default while proving the sweep runs.
        config["ServerConfig"]!["IncompleteFrameTimeoutSeconds"] = IncompleteFrameTimeoutSeconds;
        config["ServerConfig"]!["LOGIN_PORT"] = loginPort;
        config["ServerConfig"]!["SERVER_PORT"] = gamePort;

        File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// The lobby redirect sends the client to <c>MServerTable.xml</c>, which ships with port 2610, so an
    /// isolated run would hand the client back to the manual server.
    /// </summary>
    private static void WriteIsolatedRedirectTable(string runRoot, int loginPort)
    {
        string tablePath = Path.Combine(runRoot, RedirectTableFileName);
        XDocument table = XDocument.Load(tablePath);

        foreach (XElement port in table.Descendants("Port"))
        {
            port.Value = loginPort.ToString();
        }

        table.Save(tablePath);
    }

    // ponytail: the ports are released before the server binds them; a colliding process would have to
    // grab one inside that window. Retry the whole preparation if that ever shows up as a flake.
    private static (int LoginPort, int GamePort, int ObjectPort) ReserveFreePorts()
    {
        using TcpListener login = new(IPAddress.Loopback, 0);
        using TcpListener game = new(IPAddress.Loopback, 0);
        using TcpListener objects = new(IPAddress.Loopback, 0);
        login.Start();
        game.Start();
        objects.Start();

        return (
            ((IPEndPoint)login.LocalEndpoint).Port,
            ((IPEndPoint)game.LocalEndpoint).Port,
            ((IPEndPoint)objects.LocalEndpoint).Port);
    }

    private static void CopyDirectory(string source, string destination, string? skipDirectory = null)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            string name = Path.GetFileName(directory);

            if (string.Equals(name, skipDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyDirectory(directory, Path.Combine(destination, name), skipDirectory);
        }
    }
}
