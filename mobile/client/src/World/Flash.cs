using System;
using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;

namespace LodClient;

/// <summary>
/// One skill flash, played once and gone. The picture is a single row of frames cut from the original archive
/// (<c>scripts/gen/client/build-client-effects.py</c>); how many frames it has, and the order the original plays them in
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

        // 바탕의 기준점이 맞는 쪽의 칸(발에서 EffectSheet.AnchorFromFeet)에 온다 — 가운데를 맞추면 그림마다 어긋났다.
        Centered = false;
        Offset = new Vector2(-sheet.AnchorX, -sheet.AnchorY);

        // 서버가 주는 속도는 한 칸을 붙잡는 간격이다 — 원작은 칸마다 타이머를 다시 건다(4.51 0x483870).
        // 단위는 밀리초로 읽는다(5.99 스크립트는 75·100 을 쓴다).
        _perFrame = EffectSheet.SecondsPerStep(speed);

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
    /// one's drawn head (<paramref name="headTop" />, from the feet); anything else puts its anchor at
    /// <see cref="EffectSheet.AnchorFromFeet" />.
    /// </summary>
    public void Land(Vector2 feet, float? headTop) =>
        Position = OnHead && headTop is { } head
            ? feet + new Vector2(0, Mathf.Round(Overhead.Shift(_sheet, _drawnBottom, head)))
            : feet + new Vector2(EffectSheet.AnchorFromFeet.X, EffectSheet.AnchorFromFeet.Y);

    private static IReadOnlyDictionary<int, EffectLook>? _looks;

    /// <summary>
    /// 어느 칸이든 그려진 가장 아래 줄 — 생성기가 미리 잰 것(<c>effects-look.txt</c>). 없으면 맨 아래(머리 이펙트로 치지 않는다).
    /// </summary>
    private static int DrawnBottom(int number, Texture2D picture) =>
        Look(number)?.Bottom ?? picture.GetHeight() - 1;

    /// <summary>
    /// What a monster under a spell is tinted with: the colour of the picture that spell drew on it, half-way from
    /// white so the monster is still itself — 프라보's 257 is red, so a cursed monster turns reddish. White when the
    /// picture was never cut.
    /// </summary>
    /// <remarks>
    /// No original evidence for the tint itself: the 5.99 client can recolour a monster only through the four
    /// palette bytes of its 0x07 record (<c>0x63f4bb</c> → <c>0x59d770</c> → drawn by <c>0x495100</c> when the
    /// monster's own table allows it), and the 5.99 server always writes those four as zero. The colour is the
    /// picture's own, worked out by scripts/gen/client/build-client-effects.py (every drawn pixel weighted by how vivid it is).
    /// </remarks>
    public static Color Tint(int number) =>
        number > 0 && Look(number) is { } look ? Color.Color8(look.Red, look.Green, look.Blue) : Colors.White;

    private static EffectLook? Look(int number)
    {
        if (_looks is null)
        {
            string path = $"{Folder}effects-look.txt";
            _looks = Godot.FileAccess.FileExists(path)
                ? EffectLook.Read(Godot.FileAccess.GetFileAsString(path))
                : new Dictionary<int, EffectLook>();
        }

        return _looks.TryGetValue(number, out EffectLook? look) ? look : null;
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
