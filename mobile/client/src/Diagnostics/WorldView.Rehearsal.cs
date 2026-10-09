using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;

namespace LodClient;

/// <summary>월드 화면 — 손 없이 확인하는 시험 코드.</summary>
public sealed partial class WorldView
{
    /// <summary>
    /// Taps the first person the server shows us, once, when asked to on the command line. It goes through
    /// the same event the screen sends, so a run without a hand on it still checks the arithmetic that
    /// turns a place on the screen into a person.
    /// </summary>
    private void RehearseAPick()
    {
        // Not the moment somebody appears: the view is still sliding to where the server put us, and a tap
        // aimed before it settles lands on empty floor.
        // 리허설은 1초에 한 번씩 휘두른다. 한 번만 휘두르면 0.4초짜리 동작을 사진으로 잡기가 어렵다.
        // 화면의 버튼은 여전히 한 번 누르면 한 번이다(시안 AC-009).
        if (_rehearsedStrike >= 0 && _rehearsedStrike++ % 60 == 30)
        {
            Strike();
        }

        // 한참 뒤에 결과를 본다 — 서버가 답하는 데 한 프레임보다 오래 걸린다.
        if (_rehearsedStrike == 121 && server is not null)
        {
            foreach (Creature beast in server.Creatures)
            {
                GD.Print($"GREYBOX_STRUCK {beast.Serial} 체력 {server.Health(beast.Serial)?.ToString() ?? "모름"}");
            }

            GD.Print($"GREYBOX_SAID {server.Said}");
        }

        if (_rehearsedPick
            || !(Main.Picking || Main.Striking || Main.Lifting || Main.Saying.Length > 0)
            || _heard < 0
            || (Main.Picking && _crowd.Count + _herd.Count == 0)
            || (Main.Lifting && _dropped.Count == 0)
            // 버린 다음에 주우려면 버릴 때까지 기다려야 한다. 소지품은 90 프레임에 열린다.
            || _settling++ < (Main.Throwing && Main.Lifting ? 150 : 60))
        {
            return;
        }

        _rehearsedPick = true;

        if (Main.Striking)
        {
            _rehearsedStrike = 0;
        }

        if (Main.Saying.Length > 0)
        {
            Main.Fire(server?.SayAsync(Main.Saying, _leaving.Token));
        }

        if (Main.Lifting)
        {
            // 가장 가까운 것 하나. 그림 한가운데를 누른다 — 눈이 겨냥하는 자리와 같아야 Lift 의 셈까지
            // 확인된다. 표식이 스스로 어디가 가운데인지 말하므로 여기서 다시 세지 않는다.
            GroundMark? thing = _dropped.Values
                .OrderBy(mark => mark.Position.DistanceSquaredTo(_player.Position))
                .FirstOrDefault();

            // 무엇이 보이는지 먼저 말한다 — 안 주워질 때 자리와 그림 번호가 없으면 물어볼 것이 없다.
            foreach (GroundMark seen in _dropped.Values)
            {
                GD.Print($"GREYBOX_ONFLOOR {seen.Where.X},{seen.Where.Y} 그림 {seen.Sprite}");
            }

            if (thing is not null)
            {
                InputEventMouseButton onIt = new()
                {
                    ButtonIndex = MouseButton.Left,
                    Pressed = true,
                    Position = thing.Position + thing.Middle + _camera.Position
                };

                GetViewport().PushInput(onIt, true);
            }
        }

        if (!Main.Picking)
        {
            return;
        }

        // 괴물도 고를 수 있어야 전투가 확인된다 — 사람만 보면 아무도 없는 방에서 멈춘다. 가장 가까운
        // 쪽을 고르는 것은 사람이 하는 것과 같고, 멀리 헤매다 화면 밖으로 나간 것을 누르지 않게 해 준다.
        Actor? somebody = _herd.Values.Concat(_crowd.Values)
            .OrderBy(actor => actor.Position.DistanceSquaredTo(_player.Position))
            .FirstOrDefault();

        if (somebody is null)
        {
            return;
        }

        InputEventMouseButton tap = new()
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = somebody.Position + _camera.Position - new Vector2(0, 32)
        };

