using System.Text;

namespace Lod.CompanionBot;

/// <summary>
/// 봇 기록 — 화면(systemd journal)과 파일에 함께 적는다. 파일은 <see cref="MaxBytes" /> 를 넘으면 <c>.1</c>·<c>.2</c>… 로 밀려
/// <see cref="Keep" /> 개까지만 남는다(먹통을 뒤에 가려낼 수 있게, 그러나 디스크를 채우지 않게).
/// </summary>
public sealed class BotLog(string name, string? file, long maxBytes = BotLog.MaxBytes, int keep = BotLog.Keep)
{
    public const long MaxBytes = 1024 * 1024;
    public const int Keep = 5;

    private readonly object _gate = new();

    public void Write(string line)
    {
        string stamped = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{name}] {line}";
        Console.WriteLine(stamped);

        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);

                if (File.Exists(file) && new FileInfo(file).Length >= maxBytes)
                {
                    Roll();
                }

                File.AppendAllText(file, stamped + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception failed) when (failed is IOException or UnauthorizedAccessException)
            {
                // 파일을 못 써도 봇은 돈다 — 화면(journal)에는 남는다.
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{name}] 기록 파일을 못 씁니다: {failed.Message}");
            }
        }
    }

    private void Roll()
    {
        string oldest = $"{file}.{keep - 1}";

        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (int at = keep - 2; at >= 1; at--)
        {
            if (File.Exists($"{file}.{at}"))
            {
                File.Move($"{file}.{at}", $"{file}.{at + 1}");
            }
        }

        File.Move(file!, $"{file}.1");
    }
}
