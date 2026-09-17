namespace Lod.Mobile.Core.World;

/// <summary>
/// Splitting a list into pages a phone screen can hold — the pack's pictures, the skills round the attack button. Pages
/// turn round at either end, and a page that is gone (things used up, skills forgotten) falls back to the last one left.
/// </summary>
public static class Paging
{
    public static int Pages(int count, int perPage) => Math.Max(1, (count + perPage - 1) / perPage);

    public static int Kept(int page, int count, int perPage) => Math.Clamp(page, 0, Pages(count, perPage) - 1);

    public static int After(int page, int count, int perPage) => (Kept(page, count, perPage) + 1) % Pages(count, perPage);

    public static int Before(int page, int count, int perPage) =>
        (Kept(page, count, perPage) + Pages(count, perPage) - 1) % Pages(count, perPage);

    /// <summary>What is on a page, in order, padded with nothing so every page has the same number of places.</summary>
    public static IReadOnlyList<T?> Page<T>(IReadOnlyList<T> all, int page, int perPage) where T : class
    {
        int first = Kept(page, all.Count, perPage) * perPage;

        return [.. Enumerable.Range(first, perPage).Select(index => index < all.Count ? all[index] : null)];
    }
}
