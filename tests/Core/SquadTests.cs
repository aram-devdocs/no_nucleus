using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Tests
{
    public class SquadTests
    {
        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);

        // A unit with an explicit role/position (Family is derived from Role).
        private static UnitView U(string id, Role role, Vec3 pos, bool commandable = true) =>
            new UnitView(id, id, pos, UnitClass.GroundVehicle, false, commandable,
                new UnitCapability(role, true, false, false, false, false), 1f, 0f, 1);

        private static SquadConfig Cfg() => new SquadConfig { FormRadius = 4000f, MaxSquadSize = 3, DepletedFraction = 0.5f };

        [Fact]
        public void Former_groups_by_family_and_proximity()
        {
            var units = new List<UnitView>
            {
                U("a1", Role.Armor, P(0, 0)),
                U("a2", Role.Armor, P(100, 0)),       // near a1 -> same armor squad
                U("a3", Role.Armor, P(50000, 0)),     // far -> separate armor squad
                U("s1", Role.Supply, P(0, 0)),        // different family -> own squad
            };
            var squads = SquadFormer.Form(units, Cfg());

            Assert.Equal(3, squads.Count);                                   // 2 armor + 1 supply
            var armor = squads.Where(s => s.Family == RoleFamily.Armor).ToList();
            Assert.Equal(2, armor.Count);
            Assert.Contains(armor, s => s.MemberUnitIds.Count == 2 && s.MemberUnitIds.Contains("a1") && s.MemberUnitIds.Contains("a2"));
            Assert.Contains(armor, s => s.MemberUnitIds.SequenceEqual(new[] { "a3" }));
            Assert.Single(squads, s => s.Family == RoleFamily.Supply);
        }

        [Fact]
        public void Former_caps_squad_size_and_names_deterministically()
        {
            var units = Enumerable.Range(0, 5).Select(i => U("u" + i, Role.Armor, P(i, 0))).Cast<UnitView>().ToList();
            var squads = SquadFormer.Form(units, Cfg()); // MaxSquadSize=3 -> 3 + 2
            Assert.Equal(2, squads.Count);
            Assert.Equal(3, squads[0].MemberUnitIds.Count);
            Assert.Equal("Armor Alpha", squads[0].Name);
            Assert.Equal("Armor Bravo", squads[1].Name);
        }

        [Fact]
        public void Former_excludes_non_squadable_units()
        {
            var units = new List<UnitView>
            {
                U("mbt", Role.Armor, P(0, 0)),
                U("msl", Role.CruiseMissile, P(0, 0)),    // Other family -> not squadable
                U("bld", Role.BuildingMilitary, P(0, 0)), // Other family -> not squadable
            };
            var squads = SquadFormer.Form(units, Cfg());
            Assert.Single(squads);
            Assert.Equal(RoleFamily.Armor, squads[0].Family);
        }

        [Fact]
        public void Reconcile_prunes_dead_members_and_disbands_empty_auto_squads()
        {
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(100, 0)) };
            var sr = new SquadRoster(Cfg());
            sr.Reconcile(roster);
            Assert.Single(sr.Squads);
            Assert.Equal(2, sr.Squads[0].Strength);

            // Both die -> members pruned, empty auto squad disbanded.
            sr.Reconcile(new List<UnitView>());
            Assert.Empty(sr.Squads);
        }

        [Fact]
        public void Reconcile_auto_forms_loose_units()
        {
            var sr = new SquadRoster(Cfg());
            sr.Reconcile(new List<UnitView>());
            Assert.Empty(sr.Squads);
            sr.Reconcile(new List<UnitView> { U("a1", Role.Armor, P(0, 0)) });
            Assert.Single(sr.Squads);
            Assert.Contains("a1", sr.Squads[0].MemberUnitIds);
        }

        [Fact]
        public void Reconcile_marks_a_squad_depleted_below_its_strength_threshold()
        {
            // review F24: a squad with a TargetComposition is Depleted once Strength < Total * DepletedFraction
            // (0.5). Pins the strict '<' boundary at Strength == threshold.
            var sr = new SquadRoster(Cfg());
            var comp = new Composition();
            comp.Set(RoleFamily.Armor, 4);
            sr.Add(new Squad("p1", "Sq", RoleFamily.Armor, SquadOrigin.Player, new[] { "a1", "a2", "a3", "a4" }) { TargetComposition = comp });

            sr.Reconcile(new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(0, 0)), U("a3", Role.Armor, P(0, 0)), U("a4", Role.Armor, P(0, 0)) });
            Assert.NotEqual(SquadStatus.Depleted, sr.Squads[0].Status);   // full strength (4)

            sr.Reconcile(new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(0, 0)) });
            Assert.NotEqual(SquadStatus.Depleted, sr.Squads[0].Status);   // Strength 2 == 4*0.5 → NOT < threshold

            sr.Reconcile(new List<UnitView> { U("a1", Role.Armor, P(0, 0)) });
            Assert.Equal(SquadStatus.Depleted, sr.Squads[0].Status);      // Strength 1 < 2 → Depleted
        }

        [Fact]
        public void Reconcile_reports_forming_for_an_unassigned_under_strength_squad()
        {
            // review E29: SquadStatus.Forming was unreachable in the derived view. An unassigned squad below
            // its TargetComposition strength (but above the depleted floor) now reads Forming; at full strength
            // it reads Ready. (DepletedFraction=0.5, target=4 → depleted below 2.)
            var sr = new SquadRoster(Cfg());
            var comp = new Composition();
            comp.Set(RoleFamily.Armor, 4);
            sr.Add(new Squad("p1", "Sq", RoleFamily.Armor, SquadOrigin.Player, new[] { "a1", "a2", "a3", "a4" }) { TargetComposition = comp });

            sr.Reconcile(new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(0, 0)), U("a3", Role.Armor, P(0, 0)), U("a4", Role.Armor, P(0, 0)) });
            Assert.Equal(SquadStatus.Ready, sr.Squads[0].Status);     // at target → ready

            sr.Reconcile(new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(0, 0)), U("a3", Role.Armor, P(0, 0)) });
            Assert.Equal(SquadStatus.Forming, sr.Squads[0].Status);   // dropped to 3 of 4, unassigned → forming up
        }

        [Fact]
        public void Reconcile_keeps_empty_player_squads_as_reserve()
        {
            var sr = new SquadRoster(Cfg());
            sr.Add(new Squad("p1", "My Squad", RoleFamily.Armor, SquadOrigin.Player));
            sr.Reconcile(new List<UnitView>());
            Assert.Single(sr.Squads);                       // player squad not disbanded
            Assert.Equal(SquadStatus.Reserve, sr.Squads[0].Status);
        }
    }
}
