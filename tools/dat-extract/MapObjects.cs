using System.Text;
using Lorule.Client.Base.Dat;
using Lorule.Content.Editor.Dat;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Lod.DatExtract;

/// <summary>
/// What stands on a map besides its floor — buildings, trees, fences. The .map file gives every cell two of
/// these numbers (the left and the right half of the cell) and each number is a picture <c>stc#####.hpf</c>
/// in <c>ia.dat</c>, coloured by the palette <c>stcpal.tbl</c> picks for it.
/// </summary>
/// <remarks>
/// Placement follows da-lib (<c>DALib/Drawing/Graphics.cs</c> RenderMap) and the Capricorn map editor
/// (<c>FallenDev/DAMapEditor/DAGraphics.cs</c>), which agree: the left picture's left edge is the cell's left
/// corner, the right picture starts half a tile further, and both hang upward from the cell's bottom corner.
/// </remarks>
internal static class MapObjects
{
    /// <summary>Every wall picture is this wide; its height is whatever is left of the pixels.</summary>
    public const int PictureWidth = 28;

    /// <summary>An hpf carries eight bytes before its pixels.</summary>
    private const int HeaderBytes = 8;

    /// <summary>sotp.dat's flag for a picture drawn by adding its light to what is under it.</summary>
    private const byte TransparentFlag = 0x80;

    /// <summary>One picture cut out and coloured, with its see-through rows at the top already dropped.</summary>
    internal sealed record Picture(int Number, int Height, Rgba32[] Pixels, bool Glows);

    /// <summary>Where one picture went on the sheet.</summary>
    internal readonly record struct Placed(int X, int Y, int Height);

    /// <summary>
    /// Whether a wall number is drawn at all. The first twelve of each ten-thousand block are markers with no
    /// picture — da-lib <c>IntExtensions.IsRenderedTileIndex</c>.
    /// </summary>
    public static bool IsDrawn(int number) => number > 10012 || number % 10000 > 12;

    /// <summary>
    /// Which palette a wall picture takes. Asked with the wall number plus one, as da-lib and the map editor both
    /// do. A line of three numbers gives a range; a line of two names a single picture and beats any range,
    /// whatever order they come in (da-lib <c>PaletteTable</c>).
    /// </summary>
    internal sealed class PaletteChoice
    {
        private readonly Dictionary<int, int> _ranges = [];
        private readonly Dictionary<int, int> _singles = [];

        public static PaletteChoice Read(string table)
        {
            PaletteChoice choice = new();

            foreach (string line in table.Split('\n'))
            {
                string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                int[] numbers = parts.Select(part => int.TryParse(part, out int value) ? value : int.MinValue).ToArray();

                if (numbers.Contains(int.MinValue))
                {
                    continue;
                }

                if (numbers.Length == 2)
                {
                    choice._singles[numbers[0]] = numbers[1];
                }
                else if (numbers.Length == 3)
                {
                    for (int id = numbers[0]; id <= numbers[1]; id++)
                    {
                        choice._ranges[id] = numbers[2];
                    }
                }
            }

            return choice;
        }

        public int For(int id) =>
            _singles.TryGetValue(id, out int single) ? single
            : _ranges.TryGetValue(id, out int ranged) ? ranged
            : 0;
    }

    public static Picture? Cut(List<ArchivedItem> entries, int number, PaletteChoice choice, List<Palette> palettes, byte[] sotp)
    {
        string name = $"stc{number:D5}.hpf";
        ArchivedItem? entry = entries.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return null;
        }

        byte[] blob = Hpf.LooksCompressed(entry.Data) ? Hpf.Decompress(entry.Data) : entry.Data;
        int height = (blob.Length - HeaderBytes) / PictureWidth;

        if (height <= 0)
        {
            return null;
        }

        Palette palette = palettes[Math.Clamp(choice.For(number + 1), 0, palettes.Count - 1)];

        // 위쪽 빈 줄은 버린다 — 그림은 아래 모서리에 매달리므로 위를 잘라도 놓이는 자리는 같다.
        int top = 0;
        while (top < height && Enumerable.Range(0, PictureWidth).All(x => blob[HeaderBytes + (top * PictureWidth) + x] == 0))
        {
            top++;
        }

        int kept = height - top;
        Rgba32[] pixels = new Rgba32[kept * PictureWidth];

        for (int y = 0; y < kept; y++)
        {
            for (int x = 0; x < PictureWidth; x++)
            {
                byte code = blob[HeaderBytes + ((top + y) * PictureWidth) + x];

                if (code != 0)
                {
                    System.Drawing.Color colour = palette[code];
                    pixels[(y * PictureWidth) + x] = new Rgba32(colour.R, colour.G, colour.B, 255);
                }
            }
        }

        bool glows = number - 1 < sotp.Length && (sotp[number - 1] & TransparentFlag) != 0;

