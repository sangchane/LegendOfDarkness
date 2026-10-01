using System.Buffers.Binary;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>월드 연결 — 받은 패킷 하나하나를 상태에 반영하는 갈래(<see cref="Listen" /> 가 고른다).</summary>
public sealed partial class WorldClient
{
    /// <summary>
    /// 화면이 꺼내 가는 큐의 상한. 봇은 꺼내지 않으므로 상한이 없으면 접속해 있는 내내 쌓인다 — 넘으면 새 것을 버린다
    /// (<see cref="ChatLog" /> 의 0x0A 줄과 같은 방식). 화면은 매 프레임 꺼내므로 닿지 않는다.
    /// </summary>
    internal const int QueueKept = 1024;

    private static void Keep<T>(System.Collections.Concurrent.ConcurrentQueue<T> queue, T item)
    {
        if (queue.Count < QueueKept)
        {
            queue.Enqueue(item);
        }
    }

    /// <summary>
    /// 받은 번호를 맡는 갈래. 모르는 번호는 null — 해독하지 않고 넘긴다.
    /// 심장박동·나가기 답은 <see cref="Listen" /> 이 따로 맡는다(보내기를 기다리거나 몸이 없다).
    /// </summary>
    private Action<byte[]>? HandlerFor(byte command) => command switch
    {
        ServerOpcode.MapChanged => OnMapChanged,
        ServerOpcode.WorldMap => OnWorldMap,
        ServerOpcode.Location => OnLocation,
        ServerOpcode.OwnSerial => OnOwnSerial,
        ServerOpcode.DisplayCharacter => OnDisplayCharacter,
        ServerOpcode.BodyMotion => OnBodyMotion,
        ServerOpcode.Animation => OnAnimation,
        ServerOpcode.Sound => OnSound,
        ServerOpcode.Health => OnHealth,
        ServerOpcode.Vitals => OnVitals,
        ServerOpcode.Spoken => OnSpoken,
        ServerOpcode.Speech => OnSpeech,
        ServerOpcode.GroupAsk => OnGroupAsk,
        ServerOpcode.CompanionTie => OnCompanionTie,
        ServerOpcode.OtherProfile => OnOtherProfile,
        ServerOpcode.Profile => OnProfile,
        ServerOpcode.Cooldown => OnCooldown,
        ServerOpcode.Status => OnStatus,
        ServerOpcode.SeenStatus => OnSeenStatus,
        ServerOpcode.Figure => OnFigure,
        ServerOpcode.ShowCreatures => OnShowCreatures,
        ServerOpcode.TakeFromPack => OnTakeFromPack,
        ServerOpcode.Worn => OnWorn,
        ServerOpcode.TookOff => OnTookOff,
        ServerOpcode.AddSkill => OnAddSkill,
        ServerOpcode.AddSpell => OnAddSpell,
        ServerOpcode.RemoveSkill => OnRemoveSkill,
        ServerOpcode.RemoveSpell => OnRemoveSpell,
        ServerOpcode.AddToPack => OnAddToPack,
        ServerOpcode.CreatureWalked => OnCreatureWalked,
        ServerOpcode.Turned => OnTurned,
        ServerOpcode.Remove => OnRemove,
        ServerOpcode.Dialogue => OnDialogue,
        ServerOpcode.Sequence => OnSequence,
        _ => null,
    };

    private void OnMapChanged(byte[] body)
    {
        MapInfo map = ReadMap(body);
        _field = null;

        // 맵이 바뀌면 지난 맵의 체력 막대·누가 쳤나도 버린다 — 남겨 두면 다시 쓰이는 serial 이 묵은 값을 물려받고,
        // 오래 도는 봇에서는 끝없이 쌓인다. 같은 맵 새로고침은 그대로 둔다(괴물과 같은 규칙).
        if (_world.EnterMap(map))
        {
            _health.Clear();
            _struck.Clear();
        }
    }

    private void OnWorldMap(byte[] body)
    {
        try
        {
            _field = ReadWorldMap(body);
            _fieldShown++;
        }
        catch (ProtocolException cut)
        {
            NoteUnread($"월드맵 안내를 읽다가 끊겼습니다: {cut.Message}");
        }
    }

    private void OnLocation(byte[] body) => _world.Locate(ReadLocation(body));

    private void OnOwnSerial(byte[] body) => _world.Own(BinaryPrimitives.ReadUInt32BigEndian(body));

    private void OnDisplayCharacter(byte[] body) => _world.Show(ReadCharacter(body));

    private void OnBodyMotion(byte[] body)
    {
        if (body.Length >= 4)
        {
            Keep(_motions, ReadMotion(body));
        }
        else
        {
            _ignored++;
        }
    }

    private void OnAnimation(byte[] body)
    {
        if (body.Length >= 12)
        {
            Keep(_effects, ReadEffect(body));
        }
        else
        {
            _ignored++;
        }
    }

    private void OnSound(byte[] body)
    {
        if (body.Length < 3)
        {
            _ignored++;
            return;
        }

        // 같은 패킷이 효과음과 배경음악을 함께 나른다 — 번호가 가른다(Music).
        int number = ReadSound(body);

        if (Music.Song(number) is { } song)
        {
            Keep(_songs, song);
        }
        else
        {
            Keep(_sounds, number);
        }
    }

