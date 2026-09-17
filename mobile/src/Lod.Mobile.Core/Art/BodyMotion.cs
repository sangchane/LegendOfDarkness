namespace Lod.Mobile.Core.Art;

/// <summary>
/// What a person's body does when the server says it moves (0x1A): which file of each wardrobe piece holds the
/// drawings, where they start and how many there are for one side. Every stretch is drawn from behind first and
/// then the same count again from the front, like the walk.
/// </summary>
/// <remarks>
/// 1 is the plain blow, the whole of the file ending 02. From 128 the number is 128 + NO of the original's
/// <c>skill.tbl</c>, whose FN picks the class file (b priest · c warrior · d monk · e rogue · f wizard) and whose
/// SI·FC are the start and count. Arbiter and ETDA name the same numbers (128 PriestCast … 145 Summon).
/// The file ending 03 holds 6 hands up, 21 blowing a kiss and 22 waving (Legend.exe 2005 0x4e3657..0x4e36c9); the
/// emotes (9~17, 23~44) are drawn over the head instead (<see cref="Emote" />). docs/original-sprite-animation.md 3절.
/// </remarks>
/// <param name="Faster">How many times shorter each drawing is held than the speed says — the wave is 3.</param>
public sealed record BodyMotion(string File, int Start, int Count, int Faster = 1)
{
    /// <summary>How long one drawing is held when the server gives no speed — the pace a blow was always drawn at.</summary>
    public const double DefaultSecondsPerFrame = 0.14;

    public static readonly BodyMotion Blow = new("02", 0, 2);

    // skill.tbl 의 NO 0~17 차례 그대로 (FN · SI · FC).
    private static readonly BodyMotion[] Skills =
    [
        new("b", 0, 3),  // 0  성직자 시전
        new("c", 0, 4),  // 1  양손 공격
        new("c", 8, 3),  // 2  뛰기
        new("d", 0, 3),  // 3  발차기
        new("d", 6, 2),  // 4  주먹
        new("d", 10, 4), // 5  돌려차기
        new("e", 0, 2),  // 6  찌르기
        new("e", 4, 2),  // 7  두 번 찌르기
        new("f", 0, 2),  // 8  마법사 시전
        new("b", 6, 3),  // 9
        new("b", 12, 1), // 10
        new("c", 14, 2), // 11
        new("c", 18, 3), // 12
        new("c", 24, 3), // 13
        new("e", 8, 4),  // 14
        new("e", 16, 6), // 15
        new("e", 28, 4), // 16
        new("f", 4, 4)   // 17 소환
    ];

    public static BodyMotion? Of(int number) =>
        number switch
        {
            1 => Blow,
            6 => new BodyMotion("03", 0, 1),
            21 => new BodyMotion("03", 2, 2),
            22 => new BodyMotion("03", 6, 2, Faster: 3),
            >= 128 when number - 128 < Skills.Length => Skills[number - 128],
            _ => null
        };

    // skill.tbl 의 ST 칸 — 줄(NO)마다 그 기술 동작을 할 수 있는 옷(갑옷 U) 번호들. 처음 물을 때 한 번 읽는다.
    private static IReadOnlyDictionary<int, HashSet<int>>? _clothes;

    /// <summary>
    /// Whether a figure wearing this armour plays this motion. The original client (Legend.exe 2005 = 5.99, 0x4e1124..
    /// 0x4e1171; 4.51 0x4494b7) plays a skill motion only when the armour number is on that row's ST list in
    /// skill.tbl, and plays nothing otherwise — it does not fall back to the blow. The blow is played whatever is worn.
    /// </summary>
    public static bool Fits(int number, int armour)
    {
        if (number < 128)
        {
            return true;
        }

        _clothes ??= ReadClothes();
        return _clothes.TryGetValue(number - 128, out HashSet<int>? allowed) && allowed.Contains(armour);
    }

    /// <summary>skill.tbl rows are "NO FN SI FC ST…"; lines starting with ';' are the original developers' notes.</summary>
    private static Dictionary<int, HashSet<int>> ReadClothes()
    {
        Dictionary<int, HashSet<int>> rows = [];
        using Stream? stream = typeof(BodyMotion).Assembly.GetManifestResourceStream("skill.tbl");

        if (stream is null)
        {
            return rows;
        }

        using StreamReader reader = new(stream, Protocol.LegacyKoreanEncoding.Encoding);

        while (reader.ReadLine() is { } line)
        {
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 4 || !int.TryParse(parts[0], out int no))
            {
                continue;
            }

            rows[no] = [.. parts.Skip(4).Select(part => int.TryParse(part, out int clothes) ? clothes : -1).Where(clothes => clothes >= 0)];
        }

        return rows;
    }

    /// <summary>The drawing for this step, seen from this side. A step past the end holds the last drawing.</summary>
    public int Frame(Side side, int step) =>
        Start + (side == Side.Front ? Count : 0) + Math.Clamp(step, 0, Count - 1);

    /// <summary>
    /// How long each drawing is held. The speed is read as the whole motion in hundredths of a second — no file
    /// says so, but it puts Hades' blow (30) at the pace it was already drawn at, and a longer skill at a
    /// slower one. No speed at all falls back to that pace.
    /// </summary>
    public double SecondsPerFrame(int speed) =>
        (speed > 0 ? speed / 100.0 / Count : DefaultSecondsPerFrame) / Faster;
}
