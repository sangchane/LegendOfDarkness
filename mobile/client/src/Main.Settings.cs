using Godot;

namespace LodClient;

/// <summary>앱 시작 — 기기에 남기는 설정(`user://*.cfg`: 서버·로그인·자동 사냥·물약·미니맵·기술 칸)과 서버 주소 읽기.</summary>
public partial class Main : Control
{
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

        // 열기가 실패하면(권한·손상) 저장된 계정이 없는 것으로 친다 — 시작하자마자 죽지 않게.
        using Godot.FileAccess? reading = Godot.FileAccess.Open(AutoLoginFile, Godot.FileAccess.ModeFlags.Read);

        if (reading is null)
        {
            return;
        }

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

    /// <summary>미니맵이 보이는 반경(칸) — [+]·[−] 로 바꾸고 기기에 남는다(<c>user://minimap.cfg</c> 한 줄, 2026-09-26).</summary>
    private const string MinimapFile = "user://minimap.cfg";

    public static int MinimapRadius { get; private set; } = Lod.Mobile.Core.Art.Minimap.Radius;

    public static void SetMinimapRadius(int radius)
    {
        MinimapRadius = Lod.Mobile.Core.Art.Minimap.NearestStep(radius);

        Godot.FileAccess? writing = Godot.FileAccess.Open(MinimapFile, Godot.FileAccess.ModeFlags.Write);

        if (writing is not null)
        {
            writing.StoreLine(MinimapRadius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            writing.Close();
        }
    }

    private static void ReadMinimapRadius()
    {
        Godot.FileAccess? reading = Godot.FileAccess.Open(MinimapFile, Godot.FileAccess.ModeFlags.Read);

        if (reading is not null)
        {
            if (int.TryParse(reading.GetLine().Trim(), out int radius))
            {
                MinimapRadius = Lod.Mobile.Core.Art.Minimap.NearestStep(radius);
            }

            reading.Close();
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

    /// <summary>--companion: 손 없이 확인할 때, 월드가 자리를 잡으면 설정 창의 [동료 부르기] 를 한 번 스스로 누른다.</summary>
    public static bool CompanionOnStart { get; private set; }

    /// <summary>--bot-preview: 서버 없이 지어낸 봇으로 봇 칸을 그려 본다(사진·배치 검사).</summary>
    public static bool BotPreview { get; private set; }

    /// <summary>--bot-gear: --bot-preview 와 함께, 봇 장비창까지 연다.</summary>
    public static bool BotGearOpen { get; private set; }

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

    private static Lod.Mobile.Core.World.LearnLadder? _ladder;

    /// <summary>
    /// 레벨이 되면 저절로 배우는 표 — 서버와 같은 것(<c>scripts/build-auto-learn.py</c> → <c>assets/world/auto-learn.txt</c>).
    /// 기술 목록이 아직 못 배운 것에 "N레벨에 배움" 을 적는다(<see cref="AbilityBar" />). 없으면 빈 표.
    /// </summary>
    public static Lod.Mobile.Core.World.LearnLadder Ladder => _ladder ??= LoadLadder();

    private static Lod.Mobile.Core.World.LearnLadder LoadLadder()
    {
        const string path = "res://assets/world/auto-learn.txt";

        return Godot.FileAccess.FileExists(path)
            ? Lod.Mobile.Core.World.LearnLadder.Read(Godot.FileAccess.GetFileAsString(path))
            : Lod.Mobile.Core.World.LearnLadder.Empty;
    }

    public static string[] LoadAbilitySlots(string character)
    {
        string path = AbilitySlotsFile(character);

        if (!Godot.FileAccess.FileExists(path))
        {
            return [];
        }

        using Godot.FileAccess? reading = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        return reading is null ? [] : reading.GetAsText().Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>환경변수로도 준다. 데스크톱에서 인자 없이 다른 서버를 가리킬 때 쓴다.</summary>
    private const string ServerVariable = "LOD_SERVER";

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

        using Godot.FileAccess? file = Godot.FileAccess.Open(ServerFile, Godot.FileAccess.ModeFlags.Read);
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

        using Godot.FileAccess? file = Godot.FileAccess.Open(LoginFile, Godot.FileAccess.ModeFlags.Read);
        string line = file?.GetLine().Trim() ?? string.Empty;

        return line.Length > 0 && !line.StartsWith('#') ? line : null;
    }
}
