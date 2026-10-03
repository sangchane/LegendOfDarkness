using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

namespace LodClient;

/// <summary>게임 화면 — 아래 조작: 방향키·걷기·기술 단추·메시지 줄.</summary>
public partial class GameScreen : Control
{
    /// <summary>
    /// Keeps stepping while a direction is held, and lets the pad fade while the character walks so the floor under it
    /// shows. It comes back a moment after the last step, not between steps, or it would flicker.
    /// </summary>
    private void KeepWalking(double delta)
    {
        bool held = false;

        foreach ((ThumbButton key, Direction where) in _keys)
        {
            if (key.Held || ArrowHeld(where))
            {
                held = true;
                _world.StopGuiding();
                _world.SteeredByHand();

                if (_hold.IsNew(where))
                {
                    _hold.Pressing(looking: _world.Looking == where);

                    // 걸음 중이면 그 걸음이 끝난 뒤에 돈다 — 짧게 누른 것이 씹히지 않게.
                    if (!_world.Turn(where))
                    {
                        continue;
                    }

                    _hold.Began(where);
                }
                else
                {
                    _hold.Held(delta);
                }

                if (_hold.MayWalk)
                {
                    _world.Walk(where);
                }
            }
        }

        if (!held)
        {
            _hold.Released();
        }

        _stillFor = held || _world.Walking ? 0 : _stillFor + delta;

        float wanted = _stillFor < SettleSeconds ? WalkingAlpha : 1f;
        Color look = _pad.Modulate;
        look.A = Mathf.MoveToward(look.A, wanted, (float)(delta / FadeSeconds));
        _pad.Modulate = look;
    }

