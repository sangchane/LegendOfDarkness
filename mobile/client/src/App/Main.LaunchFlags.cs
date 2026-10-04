using Godot;

namespace LodClient;

/// <summary>앱 시작 — 실행 인자(`-- --screen game` 같은 것)로 켜는 시험·미리보기 스위치.</summary>
public partial class Main : Control
{
    /// <summary>The login server this run talks to. Pass <c>--server host:port</c> to change it.</summary>
    public static System.Net.IPAddress ServerAddress { get; private set; } = System.Net.IPAddress.Loopback;

    public static int ServerPort { get; private set; }

    /// <summary>
    /// Credentials to fill in and submit without waiting for typing, as <c>--login name:secret</c>. For
    /// checking a build against a local server without a device in hand; empty in a normal run.
    /// </summary>
    public static (string Username, string Password) Rehearsal { get; private set; } = (string.Empty, string.Empty);

    /// <summary>
    /// Steps to walk on their own once the world opens, as <c>--walk NESW</c>. Same purpose as --login: it
    /// lets a build be checked without a hand on the screen.
    /// </summary>
    public static string Rehearse { get; private set; } = string.Empty;

    /// <summary>
    /// A direction key to hold down on its own, as <c>--hold E</c> — pressed for a second and a half by a real press on
    /// the key, to check that holding keeps walking and that the pad fades while it does.
    /// </summary>
    public static string Holding { get; private set; } = string.Empty;

    /// <summary>
    /// A fan slot to press on its own, as <c>--skill 1</c>. For looking at what a technique draws — the motion and the
    /// flash — without a hand on the screen.
    /// </summary>
    public static string Ability { get; private set; } = string.Empty;

    /// <summary>
    /// Whether to tap the first other person the server shows us, as <c>--pick</c>. A real tap on their
    /// figure, so what it checks is the same path a thumb takes.
    /// </summary>
    public static bool Picking { get; private set; }

    /// <summary>
    /// A line to say once the world is open, as <c>--say "give Shagreen Boots"</c>. The server reads a
    /// line like that as a command when the speaker is allowed to give one, which is how a test puts
    /// something in an empty pack.
    /// </summary>
    public static string Saying { get; private set; } = string.Empty;

    /// <summary>
    /// Which tab of the full log to open on its own once the world has settled, as <c>--chat 시스템</c>
    /// (전체 · 일반 · 파티 · 시스템). Empty leaves it shut. For checking it without a thumb.
    /// </summary>
    public static string ChatTab { get; private set; } = string.Empty;

    /// <summary>손 없이 확인할 때 — 이 이름의 사람을 실제로 탭하고 [파티 초대]를 누른다(<c>--invite 이름</c>).</summary>
    public static string Inviting { get; private set; } = string.Empty;

    /// <summary>손 없이 확인할 때 — 내 장비창을 연 채 이 이름의 사람을 실제로 탭해 그 사람 장비창을 받는다(<c>--look 이름</c>).</summary>
    public static string Looking { get; private set; } = string.Empty;

    /// <summary>손 없이 확인할 때 — 들어가서 [접속자] 창을 연다(<c>--users</c>).</summary>
    public static bool ShowingUsers => System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--users") >= 0;

    /// <summary>손 없이 확인할 때 — 파티 초대가 오면 [수락]을 누른다(<c>--accept</c>).</summary>
    public static bool Accepting { get; private set; }

    /// <summary>손 없이 확인할 때 — 파티가 되면 대화 창 파티 탭에서 이 말을 보낸다(<c>--party-say 말</c>).</summary>
    public static string PartySaying { get; private set; } = string.Empty;

    /// <summary>손 없이 확인할 때 — 파티가 되고 이만큼(초) 뒤 [나가기]를 누른다(<c>--leave-after 초</c>).</summary>
    public static double LeavingAfter { get; private set; } = -1;

    /// <summary>
    /// Whether to put a handful of real server lines through the sorting on their own, as <c>--notices</c>, with no
    /// server — so the ticker, the toasts and the banner can be photographed in both orientations.
    /// </summary>
    public static bool Noticing { get; private set; }

    /// <summary>
    /// With no server (<c>--screen game</c>), what to stand over the heads to photograph the overhead stack, as
    /// <c>--overhead badges</c> (many badges, bar up and down, 일음지 and Miss) or <c>--overhead coma</c> (us in a coma).
    /// </summary>
    public static string Overhead { get; private set; } = string.Empty;

