using System;
using System.Collections.Generic;
using Godot;

namespace LodClient;

/// <summary>
/// One skill flash, played once and gone. The picture is a single row of frames cut from the original archive
/// (<c>scripts/build-client-effects.py</c>); how many frames it has is written beside it in <c>effects.txt</c>,
/// the same way a monster's frame ranges are, because the row alone does not say where one frame ends.
/// </summary>
public sealed partial class Flash : Sprite2D
{
    private const string Folder = "res://assets/effect/";

    private static Dictionary<int, int>? _frames;

    private readonly int _count;
    private readonly double _perFrame;
    private double _age;

    private Flash(Texture2D picture, int count, int speed)
    {
        Texture = picture;
        Hframes = Math.Max(1, count);
        _count = Math.Max(1, count);

        // 서버가 주는 속도는 한 프레임을 붙잡는 밀리초다(5.99 스크립트는 75·100 을 쓴다).
        _perFrame = Math.Clamp(speed, 30, 300) / 1000.0;

        // 누구의 발밑 정렬도 따르지 않고 늘 위에 그린다.
        ZIndex = 100;
    }

    /// <summary>The flash with this number, or nothing when it was never cut.</summary>
    public static Flash? Make(int number, int speed)
    {
        _frames ??= Read();

        string path = $"{Folder}efct{number:000}.png";

        if (!_frames.TryGetValue(number, out int count) || !ResourceLoader.Exists(path))
        {
            return null;
        }

        return new Flash(GD.Load<Texture2D>(path), count, speed);
    }

    public override void _Process(double delta)
    {
        _age += delta;

        int frame = (int)(_age / _perFrame);

        if (frame >= _count)
        {
            QueueFree();
            return;
        }

        Frame = frame;
    }

    private static Dictionary<int, int> Read()
    {
        Dictionary<int, int> frames = [];
        string path = $"{Folder}effects.txt";

        if (!Godot.FileAccess.FileExists(path))
        {
            return frames;
        }

        foreach (string line in Godot.FileAccess.GetFileAsString(path).Split('\n'))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 2 && int.TryParse(parts[0], out int number) && int.TryParse(parts[1], out int count))
            {
                frames[number] = count;
            }
        }

        return frames;
    }
}
