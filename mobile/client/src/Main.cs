using Godot;

namespace LodClient;

/// <summary>
/// Greybox host. Applies the one theme every screen shares, works out the safe-area insets once, and mounts
/// the screen under test. No colour or art here: greybox judges layout, reach and text, nothing else.
/// </summary>
public partial class Main : Control
{
    /// <summary>Smallest touch target, in logical units. One unit is one dp at this project's base size.</summary>
    public const int TouchMinimum = 48;

    /// <summary>Smallest gap between controls, and the least distance kept from the safe-area edge.</summary>
    public const int Gutter = 8;

    /// <summary>
    /// Widest the two thumb clusters may sit apart. Past this the world keeps growing but the controls stay
    /// where a thumb can still reach them, which is the rule for 20:9 and wider screens.
    /// </summary>
    public const int ThumbSpanMaximum = 680;

    private const int BodyFontSize = 16;

    /// <summary>Where the login server is, unless --server says otherwise.</summary>
    private const string DefaultServer = "127.0.0.1:2610";

    // 실기기에서는 아이콘을 탭해 여는 것이 전부라 --server 를 넘길 방법이 없고, 거기서 127.0.0.1 은
    // 기기 자신이라 아무것도 없다. 그렇다고 주소를 코드에 박으면 붙을 기계가 바뀔 때마다 소스를
    // 고쳐야 한다. 그래서 빌드에 함께 실리는 이 파일에서 읽는다 — 커밋되지 않는다(.gitignore).
    private const string ServerFile = "res://server.cfg";

    /// <summary>
    /// 계정이 적혀 있으면 화면을 거치지 않고 바로 들어간다. `server.cfg` 와 같은 자리에 둔다. **실기기에서는
    /// 읽지 않는다**(<see cref="LoginFromFile"/>) — 데스크톱에서 로컬 서버를 손 없이 확인하는 지름길이지,
    /// 기기에서 앱을 켠 사람을 그 계정으로 곧장 들여보내려는 것이 아니다.
    /// </summary>
    private const string LoginFile = "res://login.cfg";

    /// <summary>
    /// 로그인 화면의 "자동 로그인"을 켜고 로그인에 성공하면 그 계정이 여기 남는다(기기 안 user:// —
    /// 저장소가 아니라 이 기기에만). 로그인 화면에서 끄면 지운다. 처음에는 없다(꺼져 있다).
    /// </summary>
    private const string AutoLoginFile = "user://autologin.cfg";

    /// <summary>이 파일이 실려 있으면 캐릭터가 스스로 사냥한다. 실기기가 인자를 못 받아 파일로 켠다.</summary>
    private const string HuntFile = "res://hunt.cfg";

    /// <summary>
    /// 밟은 것을 알아서 줍나. 사람이 화면에서 켜고 끄며, 그 결정은 기기에 남는다(user:// — 저장소가
    /// 아니라 기기 쪽이라 새로 설치해도 그 기기의 선택이 남는다). 처음에는 켜져 있다.
    /// </summary>
    private const string LootFile = "user://loot.cfg";

    public static bool AutoLoot { get; private set; } = true;

    /// <summary>The account the login screen's own "자동 로그인" toggle has saved, or none.</summary>
    public static Lod.Mobile.Core.World.AutoLoginAccount? SavedLogin { get; private set; }

    // Launch-time credentials (--login / login.cfg) and a saved account may each submit once. An explicit
    // logout turns that convenience off for every login screen reached afterward, including a round trip
    // through account creation, until the app is launched again.
    private readonly Lod.Mobile.Core.World.AutoLoginGate _autoLoginGate = new();

