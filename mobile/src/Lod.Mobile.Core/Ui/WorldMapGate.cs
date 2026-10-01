namespace Lod.Mobile.Core.Ui;

/// <summary>
/// 월드맵 창을 언제 다시 띄우나. 월드맵은 서버가 띄우는 것(0x2E)이지 사람이 여는 것이 아니라 온 것을 그대로 보여 주되,
/// 고른 뒤와 닫은 뒤에는 서버가 정말 새 창을 보냈을 때만 다시 띄운다.
/// </summary>
public sealed class WorldMapGate
{
    // 고른 곳의 맵 번호. 0x15(맵 바뀜)가 올 때까지 담아 둔다 — 그 전에는 알맹이의 WorldClient.Field 가
    // 그대로 남아 있어, 창을 도로 띄워 두 번 고르게 하면 안 된다.
    private int? _chosen;

    // 닫기를 보냈을 때 알맹이의 FieldShown(0x2E 셈)을 담아 둔다. 서버가 창을 거두면
    // (0x15 → Field null) 또는 새 창을 보내면(FieldShown 이 오르면) 풀린다 — 둘 다 서버가 보낸
    // 신호라 시간에 기대지 않는다. 취소가 영영 유실돼 서버가 창을 안 거두면 "지도" 단추가 계속
    // 막힌다 — 닫았는데 도로 열리는 것보다 낫고, 그때는 사람이 다시 접속한다(사용자 결정).
    private int? _closedAtShown;

    /// <summary>닫기를 보내고 서버의 답을 기다리는 중. 그동안 "지도" 단추를 막는다.</summary>
    public bool Closing => _closedAtShown is not null;

    /// <summary>한 곳을 골랐다.</summary>
    public void Chose(int area) => _chosen = area;

    /// <summary>닫기를 보냈다 — 그때의 FieldShown(서버가 없으면 null).</summary>
    public void Closed(int? shown) => _closedAtShown = shown;

    /// <summary>
    /// 서버가 창을 보내 두었고(<paramref name="shown" /> 은 그 셈) 화면에 없을 때 띄울까. 한 곳을 고른 뒤에는 0x15 로
    /// 알맹이가 비울 때까지 다시 띄우지 않는다 — 서버가 맵을 새로 보내기까지 두 번의 0.5초를 거치는 동안
    /// (GameServerHandlers.cs:1885-1890) Field 가 그대로 남아 있어, 그새 창을 도로 띄우면 두 번 고를 수 있었다.
    /// 닫기를 보낸 뒤에는 FieldShown 이 그때와 달라졌을 때만 — 즉 서버가 새 창을 보냈을 때만 — 다시 띄운다.
    /// </summary>
    public bool ShouldShow(int shown, bool visible) =>
        !visible && _chosen is null && (_closedAtShown is null || shown != _closedAtShown);

    /// <summary>창을 띄웠다.</summary>
    public void Shown() => _closedAtShown = null;

    /// <summary>
    /// 서버가 창을 거뒀다(Field 가 비었다). 보내기가 실패해 서버가 영영 맵을 안 바꾸면(고르기도, 닫기도) 창이 다시 안 뜬다 —
    /// 두 번 이동하거나 닫았는데 도로 열리는 것보다 안 뜨는 편이 낫다고 보고, 그때는 사람이 다시 접속한다.
    /// </summary>
    public void Withdrawn()
    {
        _chosen = null;
        _closedAtShown = null;
    }
}
