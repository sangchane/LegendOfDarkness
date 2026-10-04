namespace Lod.Mobile.Core.Ui;

/// <summary>
/// 운영자 명령을 칠 때 고를 거리(사용자 2026-10-04). <c>/</c> 만 치면 명령, <c>/give 칼</c> 처럼 뒤에 글자를 치면 그 글자가
/// 든 아이템·마법·기술·맵 이름. 고르면 대화창 글이 서버가 바로 알아듣는 줄로 바뀐다 — 이름은 따옴표로 감싼다
/// (띄어쓰기가 든 이름이 있다, <c>Systems/Commander.cs</c>). 이름표는 <c>scripts/gen/client/build-gm-names.py</c>.
/// </summary>
public sealed class GmCompletion
{
    /// <summary>명령과 그 뒤에 고를 이름의 종류(없으면 이름 없이 그대로 보낸다).</summary>
    private static readonly (string Command, string? Kind, string Hint)[] Commands =
    [
        ("give", "item", "아이템 받기"),
        ("spell", "spell", "마법 배우기"),
        ("skill", "skill", "기술 배우기"),
        ("tp", "map", "순간이동"),
        ("pt", null, "사람에게 가기"),
        ("sp", null, "사람 부르기"),
    ];

    private readonly Dictionary<string, List<(string Name, string Tail)>> _names = [];

    public GmCompletion(string list)
    {
        foreach (string raw in list.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            int space = line.IndexOf(' ');

            if (line.StartsWith('#') || space < 0)
            {
                continue;
            }

            string kind = line[..space];
            string rest = line[(space + 1)..];
            string tail = string.Empty;

            // 맵은 끝의 두 수가 내려설 칸이다.
            if (kind == "map")
            {
                string[] parts = rest.Split(' ');
                if (parts.Length < 3)
                {
                    continue;
                }

                tail = $" {parts[^2]} {parts[^1]}";
                rest = string.Join(' ', parts[..^2]);
            }

            if (!_names.TryGetValue(kind, out List<(string, string)>? names))
            {
                _names[kind] = names = [];
            }

            names.Add((rest, tail));
        }
    }

    /// <summary>
    /// 지금 친 글에 맞는 고를 거리 — (단추에 보일 글, 고르면 대화창에 들어갈 글). 앞이 맞는 이름이 먼저, 그다음 가운데에
    /// 든 이름. <c>/</c> 로 시작하지 않으면 없다.
    /// </summary>
    public IReadOnlyList<(string Label, string Text)> Suggest(string typed, int most = 30)
    {
        if (!typed.StartsWith('/'))
        {
            return [];
        }

        int space = typed.IndexOf(' ');

        if (space < 0)
        {
            string start = typed[1..];
            return [.. Commands.Where(c => c.Command.StartsWith(start, StringComparison.OrdinalIgnoreCase))
                .Select(c => ($"/{c.Command} {c.Hint}", $"/{c.Command} "))];
        }

        string command = typed[1..space];
        (string Command, string? Kind, string Hint) found = Commands.FirstOrDefault(c => c.Command == command);

        if (found.Kind is null || !_names.TryGetValue(found.Kind, out List<(string Name, string Tail)>? names))
        {
            return [];
        }

        string query = typed[(space + 1)..].Trim().Trim('"');

        return [.. names.Where(n => n.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Take(most)
            .Select(n => (n.Name, $"/{command} \"{n.Name}\"{n.Tail}"))];
    }
}
