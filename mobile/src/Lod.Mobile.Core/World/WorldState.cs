using System.Collections.Concurrent;

namespace Lod.Mobile.Core.World;

/// <summary>
/// 지금 보이는 세계 — 맵과 내 자리, 나, 남, 괴물, 남에게 걸린 것. 받는 실이 쓰고 그리는 실이 읽는다.
/// </summary>
internal sealed class WorldState
{
    // 받는 실만 쓰고 읽는다.
    private MapInfo? _map;
    private Tile? _where;

    // Written by the pump, read by whoever is drawing. A whole state at once, so a reader never sees a map
    // from one moment and a tile from another.
    private volatile WorldEntry? _entry;
    private volatile int _reports;
    private volatile uint _serial;
    private volatile Character? _self;

    /// <summary>Keyed by serial, which is the only name the server gives them at first.</summary>
    public readonly ConcurrentDictionary<uint, Character> Others = new();

    /// <summary>Everything on the floor that is not a player, by the same serial the server removes them by.</summary>
    public readonly ConcurrentDictionary<uint, Creature> Creatures = new();

    /// <summary>What is on everybody else we can see, by who and which picture (0x5C).</summary>
    public readonly ConcurrentDictionary<(uint Serial, int Icon), SeenAilment> SeenAiling = new();

    public WorldEntry? Entry => _entry;

    public int Reports => _reports;

    public uint Serial => _serial;

    public Character? Self => _self;

    /// <summary>
    /// 맵이 왔다(0x15). 맵이 바뀌었으면 참을 돌려준다 — 같은 맵 새로고침은 거짓.
    /// </summary>
    public bool EnterMap(MapInfo map)
    {
        int before = _map?.Id ?? -1;
        _map = map;
        SeenAiling.Clear();

        // 맵이 바뀌면 보던 것을 모두 버린다 — 남겨 두면 지난 맵 괴물이 새 맵 위에 선다. 같은 맵 새로고침
        // (막힌 걸음·속도 초과가 부르는 GameClient.Refresh)에는 버리지 않는다: 서버는 곁의 것을 곧 0x07 로 다시
        // 보낼 뿐이고, 버리면 그때까지 괴물이 모두 사라졌다가 돌아온다(사용자 2026-09-25 "보였다가 사라진다").
        // 시야 밖이 된 것은 서버가 0x0E 로 거둔다.
        bool changed = map.Id != before;

        if (changed)
        {
            Creatures.Clear();
            Others.Clear();
        }

        Place();
        return changed;
    }

    /// <summary>서버가 내 자리를 말했다(0x04).</summary>
    public void Locate(Tile where)
    {
        _where = where;
        _reports++;
        Place();
    }

    /// <summary>서버가 우리 캐릭터의 번호를 말했다(0x05).</summary>
    public void Own(uint serial)
    {
        _serial = serial;

        // It may arrive after we have already been shown ourselves, in which case we are
        // standing in the crowd under our own name until now.
        if (Others.TryRemove(serial, out Character? mistaken))
        {
            _self = mistaken;
        }
    }

    /// <summary>Remembers somebody. The server shows us our own character too, and that one is kept apart.</summary>
    public void Show(Character character)
    {
        if (character.Serial == _serial)
        {
            _self = character;
            return;
        }

        Others[character.Serial] = character;
    }

    /// <summary>Whoever we already know by that serial, so a packet without clothes does not undress them.</summary>
    public Character? Known(uint serial) =>
        serial == _serial ? _self : Others.GetValueOrDefault(serial);

    /// <summary>시야에서 사라졌다(0x0E) — 사람이든 괴물이든, 그에게 걸린 것까지.</summary>
    public void Remove(uint gone)
    {
        Others.TryRemove(gone, out _);
        Creatures.TryRemove(gone, out _);

        foreach ((uint Serial, int Icon) key in SeenAiling.Keys.Where(key => key.Serial == gone))
        {
            SeenAiling.TryRemove(key, out _);
        }
    }

    private void Place()
    {
        if (_map is not null && _where is not null)
        {
            _entry = new WorldEntry(_map, _where.Value);
        }
    }
}
