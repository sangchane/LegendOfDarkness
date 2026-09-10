using Lorule.Client.Base.Dat;
using Lorule.Client.Base.Types;
using Lorule.Content.Editor.Dat;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

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
            Console.Error.WriteLine("        dat-extract map <seo.dat> <맵파일.map> <가로칸> <세로칸> <출력.png> [잘라낼 x y 폭 높이]");
            Console.Error.WriteLine("        dat-extract sprite <ia.dat> <항목이름> <출력.png> [가로폭] [머리말바이트]");
            Console.Error.WriteLine("        dat-extract epf <khan.dat> <이름조각> <출력.png> [칸수] [배율] [팔레트.dat]");
            Console.Error.WriteLine("        dat-extract mpf <hades.dat> <이름들> <출력.png> [배율] [투명|transparent]");
            Console.Error.WriteLine("        dat-extract pose <khan.dat> <겹칠이름들> <출력.png> [프레임들] [배율]");
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
            "sprite" => await RenderSprite(entries, args),
            "epf" => await RenderEpf(entries, args),
            "mpf" => await RenderMpf(entries, args),
            "pose" => await RenderPose(entries, args),
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

        if (args.Length >= 10)
        {
            // 화면 크기에 맞는 조각만 남긴다 — 세로 화면 배경처럼 비율이 다른 곳에 쓰려면 필요하다.
            Rectangle window = new(int.Parse(args[6]), int.Parse(args[7]), int.Parse(args[8]), int.Parse(args[9]));
            canvas.Mutate(context => context.Crop(window));
            Console.WriteLine($"  {window.X},{window.Y} 에서 {window.Width}x{window.Height} 만 잘랐습니다.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await canvas.SaveAsPngAsync(output);
        Console.WriteLine($"바닥 {drawn}칸을 {width}x{height} 로 그려 {output} 에 저장했습니다.");

        return 0;
    }

    /// <summary>
    /// Renders one character sprite. The blob is splay-Huffman compressed; what comes out is a run of
    /// palette indices where 0 means see-through.
    /// </summary>
    private static async Task<int> RenderSprite(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("sprite 에는 항목 이름과 출력 파일이 필요합니다.");
            return 2;
        }

        string entryName = args[2];
        string output = Path.GetFullPath(args[3]);
        int width = args.Length > 4 ? int.Parse(args[4]) : 28;
        int skip = args.Length > 5 ? int.Parse(args[5]) : 8;

        ArchivedItem sprite = entries.FirstOrDefault(entry =>
            entry.Name.Equals(entryName, StringComparison.OrdinalIgnoreCase));

        if (sprite is null)
        {
            Console.Error.WriteLine($"항목을 찾지 못했습니다: {entryName}");
            return 2;
        }

        byte[] pixels = Hpf.LooksCompressed(sprite.Data) ? Hpf.Decompress(sprite.Data) : sprite.Data;
        Console.WriteLine($"{sprite.Name}: {sprite.Data.Length} → {pixels.Length} bytes");
        Console.WriteLine($"  앞부분 {Convert.ToHexString(pixels.AsSpan(0, Math.Min(24, pixels.Length)))}");

        List<Palette> palettes = Palette.FromArchive(
            entries.Where(e => e.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase));

        if (palettes.Count == 0)
        {
            Console.Error.WriteLine("팔레트를 찾지 못했습니다.");
            return 2;
        }

        Palette palette = palettes[0];

        if (skip > 0 && skip < pixels.Length)
        {
            pixels = pixels[skip..];
        }

        int height = pixels.Length / width;

        if (height == 0)
        {
            Console.Error.WriteLine($"폭 {width} 로는 한 줄도 만들 수 없습니다.");
            return 2;
        }

        using Image<Rgba32> image = new(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte code = pixels[(y * width) + x];
                System.Drawing.Color colour = palette[code];
                image[x, y] = code == 0
                    ? new Rgba32(0, 0, 0, 0)
                    : new Rgba32(colour.R, colour.G, colour.B, 255);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await image.SaveAsPngAsync(output);
        Console.WriteLine($"{width}x{height} 로 {output} 에 저장했습니다 (남는 바이트 {pixels.Length - (width * height)}).");

        return 0;
    }

    /// <summary>
    /// Draws the wardrobe pieces a player is built from. One .epf holds every frame of one piece, and the
    /// piece's number decides its colours, so each frame is drawn with the palette its own table names.
    /// </summary>
    private static async Task<int> RenderEpf(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("epf 에는 이름 조각과 출력 파일이 필요합니다.");
            return 2;
        }

        string filter = args[2];
        string output = Path.GetFullPath(args[3]);
        int columns = args.Length > 4 ? int.Parse(args[4]) : 12;
        int zoom = args.Length > 5 ? int.Parse(args[5]) : 3;

        // khan2.dat carries the women's pieces but no palettes of its own; they live in khan.dat.
        List<ArchivedItem> palettes = args.Length > 6 ? await ReadEntries(Path.GetFullPath(args[6])) : entries;
        bool female = Path.GetFileName(args[1]).StartsWith("khan2", StringComparison.OrdinalIgnoreCase);

        string[] wanted = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<ArchivedItem> chosen = entries
            .Where(entry => entry.Name.EndsWith(".epf", StringComparison.OrdinalIgnoreCase)
                         && wanted.Any(part => entry.Name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (chosen.Count == 0)
        {
            Console.Error.WriteLine($"{filter} 에 맞는 .epf 가 없습니다.");
            return 2;
        }

        List<(byte[] Data, int Width, int Height, Palette Palette)> cells = [];

        foreach (ArchivedItem item in chosen)
        {
            Palette palette = Sprites.ForWardrobe(palettes, item.Name, female)
                              ?? Sprites.Named(palettes, "palb000.pal")!;

            Epf.Sheet sheet = Epf.Read(item.Data);
            List<Epf.Frame> frames = sheet.Frames;
            Console.WriteLine($"  {item.Name}: 프레임 {frames.Count}개");

            foreach (Epf.Frame frame in frames)
            {
                cells.Add((frame.Data, frame.Width, frame.Height, palette));
            }
        }

        if (cells.Count == 0)
        {
            Console.Error.WriteLine("그릴 프레임이 없습니다.");
            return 2;
        }

        await Sprites.Save(output, cells, columns, zoom);
        Console.WriteLine($"{chosen.Count}개 파일 · 프레임 {cells.Count}개를 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>
    /// Stacks wardrobe pieces into one figure. The original draws a character as separate layers — body,
    /// then what it wears, then what it holds — each carrying its own offset inside a shared box, so the
    /// pieces line up when drawn in order at the same frame number.
    /// </summary>
    private static async Task<int> RenderPose(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine(
                "pose <아카이브> <겹칠 이름들> <출력> [자세들] [확대] [칸 크기 예: 116x92]");
            return 2;
        }

        string[] layers = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string output = Path.GetFullPath(args[3]);
        int[] poses = (args.Length > 4 ? args[4] : "0")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
        int zoom = args.Length > 5 ? int.Parse(args[5]) : 4;

        bool female = Path.GetFileName(args[1]).StartsWith("khan2", StringComparison.OrdinalIgnoreCase);

        // The women's archive carries no palettes of its own and shares the men's, so colours are looked
        // up next door.
        List<ArchivedItem> palettes =
            entries.Any(entry => entry.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase))
                ? entries
                : await ReadEntries(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "khan.dat"));

        List<(Epf.Frame Frame, Palette Palette)>[] stacks = new List<(Epf.Frame, Palette)>[poses.Length];

        for (int slot = 0; slot < poses.Length; slot++)
        {
            stacks[slot] = [];
        }

        foreach (string layer in layers)
        {
            ArchivedItem? item = entries.FirstOrDefault(entry =>
                Path.GetFileNameWithoutExtension(entry.Name).Equals(layer, StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                Console.Error.WriteLine($"  {layer}: 없습니다");
                continue;
            }

            Palette? palette = Sprites.ForWardrobe(palettes, item.Name, female)
                               ?? Sprites.Named(palettes, "palb000.pal");

            if (palette is null)
            {
                Console.Error.WriteLine($"  {layer}: 색표를 찾지 못했습니다");
                return 2;
            }
            Epf.Sheet sheet = Epf.Read(item.Data);
            List<Epf.Frame> frames = sheet.Frames;
            Console.WriteLine(
                $"  {item.Name}: 프레임 {frames.Count}개, 바탕 {sheet.Width}x{sheet.Height}, " +
                $"첫 칸 {frames.FirstOrDefault()?.Left},{frames.FirstOrDefault()?.Top}");

            for (int slot = 0; slot < poses.Length; slot++)
            {
                if (poses[slot] < frames.Count)
                {
                    stacks[slot].Add((frames[poses[slot]], palette));
                }
            }
        }

        if (stacks.All(stack => stack.Count == 0))
        {
            Console.Error.WriteLine("겹칠 것이 없습니다.");
            return 2;
        }

        int drawnWidth = stacks.SelectMany(stack => stack).Max(entry => entry.Frame.Left + entry.Frame.Width) + 4;
        int drawnHeight = stacks.SelectMany(stack => stack).Max(entry => entry.Frame.Top + entry.Frame.Height) + 4;
        int cellWidth = drawnWidth;
        int cellHeight = drawnHeight;

        // Parts drawn on their own only line up with each other when every sheet uses the same cell: the
        // frames already share one origin, and a cell fitted to each part's own contents would move it.
        // Weapons reach far outside the body, so the caller says how big rather than each sheet deciding.
        if (args.Length > 6)
        {
            string[] size = args[6].Split('x', 'X');

            if (size.Length != 2 || !int.TryParse(size[0], out cellWidth) || !int.TryParse(size[1], out cellHeight))
            {
                Console.Error.WriteLine($"칸 크기는 '너비x높이' 여야 합니다: {args[6]}");
                return 2;
            }

            if (cellWidth < drawnWidth || cellHeight < drawnHeight)
            {
                Console.Error.WriteLine(
                    $"칸 {cellWidth}x{cellHeight} 이 그림 {drawnWidth}x{drawnHeight} 보다 작아 잘립니다.");
                return 2;
            }
        }

        // Transparent, because these figures get laid over a map rather than viewed on their own.
        using Image<Rgba32> canvas = new(cellWidth * poses.Length, cellHeight);

        for (int slot = 0; slot < poses.Length; slot++)
        {
            foreach ((Epf.Frame frame, Palette palette) in stacks[slot])
            {
                Sprites.Blit(
                    canvas,
                    frame.Data,
                    frame.Width,
                    frame.Height,
                    palette,
                    (slot * cellWidth) + 2 + frame.Left,
                    2 + frame.Top);
            }
        }

        if (zoom > 1)
        {
            canvas.Mutate(context => context.Resize(
                canvas.Width * zoom,
                canvas.Height * zoom,
                KnownResamplers.NearestNeighbor));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await canvas.SaveAsPngAsync(output);
        Console.WriteLine($"{layers.Length}겹 · 자세 {poses.Length}개를 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>Draws a monster's animation frames in the order the file gives them.</summary>
    private static async Task<int> RenderMpf(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("mpf 에는 항목 이름과 출력 파일이 필요합니다.");
            return 2;
        }

        string entryName = args[2];
        string output = Path.GetFullPath(args[3]);
        int zoom = args.Length > 4 ? int.Parse(args[4]) : 3;
        // Both spellings: a Korean argument does not always survive the hand-off from a shell.
        bool transparent = args.Length > 5 && args[5] is "투명" or "transparent";

        string[] names = entryName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<(byte[] Data, int Width, int Height, Palette Palette)> cells = [];

        foreach (string name in names)
        {
            ArchivedItem? item = entries.FirstOrDefault(entry =>
                entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                Console.Error.WriteLine($"항목을 찾지 못했습니다: {name}");
                continue;
            }

            Mpf.Sheet sheet = Mpf.Read(item.Data);
            Palette palette = Sprites.Named(entries, $"mns{sheet.PaletteNumber:000}.pal")
                              ?? Sprites.Named(entries, "mns000.pal")!;

            Console.WriteLine($"{item.Name}: 프레임 {sheet.Frames.Count}개 · 팔레트 {sheet.PaletteNumber} · 화폭 {sheet.CanvasWidth}x{sheet.CanvasHeight}");
            Console.WriteLine($"  서기 {sheet.StandStart}+{sheet.StandCount} · 걷기 {sheet.WalkStart}+{sheet.WalkCount} · 공격 {sheet.AttackStart}+{sheet.AttackCount}");

            // One name shows the whole animation; several show each creature standing, side by side.
            IEnumerable<Mpf.Frame> wanted = names.Length == 1
                ? sheet.Frames
                : sheet.Frames.Skip(sheet.StandStart).Take(1);

            cells.AddRange(wanted.Select(frame => (frame.Data, frame.Width, frame.Height, palette)));
        }

        if (cells.Count == 0)
        {
            Console.Error.WriteLine("그릴 프레임이 없습니다.");
            return 2;
        }

        await Sprites.Save(output, cells, Math.Min(cells.Count, 8), zoom, transparent);
        Console.WriteLine($"프레임 {cells.Count}개를 {output} 에 그렸습니다.");

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
