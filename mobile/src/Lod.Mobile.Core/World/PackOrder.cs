namespace Lod.Mobile.Core.World;

/// <summary>
/// Tidying a pack. The server keeps no order of its own — it only swaps the two slots it is told to — so
/// whatever order there is, somebody asked for it. This works out which swaps close the gaps.
/// </summary>
public static class PackOrder
{
    /// <summary>
    /// The swaps that pull everything to the front, keeping the order it is already in. Each pair is the
    /// slot to move out of and the slot to move into, in the order they must be sent.
    /// </summary>
    public static IReadOnlyList<(int From, int To)> Tidy(IReadOnlyList<InventoryItem> carried)
    {
        // 어느 칸에 무엇이 있는지. 한 번 바꿀 때마다 따라 바꿔야 다음 계산이 맞는다.
        Dictionary<int, int> at = [];

        foreach (InventoryItem item in carried.OrderBy(item => item.Slot))
        {
            at[item.Slot] = item.Slot;
        }

        List<(int From, int To)> swaps = [];
        int wanted = 1;

        foreach (int slot in carried.Select(item => item.Slot).OrderBy(slot => slot))
        {
            int now = at[slot];

            if (now == wanted)
            {
                wanted++;
                continue;
            }

            swaps.Add((now, wanted));

            // 서버는 두 칸을 맞바꾼다. 저쪽에 있던 것이 이쪽으로 온다.
            foreach (int other in at.Keys.Where(key => at[key] == wanted).ToList())
            {
                at[other] = now;
            }

            at[slot] = wanted;
            wanted++;
        }

        return swaps;
    }
}
