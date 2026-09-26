namespace Lod.Mobile.Core.World;

/// <summary>코마디움 칸을 눌렀을 때 할 일.</summary>
public enum ComaUse
{
    /// <summary>나도 봇도 혼수가 아니다.</summary>
    NotComatose,

    /// <summary>내 가방의 엑스코마디움을 쓴다(<see cref="ComaChoice.Slot" />).</summary>
    UseOnSelf,

    /// <summary>내 코마디움으로 봇을 깨운다(서버 0xF1 4 — 봇 바로 옆에서).</summary>
    WakeBot,

    /// <summary>쓸 것이 없다 — <see cref="ComaChoice.Why" /> 를 알린다.</summary>
    Missing,
}

public sealed record ComaChoice(ComaUse Use, int Slot = 0, string Why = "");

/// <summary>
/// 코마디움 칸의 규칙 — 원작 5.99 <c>Item/Potion.txt</c> 그대로:
/// <list type="bullet">
/// <item>코마디움은 **앞에 선 혼수인 사람**에게 쓴다(<c>get_front_char</c>), 쓰는 이가 혼수면 듣지 않는다(<c>get_state(@myid) != 0</c>).</item>
/// <item>엑스코마디움은 **혼수인 제게** 쓴다(<c>get_coma(@myid) == 1</c>).</item>
/// </list>
/// 그래서 내가 혼수면 엑스코마디움, 봇이 혼수면 코마디움으로 봇을 깨운다. 둘 다 풀리면 체력·마력 1000.
/// </summary>
public static class ComaChip
{
    public const string Comadium = "코마디움";
    public const string ExComadium = "엑스코마디움";

    /// <summary>칸 그림 — 코마디움 템플릿의 DisplayImage.</summary>
    public const int Icon = 32814;

    public static ComaChoice Choose(bool selfComatose, bool botComatose, IReadOnlyList<InventoryItem> pack)
    {
        if (selfComatose)
        {
            return pack.FirstOrDefault(one => one.Name == ExComadium) is { } ex
                ? new ComaChoice(ComaUse.UseOnSelf, ex.Slot)
                : new ComaChoice(ComaUse.Missing, Why: "혼수인 내게는 엑스코마디움만 듣습니다(원작) — 가방에 없습니다.");
        }

        if (botComatose)
        {
            return pack.Any(one => one.Name == Comadium)
                ? new ComaChoice(ComaUse.WakeBot)
                : new ComaChoice(ComaUse.Missing, Why: "코마디움이 없습니다.");
        }

        return new ComaChoice(ComaUse.NotComatose, Why: "혼수 상태가 아닙니다.");
    }

    /// <summary>칸에 적는 수 — 코마디움과 엑스코마디움을 함께.</summary>
    public static int Count(IReadOnlyList<InventoryItem> pack) =>
        AutoPotion.Count(pack, Comadium) + AutoPotion.Count(pack, ExComadium);
}
