using Darkages.Storage;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// Saving a character used to empty its file and then fill it, so a process that stopped in between left
/// nothing behind. These say what has to be true instead.
/// </summary>
public sealed class SafeFileTests : IDisposable
{
    private readonly string _folder = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), $"lod-safefile-{Guid.NewGuid():N}")).FullName;

    private string Saved => Path.Combine(_folder, "character.json");

    [Fact]
    public void The_previous_contents_are_kept_beside_the_new_ones()
    {
        SafeFile.Write(Saved, "first");
        SafeFile.Write(Saved, "second");

        Assert.Equal("second", File.ReadAllText(Saved));
        Assert.Equal("first", File.ReadAllText(SafeFile.BackupPath(Saved)));
    }

    [Fact]
    public async Task Two_saves_at_once_leave_one_whole_file_rather_than_a_splice()
    {
        // Long enough that writes overlapping in the file would show as a mixture rather than one of them.
        string[] versions = [.. Enumerable.Range(0, 8).Select(who => new string((char)('a' + who), 200_000))];

        await Task.WhenAll(versions.Select(version => Task.Run(() =>
        {
            for (int round = 0; round < 20; round++)
            {
                SafeFile.Write(Saved, version);
            }
        })));

        string left = File.ReadAllText(Saved);

        Assert.Contains(left, versions);
    }

    [Fact]
    public void Reading_falls_back_to_what_the_previous_save_left()
    {
        SafeFile.Write(Saved, "the one before");
        SafeFile.Write(Saved, "the current one");

        // What a save cut short in the old way left behind.
        File.WriteAllText(Saved, "{ half a chara");

        Assert.Equal("the one before", SafeFile.Read(Saved, text => !text.StartsWith('{')));
    }

    [Fact]
    public void Reading_gives_up_when_neither_can_be_used()
    {
        SafeFile.Write(Saved, "bad");

        Assert.Null(SafeFile.Read(Saved, _ => false));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
