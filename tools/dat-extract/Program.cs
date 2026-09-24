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
    // What a wardrobe piece is drawn on unless its own file says wider. Everything but weapons and
    // accessories uses this.
    /// <summary>Item icons live in files of this many frames; the number the server sends adds 0x8000.</summary>
    private const int FramesPerItemFile = 266;
    private const int ItemImageFlag = 0x8000;

    private const int WardrobeWidth = 57;

    /// <summary>The canvas weapons and accessories are drawn on, wider than the body's so they can reach out.</summary>
    private const int ReachingWidth = 111;

    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("사용법: dat-extract list <아카이브.dat>");
            Console.Error.WriteLine("        dat-extract dump <아카이브.dat> <출력 폴더> [이름 조각]");
            Console.Error.WriteLine("        dat-extract tiles <seo.dat> <출력.png> <시작> <개수> [가로칸] [눈|snow]");
            Console.Error.WriteLine("        dat-extract map <seo.dat> <맵파일.map> <가로칸> <세로칸> <출력.png> [잘라낼 x y 폭 높이]");
            Console.Error.WriteLine("        dat-extract sprite <ia.dat> <항목이름> <출력.png> [가로폭] [머리말바이트]");
            Console.Error.WriteLine("        dat-extract list <아카이브.dat> [이름조각]");
            Console.Error.WriteLine("        dat-extract epf <khan.dat> <이름조각> <출력.png> [칸수] [배율] [팔레트.dat]");
            Console.Error.WriteLine("        dat-extract efct <roh.dat> <efct042> <출력.png> [배율] [색표.pal]");
            Console.Error.WriteLine("        dat-extract mpf <hades.dat> <이름들> <출력.png> [배율] [투명|transparent]");
            Console.Error.WriteLine("        dat-extract pose <khan.dat> <겹칠이름들> <출력.png> [프레임들] [배율] [칸] [색번호|marker] [색표]");
            Console.Error.WriteLine("        dat-extract icon <Legend.dat> <번호들> <출력.png> [배율]");
            Console.Error.WriteLine("        dat-extract walls <맵파일.map> <sotp.dat> [가로칸]");
            Console.Error.WriteLine("        dat-extract layout <seo.dat> <ia.dat> <sotp.dat> <맵파일.map> <가로칸> <세로칸> <출력이름> [미리보기.png]");
            Console.Error.WriteLine("        dat-extract dyeslots <출력.txt>");
            Console.Error.WriteLine("        dat-extract metafile <database/server/metafile/ItemInfo8> [찾을 말]");
            return 2;
        }

        string command = args[0];

        // The two commands that read no archive.
        if (command == "dyeslots")
        {
            return await WriteDyeSlots(args);
        }

        if (command == "metafile")
        {
            return ShowMetaFile(args);
        }

        if (command == "walls")
        {
            return ShowWalls(args);
        }

        if (command == "layout")
        {
            return await RenderLayout(args);
        }

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
            "list" => List(entries, args),
            "dump" => await Dump(entries, args),
            "tiles" => await Tiles(entries, args),
            "map" => await RenderMap(entries, args),
            "sprite" => await RenderSprite(entries, args),
            "epf" => await RenderEpf(entries, args),
            "spf" => await RenderSpf(entries, args),
            "mpf" => await RenderMpf(entries, args),
            "pose" => await RenderPose(entries, args),
            "icon" => await RenderIcon(entries, args),
            "efct" => await RenderEfct(entries, args),
            "efa" => await RenderEfa(entries, args),
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

    private static int List(List<ArchivedItem> entries, string[] args)
    {
        // With a name fragment we want every match by name, not four samples per extension.
        string filter = args.Length > 2 ? args[2] : string.Empty;

        if (filter.Length > 0)
        {
            foreach (ArchivedItem entry in entries
                .Where(entry => entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Name))
            {
                Console.WriteLine($"  {entry.Name}  {entry.Data.Length} bytes");
            }

            return 0;
        }

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

        TileSource? source = TileSource.From(entries, WantsSnow(args));

        if (source is null)
        {
            return 2;
        }

        Console.WriteLine(
            $"타일 {source.Tiles.Count}개 · 팔레트 {source.Palettes.Count}개");

        string output = Path.GetFullPath(args[2]);
        int start = int.Parse(args[3]);
        int count = Math.Min(int.Parse(args[4]), source.Tiles.Count - start);
        int columns = args.Length > 5 && int.TryParse(args[5], out int given) ? given : 16;
        int rows = (int)Math.Ceiling(count / (double)columns);

        using Image<Rgba32> sheet = new(columns * TileWidth, rows * TileHeight);

        for (int offset = 0; offset < count; offset++)
        {
            source.Draw(sheet, start + offset, (offset % columns) * TileWidth, (offset / columns) * TileHeight);
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

        // 눈은 여기 붙이지 않는다. 맵의 바닥 번호는 TILEA 의 19,243칸 공간을 가리키는데 TILEAS 는
        // 2,805칸뿐이라 그대로 대면 전부 범위 밖이 되어 한 칸도 그리지 못한다. 원작이 눈 맵을
        // 어떻게 그리는지는 아직 모른다 — NEXT.md 참고.
        TileSource? source = TileSource.From(entries);
        if (source is null)
        {
            return 2;
        }

        List<MapTile> cells = Map.LoadMapTiles(mapPath).ToList();
        Console.WriteLine($"{Path.GetFileName(mapPath)} — 칸 {cells.Count}개 ({columns}x{rows} = {columns * rows})");

        const int halfWidth = TileWidth / 2;

        int width = (columns + rows) * halfWidth;
        int height = ((columns + rows) * HalfHeight) + TileHeight;
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
                int y = (column + row) * HalfHeight;
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
    /// Writes what the mobile client needs to put a map together itself: a sheet of the floor tiles the map uses, a
    /// sheet of what stands on it (buildings, trees, fences) and a layout saying which tile and which picture go on
    /// which cell and which cells block (<see cref="MapObjects" />). The standing pictures are kept apart from the
    /// floor because a figure has to be able to walk behind them, and the floor is kept as tiles because a baked
    /// 70x70 floor is 5.5MB where its tiles are a few hundred kilobytes.
    /// </summary>
    /// <remarks>
    /// With a preview path it also draws the whole map the way da-lib does (floor, then row by row the left half's
    /// picture before the right's), so the result can be looked at.
    /// </remarks>
    private static async Task<int> RenderLayout(string[] args)
    {
        if (args.Length < 8)
        {
            Console.Error.WriteLine("layout 에는 seo.dat, ia.dat, sotp.dat, 맵파일, 가로칸, 세로칸, 출력이름이 필요합니다.");
            return 2;
        }

        string[] paths = [.. args[1..5].Select(Path.GetFullPath)];
        int columns = int.Parse(args[5]);
        int rows = int.Parse(args[6]);
        string output = Path.GetFullPath(args[7]);

        foreach (string needed in paths)
        {
            if (!File.Exists(needed))
            {
                Console.Error.WriteLine($"파일을 찾을 수 없습니다: {needed}");
                return 2;
            }
        }

        (string seoPath, string iaPath, string sotpPath, string mapPath) = (paths[0], paths[1], paths[2], paths[3]);

        List<ArchivedItem> seo = await ReadEntries(seoPath);
        List<ArchivedItem> ia = await ReadEntries(iaPath);
        TileSource? tiles = TileSource.From(seo);
        ArchivedItem? table = ia.FirstOrDefault(entry => entry.Name.Equals("stcpal.tbl", StringComparison.OrdinalIgnoreCase));

        if (tiles is null || table is null)
        {
            Console.Error.WriteLine(tiles is null ? "바닥 타일을 읽지 못했습니다." : "stcpal.tbl 이 없습니다 — ia.dat 이 맞나요?");
            return 2;
        }

        byte[] sotp = File.ReadAllBytes(sotpPath);
        List<Walls.Cell> cells = Walls.Read(File.ReadAllBytes(mapPath));
        MapObjects.PaletteChoice choice = MapObjects.PaletteChoice.Read(System.Text.Encoding.ASCII.GetString(table.Data));

        // stc0000.pal … 의 순서가 곧 팔레트 번호다.
        List<Palette> palettes = Palette.FromArchive(
            ia.Where(entry => entry.Name.StartsWith("stc", StringComparison.OrdinalIgnoreCase)
                              && entry.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase))
              .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase));

        Dictionary<int, MapObjects.Picture> pictures = [];
        List<int> missing = [];

        foreach (int number in cells.SelectMany(cell => new[] { cell.Left, cell.Right })
                     .Where(MapObjects.IsDrawn)
                     .Distinct()
                     .Order())
        {
            if (MapObjects.Cut(ia, number, choice, palettes, sotp) is { } picture)
            {
                pictures[number] = picture;
            }
            else
            {
                missing.Add(number);
            }
        }

        // 바닥 번호는 1부터 센다(0 은 아무것도 깔지 않음). 타일셋에 없는 번호는 그리지 않는다 — map 명령과 같다.
        int[] floors = [.. cells.Select(cell => cell.Floor).Where(floor => floor > 0 && floor <= tiles.Tiles.Count).Distinct().Order()];
        int across = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(floors.Length)));
        Dictionary<int, (int X, int Y)> tileAt = [];

        using (Image<Rgba32> floorSheet = new(across * TileWidth, Math.Max(1, (int)Math.Ceiling(floors.Length / (double)across)) * TileHeight))
        {
            for (int at = 0; at < floors.Length; at++)
            {
                (int x, int y) = ((at % across) * TileWidth, (at / across) * TileHeight);
                tiles.Draw(floorSheet, floors[at] - 1, x, y);
                tileAt[floors[at]] = (x, y);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await floorSheet.SaveAsPngAsync($"{output}-floor.png");
        }

        (Image<Rgba32> sheet, Dictionary<int, MapObjects.Placed> where) = MapObjects.Pack([.. pictures.Values], 1024);

        using (sheet)
        {
            await sheet.SaveAsPngAsync($"{output}-objects.png");
        }

        await File.WriteAllTextAsync($"{output}.txt",
            MapObjects.Describe(Path.GetFileNameWithoutExtension(mapPath), columns, rows, cells, sotp, tileAt, pictures, where));
        Console.WriteLine($"{Path.GetFileName(mapPath)} — 바닥 타일 {floors.Length}종 · 세운 그림 {pictures.Count}장 → {output}.txt · -floor.png · -objects.png");

        if (missing.Count > 0)
        {
            Console.WriteLine($"  아카이브에 없는 그림 번호 {missing.Count}개: {string.Join(", ", missing.Take(12))}");
        }

        if (args.Length < 9)
        {
            return 0;
        }

        const int halfWidth = TileWidth / 2;
        using Image<Rgba32> canvas = new((columns + rows) * halfWidth, ((columns + rows) * HalfHeight) + TileHeight + 400);
        const int lift = 400;

        foreach (bool standing in new[] { false, true })
        {
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int cell = (row * columns) + column;
                    if (cell >= cells.Count)
                    {
                        continue;
                    }

                    int x = (rows * halfWidth) + ((column - row) * halfWidth) - halfWidth;
                    int y = lift + ((column + row) * HalfHeight);

                    if (!standing)
                    {
                        if (tileAt.ContainsKey(cells[cell].Floor))
                        {
                            tiles.Draw(canvas, cells[cell].Floor - 1, x, y);
                        }

                        continue;
                    }

                    if (pictures.TryGetValue(cells[cell].Left, out MapObjects.Picture? left))
                    {
                        MapObjects.Paint(canvas, left, x, y + (2 * HalfHeight) - left.Height, blend: true);
                    }

                    if (pictures.TryGetValue(cells[cell].Right, out MapObjects.Picture? right))
                    {
                        MapObjects.Paint(canvas, right, x + halfWidth, y + (2 * HalfHeight) - right.Height, blend: true);
                    }
                }
            }
        }

        await canvas.SaveAsPngAsync(Path.GetFullPath(args[8]));
        Console.WriteLine($"  세워 본 모습: {Path.GetFullPath(args[8])}");

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

        // 연출을 남의 그림 위에 얹으려면 바탕이 비어 있어야 한다. 기본은 어두운 판을 깔고 그리는데,
        // 그러면 샌드백 위에 검은 네모가 얹힌다.
        bool transparent = args.Any(given => given is "투명" or "transparent");

        // 한 줄로, 프레임 수만큼만. 격자로 두면 칸이 남아, 읽는 쪽이 `가로 ÷ 프레임수` 로 자를 때 빈
        // 칸을 프레임으로 센다 — 실제로 네 프레임짜리가 열여섯 칸 판에 그려져 첫 칸에만 그림이 있고
        // 나머지 셋은 바탕뿐이었다. 화면에서는 「정지된 그림」으로 보인다.
        bool oneRow = args.Any(given => given.Equals("row", StringComparison.OrdinalIgnoreCase));

        // khan2.dat carries the women's pieces but no palettes of its own; they live in khan.dat. Interface
        // art has no slot table to look a palette up in, so there the same argument names one outright.
        string? named = args.Length > 6 && args[6].EndsWith(".pal", StringComparison.OrdinalIgnoreCase)
            ? args[6]
            : null;

        List<ArchivedItem> palettes = args.Length > 6 && named is null
            ? await ReadEntries(Path.GetFullPath(args[6]))
            : entries;

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
            Palette? palette = named is not null
                ? Sprites.Named(palettes, named)
                : Sprites.ForWardrobe(palettes, item.Name, female) ?? Sprites.Named(palettes, "palb000.pal");

            if (palette is null)
            {
                Console.Error.WriteLine($"{item.Name} 의 색표를 찾지 못했습니다. 색표 이름을 일곱째 인자로 주세요.");
                return 2;
            }

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

        await Sprites.Save(output, cells, oneRow ? cells.Count : columns, zoom, transparent);
        Console.WriteLine($"{chosen.Count}개 파일 · 프레임 {cells.Count}개를 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>
    /// Draws one effect the way the original client places it: every frame on the file's own canvas at the
    /// <c>Left/Top</c> its table of contents gives, and beside it the anchor that lands on whoever it is cast at.
    /// </summary>
    /// <remarks>
    /// The <c>epf</c> command throws both away — it packs each frame into a cell the size of the biggest one,
    /// centred and bottom-aligned. That is right for wardrobe pieces and wrong for effects: 일음지
    /// (<c>efct042</c>) is a 13x13 sparkle at (48,10) on a 111x85 canvas, so packed on its own it became a
    /// 13x13 picture that the screen then stretched over the whole body. Laid on the canvas it stays a
    /// sparkle above the head, which is where the original puts it.
    ///
    /// The anchor is <c>efct###.tbl</c>: two 16-bit numbers per frame, x then y. 일음지 is 55,70 on that
    /// 111x85 canvas — the middle across, and low enough to sit at the feet. The original reads it only for
    /// names carrying <c>Efct</c> (4.51 <c>0x44ba04</c>, docs/disassembly.md). A note in
    /// <c>build-ability-sprites.py</c> called this file meaningless because UTF-16 renders those bytes "7F";
    /// it is not text.
    /// </remarks>
    /// <remarks><c>efct &lt;archive&gt; &lt;efct042&gt; &lt;output.png&gt; [zoom] [palette.pal]</c></remarks>
    private static async Task<int> RenderEfct(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("efct 에는 이름과 출력 파일이 필요합니다.");
            return 2;
        }

        string bare = Path.GetFileNameWithoutExtension(args[2]);
        string output = Path.GetFullPath(args[3]);
        int zoom = args.Length > 4 ? int.Parse(args[4]) : 1;
        string paletteName = args.Length > 5 ? args[5] : "eff000.pal";

        ArchivedItem? item = entries.FirstOrDefault(entry =>
            entry.Name.Equals($"{bare}.epf", StringComparison.OrdinalIgnoreCase));

        if (item is null)
        {
            Console.Error.WriteLine($"{bare}.epf 가 없습니다.");
            return 2;
        }

        Palette? palette = Sprites.Named(entries, paletteName) ?? Sprites.Named(entries, "eff000.pal");

        if (palette is null)
        {
            Console.Error.WriteLine($"{paletteName} 색표를 찾지 못했습니다.");
            return 2;
        }

        Epf.Sheet sheet = Epf.Read(item.Data);
        List<Epf.Frame> frames = sheet.Frames;

        if (frames.Count == 0)
        {
            Console.Error.WriteLine($"{bare}.epf 에 프레임이 없습니다.");
            return 2;
        }

        // A file with no canvas of its own still has to be drawn on something; the frames themselves say how big.
        int wide = sheet.Width > 0 ? sheet.Width : frames.Max(frame => frame.Left + frame.Width);
        int tall = sheet.Height > 0 ? sheet.Height : frames.Max(frame => frame.Top + frame.Height);
        wide = Math.Max(1, wide);
        tall = Math.Max(1, tall);

        // Without the table the middle of the floor is the honest guess, and it is what the numbers that do
        // exist come out near: x is the canvas midpoint every time.
        int anchorX = wide / 2, anchorY = tall;
        ArchivedItem? table = entries.FirstOrDefault(entry =>
            entry.Name.Equals($"{bare}.tbl", StringComparison.OrdinalIgnoreCase));

        if (table is not null && table.Data.Length >= 4)
        {
            anchorX = BitConverter.ToInt16(table.Data, 0);
            anchorY = BitConverter.ToInt16(table.Data, 2);
        }

        using Image<Rgba32> canvas = new(wide * frames.Count, tall);

        for (int index = 0; index < frames.Count; index++)
        {
            Epf.Frame frame = frames[index];

            for (int y = 0; y < frame.Height; y++)
            {
                int top = frame.Top + y;

                if (top < 0 || top >= tall)
                {
                    continue;
                }

                for (int x = 0; x < frame.Width; x++)
                {
                    int left = frame.Left + x;
                    byte code = frame.Data[(y * frame.Width) + x];

                    // Index 0 is see-through, and a drawing that reaches past its canvas is clipped rather
                    // than allowed to spill into the next frame's cell.
                    if (code == 0 || left < 0 || left >= wide)
                    {
                        continue;
                    }

                    System.Drawing.Color colour = palette[code];
                    canvas[(index * wide) + left, top] = new Rgba32(colour.R, colour.G, colour.B, 255);
                }
            }
        }

        if (zoom > 1)
        {
            canvas.Mutate(context => context.Resize(
                canvas.Width * zoom, canvas.Height * zoom, KnownResamplers.NearestNeighbor));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await canvas.SaveAsPngAsync(output);
        Console.WriteLine(
            $"  {item.Name}: 프레임 {frames.Count}개 · 바탕 {wide}x{tall} · 기준 {anchorX},{anchorY}");

        return 0;
    }

    /// <summary>
    /// Draws an EFA effect in one row. The newer effects (efct232 and up in the Korean 5.99 client) are kept this
    /// way instead of as EPF: each frame is its own zlib stream of RGB565 pixels, and the client makes the dark
    /// parts see-through by their brightness. Read the way <c>sources/wren11/da-lib/DALib/Drawing/EfaFile.cs</c>
    /// and <c>Graphics.RenderImage(EfaFrame)</c> read it.
    /// </summary>
    /// <remarks><c>efa &lt;archive&gt; &lt;name&gt; &lt;output.png&gt; [zoom]</c></remarks>
    private static async Task<int> RenderEfa(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("efa 에는 이름과 출력 파일이 필요합니다.");
            return 2;
        }

        string name = args[2].EndsWith(".efa", StringComparison.OrdinalIgnoreCase) ? args[2] : args[2] + ".efa";
        string output = Path.GetFullPath(args[3]);
        int zoom = args.Length > 4 ? int.Parse(args[4]) : 1;

        ArchivedItem? item = entries.FirstOrDefault(entry => entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            Console.Error.WriteLine($"{name} 이 없습니다.");
            return 2;
        }

        using var stream = new MemoryStream(item.Data);
        using var reader = new BinaryReader(stream);
        reader.ReadInt32();
        int count = reader.ReadInt32();
        reader.ReadInt32(); // 프레임 간격(ms)
        byte blending = reader.ReadByte();
        reader.ReadBytes(51);

        // Where the effect lands on whoever it was cast at. EPF effects keep this in efct###.tbl; EFA carries
        // it per frame, and the first frame speaks for the file.
        int anchorX = 0, anchorY = 0;

        var headers = new List<(int Offset, int Compressed, int Decompressed, int ByteWidth, int ByteCount,
            int ImageWidth, int ImageHeight, int Left, int Top, int FrameWidth, int FrameHeight)>();
        for (int i = 0; i < count; i++)
        {
            reader.ReadInt32();
            int offset = reader.ReadInt32();
            int compressed = reader.ReadInt32();
            int decompressed = reader.ReadInt32();
            reader.ReadInt32();
            reader.ReadInt32();
            int byteWidth = reader.ReadInt32();
            reader.ReadInt32();
            int byteCount = reader.ReadInt32();
            reader.ReadInt32();
            short centreX = reader.ReadInt16();
            short centreY = reader.ReadInt16();
            if (i == 0)
            {
                (anchorX, anchorY) = (centreX, centreY);
            }

            reader.ReadInt32();
            int imageWidth = reader.ReadInt16();
            int imageHeight = reader.ReadInt16();
            int left = reader.ReadInt16();
            int top = reader.ReadInt16();
            int frameWidth = reader.ReadInt16();
            int frameHeight = reader.ReadInt16();
            reader.ReadInt32();
            headers.Add((offset, compressed, decompressed, byteWidth, byteCount, imageWidth, imageHeight, left, top,
                frameWidth, frameHeight));
        }

        long dataStart = stream.Position;
        Console.WriteLine($"  {item.Name}: 프레임 {count}개");
        if (count == 0)
            return 2;

        int cellWidth = Math.Max(1, headers.Max(frame => frame.ImageWidth));
        int cellHeight = Math.Max(1, headers.Max(frame => frame.ImageHeight));

        // A file that names no centre still has to be placed; the middle of the floor is the honest guess.
        if (anchorX == 0 && anchorY == 0)
        {
            (anchorX, anchorY) = (cellWidth / 2, cellHeight);
        }

        // 밝기를 투명도로 — 1 이 보통, 2 는 조금 덜 비친다. 3 은 원작도 몇 개만 제대로 그린다.
        float coefficient = blending == 2 ? 1.25f : blending == 3 ? -1f : 1f;

        using Image<Rgba32> canvas = new(cellWidth * zoom * count, cellHeight * zoom);
        for (int index = 0; index < count; index++)
        {
            var frame = headers[index];
            if (frame.ByteCount == 0 || frame.ByteWidth == 0)
                continue;

            byte[] raw = new byte[frame.Decompressed];
            using (var packed = new MemoryStream(item.Data, (int)(dataStart + frame.Offset), frame.Compressed))
            using (var inflater = new System.IO.Compression.ZLibStream(packed, System.IO.Compression.CompressionMode.Decompress))
            {
                inflater.ReadAtLeast(raw, frame.Decompressed, throwOnEndOfStream: false);
            }

            int dataWidth = frame.ByteWidth / 2;
            int dataHeight = frame.ByteCount / frame.ByteWidth;
            for (int y = 0; y < dataHeight; y++)
            for (int x = 0; x < dataWidth; x++)
            {
                int xActual = x + frame.Left, yActual = y + frame.Top;
                if (xActual >= frame.FrameWidth || yActual >= frame.FrameHeight ||
                    xActual >= cellWidth || yActual >= cellHeight)
                    continue;

                ushort value = BitConverter.ToUInt16(raw, y * frame.ByteWidth + x * 2);
                byte r = (byte)(((value >> 11) & 31) * 255 / 31);
                byte g = (byte)(((value >> 5) & 63) * 255 / 63);
                byte b = (byte)((value & 31) * 255 / 31);
                byte alpha = 255;
                if (coefficient > 0)
                {
                    float linear = 0.299f * MathF.Pow(r / 255f, 2) + 0.587f * MathF.Pow(g / 255f, 2) +
                                   0.114f * MathF.Pow(b / 255f, 2);
                    alpha = (byte)Math.Clamp(MathF.Round(MathF.Pow(linear, 0.5f) * 255f * coefficient), 0, 255);
                }

                var pixel = new Rgba32(r, g, b, alpha);
                for (int dy = 0; dy < zoom; dy++)
                for (int dx = 0; dx < zoom; dx++)
                    canvas[(index * cellWidth + xActual) * zoom + dx, yActual * zoom + dy] = pixel;
            }
        }

        await canvas.SaveAsPngAsync(output);
        Console.WriteLine(
            $"  {item.Name}: 프레임 {count}개 · 바탕 {cellWidth}x{cellHeight} · 기준 {anchorX},{anchorY}");
        Console.WriteLine($"프레임 {count}개를 {output} 에 그렸습니다.");
        return 0;
    }

    /// <summary>
    /// Draws SPF pictures. An SPF brings its own palette, so unlike <c>epf</c> this needs nothing else —
    /// which is the whole reason the newer interface art is kept in that format.
    /// </summary>
    private static async Task<int> RenderSpf(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("spf 에는 이름 조각과 출력 파일이 필요합니다.");
            return 2;
        }

        string filter = args[2];
        string output = Path.GetFullPath(args[3]);
        int columns = args.Length > 4 ? int.Parse(args[4]) : 1;
        int zoom = args.Length > 5 ? int.Parse(args[5]) : 1;

        // 'tight': no gap between cells, so the client can slice the sheet by frame * width.
        bool tight = args.Length > 6 && args[6].Equals("tight", StringComparison.OrdinalIgnoreCase);

        List<ArchivedItem> chosen = entries
            .Where(entry => entry.Name.EndsWith(".spf", StringComparison.OrdinalIgnoreCase)
                         && entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (chosen.Count == 0)
        {
            Console.Error.WriteLine($"{filter} 에 맞는 .spf 가 없습니다.");
            return 2;
        }

        List<(byte[] Data, int Width, int Height, Palette Palette)> cells = [];

        foreach (ArchivedItem item in chosen)
        {
            Spf.Sheet sheet = Spf.Read(item.Data);
            Console.WriteLine($"  {item.Name}: 프레임 {sheet.Frames.Count}개");

            foreach (Spf.Frame frame in sheet.Frames)
            {
                cells.Add((frame.Data, frame.Width, frame.Height, sheet.Palette));
            }
        }

        await Sprites.Save(output, cells, columns, zoom, transparent: true, padding: tight ? 0 : 4);
        Console.WriteLine($"{chosen.Count}개 파일 · 프레임 {cells.Count}개를 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>
    /// Draws item icons. The server sends <c>DisplayImage</c>, which carries 0x8000 to say "this is an
    /// item"; below that is a tile number counted across the <c>item###.epf</c> files at 266 frames each.
    /// The palette comes from <c>itempal.tbl</c>, written in the same 1-based tile numbers.
    /// </summary>
    /// <summary>
    /// Where one item icon lives. The number the server sends carries 0x8000 to say "this is an item", and
    /// below that the tile is counted from 1 across the <c>item###.epf</c> files at 266 frames each — so
    /// the number steps back one before it is split, and tile 267 is the first frame of the second file.
    /// </summary>
    internal static (int File, int Frame, int Tile) IconCell(int display)
    {
        int tile = display >= ItemImageFlag ? display - ItemImageFlag : display;

        return (((tile - 1) / FramesPerItemFile) + 1, (tile - 1) % FramesPerItemFile, tile);
    }

    private static async Task<int> RenderIcon(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("icon 에는 번호들과 출력 파일이 필요합니다.");
            return 2;
        }

        string output = Path.GetFullPath(args[3]);
        int zoom = args.Length > 4 && int.TryParse(args[4], out int given) ? given : 4;

        ArchivedItem? table = entries.FirstOrDefault(entry =>
            entry.Name.Equals("itempal.tbl", StringComparison.OrdinalIgnoreCase));

        if (table is null)
        {
            Console.Error.WriteLine("itempal.tbl 이 이 아카이브에 없습니다.");
            return 2;
        }

        List<(byte[] Data, int Width, int Height, Palette Palette)> cells = [];

        foreach (string word in args[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(word, out int display))
            {
                continue;
            }

            (int fileNumber, int frameNumber, int tile) = IconCell(display);

            ArchivedItem? file = entries.FirstOrDefault(entry =>
                entry.Name.Equals($"item{fileNumber:000}.epf", StringComparison.OrdinalIgnoreCase));

            if (file is null)
            {
                Console.Error.WriteLine($"  {display}: item{fileNumber:000}.epf 이 없습니다.");
                continue;
            }

            Epf.Sheet sheet = Epf.Read(file.Data);

            if (frameNumber >= sheet.Frames.Count)
            {
                Console.Error.WriteLine($"  {display}: {file.Name} 에는 칸이 {sheet.Frames.Count}개뿐입니다.");
                continue;
            }

            int palette = IconPalettes.PaletteFor(table.Data, tile);
            Palette colours = Sprites.Named(entries, $"item{palette:000}.pal")
                              ?? throw new InvalidOperationException($"item{palette:000}.pal 이 없습니다.");

            Epf.Frame frame = sheet.Frames[frameNumber];
            Console.WriteLine(
                $"  {display} -> {file.Name} 칸 {frameNumber} · 색표 item{palette:000}.pal · {frame.Width}x{frame.Height}");
            cells.Add((frame.Data, frame.Width, frame.Height, colours));
        }

        if (cells.Count == 0)
        {
            Console.Error.WriteLine("그릴 아이콘이 없습니다.");
            return 2;
        }

        // 아이콘은 화면 위에 얹는 것이라 바탕을 깔면 검은 사각이 따라다닌다.
        await Sprites.Save(output, cells, cells.Count, zoom, transparent: true);
        Console.WriteLine($"아이콘 {cells.Count}개를 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>
    /// Stacks wardrobe pieces into one figure. The original draws a character as separate layers — body,
    /// then what it wears, then what it holds — each carrying its own offset inside a shared box, so the
    /// pieces line up when drawn in order at the same frame number.
    /// </summary>
    /// <summary>
    /// Prints one of the original game's own tables. They are not in any archive — they sit in the server's
    /// database folder, one zlib stream each — and nothing else here reads them.
    /// </summary>
    private static int ShowMetaFile(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("metafile <파일> [찾을 말]");
            return 2;
        }

        string wanted = args.Length > 2 ? args[2] : string.Empty;
        List<MetaFile.Row> rows = MetaFile.Read(Path.GetFullPath(args[1]));

        List<MetaFile.Row> shown = wanted.Length == 0
            ? rows
            : [.. rows.Where(row =>
                row.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
                || row.Fields.Any(field => field.Contains(wanted, StringComparison.OrdinalIgnoreCase)))];

        foreach (MetaFile.Row row in shown)
        {
            Console.WriteLine($"{row.Name}	{string.Join(" | ", row.Fields)}");
        }

        Console.WriteLine($"— {shown.Count}줄 / 전체 {rows.Count}줄");

        return 0;
    }

    /// <summary>
    /// Writes the colours <c>pose … marker</c> leaves in the dyed slots, so whoever recolours the sheets
    /// does not have to be told them twice.
    /// </summary>
    private static async Task<int> WriteDyeSlots(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("dyeslots 에는 출력 파일이 필요합니다.");
            return 2;
        }

        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);

        await File.WriteAllLinesAsync(
            output,
            ColourTable.Markers.Select(marker => $"{marker.R},{marker.G},{marker.B}"));

        Console.WriteLine($"표시색 {ColourTable.Markers.Length}개를 {output} 에 적었습니다.");

        return 0;
    }

    private static async Task<int> RenderPose(List<ArchivedItem> entries, string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine(
                "pose <아카이브> <겹칠 이름들> <출력> [자세들] [확대] [칸 크기 예: 80x88] [색 번호] [색표 경로]");
            return 2;
        }

        string[] layers = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string output = Path.GetFullPath(args[3]);
        int[] poses = (args.Length > 4 ? args[4] : "0")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
        int zoom = args.Length > 5 ? int.Parse(args[5]) : 4;

        // Optional: a dye number and the table it is in, which recolours the run of palette entries the
        // original leaves free for exactly that.
        System.Drawing.Color[]? dye = null;

        if (args.Length > 7 && args[7].Equals("marker", StringComparison.OrdinalIgnoreCase))
        {
            dye = ColourTable.Markers;
        }
        else if (args.Length > 8 && int.TryParse(args[7], out int wantedDye))
        {
            Dictionary<int, System.Drawing.Color[]> table = ColourTable.Read(Path.GetFullPath(args[8]));

            if (!table.TryGetValue(wantedDye, out dye))
            {
                Console.Error.WriteLine($"색 {wantedDye} 번이 {args[8]} 에 없습니다.");
                return 2;
            }
        }

        bool female = Path.GetFileName(args[1]).StartsWith("khan2", StringComparison.OrdinalIgnoreCase);

        // The women's archive carries no palettes of its own and shares the men's, so colours are looked
        // up next door. Where "next door" is depends on how the archives are laid out: loose in one folder,
        // or each in a folder of its own under database/archives — try the first, then the second.
        // The 5.99 Korean client's women's archive holds one stray palette (palb00) and none of the rest, so
        // having a palette is not enough to stop looking — a woman's pieces always read the men's as well.
        List<ArchivedItem> palettes = female
            ? [.. entries, .. await ReadEntries(MensArchive(Path.GetFullPath(args[1])))]
            : entries.Any(entry => entry.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase))
                ? entries
                : await ReadEntries(MensArchive(Path.GetFullPath(args[1])));

        List<(Epf.Frame Frame, Palette Palette, int ShiftX, int ShiftY)>[] stacks =
            new List<(Epf.Frame, Palette, int, int)>[poses.Length];

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

            // The markers only work because nothing else in the palette wears those colours.
            if (dye == ColourTable.Markers)
            {
                for (int code = 1; code < 256; code++)
                {
                    if (code >= ColourTable.FirstDyedIndex && code < ColourTable.FirstDyedIndex + dye.Length)
                    {
                        continue;
                    }

                    if (ColourTable.Markers.Any(marker =>
                            marker.R == palette[code].R
                            && marker.G == palette[code].G
                            && marker.B == palette[code].B))
                    {
                        Console.Error.WriteLine($"  {layer}: 표시색이 팔레트 {code} 번과 겹칩니다");
                        return 2;
                    }
                }
            }
            Epf.Sheet sheet = Epf.Read(item.Data);
            List<Epf.Frame> frames = sheet.Frames;
            Console.WriteLine(
                $"  {item.Name}: 프레임 {frames.Count}개, 바탕 {sheet.Width}x{sheet.Height}, " +
                $"첫 칸 {frames.FirstOrDefault()?.Left},{frames.FirstOrDefault()?.Top}");

            // Every piece is centred on its canvas, and the plain wardrobe canvas is 57x85. Weapons and
            // accessories are drawn on a wider 111x85 one so they can reach out past the body, and lining the
            // two centres up is what puts the hilt in a hand instead of a stick beside it. Which canvas comes
            // from what the piece is, not from its file's header: mw002's header says 20x18 while mw028's says
            // 111x85, and trusting it left every such sword 27 pixels out of the hand. The atlases the
            // reference client ships give all 9,962 men's weapon frames and every accessory 111x85, and every
            // other piece 57x85 (sources/FallenDev/dark-ages-ts/apps/client/public/aislings/*/*.atlas).
            // The front piece of a weapon (p, drawn over the body with the weapon's number) sits on the weapon's
            // canvas too: mp127's frames are placed exactly where mw127's are.
            int canvasWidth = layer.Length > 1 && char.ToLowerInvariant(layer[1]) is 'w' or 'c' or 'p'
                ? ReachingWidth
                : WardrobeWidth;
            int shiftX = (canvasWidth - WardrobeWidth) / 2;
            int shiftY = 0;

            for (int slot = 0; slot < poses.Length; slot++)
            {
                if (poses[slot] < frames.Count)
                {
                    stacks[slot].Add((frames[poses[slot]], palette, shiftX, shiftY));
                }
            }
        }

        if (stacks.All(stack => stack.Count == 0))
        {
            Console.Error.WriteLine("겹칠 것이 없습니다.");
            return 2;
        }

        int drawnWidth = stacks.SelectMany(stack => stack)
            .Max(entry => entry.Frame.Left - entry.ShiftX + entry.Frame.Width) + 4;
        int drawnHeight = stacks.SelectMany(stack => stack)
            .Max(entry => entry.Frame.Top - entry.ShiftY + entry.Frame.Height) + 4;
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
            foreach ((Epf.Frame frame, Palette palette, int pieceX, int pieceY) in stacks[slot])
            {
                Sprites.Blit(
                    canvas,
                    frame.Data,
                    frame.Width,
                    frame.Height,
                    palette,
                    (slot * cellWidth) + 2 + frame.Left - pieceX,
                    2 + frame.Top - pieceY,
                    dye);
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

        // 'strip': one row of square cells, edge to edge, plus a text file naming the motion segments.
        // That is the only shape the client can read — it works the frame size out from the sheet alone
        // (width / height), and it needs the segments because every creature numbers its own differently.
        bool strip = args.Length > 6 && args[6].Equals("strip", StringComparison.OrdinalIgnoreCase);

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

            if (strip && names.Length == 1)
            {
                await WriteMotion(output, sheet);
            }
        }

        if (cells.Count == 0)
        {
            Console.Error.WriteLine("그릴 프레임이 없습니다.");
            return 2;
        }

        await Sprites.Save(
            output,
            cells,
            strip ? cells.Count : Math.Min(cells.Count, 8),
            zoom,
            transparent,
            padding: strip ? 0 : 4,
            square: strip);

        Console.WriteLine($"프레임 {cells.Count}개를 {output} 에 그렸습니다.");

        return 0;
    }

    /// <summary>Where the men's archive is, given the women's. It holds the palettes both share.</summary>
    private static string MensArchive(string womens)
    {
        string folder = Path.GetDirectoryName(womens)!;
        string beside = Path.Combine(folder, "khan.dat");

        if (File.Exists(beside))
        {
            return beside;
        }

        // database/archives/khan2/khan2.dat → database/archives/khan/khan.dat
        string sibling = Path.Combine(Path.GetDirectoryName(folder)!, "khan", "khan.dat");

        return File.Exists(sibling) ? sibling : beside;
    }

    /// <summary>
    /// Writes a creature's motion segments beside its sheet. Every creature numbers its own frames — one
    /// walks on 0..2 and swings on 6, the next walks on 0..4 and swings on 10 — so a reader that guesses
    /// plays the wrong drawings. A count of zero means the creature has no such motion: the original's
    /// <c>fStop</c> says a wasp may not stand still, and its file gives it no standing frames to stand on.
    /// </summary>
    private static async Task WriteMotion(string output, Mpf.Sheet sheet)
    {
        string beside = Path.ChangeExtension(output, ".txt");

        await File.WriteAllTextAsync(
            beside,
            $"frames {sheet.Frames.Count}\n"
            + $"stand {sheet.StandStart} {sheet.StandCount}\n"
            + $"walk {sheet.WalkStart} {sheet.WalkCount}\n"
            + $"attack {sheet.AttackStart} {sheet.AttackCount}\n");
    }

    /// <summary>
    /// Whether this run wants the snow ground set. The server says so with MapFlags.SnowTileset (128), but
    /// a .map file carries only tile numbers, so the word has to come from the command line.
    /// </summary>
    private static bool WantsSnow(string[] args) => args.Any(word =>
        word.Equals("눈", StringComparison.Ordinal)
        || word.Equals("snow", StringComparison.OrdinalIgnoreCase));

    private const int TileWidth = 56;
    private const int TileHeight = 27;
    // 한 칸이 아래로 내려가는 폭. mobile/src/Lod.Mobile.Core/Art/IsometricFloor.cs 의 HalfHeight 와 같아야 한다.
    private const int HalfHeight = 13;

    /// <summary>The ground tile set and the palettes that colour it, read once and reused.</summary>
    private sealed class TileSource
    {
        public required List<Tile> Tiles { get; init; }
        public required List<Palette> Palettes { get; init; }
        public required MapObjects.PaletteChoice Choice { get; init; }

        /// <summary>How far into the palette table this set's own tile 0 sits.</summary>
        public required int PaletteOffset { get; init; }

        /// <summary>
        /// TILEAS is the same ground set in snow — the S is for snow, not for anything structural, and the
        /// map editor picks between the two exactly this way. The palette table numbers tiles across both
        /// sets appended together, which da-lib's Tileset docs say outright ("Ensure the tile ids you add
        /// to the PaletteTable are based on appending to the existing tileset"), so a snow tile's palette
        /// is found at TILEA's count plus its own index. Asking at its own index lands in TILEA's rows and
        /// paints some tiles from the wrong palette.
        /// </summary>
        public static TileSource? From(List<ArchivedItem> entries, bool snow = false)
        {
            string wanted = snow ? "TILEAS.BMP" : "TILEA.BMP";

            ArchivedItem? tileSet = entries.FirstOrDefault(entry =>
                entry.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase));

            if (tileSet is null)
            {
                Console.Error.WriteLine($"{wanted} 를 찾지 못했습니다.");
                return null;
            }

            int offset = 0;

            if (snow)
            {
                ArchivedItem? plain = entries.FirstOrDefault(entry =>
                    entry.Name.Equals("TILEA.BMP", StringComparison.OrdinalIgnoreCase));

                if (plain is null)
                {
                    Console.Error.WriteLine("TILEA.BMP 가 없어 눈 타일의 팔레트 번호를 셀 수 없습니다.");
                    return null;
                }

                offset = plain.Data.Length / (TileWidth * TileHeight);
            }

            ArchivedItem? table = entries.FirstOrDefault(entry =>
                entry.Name.Equals($"{FloorPaletteFamily}pal.tbl", StringComparison.OrdinalIgnoreCase));

            if (table is null)
            {
                Console.Error.WriteLine($"{FloorPaletteFamily}pal.tbl 을 찾지 못했습니다.");
                return null;
            }

            return new TileSource
            {
                Tiles = new TileCollection(tileSet).Load(),
                Palettes = Palette.FromArchive(
                    entries.Where(e => e.Name.StartsWith(FloorPaletteFamily, StringComparison.OrdinalIgnoreCase)
                                       && e.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase))
                           .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)),
                Choice = MapObjects.PaletteChoice.Read(System.Text.Encoding.ASCII.GetString(table.Data)),
                PaletteOffset = offset
            };
        }

        public void Draw(Image<Rgba32> canvas, int index, int originX, int originY)
        {
            Palette palette = Palettes[Math.Clamp(FloorPalette(Choice, index + PaletteOffset), 0, Palettes.Count - 1)];
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

    /// <summary>
    /// Which family of floor palettes the client reads. 5.99 seo.dat carries two — mpt0000~0079.pal with
    /// mptpal.tbl, and mps0000~0022.pal with mpspal.tbl — but Legend.exe names only the first (its strings
    /// "mpt%04d.pal" · "mpt%04d.tbl" · "mptpal.tbl" at file 0x313180~0x313306; "mps" appears nowhere). Taking
    /// the mps pair painted grass as orange speckle, a stream as white snow and flower beds as red and blue dots.
    /// </summary>
    internal const string FloorPaletteFamily = "mpt";

    /// <summary>
    /// The palette of the floor tile at a 0-based index (map floor number - 1). The table is asked with the
    /// index plus two, as da-lib's Graphics.RenderMap does — the tables' first range starts at 2 for that reason.
    /// A two-number line names one tile and beats any range (<see cref="MapObjects.PaletteChoice" />).
    /// </summary>
    internal static int FloorPalette(MapObjects.PaletteChoice choice, int tileIndex) => choice.For(tileIndex + 2);

    /// <summary>
    /// Says which cells of a map block. Walls are not pictures of their own — the map's two wall numbers
    /// point into sotp.dat, one flag byte per tile, and 0x0F means wall. This reads no archive: a .map and
    /// sotp.dat are the whole story.
    /// </summary>
    private static int ShowWalls(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("walls 에는 맵 파일과 sotp.dat 이 필요합니다.");
            return 2;
        }

        string mapPath = Path.GetFullPath(args[1]);
        string sotpPath = Path.GetFullPath(args[2]);

        foreach (string needed in new[] { mapPath, sotpPath })
        {
            if (!File.Exists(needed))
            {
                Console.Error.WriteLine($"파일을 찾을 수 없습니다: {needed}");
                return 2;
            }
        }

        byte[] sotp = File.ReadAllBytes(sotpPath);
        List<Walls.Cell> cells = Walls.Read(File.ReadAllBytes(mapPath));

        int blocked = cells.Count(cell => Walls.Blocks(sotp, cell.Left, cell.Right));
        int walled = cells.Count(cell => cell.Left != 0 || cell.Right != 0);

        Console.WriteLine($"{Path.GetFileName(mapPath)} — 칸 {cells.Count}개");
        Console.WriteLine($"  벽 번호가 붙은 칸 {walled}개 · 그중 막는 칸 {blocked}개");
        Console.WriteLine($"  sotp.dat {sotp.Length}바이트 (타일 한 칸당 한 바이트)");

        int columns = args.Length > 3 && int.TryParse(args[3], out int given) ? given : 0;

        if (columns <= 0)
        {
            return 0;
        }

        // 그림으로 보면 방 모양이 바로 드러난다. 막는 칸은 #, 지나갈 수 있으면 · 다.
        Console.WriteLine();

        for (int row = 0; row * columns < cells.Count; row++)
        {
            char[] line = new char[Math.Min(columns, cells.Count - (row * columns))];

            for (int column = 0; column < line.Length; column++)
            {
                Walls.Cell cell = cells[(row * columns) + column];
                line[column] = Walls.Blocks(sotp, cell.Left, cell.Right) ? '#' : '.';
            }

            Console.WriteLine(new string(line));
        }

        return 0;
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"알 수 없는 명령: {command}");
        return 2;
    }
}
