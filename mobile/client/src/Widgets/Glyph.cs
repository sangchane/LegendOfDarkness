using System;
using System.Linq;
using Godot;

namespace LodClient;

/// <summary>What a <see cref="Glyph" /> draws. Drawn with lines, not pictures — the original 4.51 has no icons for these.</summary>
public enum GlyphKind
{
    Close,
    Bag,
    Armor,
    Sort,
    Loot,
    Use,
    Drop,
    TakeOff,
    Auto,
    Bot,
    Zoom,
    Stop,
    Town,
    Field,
    Plus,
    Minus,

    /// <summary>엔터 키(↵) — [대화] 단추(2026-09-27).</summary>
    Enter,

    /// <summary>문틀 밖으로 나가는 화살표 — 파티 [나가기](2026-09-27).</summary>
    Leave,

    /// <summary>금이 간 보석 — 소지품 [분해](2026-10-08).</summary>
    Break
}

/// <summary>
/// A small line-drawn icon — the X in a window's corner, the icon tabs, the buttons of an item's action row. Like the
/// mock-up's <c>.pip</c> beads (docs/ui/mockups-451) it is drawn, not loaded, so it stays crisp at any size and takes its
/// colour from the 4.51 palette (<see cref="Greybox" />).
/// </summary>
public sealed partial class Glyph : Control
{
    private GlyphKind _kind;
    private Color _paint = Greybox.Title;