        return new Picture(number, kept, pixels, glows);
    }

    /// <summary>
    /// Stacks the pictures into columns one picture wide. They are all the same width, so this wastes only the
    /// bottom of each column.
    /// </summary>
    public static (Image<Rgba32> Sheet, Dictionary<int, Placed> Where) Pack(IReadOnlyList<Picture> pictures, int columnHeight)
    {
        Dictionary<int, Placed> where = [];
        int tallest = pictures.Count == 0 ? 1 : pictures.Max(picture => picture.Height);
        int limit = Math.Max(columnHeight, tallest);
        int column = 0;
        int filled = 0;

        foreach (Picture picture in pictures.OrderByDescending(picture => picture.Height).ThenBy(picture => picture.Number))
        {
            if (filled + picture.Height > limit)
            {
                column++;
                filled = 0;
            }

            where[picture.Number] = new Placed(column * PictureWidth, filled, picture.Height);
            filled += picture.Height;
        }

        int height = where.Count == 0 ? 1 : where.Values.Max(placed => placed.Y + placed.Height);
        Image<Rgba32> sheet = new((column + 1) * PictureWidth, height);

        foreach (Picture picture in pictures)
        {
            Placed placed = where[picture.Number];
            Paint(sheet, picture, placed.X, placed.Y);
        }

        return (sheet, where);
    }

    /// <summary>Copies a picture onto a canvas. With <paramref name="blend" />, one that glows adds its light instead of covering.</summary>
    public static void Paint(Image<Rgba32> canvas, Picture picture, int left, int top, bool blend = false)
    {
        for (int y = 0; y < picture.Height; y++)
        {
            int targetY = top + y;
            if (targetY < 0 || targetY >= canvas.Height)
            {
                continue;
            }

            for (int x = 0; x < PictureWidth; x++)
            {
                int targetX = left + x;
                Rgba32 pixel = picture.Pixels[(y * PictureWidth) + x];

                if (pixel.A == 0 || targetX < 0 || targetX >= canvas.Width)
                {
                    continue;
                }

                if (blend && picture.Glows)
                {
                    Rgba32 under = canvas[targetX, targetY];
                    canvas[targetX, targetY] = new Rgba32(
                        (byte)Math.Min(255, under.R + pixel.R),
                        (byte)Math.Min(255, under.G + pixel.G),
                        (byte)Math.Min(255, under.B + pixel.B),
                        255);
                }
                else
                {
                    canvas[targetX, targetY] = pixel;
                }
            }
        }
    }

    /// <summary>
    /// The layout file the mobile client reads beside the two sheets (<c>Lod.Mobile.Core.Art.MapLayout</c>): the map's
    /// size, which cells block, which floor tile each cell takes, where each tile and picture sits on its sheet, and
    /// which picture stands on which half of which cell.
    /// </summary>
    public static string Describe(
        string title,
        int columns,
        int rows,
        IReadOnlyList<Walls.Cell> cells,
        byte[] sotp,
        IReadOnlyDictionary<int, (int X, int Y)> tiles,
        IReadOnlyDictionary<int, Picture> pictures,
        IReadOnlyDictionary<int, Placed> where)
    {
        StringBuilder text = new();
        text.AppendLine($"# {title} — tools/dat-extract layout 이 만든다. 손으로 고치지 말 것.");
        text.AppendLine($"size {columns} {rows}");

        text.AppendLine("blocked");
        for (int row = 0; row < rows; row++)
        {
            StringBuilder line = new(columns);

            for (int column = 0; column < columns; column++)
            {
                int cell = (row * columns) + column;
                bool blocks = cell < cells.Count && Walls.Blocks(sotp, cells[cell].Left, cells[cell].Right);
                line.Append(blocks ? '#' : '.');
            }

            text.AppendLine(line.ToString());
        }

        text.AppendLine("floor");
        for (int row = 0; row < rows; row++)
        {
            IEnumerable<int> line = Enumerable.Range(0, columns)
                .Select(column => (row * columns) + column)
                .Select(cell => cell < cells.Count && tiles.ContainsKey(cells[cell].Floor) ? cells[cell].Floor : 0);
            text.AppendLine(string.Join(' ', line));
        }

        foreach ((int number, (int x, int y)) in tiles.OrderBy(pair => pair.Key))
        {
            text.AppendLine($"tile {number} {x} {y}");
        }

        foreach ((int number, Placed placed) in where.OrderBy(pair => pair.Key))
        {
            text.AppendLine($"picture {number} {placed.X} {placed.Y} {placed.Height} {(pictures[number].Glows ? 1 : 0)}");
        }

        for (int cell = 0; cell < Math.Min(cells.Count, columns * rows); cell++)
        {
            (int column, int row) = (cell % columns, cell / columns);

            if (where.ContainsKey(cells[cell].Left))
            {
                text.AppendLine($"object {column} {row} left {cells[cell].Left}");
            }

            if (where.ContainsKey(cells[cell].Right))
            {
                text.AppendLine($"object {column} {row} right {cells[cell].Right}");
            }
        }

        return text.ToString();
    }
}
