using Godot;
using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Model;
using Lod.Mobile.Core.Ui;

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

    /// <summary>대신 사냥에 맡기려고 스스로 닫았다(<see cref="Background" />).</summary>
    private bool _handedOff;

    // 마지막 프레임 때(ms)와, 앱이 멈춰 있다 깨어났는지(프레임 사이가 Asleep 넘게 빔 — iOS 가 뒤로 보냈다).
    private ulong _lastFrameMs;
    private bool _wokeUp;
    private const ulong Asleep = 5000;

    /// <summary>
    /// 자동 사냥 중 끊겼으면 다시 들어간다 — 그동안은 대리가 사냥했다. 스스로 닫았거나(<see cref="Background" />) 앱이 멈춰 있다
    /// 깨어난 뒤의 끊김만. 앱이 돌고 있는데 끊긴 것(다른 기기의 로그인·운영자)은 되찾지 않는다 — 새 접속이 이긴다.
    /// </summary>
    private void RejoinIfHandedOff()
    {
        ulong now = Time.GetTicksMsec();
        if (_lastFrameMs > 0 && now - _lastFrameMs > Asleep) _wokeUp = true;
        _lastFrameMs = now;

        if (_leaving || Main.InBackground || !(_handedOff || (_wokeUp && _server?.Broke is not null && _world.AutoHunting)))
        {
            return;
        }

        _leaving = true;
        GD.Print(_handedOff ? "대신 사냥: 앱으로 돌아옴 — 다시 접속" : "대신 사냥: 자동 사냥 중 끊김 — 다시 접속");
        Rejoin?.Invoke();
    }

    /// <summary>앱이 뒤로 간다 — 자동 사냥 중이고 대신 사냥을 켰으면 접속을 닫아 서버가 바로 대리에게 넘기게 한다.</summary>
    public void Background()
    {
        if (_leaving || _handedOff || !_world.AutoHunting || Main.ProxyHours <= 0 || _server is null)
        {
            return;
        }

        _handedOff = true;
        GD.Print("대신 사냥: 뒤로 감 — 접속을 닫고 맡김");
        _server.Dispose();
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
