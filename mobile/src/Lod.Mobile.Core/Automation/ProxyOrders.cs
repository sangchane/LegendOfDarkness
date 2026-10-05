using System.Text;
using System.Text.Json;

namespace Lod.Mobile.Core.Automation;

/// <summary>
/// 대신 사냥 맡김 설정(0xF1 7) — 앱이 자동 사냥을 켤 때 서버에 맡겨 두고, 앱이 끊기면 대리 프로그램(Lod.HuntProxy)이 이대로 사냥한다.
/// 기술·마법은 <b>이름</b>으로 — 칸 번호는 앱의 배치 기준이라 대리가 받는 칸과 다를 수 있다. 설계 <c>autopilot/proxy-hunt/05-api-contract.md</c>.
/// </summary>
public sealed record ProxyOrders
{
    public const int DefaultHours = 2;

    public int V { get; init; } = 1;
    public int Hours { get; init; } = DefaultHours;
    public int Radius { get; init; } = AutoHuntSettings.DefaultRadius;
    public int HealPercent { get; init; } = AutoHuntSettings.DefaultHealPercent;
    public PotionRule Hp { get; init; } = new(false, 70, "쿠룸");
    public PotionRule Mp { get; init; } = new(false, 70, "마라디움");
    public bool Loot { get; init; } = true;
    public IReadOnlyList<string> Skills { get; init; } = [];
    public IReadOnlyList<string> Spells { get; init; } = [];
    public IReadOnlyList<string> EnemySpells { get; init; } = [];
    public int Map { get; init; }
    public int X { get; init; }
    public int Y { get; init; }

    public AutoHuntSettings Hunt => new(Radius, HealPercent);

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>JSON 을 읽는다. 틀리면 null.</summary>
    public static ProxyOrders? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ProxyOrders>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>0xF1 7 몸 — 종류 · 길이(2, 큰 끝) · UTF-8 JSON. null 이면 길이 0(맡김 지움).</summary>
    public static byte[] Packet(ProxyOrders? orders)
    {
        byte[] json = orders is null ? [] : Encoding.UTF8.GetBytes(orders.ToJson());
        return [7, (byte)(json.Length >> 8), (byte)json.Length, .. json];
    }
}