    /// <summary>Whether to open the pack on its own, as <c>--pack</c>. For checking it without a thumb.</summary>
    public static bool OpeningPack { get; private set; }

    /// <summary>Whether to open the settings window on its own, as <c>--settings</c>. Same purpose as --pack.</summary>
    public static bool OpeningSettings { get; private set; }

    /// <summary><c>--settings-tab 자동|봇</c>: which tab the settings window opens on. For photographs.</summary>
    public static string SettingsTab { get; private set; } = string.Empty;

    /// <summary><c>--minimap-zoom N</c>: 자리를 잡은 뒤 미니맵의 [+](N>0) 또는 [−](N<0) 를 |N| 번 실제로 누른다 — 사진·배선 확인용(기기에 남는다).</summary>
    public static int MinimapZoom { get; private set; }

    /// <summary><c>--party-preview</c>: 서버 없이 파티원 다섯을 지어 파티원 칸을 그린다(사진·배치 검사용).</summary>
    public static bool PartyPreview { get; private set; }

    /// <summary><c>--minimap</c>: prints where the minimap stands and what it shows (<c>GREYBOX_MINIMAP</c>), to check it without a thumb.</summary>
    public static bool CheckingMinimap { get; private set; }

    /// <summary><c>--pack-pick N</c>: once the pack is open, taps its N-th picture (1-based) so the action row beside it can be photographed.</summary>
    public static int PackPick { get; private set; }

    /// <summary><c>--exit-menu</c>: opens settings and presses [로그아웃] on its title row once the screen settles, to photograph the choice.</summary>
    public static bool OpeningExit { get; private set; }

    /// <summary>Whether to hold the health-potion button on its own, as <c>--pick-potion</c>, to see the row it opens.</summary>
    public static bool PickingPotion { get; private set; }

    /// <summary>
    /// Which ability-bar slot (1-based, 0 = none) to hold on its own, as <c>--slot-hold 2</c>, to see the
    /// picker it opens without a thumb.
    /// </summary>
    public static int SlotHold { get; private set; }

    /// <summary>
    /// <c>--learn-preview 5:31</c>: 서버 없이(<c>--screen game</c>) 기술 목록을 그 직업·레벨로 본다 — 표에서 그 레벨까지를
    /// 배운 셈 치고, 그 위는 흐리게 "N레벨에 배움". <c>--slot-hold</c> 와 함께 찍는다.
    /// </summary>
    public static (int Path, int Level)? LearnPreview { get; private set; }

    /// <summary>
    /// Which settings select box (<c>health</c>·<c>mana</c>·<c>heal</c>) to open on its own, as
    /// <c>--percent-open health</c>, to see the list it drops down without a thumb.
    /// </summary>
    public static string PercentOpen { get; private set; } = string.Empty;

    /// <summary>Whether to press the "지도" button on its own, as <c>--map</c>. For checking it without a thumb.</summary>
    public static bool OpeningMap { get; private set; }

    /// <summary><c>--map-tab 마을|사냥터</c> — 서버 없이 <c>--map</c> 으로 띄운 카드 창을 그 탭으로 연다(선 곳을 그 종류로 친다). 사진용.</summary>
    public static string MapTab { get; private set; } = string.Empty;

    /// <summary><c>--map-go 이름</c> — <c>--map</c> 으로 연 월드맵에서 그 이름의 줄을 눌러 그리로 간다(닫기는 누르지 않는다).</summary>
    public static string MapGo { get; private set; } = string.Empty;

    /// <summary>
    /// Whether to press the minimap (it opens the 길 찾기 map — the old 「길」 button became the minimap) on its own, as <c>--tabmap</c>; with <c>--tabmap-go 이름</c> it then taps the exit or
    /// NPC of that name on the map, and with <c>--tabmap-close 초</c> it presses 닫기 that long after opening. For checking the
    /// 길 찾기 map without a thumb.
    /// </summary>
    public static bool OpeningTabMap { get; private set; }

    public static string TabMapGo { get; private set; } = string.Empty;

    public static double TabMapCloseAfter { get; private set; } = -1;

    /// <summary>With <c>--tabmap-zoom</c>, presses 확대 once the map is open.</summary>
    public static bool TabMapZoom { get; private set; }

