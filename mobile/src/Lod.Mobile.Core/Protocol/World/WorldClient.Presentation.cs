using System.Buffers.Binary;
using System.Collections.Concurrent;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Net;
using Lod.Mobile.Core.Protocol.Login;
using Lod.Mobile.Core.Protocol;

namespace Lod.Mobile.Core.Protocol.World;

/// <summary>월드 연결 — 화면이 꺼내 가는 큐(동작·이펙트·소리·음악·맞음·숫자).</summary>
public sealed partial class WorldClient
{
    /// <summary>
    /// Takes the next figure the server said had moved its body, if any. The server tells everyone nearby
    /// when somebody swings, and this is how that reaches whatever is drawing them.
    /// </summary>
    /// <remarks>
    /// It says which motion as well, and how fast. The reference client ignores that and plays its attack for
    /// every one (map-scene.ts); telling them apart is <see cref="Art.BodyMotion" />, out of skill.tbl.
    /// </remarks>
    public bool TakeMotion([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Motion? motion) =>
        _motions.TryDequeue(out motion);

    /// <summary>Takes the next flash the server asked to be drawn, if any. Like a motion, it is a moment.</summary>
    public bool TakeEffect([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Effect? effect) =>
        _effects.TryDequeue(out effect);

    /// <summary>Takes the next sound the server asked to be played — the number is the file's name.</summary>
    public bool TakeSound(out int sound) => _sounds.TryDequeue(out sound);

    /// <summary>The next song the server asked for, if it asked. <see cref="Music.Silence" /> means stop.</summary>
    public bool TakeMusic(out int song) => _songs.TryDequeue(out song);

    /// <summary>
    /// The next blow the server told us about (0x13): whose it was and what percentage of them is left. A screen
    /// takes these to put a bar over that one's head, which is the only place the original shows how a fight is
    /// going. Serial zero is a swing that hit nothing.
    /// </summary>
    public bool TakeHurt(out uint serial, out int left)
    {
        if (_hurts.TryDequeue(out (uint Serial, int Left) hurt))
        {
            (serial, left) = hurt;
            return true;
        }

        (serial, left) = (0, 0);
        return false;
    }

    /// <summary>Takes the next amount a blow took or a heal gave (0x5D), oldest first.</summary>
    /// <summary>
    /// 다른 사람(우리 말고 보이는 플레이어)이 <paramref name="within"/> 안에 이 괴물을 쳤나. 원작에는 없는 0x5D 의
    /// Source 로 안다 — 서버는 가까운 사람 모두에게 보낸다(<c>ServerFormat5D</c>, Scope.VeryNearbyAislings).
    /// </summary>
    public bool StruckByOthers(uint target, TimeSpan within) =>
        _struck.TryGetValue(target, out (uint Source, DateTime At) hit)
        && hit.Source != _world.Serial
        && _world.Others.ContainsKey(hit.Source)
        && DateTime.UtcNow - hit.At <= within;

    public bool TakeFigure([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Figure? figure) => _figures.TryDequeue(out figure);
}
