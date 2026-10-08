using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>
/// 길 찾기 창 — 지도 아래 이 맵 NPC 이름 목록, 누르면 지도 위에 그 NPC 가 하는 일(파는 것·가르치는 것·체력 사기 …) 팝업과
/// [가기](사용자 2026-10-05). 하는 일은 <c>guide.txt</c> 의 <c>about</c> 줄(<c>build-client-guide.py</c>)이다.
/// </summary>
public sealed partial class TabMapPanel
{
    private readonly HFlowContainer _npcList = new();
    private readonly ScrollContainer _npcScroll = new() { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
    private readonly PanelContainer _npcCard = new() { Visible = false };
    private readonly Label _npcName = new();
    private readonly Label _npcAbout = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private int _npcListMap = -1;
    // [가기]가 누를 지도 위 자리 — 지도 크기가 바뀌어도 맞게 누를 때 셈한다.
    private System.Func<Vector2?>? _npcGoal;

    private Control BuildNpcList()
    {
        _npcList.AddThemeConstantOverride("h_separation", 4);
        _npcList.AddThemeConstantOverride("v_separation", 4);
        _npcList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _npcScroll.CustomMinimumSize = new Vector2(0, Main.Portrait ? 64 : 34);
        _npcScroll.AddChild(_npcList);
        return _npcScroll;
    }

    /// <summary>지도 아래쪽에 겹쳐 뜨는 판 — 이름 · [가기] · X, 그 아래 하는 일(길면 굴린다).</summary>
    private void BuildNpcCard()
    {
        StyleBoxFlat plate = Greybox.Plate();
        plate.BgColor = new Color(0.06f, 0.06f, 0.06f, 0.94f);
        plate.BorderColor = Greybox.Muted;
        plate.SetCornerRadiusAll(10);
        plate.SetContentMarginAll(8);
        _npcCard.AddThemeStyleboxOverride("panel", plate);
        _npcCard.AnchorLeft = 0;
        _npcCard.AnchorRight = 1;
        _npcCard.AnchorTop = 1;
        _npcCard.AnchorBottom = 1;
        _npcCard.OffsetLeft = 6;
        _npcCard.OffsetRight = -6;
        _npcCard.OffsetBottom = -6;
        _npcCard.GrowVertical = GrowDirection.Begin;
        _npcCard.MouseFilter = MouseFilterEnum.Stop;

        _npcName.AddThemeColorOverride("font_color", Greybox.Title);
        _npcName.AddThemeFontSizeOverride("font_size", 15);
        _npcName.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _npcAbout.AddThemeColorOverride("font_color", Greybox.Text);
        _npcAbout.AddThemeFontSizeOverride("font_size", 13);
        _npcAbout.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        Button go = new() { Text = "가기", CustomMinimumSize = new Vector2(56, 36), FocusMode = FocusModeEnum.None };
        Greybox.Commit(go);
        go.Pressed += () =>
        {
            if (_npcGoal?.Invoke() is { } spot)
            {
                TapAt(spot);
            }

            _npcCard.Visible = false;
        };
        Button shut = WindowFrame.CloseButton();
        shut.Pressed += () => _npcCard.Visible = false;

        HBoxContainer head = new();
        head.AddThemeConstantOverride("separation", Main.Gutter / 2);
        head.AddChild(_npcName);
        head.AddChild(go);
        head.AddChild(shut);

        ScrollContainer words = new()
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 96)
        };
        words.AddChild(_npcAbout);

        VBoxContainer column = new();
        column.AddThemeConstantOverride("separation", 4);
        column.AddChild(head);
        column.AddChild(words);
        _npcCard.AddChild(column);
        _canvas.AddChild(_npcCard);
    }

    /// <summary>맵이 바뀌면 목록을 새로 — 이 맵에 NPC 가 없으면 목록 칸을 숨긴다.</summary>
    private void KeepNpcList()
    {
        if (_npcListMap == _world.MapId)
        {
            return;
        }

        _npcListMap = _world.MapId;
        _npcCard.Visible = false;

        foreach (Node old in _npcList.GetChildren())
        {
            old.QueueFree();
        }

        IReadOnlyList<MapRoom> rooms = _guide.RoomsOn(_world.MapId);
        IReadOnlyList<MapSign> signs = _guide.SignsOn(_world.MapId);
        _npcScroll.Visible = rooms.Count + signs.Count > 0;

        // 건물(출구 너머 NPC)이 먼저 — 마을에서는 상점이 거의 다 건물 안이다.
        foreach (MapRoom room in rooms)
        {
            _npcList.AddChild(Chip(room.To, room.Kinds, () => ShowNpc(room.To, room.Kinds, room.About, () => PointOf(room.To))));
        }

        foreach (MapSign sign in signs)
        {
            _npcList.AddChild(Chip(sign.Name, [sign.Role], () => ShowNpc(sign.Name, [sign.Role], sign.About, () => At(sign.Where))));
        }
    }

    /// <summary>
    /// 목록 한 칸 — 앞에 역할 아이콘(지도·미니맵과 같은 것), 이름 뒤에 역할 낱말(「가이 · 무기」, 사용자 2026-10-08 「어느 npc가 뭐하는지」).
    /// 안내만 하는 NPC 는 낱말을 붙이지 않는다.
    /// </summary>
    private static Button Chip(string name, IReadOnlyList<NpcRole> roles, System.Action pressed)
    {
        NpcRole[] shown = [.. roles.Take(3)];
        string words = string.Join("·", shown.Where(role => role != NpcRole.Talk).Select(NpcRoles.Word));
        Button chip = new() { Text = words.Length > 0 ? $"{name} · {words}" : name, CustomMinimumSize = new Vector2(0, 28), FocusMode = FocusModeEnum.None };
        chip.SetMeta("name", name);
        Greybox.Plain(chip);
        chip.AddThemeFontSizeOverride("font_size", 12);

        // 글자는 아이콘 몫만큼 오른쪽에서 시작한다.
        foreach (string state in new[] { "normal", "hover", "focus", "pressed" })
        {
            if (chip.GetThemeStylebox(state) is StyleBoxFlat box)
            {
                box.ContentMarginLeft = 6 + (shown.Length * 17);
            }
        }

        for (int at = 0; at < shown.Length; at++)
        {
            chip.AddChild(new RoleBadge(shown[at]) { Position = new Vector2(5 + (at * 17), 6) });
        }

        chip.Pressed += pressed;
        return chip;
    }

    private Vector2 At(Tile where)
    {
        (float x, float y) = _canvas.Projection.Centre(where.X, where.Y);
        return new Vector2(x, y);
    }

    /// <summary>손 없이 — 그 이름을 목록에서 누른 것처럼(<c>--tabmap-npc</c>).</summary>
    public void PressNpc(string name)
    {
        if (_npcCard.Visible)
        {
            return;
        }

        foreach (Node child in _npcList.GetChildren())
        {
            if (child is Button chip && chip.GetMeta("name").AsString() == name)
            {
                chip.EmitSignal(BaseButton.SignalName.Pressed);
                return;
            }
        }
    }

    private void ShowNpc(string name, IReadOnlyList<NpcRole> roles, string about, System.Func<Vector2?> goal)
    {
        _npcGoal = goal;
        string titles = string.Join(" · ", roles.Where(role => role != NpcRole.Talk).Select(NpcRoles.Title));
        _npcName.Text = titles.Length > 0 ? $"{name} — {titles}" : name;
        _npcAbout.Text = about.Length > 0 ? about : "안내";
        _npcCard.Visible = true;
    }
}
