using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol.World;
using Lod.Mobile.Core;

namespace LodClient;

/// <summary>월드 화면 — 사람·괴물 세우기, 옷 입히기, 누르기·고르기 표시.</summary>
public sealed partial class WorldView
{
    private Actor Add(Actor actor, Vector2 where)
    {
        actor.Position = where;
        _camera.AddChild(actor);

        return actor;
    }

    /// <summary>
    /// A tap picks out whoever was tapped, and picks nobody when it lands on empty floor. Picking a monster or a
    /// person is all it does — attacking is a separate ask, so a mistaken tap costs nothing — but an NPC is talked to,
    /// as the original client talks to one it is clicked on.
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (Frozen && !PeopleOnly)
        {
            return;
        }

        Vector2 at;

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                at = click.Position;
                break;

            case InputEventScreenTouch { Pressed: true } touch:
                at = touch.Position;
                break;

            default:
                return;
        }

        AcceptEvent();

        // Already in this view's own coordinates; the camera says how far the world has been slid under it.
        if (Frozen)
        {
            PressPerson(at - _camera.Position);
            return;
        }

        Choose(at - _camera.Position);
    }

    private const float Reach = 34;

    private const float FigureWaist = 32;

    /// <summary>Asks the server for the window of whichever person stands nearest the tap (0x43 → 0x34), if one is near enough.</summary>
    private void PressPerson(Vector2 where)
    {
        float nearest = Reach * Reach;
        uint who = 0;

        foreach ((uint serial, Actor person) in _crowd)
        {
            float distance = (person.Position - new Vector2(0, FigureWaist)).DistanceSquaredTo(where);

            if (distance < nearest)
            {
                nearest = distance;
                who = serial;
            }
        }

        if (who != 0 && server is { } world)
        {
            Main.Fire(world.ClickAsync(who, System.Threading.CancellationToken.None));
        }
    }

    /// <summary>
    /// Whoever stands nearest the tap, if anybody stands near enough. Measured against the middle of a
    /// figure rather than its feet, because that is the part of it a thumb aims at.
    /// </summary>
    /// <remarks>
    /// Things lying on the floor are looked at first. They are small and they sit under everyone's feet,
    /// so a tap that lands on both is far likelier to have meant the thing than the figure standing over it.
    /// </remarks>
    private void Choose(Vector2 where)
    {
        if (Lift(where))
        {
            return;
        }

        float nearest = Reach * Reach;

        uint before = _target;
        _target = 0;

        // 사람·괴물은 허리를, 표식은 떠 있는 높이(NpcMark.Waist)를 겨눈다.
        IEnumerable<(uint Serial, Vector2 Aim)> standing = _crowd.Concat(_herd)
            .Select(one => (one.Key, one.Value.Position - new Vector2(0, FigureWaist)))
            .Concat(_signs.Select(sign => (sign.Key, sign.Value.Position - new Vector2(0, NpcMark.Waist))));

        foreach ((uint serial, Vector2 aim) in standing)
        {
            float distance = aim.DistanceSquaredTo(where);

            if (distance < nearest)
            {
                nearest = distance;
                _target = serial;
            }
        }

        TargetByHand = _target != 0;
        Mark();

        // 서버가 창을 보내 오면 화면이 연다(GameScreen). 여기서는 누른 것만 알린다. 사람은 이미 고른 이를 한 번 더 누를 때만 —
        // 한 번 누르는 것은 겨누기다. 그러면 서버가 그 사람 장비창(0x34)을 보낸다(사용자 2026-10-01: 장비창에서 그룹 신청).
        if (server is { } world
            && (world.Creatures.FirstOrDefault(one => one.Serial == _target) is { Kind: CreatureKind.Merchant }
                || (_target != 0 && _target == before && _crowd.ContainsKey(_target))))
        {
            Main.Fire(world.ClickAsync(_target, System.Threading.CancellationToken.None));
        }
    }

    /// <summary>
    /// One creature's sheet and the numbering that goes with it. The frames lie side by side in square
    /// cells, so a cell is as tall as the sheet and as wide as it is tall — that is the whole reason the
    /// extractor squares them. The numbering comes out of the archive too, in a text file beside the
    /// picture: every creature walks and swings on frames of its own choosing, and a creature played with
    /// the person's numbering asks for frames that are not there (docs/original-sprite-animation.md 4절).
    /// </summary>
    private static Actor.Sheet CreatureSheet(string path)
    {
        int size = GD.Load<Texture2D>(path).GetHeight();
        string beside = System.IO.Path.ChangeExtension(path, ".txt");

        CreatureMotion? motion = Godot.FileAccess.FileExists(beside)
            ? CreatureMotion.Read(Godot.FileAccess.GetFileAsString(beside))
            : null;

        return Actor.Sheet.Creature(path, size, motion);
    }

    /// <summary>
    /// Asks for whatever lies nearest the tap, and says whether there was anything to ask for. **The
    /// original has no automatic looting** — walking over a thing leaves it lying there, and only asking
    /// takes it — so this happens on a tap and at no other time.
    /// </summary>
    /// <remarks>
    /// Nothing is drawn or removed here. The server decides whether we are close enough (its
    /// <c>ClickLootDistance</c> is ten tiles) and answers by no longer showing the thing, which is what
    /// makes it disappear. A tap on bare floor sends nothing.
    /// </remarks>
    private bool Lift(Vector2 where)
    {
        if (server is null)
        {
            return false;
        }

        // 한 칸 남짓. 사람을 고르는 34 보다 좁은 것은, 빗나간 탭이 엉뚱한 것을 줍는 편이
        // 아무도 고르지 못하는 것보다 나쁘기 때문이다.
        const float reach = 24;

        float nearest = reach * reach;
        GroundMark? asked = null;

        // 그려진 것을 그대로 재려고 server.Creatures 가 아니라 표식을 돈다. 같은 칸이라도 그림 크기에
        // 따라 눌러야 할 자리가 달라진다.
        foreach (GroundMark mark in _dropped.Values)
        {
            float distance = (mark.Position + mark.Middle).DistanceSquaredTo(where);

            if (distance < nearest)
            {
                nearest = distance;
                asked = mark;
            }
        }

        if (asked is null)
        {
            return false;
        }

        Main.Fire(server.PickUpAsync(asked.Where, _leaving.Token));

        // 실제로 보낼 때만 말한다. 리허설 쪽에서 말하면 화면이 얼어 탭이 무시돼도 찍혀서,
        // 되는 줄 알고 한참 헤맸다(2026-09-11).
        GD.Print($"GREYBOX_LIFTED {asked.Where.X},{asked.Where.Y} 그림 {asked.Sprite}");

        return true;
    }

    /// <summary>Puts the mark on whoever is picked out, or takes it away.</summary>
    private void Mark()
    {
        if (_target != 0 && (_crowd.TryGetValue(_target, out Actor? actor) || _herd.TryGetValue(_target, out actor)))
        {
            // A hair above the figure so the ring sorts behind its feet rather than over them.
            _mark.Position = actor.Position - new Vector2(0, 1);
            _mark.Visible = true;

            return;
        }

        if (_target != 0 && _signs.TryGetValue(_target, out NpcMark? sign))
        {
            _mark.Position = sign.Position - new Vector2(0, 1);
            _mark.Visible = true;

            return;
        }

        _target = 0;
        _mark.Visible = false;
    }

    /// <summary>
    /// Draws everyone the server has shown us, and stops drawing the ones it has taken away.
    /// </summary>
    private void Crowd()
    {
        if (server is null)
        {
            return;
        }

        HashSet<uint> present = [];

        foreach (Character one in server.Others)
        {
            present.Add(one.Serial);

            // 갈아입었으면 그리던 것을 버리고 다시 짓는다.
            if (_crowd.TryGetValue(one.Serial, out Actor? standing)
                && _worn.TryGetValue(one.Serial, out Appearance? before)
                && before != one.Wearing)
            {
                standing.QueueFree();
                _crowd.Remove(one.Serial);
            }

            if (!_crowd.TryGetValue(one.Serial, out Actor? actor))
            {
                // The server gives a name with the appearance; a serial is only for somebody we have
                // only ever seen take a step.
                string called = one.Name.Length > 0 ? one.Name : one.Serial.ToString();

                // Somebody who changes clothes is not redressed until they leave and come back; the
                // server does say so, and this is where to listen when there is anything to wear.
                actor = Add(new Actor(called, Dress(one)), Ground(one.Where));
                _crowd[one.Serial] = actor;
                _worn[one.Serial] = one.Wearing;
            }

            actor.GoTo(Ground(one.Where), TileStep, Tuning.StepSeconds);

            // 매 프레임 돌려세우면 서 있는 그림으로 되돌아가 걷는 동작이 지워진다. 바뀔 때만.
            if (actor.Looking != one.Facing)
            {
                actor.Face(one.Facing);
            }
        }

        foreach (uint serial in _crowd.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _crowd[serial].QueueFree();
            _crowd.Remove(serial);
            _worn.Remove(serial);
        }

        Mark();
    }

    /// <summary>
    /// The sheets one person is drawn from. The equipment panel draws the same figure in its middle, so
    /// this is shared rather than written twice — one wardrobe, one set of rules about layer order. A piece we
    /// have no picture for is left out rather than left blank — the wardrobe in this repository only holds what
    /// the world can currently hand out.
    /// </summary>
    internal static Actor.Sheet Dress(Character one)
    {
        if (one.Wearing is null)
        {
            return Actor.Sheet.Walk(OtherSheet);
        }

        List<string> paths = [];
        List<int> colours = [];
        List<string> striking = [];
        List<char> parts = [];

        foreach (Piece piece in Wardrobe.Pieces(one.Wearing))
        {
            string path = $"{PartsFolder}{piece.Name}.png";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            paths.Add(path);
            colours.Add(piece.Colour);
            parts.Add(piece.Name[1]);

            // A piece with no swing of its own is left out while the rest of the figure swings (Actor.Play).
            string swung = $"{PartsFolder}{piece.Name}02.png";
            striking.Add(ResourceLoader.Exists(swung) ? swung : path);
        }

        return paths.Count > 0 ? Actor.Sheet.Walk(paths, colours, striking, parts) : Actor.Sheet.Walk(OtherSheet);
    }

    /// <summary>
    /// Puts our own figure into what the server says we are wearing. It describes us like anybody else, but
    /// only after we are already standing there, so the figure has to be built again once it does.
    /// </summary>
    private void Wear()
    {
        if (server?.Self is not { Wearing: not null } mine)
        {
            return;
        }

        if (_dressedOnce && _wearingOwn == mine.Wearing)
        {
            return;
        }

        _wearingOwn = mine.Wearing;
        _dressedOnce = true;

        Direction looking = _player.Looking;

        Actor dressed = new(mine.Name.Length > 0 ? mine.Name : _player.DisplayName, Dress(mine))
        {
            Position = _player.Position
        };

        _camera.AddChild(dressed);
        _player.QueueFree();

        _player = dressed;
        _player.Face(looking);
    }

    /// <summary>
    /// Draws the monsters and merchants the server has shown us, and stops drawing the ones it has taken
    /// away. Same shape as the crowd, but each of these brings its own drawing rather than a wardrobe.
    /// </summary>
    private void Herd()
    {
        if (server is null)
        {
            return;
        }

        HashSet<uint> present = [];

        foreach (Creature one in server.Creatures)
        {
            present.Add(one.Serial);

            if (one.Kind == CreatureKind.Passable)
            {
                if (!_dropped.TryGetValue(one.Serial, out GroundMark? mark))
                {
                    mark = new GroundMark
                    {
                        Name = $"Dropped{one.Serial}",
                        Picture = ItemIcons.For(one.Sprite)
                    };

                    _camera.AddChild(mark);
                    _dropped[one.Serial] = mark;
                }

                mark.Where = one.Where;
                mark.Sprite = one.Sprite;
                mark.Count = one.Count;
                // 같은 칸이면 금화를 먼저(아래에) 그린다 — 사용자 2026-09-26 "돈은 항상 아이템 밑에".
                mark.Position = Ground(one.Where) + new Vector2(0, GroundPile.SortNudge(one.Sprite));

                continue;
            }

            if (!_herd.TryGetValue(one.Serial, out Actor? actor))
            {
                string path = $"{CreatureFolder}mns{one.Sprite - CreatureNumbering:000}.png";

                if (!ResourceLoader.Exists(path))
                {
                    // A merchant with no picture is one of the pack's script NPCs: it gets a sign so it can be found and
                    // tapped (NpcMark). Anything else with nothing cut yet stays off the floor — better an empty tile
                    // than a wrong picture.
                    if (one.Kind == CreatureKind.Merchant)
                    {
                        if (!_signs.TryGetValue(one.Serial, out NpcMark? sign))
                        {
                            sign = new NpcMark { Name = $"Sign{one.Serial}" };
                            _camera.AddChild(sign);
                            _signs[one.Serial] = sign;
                        }

                        sign.Position = Ground(one.Where);
                    }

                    continue;
                }

                string called = one.Name.Length > 0 ? one.Name : one.Serial.ToString();

                actor = Add(new Actor(called, CreatureSheet(path)), Ground(one.Where));
                _herd[one.Serial] = actor;
            }

            actor.GoTo(Ground(one.Where), TileStep, Tuning.StepSeconds);

            // 매 프레임 돌려세우면 서 있는 그림으로 되돌아가 걷는 동작이 지워진다. 바뀔 때만.
            if (actor.Looking != one.Facing)
            {
                actor.Face(one.Facing);
            }
        }

        foreach (uint serial in _herd.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _herd[serial].QueueFree();
            _herd.Remove(serial);
        }

        foreach (uint serial in _dropped.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _dropped[serial].QueueFree();
            _dropped.Remove(serial);
        }

        foreach (uint serial in _signs.Keys.Where(known => !present.Contains(known)).ToList())
        {
            _signs[serial].QueueFree();
            _signs.Remove(serial);
        }
    }

    /// <summary>
    /// Only when checking without a hand (<c>--invite</c>): taps the person of that name where a thumb would — the
    /// middle of the figure — so the picking itself is what gets checked. False while they are not on screen.
    /// </summary>
    public bool TapPerson(string name)
    {
        uint serial = server?.Others.FirstOrDefault(other => other.Name == name)?.Serial ?? 0;

        if (serial == 0 || !_crowd.TryGetValue(serial, out Actor? actor))
        {
            return false;
        }

        GetViewport().PushInput(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true,
            Position = actor.Position + _camera.Position - new Vector2(0, 32)
        }, true);

        return true;
    }

    /// <summary>The armour number somebody is shown wearing, 0 when bare or not described.</summary>
    private static int ArmourOf(WorldClient world, uint serial) =>
        (serial == world.Serial ? world.Self : world.Others.FirstOrDefault(other => other.Serial == serial))?.Wearing?.Armor ?? 0;

    private Actor? Someone(WorldClient world, uint serial) =>
        serial == world.Serial ? _player
        : _crowd.TryGetValue(serial, out Actor? person) ? person
        : _herd.TryGetValue(serial, out Actor? beast) ? beast
        : null;
}
