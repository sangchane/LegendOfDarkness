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
    private const int WardrobeHeight = 85;

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
            Console.Error.WriteLine("        dat-extract mpf <hades.dat> <이름들> <출력.png> [배율] [투명|transparent]");
            Console.Error.WriteLine("        dat-extract pose <khan.dat> <겹칠이름들> <출력.png> [프레임들] [배율] [칸] [색번호|marker] [색표]");
            Console.Error.WriteLine("        dat-extract icon <Legend.dat> <번호들> <출력.png> [배율]");
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
            $"타일 {source.Tiles.Count}개 · 팔레트 {source.Palettes.Count}개 · 표 {source.Tables.Count}개");

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

        await Sprites.Save(output, cells, columns, zoom);
        Console.WriteLine($"{chosen.Count}개 파일 · 프레임 {cells.Count}개를 {output} 에 그렸습니다.");

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
        List<ArchivedItem> palettes =
            entries.Any(entry => entry.Name.EndsWith(".pal", StringComparison.OrdinalIgnoreCase))
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

            // Every piece is centred on the canvas its own file declares, and the plain wardrobe canvas is
            // 57x85. A weapon or an accessory declares a wider one so it can reach out past the body, and
            // lining the two centres up is what puts the hilt in a hand instead of a stick beside it. The
            // atlases the reference client ships agree piece for piece
            // (sources/FallenDev/dark-ages-ts/apps/client/public/aislings/*.atlas).
            int shiftX = (Math.Max(sheet.Width, WardrobeWidth) - WardrobeWidth) / 2;
            int shiftY = (Math.Max(sheet.Height, WardrobeHeight) - WardrobeHeight) / 2;

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

    /// <summary>The ground tile set and the palettes that colour it, read once and reused.</summary>
    private sealed class TileSource
    {
        public required List<Tile> Tiles { get; init; }
        public required List<Palette> Palettes { get; init; }
        public required List<PaletteTable> Tables { get; init; }

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

            return new TileSource
            {
                Tiles = new TileCollection(tileSet).Load(),
                Palettes = Palette.FromArchive(Matching(entries, ".pal")),
                Tables = PaletteTable.FromArchive(Matching(entries, ".tbl"), "mpt", _ => { }).Result,
                PaletteOffset = offset
            };
        }

        private static IEnumerable<ArchivedItem> Matching(List<ArchivedItem> entries, string extension) =>
            entries.Where(e => e.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                            && e.Name.StartsWith("mpt", StringComparison.OrdinalIgnoreCase))
                   .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase);

        public void Draw(Image<Rgba32> canvas, int index, int originX, int originY)
        {
            Palette palette = PaletteFor(index + PaletteOffset, Tables, Palettes);
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
