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

    private Flash(Texture2D picture, EffectSheet sheet, int speed)
    {
        Texture = picture;
        Hframes = Math.Max(1, sheet.Frames);
        _sheet = sheet;

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

        return new Flash(GD.Load<Texture2D>(path), sheet, speed);
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
