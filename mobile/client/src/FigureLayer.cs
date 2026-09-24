using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// The damage and heal numbers that float up over whoever was struck or healed (0x5D, our server's own packet —
/// the original only sends a percentage). They start just above the bar and badges (<see cref="FloatingFigure" />),
/// rise and fade in under a second: ours in yellow, somebody else's in white, a blow on us in red, a heal in green
/// with "+".
/// </summary>
/// <remarks>
/// One node draws every number, rather than a label per number: a pack of thirty monsters being hit would
/// otherwise be dozens of nodes to lay out every frame. It only redraws while a number is alive.
/// </remarks>
public sealed partial class FigureLayer : Node2D
{
    /// <summary>
    /// The colours: our blow in the theme's "worn" amber-yellow, anyone else's in the dark-stone text white, a blow
    /// on us in the bar's "dying" red lightened for a dark floor, a heal in green.
    /// </summary>
    private static readonly Color Dealt = new("#ffd84a");

    private static readonly Color Seen = new("#ece8dc");
    private static readonly Color Taken = new("#ff5a48");
    private static readonly Color Healed = new("#72e07e");
    private static readonly Color Outline = new(0f, 0f, 0f, 0.9f);

    private const int OutlineSize = 4;

    private readonly List<Shown> _shown = [];

    private Font? _font;

    private sealed class Shown
    {
        public required Node2D On;
        public required string Text;
        public required Color Paint;
        public required int Size;
        public required float Start;
        public Vector2 Last;
        public double Age;
    }

    public FigureLayer()
    {
        Name = "Figures";

        // Flashes sit at 100; the numbers are over them, so a head effect never hides what a blow took.
        ZIndex = 110;
    }

    /// <summary>Puts one number over <paramref name="on" />, starting at <paramref name="start" /> from its feet.</summary>
    public void Add(Node2D on, float start, Figure figure, uint self)
    {
        FigureTone tone = FloatingFigure.Tone(figure, self);

        // 같은 이에게 방금 뜬 숫자가 있으면 한 줄 위에서 시작한다 — 두 방이 겹쳐 찍히지 않게.
        int crowd = 0;

        foreach (Shown one in _shown)
        {
            if (one.On == on && one.Age < FloatingFigure.Crowded)
            {
                crowd++;
            }
        }

        _shown.Add(new Shown
        {
            On = on,
            Text = FloatingFigure.Text(figure),
            Paint = tone switch
            {
                FigureTone.Dealt => Dealt,
                FigureTone.Taken => Taken,
                FigureTone.Healed => Healed,
                _ => Seen,
            },
            // 내가 준·받은 것은 크게, 남의 싸움은 작게 — 서른 마리 사냥터에서 내 숫자가 묻히지 않게.
            Size = tone == FigureTone.Seen ? 13 : 16,
            Start = start - crowd * FloatingFigure.Line,
            Last = on.Position,
        });

        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_shown.Count == 0)
        {
            return;
        }

        for (int index = _shown.Count - 1; index >= 0; index--)
        {
            Shown one = _shown[index];
            one.Age += delta;

            if (one.Age >= FloatingFigure.Seconds)
            {
                _shown.RemoveAt(index);
                continue;
            }

            // 걷는 이를 따라간다. 사라진 이의 숫자는 마지막 자리에서 마저 뜬다.
            if (IsInstanceValid(one.On))
            {
                one.Last = one.On.Position;
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        _font ??= Numbers();

        foreach (Shown one in _shown)
        {
            (float lift, float alpha) = FloatingFigure.At(one.Age);
            float width = _font.GetStringSize(one.Text, HorizontalAlignment.Left, -1, one.Size).X;
            Vector2 at = one.Last + new Vector2(-Mathf.Round(width / 2), Mathf.Round(one.Start - lift));

            DrawStringOutline(_font, at, one.Text, HorizontalAlignment.Left, -1, one.Size, OutlineSize,
                new Color(Outline, Outline.A * alpha));
            DrawString(_font, at, one.Text, HorizontalAlignment.Left, -1, one.Size, new Color(one.Paint, alpha));
        }
    }

    /// <summary>
    /// The engine's own face with fixed-width figures ("tnum"): numbers are fixed width in this theme
    /// (docs/original-ui-451.md), so a count does not wobble. Only digits and "+" are drawn — no Hangul needed.
    /// </summary>
    private static Font Numbers()
    {
        // 조금 굵게 — 풀밭·나무껍질 위에서도 가는 획이 먹히지 않게.
        FontVariation face = new() { BaseFont = ThemeDB.FallbackFont, VariationEmbolden = 0.6f };
        face.OpentypeFeatures = new Godot.Collections.Dictionary
        {
            { TextServerManager.GetPrimaryInterface().NameToTag("tnum"), 1 },
        };

        return face;
    }
}