    public Glyph(GlyphKind kind, float size = 18)
    {
        _kind = kind;
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    public Color Paint
    {
        get => _paint;
        set
        {
            _paint = value;
            QueueRedraw();
        }
    }

    public GlyphKind Kind
    {
        get => _kind;
        set
        {
            _kind = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        float s = Math.Min(Size.X, Size.Y);
        Vector2 o = (Size - new Vector2(s, s)) / 2;
        Vector2 P(float x, float y) => o + new Vector2(x * s, y * s);
        float w = Math.Max(1.5f, s / 10);
        Color c = _paint;

        switch (_kind)
        {
            case GlyphKind.Close:
                DrawLine(P(0.2f, 0.2f), P(0.8f, 0.8f), c, w + 0.5f, true);
                DrawLine(P(0.8f, 0.2f), P(0.2f, 0.8f), c, w + 0.5f, true);
                break;

            case GlyphKind.Bag:
                DrawRect(new Rect2(P(0.18f, 0.36f), new Vector2(0.64f * s, 0.52f * s)), c, false, w);
                DrawArc(P(0.5f, 0.36f), 0.17f * s, Mathf.Pi, Mathf.Tau, 12, c, w, true);
                DrawLine(P(0.18f, 0.55f), P(0.82f, 0.55f), c, w * 0.8f, true);
                break;

            case GlyphKind.Armor:
                DrawPolyline([P(0.5f, 0.1f), P(0.85f, 0.24f), P(0.8f, 0.6f), P(0.5f, 0.9f), P(0.2f, 0.6f), P(0.15f, 0.24f), P(0.5f, 0.1f)], c, w, true);
                DrawLine(P(0.5f, 0.28f), P(0.5f, 0.72f), c, w * 0.8f, true);
                break;

            case GlyphKind.Sort:
                DrawLine(P(0.15f, 0.25f), P(0.85f, 0.25f), c, w, true);
                DrawLine(P(0.15f, 0.5f), P(0.65f, 0.5f), c, w, true);
                DrawLine(P(0.15f, 0.75f), P(0.45f, 0.75f), c, w, true);
                break;

            case GlyphKind.Loot:
                DrawLine(P(0.5f, 0.1f), P(0.5f, 0.6f), c, w, true);
                DrawPolyline([P(0.3f, 0.42f), P(0.5f, 0.62f), P(0.7f, 0.42f)], c, w, true);
                DrawPolyline([P(0.15f, 0.6f), P(0.15f, 0.88f), P(0.85f, 0.88f), P(0.85f, 0.6f)], c, w, true);
                break;

            case GlyphKind.TakeOff:
                DrawLine(P(0.5f, 0.62f), P(0.5f, 0.12f), c, w, true);
                DrawPolyline([P(0.3f, 0.32f), P(0.5f, 0.12f), P(0.7f, 0.32f)], c, w, true);
                DrawPolyline([P(0.15f, 0.6f), P(0.15f, 0.88f), P(0.85f, 0.88f), P(0.85f, 0.6f)], c, w, true);
                break;

            case GlyphKind.Use:
                DrawPolyline([P(0.15f, 0.52f), P(0.4f, 0.78f), P(0.86f, 0.22f)], c, w + 0.5f, true);
                break;

            case GlyphKind.Drop:
                DrawLine(P(0.15f, 0.25f), P(0.85f, 0.25f), c, w, true);
                DrawLine(P(0.4f, 0.12f), P(0.6f, 0.12f), c, w, true);
                DrawPolyline([P(0.24f, 0.25f), P(0.3f, 0.9f), P(0.7f, 0.9f), P(0.76f, 0.25f)], c, w, true);
                DrawLine(P(0.43f, 0.4f), P(0.44f, 0.76f), c, w * 0.7f, true);
                DrawLine(P(0.57f, 0.4f), P(0.56f, 0.76f), c, w * 0.7f, true);
                break;

            case GlyphKind.Break:
                DrawPolyline([P(0.5f, 0.12f), P(0.86f, 0.4f), P(0.5f, 0.9f), P(0.14f, 0.4f), P(0.5f, 0.12f)], c, w, true);
                DrawLine(P(0.14f, 0.4f), P(0.86f, 0.4f), c, w * 0.7f, true);
                DrawPolyline([P(0.56f, 0.12f), P(0.46f, 0.3f), P(0.58f, 0.52f), P(0.5f, 0.9f)], c, w * 0.7f, true);
                break;

            case GlyphKind.Auto:
                DrawArc(P(0.5f, 0.5f), 0.32f * s, -0.4f, Mathf.Pi * 1.55f, 20, c, w, true);
                DrawColoredPolygon([P(0.8f, 0.2f), P(0.86f, 0.46f), P(0.6f, 0.4f)], c);
                break;

            case GlyphKind.Bot:
                DrawRect(new Rect2(P(0.42f, 0.14f), new Vector2(0.16f * s, 0.72f * s)), c);
                DrawRect(new Rect2(P(0.2f, 0.34f), new Vector2(0.6f * s, 0.16f * s)), c);
                break;

            case GlyphKind.Zoom:
                DrawArc(P(0.42f, 0.42f), 0.26f * s, 0, Mathf.Tau, 18, c, w, true);
                DrawLine(P(0.62f, 0.62f), P(0.88f, 0.88f), c, w + 0.5f, true);
                break;

            case GlyphKind.Stop:
                DrawRect(new Rect2(P(0.25f, 0.25f), new Vector2(0.5f * s, 0.5f * s)), c);
                break;

            case GlyphKind.Town:
                // 지붕 있는 집.
                DrawPolyline([P(0.12f, 0.48f), P(0.5f, 0.15f), P(0.88f, 0.48f)], c, w, true);
                DrawPolyline([P(0.22f, 0.42f), P(0.22f, 0.86f), P(0.78f, 0.86f), P(0.78f, 0.42f)], c, w, true);
                DrawRect(new Rect2(P(0.42f, 0.6f), new Vector2(0.16f * s, 0.26f * s)), c);
                break;

            case GlyphKind.Plus:
                DrawLine(P(0.2f, 0.5f), P(0.8f, 0.5f), c, w + 0.5f, true);
                DrawLine(P(0.5f, 0.2f), P(0.5f, 0.8f), c, w + 0.5f, true);
                break;

            case GlyphKind.Minus:
                DrawLine(P(0.2f, 0.5f), P(0.8f, 0.5f), c, w + 0.5f, true);
                break;

            case GlyphKind.Enter:
                // 위에서 내려와 왼쪽으로 꺾이고, 끝에 왼쪽을 가리키는 촉 — 키보드의 엔터.
                DrawPolyline([P(0.78f, 0.15f), P(0.78f, 0.62f), P(0.2f, 0.62f)], c, w, true);
                DrawPolyline([P(0.4f, 0.42f), P(0.19f, 0.62f), P(0.4f, 0.82f)], c, w, true);
                break;

            case GlyphKind.Leave:
                // 왼쪽이 막힌 문틀과 그 밖으로 나가는 오른쪽 화살표.
                DrawPolyline([P(0.5f, 0.15f), P(0.15f, 0.15f), P(0.15f, 0.85f), P(0.5f, 0.85f)], c, w, true);
                DrawLine(P(0.36f, 0.5f), P(0.88f, 0.5f), c, w, true);
                DrawPolyline([P(0.68f, 0.3f), P(0.89f, 0.5f), P(0.68f, 0.7f)], c, w, true);
                break;

            case GlyphKind.Field:
                // 엇갈린 두 칼 — 싸우는 곳.
                DrawLine(P(0.18f, 0.18f), P(0.78f, 0.78f), c, w, true);
                DrawLine(P(0.82f, 0.18f), P(0.22f, 0.78f), c, w, true);
                DrawLine(P(0.62f, 0.86f), P(0.86f, 0.62f), c, w, true);
                DrawLine(P(0.14f, 0.62f), P(0.38f, 0.86f), c, w, true);
                break;
        }
    }
}