        GetViewport().PushInput(tap, true);


        // Said out loud so a run with nobody watching can be checked afterwards.
        GD.Print($"GREYBOX_PICKED {TargetName}");
    }

    private double _overheadAt = -1;

    /// <summary>
    /// With no server, stands what <c>--overhead</c> asks for over the heads, so the stack can be photographed: badges
    /// on everyone (seven on 주모 with the bar up, six on us with it down), 일음지 42 and Miss 33/115 over 주모 and the
    /// wasp every second — or, for <c>coma</c>, us in a coma with its effect 24 and a Miss that must not show.
    /// </summary>
    private void RehearseOverhead(double delta)
    {
        if (server is not null || Main.Overhead.Length == 0)
        {
            return;
        }

        int was = (int)_overheadAt;
        _overheadAt += delta;

        // 걷는 중에 건 그림이 따라오나(`--overhead follow --walk EEEE`) — 매 프레임 그림과 내 발의 차이를 찍는다. 따라오면 차이가 그대로다.
        if (Main.Overhead == "follow")
        {
            foreach (Flash flash in _camera.GetChildren().OfType<Flash>())
            {
                GD.Print($"GREYBOX_FOLLOW gap {flash.Position - _player.Position} feet {_player.Position}");
            }
        }

        if ((int)_overheadAt == was && _overheadAt > 0)
        {
            return;
        }

        // 따라오기 — 매초 내게 쿠로 그림(21)을 느리게(칸마다 0.3초) 건다.
        if (Main.Overhead == "follow")
        {
            Show(21, _player.Position, 300, _player);
            return;
        }

        // 쿠로토 — 서버가 보내는 그대로(쓴 쪽 그림 4, 속도 117)를 매초 내게 그린다. 링이 몸에 겹치는지 찍어 본다.
        if (Main.Overhead == "kuroto")
        {
            Show(4, _player.Position, 117, _player);
            return;
        }

        Ailment[] many = [new(3, 6), new(12, 1), new(27, 4), new(40, 2), new(55, 5), new(82, 3), new(101, 6)];
        List<Actor> others = [.. _camera.GetChildren().OfType<Actor>().Where(actor => actor != _player)];
        Actor? person = others.Find(actor => actor.DisplayName == "주모");
        Actor? beast = others.Find(actor => actor.DisplayName == "말벌");
        bool coma = Main.Overhead == "coma";
        int beat = (int)_overheadAt;

        _player.Ailing(coma ? [new(Lod.Mobile.Core.Art.Overhead.ComaIcon, 2), .. many[..3]] : many[..6]);
        person?.Ailing(many);
        person?.Struck(55);
        beast?.Struck(30);

        if (coma)
        {
            Show(Lod.Mobile.Core.Art.Overhead.ComaEffect, _player.Position, 100, _player);
            Show(33, _player.Position, 100, _player);
            GD.Print($"GREYBOX_OVERHEAD coma tile {_tile} at {_player.Position} comatose {Comatose}");
            return;
        }

        foreach (Actor? one in new[] { person, beast })
        {
            if (one is not null)
            {
                Show(beat % 2 == 0 ? 42 : one == person ? 33 : 115, one.Position, 100, one);
            }
        }

        // 떠오르는 숫자 넷 — 내가 준 것(말벌) · 남의 싸움(주모) · 내가 받은 것 · 회복, 나를 1번으로 친다.
        const uint self = 1;

        if (beast is not null)
        {
            _figures.Add(beast, beast.FigureStart, new Figure(2, self, 37 + beat, FigureKind.Damage), self);
        }

        if (person is not null)
        {
            _figures.Add(person, person.FigureStart, new Figure(3, 9, 12, FigureKind.Damage), self);
        }

        _figures.Add(_player, _player.FigureStart,
            beat % 2 == 0 ? new Figure(self, 2, 8, FigureKind.Damage) : new Figure(self, self, 120, FigureKind.Heal), self);
    }
}
