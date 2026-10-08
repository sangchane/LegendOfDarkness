using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 파티원·봇 타일 하나(WoW "그리드" 식, 2026-09-27): 체력이 곧 타일 바탕(채워진 만큼 체력 색, 15% 이하 빨강, 쓰러지면 회색),
/// 그 위 가운데 이름, 그룹장이면 왼쪽 위에 작은 별, 오른쪽 위 구석에 상태 아이콘 셋까지(아주 작게, 해로운 것은 빨간 테두리), 타일 아래 마력 한 줄.
/// 체력을 모르면(아직 알림 전) 바탕은 비고 이름만 — 쓰러진 것(회색으로 가득)과 구별된다.
/// </summary>
public sealed partial class GridTile : VBoxContainer
{
    private static readonly Color Dead = new("#5c5c58");

    private readonly ProgressBar _health = new() { MaxValue = 100, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
    private readonly ProgressBar _mana = new() { MaxValue = 100, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _name = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        ClipText = true,
        TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        MouseFilter = MouseFilterEnum.Ignore
    };
    private readonly Label _star = new() { Text = "★", MouseFilter = MouseFilterEnum.Ignore, Visible = false };
    private readonly StatusStrip _status = new(side: 6, most: 3, timed: false);

    public GridTile()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 0);

        _health.CustomMinimumSize = new Vector2(PartyColumn.TileWide, PartyColumn.TileTall);
        StyleBoxFlat ground = Greybox.Plate();
        ground.SetContentMarginAll(0);
        _health.AddThemeStyleboxOverride("background", ground);

        _mana.CustomMinimumSize = new Vector2(PartyColumn.TileWide, PartyColumn.ManaTall);
        _mana.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("#0f0f0f") });
        _mana.AddThemeStyleboxOverride("fill", Greybox.Fill(Greybox.Mana));

        _name.AddThemeFontSizeOverride("font_size", Greybox.TightText);
        _name.AddThemeColorOverride("font_outline_color", new Color("#030303"));
        _name.AddThemeConstantOverride("outline_size", 2);
        _name.SetAnchorsPreset(LayoutPreset.FullRect);
        _name.OffsetLeft = 3;
        _name.OffsetRight = -3;
        // 위 구석(별·상태 아이콘)을 비켜 조금 아래로.
        _name.OffsetTop = 5;
        _health.AddChild(_name);

        _star.AddThemeFontSizeOverride("font_size", 8);
        _star.AddThemeColorOverride("font_color", Greybox.Title);
        _star.AddThemeColorOverride("font_outline_color", new Color("#030303"));
        _star.AddThemeConstantOverride("outline_size", 2);
        _star.Position = new Vector2(2, -2);
        _health.AddChild(_star);

        _status.MouseFilter = MouseFilterEnum.Ignore;
        _status.SetAnchorsPreset(LayoutPreset.TopRight);
        _status.GrowHorizontal = GrowDirection.Begin;
        _health.AddChild(_status);

        AddChild(_health);
        AddChild(_mana);
    }

    public void Show(string name, bool leader, int? health, int? mana, IReadOnlyList<StatusBadge> statuses)
    {
        _name.Text = name;
        _star.Visible = leader;

        bool dead = health is 0;
        _health.Value = dead ? 100 : health ?? 0;
        _health.AddThemeStyleboxOverride("fill", Greybox.Fill(dead ? Dead : health <= 15 ? Greybox.Gone : Greybox.Health));
        _name.AddThemeColorOverride("font_color", leader ? Greybox.Title : Greybox.Text);

        _mana.Value = mana ?? 0;
        _mana.Modulate = mana is null ? Colors.Transparent : Colors.White;
        _status.Show(statuses);
    }
}

/// <summary>
/// What a party member's frame shows — health and mana %, when known, what is on them (icons only), and the exact numbers
/// when the server sends them (0x5E 종류 6 꼬리, 2026-09-27 — 타일에는 적지 않고 안내에만).
/// </summary>
public sealed record MemberLook(int? Health, int? Mana, IReadOnlyList<StatusBadge> Statuses, VitalNumbers? Numbers = null);
