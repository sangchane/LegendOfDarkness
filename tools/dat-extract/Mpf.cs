namespace Lod.DatExtract;

/// <summary>
/// Reads a monster animation file. The header names which frames belong to standing, walking and attacking,
/// the frame table gives each frame's box, and the pixels sit in one block at the end of the file.
/// A frame whose box reads -1,-1 is not a picture: it carries the palette number instead.
/// </summary>
internal static class Mpf
{
    internal sealed record Frame(int Left, int Top, int Width, int Height, int CenterX, int CenterY, byte[] Data);

    internal sealed record Sheet(
        int PaletteNumber,
        int CanvasWidth,
        int CanvasHeight,
        int WalkStart,
        int WalkCount,
        int StandStart,
        int StandCount,
        int AttackStart,
        int AttackCount,
        List<Frame> Frames);

    public static Sheet Read(byte[] blob)
    {
        using BinaryReader reader = new(new MemoryStream(blob));

        if (reader.ReadInt32() == -1)
        {
            // Four extra header bytes, and eight more again when the first of them reads 4.
            byte[] extra = reader.ReadBytes(4);

            if (BitConverter.ToInt32(extra) == 4)
            {
                reader.ReadBytes(8);
            }
        }
        else
        {
            reader.BaseStream.Seek(-4, SeekOrigin.Current);
        }

        int frameCount = reader.ReadByte();
        int canvasWidth = reader.ReadInt16();
        int canvasHeight = reader.ReadInt16();
        int dataLength = reader.ReadInt32();
        int walkStart = reader.ReadByte();
        int walkCount = reader.ReadByte();

        int standStart, standCount, attackStart, attackCount;

        if (reader.ReadInt16() == -1)
        {
            standStart = reader.ReadByte();
            standCount = reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            attackStart = reader.ReadByte();
            attackCount = reader.ReadByte();
            reader.ReadBytes(4);
        }
        else
        {
            reader.BaseStream.Seek(-2, SeekOrigin.Current);
            attackStart = reader.ReadByte();
            attackCount = reader.ReadByte();
            standStart = reader.ReadByte();
            standCount = reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
        }

        long dataStart = blob.Length - dataLength;
        int paletteNumber = 0;
        List<(int Left, int Top, int Width, int Height, int CenterX, int CenterY, int Start)> boxes = [];

        for (int index = 0; index < frameCount; index++)
        {
            int left = reader.ReadInt16();
            int top = reader.ReadInt16();
            int right = reader.ReadInt16();
            int bottom = reader.ReadInt16();
            int centerX = reader.ReadInt16();
            int centerY = reader.ReadInt16();
            int start = reader.ReadInt32();

            if (left == -1 && top == -1)
            {
                paletteNumber = start;
                frameCount--;
                continue;
            }

            boxes.Add((left, top, right - left, bottom - top, centerX, centerY, start));
        }

        List<Frame> frames = [];

        foreach ((int left, int top, int width, int height, int centerX, int centerY, int start) in boxes)
        {
            long offset = dataStart + start;

            if (width <= 0 || height <= 0 || offset + (width * height) > blob.Length)
            {
                continue;
            }

            byte[] data = new byte[width * height];
            Array.Copy(blob, offset, data, 0, data.Length);

            frames.Add(new Frame(left, top, width, height, centerX, centerY, data));
        }

        return new Sheet(
            paletteNumber,
            canvasWidth,
            canvasHeight,
            walkStart,
            walkCount,
            standStart,
            standCount,
            attackStart,
            attackCount,
            frames);
    }
}