    /// <summary>Saves, or (given <c>null</c>) forgets, the account to sign in with automatically next launch.</summary>
    public static void SetSavedLogin(Lod.Mobile.Core.World.AutoLoginAccount? account)
    {
        SavedLogin = account;

        if (account is null)
        {
            if (Godot.FileAccess.FileExists(AutoLoginFile))
            {
                Godot.DirAccess.RemoveAbsolute(AutoLoginFile);
            }

            return;
        }

        Godot.FileAccess? writing = Godot.FileAccess.Open(AutoLoginFile, Godot.FileAccess.ModeFlags.Write);

        if (writing is not null)
        {
            foreach (string line in account.ToLines())
            {
                writing.StoreLine(line);
            }

            writing.Close();
        }
    }

    private static void ReadSavedLogin()
    {
        if (!Godot.FileAccess.FileExists(AutoLoginFile))
        {
            return;
        }

        using Godot.FileAccess reading = Godot.FileAccess.Open(AutoLoginFile, Godot.FileAccess.ModeFlags.Read);
        SavedLogin = Lod.Mobile.Core.World.AutoLoginAccount.Parse(reading.GetLine().Trim(), reading.GetLine().Trim());
    }

    /// <summary>Turns picking-up-as-you-walk on or off, and remembers which.</summary>
    public static void SetAutoLoot(bool on)
    {
        AutoLoot = on;

        Godot.FileAccess? writing = Godot.FileAccess.Open(LootFile, Godot.FileAccess.ModeFlags.Write);

        if (writing is not null)
        {
            writing.StoreLine(on ? "on" : "off");
            writing.Close();
        }
    }

    private static void ReadAutoLoot()
    {
        Godot.FileAccess? reading = Godot.FileAccess.Open(LootFile, Godot.FileAccess.ModeFlags.Read);

        if (reading is not null)
        {
            AutoLoot = reading.GetLine().Trim() != "off";
            reading.Close();
        }
    }

    /// <summary>
    /// 체력·마력이 몇 % 이하일 때 어느 포션을 저절로 마시나. 줍기처럼 기기에 남는다(한 줄에 하나, "on 70 쿠룸").
    /// 처음에는 꺼져 있다 — 사람이 켜기 전에는 가방의 물건을 쓰지 않는다. 줄은 70%(사용자, 2026-09-23).
    /// </summary>
    private const string PotionFile = "user://potion.cfg";

    public static Lod.Mobile.Core.World.PotionRule HealthPotion { get; private set; } = new(false, 70, "쿠룸");

    public static Lod.Mobile.Core.World.PotionRule ManaPotion { get; private set; } = new(false, 70, "마라디움");

    public static void SetPotions(Lod.Mobile.Core.World.PotionRule health, Lod.Mobile.Core.World.PotionRule mana)
    {
        HealthPotion = health;
        ManaPotion = mana;

        Godot.FileAccess? writing = Godot.FileAccess.Open(PotionFile, Godot.FileAccess.ModeFlags.Write);

        if (writing is not null)
        {
            foreach (Lod.Mobile.Core.World.PotionRule rule in new[] { health, mana })
            {
                writing.StoreLine($"{(rule.Enabled ? "on" : "off")} {rule.Percent} {rule.Potion}");
            }

            writing.Close();
        }
    }

    private static void ReadPotions()
    {
        Godot.FileAccess? reading = Godot.FileAccess.Open(PotionFile, Godot.FileAccess.ModeFlags.Read);

        if (reading is null)
        {
            return;
        }

        HealthPotion = Rule(reading.GetLine(), HealthPotion);
        ManaPotion = Rule(reading.GetLine(), ManaPotion);
        reading.Close();

        static Lod.Mobile.Core.World.PotionRule Rule(string line, Lod.Mobile.Core.World.PotionRule fallback)
        {
            string[] parts = line.Trim().Split(' ');

            return parts.Length == 3 && int.TryParse(parts[1], out int percent) && percent is > 0 and < 100
                ? new(parts[0] == "on", percent, parts[2])
                : fallback;
        }
    }

    /// <summary>
    /// 자동 사냥의 반경(칸)과 회복 기술 줄(%). potion.cfg 처럼 기기에 한 줄("12 50")로 남는다.
    /// </summary>
    private const string AutoHuntFile = "user://autohunt.cfg";

