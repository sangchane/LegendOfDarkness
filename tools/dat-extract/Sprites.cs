using Lorule.Client.Base.Dat;
using Lorule.Content.Editor.Dat;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Lod.DatExtract;

/// <summary>
/// Turns palette indices into pictures. Which palette applies is decided by the item's number: the archive
/// carries one text table per wardrobe slot (palh.tbl for heads, palc.tbl for coats, and so on) mapping a
/// number to a palette file. Index 0 is see-through everywhere.
/// </summary>
internal static class Sprites
{
    /// <summary>Reads one named palette file, 256 colours of three bytes each.</summary>
    public static Palette? Named(List<ArchivedItem> entries, string name)
    {
        ArchivedItem? item = entries.FirstOrDefault(entry =>
            entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return item is null ? null : Palette.FromArchive([item]).FirstOrDefault();
    }

    /// <summary>
    /// Picks the palette for a wardrobe entry such as <c>mh28501.epf</c>: the letter names the slot, the
    /// digits name the item. Rows ending in -1 apply to men only and -2 to women only.
    /// </summary>
    public static Palette? ForWardrobe(List<ArchivedItem> palettes, string entryName, bool female)
    {
        string bare = Path.GetFileNameWithoutExtension(entryName);

        if (bare.Length < 5)
        {
            return null;
        }

        char slot = char.ToLowerInvariant(bare[1]);
        if (!int.TryParse(bare.AsSpan(2, 3), out int id))
        {
            return null;
        }

        int chosen = LookUp(palettes, $"pal{slot}.tbl", id, female);

        return Named(palettes, $"pal{slot}{chosen:000}.pal");
    }

    private static int LookUp(List<ArchivedItem> palettes, string tableName, int id, bool female)
    {
        ArchivedItem? table = palettes.FirstOrDefault(entry =>
            entry.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase));

        if (table is null)
        {
            return 0;
        }

        int chosen = 0;
        int wanted = female ? -2 : -1;

        using StringReader reader = new(System.Text.Encoding.ASCII.GetString(table.Data));

        while (reader.ReadLine() is { } line)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length < 2 || !int.TryParse(parts[0], out int first) || !int.TryParse(parts[1], out int second))
            {
                continue;
            }

            if (parts.Length == 2)
            {
                // "id palette" — a single item, which outranks any range it falls inside.
                if (first == id)
                {
                    chosen = second;
                }

                continue;
            }

            if (!int.TryParse(parts[2], out int third))
            {
                continue;
            }

            if (third is -1 or -2)
            {
                if (first == id && third == wanted)
                {
                    chosen = second;
                }
            }
            else if (id >= first && id <= second)
            {
                chosen = third;
            }
        }

        return chosen;
    }

    /// <summary>Draws one frame of palette indices onto the canvas, leaving index 0 untouched.</summary>
    public static void Blit(
        Image<Rgba32> canvas,
        byte[] data,
        int width,
        int height,
        Palette palette,
        int originX,
        int originY,
        IReadOnlyList<System.Drawing.Color>? dye = null)
    {
        for (int y = 0; y < height; y++)
        {
            int targetY = originY + y;

            if (targetY < 0 || targetY >= canvas.Height)
            {
                continue;
            }

            for (int x = 0; x < width; x++)
            {
                int targetX = originX + x;
                byte code = data[(y * width) + x];

                if (code == 0 || targetX < 0 || targetX >= canvas.Width)
                {
                    continue;
                }

                // A dye does not repaint the picture, it replaces a run of the palette it was drawn with.
                int dyed = code - ColourTable.FirstDyedIndex;

                System.Drawing.Color colour = dye is not null && dyed >= 0 && dyed < dye.Count
                    ? dye[dyed]
                    : palette[code];
                canvas[targetX, targetY] = new Rgba32(colour.R, colour.G, colour.B, 255);
            }
        }
    }

    /// <summary>
    /// Lays frames out in a grid on a dark ground and scales the result so it is legible on screen. The
    /// gap between cells is there so a person can tell them apart; a sheet the client slices by
    /// <c>frame * cellWidth</c> asks for none, and then every cell is exactly the frame.
    /// </summary>
    public static async Task Save(
        string output,
        List<(byte[] Data, int Width, int Height, Palette Palette)> cells,
        int columns,
        int zoom,
        bool transparent = false,
        int padding = 4,
        bool square = false)
    {
        // A square cell lets the reader work the frame size out from the sheet alone: one row of cells as
        // tall as they are wide means width / height is the frame count. Without it a sheet has to carry
        // its cell size some other way, and a creature whose drawing is wider than it is tall slices wrong.
        int side = Math.Max(cells.Max(cell => cell.Width), cells.Max(cell => cell.Height));

        int cellWidth = (square ? side : cells.Max(cell => cell.Width)) + padding;
        int cellHeight = (square ? side : cells.Max(cell => cell.Height)) + padding;
        int rows = (int)Math.Ceiling(cells.Count / (double)columns);

        using Image<Rgba32> sheet = new(columns * cellWidth, rows * cellHeight);
        if (!transparent)
        {
            sheet.Mutate(context => context.BackgroundColor(Color.FromRgb(0x14, 0x17, 0x20)));
        }

        for (int index = 0; index < cells.Count; index++)
        {
            (byte[] data, int width, int height, Palette palette) = cells[index];
            int originX = ((index % columns) * cellWidth) + ((cellWidth - width) / 2);
            int originY = ((index / columns) * cellHeight) + cellHeight - (padding / 2) - height;

            Blit(sheet, data, width, height, palette, originX, originY);
        }

        if (zoom > 1)
        {
            sheet.Mutate(context => context.Resize(
                sheet.Width * zoom,
                sheet.Height * zoom,
                KnownResamplers.NearestNeighbor));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await sheet.SaveAsPngAsync(output);
    }
}
