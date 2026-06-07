using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Sim.Tests
{
    /// <summary>WS7 — squad rows must read as composition ("2× MBT, 1× IFV"), not just a family count.</summary>
    public class SquadLegibilityTests
    {
        private static UnitView U(string id, Role role) => new UnitView(
            id, id, new Vec3(0, 0, 0), UnitClass.GroundVehicle, disabled: false, commandable: true,
            new UnitCapability(role, canEngageGround: true, canEngageAir: false,
                canCapture: role == Role.Armor || role == Role.Ifv, isSupply: false, isAirDefense: false),
            antiSurface: 1f, antiAir: 0f, armorTier: 3);

        [Fact]
        public void RoleLabels_are_short_and_distinct()
        {
            Assert.Equal("MBT", RoleLabels.Short(Role.Armor));
            Assert.Equal("IFV", RoleLabels.Short(Role.Ifv));
            Assert.Equal("SAM", RoleLabels.Short(Role.GroundAirDefense));
        }

        [Fact]
        public void Squad_view_shows_composition_from_the_roster_role_map()
        {
            var state = new CommanderState();
            var roster = new List<UnitView> { U("a1", Role.Armor), U("a2", Role.Armor), U("i1", Role.Ifv) };
            state.Squads.Reconcile(roster, null);           // auto-forms an Armor-family squad of 3
            var roles = roster.ToDictionary(u => u.Id, u => u.Role);

            var hq = HqView.Build(state, state.Log, null, 10, roles);
            var sq = hq.Squads.First();
            Assert.Equal("2× MBT, 1× IFV", sq.Composition);
        }

        [Fact]
        public void Composition_is_empty_without_a_role_map_so_the_ui_falls_back()
        {
            var state = new CommanderState();
            var roster = new List<UnitView> { U("a1", Role.Armor) };
            state.Squads.Reconcile(roster, null);

            var hq = HqView.Build(state, state.Log, null); // no role map (e.g. enemy-side Hq)
            Assert.Equal("", hq.Squads.First().Composition);
        }
    }
}
