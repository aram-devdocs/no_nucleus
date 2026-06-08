using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Tests
{
    public class HqViewTests
    {
        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);

        private static Objective Obj(string id, ObjectiveKind kind) =>
            new Objective(id, kind, P(0, 0), ObjectiveSource.Auto);

        private static Squad Sq(string id, string name, RoleFamily fam, int strength, string assignedOp = null)
        {
            var s = new Squad(id, name, fam, SquadOrigin.Auto, Enumerable.Range(0, strength).Select(i => id + "u" + i));
            s.AssignedOperationId = assignedOp;
            return s;
        }

        [Fact]
        public void Build_maps_operations_with_correct_fields_and_count()
        {
            var state = new CommanderState();
            var op = new Operation("op-1", Obj("o1", ObjectiveKind.CapturePoint), new[] { "s1", "s2" })
            {
                CombatPhase = CombatPhase.Strike,
                Status = OperationStatus.Active,
                Autonomy = AutonomyLevel.Assisted,
            };
            state.Operations.Add(op);

            var snap = HqView.Build(state, new BattleLog(), new ProductionQueue());

            var view = Assert.Single(snap.Operations);
            Assert.Equal("op-1", view.Id);
            Assert.Equal(ObjectiveKind.CapturePoint, view.Kind);
            Assert.Equal(CombatPhase.Strike, view.Phase);
            Assert.Equal(OperationStatus.Active, view.Status);
            Assert.Equal(2, view.SquadCount);
            Assert.Equal(AutonomyLevel.Assisted, view.Autonomy);
        }

        [Fact]
        public void Build_maps_squads_with_correct_fields_and_count()
        {
            var state = new CommanderState();
            state.Squads.Add(Sq("s1", "Alpha", RoleFamily.Armor, 3, assignedOp: "op-1"));
            state.Squads.Add(Sq("s2", "Bravo", RoleFamily.Artillery, 1));

            var snap = HqView.Build(state, new BattleLog(), new ProductionQueue());

            Assert.Equal(2, snap.Squads.Count);

            var alpha = snap.Squads.Single(s => s.Id == "s1");
            Assert.Equal("Alpha", alpha.Name);
            Assert.Equal(RoleFamily.Armor, alpha.Family);
            Assert.Equal(3, alpha.Strength);
            Assert.Equal("op-1", alpha.AssignedOperationId);

            var bravo = snap.Squads.Single(s => s.Id == "s2");
            Assert.Equal(RoleFamily.Artillery, bravo.Family);
            Assert.Equal(1, bravo.Strength);
            Assert.Null(bravo.AssignedOperationId);
        }

        [Fact]
        public void Build_production_lines_come_from_queue_describe()
        {
            var state = new CommanderState();
            var prod = new ProductionQueue();
            prod.Enqueue(new PurchaseRequest("Armor convoy", 1200f, "s1", RoleFamily.Armor));
            prod.Enqueue(new PurchaseRequest("AA convoy", 800f, "s2", RoleFamily.AirDefense));

            var snap = HqView.Build(state, new BattleLog(), prod);

            Assert.Equal(prod.Describe(), snap.Production);
            Assert.Equal(2, snap.Production.Count);
            Assert.Contains("Armor convoy", snap.Production[0]);
        }

        [Fact]
        public void Build_recent_comes_from_log_in_newest_first_order_capped()
        {
            var state = new CommanderState();
            var log = new BattleLog();
            log.Append(new ReportEvent(1f, ReportKind.ObjectiveAdded, "first"));
            log.Append(new ReportEvent(2f, ReportKind.OperationStarted, "second"));
            log.Append(new ReportEvent(3f, ReportKind.PhaseChanged, "third"));

            var snap = HqView.Build(state, log, new ProductionQueue(), recentCount: 2);

            Assert.Equal(2, snap.Recent.Count);
            Assert.Equal("third", snap.Recent[0].Text);   // newest first
            Assert.Equal("second", snap.Recent[1].Text);
        }

        [Fact]
        public void Build_null_log_and_production_yield_empty_lists()
        {
            var state = new CommanderState();

            var snap = HqView.Build(state, null, null);

            Assert.NotNull(snap.Production);
            Assert.NotNull(snap.Recent);
            Assert.Empty(snap.Production);
            Assert.Empty(snap.Recent);
        }

        [Fact]
        public void Build_command_toggles_reflect_state()
        {
            var state = new CommanderState { AiCreatesObjectives = false, AiAutoFill = true };

            var snap = HqView.Build(state, new BattleLog(), new ProductionQueue());

            Assert.False(snap.AiCreatesObjectives);
            Assert.True(snap.AiAutoFill);
        }

        [Fact]
        public void Build_empty_state_yields_empty_operation_and_squad_lists()
        {
            var state = new CommanderState();

            var snap = HqView.Build(state, new BattleLog(), new ProductionQueue());

            Assert.Empty(snap.Operations);
            Assert.Empty(snap.Squads);
        }

        [Fact]
        public void A_dropped_objective_with_no_operation_yet_still_shows_as_a_selectable_row()
        {
            // The player drops an objective (auto-fill off, so no squad/operation forms yet). It must STILL
            // surface as a row/marker so it can be selected, edited and moved in place.
            var state = new CommanderState();
            var dropped = new Objective("obj-7", ObjectiveKind.DestroyTarget, P(1234f, 5678f), ObjectiveSource.Player, priority: 3f);
            state.Objectives.Add(dropped);

            var snap = HqView.Build(state, new BattleLog(), new ProductionQueue());

            var view = Assert.Single(snap.Operations);
            Assert.Equal("obj-7", view.ObjectiveId);
            Assert.Equal(ObjectiveKind.DestroyTarget, view.Kind);
            Assert.Equal(1234f, view.Position.X, 3);
            Assert.Equal(3f, view.Priority, 3);
            Assert.True(view.PlayerOwned);
            Assert.Equal(0, view.SquadCount);
        }

        [Fact]
        public void An_objective_that_already_has_an_operation_is_not_duplicated()
        {
            var state = new CommanderState();
            var obj = Obj("o1", ObjectiveKind.CapturePoint);
            state.Objectives.Add(obj);
            state.Operations.Add(new Operation("op-1", obj, new[] { "s1" }));

            var snap = HqView.Build(state, new BattleLog(), new ProductionQueue());

            // One row — the operation — not a second placeholder for the same objective.
            Assert.Single(snap.Operations);
            Assert.Equal("op-1", snap.Operations[0].Id);
        }
    }
}
