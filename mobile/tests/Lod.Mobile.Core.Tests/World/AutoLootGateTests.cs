using Lod.Mobile.Core.Art;
using Lod.Mobile.Core.Automation;
using Lod.Mobile.Core.Model;

namespace Lod.Mobile.Core.Tests.World;

public sealed class AutoLootGateTests
{
    private static readonly Tile Standing = new(12, 9);

    private static Creature Ground(uint serial, Tile? where = null) =>
        new(serial, where ?? Standing, Direction.South, 1, CreatureKind.Passable, string.Empty);

    [Fact]
    public void Enabled_walk_over_requests_the_ground_item_once()
    {
        AutoLootGate gate = new();
        Creature dropped = Ground(41);

        Assert.Equal(Standing, gate.Next(true, Standing, [dropped]));
        Assert.Null(gate.Next(true, Standing, [dropped]));
    }

    [Fact]
    public void Disabled_walk_over_does_not_request_the_ground_item()
    {
        AutoLootGate gate = new();

        Assert.Null(gate.Next(false, Standing, [Ground(41)]));
        // Turning it on later must not have consumed a request while it was off.
        Assert.Equal(Standing, gate.Next(true, Standing, [Ground(41)]));
    }

    [Fact]
    public void Removed_item_is_forgotten_so_the_next_ground_item_can_be_requested()
    {
        AutoLootGate gate = new();
        Creature first = Ground(41);
        Creature second = Ground(42);

        Assert.Equal(Standing, gate.Next(true, Standing, [first]));
        Assert.Null(gate.Next(true, Standing, [])); // 0x0E: successful inventory response removed it.
        Assert.Equal(Standing, gate.Next(true, Standing, [second]));
    }

    [Fact]
    public void A_floor_item_elsewhere_is_not_requested()
    {
        AutoLootGate gate = new();

        Assert.Null(gate.Next(true, Standing, [Ground(41, new Tile(13, 9))]));
    }

    [Fact]
    public void What_we_threw_at_our_feet_is_not_picked_back_up()
    {
        AutoLootGate gate = new();
        Tile next = new(13, 9);

        gate.Threw(Standing);
        Assert.Null(gate.Next(true, Standing, [])); // the drop is on its way
        Assert.Null(gate.Next(true, Standing, [Ground(41)]));
        Assert.Null(gate.Next(true, next, [Ground(41)]));
        Assert.Null(gate.Next(true, Standing, [Ground(41)])); // walking back over it
        Assert.Equal(next, gate.Next(true, next, [Ground(41), Ground(42, next)]));
    }
}
