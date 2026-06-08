using Nucleus.Core.Command;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>The pure delivery model: a one-at-a-time cooldown drain turns the queue into per-item progress +
    /// ETA, the data behind the build panel's progress bars and the map arrival markers.</summary>
    public class ProductionQueueTests
    {
        private static ProductionQueue Queue(params string[] names)
        {
            var q = new ProductionQueue();
            foreach (var n in names) q.Enqueue(new PurchaseRequest(n, 100f, null, RoleFamily.Armor, n + "-contents"));
            return q;
        }

        [Fact]
        public void Head_item_progresses_linearly_over_the_cooldown()
        {
            var q = Queue("A");
            Assert.Equal(0f, q.Snapshot(nowSeconds: 100f, lastPurchaseSeconds: 100f, cooldownSeconds: 60f)[0].Progress01, 3);
            Assert.Equal(60f, q.Snapshot(100f, 100f, 60f)[0].EtaSeconds, 3);

            var mid = q.Snapshot(nowSeconds: 130f, lastPurchaseSeconds: 100f, cooldownSeconds: 60f)[0];
            Assert.Equal(0.5f, mid.Progress01, 3);
            Assert.Equal(30f, mid.EtaSeconds, 3);

            var done = q.Snapshot(nowSeconds: 160f, lastPurchaseSeconds: 100f, cooldownSeconds: 60f)[0];
            Assert.Equal(1f, done.Progress01, 3);
            Assert.Equal(0f, done.EtaSeconds, 3);
        }

        [Fact]
        public void Later_items_wait_their_turn_then_build()
        {
            var q = Queue("A", "B");
            var s = q.Snapshot(nowSeconds: 130f, lastPurchaseSeconds: 100f, cooldownSeconds: 60f);
            // B starts only when A delivers (t=160); at t=130 it hasn't started.
            Assert.Equal(0f, s[1].Progress01, 3);
            Assert.Equal(90f, s[1].EtaSeconds, 3);   // delivers at 100 + 60*2 = 220 -> 90s away
        }

        [Fact]
        public void Zero_cooldown_is_instant()
        {
            var v = Queue("A").Snapshot(nowSeconds: 5f, lastPurchaseSeconds: 0f, cooldownSeconds: 0f)[0];
            Assert.Equal(1f, v.Progress01, 3);
            Assert.Equal(0f, v.EtaSeconds, 3);
        }

        [Fact]
        public void Item_metadata_passes_through()
        {
            var v = Queue("Armor column").Snapshot(0f, 0f, 60f)[0];
            Assert.Equal("Armor column", v.Name);
            Assert.Equal("Armor column-contents", v.Contents);
            Assert.Equal(100f, v.Cost, 3);
        }
    }
}
