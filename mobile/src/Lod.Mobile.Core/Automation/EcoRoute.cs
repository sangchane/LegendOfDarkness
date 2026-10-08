using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Automation;

/// <summary>워프 칸 하나 — <see cref="Map" /> 의 <see cref="Where" /> 를 밟으면 <see cref="ToMap" /> 으로. 레벨은 서버 워프 검사 그대로(최대 0 = 없음).</summary>
public sealed record EcoLink(int Map, Tile Where, int ToMap, int MinLevel, int MaxLevel);

/// <summary>월드맵에서 고를 수 있는 곳(카드 도착지·구역). 레벨은 서버 <c>WorldMapRefusal</c> 그대로.</summary>
public sealed record EcoField(int Map, int MinLevel, int MaxLevel);

/// <summary>
/// 다음 한 걸음 — <see cref="Map" /> 에서 <see cref="Tiles" /> 중 하나를 밟는다. <see cref="Field" /> 가 0 이면 그 칸이 워프라 바로
/// <see cref="ToMap" /> 이고, 아니면 월드맵이 열리고 <see cref="Field" />(= <see cref="ToMap" />)를 고른다.
/// </summary>
public sealed record EcoLeg(int Map, IReadOnlyList<Tile> Tiles, int ToMap, int Field);

/// <summary>맵 사이 길 자료 <c>links.txt</c>(생성기 <c>scripts/gen/eco/build-eco-links.py</c>, 설계 <c>autopilot/eco-bots/walk-SPEC.md</c>).</summary>
public sealed class EcoLinks
{
    private readonly ILookup<int, EcoLink> _links;
    private readonly ILookup<int, Tile> _gates;
    private readonly ILookup<int, Tile> _blocks;

    private EcoLinks(IEnumerable<EcoLink> links, IEnumerable<(int Map, Tile Where)> gates, IReadOnlyList<EcoField> fields, IEnumerable<(int Map, Tile Where)> blocks)
    {
        _links = links.ToLookup(link => link.Map);
        _gates = gates.ToLookup(gate => gate.Map, gate => gate.Where);
        _blocks = blocks.ToLookup(block => block.Map, block => block.Where);
        Fields = fields;
    }

    public static EcoLinks Empty { get; } = new([], [], [], []);

    public IReadOnlyList<EcoField> Fields { get; }

    public IEnumerable<EcoLink> LinksOn(int map) => _links[map];

    public IEnumerable<Tile> GatesOn(int map) => _gates[map];

    /// <summary>그 맵에서 밟으면 다른 곳으로 가거나 NPC 스크립트가 도는 칸 모두 — 걷다가 엉뚱한 워프를 밟지 않게.</summary>
    public IEnumerable<Tile> TilesOn(int map) => _links[map].Select(link => link.Where).Concat(_gates[map]).Concat(_blocks[map]);

    /// <summary>
    /// <c>link 맵 x y 갈맵 갈x 갈y 최소 최대</c> · <c>gate 맵 x y</c> · <c>field 갈맵 갈x 갈y 최소 최대</c> · <c>block 맵 x y</c>(NPC 스크립트 워프 — 길로 안 쓰고 피하기만).
    /// <c>#</c> 은 주석, 틀린 줄은 건너뛴다.
    /// 도착 칸은 서버가 정하므로 읽지 않는다.
    /// </summary>
    public static EcoLinks Read(string text)
    {
        List<EcoLink> links = [];
        List<(int, Tile)> gates = [];
        List<EcoField> fields = [];
        List<(int, Tile)> blocks = [];

        foreach (string line in text.Split('\n'))
        {
            string[] part = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int[] n = [.. part.Skip(1).Select(word => int.TryParse(word, out int value) ? value : -1)];
            if (n.Contains(-1))
            {
                continue;
            }

            switch (part.FirstOrDefault(), n.Length)
            {
                case ("link", 8):
                    links.Add(new EcoLink(n[0], new Tile(n[1], n[2]), n[3], n[6], n[7]));
                    break;
                case ("gate", 3):
                    gates.Add((n[0], new Tile(n[1], n[2])));
                    break;
                case ("field", 5):
                    fields.Add(new EcoField(n[0], n[3], n[4]));
                    break;
                case ("block", 3):
                    blocks.Add((n[0], new Tile(n[1], n[2])));
                    break;
            }
        }

        return new EcoLinks(links, gates, fields, blocks);
    }
}

/// <summary>맵 사이 길찾기 — 워프 수가 가장 적은 길의 첫 걸음.</summary>
public static class EcoRoute
{
    /// <summary>
    /// <paramref name="from" /> 에서 <paramref name="to" /> 로 가는 길의 첫 걸음. 맵 단위 BFS(워프 수 최소, 같은 수면 워프가 월드맵보다 먼저),
    /// 레벨이 안 맞는 워프·월드맵 곳과 <paramref name="avoid" />(이번 길에서 막힌 칸)는 뺀다. 길이 없거나 이미 그 맵이면 null.
    /// ponytail: 맵 안은 모든 칸이 이어진다고 친다 — 걷다 막히면 부르는 쪽이 그 칸을 avoid 에 넣고 다시 묻는다.
    /// </summary>
    public static EcoLeg? Plan(EcoLinks links, int from, int to, int level, IReadOnlySet<(int Map, Tile Where)>? avoid = null)
    {
        bool Open(int min, int max) => level >= min && (max == 0 || level <= max);
        bool Free(int map, Tile where) => avoid?.Contains((map, where)) != true;
        int[] fields = [.. links.Fields.Where(field => Open(field.MinLevel, field.MaxLevel)).Select(field => field.Map)];

        // 맵마다 거기에 처음 닿은 길의 첫 걸음(다음 맵, 월드맵으로 갔나).
        Dictionary<int, (int Next, bool Field)> first = new() { [from] = (from, false) };
        Queue<int> edge = new([from]);

        while (edge.Count > 0)
        {
            int map = edge.Dequeue();
            IEnumerable<(int To, bool Field)> ways = links.LinksOn(map)
                .Where(link => Open(link.MinLevel, link.MaxLevel) && Free(map, link.Where))
                .Select(link => (link.ToMap, false))
                .Concat(links.GatesOn(map).Any(gate => Free(map, gate)) ? fields.Select(field => (field, true)) : []);

            foreach ((int next, bool field) in ways)
            {
                if (first.ContainsKey(next))
                {
                    continue;
                }

                first[next] = map == from ? (next, field) : first[map];
                if (next == to)
                {
                    (int step, bool viaField) = first[next];
                    IReadOnlyList<Tile> tiles = viaField
                        ? [.. links.GatesOn(from).Where(gate => Free(from, gate))]
                        : [.. links.LinksOn(from).Where(link => link.ToMap == step && Open(link.MinLevel, link.MaxLevel) && Free(from, link.Where))
                            .Select(link => link.Where)];
                    return new EcoLeg(from, tiles, step, viaField ? step : 0);
                }

                edge.Enqueue(next);
            }
        }

        return null;
    }
}
