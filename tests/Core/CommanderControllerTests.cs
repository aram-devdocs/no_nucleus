using System.Linq;
using CommanderLayer.Core.Controller;
using CommanderLayer.Core.Model;
using Xunit;

namespace CommanderLayer.Tests
{
    public class CommanderControllerTests
    {
        private static FactionInfo Faction() => new FactionInfo("Boscali", new ColorRgba(0.2f, 0.6f, 1f));

        private static (CommanderController c, FakePlayerContext p, FakeUnitQuery u, FakeObjectiveService o, FakeClock clk)
            Build(bool withFaction = true, float arriveRadius = 250f)
        {
            var p = new FakePlayerContext { Faction = withFaction ? Faction() : null };
            var u = new FakeUnitQuery();
            var o = new FakeObjectiveService();
            var clk = new FakeClock();
            var c = new CommanderController(p, u, o, clk, arriveRadius);
            return (c, p, u, o, clk);
        }

        [Fact]
        public void Refresh_WithoutFaction_YieldsNoFactionState()
        {
            var (c, _, _, _, _) = Build(withFaction: false);
            c.Refresh();
            Assert.False(c.State.HasLocalFaction);
            Assert.Null(c.State.Objective);
        }

        [Fact]
        public void Refresh_WithFaction_ExposesFactionAndNoObjective()
        {
            var (c, _, _, _, _) = Build();
            c.Refresh();
            Assert.True(c.State.HasLocalFaction);
            Assert.Equal("Boscali", c.State.Faction.Name);
            Assert.Null(c.State.Objective);
        }

        [Fact]
        public void TryPlaceAt_WithFaction_PlacesExactlyOnce_AndSetsObjective()
        {
            var (c, _, _, o, _) = Build();
            bool ok = c.TryPlaceAt(new Vec3(100, 0, 200));

            Assert.True(ok);
            Assert.Equal(1, o.PlaceCount);
            Assert.NotNull(c.State.Objective);
            Assert.Equal(ObjectiveKind.MoveAttack, c.State.Objective.Kind);
            Assert.Equal(100, c.State.Objective.Position.X);
            Assert.Equal(200, c.State.Objective.Position.Z);
            Assert.Same(o.LastPlaced, c.State.Objective);
        }

        [Fact]
        public void TryPlaceAt_WithoutFaction_ReturnsFalse_AndPlacesNothing()
        {
            var (c, _, _, o, _) = Build(withFaction: false);
            bool ok = c.TryPlaceAt(new Vec3(1, 0, 1));

            Assert.False(ok);
            Assert.Equal(0, o.PlaceCount);
            Assert.Null(c.State.Objective);
        }

        [Fact]
        public void ArmPlacement_SetsArmed_AndPlacingDisarms()
        {
            var (c, _, _, _, _) = Build();
            c.ArmPlacement();
            Assert.True(c.State.PlacementArmed);

            c.TryPlaceAt(new Vec3(0, 0, 0));
            Assert.False(c.State.PlacementArmed);
        }

        [Fact]
        public void Clear_RemovesObjective_AndCallsServiceClear()
        {
            var (c, _, _, o, _) = Build();
            c.TryPlaceAt(new Vec3(0, 0, 0));
            c.Clear();

            Assert.Null(c.State.Objective);
            Assert.Equal(1, o.ClearCount);
        }

        [Fact]
        public void Clear_WithNoObjective_DoesNotCallServiceClear()
        {
            var (c, _, _, o, _) = Build();
            c.Clear();
            Assert.Equal(0, o.ClearCount);
        }

        [Fact]
        public void Refresh_RanksUnitsByDistance_AndFlagsArrivedWithinRadius()
        {
            var (c, _, u, _, _) = Build(arriveRadius: 250f);
            // objective at origin
            // far unit (~500m), near unit (~100m, within arrive radius)
            u.Units.Add(new UnitInfo("u1", "Far Ship", "Ship", new Vec3(500, 0, 0), commandable: true, disabled: false));
            u.Units.Add(new UnitInfo("u2", "Near Tank", "GroundVehicle", new Vec3(100, 0, 0), commandable: true, disabled: false));

            c.TryPlaceAt(new Vec3(0, 0, 0));
            var units = c.State.Assignments.Units;

            Assert.Equal(2, units.Count);
            Assert.Equal("Near Tank", units[0].UnitName); // closest first
            Assert.Equal(AssignmentState.Arrived, units[0].State);
            Assert.Equal("Far Ship", units[1].UnitName);
            Assert.Equal(AssignmentState.EnRoute, units[1].State);
        }

        [Fact]
        public void Assignments_ExcludeDisabled_AndCountCommandable()
        {
            var (c, _, u, _, _) = Build();
            u.Units.Add(new UnitInfo("u1", "Ship", "Ship", new Vec3(10, 0, 0), commandable: true, disabled: false));
            u.Units.Add(new UnitInfo("u2", "Jet", "Aircraft", new Vec3(20, 0, 0), commandable: false, disabled: false));
            u.Units.Add(new UnitInfo("u3", "Wreck", "Ship", new Vec3(5, 0, 0), commandable: true, disabled: true));

            c.TryPlaceAt(new Vec3(0, 0, 0));
            var snap = c.State.Assignments;

            Assert.Equal(2, snap.Total);            // disabled excluded
            Assert.Equal(1, snap.CommandableCount); // only the live ship
        }

        [Fact]
        public void Assignments_EmptyWhenNoObjective()
        {
            var (c, _, u, _, _) = Build();
            u.Units.Add(new UnitInfo("u1", "Ship", "Ship", new Vec3(10, 0, 0), commandable: true, disabled: false));
            c.Refresh();
            Assert.Equal(0, c.State.Assignments.Total);
        }

        [Fact]
        public void StateChanged_RaisedOnPlaceAndClear()
        {
            var (c, _, _, _, _) = Build();
            int raised = 0;
            c.StateChanged += _ => raised++;

            c.TryPlaceAt(new Vec3(0, 0, 0)); // 1
            c.Clear();                        // 2
            Assert.Equal(2, raised);
        }

        [Fact]
        public void LosingFaction_DropsObjectiveState()
        {
            var (c, p, _, _, _) = Build();
            c.TryPlaceAt(new Vec3(0, 0, 0));
            Assert.NotNull(c.State.Objective);

            p.Faction = null; // left the mission
            c.Refresh();

            Assert.False(c.State.HasLocalFaction);
            Assert.Null(c.State.Objective);
        }
    }
}
