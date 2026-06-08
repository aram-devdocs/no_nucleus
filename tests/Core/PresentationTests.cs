using System.Collections.Generic;
using Nucleus.Core.Command;
using Nucleus.Presentation;
using Xunit;

namespace Nucleus.Tests
{
    /// <summary>The pure presentation layer turns an HqSnapshot + interaction into render-ready rows; these pin
    /// the display decisions (selection cue, affordability, status wording) without any Unity dependency.</summary>
    public class PresentationTests
    {
        private static OperationView Op(string id, ObjectiveKind kind, int squads = 1,
            OperationStatus status = OperationStatus.Active, AutonomyLevel autonomy = AutonomyLevel.Auto,
            bool playerOwned = false)
            => new OperationView(id, kind, CombatPhase.Strike, status, squads, autonomy, id, default, 5f, playerOwned);

        private static HqSnapshot Hq(IReadOnlyList<OperationView> ops = null, IReadOnlyList<SquadView> squads = null,
            float queued = 0f)
            => new HqSnapshot(ops ?? new List<OperationView>(), squads ?? new List<SquadView>(),
                new List<string>(), new List<ReportEvent>(), true, true, queued);

        [Fact]
        public void Selected_objective_row_uses_the_active_cue_and_a_caret()
        {
            var hq = Hq(new List<OperationView> { Op("o1", ObjectiveKind.CapturePoint) });
            var vm = PresentationBuilder.Build(hq, new PanelInteraction(null, "o1"), null, 0f);

            var row = Assert.Single(vm.ObjectiveRows);
            Assert.Equal("o1", row.Id);
            Assert.StartsWith("▸ ", row.Label);
            Assert.Equal(UiColor.Active, row.LabelColor);
            Assert.Equal(UiColor.Active, row.ButtonColor);   // SELECT highlighted when selected
            Assert.True(row.ShowKindDot);
        }

        [Fact]
        public void Unselected_objective_row_is_plain()
        {
            var hq = Hq(new List<OperationView> { Op("o1", ObjectiveKind.DestroyTarget) });
            var vm = PresentationBuilder.Build(hq, new PanelInteraction(null, null), null, 0f);

            var row = Assert.Single(vm.ObjectiveRows);
            Assert.DoesNotContain("▸", row.Label);
            Assert.Equal(UiColor.Text, row.LabelColor);
            Assert.Equal(UiColor.Idle, row.ButtonColor);
            Assert.Null(vm.ObjectiveEditor);   // nothing selected
        }

        [Fact]
        public void Unaffordable_build_row_is_disabled_and_idle()
        {
            var catalog = new ConvoyCatalog(new[]
            {
                new ConvoyOption("Cheap", 50f, new Composition()),
                new ConvoyOption("Dear", 500f, new Composition()),
            });
            var vm = PresentationBuilder.Build(Hq(), new PanelInteraction(null, null), catalog, funds: 100f);

            Assert.Equal(2, vm.BuildRows.Count);
            var cheap = vm.BuildRows[0]; var dear = vm.BuildRows[1];
            Assert.True(cheap.ButtonEnabled); Assert.Equal(UiColor.Active, cheap.ButtonColor);
            Assert.False(dear.ButtonEnabled); Assert.Equal(UiColor.Idle, dear.ButtonColor);
        }

        [Fact]
        public void Build_funds_warns_when_over_committed()
        {
            var vm = PresentationBuilder.Build(Hq(queued: 200f), new PanelInteraction(null, null), null, funds: 100f);
            Assert.Equal(UiColor.Warn, vm.BuildFundsColor);   // After = -100
        }

        [Fact]
        public void Manual_squad_row_reads_YOU_in_the_faction_color_and_depleted_warns()
        {
            var squads = new List<SquadView>
            {
                new SquadView("s1", "Alpha", RoleFamily.Armor, 1, SquadStatus.Depleted, null,
                    AutonomyLevel.Manual, "Reserve [YOU]"),
            };
            var vm = PresentationBuilder.Build(Hq(squads: squads), new PanelInteraction(null, null), null, 0f);

            var row = Assert.Single(vm.SquadRows);
            Assert.Equal("YOU", row.Button);
            Assert.Equal(UiColor.Accent, row.ButtonColor);
            Assert.Equal(UiColor.Warn, row.LabelColor);       // depleted
        }

        [Fact]
        public void Scoreboard_bars_are_fraction_of_the_starting_pool()
        {
            var war = new WarfareCampaign();
            war.War.Blufor.FactionName = "Blu"; war.War.Opfor.FactionName = "Op";
            var vm = PresentationBuilder.BuildScoreboard(war.SnapshotBoard());
            Assert.Equal(1.0f, vm.BluforFraction, 3);         // fresh war = full pool (1000/1000)
            Assert.Equal(UiColor.Muted, vm.StatusColor);      // not over
        }
    }
}