    /// <summary>
    /// How hurt somebody is: whose, how much of their health is left out of a hundred, and a sound to play.
    /// A full bar is written as 255 rather than 100 — that is the server saying there is nothing to show,
    /// which is also how it answers a blow that could not land.
    /// </summary>
    private void OnHealth(byte[] body)
    {
        const int fixedLength = 7;

        if (body.Length < fixedLength)
        {
            throw new ProtocolException($"체력 안내가 {fixedLength}바이트보다 짧습니다 ({body.Length}바이트).");
        }

        if (ReadHealthSound(body) is int sound)
        {
            Keep(_sounds, sound);
        }

        int left = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(4));

        if (left > 100)
        {
            return;
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body);

        _health[serial] = left;
        Keep(_hurts, (serial, left));
    }

    private void OnVitals(byte[] body) => _vitals = ReadVitals(body, _vitals);

    private void OnSpoken(byte[] body)
    {
        // A sound with no words is still this packet; there is simply nothing to show.
        if (ReadTold(body) is { } told)
        {
            _chat.Tell(told);
        }
    }

    private void OnSpeech(byte[] body) => _chat.Hear(ReadSpoken(body));

    private void OnGroupAsk(byte[] body)
    {
        if (Party.ReadAsk(body) is { } asker && _asks.Count < 8)
        {
            _asks.Enqueue(asker);
        }
    }

    private void OnCompanionTie(byte[] body)
    {
        try
        {
            switch (body.Length > 0 ? body[0] : 0)
            {
                case World.Companion.MasterKind:
                    _master = World.Companion.ReadTie(body).Tie;
                    _statuses.Clear();
                    break;
                case World.Companion.CompanionKind:
                    _companion = World.Companion.ReadTie(body).Tie;

                    if (_companion is null)
                    {
                        _companionLife = null;
                        _companionNumbers = null;
                        _companionKit = null;
                        _companionKitCount++;
                    }

                    break;
                case World.Companion.StatusesKind:
                    (uint on, IReadOnlyList<CompanionStatus> listed) = World.Companion.ReadStatuses(body);
                    _statuses[on] = listed;
                    break;
                case World.Companion.VitalsKind:
                    _companionLife = World.Companion.ReadLife(body);
                    _companionNumbers = PartyNumbers.ReadLife(body);
                    break;
                case World.Companion.MemberKind:
                    OnMember(body);
                    break;
                case World.Companion.KitKind:
                    _companionKit = World.Companion.ReadKit(body);
                    _companionKitCount++;
                    break;
            }
        }
        catch (ProtocolException cut)
        {
            NoteUnread($"0x5E: {cut.Message}");
        }
    }

    /// <summary>0x5E 종류 6 — 그룹원 한 사람의 체력·마력.</summary>
    private void OnMember(byte[] body)
    {
        PartyMemberStatus member = World.Companion.ReadMember(body);

        // serial 0 — 그룹이 끝났다(나갔거나 흩어졌다). 모두 지운다.
        if (member.Serial == 0)
        {
            _members.Clear();
            _memberNumbers.Clear();
            return;
        }

        _members[member.Serial] = member;

        if (PartyNumbers.ReadMember(body) is { } numbers)
        {
            _memberNumbers[member.Serial] = numbers;
        }
        else
        {
            _memberNumbers.TryRemove(member.Serial, out _);
        }
    }

    private void OnOtherProfile(byte[] body)
    {
        try
        {
            _seen = OtherProfile.Read(body);
        }
        catch (ProtocolException cut)
        {
            NoteUnread($"0x34: {cut.Message}");
        }
    }

    private void OnProfile(byte[] profile)
    {
        try
        {
            _path = LearnLadder.PathFromProfile(profile) ?? _path;
            _groupOpen = LearnLadder.GroupOpenFromProfile(profile) ?? _groupOpen;
            _roster = Party.ReadRoster(profile);
            _rosterCount++;
        }
        catch (ProtocolException cut)
        {
            NoteUnread($"0x39: {cut.Message}");
        }
    }

    private void OnCooldown(byte[] body)
    {
        Cooldown cooling = ReadCooldown(body);

        _cooling[(cooling.Skill, cooling.Slot)] = DateTime.UtcNow.AddSeconds(cooling.Seconds);
    }

    private void OnStatus(byte[] body)
    {
        Ailment told = ReadAilment(body);

        // 등급 0 은 풀렸다는 뜻이다(Debuff.OnEnded 가 0 을 보낸다).
        if (told.Left == 0)
        {
            _ailing.TryRemove(told.Icon, out _);
        }
        else
        {
            _ailing[told.Icon] = told;
        }
    }

    private void OnSeenStatus(byte[] body)
    {
        SeenAilment seen = ReadSeenAilment(body);

        if (seen.Left == 0)
        {
            _world.SeenAiling.TryRemove((seen.Serial, seen.Icon), out _);
        }
        else
        {
            _world.SeenAiling[(seen.Serial, seen.Icon)] = seen;
        }
    }

    private void OnFigure(byte[] body)
    {
        Figure figure = ReadFigure(body);
        Keep(_figures, figure);

        if (figure.Kind == FigureKind.Damage && figure.Source != 0)
        {
            _struck[figure.Target] = (figure.Source, DateTime.UtcNow);
        }
    }

    private void OnShowCreatures(byte[] body)
    {
        foreach (Creature creature in ReadCreatures(body))
        {
            _world.Creatures[creature.Serial] = creature;
            _world.Others.TryRemove(creature.Serial, out _);
        }
    }

    private void OnTakeFromPack(byte[] gone)
    {
        if (gone.Length >= 1)
        {
            _pack.TryRemove(gone[0], out _);
        }
    }

    private void OnWorn(byte[] body)
    {
        WornItem gear = ReadWorn(body);
        _worn[gear.Slot] = gear;
    }

    private void OnTookOff(byte[] bare)
    {
        if (bare.Length >= 1)
        {
            _worn.TryRemove(bare[0], out _);
        }
    }

    private void OnAddSkill(byte[] body)
    {
        LearnedSkill skill = ReadSkill(body);
        _skills[skill.Slot] = skill;
    }

    private void OnAddSpell(byte[] body)
    {
        LearnedSpell spell = ReadSpell(body);
        _spells[spell.Slot] = spell;
    }

    private void OnRemoveSkill(byte[] body) => _skills.TryRemove(ReadAbilitySlot(body), out _);

    private void OnRemoveSpell(byte[] body) => _spells.TryRemove(ReadAbilitySlot(body), out _);

    private void OnAddToPack(byte[] body)
    {
        InventoryItem carried = ReadPackItem(body);
        _pack[carried.Slot] = carried;
    }

    /// <summary>
    /// A step somebody took. The tile in the packet is where they were, not where they are now, so the
    /// direction has to be applied to it.
    /// </summary>
    private void OnCreatureWalked(byte[] body)
    {
        if (body.Length < 9)
        {
            throw new ProtocolException($"걸음 안내가 9바이트보다 짧습니다 ({body.Length}바이트).");
        }

        uint serial = BinaryPrimitives.ReadUInt32BigEndian(body);
        int fromX = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(4));
        int fromY = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(6));
        Direction facing = FromServer(body[8]);

        (int column, int row) = Facing.TileStep(facing);
        Tile now = new(fromX + column, fromY + row);

        // Monsters walk too, and the server uses this same packet for them. Take somebody we already know
        // to be a monster as a monster — otherwise it joins the crowd as a nameless person and stands there
        // wearing borrowed clothes on the tile its own picture is drawn on.
        if (_world.Creatures.TryGetValue(serial, out Creature? beast))
        {
            _world.Creatures[serial] = beast with { Where = now, Facing = facing };
            return;
        }

        // A step says nothing about clothes, so keep the ones we were shown rather than undressing them.
        // 모르는 serial 의 걸음은 버린다. 서버는 걸음(0x0C)을 걸음 뒤 자리로, 보여 주기(0x07·0x33)는 걸음 전 자리로
        // 곁의 사람을 골라(Sprite.Walk) 시야로 걸어 들어오는 괴물의 걸음이 먼저 온다 — 사람으로 받으면 npc-walk.png 를
        // 입은 이름 없는 사람이 괴물 자리에 선다(2026-09-24 사용자 보고). 곧 올 0x07·0x33 이 제자리에 세운다.
        if (_world.Known(serial) is { } known)
        {
            _world.Show(known with { Where = now, Facing = facing });
        }
    }

    /// <summary>
    /// Somebody turning where they stand. A monster does it right before it swings, so its blow is drawn
    /// towards whoever it hits rather than wherever it last walked.
    /// </summary>
    private void OnTurned(byte[] body)
    {
        if (body.Length < 5)
        {
            return;
        }

        (uint serial, Direction facing) = ReadTurn(body);

        if (_world.Creatures.TryGetValue(serial, out Creature? beast))
        {
            _world.Creatures[serial] = beast with { Facing = facing };
            return;
        }

        if (_world.Known(serial) is { } known)
        {
            _world.Show(known with { Facing = facing });
        }
    }

    private void OnRemove(byte[] body) => _world.Remove(BinaryPrimitives.ReadUInt32BigEndian(body));

    private void OnDialogue(byte[] body)
    {
        Dialogue talk = ReadDialogue(body);
        Talking = talk;
        _talkCount++;

        if (talk.Unread is { } cut)
        {
            NoteUnread($"0x2F: {cut}");
        }
    }

    private void OnSequence(byte[] sequence)
    {
        if (ShutsDialogue(sequence))
        {
            Talking = null;
            _talkCount++;
            return;
        }

        // 반응기 창(ReactorSequence·ReactorInputSequence)도 0x30 으로 온다. 아직 그리지 않지만 말없이 버리지는 않는다.
        string kind = sequence.Length > 0 ? $"0x{sequence[0]:X2}" : "없음";
        NoteUnread($"0x30: 닫기가 아닌 창 순서입니다 (첫 바이트 {kind}).");
    }
}
