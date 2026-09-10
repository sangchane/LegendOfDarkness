using Lorule.Client.Base.Dat;
using Lorule.Client.Base.Types;
using Lorule.Content.Editor.Dat;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Lod.DatExtract;

/// <summary>
/// Reads this repository's own game archives. Assets for design work come from here rather than from a
/// restored copy made elsewhere, because the archives are the original bytes.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("사용법: dat-extract list <아카이브.dat>");
            Console.Error.WriteLine("        dat-extract dump <아카이브.dat> <출력 폴더> [이름 조각]");
            Console.Error.WriteLine("        dat-extract tiles <seo.dat> <출력.png> <시작> <개수> [가로칸]");
            Console.Error.WriteLine("        dat-extract map <seo.dat> <맵파일.map> <가로칸> <세로칸> <출력.png>");
            return 2;
        }

        string command = args[0];
        string archivePath = Path.GetFullPath(args[1]);

        if (!File.Exists(archivePath))
        {
            Console.Error.WriteLine($"아카이브를 찾을 수 없습니다: {archivePath}");
            return 2;
        }

        List<ArchivedItem> entries = await ReadEntries(archivePath);
        Console.WriteLine($"{Path.GetFileName(archivePath)} — 항목 {entries.Count}개");

        return command switch
        {
            "list" => List(entries),
            "dump" => await Dump(entries, args),
            "tiles" => await Tiles(entries, args),
            "map" => await RenderMap(entries, args),
            _ => Unknown(command)
        };
    }

    /// <summary>Archive.Load expects a root folder under its location, so the path is split to match.</summary>
    private static async Task<List<ArchivedItem>> ReadEntries(string archivePath)
    {
        string directory = Path.GetDirectoryName(archivePath)!;
        string name = Path.GetFileName(archivePath);

        Archive archive = new(Path.GetDirectoryName(directory) ?? directory);
        await archive.Load(name, root: Path.GetFileName(directory));

        List<ArchivedItem> entries = [];
        entries.AddRange(archive.SearchArchive(string.Empty, string.Empty, name));

        return entries;
    }

    private static int List(List<ArchivedItem> entries)
    {
        foreach (IGrouping<string, ArchivedItem> group in entries
            .GroupBy(entry => Path.GetExtension(entry.Name).ToLowerInvariant())
            .OrderByDescending(group => group.Count()))
        {
            string extension = group.Key.Length == 0 ? "(확장자 없음)" : group.Key;
            long bytes = group.Sum(entry => (long)entry.Data.Length);
            Console.WriteLine($"  {extension,-10} {group.Count(),6}개  {bytes / 1024,8} KB");

            foreach (ArchivedItem sample in group.OrderBy(entry => entry.Name).Take(4))
            {
                Console.WriteLine($"      {sample.Name}  {sample.Data.Length} bytes");
            }
        }

        return 0;
    }

    private static async Task<int> Dump(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("dump 에는 출력 폴더가 필요합니다.");
            return 2;
        }

        string outputDirectory = Path.GetFullPath(args[2]);
        string filter = args.Length > 3 ? args[3] : string.Empty;
        Directory.CreateDirectory(outputDirectory);

        int written = 0;
        foreach (ArchivedItem entry in entries)
        {
            if (filter.Length > 0 && !entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await entry.Save(outputDirectory);
            written++;
        }

        Console.WriteLine($"{written}개를 {outputDirectory} 에 저장했습니다.");

        return 0;
    }

    /// <summary>
    /// Renders ground tiles as they actually look. Each tile is 56x27 palette indices, and which palette
    /// applies depends on the tile's index, which the .tbl tables decide.
    /// </summary>
    private static async Task<int> Tiles(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 5)
        {
            Console.Error.WriteLine("tiles 에는 출력 파일, 시작 번호, 개수가 필요합니다.");
            return 2;
        }

        const int tileWidth = 56;
        const int tileHeight = 27;

        ArchivedItem? tileSet = entries.FirstOrDefault(entry =>
            entry.Name.Equals("TILEA.BMP", StringComparison.OrdinalIgnoreCase));

        if (tileSet is null)
        {
            Console.Error.WriteLine("TILEA.BMP 를 찾지 못했습니다.");
            return 2;
        }

        List<Tile> tiles = new TileCollection(tileSet).Load();
        List<Palette> palettes = Palette.FromArchive(
            entries.Where(e => e.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase)
                            && e.Name.StartsWith("mpt", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase));

        List<PaletteTable> tables = await PaletteTable.FromArchive(
            entries.Where(e => e.Name.EndsWith(".tbl", StringComparison.OrdinalIgnoreCase)
                            && e.Name.StartsWith("mpt", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            "mpt",
            _ => { });

        Console.WriteLine($"타일 {tiles.Count}개 · 팔레트 {palettes.Count}개 · 표 {tables.Count}개");

        string output = Path.GetFullPath(args[2]);
        int start = int.Parse(args[3]);
        int count = Math.Min(int.Parse(args[4]), tiles.Count - start);
        int columns = args.Length > 5 ? int.Parse(args[5]) : 16;
        int rows = (int)Math.Ceiling(count / (double)columns);

        using Image<Rgba32> sheet = new(columns * tileWidth, rows * tileHeight);

        for (int offset = 0; offset < count; offset++)
        {
            int index = start + offset;
            Palette palette = PaletteFor(index, tables, palettes);
            byte[] data = tiles[index].Data;
            int originX = (offset % columns) * tileWidth;
            int originY = (offset / columns) * tileHeight;

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    System.Drawing.Color colour = palette[data[(y * tileWidth) + x]];
                    sheet[originX + x, originY + y] = new Rgba32(colour.R, colour.G, colour.B, 255);
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await sheet.SaveAsPngAsync(output);
        Console.WriteLine($"{count}개 타일을 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>
    /// Draws a map's floor as the client does: the grid is diamond shaped, so each cell steps half a tile
    /// across and half a tile down from its neighbours. Floor index 0 means nothing is laid there.
    /// </summary>
    private static async Task<int> RenderMap(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 6)
        {
            Console.Error.WriteLine("map 에는 맵파일, 가로칸, 세로칸, 출력 파일이 필요합니다.");
            return 2;
        }

        string mapPath = Path.GetFullPath(args[2]);
        int columns = int.Parse(args[3]);
        int rows = int.Parse(args[4]);
        string output = Path.GetFullPath(args[5]);

        if (!File.Exists(mapPath))
        {
            Console.Error.WriteLine($"맵 파일을 찾을 수 없습니다: {mapPath}");
            return 2;
        }

        TileSource source = TileSource.From(entries);
        if (source is null)
        {
            return 2;
        }

        List<MapTile> cells = Map.LoadMapTiles(mapPath).ToList();
        Console.WriteLine($"{Path.GetFileName(mapPath)} — 칸 {cells.Count}개 ({columns}x{rows} = {columns * rows})");

        const int halfWidth = TileWidth / 2;
        const int halfHeight = 13;

        int width = (columns + rows) * halfWidth;
        int height = ((columns + rows) * halfHeight) + TileHeight;
        int originX = rows * halfWidth;

        using Image<Rgba32> canvas = new(width, height);
        int drawn = 0;

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int cell = (row * columns) + column;
                if (cell >= cells.Count)
                {
                    continue;
                }

                int floor = cells[cell].Floor;
                if (floor <= 0 || floor > source.Tiles.Count)
                {
                    continue;
                }

                int x = originX + ((column - row) * halfWidth) - halfWidth;
                int y = (column + row) * halfHeight;
                source.Draw(canvas, floor - 1, x, y);
                drawn++;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await canvas.SaveAsPngAsync(output);
        Console.WriteLine($"바닥 {drawn}칸을 {width}x{height} 로 그려 {output} 에 저장했습니다.");

        return 0;
    }

    private const int TileWidth = 56;
    private const int TileHeight = 27;

    /// <summary>The ground tile set and the palettes that colour it, read once and reused.</summary>
    private sealed class TileSource
    {
        public required List<Tile> Tiles { get; init; }
        public required List<Palette> Palettes { get; init; }
        public required List<PaletteTable> Tables { get; init; }

        public static TileSource From(List<ArchivedItem> entries)
        {
            ArchivedItem tileSet = entries.FirstOrDefault(entry =>
                entry.Name.Equals("TILEA.BMP", StringComparison.OrdinalIgnoreCase));

            if (tileSet is null)
            {
                Console.Error.WriteLine("TILEA.BMP 를 찾지 못했습니다.");
                return null;
            }

            return new TileSource
            {
                Tiles = new TileCollection(tileSet).Load(),
                Palettes = Palette.FromArchive(Matching(entries, ".pal")),
                Tables = PaletteTable.FromArchive(Matching(entries, ".tbl"), "mpt", _ => { }).Result
            };
        }

        private static IEnumerable<ArchivedItem> Matching(List<ArchivedItem> entries, string extension) =>
            entries.Where(e => e.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                            && e.Name.StartsWith("mpt", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        public void Draw(Image<Rgba32> canvas, int index, int originX, int originY)
        {
            Palette palette = PaletteFor(index, Tables, Palettes);
            byte[] data = Tiles[index].Data;

            for (int y = 0; y < TileHeight; y++)
            {
                int targetY = originY + y;
                if (targetY < 0 || targetY >= canvas.Height)
                {
                    continue;
                }

                for (int x = 0; x < TileWidth; x++)
                {
                    int targetX = originX + x;
                    if (targetX < 0 || targetX >= canvas.Width)
                    {
                        continue;
                    }

                    byte code = data[(y * TileWidth) + x];
                    if (code == 0)
                    {
                        continue;
                    }

                    System.Drawing.Color colour = palette[code];
                    canvas[targetX, targetY] = new Rgba32(colour.R, colour.G, colour.B, 255);
                }
            }
        }
    }

    private static Palette PaletteFor(int index, List<PaletteTable> tables, List<Palette> palettes)
    {
        int chosen = 0;

        foreach (PaletteTable table in tables
            .Where(table => index >= table.PaletteRange.Item1 && index <= table.PaletteRange.Item2))
        {
            chosen = table.Palette;
        }

        return palettes[Math.Clamp(chosen, 0, palettes.Count - 1)];
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"알 수 없는 명령: {command}");
        return 2;
    }
}
