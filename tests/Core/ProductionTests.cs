using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Command;
using Xunit;

namespace CommanderLayer.Tests
{
    public class ProductionTests
    {
        // ---------- builders ----------
        private static Composition Comp(params (RoleFamily f, int n)[] parts)
        {
            var c = new Composition();
            foreach (var p in parts) c.Set(p.f, p.n);
            return c;
        }

        private static ConvoyOption Opt(string name, float cost, params (RoleFamily f, int n)[] delivers) =>
            new ConvoyOption(name, cost, Comp(delivers));

        // ---------- ProductionPlanner ----------
        [Fact]
        public void Plan_picks_cheapest_set_that_covers_the_gap_within_funds()
        {
            var gap = Comp((RoleFamily.Armor, 2), (RoleFamily.AirDefense, 1));
            var catalog = new ConvoyCatalog(new[]
            {
                Opt("Armor convoy", 500f, (RoleFamily.Armor, 1)),
                Opt("AirDefense convoy", 400f, (RoleFamily.AirDefense, 1)),
                // A bundle is the efficient cover: all 3 units for 1000 (vs 1400 buying singles).
                Opt("Combined bundle", 1000f, (RoleFamily.Armor, 2), (RoleFamily.AirDefense, 1)),
            });

            var plan = ProductionPlanner.Plan(gap, catalog, funds: 2000f);

            Assert.Equal(new[] { "Combined bundle" }, plan.Select(p => p.Name).ToArray());
            Assert.Equal(1000f, plan.Sum(p => p.Cost));
        }

        [Fact]
        public void Plan_buys_multiple_options_to_cover_a_multi_family_gap()
        {
            var gap = Comp((RoleFamily.Armor, 1), (RoleFamily.AirDefense, 1));
            var catalog = new ConvoyCatalog(new[]
            {
                Opt("Armor convoy", 100f, (RoleFamily.Armor, 1)),
                Opt("AirDefense convoy", 100f, (RoleFamily.AirDefense, 1)),
            });

            var plan = ProductionPlanner.Plan(gap, catalog, funds: 500f);

            Assert.Equal(2, plan.Count);
            Assert.Contains(plan, p => p.Name == "Armor convoy");
            Assert.Contains(plan, p => p.Name == "AirDefense convoy");
            var delivered = Delivered(plan);
            Assert.Equal(0, gap.Shortfall(delivered).Total);
        }

        [Fact]
        public void Plan_returns_partial_when_funds_cover_only_some_of_the_gap()
        {
            var gap = Comp((RoleFamily.Armor, 1), (RoleFamily.AirDefense, 1));
            var catalog = new ConvoyCatalog(new[]
            {
                Opt("Armor convoy", 300f, (RoleFamily.Armor, 1)),
                Opt("AirDefense convoy", 300f, (RoleFamily.AirDefense, 1)),
            });

            var plan = ProductionPlanner.Plan(gap, catalog, funds: 400f);

            Assert.Single(plan);
            Assert.True(plan.Sum(p => p.Cost) <= 400f);
            Assert.True(gap.Shortfall(Delivered(plan)).Total > 0);
        }

        [Fact]
        public void Plan_returns_empty_when_nothing_is_affordable()
        {
            var gap = Comp((RoleFamily.Armor, 1));
            var catalog = new ConvoyCatalog(new[] { Opt("Armor convoy", 1000f, (RoleFamily.Armor, 1)) });

            var plan = ProductionPlanner.Plan(gap, catalog, funds: 100f);

            Assert.Empty(plan);
        }

        [Fact]
        public void Plan_never_exceeds_funds()
        {
            var gap = Comp((RoleFamily.Armor, 5));
            var catalog = new ConvoyCatalog(new[] { Opt("Armor convoy", 300f, (RoleFamily.Armor, 1)) });

            var plan = ProductionPlanner.Plan(gap, catalog, funds: 1000f);

            Assert.True(plan.Sum(p => p.Cost) <= 1000f);
            Assert.Equal(3, plan.Count);
        }

        [Fact]
        public void Plan_ignores_options_that_deliver_nothing_useful()
        {
            var gap = Comp((RoleFamily.Armor, 1));
            var catalog = new ConvoyCatalog(new[]
            {
                Opt("Supply convoy", 10f, (RoleFamily.Supply, 5)),   // cheap but irrelevant to the gap
                Opt("Armor convoy", 500f, (RoleFamily.Armor, 1)),
            });

            var plan = ProductionPlanner.Plan(gap, catalog, funds: 600f);

            Assert.Equal(new[] { "Armor convoy" }, plan.Select(p => p.Name).ToArray());
        }

        [Fact]
        public void Plan_is_deterministic_and_breaks_ties_by_name()
        {
            var gap = Comp((RoleFamily.Armor, 1));
            var catalog = new ConvoyCatalog(new[]
            {
                Opt("Bravo armor", 500f, (RoleFamily.Armor, 1)),
                Opt("Alpha armor", 500f, (RoleFamily.Armor, 1)),
            });

            var first = ProductionPlanner.Plan(gap, catalog, funds: 500f);
            var second = ProductionPlanner.Plan(gap, catalog, funds: 500f);

            Assert.Equal(new[] { "Alpha armor" }, first.Select(p => p.Name).ToArray());
            Assert.Equal(first.Select(p => p.Name), second.Select(p => p.Name));
        }

        // ---------- ProductionQueue ----------
        [Fact]
        public void Queue_dequeues_FIFO_and_returns_null_when_empty()
        {
            var q = new ProductionQueue();
            Assert.Null(q.Dequeue());

            q.Enqueue(new PurchaseRequest("Armor convoy", 1200f, "Bravo", RoleFamily.Armor));
            q.Enqueue(new PurchaseRequest("AirDefense convoy", 800f, "Alpha", RoleFamily.AirDefense));

            Assert.Equal("Armor convoy", q.Dequeue().ConvoyName);
            Assert.Equal("AirDefense convoy", q.Dequeue().ConvoyName);
            Assert.Null(q.Dequeue());
        }

        [Fact]
        public void Queue_tracks_queued_cost_and_pending_shrinks_on_dequeue()
        {
            var q = new ProductionQueue();
            q.Enqueue(new PurchaseRequest("Armor convoy", 1200f, "Bravo", RoleFamily.Armor));
            q.Enqueue(new PurchaseRequest("AirDefense convoy", 800f, "Alpha", RoleFamily.AirDefense));

            Assert.Equal(2, q.Pending.Count);
            Assert.Equal(2000f, q.QueuedCost);

            q.Dequeue();
            Assert.Single(q.Pending);
            Assert.Equal(800f, q.QueuedCost);
        }

        [Fact]
        public void Queue_describe_renders_building_lines()
        {
            var q = new ProductionQueue();
            q.Enqueue(new PurchaseRequest("Armor convoy", 1200f, "Bravo", RoleFamily.Armor));
            q.Enqueue(new PurchaseRequest("AA convoy", 800f, null, RoleFamily.AirDefense));

            var lines = q.Describe();
            Assert.Equal(2, lines.Count);
            Assert.Equal("BUILDING · Armor convoy → Bravo · 1200", lines[0]); // named squad
            Assert.Equal("BUILDING · AA convoy · 800", lines[1]);             // no squad suffix when unassigned
        }

        // ---------- helpers ----------
        private static Composition Delivered(IEnumerable<ConvoyOption> picks)
        {
            var sum = new Composition();
            foreach (var p in picks)
                foreach (var kv in p.Delivers.Items)
                    sum.Add(kv.Key, kv.Value);
            return sum;
        }
    }
}
