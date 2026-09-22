using System.Collections.Generic;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>
/// Says whether the screen still fits at the size the run asked for.
/// </summary>
/// <remarks>
/// A layout that overflows does it quietly: the row simply walks off the bottom of the screen and nobody
/// notices until a screenshot looks wrong. That happened when the pack panel was given a minimum height —
/// it pushed the status bar and the movement pad out of the window entirely. So this walks the parts that
/// have to stay reachable and names the first one that does not.
///
/// <c>godot --path mobile/client -- --screen game --size 360x780 --orient portrait --layout</c>
/// </remarks>
public static class LayoutCheck
{
    private const string Flag = "--layout";

    private const string StuffFlag = "--stuff";

    public static bool Requested() => System.Array.IndexOf(OS.GetCmdlineUserArgs(), Flag) >= 0;

    /// <summary>
    /// Whether to fill the pack with pretend things while nothing is connected, as <c>--stuff</c>. The
    /// layout check always does; a screenshot has to ask, because a picture of an empty panel proves
    /// nothing either.
    /// </summary>
    private static bool Stuffed() =>
        Requested() || System.Array.IndexOf(OS.GetCmdlineUserArgs(), StuffFlag) >= 0;

    // 실제로 잘라 둔 아이템 그림. 없는 번호를 쓰면 칸이 비어 크기가 줄어든다.
    private static readonly int[] Icons = [32882, 32957, 33002, 32905, 32910];

    /// <summary>
    /// 걸친 것으로 꾸며 볼 때 어느 자리에 무엇을 놓나. **자리는 아이템이 정한다** — 서버의 아이템
    /// 템플릿에 <c>EquipmentSlot</c> 이 적혀 있고(장화 13 · 방패 3 · 귀걸이 5), 그 번호가 곧
    /// <c>WornPlace</c> 이고 <c>GearLayout</c> 이 그리는 자리다. 그래서 꾸민 것도 아무 자리에나
    /// 놓지 않는다: 그림이 있는 셋은 제자리에 놓고, 나머지는 비워 부위 그림이 나오게 둔다.
    /// 돈은 여기 없다 — 걸칠 수 있는 것이 아니다.
    /// </summary>
    private static readonly Dictionary<int, int> WornIcons = new()
    {
        [3] = 32957,   // 방패 — Luathas Bronze Shield
        [5] = 33002,   // 귀고리 — Luathas Coral Earrings
        [13] = 32882,  // 신발 — Shagreen Boots
    };

    /// <summary>
    /// Things to put in the pack while nothing is connected. A check against an empty panel measures a
    /// panel nobody will ever see: the worn places were added, the panel overflowed, and the check still
    /// said nothing was wrong because there was nothing in it to overflow with.
    /// </summary>
    /// <remarks>
    /// A full pack rather than a likely one — sixty slots is what the original holds, and the panel has to
    /// survive the worst of it. The numbers are the item pictures that have actually been cut.
    /// </remarks>
    public static IReadOnlyList<InventoryItem> PretendPack { get; } = Stuffed()
        ? [.. Enumerable(1, 60, slot => new InventoryItem(
            slot,
            Icons[slot % Icons.Length],
            0,
            $"자리 {slot} 의 시험용 물건",
            slot % 3 == 0 ? 12 : 1,
            30,
            100))]
        : [];

    /// <summary>
    /// Something on in every place the server can name, so the panel is measured full rather than empty.
    /// Only the three places we have a drawing for wear one; the rest are worn but pictureless, which is
    /// also what the real thing does for an item whose icon has not been cut yet.
    /// </summary>
    public static IReadOnlyList<WornItem> PretendWorn { get; } = Stuffed()
        ? [.. Enumerable(1, 18, slot => new WornItem(
            slot,
            WornIcons.TryGetValue(slot, out int icon) ? icon : 0,
            $"자리 {slot} 의 시험용 장비",
            WornPlace.Of(slot),
            30,
            100))]
        : [];

    /// <summary>
    /// Our own numbers while nothing is connected. The widest the top row will ever have to hold — a level-99 character
    /// with five-digit health and mana — so a check measures the row at its fullest, and a plain run still shows bars.
    /// </summary>
    public static Vitals PretendVitals { get; } = Vitals.Unknown with
    {
        Level = 99, Health = 99999, MaximumHealth = 99999, Mana = 99999, MaximumMana = 99999, Gold = 999_999_999
    };

    /// <summary>Representative pane entries so layout and screenshots exercise the restored icon sheets.</summary>
    /// <remarks>Filled past one page when stuffed, so the fan round the attack button is seen with a page to turn.</remarks>
    public static IReadOnlyList<LearnedSkill> PretendSkills { get; } = Stuffed()
        ? [.. Enumerable(1, 14, slot => new LearnedSkill(slot, slot, $"시험 기술 {slot}"))]
        : [new LearnedSkill(1, 1, "Assail")];

