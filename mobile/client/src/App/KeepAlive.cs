using Godot;

namespace LodClient;

/// <summary>
/// 앱이 뒤로 가도 자동 사냥이 이어지게(사용자 2026-10-05 「앱을 뒤에서 살려 두기」) — 아이폰은 뒤로 간 앱을 몇 초 안에 멈추지만
/// 소리를 내는 앱은 멈추지 않는다(Info.plist UIBackgroundModes audio · project.godot 의 iOS 세션 Playback + 다른 소리와 섞기).
/// 그래서 1초짜리 무음을 끝없이 돌린다. 뒤에 있는 동안 10초마다 한 줄 남긴다 — 그 줄이 이어지면 게임 루프도 돌고 있는 것이다
/// (`scripts/ops/ios-build.sh logs`).
/// </summary>
/// <remarks>ponytail: 무음 재생으로 살려 두기 — 앱을 완전히 닫으면 멈춘다. 그것까지 바라면 서버가 대신 사냥하는 쪽으로.</remarks>
public sealed partial class KeepAlive : Node
{
    private const int Rate = 8000;
    private const double TellEvery = 10;

    private readonly AudioStreamPlayer _silence = new() { Name = "Silence" };
    private bool _away;
    private double _awayFor;
    private double _sinceTold;

    public override void _Ready()
    {
        if (OS.GetName() != "iOS")
        {
            return;
        }

        AudioStreamWav quiet = new()
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = Rate,
            Data = new byte[Rate * 2],
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopEnd = Rate
        };

        _silence.Stream = quiet;
        AddChild(_silence);
        _silence.Play();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationApplicationPaused)
        {
            _away = true;
            _awayFor = 0;
            _sinceTold = 0;
            GD.Print("뒤로 감 — 무음 재생으로 살려 둔다");
        }
        else if (what == NotificationApplicationFocusIn || what == NotificationApplicationResumed)
        {
            if (_away)
            {
                GD.Print($"앞으로 옴 — 뒤에서 {_awayFor:0}초");
            }

            _away = false;

            if (_silence.Stream is not null && !_silence.Playing)
            {
                _silence.Play();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!_away)
        {
            return;
        }

        _awayFor += delta;
        _sinceTold += delta;

        if (_sinceTold >= TellEvery)
        {
            _sinceTold = 0;
            GD.Print($"뒤에서 도는 중 — {_awayFor:0}초");
        }
    }
}