    public static Lod.Mobile.Core.World.AutoHuntSettings AutoHuntSettings { get; private set; } = new();

    /// <summary><c>--auto-hunt</c> — 게임 화면이 뜨고 자리를 잡으면 [자동] 단추를 스스로 누른다. 손 없이 확인하는 용.</summary>
    public static bool AutoHuntOnStart { get; private set; }

    /// <summary>
    /// <c>--auto-hunt-preview</c> — 서버 없이(<c>--screen game</c>) 공격 단추의 켜짐 표시(테두리·도는 빛·"자동" 글자,
    /// <see cref="AbilityBar.ShowAutoHunt" />)만 그려 본다. <c>--auto-hunt</c> 는 실제 서버 접속이 있어야 켜져 이
    /// 화면에서는 켜지지 않는다.
    /// </summary>
    public static bool AutoHuntPreview { get; private set; }

    public static void SetAutoHuntSettings(Lod.Mobile.Core.World.AutoHuntSettings settings)
    {
        AutoHuntSettings = settings;

        Godot.FileAccess? writing = Godot.FileAccess.Open(AutoHuntFile, Godot.FileAccess.ModeFlags.Write);

        if (writing is not null)
        {
            writing.StoreLine(settings.ToLine());
            writing.Close();
        }
    }

    private static void ReadAutoHuntSettings()
    {
        Godot.FileAccess? reading = Godot.FileAccess.Open(AutoHuntFile, Godot.FileAccess.ModeFlags.Read);

        if (reading is not null)
        {
            AutoHuntSettings = Lod.Mobile.Core.World.AutoHuntSettings.Parse(reading.GetLine());
            reading.Close();
        }
    }

    /// <summary>
    /// 기술 슬롯을 길게 눌러 정한 배치 — 캐릭터 이름마다 따로 남는다(사용자 요청, 2026-09-25). 규칙(무엇을
    /// 어디에 두나)은 알맹이 <see cref="Lod.Mobile.Core.World.AbilityArrangement"/> 가 갖고, 여기는 potion.cfg 처럼
    /// 한 줄에 하나씩 기기(user://)에 적고 읽기만 한다.
    /// </summary>
    private static string AbilitySlotsFile(string character)
    {
        string safe = new([.. character.Where(char.IsLetterOrDigit)]);
        return $"user://ability_slots_{(safe.Length > 0 ? safe : "player")}.cfg";
    }

    public static void SaveAbilitySlots(string character, IReadOnlyList<string> lines)
    {
        Godot.FileAccess? writing = Godot.FileAccess.Open(AbilitySlotsFile(character), Godot.FileAccess.ModeFlags.Write);

        if (writing is null)
        {
            return;
        }

        foreach (string line in lines)
        {
            writing.StoreLine(line);
        }

        writing.Close();
    }

