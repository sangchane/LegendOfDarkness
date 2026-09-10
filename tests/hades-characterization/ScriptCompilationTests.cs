using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// The server compiles the scripts under its database folder at startup, and everything a script drives
/// depends on that having worked: monsters spawning, mundanes appearing, monster behaviour at all.
/// </summary>
/// <remarks>
/// It fails quietly. A failed compilation loads no assembly, leaves the script table empty, and reports the
/// reason at information level among a hundred other startup lines — so the world simply stays empty and
/// looks like a world nobody ever populated. It had been failing on the development server since before any
/// of this work began, on nothing worse than the original game's mss32.dll sitting in a folder nearby: every
/// file ending in .dll under the working directory is handed to the compiler as a reference, and one that is
/// not a managed assembly fails the whole compilation.
///
/// So this puts such a file where the server will find it. Without it the test proves nothing — the isolated
/// run root has no native libraries of its own, which is exactly why the fault survived so long unseen.
/// </remarks>
public sealed class ScriptCompilationTests
{
    private const string Finished = "Compiling all scripts... completed.";

    [Fact]
    public void Every_script_compiles_even_beside_a_file_that_is_not_an_assembly()
    {
        using IsolatedHadesServer server = IsolatedHadesServer.Prepare();

        // Named like a library, shaped like nothing: a DOS header and no managed metadata, which is what
        // the game's own audio libraries look like to a compiler.
        string intruder = Path.Combine(server.RunRoot, "game");
        Directory.CreateDirectory(intruder);
        File.WriteAllBytes(Path.Combine(intruder, "mss32.dll"), "MZ not a managed assembly"u8.ToArray());

        server.Start(TimeSpan.FromMinutes(2));

        string console = server.ConsoleOutput;

        Assert.Contains(Finished, console, StringComparison.Ordinal);

        string[] complaints =
        [
            .. console.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Contains("\"CS", StringComparison.Ordinal))
        ];

        Assert.True(
            complaints.Length == 0,
            "스크립트가 컴파일되지 않았습니다. 하나라도 실패하면 스크립트가 통째로 없는 것과 같습니다 — "
            + $"괴물도 상인도 나오지 않습니다:\n{string.Join('\n', complaints)}");
    }
}
