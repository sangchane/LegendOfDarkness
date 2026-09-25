namespace Lod.Mobile.Core.Art;

/// <summary>
/// One effect's drawings, where they sit, and the order they are shown in. The picture is a row of
/// <see cref="Frames" /> cells, each one the whole canvas the original drew that effect on; the original plays
/// them in the order effect.tbl gives for that effect number (203 is "0 1 1"), one step per interval the server
/// asks for. With no order written, every drawing is shown once in turn.
/// </summary>
/// <remarks>
/// <see cref="AnchorX" />/<see cref="AnchorY" /> is the point of the canvas that lands on whoever the effect was
/// cast at. It is what keeps an effect the size and place the original gave it: 일음지 (efct042) is a 13x13
/// sparkle high up a 111x85 canvas, so anchored at 55,70 it plays above the head. Cut out on its own and scaled
/// to a body it covered the whole target instead — which is what the screen did before the canvas was kept
/// (scripts/build-client-effects.py, docs/disassembly.md 4.51 0x44ba04).
/// </remarks>
public sealed record EffectSheet(int Frames, int Wide, int Tall, int AnchorX, int AnchorY, IReadOnlyList<int> Order)
{
    /// <summary>
    /// Where the anchor goes, from the feet of the figure it lands on. Not on the feet: the original lays an effect
    /// canvas on the figure the way it lays a weapon's 111x85 canvas on the 57x85 wardrobe canvas — centred, same
    /// top — and every EPF effect keeps its anchor at the canvas middle, 15 above the bottom (effects.txt: 55,70 on
    /// 111x85, 28,70 on 57x85, 55,97 on 111x112). So the anchor is wardrobe point (28,70), which the wardrobe cell
    /// puts at (30,72) (dat-extract pose draws the canvas 2,2 in) against feet at (31.5,83) (Actor.Sheet.Walk).
    /// On the feet it was 11 too low: 쿠로토's ring turned round the knees, not the body (사용자, 2026-09-24).
    /// </summary>
    public static readonly (float X, float Y) AnchorFromFeet = (30 - 31.5f, 72 - 83f);

    /// <summary>
    /// How long each step is held. The server's speed is that interval in milliseconds (the original re-arms its timer
    /// per frame, 4.51 0x483870), kept to 30~300. 쿠로토's slower ring is the server's to say (Pack599 쿠로토, 117).
    /// </summary>
    public static double SecondsPerStep(int speed) => Math.Clamp(speed, 30, 300) / 1000.0;

    /// <summary>How many steps the effect lasts.</summary>
    public int Steps => Order.Count > 0 ? Order.Count : Frames;

    /// <summary>The drawing to show at this step, kept inside the sheet.</summary>
    public int FrameAt(int step)
    {
        int frame = Order.Count > 0 ? Order[Math.Clamp(step, 0, Order.Count - 1)] : step;
        return Math.Clamp(frame, 0, Math.Max(0, Frames - 1));
    }

    /// <summary>
    /// Reads what scripts/build-client-effects.py writes: "number drawings wide tall anchorX anchorY order…"
    /// per line, '#' lines are notes.
    /// </summary>
    public static IReadOnlyDictionary<int, EffectSheet> Read(string text)
    {
        Dictionary<int, EffectSheet> sheets = [];

        foreach (string line in text.Split('\n'))
        {
            if (line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            int[] numbers = [.. line.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => int.TryParse(part, out int value) ? value : -1)];

            // A row without its canvas and anchor cannot be placed, so it is passed over rather than drawn
            // somewhere invented.
            if (numbers.Length < 6 || numbers.Any(value => value < 0))
            {
                continue;
            }

            sheets[numbers[0]] = new EffectSheet(
                numbers[1], numbers[2], numbers[3], numbers[4], numbers[5], numbers[6..]);
        }

        return sheets;
    }
}
