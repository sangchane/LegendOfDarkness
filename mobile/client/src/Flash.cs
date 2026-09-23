using System;
using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// One skill flash, played once and gone. The picture is a single row of frames cut from the original archive
/// (<c>scripts/build-client-effects.py</c>); how many frames it has, and the order the original plays them in
/// (effect.tbl — 203 is "0 1 1"), are written beside it in <c>effects.txt</c>.
/// </summary>
public sealed partial class Flash : Sprite2D
{
    private const string Folder = "res://assets/effect/";

    private static IReadOnlyDictionary<int, EffectSheet>? _sheets;

    private readonly EffectSheet _sheet;
    private readonly double _perFrame;
    private double _age;

    /// <summary>
    /// Whether this is a head effect (<see cref="Overhead.IsHeadClass" />) — Miss, 일음지, the coma — which plays in
    /// the slot just over the head rather than where the sheet's anchor would put it.
    /// </summary>
    public bool OnHead { get; }

    /// <summary>The lowest drawn row of the sheet, in cell pixels, for <see cref="Overhead.Shift" />.</summary>
    private readonly int _drawnBottom;

    private Flash(Texture2D picture, EffectSheet sheet, int speed, int drawnBottom)
    {
        Texture = picture;
        Hframes = Math.Max(1, sheet.Frames);
        _sheet = sheet;
        _drawnBottom = drawnBottom;
        OnHead = Overhead.IsHeadClass(sheet, drawnBottom);

        // 바탕의 기준점이 맞는 쪽의 발밑(칸)에 온다 — 원작이 그렇게 놓고, 머리 위 반짝임(efct042)이나
        // 머리 위 「Miss」(efct033·115)가 제 높이에 서는 것도 이 때문이다. 가운데를 맞추면 그림마다 어긋났다.
        Centered = false;
        Offset = new Vector2(-sheet.AnchorX, -sheet.AnchorY);

        // 서버가 주는 속도는 한 칸을 붙잡는 간격이다 — 원작은 칸마다 타이머를 다시 건다(4.51 0x483870).
        // 단위는 밀리초로 읽는다(5.99 스크립트는 75·100 을 쓴다).
        _perFrame = Math.Clamp(speed, 30, 300) / 1000.0;

        // 누구의 발밑 정렬도 따르지 않고 늘 위에 그린다.
        ZIndex = 100;
    }

    /// <summary>The flash with this number, or nothing when it was never cut.</summary>
    public static Flash? Make(int number, int speed)
    {
        _sheets ??= Read();

        string path = $"{Folder}efct{number:000}.png";

        if (!_sheets.TryGetValue(number, out EffectSheet? sheet) || !ResourceLoader.Exists(path))
        {
            return null;
        }

        Texture2D picture = GD.Load<Texture2D>(path);

        return new Flash(picture, sheet, speed, DrawnBottom(number, picture));
    }

    /// <summary>
    /// Puts the effect on somebody whose feet are at <paramref name="feet" />. A head effect sits just over that
    /// one's drawn head (<paramref name="headTop" />, from the feet); anything else stays where its anchor puts it.
    /// </summary>
    public void Land(Vector2 feet, float? headTop) =>
        Position = OnHead && headTop is { } head
            ? feet + new Vector2(0, Mathf.Round(Overhead.Shift(_sheet, _drawnBottom, head)))
            : feet;

    private static readonly Dictionary<int, int> Bottoms = [];

    /// <summary>The lowest row anything is drawn on in any frame — measured once per effect, as reading back is slow.</summary>
    private static int DrawnBottom(int number, Texture2D picture)
    {
        if (Bottoms.TryGetValue(number, out int known))
        {
            return known;
        }

        int bottom = picture.GetHeight() - 1;

        if (picture.GetImage() is { } image)
        {
            if (image.IsCompressed())
            {
                image.Decompress();
            }

            bottom = -1;

            for (int y = image.GetHeight() - 1; y >= 0 && bottom < 0; y--)
            {
                for (int x = 0; x < image.GetWidth(); x++)
                {
                    if (image.GetPixel(x, y).A > 0.1f)
                    {
                        bottom = y;
                        break;
                    }
                }
            }

            // 빈 그림은 머리 이펙트로 치지 않는다.
            bottom = bottom < 0 ? image.GetHeight() - 1 : bottom;
        }

        Bottoms[number] = bottom;
        return bottom;
    }

    private static readonly Dictionary<int, Color> Tints = [];

    /// <summary>
    /// What a monster under a spell is tinted with: the colour of the picture that spell drew on it, half-way from
    /// white so the monster is still itself — 프라보's 257 is red, so a cursed monster turns reddish. White when the
    /// picture was never cut.
    /// </summary>
    /// <remarks>
    /// No original evidence for the tint itself: the 5.99 client can recolour a monster only through the four
    /// palette bytes of its 0x07 record (<c>0x63f4bb</c> → <c>0x59d770</c> → drawn by <c>0x495100</c> when the
    /// monster's own table allows it), and the 5.99 server always writes those four as zero. The colour is the
    /// picture's own: every drawn pixel weighted by how vivid it is, so the dark edges and grey smoke do not wash it
    /// out.
    /// </remarks>
    public static Color Tint(int number)
    {
        if (Tints.TryGetValue(number, out Color known))
        {
            return known;
        }

        string path = $"{Folder}efct{number:000}.png";
        Color tint = Colors.White;

        if (number > 0 && ResourceLoader.Exists(path) && GD.Load<Texture2D>(path).GetImage() is { } image)
        {
            if (image.IsCompressed())
            {
                image.Decompress();
            }

            float red = 0, green = 0, blue = 0, weight = 0;

            // 큰 그림도 있다(257 은 4800x180) — 둘째 칸마다 본다. 색을 고르는 데는 충분하다.
            for (int y = 0; y < image.GetHeight(); y += 2)
            {
                for (int x = 0; x < image.GetWidth(); x += 2)
                {
                    Color pixel = image.GetPixel(x, y);

                    if (pixel.A <= 0)
                    {
                        continue;
                    }

                    float vivid = pixel.S * pixel.V;
                    red += pixel.R * vivid;
                    green += pixel.G * vivid;
                    blue += pixel.B * vivid;
                    weight += vivid;
                }
            }

            if (weight > 0)
            {
                Color mean = new(red / weight, green / weight, blue / weight);
                float top = Mathf.Max(mean.R, Mathf.Max(mean.G, mean.B));
                tint = Colors.White.Lerp(top > 0 ? mean / top : Colors.White, 0.5f);
                tint.A = 1;
            }
        }

        Tints[number] = tint;
        return tint;
    }

    public override void _Process(double delta)
    {
        _age += delta;

        int step = (int)(_age / _perFrame);

        if (step >= _sheet.Steps)
        {
            QueueFree();
            return;
        }

        Frame = _sheet.FrameAt(step);
    }

    private static IReadOnlyDictionary<int, EffectSheet> Read()
    {
        string path = $"{Folder}effects.txt";

        return Godot.FileAccess.FileExists(path)
            ? EffectSheet.Read(Godot.FileAccess.GetFileAsString(path))
            : new Dictionary<int, EffectSheet>();
    }
}
