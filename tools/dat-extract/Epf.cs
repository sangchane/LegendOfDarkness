namespace Lod.DatExtract;

/// <summary>
/// Reads an uncompressed EPF image. Everything after the 12-byte header is addressed from byte 12, so the
/// table of contents and every frame start is an offset into that part of the file.
/// </summary>
internal static class Epf
{
    private const int HeaderLength = 12;

    internal sealed record Frame(int Left, int Top, int Width, int Height, byte[] Data);

    public static List<Frame> Read(byte[] blob)
    {
        using BinaryReader reader = new(new MemoryStream(blob));

        int expected = reader.ReadUInt16();
        reader.ReadUInt16();
        reader.ReadUInt16();
        reader.ReadUInt16();
        long toc = reader.ReadUInt32() + HeaderLength;

        List<Frame> frames = [];

        for (int index = 0; index < expected; index++)
        {
            long record = toc + (index * 16);

            if (record + 16 > blob.Length)
            {
                break;
            }

            reader.BaseStream.Seek(record, SeekOrigin.Begin);

            int top = reader.ReadUInt16();
            int left = reader.ReadUInt16();
            int bottom = reader.ReadUInt16();
            int right = reader.ReadUInt16();
            long start = reader.ReadUInt32() + HeaderLength;
            long end = reader.ReadUInt32() + HeaderLength;

            int width = right - left;
            int height = bottom - top;

            if (width <= 0 || height <= 0 || start >= blob.Length)
            {
                continue;
            }

            // A frame whose span does not match its box holds the rest of the file instead.
            long length = end - start == width * height ? end - start : toc - start;
            length = Math.Min(length, blob.Length - start);

            if (length < width * height)
            {
                continue;
            }

            byte[] data = new byte[width * height];
            Array.Copy(blob, start, data, 0, data.Length);

            frames.Add(new Frame(left, top, width, height, data));
        }

        return frames;
    }
}
