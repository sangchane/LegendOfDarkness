using System.Linq;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// 코마디움 칸 — 자동 포션 두 칸과 같은 크기로 기술 부채꼴 왼쪽 위(<see cref="AbilityFan.Coma" />). 코마디움 그림에 가방의
/// 코마디움·엑스코마디움 수를 작게 적고, 없으면 흐리게(봇이 혼수면 흐리지 않는다). 누르면 내가 혼수면 엑스코마디움을 쓰고, 아니고
/// 봇이 혼수면 봇을 깨운다 — 코마디움 없이(<see cref="ComaChip" />). 둘 다 아니면 "혼수 상태가 아닙니다".
/// </summary>
public sealed partial class ComaButton : Button
{
    private readonly System.Func<WorldClient?> _server;
    private readonly System.Action<string> _notify;
    private readonly Label _count = new();
    private int _shown = -1;

    public ComaButton(System.Func<WorldClient?> server, System.Action<string> notify)
    {
        _server = server;
        _notify = notify;
        Name = "Coma";
        ExpandIcon = true;
        IconAlignment = HorizontalAlignment.Center;
        Icon = ItemIcons.For(ComaChip.Icon);
        TooltipText = "코마디움";
        Greybox.Plain(this);

        _count.AddThemeFontSizeOverride("font_size", 11);
        _count.AddThemeColorOverride("font_outline_color", Colors.Black);
        _count.AddThemeConstantOverride("outline_size", 4);
        _count.HorizontalAlignment = HorizontalAlignment.Right;
        _count.VerticalAlignment = VerticalAlignment.Bottom;
        _count.MouseFilter = MouseFilterEnum.Ignore;
        _count.SetAnchorsPreset(LayoutPreset.FullRect);
        _count.OffsetRight = -3;
        _count.OffsetBottom = -1;
        AddChild(_count);

        Pressed += Use;
    }

    public override void _Process(double delta)
    {
        // 봇이 혼수면 코마디움이 없어도 눌린다(봇은 아무것도 쓰지 않고 깨운다) — 그때는 흐리지 않는다.
        WorldClient? world = _server();
        int count = world is null ? 0 : ComaChip.Count(world.Pack);
        bool botDown = world?.Companion is { } tie && world.AilmentsOf(tie.Serial).Any(one => one.Icon == Overhead.ComaIcon);
        int shown = botDown ? count + 100_000 : count;

        if (shown == _shown)
        {
            return;
        }

        _shown = shown;
        _count.Text = count > 0 ? count.ToString() : string.Empty;
        Modulate = count > 0 || botDown ? Colors.White : new Color(1, 1, 1, 0.4f);
    }

    private void Use()
    {
        if (_server() is not { } world)
        {
            _notify("혼수 상태가 아닙니다.");
            return;
        }

        bool self = Overhead.InComa(world.Ailments);
        bool bot = world.Companion is { } tie && world.AilmentsOf(tie.Serial).Any(one => one.Icon == Overhead.ComaIcon);
        ComaChoice choice = ComaChip.Choose(self, bot, world.Pack);

        switch (choice.Use)
        {
            case ComaUse.UseOnSelf:
                _ = world.UseAsync(choice.Slot, System.Threading.CancellationToken.None);
                break;
            case ComaUse.WakeBot:
                _ = world.WakeCompanionAsync(System.Threading.CancellationToken.None);
                break;
            default:
                _notify(choice.Why);
                break;
        }
    }
}
