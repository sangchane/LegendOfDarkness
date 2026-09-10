using System.IO.Compression;
using System.Text;

namespace Lod.DatExtract;

/// <summary>
/// The original game's own tables, as the server hands them to a client.
/// </summary>
/// <remarks>
/// They sit in the server's database folder rather than in any .dat archive — <c>database/server/metafile/</c>
/// — and each one is a zlib stream. Inside is a plain list: how many rows, then each row as a name and a
/// handful of fields.
///
/// This is where the game says what its items are (<c>ItemInfo0</c>..<c>ItemInfo11</c>), what each class can
/// learn (<c>SClass1</c>..<c>SClass5</c>), what its quests say (<c>SEvent1</c>..<c>SEvent7</c>), which
/// portrait belongs to which merchant (<c>NPCIllust</c>) and how bright a map is (<c>Light</c>).
/// </remarks>
internal static class MetaFile
{
    internal sealed record Row(string Name, IReadOnlyList<string> Fields);

    public static List<Row> Read(string path)
    {
        byte[] raw = File.ReadAllBytes(path);

        using MemoryStream compressed = new(raw);
        using ZLibStream unpacking = new(compressed, CompressionMode.Decompress);
        using MemoryStream plain = new();

        unpacking.CopyTo(plain);

        byte[] body = plain.ToArray();
        Encoding korean = CodePagesEncodingProvider.Instance.GetEncoding(949) ?? Encoding.Default;

        int at = 0;
        int rows = Word();
        List<Row> read = [];

        for (int index = 0; index < rows && at < body.Length; index++)
        {
            string name = Text(body[at++]);
            int fields = Word();
            List<string> values = [];

            for (int field = 0; field < fields && at < body.Length; field++)
            {
                values.Add(Text(Word()));
            }

            read.Add(new Row(name, values));
        }

        return read;

        int Word()
        {
            if (at + 1 >= body.Length)
            {
                at = body.Length;

                return 0;
            }

            int value = (body[at] << 8) | body[at + 1];
            at += 2;

            return value;
        }

        string Text(int length)
        {
            length = Math.Min(length, body.Length - at);
            string value = korean.GetString(body, at, Math.Max(0, length));
            at += Math.Max(0, length);

            return value;
        }
    }
}
