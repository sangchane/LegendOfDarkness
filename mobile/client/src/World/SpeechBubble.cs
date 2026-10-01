using Godot;

namespace LodClient;

/// <summary>
/// What somebody said, over their head for a few seconds (0x0D) — the words go to the log as well. It sits on the
/// figure, so it walks with whoever said it, and above every figure, so the one standing in front does not hide it.
/// </summary>
public sealed partial class SpeechBubble : Node2D
{
    private const int FontSize = 12;

    /// <summary>How wide a line may get before it wraps — a little over three tiles.</summary>
    private const float Widest = 132;

    private const double StaysFor = 5;
    private const double FadesFor = 0.8;

    private readonly PanelContainer _plate = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly Label _words = new()
    {
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        HorizontalAlignment = HorizontalAlignment.Center,
        MaxLinesVisible = 3,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        MouseFilter = Control.MouseFilterEnum.Ignore
    };

    private double _age;

    public SpeechBubble()
    {
        ZIndex = 60;

        // 원작 말풍선(msgsm)은 밝은 돌이지만, 밝은 돌에 작은 글자를 얹으면 먼저 무너진다(docs/original-ui-451.md) —
        // 기록 칸과 같은 어두운 속에 밝은 글자.
        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = plate.BgColor with { A = 0.9f };
        plate.ContentMarginLeft = 6;
        plate.ContentMarginRight = 6;
        plate.ContentMarginTop = 2;
        plate.ContentMarginBottom = 2;
        plate.SetCornerRadiusAll(6);
        _plate.AddThemeStyleboxOverride("panel", plate);

        _words.AddThemeFontSizeOverride("font_size", FontSize);
        _words.AddThemeColorOverride("font_color", Greybox.Text);
        _plate.AddChild(_words);
        AddChild(_plate);
    }

    /// <summary>Shows new words, starting the clock again.</summary>
    public void Say(string words)
    {
        _words.Text = words;

        Font font = _words.GetThemeFont("font");
        float across = font.GetStringSize(words, HorizontalAlignment.Left, -1, FontSize).X + 2;
        _words.CustomMinimumSize = new Vector2(Mathf.Min(across, Widest), 0);

        // 한 줄에 들어가는 말은 줄바꿈을 끈다 — 켜 두면 폭을 재기 전의 높이(두 줄)가 남아 판이 헐렁해졌다.
        _words.AutowrapMode = across > Widest ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;

        _plate.ResetSize();
        _age = 0;
        Visible = true;
        Modulate = Colors.White;
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        // 크기는 글자를 잰 뒤에 정해지므로 늘 가운데·아래를 기준점에 맞춘다.
        _plate.Position = new Vector2(-_plate.Size.X / 2, -_plate.Size.Y);

        _age += delta;

        if (_age >= StaysFor + FadesFor)
        {
            Visible = false;
            return;
        }

        Modulate = new Color(1, 1, 1, 1f - (float)Math.Clamp((_age - StaysFor) / FadesFor, 0, 1));
    }
}
