using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.World;

namespace LodClient;

/// <summary>게임 화면 — 알림 갈래(`Route`)·토스트·들은 말·끊김과 읽지 못한 패킷 기록.</summary>
public partial class GameScreen : Control
{
    /// <summary>Says something this screen itself has to say — a refusal, a lost connection. It goes on the ticker.</summary>
    private void Notify(string line) => Route(MessageSort.Own(line));

    /// <summary>
    /// Puts one sorted line where it belongs (<see cref="MessageSort" />) and keeps it for reading back through. Every
    /// line goes to the log; only some of them go anywhere over the world.
    /// </summary>
    private void Route(Notice notice, uint speaker = 0)
    {
        if (notice.Text.Length == 0)
        {
            return;
        }

        _history.Add((notice.Channel, notice.Text));

        if (_history.Count > HistoryKept)
        {
            _history.RemoveRange(0, _history.Count - HistoryKept);
        }

        switch (notice.Place)
        {
            case MessagePlace.Ticker:
                _messages.Add(notice.Text);
                break;

            case MessagePlace.Toast:
                _toasts.Add(notice.Short);
                break;

            case MessagePlace.Banner:
                _banner.Show(notice.Short, bright: notice.Short == "레벨이 올랐습니다");
                break;

            case MessagePlace.Bubble:
                _world.Speak(speaker, notice.Short);
                break;
        }

        GD.Print($"GREYBOX_MESSAGE {Time.GetTicksMsec() / 1000.0:0.0}s {notice.Place} {notice.Channel} {notice.Text}");
    }

    /// <summary>
    /// Keeps the toasts clear of the attack fan in landscape. The fan sits where the width cap puts it, not against the
    /// screen edge, so its left end is read rather than assumed.
    /// </summary>
    private void PlaceToasts()
    {
        if (Main.Portrait)
        {
            return;
        }

        float fanLeft = _abilities.GetGlobalRect().Position.X - _over.GetGlobalRect().Position.X;
        _toasts.AnchorLeft = 0;
        _toasts.AnchorRight = 0;
        _toasts.OffsetRight = fanLeft - Main.Gutter;
        _toasts.OffsetLeft = _toasts.OffsetRight - ToastWidth;
    }

    /// <summary>Names the place in the middle of the screen when the character comes into it.</summary>
    private void Entered()
    {
        string place = _world.PlaceName;

        if (place.Length == 0 || place == _bannered)
        {
            return;
        }

        _bannered = place;
        _banner.Show(place);
    }

    // 듣기가 멈춘 것을 한 번만 알린다.
    private bool _toldBroken;

    /// <summary>
    /// Says when the listening has stopped. It used to stop without a word — the character froze on screen while
    /// everything else looked fine, and nothing said why (2026-09-18 조사).
    /// </summary>
    private void Dropped()
    {
        if (_toldBroken || _server?.Broke is not { } why)
        {
            return;
        }

        _toldBroken = true;
        Notify($"연결이 끊겼습니다 — {why}");
        GD.PushError($"받기 멈춤: {why}");
    }

    // 기록에 남긴 마지막 "읽지 못한 패킷" 수.
    private int _unreadLogged;

    /// <summary>
    /// 알맹이가 읽지 못하고 버린 패킷을 기록(godot.log)에 남긴다. 화면에는 안 보이고, 서버와 클라이언트가 어긋난
    /// 곳을 나중에 찾는 단서다(2026-10-02 — 전에는 세기만 하고 아무 데도 적지 않았다).
    /// </summary>
    private void LogUnread()
    {
        if (_server is not { } server || server.UnreadCount == _unreadLogged)
        {
            return;
        }

        _unreadLogged = server.UnreadCount;
        GD.PushWarning($"읽지 못한 패킷 {_unreadLogged}번째: {server.Unread}");
    }

    /// <summary>
    /// Puts what people nearby said (0x0D) with the rest of the messages. A chant is a spell being said aloud as it is
    /// cast, not somebody talking, so it is left out.
    /// </summary>
    private void Listen()
    {
        if (_server is not { } server || server.HeardCount == _heardSeen)
        {
            return;
        }

        int missed = Math.Min(server.HeardCount - _heardSeen, server.Heard.Count);
        _heardSeen = server.HeardCount;

        foreach (Spoken spoken in server.Heard.TakeLast(missed))
        {
            // 서버가 이미 "이름: 말" 로 보낸다(Hades ServerFormat0D) — 기록에는 그대로, 머리 위에는 이름을 뗀 말만.
            if (MessageSort.FromSpeech(spoken.Kind, spoken.Text) is { } notice)
            {
                Route(notice, spoken.Serial);
            }
        }
    }
}
