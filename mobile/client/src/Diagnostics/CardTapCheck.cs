using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace LodClient;

/// <summary>
/// 로그인 카드 누르기 검사(<c>--kakao 표 --card-taps</c>) — 손가락 대신 누름·뗌을 넣어, 귀퉁이 아이콘은 제 일만 하고
/// 카드(들어가기)는 카드를 눌렀을 때만, 가로로 끌면 굴리기만 하는지 본다(사용자 2026-10-11: 아이콘을 누르면 바로 로그인됐다).
/// </summary>
/// <remarks>
/// 폰처럼 마우스를 손가락으로 친다(<see cref="Input.EmulateTouchFromMouse" />) — 그래야 굴림 칸이 끌기를 받는다.
/// 미리보기 표는 빼 둔다: 카드가 눌려도 서버에 붙지 않고 카드의 Pressed 만 센다(미리보기는 기기 표를 건드리지 않는다).
/// </remarks>
public static class CardTapCheck
{
    public static bool Requested() => System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--card-taps") >= 0;

    public static async Task Run(Node host, LoginScreen screen)
    {
        await Frames(host, 5);

        Main.SetKakao(null);
        screen.WantsToChangeLook = null;
        Input.EmulateTouchFromMouse = true;

        Button card = (Button)screen.FindChild("Card", true, false);
        Button look = (Button)screen.FindChild("Look", true, false);
        Button delete = (Button)screen.FindChild("Delete", true, false);
        Node up = card;
        while (up is not ScrollContainer) up = up.GetParent();
        ScrollContainer list = (ScrollContainer)up;
        int cards = 0, looks = 0, deletes = 0;
        card.Pressed += () => cards++;
        look.Pressed += () => looks++;
        delete.Pressed += () => deletes++;
        List<string> wrong = [];

        await Drag(host, look.GetGlobalRect().GetCenter(), 0);
        if (looks != 1 || cards != 0) wrong.Add($"생김새 아이콘: 생김새 {looks} 카드 {cards}");

        await Drag(host, delete.GetGlobalRect().GetCenter(), 0);
        if (deletes != 1 || cards != 0) wrong.Add($"삭제 아이콘: 삭제 {deletes} 카드 {cards}");
        foreach (Node cover in screen.GetChildren().OfType<CenterContainer>().Where(cover => cover.ZIndex == 100)) cover.QueueFree();
        await Frames(host, 2);

        await Drag(host, card.GetGlobalRect().GetCenter() + new Vector2(0, 20), 0);
        if (cards != 1) wrong.Add($"카드 누름: 카드 {cards}");

        // 한 장 반쯤 왼쪽으로 끈다 — 굴러야 하고 들어가기는 아니다. 아이콘에서 시작한 끌기도 같다.
        int before = list.ScrollHorizontal;
        await Drag(host, card.GetGlobalRect().GetCenter() + new Vector2(0, 20), -120);
        await Drag(host, delete.GetGlobalRect().GetCenter(), -60);
        // 카드가 다 들어가 굴릴 것이 없는 넓은 화면(852 가로)은 굴림을 묻지 않는다.
        bool scrollable = list.GetHScrollBar().MaxValue > list.GetHScrollBar().Page;
        if (cards != 1 || deletes != 1 || (scrollable && list.ScrollHorizontal <= before))
            wrong.Add($"가로 끌기: 카드 {cards} 삭제 {deletes} 굴림 {before}→{list.ScrollHorizontal}");

        GD.Print($"GREYBOX_CARD_TAPS 생김새 {looks} 삭제 {deletes} 카드 {cards} 굴림 {before}→{list.ScrollHorizontal}");
        foreach (string complaint in wrong) GD.Print($"GREYBOX_CARD_TAPS_BAD {complaint}");
        GD.Print(wrong.Count == 0 ? "GREYBOX_CARD_TAPS_OK" : $"GREYBOX_CARD_TAPS_BAD {wrong.Count}건");
        host.GetTree().Quit(wrong.Count == 0 ? 0 : 1);
    }

    /// <summary>그 점을 누르고 가로로 <paramref name="by" /> 만큼 여덟 걸음에 끈 뒤 뗀다(0 이면 그냥 누름). 화면 좌표 그대로 넣는다.</summary>
    private static async Task Drag(Node host, Vector2 at, float by)
    {
        Viewport viewport = host.GetViewport();
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left }, true);
        await Frames(host, 1);

        for (int step = 0; by != 0 && step < 8; step++)
        {
            Vector2 next = at + new Vector2(by / 8, 0);
            viewport.PushInput(new InputEventMouseMotion { Position = next, GlobalPosition = next, Relative = next - at, ButtonMask = MouseButtonMask.Left }, true);
            at = next;
            await Frames(host, 1);
        }

        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at }, true);
        await Frames(host, 30);
    }

    private static async Task Frames(Node host, int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
