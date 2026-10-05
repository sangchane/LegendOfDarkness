using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>게임 화면 — 위 판: 메뉴 단추·길 안내·자동 사냥 단추·맵 로딩 띠·체력/마력 막대.</summary>
public partial class GameScreen : Control
{
    /// <summary>
    /// Name and health on the left, whoever is picked out in the middle, world state and inventory on the right — each on
    /// a plate of its own that keeps it readable over the floor, with the floor showing between them.
    /// </summary>
    private Control BuildTopRow()
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        // Empty until the server names us, in step with the place name below: a made-up name on the
        // HUD is worse than none, because there is no way to tell it from a real one.
        _who = Aux(string.Empty);
        _who.ClipText = true;
        _who.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _who.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _who.CustomMinimumSize = new Vector2(48, 0);

        // 세로는 이름을 막대 위 한 줄로 — 옆에 두면 이름이 긴 만큼 판이 넓어져 같은 줄의 미니맵이 화면 밖으로 밀렸다(2026-09-26).
        // 가로도 같게(2026-09-26) — 월드맵 마름모가 맨 왼쪽에 서면서 640 가로에서 이름 옆에 막대를 두면 위 줄이 넘쳤다.
        VBoxContainer mine = new();
        mine.AddThemeConstantOverride("separation", 0);
        // 첫 줄: 이름과 그 옆 상태 아이콘 줄(버프·디버프) — 둘 다 없으면 줄째 접힌다.
        HBoxContainer headline = new() { MouseFilter = MouseFilterEnum.Ignore };
        headline.AddThemeConstantOverride("separation", Main.Gutter);
        _myStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _myStatus.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        // 첫 줄: 레벨 배지(금테 동그라미) · 금색 이름 · 금화.
        _level = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _level.AddThemeFontSizeOverride("font_size", 10);
        _level.AddThemeColorOverride("font_color", LolText);
        PanelContainer badge = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(22, 18) };
        StyleBoxFlat round = new() { BgColor = new Color("#0A1428"), BorderColor = LolCoin };
        round.SetBorderWidthAll(1);
        round.SetCornerRadiusAll(9);
        round.SetContentMarginAll(1);
        badge.AddThemeStyleboxOverride("panel", round);
        badge.AddChild(_level);
        badge.Visible = false;
        _who.VisibilityChanged += () => badge.Visible = _who.Visible;
        headline.AddChild(badge);
        headline.AddChild(_who);
        _who.AddThemeColorOverride("font_color", LolGold);
        _wealth = Aux(string.Empty);
        _wealth.AddThemeFontSizeOverride("font_size", 11);
        _wealth.AddThemeColorOverride("font_color", LolCoin);
        // 금전 숫자가 무엇인지 알 수 있게 왼쪽에 금화 그림(원작 바닥 금화 32905, 사용자 2026-10-04).
        TextureRect coin = new()
        {
            Texture = GD.Load<Texture2D>("res://assets/item/32905.png"),
            CustomMinimumSize = new Vector2(16, 12),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _wealth.VisibilityChanged += () => coin.Visible = _wealth.Visible;
        headline.AddChild(coin);
        headline.AddChild(_wealth);
        mine.AddChild(headline);
        mine.AddChild(BuildVitals());
        mine.AddChild(_myStatus);

        // 이름 줄은 이름이 오기 전에는 접는다 — 빈 줄이 판 위에 남는다. 글자는 조금 작게 — 가로 360 에서 위 줄이 한 줄 늘어난
        // 만큼 조작 줄을 밀어내지 않게(판 네 줄이 80 안에 들어야 한다).
        _who.Visible = false;
        _who.AddThemeFontSizeOverride("font_size", 12);
        // 가로도 세로와 같은 여백 — 가로에서 납작하게(compact) 눌러 체력·마력 판의 비율이 틀어졌다(사용자 2026-10-04).
        row.AddChild(LolPlated(mine, compact: false));

        // Whoever is picked out, in the middle where the original kept it. Empty until somebody is.
        _target = Aux(string.Empty);

        _targetHealth = new ProgressBar
        {
            CustomMinimumSize = new Vector2(Main.Portrait ? 48 : 72, 10),
            MaxValue = 100,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Visible = false
        };
        _targetHealth.AddThemeStyleboxOverride("background", Greybox.Surface());
        _targetHealth.AddThemeStyleboxOverride("fill", Greybox.Fill());

        HBoxContainer picked = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        picked.AddThemeConstantOverride("separation", Main.Gutter / 2);
        picked.AddChild(_target);
        picked.AddChild(_targetHealth);

        // 고른 이가 없으면 판째로 숨긴다 — 빈 판이 바닥 한가운데를 가린다. 가로는 위 줄 가운데, 세로는 둘째 줄 왼쪽(첫 줄
        // 오른쪽은 미니맵 자리다).
        CenterContainer middle = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _targetPlate = Plated(picked);
        _targetPlate.Visible = false;
        middle.AddChild(_targetPlate);

        // 곳 이름은 미니맵 아래 구석에 적는다(MinimapView) — 가로 위 줄에 따로 두던 판은 뺐다(2026-09-26).
        _place = Aux(string.Empty);

        // 위 줄 단추(2026-09-26, 장비 2026-10-01): [월드맵] · [인벤토리] · [장비] · [설정]. [종료]는 설정 창 제목 줄의 [로그아웃]으로, [길]은 미니맵이 되었다.
        HBoxContainer actions = new() { MouseFilter = MouseFilterEnum.Ignore };
        actions.AddThemeConstantOverride("separation", Main.Gutter);

        // 위 메뉴는 그림 + 아래 글자, 반투명 원에 금테만 — 뒤가 비친다(사용자 2026-10-02).
        Button pack = MenuButton("인벤토리", "res://assets/item/40999.png", pixel: true);
        pack.Pressed += () => Carrying(!_pack.Visible);
        actions.AddChild(pack);

        // 장비는 소지품 탭에서 빼서 따로 연다(사용자, 2026-10-01).
        Button gear = MenuButton("장비", "res://assets/item/32786.png", pixel: true);
        gear.Pressed += () => Dressing(!_gearPanel.Visible);
        actions.AddChild(gear);

        // 월드맵은 인벤토리·설정과 같은 보통 단추(2026-09-26 3차 — 2차의 마름모 단추는 요청을 잘못 읽은 것이었다. 맨 왼쪽으로
        // 가는 것은 미니맵이다). 누르면 카드형 월드맵.
        _map = MenuButton("월드맵", "res://assets/ui/menu-map.png");
        _map.Pressed += () => Main.Fire(_server?.OpenFieldAsync(System.Threading.CancellationToken.None));
        actions.AddChild(_map);
        actions.MoveChild(_map, 0);

        Button settings = MenuButton("설정", "res://assets/ui/menu-settings.png");
        settings.Pressed += () => SetWindow(GameWindow.Settings, !_settings.Visible);
        actions.AddChild(settings);

        if (Main.Portrait)
        {
            // 세로: 첫 줄 = 둥근 미니맵(맨 왼쪽) · 내 판, 둘째 줄 = 고른 이 · 월드맵 · 인벤토리 · 설정.
            row.AddChild(_minimap);
            row.MoveChild(_minimap, 0);

            HBoxContainer second = new() { MouseFilter = MouseFilterEnum.Ignore };
            second.AddThemeConstantOverride("separation", Main.Gutter);
            middle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            second.AddChild(middle);
            second.AddChild(actions);

            VBoxContainer top = new() { MouseFilter = MouseFilterEnum.Ignore };
            top.AddThemeConstantOverride("separation", Main.Gutter);
            top.AddChild(row);
            top.AddChild(second);

            return top;
        }

        // 가로: 미니맵(맨 왼쪽) · 내 판 · 고른 이(가운데) · 월드맵 · 인벤토리 · 설정, 한 줄.
        row.AddChild(_minimap);
        row.MoveChild(_minimap, 0);
        row.AddChild(middle);
        row.AddChild(actions);

        return row;
    }

    /// <summary>Exits and standing NPCs for every drawn map (<c>scripts/gen/client/build-client-guide.py</c>). Empty when not shipped.</summary>
    private static MapGuide LoadGuide()
    {
        const string path = "res://assets/world/guide.txt";

        return Godot.FileAccess.FileExists(path) ? MapGuide.Read(Godot.FileAccess.GetFileAsString(path)) : MapGuide.Empty;
    }

    /// <summary>
    /// The small plate that says where we are being walked to, with a 멈춤 on it — under the top row, in the middle,
    /// where nothing else sits. Only while guiding and the map is closed; the map window says the same itself.
    /// </summary>
    private Control BuildGuideChip()
    {
        // 작게, 뒤가 비치게(사용자 2026-10-05) — 위 메뉴 원처럼 반투명 검정 + 어두운 금테.
        HBoxContainer inside = new();
        inside.AddThemeConstantOverride("separation", Main.Gutter / 2);
        _guideText = new Label { VerticalAlignment = VerticalAlignment.Center };
        _guideText.AddThemeColorOverride("font_color", LolText);
        _guideText.AddThemeFontSizeOverride("font_size", 12);

        Button stop = new() { Text = "멈춤", CustomMinimumSize = new Vector2(44, 28), FocusMode = FocusModeEnum.None };
        StyleBoxFlat stopPlate = new() { BgColor = new Color(0, 0, 0, 0.35f), BorderColor = LolGoldDark };
        stopPlate.SetBorderWidthAll(1);
        stopPlate.SetCornerRadiusAll(14);
        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
        {
            stop.AddThemeStyleboxOverride(state, stopPlate);
        }
        stop.AddThemeFontSizeOverride("font_size", 12);
        stop.AddThemeColorOverride("font_color", LolText);
        stop.Pressed += () => _world.StopGuiding();

        inside.AddChild(_guideText);
        inside.AddChild(stop);

        StyleBoxFlat glass = new() { BgColor = new Color(0, 0, 0, 0.4f), BorderColor = LolGoldDark };
        glass.SetBorderWidthAll(1);
        glass.SetCornerRadiusAll(16);
        glass.ContentMarginLeft = 10;
        glass.ContentMarginRight = 3;
        glass.ContentMarginTop = 2;
        glass.ContentMarginBottom = 2;
        PanelContainer plate = new();
        plate.AddThemeStyleboxOverride("panel", glass);
        plate.AddChild(inside);

        CenterContainer holder = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        holder.AnchorLeft = 0;
        holder.AnchorRight = 1;
        holder.AddChild(plate);
        _topRow.Resized += () =>
        {
            holder.OffsetTop = _topRow.Position.Y + _topRow.Size.Y + Main.Gutter;
            holder.OffsetBottom = holder.OffsetTop + 32;
        };

        return holder;
    }

    /// <summary>자동 사냥을 켜고 끄고, 켰다·껐다 한 줄로 알린다 — 공격 단추를 0.5초 길게 눌러도(<see cref="AbilityBar.AutoHuntToggleRequested" />)
    /// <c>--auto-hunt</c> 로 스스로 눌러도 같은 문을 지난다.</summary>
    private void ToggleAutoHunt()
    {
        _world.SetAutoHunt(!_world.AutoHunting);
        Notify(_world.AutoHunting
            ? $"자동 사냥을 켰습니다 — 이 자리에서 {Main.AutoHuntSettings.Radius}칸 안."
            : "자동 사냥을 껐습니다.");
    }

    /// <summary>
    /// 공격 단추의 모양을 자동 사냥과 맞춘다 — 켜짐은 테두리 + "자동" 글자, 손이 잠시 조작 중이면 흐리게
    /// (<see cref="AbilityBar.ShowAutoHunt" />). <c>--auto-hunt</c> 면 자리를 잡은 뒤 한 번 스스로 켠다.
    /// </summary>
    private Button _botToggle = null!;
    private StyleBoxFlat _botRing = null!;
    private TextureRect _botPicture = null!;
    private static readonly Color BotOff = new(0.55f, 0.55f, 0.55f, 0.8f);
    private bool? _botDrawn;

    /// <summary>
    /// 메인 메뉴 아래 오른쪽의 작은 단추 둘 — [접속자] · [봇 켬/끔](사용자 2026-10-04). 메뉴 줄에 세우면 세로 360 에서 위 줄이 넘쳤다 —
    /// 다른 칸을 밀지 않게 덮는 층에 띄운다(Cover). 봇 설정은 설정 창 봇 탭 그대로.
    /// 모양은 위 메뉴처럼 반투명 원 + 금테(2026-10-05 — 원작 돌판 40px 이 커서 자동 물약 칩을 덮었다).
    /// </summary>
    private Control BuildSideButtons()
    {
        HBoxContainer side = new() { MouseFilter = MouseFilterEnum.Ignore };
        side.AddThemeConstantOverride("separation", Main.Gutter / 2);

        TextureRect person = new()
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/menu-users.png"),
            CustomMinimumSize = new Vector2(20, 20),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        Button users = SideButton(person, "접속자", out _);
        users.Pressed += () => SetWindow(GameWindow.Users, !_users.Visible);
        side.AddChild(users);

        // 봇은 원작 펫 그림(펫-강시 40889) — 따라다니는 작은 사람(사용자 2026-10-05: 선 그림은 별로).
        _botPicture = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/item/40889.png"),
            CustomMinimumSize = new Vector2(26, 26),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            Modulate = BotOff,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _botToggle = SideButton(_botPicture, "봇 켬/끔", out _botRing);
        _botToggle.Pressed += () => _settings.Companion.EmitSignal(BaseButton.SignalName.Pressed);
        side.AddChild(_botToggle);

        return side;
    }

    /// <summary>
    /// 위 메뉴 단추처럼 반투명 원에 금테(사용자 2026-10-05) — 글자 없이 작게, 자동 물약 칩을 덮지 않게.
    /// </summary>
    private static Button SideButton(Control inside, string tip, out StyleBoxFlat ring)
    {
        Button button = new() { CustomMinimumSize = new Vector2(SideButtonSize, SideButtonSize), FocusMode = FocusModeEnum.None, TooltipText = tip };

        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        StyleBoxFlat circle = new() { BgColor = new Color(0, 0, 0, 0.35f), BorderColor = LolGoldDark };
        circle.SetBorderWidthAll(1);
        circle.SetCornerRadiusAll(SideButtonSize / 2);
        PanelContainer plate = new() { MouseFilter = MouseFilterEnum.Ignore };
        plate.AddThemeStyleboxOverride("panel", circle);
        plate.SetAnchorsPreset(LayoutPreset.FullRect);
        CenterContainer middle = new() { MouseFilter = MouseFilterEnum.Ignore };
        middle.AddChild(inside);
        plate.AddChild(middle);
        button.AddChild(plate);

        button.ButtonDown += () => circle.BorderColor = LolGold;
        button.ButtonUp += () => circle.BorderColor = circle.BorderWidthLeft > 1 ? LolGold : LolGoldDark;
        ring = circle;

        return button;
    }

    /// <summary>위 메뉴 원(40)보다 작게 — 엄지로 누를 만큼은.</summary>
    private const int SideButtonSize = 32;

    /// <summary>봇이 따라오면 금테를 굵고 밝게, 그림은 제 색 — 꺼지면 흐리게.</summary>
    private void PaintBotToggle(bool on)
    {
        _botDrawn = on;
        _botRing.SetBorderWidthAll(on ? 2 : 1);
        _botRing.BorderColor = on ? LolGold : LolGoldDark;
        _botPicture.Modulate = on ? Colors.White : BotOff;
    }

    private void KeepAutoHuntButton()
    {
        if ((_server?.Companion is not null) != _botDrawn)
        {
            PaintBotToggle(_server?.Companion is not null);
        }

        if (Main.AutoHuntOnStart && _autoHuntSettling >= 0 && _world.MapId > 0 && _server?.Vitals is not null
            && ++_autoHuntSettling == 120)
        {
            _autoHuntSettling = -1;
            ToggleAutoHunt();
            GD.Print("GREYBOX_AUTOHUNT 켬");
        }

        bool on = _world.AutoHunting;
        bool paused = _world.AutoHuntPaused;

        if (on != _autoHuntDrawn || paused != _autoHuntPausedDrawn)
        {
            _autoHuntDrawn = on;
            _autoHuntPausedDrawn = paused;
            _abilities.ShowAutoHunt(on, paused);
        }
    }

    /// <summary>Keeps the guide plate in step, and — hands-free only — opens the map, taps a place on it, and closes it.</summary>
    private void KeepGuiding(double delta)
    {
        string? going = _world.Guiding;
        _guideChip.Visible = going is not null && !_tabMap.Visible && _world.Route.Count > 0;

        if (_guideChip.Visible)
        {
            _guideText.Text = $"→ {(going!.Length > 0 ? going : "고른 자리")} · {_world.Route.Count}걸음";
        }

        if (!Main.OpeningTabMap)
        {
            return;
        }

        // 옆 단추와 같은 규칙 — 미니맵 자신의 눌림으로 연다. 월드가 자리를 잡고 이 맵의 벽을 읽은 뒤에.
        if (_tabMapOpenFor < 0 && (_world.MapId > 0 || _server is null) && _tabMapSettling++ == 90)
        {
            _minimap.EmitSignal(BaseButton.SignalName.Pressed);
            _tabMapOpenFor = 0;
        }

        if (_tabMapOpenFor < 0)
        {
            return;
        }

        _tabMapOpenFor += delta;

        if (Main.TabMapGo.Length > 0 && !_tabMapWent && _tabMapOpenFor >= 2 && _tabMap.PointOf(Main.TabMapGo) is { } spot)
        {
            _tabMapWent = true;
            _tabMap.TapAt(spot);
        }

        if (Main.TabMapZoom && _tabMapOpenFor >= 1 && !_tabMap.Zoomed)
        {
            _tabMap.Zoom.EmitSignal(BaseButton.SignalName.Pressed);
        }

        if (Main.TabMapCloseAfter >= 0 && _tabMap.Visible && _tabMapOpenFor >= Main.TabMapCloseAfter)
        {
            _tabMap.Close.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    // 맵이 바뀔 때 원작 「Loading Map」 띠(LoadingBand) — 맵 그림은 앱 안에 있어 금방이므로, 홈이 다 차는 0.5초만 보인다.
    private readonly LoadingBand _loadingMap = new(map: true);

    private int _loadedMap = -1;

    private double _mapLoading = -1;

    private void ShowMapLoading(double delta)
    {
        if (_world.MapId != _loadedMap)
        {
            _loadedMap = _world.MapId;
            _mapLoading = _loadedMap > 0 ? 0 : -1;
        }

        if (_mapLoading < 0)
        {
            return;
        }

        _mapLoading += delta;
        _loadingMap.Show((float)(_mapLoading / 0.5));

        if (_mapLoading >= 0.6)
        {
            _loadingMap.Visible = false;
            _mapLoading = -1;
        }
    }

    /// <summary>
    /// The top-right plate in the LoL client's dress: near-opaque blue-black, corners cut at 45° (not rounded), a dark gold
    /// rim with a thin bright gold line inside it (사용자 2026-10-02: 테두리가 촌스럽다 → 롤 같게).
    /// </summary>
    private static Control LolPlated(Control inside, bool compact)
    {
        StyleBoxFlat outer = new() { BgColor = LolBack, BorderColor = LolGoldDark, CornerDetail = 1 };
        outer.SetBorderWidthAll(2);
        outer.SetCornerRadiusAll(7);
        outer.SetContentMarginAll(1);

        StyleBoxFlat line = new() { DrawCenter = false, BorderColor = LolGold, CornerDetail = 1 };
        line.SetBorderWidthAll(1);
        line.SetCornerRadiusAll(6);
        line.SetContentMarginAll(5);
        // 가로는 판이 미니맵 높이(76) 안에 들어야 해 위아래 여백을 뺀다.
        if (compact)
        {
            outer.SetContentMarginAll(0);
            line.ContentMarginTop = line.ContentMarginBottom = 0;
        }

        PanelContainer inner = new();
        inner.AddThemeStyleboxOverride("panel", line);
        inner.AddChild(inside);

        PanelContainer plate = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        plate.AddThemeStyleboxOverride("panel", outer);
        plate.AddChild(inner);

        return plate;
    }

    /// <summary>
    /// A top-menu button: a picture in a see-through dark ring with a thin dark-gold rim, its name small underneath — no
    /// plate, so the world shows through. Original item pictures (<paramref name="pixel" />) are enlarged to fill the ring
    /// with the nearest pixel; the drawn ones are shrunk to fit.
    /// </summary>
    private static Button MenuButton(string name, string art, bool pixel = false)
    {
        Button button = new() { CustomMinimumSize = new Vector2(56, 56), FocusMode = FocusModeEnum.None, TooltipText = name };

        foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
        {
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        }

        StyleBoxFlat ring = new() { BgColor = new Color(0, 0, 0, 0.35f), BorderColor = LolGoldDark };
        ring.SetBorderWidthAll(1);
        ring.SetCornerRadiusAll(20);
        PanelContainer circle = new()
        {
            CustomMinimumSize = new Vector2(40, 40),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore
        };
        circle.AddThemeStyleboxOverride("panel", ring);

        TextureRect picture = new()
        {
            Texture = ResourceLoader.Exists(art) ? GD.Load<Texture2D>(art) : null,
            // 원작 아이템 그림(25px 안팎)은 원 안을 채우도록 키운다(사용자 2026-10-02) — 화소가 뭉개지지 않게 가장 가까운 화소로.
            CustomMinimumSize = new Vector2(pixel ? 36 : 30, pixel ? 36 : 30),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = pixel ? TextureFilterEnum.Nearest : TextureFilterEnum.Linear,
            MouseFilter = MouseFilterEnum.Ignore
        };
        circle.AddChild(picture);

        Label word = new() { Text = name, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
        word.AddThemeFontSizeOverride("font_size", 11);
        word.AddThemeColorOverride("font_color", LolText);
        word.AddThemeColorOverride("font_outline_color", Colors.Black);
        word.AddThemeConstantOverride("outline_size", 3);

        VBoxContainer stack = new() { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        stack.AddThemeConstantOverride("separation", 0);
        stack.SetAnchorsPreset(LayoutPreset.FullRect);
        stack.AddChild(circle);
        stack.AddChild(word);
        button.AddChild(stack);

        // 누르는 동안 테두리가 밝은 금으로.
        button.ButtonDown += () => ring.BorderColor = LolGold;
        button.ButtonUp += () => ring.BorderColor = LolGoldDark;

        return button;
    }

    /// <summary>
    /// A panel over the world: an original stone frame with a dark, nearly opaque inside. The frame is what
    /// carries the theme; the inside is flat, because a pattern under small text is the first thing to fail.
    /// </summary>
    private static Control Plated(Control inside, bool compact = false)
    {
        PanelContainer inner = new();
        StyleBoxFlat surface = Greybox.Plate();
        if (compact) surface.ContentMarginTop = surface.ContentMarginBottom = 1;
        inner.AddThemeStyleboxOverride("panel", surface);
        inner.AddChild(inside);

        PanelContainer plate = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        plate.AddThemeStyleboxOverride("panel", Greybox.Stone());
        plate.AddChild(inner);

        return plate;
    }

    /// <summary>Our latest numbers, or rehearsal numbers offline.</summary>
    private Vitals Mine => _server is null ? LayoutCheck.PretendVitals : _server.Vitals ?? Vitals.Unknown;

    /// <summary>
    /// Health over mana, each a filling gauge with its exact numbers beside it — the wireframes always asked for
    /// both together (docs/mobile-test-v1-wireframes.md: "HP는 막대와 숫자를 함께 표시"), and each keeps its own
    /// theme colour (health's orange, mana's blue — data/ui-vault/색) so the two are told apart without reading.
    /// </summary>
    private Control BuildVitals()
    {
        VBoxContainer vitals = new() { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        vitals.AddThemeConstantOverride("separation", 0);

        // 경험치도 게이지로 — 이번 레벨에 모은 양 / 드는 양(사용자 요청 2026-09-26: "문구는 필요 없으니 게이지로 하고 필요한
        // 경험치 표기"). 서버는 남은 양만 보내므로 드는 양은 원작 표(ExperienceGauge)에서 읽는다.
        vitals.AddChild(Gauge("체력", HudHealth, out _healthBar, out _healthText, ticks: true));
        vitals.AddChild(Gauge("마력", HudMana, out _manaBar, out _manaText, ticks: true));
        vitals.AddChild(Gauge("EXP", LolCoin, out _experienceBar, out _experienceText));

        return vitals;
    }

    /// <summary>
    /// One vital: a name, a bar that fills in its theme colour, and the exact numbers on it, small (2026-09-27 — they used to
    /// stand beside it; on the bar the panel is narrower and the bar longer). The
    /// over-the-head bar (HealthBar) still carries how a fight is going; this one is the place the numbers are
    /// always exact, so the bar and the numbers are read together rather than the same thing drawn twice.
    /// </summary>
    private static Control Gauge(string name, Color paint, out ProgressBar bar, out Label text, bool ticks = false)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", Main.Gutter / 2);

        // 구슬만 두었더니 무엇을 뜻하는지 알 수 없다는 말을 들었다(사용자, 2026-09-18). 이름을 되살린다 —
        // 색은 거드는 것이지 뜻을 나르는 것이 아니다.
        Label named = Aux(name);
        named.AddThemeColorOverride("font_color", LolMuted);
        if (!Main.Portrait) named.AddThemeFontSizeOverride("font_size", GaugeFontSize);

        bar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(GaugeWidth, GaugeHeight),
            MaxValue = 1,
            ShowPercentage = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        // 롤 막대: 어두운 홈 + 얇은 회색 선, 채움은 위가 밝은 그라데이션, 체력·마력은 그 위에 얇은 눈금과 광택 한 줄.
        StyleBoxFlat trough = new() { BgColor = new Color("#050D18"), BorderColor = new Color("#3B3B3B") };
        trough.SetBorderWidthAll(1);
        bar.AddThemeStyleboxOverride("background", trough);
        bar.AddThemeStyleboxOverride("fill", LolFill(paint));

        if (ticks)
        {
            ProgressBar measured = bar;
            Control marks = new() { MouseFilter = MouseFilterEnum.Ignore };
            marks.SetAnchorsPreset(LayoutPreset.FullRect);
            marks.Draw += () =>
            {
                float filled = (float)(measured.Value / measured.MaxValue) * marks.Size.X;
                marks.DrawLine(new Vector2(1, 1.5f), new Vector2(Mathf.Max(1, filled - 1), 1.5f), new Color(1, 1, 1, 0.3f));

                for (float x = 12; x < filled - 1; x += 12)
                {
                    marks.DrawLine(new Vector2(x, 2), new Vector2(x, marks.Size.Y - 2), new Color(0, 0, 0, 0.45f));
                }
            };
            bar.ValueChanged += _ => marks.QueueRedraw();
            bar.Resized += marks.QueueRedraw;
            bar.AddChild(marks);
        }

        text = Greybox.OnBar(bar, GaugeFontSize);

        row.AddChild(named);
        row.AddChild(bar);

        return row;
    }

    /// <summary>Puts the newest health and mana on the gauges, only when they have changed.</summary>
    private void ShowVitals()
    {
        Vitals mine = Mine;

        if (mine == _shownVitals)
        {
            return;
        }

        _shownVitals = mine;
        Fill(_healthBar, _healthText, mine.Health, mine.MaximumHealth, HudHealth);
        Fill(_manaBar, _manaText, mine.Mana, mine.MaximumMana, HudMana);

        if (ExperienceGauge.Of(mine.Level, mine.ExperienceToGo) is { } exp)
        {
            _experienceBar.MaxValue = exp.Need;
            _experienceBar.Value = exp.Earned;
            // 절대 수치보다 몇 %인지(사용자 2026-10-02).
            _experienceText.Text = $"{Math.Floor(exp.Earned * 1000.0 / exp.Need) / 10:0.0}%";
        }
        else
        {
            // 99 레벨(다음이 없다)이거나 아직 레벨을 모른다. 99 면 쌓인 경험치(레벨 1부터, 99억까지 — 서버가 아래·윗자리로 나눠
            // 보낸다) — 세오·칸에게 팔아 체력·마력을 사는 양이라 금전처럼 만·억으로 보인다(사용자 2026-10-05).
            _experienceBar.MaxValue = 1;
            _experienceBar.Value = mine.Level > 0 ? 1 : 0;
            _experienceText.Text = mine.Level >= 99 ? $"보유 {GoldFormat.Short((mine.ExperienceToGo << 32) | mine.Experience)}" : string.Empty;
        }
    }

    /// <summary>
    /// Fills one vital's bar and writes its number on it. The number used to turn colour as it fell; written on the bar it
    /// now stays light (a coloured number over its own colour would vanish), and the bar's fill turns 위험(Gone) red at 15% or
    /// below instead — the length already tells "half gone". The numbers themselves are still the reading, so somebody who
    /// cannot tell the colours apart loses nothing.
    /// </summary>
    private static void Fill(ProgressBar bar, Label text, int left, int most, Color paint)
    {
        bar.MaxValue = most > 0 ? most : 1;
        bar.Value = most > 0 ? Mathf.Clamp(left, 0, most) : 0;

        text.Text = $"{left}/{most}";

        bar.AddThemeStyleboxOverride("fill", LolFill(most > 0 && left <= most * 0.15 ? Greybox.Gone : paint));
    }

    /// <summary>A bar's fill brighter at the top and darker at the bottom, as the LoL bars are.</summary>
    private static StyleBoxTexture LolFill(Color paint)
    {
        Gradient shade = new() { Colors = [paint.Lightened(0.15f), paint.Darkened(0.4f)], Offsets = [0, 1] };

        return new StyleBoxTexture
        {
            Texture = new GradientTexture2D { Gradient = shade, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 16 }
        };
    }
}
