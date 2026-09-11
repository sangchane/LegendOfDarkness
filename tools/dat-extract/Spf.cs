using Lorule.Content.Editor.Dat;

namespace Lod.DatExtract;

/// <summary>
/// Reads an SPF image. Unlike EPF, an SPF carries its own 256-colour palette, so nothing outside the file
/// decides how it looks — which is why the newer interface art (<c>_nui_*.spf</c> in <c>setoa.dat</c>) is
/// kept in this format while the older screens are EPF plus one of the <c>gui##.pal</c> tables.
/// </summary>
internal static class Spf
{
    private const int PaletteSize = 256;

    /// <summary>Palettized files hold one byte per pixel; anything else holds the colour itself.</summary>
    private const int Palettized = 0;

    internal sealed record Frame(int Width, int Height, byte[] Data);

    internal sealed record Sheet(Palette Palette, List<Frame> Frames);

    public static Sheet Read(byte[] blob)
    {
        using BinaryReader reader = new(new MemoryStream(blob));

        reader.ReadUInt32();
        reader.ReadUInt32();
        uint format = reader.ReadUInt32();

        if (format != Palettized)
        {
            throw new InvalidDataException($"SPF 형식 {format} 은 아직 못 읽는다 (팔레트가 없는 판이다).");
        }

        Palette palette = new();

        // Two palettes follow: 256 colours in RGB565, then the same in RGB555. Only the first is used.
        for (int index = 0; index < PaletteSize; index++)
        {
            palette.Colors[index] = FromRgb565(reader.ReadUInt16());
        }

        reader.BaseStream.Seek(PaletteSize * sizeof(ushort), SeekOrigin.Current);

        int expected = (int)reader.ReadUInt32();
        List<(int Width, int Height, long Start, int Length)> records = [];

        for (int index = 0; index < expected; index++)
        {
            int left = reader.ReadUInt16();
            int top = reader.ReadUInt16();
            int right = reader.ReadUInt16();
            int bottom = reader.ReadUInt16();
            reader.ReadUInt32();
            reader.ReadUInt32();
            long start = reader.ReadUInt32();
            reader.ReadUInt32();
            int length = (int)reader.ReadUInt32();
            reader.ReadUInt32();

            records.Add((right - left, bottom - top, start, length));
        }

        // Every frame start is an offset into the block that begins after this count, not into the file.
        reader.ReadUInt32();
        long origin = reader.BaseStream.Position;

        List<Frame> frames = [];

        foreach ((int width, int height, long start, int length) in records)
        {
            if (width <= 0 || height <= 0 || origin + start + length > blob.Length)
            {
                continue;
            }

            byte[] data = new byte[width * height];
            Array.Copy(blob, origin + start, data, 0, Math.Min(data.Length, length));
            frames.Add(new Frame(width, height, data));
        }

        return new Sheet(palette, frames);
    }

    /// <summary>Five bits of red, six of green, five of blue, each stretched to fill a whole byte.</summary>
    private static System.Drawing.Color FromRgb565(ushort encoded)
    {
        int red = (encoded >> 11) & 0x1F;
        int green = (encoded >> 5) & 0x3F;
        int blue = encoded & 0x1F;

        return System.Drawing.Color.FromArgb(
            (red * 255) / 31,
            (green * 255) / 63,
            (blue * 255) / 31);
    }
}