    public static IReadOnlyList<LearnedSpell> PretendSpells { get; } = Stuffed()
        ? [.. Enumerable(1, 8, slot => new LearnedSpell(slot, 20 + slot, SpellTargetType.NoTarget, $"시험 마법 {slot}", string.Empty, 1))]
        : [new LearnedSpell(1, 21, SpellTargetType.ChooseTarget, "beag ioc", "Target", 2)];

    /// <summary>
    /// Somebody to stand in the middle of the equipment ring while nothing is connected. The numbers are
    /// wardrobe pieces that have actually been cut — head 1, body 1, boots 1, shield 6 — so the figure that
    /// comes up is the one a real character would be drawn from.
    /// </summary>
    public static Character? PretendSelf { get; } = Stuffed()
        ? new Character(
            1,
            new Tile(0, 0),
            Direction.South,
            new Appearance(1, 1, 0, 1, 6, 0, 0, 0, 0, 0, 0, 0, 0),
            "시험용 수련생")
        : null;

    private static IEnumerable<T> Enumerable<T>(int from, int count, System.Func<int, T> make)
    {
        for (int index = 0; index < count; index++)
        {
            yield return make(from + index);
        }
    }

    public static void RunIfRequested(Node host, GameScreen screen)
    {
        if (Requested())
        {
            _ = ReportAfterLayout(host, screen);
        }
    }

    private static async System.Threading.Tasks.Task ReportAfterLayout(Node host, GameScreen screen)
    {
        List<string> wrong = [];

        // 소지품 탭을 재고, 장비 탭으로 넘겨 한 번 더 잰다. 둘의 높이가 다르고 넘치는 쪽은 장비였다.
        foreach (bool gear in new[] { false, true })
        {
            wrong.AddRange(await Measure(host, screen, gear));
        }

        foreach (string complaint in wrong)
        {
            GD.Print($"GREYBOX_LAYOUT_BAD {complaint}");
        }

        GD.Print(wrong.Count == 0 ? "GREYBOX_LAYOUT_OK" : $"GREYBOX_LAYOUT_BAD {wrong.Count}건");

        host.GetTree().Quit(wrong.Count == 0 ? 0 : 1);
    }

    /// <summary>만들기 화면에는 소지품·장비 같은 탭이 없다 — 갈아 끼울 상태가 없으니 한 번만 잰다.</summary>
    public static void RunIfRequested(Node host, CreateScreen screen)
    {
        if (Requested())
        {
            _ = ReportAfterLayout(host, screen.Parts);
        }
    }

    private static async System.Threading.Tasks.Task ReportAfterLayout(
        Node host,
        IReadOnlyList<(string Name, Control Part)> parts)
    {
        // Containers settle over a couple of frames; asking before that reads sizes nobody will ever see.
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        Vector2 screenSize = host.GetViewport().GetVisibleRect().Size;
        List<string> wrong = [];

        GD.Print($"GREYBOX_LAYOUT size {screenSize.X}x{screenSize.Y}");

        foreach ((string name, Control part) in parts)
        {
            Rect2 where = part.GetGlobalRect();

            GD.Print(
                $"GREYBOX_LAYOUT {name} {where.Position.X:0},{where.Position.Y:0} "
                + $"{where.Size.X:0}x{where.Size.Y:0}{(part.Visible ? string.Empty : " (숨김)")}");

            if (!part.Visible)
            {
                continue;
            }

            if (where.Position.Y < -1 || where.End.Y > screenSize.Y + 1)
            {
                wrong.Add($"{name} 이(가) 화면 위아래를 벗어납니다");
            }

            if (where.Position.X < -1 || where.End.X > screenSize.X + 1)
            {
                wrong.Add($"{name} 이(가) 화면 좌우를 벗어납니다");
            }
        }

        wrong.AddRange(Overlaps(parts));

        foreach (string complaint in wrong)
        {
            GD.Print($"GREYBOX_LAYOUT_BAD {complaint}");
        }

        GD.Print(wrong.Count == 0 ? "GREYBOX_LAYOUT_OK" : $"GREYBOX_LAYOUT_BAD {wrong.Count}건");

        host.GetTree().Quit(wrong.Count == 0 ? 0 : 1);
    }

    private static async System.Threading.Tasks.Task<List<string>> Measure(
        Node host,
        GameScreen screen,
        bool gear)
    {
        screen.ShowGearTab(gear);

        // Containers settle over a couple of frames; asking before that reads sizes nobody will ever see.
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);

