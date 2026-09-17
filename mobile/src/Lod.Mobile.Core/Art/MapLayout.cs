using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Art;

/// <summary>Where one picture sits on a map's object sheet, and whether it adds light rather than covering.</summary>
public readonly record struct MapPicture(int X, int Y, int Height, bool Glows)
{
    /// <summary>Every object picture is this wide (stc#####.hpf).</summary>
    public const int Width = 28;
}

/// <summary>One picture standing on the left or right half of a cell.</summary>
public readonly record struct MapObject(int Column, int Row, bool Right, int Picture);

/// <summary>
/// A map put together from its parts: which floor tile lies on each cell, which cells cannot be walked into, and
/// what stands on them — buildings, trees, lamps. Written by <c>tools/dat-extract layout</c> as
/// <c>map&lt;번호&gt;.txt</c> beside two sheets (<c>-floor.png</c> of 56x27 tiles, <c>-objects.png</c> of pictures),
/// from the same .map file and sotp.dat the server reads, so the client and the server agree on every wall.
/// </summary>
public sealed class MapLayout
{
    private readonly bool[] _blocked;
    private readonly int[] _floor;

    private MapLayout(
        int columns,
        int rows,
        bool[] blocked,
        int[] floor,
        IReadOnlyDictionary<int, (int X, int Y)> tiles,
        IReadOnlyDictionary<int, MapPicture> pictures,
        IReadOnlyList<MapObject> objects)
    {
        Columns = columns;
        Rows = rows;
        _blocked = blocked;
        _floor = floor;
        Tiles = tiles;
        Pictures = pictures;
        Objects = objects;
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Where each floor tile, by its number, sits on the floor sheet.</summary>
    public IReadOnlyDictionary<int, (int X, int Y)> Tiles { get; }

    public IReadOnlyDictionary<int, MapPicture> Pictures { get; }

    public IReadOnlyList<MapObject> Objects { get; }

    /// <summary>Whether a step onto this tile is refused. Off the map counts as a wall, as it does on the server (Area.IsWall).</summary>
    public bool Blocks(Tile tile) =>
        tile.X < 0 || tile.Y < 0 || tile.X >= Columns || tile.Y >= Rows || _blocked[(tile.Y * Columns) + tile.X];

    /// <summary>The floor tile on a cell, or 0 where nothing is laid.</summary>
    public int Floor(int column, int row) => _floor[(row * Columns) + column];

    public static MapLayout Read(string text)
    {
        string[] lines = text.Split('\n', StringSplitOptions.TrimEntries);
        int columns = 0;
        int rows = 0;
        bool[] blocked = [];
        int[] floor = [];
        Dictionary<int, (int X, int Y)> tiles = [];
        Dictionary<int, MapPicture> pictures = [];
        List<MapObject> objects = [];

        for (int at = 0; at < lines.Length; at++)
        {
            string[] words = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0 || words[0].StartsWith('#'))
            {
                continue;
            }

            switch (words[0])
            {
                case "size":
                    columns = int.Parse(words[1]);
                    rows = int.Parse(words[2]);
                    blocked = new bool[columns * rows];
                    floor = new int[columns * rows];
                    break;

                case "blocked":
                    // 가로 칸 수만큼의 글자가 세로 칸 수만큼 줄로 온다. # 이 막힌 칸이다.
                    for (int row = 0; row < rows; row++)
                    {
                        string cells = lines[++at];

                        for (int column = 0; column < Math.Min(columns, cells.Length); column++)
                        {
                            blocked[(row * columns) + column] = cells[column] == '#';
                        }
                    }

                    break;

                case "floor":
                    // 한 줄에 가로 칸 수만큼 바닥 번호가 띄어 쓰여 온다.
                    for (int row = 0; row < rows; row++)
                    {
                        string[] numbers = lines[++at].Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        for (int column = 0; column < Math.Min(columns, numbers.Length); column++)
                        {
                            floor[(row * columns) + column] = int.Parse(numbers[column]);
                        }
                    }

                    break;

                case "tile":
                    tiles[int.Parse(words[1])] = (int.Parse(words[2]), int.Parse(words[3]));
                    break;

                case "picture":
                    pictures[int.Parse(words[1])] =
                        new MapPicture(int.Parse(words[2]), int.Parse(words[3]), int.Parse(words[4]), words[5] == "1");
                    break;

                case "object":
                    objects.Add(new MapObject(int.Parse(words[1]), int.Parse(words[2]), words[3] == "right", int.Parse(words[4])));
                    break;
            }
        }

        return new MapLayout(columns, rows, blocked, floor, tiles, pictures, objects);
    }
}
