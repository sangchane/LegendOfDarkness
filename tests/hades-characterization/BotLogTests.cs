using Lod.CompanionBot;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>봇 기록 파일은 크기를 넘으면 밀려 나고 정한 개수까지만 남는다(2026-09-27 — 먹통을 뒤에 가려내려고 파일로도 남긴다).</summary>
public sealed class BotLogTests
{
    [Fact]
    public void The_log_file_rolls_over_and_keeps_only_a_few()
    {
        string folder = Path.Combine(Path.GetTempPath(), $"botlog-{Guid.NewGuid():N}");
        string file = Path.Combine(folder, "logs", "companion-bot.log");

        try
        {
            BotLog log = new("시험봇", file, maxBytes: 200, keep: 3);

            for (int line = 0; line < 100; line++)
            {
                log.Write($"줄 {line} — 주인 serial 12345 · 맵 20028");
            }

            string[] files = Directory.GetFiles(Path.GetDirectoryName(file)!).Order().ToArray();

            Assert.Equal([file, $"{file}.1", $"{file}.2"], files);
            Assert.All(files, one => Assert.True(new FileInfo(one).Length < 400, $"{one} 가 너무 큽니다."));
            Assert.Contains("줄 99", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