    public static string[] LoadAbilitySlots(string character)
    {
        string path = AbilitySlotsFile(character);

        if (!Godot.FileAccess.FileExists(path))
        {
            return [];
        }

        using Godot.FileAccess reading = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        return reading.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>환경변수로도 준다. 데스크톱에서 인자 없이 다른 서버를 가리킬 때 쓴다.</summary>
    private const string ServerVariable = "LOD_SERVER";

    /// <summary>Logical size of a portrait screen. One unit is one dp here too.</summary>
    private static readonly Vector2I PortraitSize = new(360, 780);

    // Windows ships a Korean face; Android and iOS do not have this path. Until a licensed font is bundled,
    // a missing face is reported on screen rather than left to render as empty boxes.
    private const string WindowsKoreanFont = "C:/Windows/Fonts/malgun.ttf";

    // Desktops other than Windows may still have a Korean face. Asking the system for one by name is the
    // only way to tell — the question is whether Hangul draws, not whether one particular file exists.
    private static readonly string[] SystemKoreanFonts =
        { "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Malgun Gothic", "NanumGothic" };

    /// <summary>Insets that keep text and controls clear of notches and the home indicator.</summary>
    public static (int Left, int Top, int Right, int Bottom) SafeInsets { get; private set; } =
        (Gutter, Gutter, Gutter, Gutter);

    /// <summary>The font actually in use. Printed on screen, because Hangul depends on it.</summary>
    public static string FontName { get; private set; } = "확인 전";

    /// <summary>
    /// Which way the screen is held. Portrait is not landscape squeezed: it has enough height to keep the
    /// controls out of the world, so the screens lay themselves out differently rather than scaling.
    /// </summary>
    public static bool Portrait { get; private set; }

    public static event System.Action? LayoutChanged;
    private bool _orientationWasForced;

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

    /// <summary><c>--exit-menu</c>: presses the top row's [종료] once the screen settles, to photograph the choice.</summary>
    public static bool OpeningExit { get; private set; }

    /// <summary>Whether to hold the health-potion button on its own, as <c>--pick-potion</c>, to see the row it opens.</summary>
    public static bool PickingPotion { get; private set; }

    /// <summary>
    /// Which ability-bar slot (1-based, 0 = none) to hold on its own, as <c>--slot-hold 2</c>, to see the
    /// picker it opens without a thumb.
    /// </summary>
    public static int SlotHold { get; private set; }

    /// <summary>
    /// Which settings select box (<c>health</c>·<c>mana</c>·<c>heal</c>) to open on its own, as
    /// <c>--percent-open health</c>, to see the list it drops down without a thumb.
    /// </summary>
    public static string PercentOpen { get; private set; } = string.Empty;

    /// <summary>Whether to press the "지도" button on its own, as <c>--map</c>. For checking it without a thumb.</summary>
    public static bool OpeningMap { get; private set; }

    /// <summary><c>--map-go 이름</c> — <c>--map</c> 으로 연 월드맵에서 그 이름의 줄을 눌러 그리로 간다(닫기는 누르지 않는다).</summary>
    public static string MapGo { get; private set; } = string.Empty;

    /// <summary>
    /// Whether to press the 「길」 button on its own, as <c>--tabmap</c>; with <c>--tabmap-go 이름</c> it then taps the exit or
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
    /// Whether to turn to the gear tab a little after the pack opens — after <c>--wear</c> has pressed 입기, when given —
    /// as <c>--gear-after</c>, so one run shows the pack tab and then the gear tab, or the thing leaving the pack and then
    /// sitting in its worn place.
    /// </summary>
    public static bool GearAfter { get; private set; }

    /// <summary>
    /// Whether to throw the first carried thing on the floor instead, as <c>--throw</c>. Together with
    /// <c>--lift</c> in a second run that closes the loop with no hand on it: down, then back up.
    /// </summary>
    public static bool Throwing { get; private set; }

    /// <summary>
    /// Whether the pack opens on the gear tab rather than the pack tab, as <c>--gear</c>. Without it there
    /// is no way to photograph the gear tab with no hand on the screen.
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

    public override void _Ready()
    {
        _orientationWasForced = Flag("--orient").Length > 0;
        Portrait = Flag("--orient") == "portrait";
        ReadServer(Flag("--server"));
        ReadRehearsal(Flag("--login"));
        Rehearse = Flag("--walk");
        Holding = Flag("--hold");
        Ability = Flag("--skill");
        Picking = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pick") >= 0;
        PickedLook = ReadPickedLook(Flag("--pick-look"));
        PickedPath = ReadPickedPath(Flag("--pick-job"));
        CreateNow = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--create-now") >= 0;
        Saying = Flag("--say");
        ChatTab = Flag("--chat");
        Inviting = Flag("--invite");
        Accepting = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--accept") >= 0;
        PartySaying = Flag("--party-say");
        LeavingAfter = double.TryParse(Flag("--leave-after"), out double leaveAfter) ? leaveAfter : -1;
        Noticing = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--notices") >= 0;
        Overhead = Flag("--overhead");
        OpeningPack = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pack") >= 0;
        OpeningSettings = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--settings") >= 0;
        OpeningExit = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--exit-menu") >= 0;
        PickingPotion = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pick-potion") >= 0;
        SlotHold = int.TryParse(Flag("--slot-hold"), out int slotHold) ? slotHold : 0;
        PercentOpen = Flag("--percent-open");
        MapGo = Flag("--map-go");
        OpeningMap = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--map") >= 0 || MapGo.Length > 0;
        TabMapGo = Flag("--tabmap-go");
        TabMapCloseAfter = double.TryParse(Flag("--tabmap-close"), out double tabMapClose) ? tabMapClose : -1;
        TabMapZoom = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--tabmap-zoom") >= 0;
        OpeningTabMap = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--tabmap") >= 0 || TabMapGo.Length > 0 || TabMapZoom;
        OnGear = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--gear") >= 0;
        Striking = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--strike") >= 0;
        Hunting = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--hunt") >= 0
            || Godot.FileAccess.FileExists(HuntFile);

        // 사냥은 사람이 손대지 않는 채로 오래 돈다. iOS 는 화면이 꺼지면 앱을 재우고, 그러면 _Process 가
        // 멈춰 캐릭터가 그 자리에 굳는다 — 접속은 살아 있어서 서버 쪽에서는 멀쩡해 보인다.
        if (Hunting)
        {
            DisplayServer.ScreenSetKeepOn(true);
        }
        Lifting = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--lift") >= 0;
        Wearing = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--wear") >= 0;
        GearAfter = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--gear-after") >= 0;
        ReadAutoLoot();
        ReadPotions();
        ReadAutoHuntSettings();
        AutoHuntOnStart = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--auto-hunt") >= 0;
        AutoHuntPreview = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--auto-hunt-preview") >= 0;
        ReadSavedLogin();
        Throwing = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--throw") >= 0;

        // 입거나 버려 보려면 소지품이 열려 있어야 한다 — 따로 적게 하지 않는다.
        OpeningPack = OpeningPack || Wearing || Throwing;

        // 사진을 찍거나 자리를 재는 실행은 사람이 볼 것이 아니다. 창을 아주 없앨 수는 없다 —
        // --headless 는 아무것도 그리지 않을 뿐 아니라 --size 도 듣지 않아 780x780 을 재게 된다.
        // 그래서 화면 밖으로 내보내고 키보드를 빼앗지 않게 한다. 작업표시줄에만 남는다.
        if (Screenshot.Requested() || LayoutCheck.Requested())
        {
            StayOutOfTheWay();
        }

        // --size wins over the orientation's own default, so a check can walk several shapes of screen.
        Vector2I? asked = SizeFromCommandLine();

        if (asked is { } wanted)
        {
            GetWindow().ContentScaleSize = wanted;
            DisplayServer.WindowSetSize(wanted);
        }
        else if (Portrait)
        {
            GetWindow().ContentScaleSize = PortraitSize;
            DisplayServer.WindowSetSize(PortraitSize * 5 / 4);
        }

        Theme = BuildTheme();

        // 화면 키보드·키보드 치우기·목록 끌기 — 화면마다 따로 하지 않고 여기 한 곳에서(TouchInput).
        AddChild(new TouchInput());
        GetWindow().SizeChanged += RefreshDrawableLayout;
        RefreshDrawableLayout();

        if (Flag("--screen") == "game")
        {
            GameScreen game = new();

            AddChild(game);
            LayoutCheck.RunIfRequested(this, game);
        }
        else if (Flag("--screen") == "create")
        {
            CreateScreen create = BuildCreateScreen();

            AddChild(create);
            LayoutCheck.RunIfRequested(this, create);
        }
        else
        {
            AddChild(BuildLoginScreen());
        }

        Screenshot.CaptureIfRequested(this);
    }

    /// <summary>Re-reads the iOS drawable and safe area after every resize and foreground notification.</summary>
    public override void _Notification(int what)
    {
        base._Notification(what);
        // 1004 is Godot's NOTIFICATION_WM_WINDOW_FOCUS_IN; the C# binding exposes the application
        // notification but not this window-only alias.
        if (what == NotificationApplicationFocusIn || what == 1004)
        {
            RefreshDrawableLayout();
        }
    }

    private void RefreshDrawableLayout()
    {
        if (!IsInsideTree()) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        bool oldPortrait = Portrait;
        if (!_orientationWasForced) Portrait = viewport.Y >= viewport.X;
        SafeInsets = ComputeSafeInsets(viewport);
        ApplySafeInsets(this);
        if (oldPortrait != Portrait) LayoutChanged?.Invoke();
    }

    /// <summary>Touches only SafeArea nodes which are still children of the live host.</summary>
    private static void ApplySafeInsets(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MarginContainer safe && safe.Name.ToString() == "SafeArea" && GodotObject.IsInstanceValid(safe))
            {
                safe.AddThemeConstantOverride("margin_left", SafeInsets.Left);
                safe.AddThemeConstantOverride("margin_top", SafeInsets.Top);
                safe.AddThemeConstantOverride("margin_right", SafeInsets.Right);
                safe.AddThemeConstantOverride("margin_bottom", SafeInsets.Bottom);
            }
            ApplySafeInsets(child);
        }
    }

    /// <summary>Swaps the login screen for the world the connection leads to.</summary>
    private void Enter(LoginScreen login, Lod.Mobile.Core.Net.WorldSession session)
    {
        RemoveChild(login);
        login.QueueFree();

        GameScreen game = new(new Lod.Mobile.Core.World.WorldClient(session));

        game.LoggedOut = () => Callable.From(() => BackToLogin(game)).CallDeferred();
        AddChild(game);
    }

    private LoginScreen BuildLoginScreen()
    {
        LoginScreen login = new() { AutomaticLogin = _autoLoginGate.MaySubmit };

        // Deferred, because this runs from the login screen's own frame and the tree may not be changed
        // in the middle of one.
        login.Entered = session => Callable.From(() => Enter(login, session)).CallDeferred();
        login.WantsToCreate = () => Callable.From(() => GoToCreate(login)).CallDeferred();

        return login;
    }

    private CreateScreen BuildCreateScreen()
    {
        CreateScreen create = new();

        create.Cancelled = () => Callable.From(() => BackToLogin(create)).CallDeferred();
        create.Entered = session => Callable.From(() => Enter(create, session)).CallDeferred();

        return create;
    }

    /// <summary>Swaps a completed creation screen directly for its already authenticated game session.</summary>
    private void Enter(CreateScreen create, Lod.Mobile.Core.Net.WorldSession session)
    {
        if (!GodotObject.IsInstanceValid(create) || create.GetParent() != this)
        {
            session.Dispose();
            return;
        }

        RemoveChild(create);
        create.QueueFree();

        GameScreen game = new(new Lod.Mobile.Core.World.WorldClient(session));
        game.LoggedOut = () => Callable.From(() => BackToLogin(game)).CallDeferred();
        AddChild(game);
    }

    /// <summary>계정이 없어 만들기로 간다.</summary>
    private void GoToCreate(LoginScreen login)
    {
        RemoveChild(login);
        login.QueueFree();

        AddChild(BuildCreateScreen());
    }

    /// <summary>취소를 눌러 로그인 화면으로 돌아간다.</summary>
    private void BackToLogin(CreateScreen create)
    {
        RemoveChild(create);
        create.QueueFree();

        AddChild(BuildLoginScreen());
    }

    /// <summary>The game has already released its connection; replace its node on the next safe tree turn.</summary>
    private void BackToLogin(GameScreen game)
    {
        if (!GodotObject.IsInstanceValid(game) || game.GetParent() != this)
        {
            return;
        }

        RemoveChild(game);
        game.QueueFree();

        // login.cfg, --login and a saved account are all launch conveniences. An explicit logout must not
        // consume them again and immediately put the same account back in the world.
        _autoLoginGate.NoteLogout();
        AddChild(BuildLoginScreen());
    }

    private static void StayOutOfTheWay()
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.NoFocus, true);
        DisplayServer.WindowSetPosition(new Vector2I(-4000, -4000));
    }

    /// <summary>The screen size a run asks for, as <c>--size 360x780</c>, or nothing for the default.</summary>
    private static Vector2I? SizeFromCommandLine()
    {
        string given = Flag("--size");
        string[] parts = given.Split('x', 'X');

        return parts.Length == 2
               && int.TryParse(parts[0], out int across)
               && int.TryParse(parts[1], out int down)
               && across > 0
               && down > 0
            ? new Vector2I(across, down)
            : null;
    }

    /// <summary>
    /// Keeps a row from growing wider than a thumb can travel, without forcing that width on a screen that
    /// is narrower than it.
    /// </summary>
    /// <remarks>
    /// Godot sizes a control by its minimum and has no maximum, so a minimum width of the thumb span made
    /// the row hang off both edges of a 640-wide screen — and dragged the status bar out with it, because
    /// the column is as wide as its widest child. The width is a ceiling, not a floor, so the side margins
    /// are worked out again whenever the box changes shape.
    /// </remarks>
    public static MarginContainer Capped(Control inner, int widest)
    {
        MarginContainer box = new();

        inner.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(inner);

        box.Resized += () =>
        {
            int side = Mathf.Max(0, Mathf.RoundToInt((box.Size.X - widest) / 2));

            box.AddThemeConstantOverride("margin_left", side);
            box.AddThemeConstantOverride("margin_right", side);
        };

        return box;
    }

    /// <summary>A container whose padding keeps its contents inside the safe area.</summary>
    public static MarginContainer SafeAreaContainer()
    {
        MarginContainer container = new()
        {
            Name = "SafeArea",
            AnchorRight = 1,
            AnchorBottom = 1,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both
        };

        container.AddThemeConstantOverride("margin_left", SafeInsets.Left);
        container.AddThemeConstantOverride("margin_top", SafeInsets.Top);
        container.AddThemeConstantOverride("margin_right", SafeInsets.Right);
        container.AddThemeConstantOverride("margin_bottom", SafeInsets.Bottom);

        return container;
    }

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

    /// <summary>Reads <c>host:port</c>, falling back to the local server the run scripts start.</summary>
    private static void ReadServer(string value)
    {
        string fallback = ServerFromEnvironment() ?? ServerFromFile() ?? DefaultServer;
        // IPv6 는 `[주소]:포트` 로 적는다 — 아이폰 테더링은 IPv6 뿐이다.
        if (!System.Net.IPEndPoint.TryParse(value.Length > 0 ? value : fallback, out System.Net.IPEndPoint? server)
            || server.Port == 0)
        {
            GD.PushWarning($"--server 를 읽을 수 없어 {fallback} 을 씁니다: {value}");
            server = System.Net.IPEndPoint.Parse(fallback);
        }

        ServerAddress = server.Address;
        ServerPort = server.Port;
    }

    /// <summary>`LOD_SERVER=host:port`. 없으면 null.</summary>
    private static string? ServerFromEnvironment()
    {
        string value = OS.GetEnvironment(ServerVariable);

        return value.Length > 0 ? value : null;
    }

    /// <summary>
    /// 빌드에 함께 실리는 `server.cfg` 의 첫 줄(`host:port`). 실기기는 인자도 환경변수도 받을 수 없어
    /// 이것이 유일한 길이다. 파일은 커밋하지 않으므로 저장소에 기계 주소가 남지 않는다.
    /// </summary>
    private static string? ServerFromFile()
    {
        if (!Godot.FileAccess.FileExists(ServerFile))
        {
            return null;
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(ServerFile, Godot.FileAccess.ModeFlags.Read);
        string line = file?.GetLine().Trim() ?? string.Empty;

        return line.Length > 0 && !line.StartsWith('#') ? line : null;
    }

    /// <summary>
    /// 빌드에 함께 실리는 `login.cfg` 의 첫 줄(`name:secret`). `server.cfg` 와 같은 이유로 둔다 — 실기기는
    /// 아이콘을 탭해 여는 것이 전부라 `--login` 을 줄 수 없고, 그러면 화면 앞에 사람이 없을 때 아무도
    /// 들어갈 수 없다. 파일은 커밋하지 않으므로 저장소에 계정이 남지 않는다.
    /// </summary>
    private static string? LoginFromFile()
    {
        // A real device only ever has its icon tapped — whoever does that would be signed straight into
        // this account. login.cfg is a desktop rehearsal convenience for a local server with no one at the
        // keyboard, so a handheld must never read it, regardless of what an export filter lets through.
        if (OS.GetName() is "iOS" or "Android")
        {
            return null;
        }

        if (!Godot.FileAccess.FileExists(LoginFile))
        {
            return null;
        }

        using Godot.FileAccess file = Godot.FileAccess.Open(LoginFile, Godot.FileAccess.ModeFlags.Read);
        string line = file?.GetLine().Trim() ?? string.Empty;

        return line.Length > 0 && !line.StartsWith('#') ? line : null;
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

    /// <summary>
    /// The first system face that actually carries Hangul, or null when this machine has none. Names are
    /// tried in order because the engine reports a match for a family it can substitute, not only for one
    /// it has; the glyph check is what decides.
    /// </summary>
    private static SystemFont? SystemKoreanFont()
    {
        foreach (string name in SystemKoreanFonts)
        {
            SystemFont candidate = new() { FontNames = new[] { name } };

            if (candidate.HasChar('가'))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Theme BuildTheme()
    {
        Theme theme = new() { DefaultFontSize = BodyFontSize };

        if (Godot.FileAccess.FileExists(WindowsKoreanFont))
        {
            FontFile korean = new();
            korean.LoadDynamicFont(WindowsKoreanFont);
            theme.DefaultFont = korean;
            FontName = "Malgun Gothic";
        }
        else if (SystemKoreanFont() is SystemFont system)
        {
            // A desktop that is not Windows can still have a Korean face — macOS does. Saying "Hangul
            // breaks" while Hangul is plainly drawing on this very line is worse than saying nothing.
            theme.DefaultFont = system;
            FontName = $"시스템 {system.FontNames[0]}";
        }
        else
        {
            // Godot's built-in face has no Hangul, so this is the state where Korean text breaks.
            // Phones land here: neither the Windows path nor a system Korean face exists.
            FontName = "없음 — 한글이 깨집니다";
        }

        // Buttons sit over the map now. The engine default is translucent, which the floor shows straight
        // through, so every button carries its own opaque plate.
        theme.SetStylebox("normal", "Button", Greybox.Plate());
        theme.SetStylebox("hover", "Button", Greybox.Plate());
        theme.SetStylebox("pressed", "Button", Greybox.Surface());
        theme.SetStylebox("focus", "Button", Greybox.Plate());
        theme.SetStylebox("disabled", "Button", Greybox.Surface());

        return theme;
    }

    private static (int Left, int Top, int Right, int Bottom) ComputeSafeInsets(Vector2 viewport)
    {
        Vector2I screen = DisplayServer.ScreenGetSize();
        Rect2I safe = DisplayServer.GetDisplaySafeArea();

        if (screen.X <= 0 || screen.Y <= 0 || safe.Size.X <= 0 || safe.Size.Y <= 0)
        {
            return (Gutter, Gutter, Gutter, Gutter);
        }

        return (
            Gutter + Mathf.RoundToInt(safe.Position.X / (float)screen.X * viewport.X),
            Gutter + Mathf.RoundToInt(safe.Position.Y / (float)screen.Y * viewport.Y),
            Gutter + Mathf.RoundToInt((screen.X - safe.End.X) / (float)screen.X * viewport.X),
            Gutter + Mathf.RoundToInt((screen.Y - safe.End.Y) / (float)screen.Y * viewport.Y));
    }
}