        Vector2 screenSize = host.GetViewport().GetVisibleRect().Size;
        List<string> wrong = [];
        string tab = gear ? "장비" : "소지품";

        GD.Print($"GREYBOX_LAYOUT size {screenSize.X}x{screenSize.Y} tab {tab}");

        foreach ((string name, Control part) in screen.Parts)
        {
            Rect2 where = part.GetGlobalRect();

            GD.Print(
                $"GREYBOX_LAYOUT {name} {where.Position.X:0},{where.Position.Y:0} "
                + $"{where.Size.X:0}x{where.Size.Y:0}{(part.Visible ? string.Empty : " (숨김)")}");

            if (!part.Visible)
            {
                continue;
            }

            if (where.Position.Y < -1 || where.End.Y > screenSize.Y + 1)
            {
                wrong.Add($"{name} 이(가) 화면 위아래를 벗어납니다");
            }

            if (where.Position.X < -1 || where.End.X > screenSize.X + 1)
            {
                wrong.Add($"{name} 이(가) 화면 좌우를 벗어납니다");
            }
        }

        wrong.AddRange(Overlaps(screen.Parts));

        if (gear)
        {
            wrong.AddRange(GearCellsCut(screen, screenSize));
        }

        return [.. wrong.ConvertAll(complaint => $"[{tab}] {complaint}")];
    }

    /// <summary>
    /// Every worn place has to be in full sight on the gear tab at once — inside the window, on the screen, and not cut
    /// by anything that clips it. A ring that has to be scrolled to find the armour is one nobody uses (사용자, 2026-09-23:
    /// 가로에서 장비창을 스크롤로 내리게 하는건 불편해서 못 쓴다).
    /// </summary>
    private static IEnumerable<string> GearCellsCut(Control screen, Vector2 screenSize)
    {
        // 창이 닫혀 있으면 볼 것이 없다.
        if (screen.FindChild("Ring", true, false) is not Control { } ring || !ring.IsVisibleInTree())
        {
            yield break;
        }

        Rect2 whole = new(Vector2.Zero, screenSize);
        int cut = 0;

        foreach (Node node in ring.GetChildren())
        {
            if (node is not Button cell)
            {
                continue;
            }

            Rect2 where = cell.GetGlobalRect();
            bool seen = whole.Encloses(where);

            for (Node? up = cell.GetParent(); seen && up is not null && up != screen; up = up.GetParent())
            {
                if (up is Control { ClipContents: true } or PackPanel)
                {
                    seen = ((Control)up).GetGlobalRect().Encloses(where);
                }
            }

            cut += seen ? 0 : 1;
        }

        if (cut > 0)
        {
            yield return $"장비 칸 {cut}개가 창 안에 다 보이지 않습니다";
        }
    }

    /// <summary>
    /// The bars must not sit on top of each other. The world is left out, because in landscape everything is meant to
    /// float over it. The pack may cover the control row, because it is a modal that runs down to the bottom edge on
    /// purpose and nothing under it can be pressed while it is open (docs/mobile-test-v1-wireframes.md 8절). Upright it
    /// must leave the top row in sight; on its side it takes nearly the whole height, top row included, so the whole
    /// gear ring stands on one screen. Only a bar hiding another bar is a fault.
    /// </summary>
    /// <remarks>
    /// 전에는 소지품 창을 겹침 검사에서 통째로 뺐다. 그래서 세로 위 줄이 두 줄(120)이 된 뒤 장비 탭이 인벤토리·지도·
    /// 로그아웃 단추를 덮어도 검사가 조용했다(2026-09-23).
    /// </remarks>
    private static readonly string[] MeantToCover = ["월드"];

    private static bool MeantToLieOver(string one, string other) =>
        (one, other) is ("인벤토리", "조작 줄") or ("조작 줄", "인벤토리")
        || (!Main.Portrait && (one, other) is ("인벤토리", "위 줄") or ("위 줄", "인벤토리"));

    private static IEnumerable<string> Overlaps(IReadOnlyList<(string Name, Control Part)> parts)
    {
        for (int first = 0; first < parts.Count; first++)
        {
            for (int second = first + 1; second < parts.Count; second++)
            {
                (string oneName, Control one) = parts[first];
                (string otherName, Control other) = parts[second];

                if (System.Array.IndexOf(MeantToCover, oneName) >= 0
                    || System.Array.IndexOf(MeantToCover, otherName) >= 0
                    || MeantToLieOver(oneName, otherName)
                    || !one.Visible
                    || !other.Visible)
                {
                    continue;
                }

                if (one.GetGlobalRect().Intersects(other.GetGlobalRect()))
                {
                    yield return $"{oneName} 과(와) {otherName} 이(가) 겹칩니다";
                }
            }
        }
    }
}
