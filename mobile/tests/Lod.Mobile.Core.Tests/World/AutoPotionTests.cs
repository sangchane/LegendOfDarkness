using Lod.Mobile.Core.World;

namespace Lod.Mobile.Core.Tests.World;

public sealed class AutoPotionTests
{
    private static readonly PotionRule Off = new(false, 50, "쿠룸");
    private static readonly PotionRule Half = new(true, 50, "쿠룸");
    private static readonly PotionRule HalfMana = new(true, 50, "마라디움");

    private static Vitals Life(int health, int mana, int maximum = 1000) => Vitals.Unknown with
    {
        Health = health,
        MaximumHealth = maximum,
        Mana = mana,
        MaximumMana = maximum,
    };

    private static InventoryItem Carried(int slot, string name, int stacks = 5) =>
        new(slot, 0, 0, name, stacks, 0, 0);

    [Fact]
    public void Low_health_drinks_the_chosen_potion()
    {
        InventoryItem[] pack = [Carried(1, "엑스쿠라눔"), Carried(2, "쿠룸"), Carried(3, "마라디움")];

        Assert.Equal(2, new AutoPotion().Next(Life(400, 1000), pack, Half, Off, TimeSpan.Zero));
        Assert.Equal(1, new AutoPotion().Next(Life(400, 1000), pack, Half with { Potion = "엑스쿠라눔" }, Off, TimeSpan.Zero));
    }

    [Fact]
    public void Another_potion_is_not_used_when_the_chosen_one_has_run_out()
    {
        AutoPotion potion = new();

        Assert.Null(potion.Next(Life(100, 1000), [Carried(1, "엑스쿠라눔")], Half, Off, TimeSpan.Zero));
    }

    [Fact]
    public void Count_adds_up_every_slot_of_one_potion()
    {
        Assert.Equal(8, AutoPotion.Count([Carried(1, "쿠룸", 5), Carried(4, "쿠룸", 3), Carried(2, "마라디움", 9)], "쿠룸"));
    }

    [Fact]
    public void At_the_line_counts_as_below_it()
    {
        AutoPotion potion = new();

        Assert.Equal(1, potion.Next(Life(500, 1000), [Carried(1, "쿠룸")], Half, Off, TimeSpan.Zero));
        Assert.Null(new AutoPotion().Next(Life(501, 1000), [Carried(1, "쿠룸")], Half, Off, TimeSpan.Zero));
    }

    [Fact]
    public void Low_mana_drinks_a_mana_potion_and_not_a_healing_one()
    {
        AutoPotion potion = new();
        InventoryItem[] pack = [Carried(1, "쿠룸"), Carried(2, "하급마력포션"), Carried(3, "마라디움")];

        Assert.Equal(3, potion.Next(Life(1000, 100), pack, Half, HalfMana, TimeSpan.Zero));
    }

    [Fact]
    public void Switched_off_kind_is_not_drunk()
    {
        AutoPotion potion = new();

        Assert.Null(potion.Next(Life(100, 100), [Carried(1, "쿠룸"), Carried(2, "마라디움")], Off, Off, TimeSpan.Zero));
    }

    [Fact]
    public void Nothing_is_drunk_before_the_server_has_said_how_we_are_or_after_death()
    {
        AutoPotion potion = new();
        InventoryItem[] pack = [Carried(1, "쿠룸")];

        Assert.Null(potion.Next(Vitals.Unknown, pack, Half, Half, TimeSpan.Zero));
        Assert.Null(potion.Next(Life(0, 1000), pack, Half, Half, TimeSpan.Zero));
    }

    [Fact]
    public void One_drink_waits_for_the_pack_to_change_before_the_next()
    {
        AutoPotion potion = new();

        Assert.Equal(1, potion.Next(Life(100, 1000), [Carried(1, "쿠룸", 5)], Half, Off, TimeSpan.Zero));
        // The server has not answered yet: same stack, still low. Asking again would drink twice.
        Assert.Null(potion.Next(Life(100, 1000), [Carried(1, "쿠룸", 5)], Half, Off, TimeSpan.FromMilliseconds(300)));
        // One went down — the drink landed, so a still-low bar may ask for another.
        Assert.Equal(1, potion.Next(Life(350, 1000), [Carried(1, "쿠룸", 4)], Half, Off, TimeSpan.FromMilliseconds(400)));
    }

    [Fact]
    public void The_last_one_leaving_the_pack_also_counts_as_an_answer()
    {
        AutoPotion potion = new();

        Assert.Equal(1, potion.Next(Life(100, 1000), [Carried(1, "쿠룸", 1), Carried(2, "쿠룸", 3)], Half, Off, TimeSpan.Zero));
        Assert.Equal(2, potion.Next(Life(100, 1000), [Carried(2, "쿠룸", 3)], Half, Off, TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void An_unanswered_drink_is_given_up_after_a_while()
    {
        AutoPotion potion = new();
        InventoryItem[] pack = [Carried(1, "쿠룸", 5)];

        Assert.Equal(1, potion.Next(Life(100, 1000), pack, Half, Off, TimeSpan.Zero));
        Assert.Null(potion.Next(Life(100, 1000), pack, Half, Off, TimeSpan.FromSeconds(1.9)));
        Assert.Equal(1, potion.Next(Life(100, 1000), pack, Half, Off, TimeSpan.FromSeconds(2)));
    }
}
