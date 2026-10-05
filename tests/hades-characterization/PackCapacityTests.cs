using Darkages;
using Darkages.Types;
using Xunit;

namespace Lod.Hades.Characterization.Tests;

/// <summary>
/// 가방 150칸(5탭 × 30, 사용자 2026-10-05)과 무게 제한 끔. 원작은 59칸이라 이미 저장된 캐릭터 파일에는 1~59 칸만 있다 —
/// 읽어 들여도 60~150 칸이 남아 있어야 150번째 칸까지 물건이 들어간다.
/// </summary>
public sealed class PackCapacityTests
{
    [Fact]
    public void A_pack_saved_with_59_places_comes_back_with_150_and_fills_to_the_last()
    {
        string saved = "{\"Items\":{" + string.Join(",", Enumerable.Range(1, 59).Select(slot => $"\"{slot}\":null")) + "}}";

        // 서버가 캐릭터를 읽는 그 Newtonsoft(StorageManager.Deserialize 는 서버 기동 없이 부를 수 없다 — 정적 생성자가 로그를 켠다).
        // 설정은 ObjectCreationHandling 을 건드리지 않으니 기본값과 같다: 생성자가 깐 사전에 저장된 칸만 덮어쓴다.
        Inventory pack = (Inventory)Type.GetType("Newtonsoft.Json.JsonConvert, Newtonsoft.Json", throwOnError: true)!
            .GetMethod("DeserializeObject", [typeof(string), typeof(Type)])!
            .Invoke(null, [saved, typeof(Inventory)])!;

        Assert.Equal(150, pack.Length);

        for (var slot = 1; slot < 150; slot++) pack.Items[slot] = new Item { Slot = (byte)slot };

        Assert.Equal(150, pack.FindEmpty());

        pack.Items[150] = new Item { Slot = 150 };

        Assert.Equal(byte.MaxValue, pack.FindEmpty());
    }

    /// <summary>
    /// 줍기·사기·교환·봇 받기·장비가 모두 견주는 상한(Item.CanCarry 등). 예전 식(레벨/4 + 힘 + 보정)이면 무게 1만은 한참
    /// 넘었다 — 이제 150칸을 가장 무거운 것(255)으로 채워도 안 넘는다. CanCarry 자체는 서버 기동 없이 부를 수 없다.
    /// </summary>
    [Fact]
    public void Weight_no_longer_stops_carrying()
    {
        Aisling heavy = new() { CurrentWeight = 10_000 };

        Assert.True(heavy.CurrentWeight + 200 < heavy.MaximumWeight);
        Assert.True(150 * byte.MaxValue < heavy.MaximumWeight);
    }
}
