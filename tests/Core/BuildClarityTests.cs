using Nucleus.Core.Command;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>WS8 — the build panel must surface the queued-cost total so the player can see "Funds · Queued ·
    /// After" before over-committing. (The actual Drain/dispatch is Unity-coupled and verified in-game.)</summary>
    public class BuildClarityTests
    {
        [Fact]
        public void HqSnapshot_surfaces_the_queued_production_cost()
        {
            var state = new CommanderState();
            var queue = new ProductionQueue();
            queue.Enqueue(new PurchaseRequest("Armor column", 1200f, null, RoleFamily.Armor, "3× MBT", manual: true));
            queue.Enqueue(new PurchaseRequest("SAM battery", 800f, null, RoleFamily.AirDefense, "2× SAM", manual: true));

            var hq = HqView.Build(state, state.Log, queue);
            Assert.Equal(2000f, hq.QueuedCost);
        }

        [Fact]
        public void Queued_cost_is_zero_with_no_production_queue()
        {
            var state = new CommanderState();
            var hq = HqView.Build(state, state.Log, null);
            Assert.Equal(0f, hq.QueuedCost);
        }
    }
}