    /// <summary>Whether to swing once after picking somebody, as <c>--strike</c>.</summary>
    public static bool Striking { get; private set; }

    /// <summary>
    /// Whether the character hunts on its own, as <c>--hunt</c> or by shipping a `hunt.cfg`. It walks the
    /// zone, swings at what it meets and spends the points a level hands out.
    /// </summary>
    /// <remarks>
    /// 이것이 있어야 기술·모션·소리·이펙트를 **실기기 화면에서** 확인할 수 있다. 밖에서 딴 연결로
    /// 캐릭터를 굴리면 화면에 있는 캐릭터는 가만히 서 있고, 확인되는 것은 서버 수치뿐이다.
    /// </remarks>
    public static bool Hunting { get; private set; }

    /// <summary>
    /// Whether to tap the nearest thing lying on the floor, as <c>--lift</c>. A real tap on its picture,
    /// so what it checks is the arithmetic a thumb goes through — not just the command underneath it.
    /// </summary>
    public static bool Lifting { get; private set; }

    /// <summary>Whether to press the first carried thing once the pack is open, as <c>--wear</c>.</summary>
    public static bool Wearing { get; private set; }

    /// <summary>
    /// Whether to open the gear window a little after the pack opens — after <c>--wear</c> has pressed 입기, when given —
    /// as <c>--gear-after</c>, so one run shows the pack and then the gear window, or the thing leaving the pack and then
    /// sitting in its worn place.
    /// </summary>
    public static bool GearAfter { get; private set; }

    /// <summary>
    /// Whether to throw the first carried thing on the floor instead, as <c>--throw</c>. Together with
    /// <c>--lift</c> in a second run that closes the loop with no hand on it: down, then back up.
    /// </summary>
    public static bool Throwing { get; private set; }

    /// <summary>
    /// Whether <c>--pack</c> opens the gear window rather than the pack, as <c>--gear</c>. Without it there
    /// is no way to photograph the gear window with no hand on the screen.
    /// </summary>
    public static bool OnGear { get; private set; }

    /// <summary>
    /// 만들기 화면을 손 없이 확인할 때 미리 고른 성별·머리·색, as <c>--pick-look 2,32,40</c>(성별 2 ·
    /// 머리 32 · 색 40). --walk·--pick 과 같은 목적이다 — 손이 없어도 화면을 그 상태로 찍을 수 있게 한다.
    /// </summary>
    public static (byte Gender, byte HairStyle, byte HairColor)? PickedLook { get; private set; }

    /// <summary>Optional hand-free creation choice, as <c>--pick-job 1</c> through <c>5</c>.</summary>
    public static byte? PickedPath { get; private set; }

    /// <summary>
    /// 만들기 화면에서 "만들기"를 손 없이 눌러 본다, as <c>--create-now</c>. 이름·비밀번호는 새 인자를
    /// 만들지 않고 <see cref="Rehearsal"/>(<c>--login</c>)을 그대로 쓴다 — 로그인 화면이 같은 값으로
    /// 자동 로그인하는 것과 같은 결이다.
    /// </summary>
    public static bool CreateNow { get; private set; }

    /// <summary>Reads a value passed after a double dash, as in <c>-- --screen game --orient portrait</c>.</summary>
    private static string Flag(string name)
    {
        string[] args = OS.GetCmdlineUserArgs();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == name)
            {
                return args[index + 1];
            }
        }

        return string.Empty;
    }

    private static void ReadRehearsal(string value)
    {
        string given = value.Length > 0 ? value : LoginFromFile() ?? string.Empty;
        int split = given.IndexOf(':');

        if (split > 0 && split < given.Length - 1)
        {
            Rehearsal = (given[..split], given[(split + 1)..]);
        }
    }

    /// <summary>`gender,hairStyle,hairColor` — 셋 다 숫자가 아니면 아무것도 미리 고르지 않는다.</summary>
    private static (byte, byte, byte)? ReadPickedLook(string value)
    {
        string[] parts = value.Split(',');

        return parts.Length == 3
               && byte.TryParse(parts[0], out byte gender)
               && byte.TryParse(parts[1], out byte hairStyle)
               && byte.TryParse(parts[2], out byte hairColor)
            ? (gender, hairStyle, hairColor)
            : null;
    }

    private static byte? ReadPickedPath(string value) =>
        byte.TryParse(value, out byte path) && path is >= 1 and <= 5 ? path : null;
}
