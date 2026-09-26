using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

/// <summary>봇 장비창의 규칙(<see cref="BotKit" />) — 무엇을 목록에 보이나, 봇 가방 포션을 어떻게 적나.</summary>
public sealed class BotKitTests
{
    private static InventoryItem Item(int slot, string name, int max = 0, int stacks = 1) => new(slot, 0, 0, name, stacks, max, max);

    private static readonly IReadOnlyList<InventoryItem> Pack =
    [
        Item(5, "마라디움", stacks: 3),
        Item(1, "홀리머큐리아", max: 3000),
        Item(2, "쿠룸", stacks: 10),
        Item(3, "레더로브", max: 2000),
        Item(4, "사과", stacks: 2),
    ];

    [Fact]
    public void Wearables_are_what_has_durability_in_slot_order()
    {
        Assert.Equal(["홀리머큐리아", "레더로브"], BotKit.Wearables(Pack).Select(one => one.Name));
    }

    [Fact]
    public void Potions_are_healing_first_then_mana_and_food_is_left_out()
    {
        Assert.Equal(["쿠룸", "마라디움"], BotKit.Potions(Pack).Select(one => one.Name));
    }

    [Fact]
    public void The_bots_potions_read_as_one_line()
    {
        CompanionKit kit = new([], [new CarriedItem("마라디움", 0, 2), new CarriedItem("쿠룸", 0, 4)]);

        Assert.Equal("쿠룸 4 · 마라디움 2", BotKit.Summary(kit));
        Assert.Equal("포션 없음", BotKit.Summary(null));
    }

    [Fact]
    public void A_place_shows_what_is_worn_there()
    {
        WornItem staff = new(1, 0, "홀리파나", "홀리파나", 1, 1);
        CompanionKit kit = new([staff], []);

        Assert.Equal(staff, BotKit.At(kit, 1));
        Assert.Null(BotKit.At(kit, 2));
    }

    [Fact]
    public void Bars_are_hidden_until_the_server_says()
    {
        Assert.Equal((null, null), BotKit.Bars(null));
        Assert.Equal((80, 35), BotKit.Bars(new CompanionLife(9, 80, 35)));
    }
}
