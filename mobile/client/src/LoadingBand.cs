using Godot;

namespace LodClient;

/// <summary>
/// The original loading band, whole (작업지침 「통째로」, 사용자 2026-10-02): <c>lodmap</c> 「Loading Map」 while a map changes,
/// <c>lodusr</c> 「Loading ...」 while logging in — 304×76, in the middle of the screen, never enlarged (shrunk to fit a narrow
/// phone), the progress filled into the band's own groove. It takes no touches.
/// </summary>
public sealed partial class LoadingBand : CenterContainer
{
    private static readonly Vector2 Art = new(304, 76);

    // 그림 속 진행 홈(원작 lodmap·lodusr 같은 자리, 재어 둔 값).
    private static readonly Rect2 Groove = new(23, 35, 266, 10);

    private readonly TextureRect _band = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore
    };

    private readonly ColorRect _fill = new() { Color = new Color("#C8AA6E"), MouseFilter = MouseFilterEnum.Ignore };

    public LoadingBand(bool map)
    {
        Name = map ? "LoadingMap" : "LoadingUser";
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);

        string art = map ? "res://assets/ui/lodmap.png" : "res://assets/ui/lodusr.png";
        _band.Texture = ResourceLoader.Exists(art) ? GD.Load<Texture2D>(art) : null;
        _band.AddChild(_fill);
        AddChild(_band);
        Resized += Fit;
    }

    /// <summary>How far along, 0 to 1 — the groove fills from the left.</summary>
    public void Show(float done)
    {
        Visible = true;
        float scale = _band.Size.X / Art.X;
        _fill.Position = Groove.Position * scale;
        _fill.Size = new Vector2(Groove.Size.X * Mathf.Clamp(done, 0, 1), Groove.Size.Y) * scale;
    }

    private void Fit()
    {
        float scale = Mathf.Min(1, (Size.X - (Main.Gutter * 4)) / Art.X);
        _band.CustomMinimumSize = Art * scale;
    }
}
