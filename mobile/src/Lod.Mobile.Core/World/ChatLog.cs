using System.Collections.Concurrent;

namespace Lod.Mobile.Core.World;

/// <summary>
/// 서버가 한 말(0x0A)과 곁의 사람이 한 말(0x0D). 받는 실이 넣고 화면 실이 읽는다.
/// </summary>
internal sealed class ChatLog
{
    /// <summary>How many lines of what people said are kept for looking back at.</summary>
    private const int HeardKept = 60;

    private volatile string _said = string.Empty;
    private volatile int _saidCount;

    // 0x0A 를 타입 바이트와 함께 줄줄이 담는다. _said 는 마지막 한 줄뿐이라, 한 프레임에 둘이 오면(주운 것 + 경험치)
    // 하나를 잃었다.
    private readonly ConcurrentQueue<(byte Type, string Text)> _told = new();

    // 받는 쪽은 다른 실이다. 목록을 고치는 대신 새 목록으로 바꿔 끼워, 읽는 쪽이 훑는 도중에 바뀌지 않게 한다.
    private volatile IReadOnlyList<Spoken> _heard = [];
    private volatile int _heardTotal;

    /// <summary>The last thing the server said in words — a refused blow, a greeting, a warning.</summary>
    public string Said => _said;

    /// <summary>How many times it has spoken, so a reader can tell a repeat from a new line.</summary>
    public int SaidCount => _saidCount;

    /// <summary>The last lines anybody near us said, oldest first.</summary>
    public IReadOnlyList<Spoken> Heard => _heard;

    /// <summary>How many lines have been heard in all, so a screen can tell a new one from the same one again.</summary>
    public int HeardCount => _heardTotal;

    /// <summary>서버가 한 말 한 줄(0x0A)을 받는다.</summary>
    public void Tell((byte Type, string Text) told)
    {
        _said = told.Text;
        _saidCount++;

        // 읽는 쪽이 없으면 끝없이 쌓이지 않게 넉넉히 자른다.
        if (_told.Count < HeardKept)
        {
            _told.Enqueue(told);
        }
    }

    /// <summary>곁의 사람이 한 말 한 줄(0x0D)을 받는다.</summary>
    public void Hear(Spoken spoken)
    {
        // 지난 말은 다시 볼 수 있어야 하지만 접속해 있는 내내 쌓아 둘 것은 아니다.
        if (spoken.Text.Length > 0)
        {
            _heard = [.. _heard.TakeLast(HeardKept - 1), spoken];
            _heardTotal++;
        }
    }

    /// <summary>
    /// Takes the next line the server said (0x0A) with its type byte (Hades <c>ServerFormat0A.MsgType</c>), oldest
    /// first, so a screen can sort it (<see cref="MessageSort.FromServer" />) without missing any.
    /// </summary>
    public bool TakeTold(out byte type, out string text)
    {
        if (_told.TryDequeue(out (byte Type, string Text) told))
        {
            (type, text) = told;
            return true;
        }

        (type, text) = (0, string.Empty);
        return false;
    }
}