    /// <summary>
    /// Movement on the left, the attack button with the skills fanned round it on the right, status between them, inside
    /// a width capped so the two clusters never drift further apart than a thumb can travel on a very wide screen.
    /// </summary>
    /// <remarks>
    /// In landscape the row lies over the floor, so nothing in it but the buttons and the notice takes a tap — a figure
    /// standing between the pad and the fan must still be pickable.
    /// </remarks>
    private Control BuildControlRow()
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };

        // 세로 360 폭은 방향판 152 + 틈 8 + 부채꼴 184 로 꼭 찬다. 칸 사이 틈을 두 번 두면 8 이 넘쳐,
        // 세로에서는 가운데 칸 자체를 틈으로 쓴다.
        row.AddThemeConstantOverride("separation", Main.Portrait ? 0 : Main.Gutter);

        _pad = BuildMovementPad();

        if (Main.Portrait)
        {
            row.AddChild(_pad);
        }
        else
        {
            // 가로에는 기록 줄이 들어갈 자리가 없다 — 방향판 위에 얹는다(사용자, 2026-09-18). 이쪽은
            // 기록판이 아니라 잠깐 뜨는 토스트다. 중앙의 캐릭터를 가리지 않도록, 세 칸 방향판 너비를
            // 넘지 않는다. 긴 말은 그 안에서 줄바꿈하고 [대화]가 지난 말을 모두 다시 보여 준다.
            int toastWidth = MessageToastLayout.DirectionPadWidth(Main.TouchMinimum, Main.Gutter / 2);
            _messages = new MessageLog(2, wraps: true) { CustomMinimumSize = new Vector2(toastWidth, 0) };

            VBoxContainer left = new()
            {
                CustomMinimumSize = new Vector2(toastWidth, 0),
                SizeFlagsVertical = SizeFlags.ShrinkEnd,
                MouseFilter = MouseFilterEnum.Ignore
            };
            left.AddThemeConstantOverride("separation", Main.Gutter);
            left.AddChild(BuildMessageRow());
            left.AddChild(_pad);

            row.AddChild(left);

            // The empty middle takes the extra width, not the toast. That keeps the two thumb clusters at
            // opposite sides while leaving their play area clear.
            row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        }

        // 세로는 방향판과 부채꼴 사이의 틈이 이 칸이다(360 폭에 152 + 8 + 184). 가로는 기록 줄이 방향판 위로
        // 올라가 비었으므로 두지 않는다 — 두면 남는 폭을 반씩 가져가 기록 줄이 좁아진다.
        if (Main.Portrait)
        {
            row.AddChild(new Control
            {
                CustomMinimumSize = new Vector2(Main.Gutter, 0),
                MouseFilter = MouseFilterEnum.Ignore
            });
        }

        _abilities = new AbilityBar { SizeFlagsVertical = SizeFlags.ShrinkEnd };

        // [대화] — 기술 단추 왼쪽의 둥근 단추, 엔터 키(↵) 그림(사용자, 2026-09-27). 전에는 기록 줄 끝의 "대화" 글자 단추였다.
        Button said = new() { TooltipText = "대화", CustomMinimumSize = new Vector2(AbilityFan.ChatSide, AbilityFan.ChatSide), FocusMode = FocusModeEnum.None };
        Greybox.Disc(said);
        Glyph enter = new(GlyphKind.Enter, 20) { Paint = Greybox.Text };
        said.AddChild(enter);
        WindowFrame.Centre(enter, 20);
        said.Pressed += () => Chatting(true);
        _abilities.HoldChat(said);
        _abilities.Cooling = (skill, slot) => _server?.CoolingFor(skill, slot) ?? 0;
        _abilities.Standing = () => (_server?.Path, _server?.Vitals?.Level ?? 0);
        _abilities.SkillUsed += slot =>
        {
            _world.FoughtByHand();
            _world.UseSkill(slot);
        };
        _world.BarSkills = _abilities.PlacedSkills;
        _abilities.SpellUsed += slot => UseSpell(slot);
        _abilities.LoadSlots = Main.LoadAbilitySlots;
        _abilities.SaveSlots = Main.SaveAbilitySlots;

        // One tap is one blow. It does not chase and it does not repeat — the server decides whether it
        // landed, and says so in words we show below rather than guessing at damage here.
        _abilities.AttackReleased += () =>
        {
            // 사람이 직접 치면 자동 사냥은 3초 쉰다 — 끄지 않는다. 손을 떼면 다시 돈다.
            _world.FoughtByHand();
            _world.Strike();
        };

        // 0.5초 길게 누르면 자동 사냥을 켜고 끈다(위 줄의 [자동] 단추는 없앴다 — 사용자 요청, 2026-09-26).
        _abilities.AutoHuntToggleRequested += ToggleAutoHunt;

        // 자동 포션은 창 안에 숨기지 않는다 — 싸우는 중에 한 번에 닿아야 한다(사용자, 2026-09-23). 위 줄에 있던 것을
        // 기술 부채꼴 맨 위, 가장 높은 기술 칸 위로 옮겼다 — 기술 칸(48)보다 조금 작게(사용자, 2026-09-23 "기술창 제일
        // 상단쪽에 … 기술창 보다 조금 작게"). 마실 포션의 그림에 줄을 작게 적는다. 누르면 켜고 끄기, 길게 누르면 다른
        // 포션을 고른다. 줄은 설정 창에서.
        _abilities.Hold(new PotionChip(AutoPotion.Healing,
            () => Main.HealthPotion, rule => Main.SetPotions(rule, Main.ManaPotion), () => _server?.Pack ?? LayoutCheck.PretendPack), 0);
        _abilities.Hold(new PotionChip(AutoPotion.Restoring,
            () => Main.ManaPotion, rule => Main.SetPotions(Main.HealthPotion, rule), () => _server?.Pack ?? LayoutCheck.PretendPack), 1);
        _abilities.HoldComa(new ComaButton(() => _server, Notify));

        row.AddChild(_abilities);

        MarginContainer capped = Main.Capped(row, Main.ThumbSpanMaximum);
        capped.MouseFilter = MouseFilterEnum.Ignore;

        return capped;
    }

    /// <summary>
    /// Targeted spells use the figure selected in the world. Everything else sends zero, which Hades
    /// deliberately turns into the caster. Typed-input spells need their prompt UI before they are usable.
    /// </summary>
    private void UseSpell(int slot)
    {
        LearnedSpell? spell = _server?.Spells.FirstOrDefault(one => one.Slot == slot);

        if (spell is null)
        {
            return;
        }

        if (spell.TargetType is SpellTargetType.Prompt or SpellTargetType.FourDigit
            or SpellTargetType.ThreeDigit or SpellTargetType.TwoDigit or SpellTargetType.OneDigit)
        {
            Notify($"{spell.Name}: 입력 창이 필요한 마법입니다.");
            return;
        }

        // 대상 마법인데 고른 이가 없으면 나에게(SpellAim) — 전에는 "마법 대상을 먼저 누르세요" 로 거절해 호르라마·쿠로를 제게 못 걸었다.
        _world.UseSpell(spell.Slot, SpellAim.Target(spell.TargetType, _world.Target, _server?.Serial ?? 0));
    }

    /// <summary>
    /// Four directions, no diagonals: one tap is one tile, which is what this game is about, and holding a direction keeps
    /// walking (wireframes section 5). The floor is laid in diamonds, so each of them moves diagonally on screen.
    /// </summary>
    /// <remarks>
    /// A step starts the moment the key goes down rather than when it comes back up, and <see cref="KeepWalking" /> takes
    /// the next one each time a step ends while it is still down.
    /// </remarks>
    private Control BuildMovementPad()
    {
        GridContainer pad = new() { Columns = 3, SizeFlagsVertical = SizeFlags.ShrinkEnd, MouseFilter = MouseFilterEnum.Ignore };
        pad.AddThemeConstantOverride("h_separation", Main.Gutter / 2);
        pad.AddThemeConstantOverride("v_separation", Main.Gutter / 2);

        (string Glyph, Direction Where)?[] layout =
        [
            null, ("↑", Direction.North), null,
            ("←", Direction.West), null, ("→", Direction.East),
            null, ("↓", Direction.South), null
        ];

        foreach ((string Glyph, Direction Where)? key in layout)
        {
            if (key is null)
            {
                pad.AddChild(new Control
                {
                    CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum),
                    MouseFilter = MouseFilterEnum.Ignore
                });
                continue;
            }

            Direction where = key.Value.Where;

            ThumbButton button = new()
            {
                Text = key.Value.Glyph,
                CustomMinimumSize = new Vector2(Main.TouchMinimum, Main.TouchMinimum)
            };

            Greybox.Disc(button);
            button.ButtonDown += () =>
            {
                // 방향판을 누르면 길 안내는 멈춘다 — 손이 이긴다. 자동 사냥은 잠시 쉬고, 선 자리가 새 중심이 된다.
                _world.StopGuiding();
                _world.SteeredByHand();
                _world.Walk(where);
            };
            _keys.Add((button, where));

            pad.AddChild(button);
        }

        return pad;
    }

    /// <summary>PC 방향키 — 방향판을 누른 채인 것과 같다. 글을 쓰는 중이면 글자 칸의 것이다.</summary>
    private bool ArrowHeld(Direction where) =>
        GetViewport().GuiGetFocusOwner() is not (LineEdit or TextEdit)
        && Input.IsKeyPressed(where switch
        {
            Direction.North => Key.Up,
            Direction.East => Key.Right,
            Direction.South => Key.Down,
            _ => Key.Left,
        });

    /// <summary>PC 숫자키 1~9 — 기술(또는 마법) 칸을 차례대로 쓴다(사용자, 2026-10-03). 글자 칸이 먹은 키는 여기 오지 않는다.</summary>
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } press && press.Keycode is >= Key.Key1 and <= Key.Key9)
        {
            _abilities.UseNth((int)(press.Keycode - Key.Key1));
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>The messages with the button that opens what was said — the lines fade, this brings them back.</summary>
    private Control BuildMessageRow()
    {
        HBoxContainer row = new() { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", Main.Gutter);

        // 대화 단추는 손댈 자리를 지키려고 48 높이를 요구한다(TouchMinimum) — 여기서 ShrinkEnd 를 안 주면
        // 줄이 한 줄뿐이어도 판이 그 48 높이까지 늘어나 아래에 빈 검정이 남는다. 세로는 이미 LogHeight 만큼
        // 커스텀 최소 높이를 주므로(단추의 48보다 커) 줄지 않는다 — 그대로다.
        _messages.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _messages.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        _messages.Tapped += () => Chatting(true);
        row.AddChild(_messages);

        // [대화] 는 이 줄이 아니라 기술 단추 왼쪽에 선다 — 글자 대신 엔터 키 그림(사용자, 2026-09-27, AbilityBar.HoldChat).
        return row;
    }

    private static Label Aux(string text)
    {
        Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", AuxFontSize);
        label.AddThemeColorOverride("font_color", Greybox.Muted);

        return label;
    }
}
