namespace Lod.Hades.Characterization;

/// <summary>Locates the read-only Hades sources inside this workspace.</summary>
public static class HadesWorkspace
{
    public const string ServerEntryPoint = "Lorule.GameServer.dll";
    public const string ConfigFileName = "LoruleConfig.json";

    /// <summary>
    /// The object server port is hardcoded as <c>http://localhost:2620/</c> in
    /// <c>Hades.Server.Base/Network/Game/GameServer.cs</c>, so no configuration can move it and only one
    /// Hades instance can run on this machine at a time.
    /// </summary>
    public const int ObjectServerPort = 2620;

    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string HadesRoot { get; } =
        Path.Combine(RepositoryRoot, "sources", "wren11", "Dark-Ages-Private-Server");

    /// <summary>Build output of <c>dotnet build src/Hades.sln -c Debug</c>.</summary>
    public static string StagingDirectory { get; } = Path.Combine(HadesRoot, "Staging", "net5.0");

    /// <summary>Server content the running server rewrites in place, so tests only ever use a copy.</summary>
    public static string ServerDataDirectory { get; } = Path.Combine(HadesRoot, "database", "server");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"No workspace root containing AGENTS.md was found above '{AppContext.BaseDirectory}'.");
    }
}
