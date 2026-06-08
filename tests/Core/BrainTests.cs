using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Xunit;

namespace Nucleus.Tests
{
    public class BrainTests
    {
        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);

        private static EnemyView E(string id, Vec3 pos, float priority = 1f) =>
            new EnemyView(id, pos, UnitClass.GroundVehicle, default, true, priority, 0);

        private static EnemyView EArmor(string id, Vec3 pos) =>
            new EnemyView(id, pos, UnitClass.GroundVehicle, new UnitCapability(Role.Armor, true, false, false, false, false), true, 1f, 0);

        private static Squad Sq(string id, RoleFamily fam, int strength, string assignedOp = null)
        {
            var s = new Squad(id, id, fam, SquadOrigin.Auto, Enumerable.Range(0, strength).Select(i => id + "u" + i));
            s.AssignedOperationId = assignedOp;
            return s;
        }

        private static BrainConfig Cfg() => new BrainConfig { ClusterRadius = 3000f, CoverageRadius = 4000f, MaxSquadsPerOperation = 2 };
        private static SquadConfig SquadCfg() => new SquadConfig { FormRadius = 4000f, MaxSquadSize = 5 };

        private static UnitView U(string id, Role role, Vec3 pos) =>
            new UnitView(id, id, pos, UnitClass.GroundVehicle, false, true,
                new UnitCapability(role, true, false, false, false, false), 1f, 0f, 1);

        [Fact]
        public void Auto_objectives_are_capped_to_the_highest_priority_targets()
        {
            // A huge spread-out front: 20 well-separated enemy clusters (each its own pocket). The AI must NOT
            // open an objective on every one — it caps to the top MaxAutoObjectives by priority.
            var cfg = new BrainConfig { ClusterRadius = 3000f, CoverageRadius = 4000f, MaxAutoObjectives = 6 };
            var state = new CommanderState(SquadCfg(), null, cfg);
            var known = new List<EnemyView>();
            for (int i = 0; i < 20; i++) known.Add(E("e" + i, P(20000 + i * 12000, 0), priority: i)); // far apart, varied priority

            // Run several ticks (objectives could otherwise accrue across ticks).
            for (int t = 0; t < 5; t++) CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), known, 0f, null, t), state);

            int auto = state.Objectives.Count(o => o.Source == ObjectiveSource.Auto);
            Assert.True(auto <= 6, $"auto objectives {auto} should be capped at 6");
            Assert.True(auto > 0, "should still create the top targets");
        }

        [Fact]
        public void Tick_generates_operation_and_tasks_from_threat()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)), U("a2", Role.Armor, P(100, 0)) };
            var known = new List<EnemyView> { E("e1", P(5000, 0)) };

            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known), state);

            Assert.Single(state.Objectives);
            Assert.Single(state.Operations);
            Assert.Equal(OperationStatus.Active, state.Operations[0].Status);
            Assert.Equal(2, tasks.Count);                                  // both armor units tasked
            Assert.All(tasks, t => Assert.Equal(TaskVerb.MoveTo, t.Verb));
        }

        [Fact]
        public void Tick_requests_production_when_no_force_is_available()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            // Enemy present but NO friendly units -> objective generated, no squads, no operation -> production need.
            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), new List<EnemyView> { E("e1", P(5000, 0)) }), state);
            Assert.NotEmpty(state.ProductionNeeds);
            Assert.True(state.ProductionNeeds[0].Total > 0);
        }

        [Fact]
        public void Tick_with_no_enemies_does_nothing()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var tasks = CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), new List<EnemyView>()), state);
            Assert.Empty(state.Objectives);
            Assert.Empty(state.Operations);
            Assert.Empty(tasks);
        }

        [Fact]
        public void Tick_emits_battle_feed_events()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };

            CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView> { E("e1", P(5000, 0)) }, 0f, null, 10f), state);
            Assert.Contains(state.Log.Recent(20), e => e.Kind == ReportKind.OperationStarted);

            CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView>(), 0f, null, 20f), state); // threat dies
            Assert.Contains(state.Log.Recent(20), e => e.Kind == ReportKind.ObjectiveComplete);
        }

        [Fact]
        public void Tick_softens_with_artillery_before_committing_armor()
        {
            var state = new CommanderState(SquadCfg(), new Doctrine { RiskTolerance = 0f }, Cfg());
            var roster = new List<UnitView> { U("armor1", Role.Armor, P(0, 0)), U("art1", Role.Artillery, P(50, 0)) };
            var known = new List<EnemyView> { EArmor("e1", P(5000, 0)), EArmor("e2", P(5010, 0)), EArmor("e3", P(5020, 0)) };

            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known), state);

            Assert.Equal(CombatPhase.Strike, state.Operations[0].CombatPhase); // not yet softened
            Assert.Contains(tasks, t => t.UnitId == "art1");                   // artillery engages (soften)
            Assert.DoesNotContain(tasks, t => t.UnitId == "armor1");           // armor held until softened
        }

        [Fact]
        public void Tick_completes_operation_when_threat_gone_and_frees_squad()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView> { E("e1", P(5000, 0)) }), state);
            Assert.Single(state.Operations);
            var sqId = state.Operations[0].SquadIds[0];
            Assert.Equal(state.Operations[0].Id, state.Squads.ById(sqId).AssignedOperationId);

            // Threat dies: op completes + is removed, objective pruned, squad freed (B1+B2).
            CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView>()), state);
            Assert.Empty(state.Operations);
            Assert.Empty(state.Objectives);
            Assert.Null(state.Squads.ById(sqId).AssignedOperationId);
        }

        [Fact]
        public void Tick_does_not_re_task_unchanged_units()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var first = CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView> { E("e1", P(5000, 0)) }), state);
            Assert.Single(first);                                   // a1 tasked once
            var second = CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView> { E("e1", P(5000, 0)) }), state);
            Assert.Empty(second);                                   // same objective -> no re-spam
        }

        [Fact]
        public void Tick_re_tasks_units_when_their_objective_moves()
        {
            // Drag-to-move (and re-type) mutate Objective.Position/Kind in place. The brain must re-route the
            // already-committed units to the new point — not de-dup them away on the unchanged objective id and
            // leave them driving to the old spot (stale-position desync regression).
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var enemy = new List<EnemyView> { E("e1", P(5000, 0)) };
            state.Objectives.Add(new Objective("obj-1", ObjectiveKind.DestroyTarget, P(5000, 0), ObjectiveSource.Player, priority: 5f));

            var first = CommanderBrain.Tick(new WorldSnapshot(roster, enemy), state);
            Assert.Contains(first, t => t.UnitId == "a1" && t.Position.HorizontalDistanceTo(P(5000, 0)) < 1f);

            // Unchanged -> no re-task (the de-dup still suppresses per-tick SetDestination spam).
            Assert.Empty(CommanderBrain.Tick(new WorldSnapshot(roster, enemy), state));

            // Move it (still within coverage of the live enemy so the op stays Active) -> the unit re-tasks.
            state.Objectives[0].Position = P(5400, 0);
            var moved = CommanderBrain.Tick(new WorldSnapshot(roster, enemy), state);
            Assert.Contains(moved, t => t.UnitId == "a1" && t.Position.HorizontalDistanceTo(P(5400, 0)) < 1f);
        }

        [Fact]
        public void DefendArea_op_tasks_its_defensive_squads()
        {
            // A DefendArea holds ground — it must task its {AirDefense, Armor} squads. Previously
            // the phase-gated active set ({AirCombat,Artillery} in Strike) excluded them, so the AI's home
            // defence issued ZERO unit tasks in-game (a silent no-op the sim masked via proximity attrition).
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            var roster = new List<UnitView> { U("adu0", Role.GroundAirDefense, P(1000, 0)), U("armu0", Role.Armor, P(1000, 0)) };
            state.Squads.Add(Sq("ad", RoleFamily.AirDefense, 1));   // member adu0
            state.Squads.Add(Sq("arm", RoleFamily.Armor, 1));       // member armu0
            state.Objectives.Add(new Objective("def-1", ObjectiveKind.DefendArea, P(1000, 0), ObjectiveSource.Auto, priority: 50f));
            var enemy = new List<EnemyView> { E("e1", P(1000, 0)) };  // within coverage → op stays active

            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, enemy, 0f, null, 0f), state);
            Assert.Single(state.Operations);
            Assert.Contains(tasks, t => t.UnitId == "adu0");   // air-defense tasked to hold (never tasked before the fix)
            Assert.Contains(tasks, t => t.UnitId == "armu0");  // armor tasked too
            Assert.All(tasks, t => Assert.Equal(TaskVerb.MoveTo, t.Verb));
        }

        [Fact]
        public void Home_defense_does_not_proliferate_as_home_drifts()
        {
            // Home is a MOVING centroid. A defence must NOT spawn a fresh DefendArea every time home
            // drifts past CoverageRadius while the previous one still lingers (the proliferation that starved the
            // offence). The covered-check spans DefendRadius, so a home drifting within that band stays covered.
            var state = new CommanderState(SquadCfg(), null, Cfg()) { HomeBase = P(1000, 1000) };
            var roster = new List<UnitView> { U("d0", Role.Armor, P(1000, 1000)) };   // forms a defensive squad
            var enemy = new List<EnemyView> { E("e1", P(3500, 1000)) };               // within DefendRadius of both homes

            CommanderBrain.Tick(new WorldSnapshot(roster, enemy, 0f, null, 0f), state);
            Assert.Single(state.Objectives, o => o.Kind == ObjectiveKind.DefendArea);

            state.HomeBase = P(6000, 1000);   // drifts 5000m: past CoverageRadius (4000) but within DefendRadius (8000)
            CommanderBrain.Tick(new WorldSnapshot(roster, enemy, 0f, null, 1f), state);

            Assert.Single(state.Objectives, o => o.Kind == ObjectiveKind.DefendArea);  // still covered → no 2nd chase objective
        }

        [Fact]
        public void AutoFill_gives_a_contested_squad_to_the_higher_priority_objective()
        {
            // With one Armor squad and both an older low-priority DestroyTarget (5) and a newer
            // high-priority DefendArea (50) lacking ops, auto-fill must hand the squad to the DefendArea
            // (priority order), not to whichever objective was created first.
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            var roster = new List<UnitView> { U("au0", Role.Armor, P(0, 0)) };
            state.Squads.Add(Sq("a", RoleFamily.Armor, 1));   // member au0
            state.Objectives.Add(new Objective("off-1", ObjectiveKind.DestroyTarget, P(5000, 0), ObjectiveSource.Player, priority: 5f));  // older, lower
            state.Objectives.Add(new Objective("def-1", ObjectiveKind.DefendArea, P(0, 0), ObjectiveSource.Player, priority: 50f));        // newer, higher

            CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView>(), 0f, null, 0f), state);

            Assert.Single(state.Operations);
            Assert.Equal(ObjectiveKind.DefendArea, state.Operations[0].Objective.Kind);
        }

        [Fact]
        public void DefendArea_op_is_stable_against_a_mid_range_threat()
        {
            // A DefendArea is raised at DefendRadius (8000m) but was advanced/pruned at the tighter
            // CoverageRadius (4000m), so a threat in the 4000–8000 band made the op Complete on its first advance
            // tick and the objective flap. With the kind-aware radius it must stay Active across ticks.
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            var roster = new List<UnitView> { U("defu0", Role.Armor, P(0, 0)) };
            state.Squads.Add(Sq("def", RoleFamily.Armor, 1));   // member defu0
            state.Objectives.Add(new Objective("def-1", ObjectiveKind.DefendArea, P(0, 0), ObjectiveSource.Auto, priority: 50f));
            var enemy = new List<EnemyView> { E("e1", P(6000, 0)) };  // inside DefendRadius (8000), outside CoverageRadius (4000)

            for (int t = 0; t < 3; t++) CommanderBrain.Tick(new WorldSnapshot(roster, enemy, 0f, null, t), state);

            Assert.Single(state.Objectives, o => o.Kind == ObjectiveKind.DefendArea); // not pruned/flapped
            Assert.Single(state.Operations);
            Assert.Equal(OperationStatus.Active, state.Operations[0].Status);                // not prematurely Complete
        }

        [Fact]
        public void Tick_fails_operation_and_reports_when_force_is_wiped_but_threat_remains()
        {
            // An active op that loses its whole force while the threat
            // remains is set Failed and reported "lost the force"; its terminal op is then pruned + squad freed.
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var enemy = new List<EnemyView> { E("e1", P(5000, 0)) };
            CommanderBrain.Tick(new WorldSnapshot(roster, enemy, 0f, null, 10f), state);
            Assert.Single(state.Operations);

            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), enemy, 0f, null, 20f), state); // force wiped
            Assert.Contains(state.Log.Recent(20), e => e.Kind == ReportKind.Blocked && e.Text.Contains("lost the force"));
            Assert.Empty(state.Operations);      // terminal op pruned
            Assert.Empty(state.Squads.Squads);   // force wiped → no squads remain
        }

        [Fact]
        public void Recon_op_completes_only_after_all_contacts_become_accurate()
        {
            // A Recon op resolves only once no low-confidence (inaccurate) contact remains near it.
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            var roster = new List<UnitView> { U("recu0", Role.GroundRadar, P(0, 0)) };
            state.Squads.Add(Sq("rec", RoleFamily.Recon, 1));   // member "recu0" → matches the Recon objective
            state.Objectives.Add(new Objective("obj-r", ObjectiveKind.Recon, P(5000, 0), ObjectiveSource.Player, priority: 5f));

            var inaccurate = new List<EnemyView> { new EnemyView("e1", P(5000, 0), UnitClass.GroundVehicle, default, false, 1f, 0) };
            CommanderBrain.Tick(new WorldSnapshot(roster, inaccurate, 0f, null, 10f), state);
            Assert.Single(state.Operations);

            CommanderBrain.Tick(new WorldSnapshot(roster, inaccurate, 0f, null, 20f), state); // still fuzzy → not resolved
            Assert.Single(state.Operations);
            Assert.Equal(OperationStatus.Active, state.Operations[0].Status);

            var accurate = new List<EnemyView> { E("e1", P(5000, 0)) };   // E(...) builds an accurate contact
            CommanderBrain.Tick(new WorldSnapshot(roster, accurate, 0f, null, 30f), state); // resolved → complete
            Assert.Contains(state.Log.Recent(20), e => e.Kind == ReportKind.ObjectiveComplete);
        }

        [Fact]
        public void Recon_production_need_uses_only_recon_suitable_families()
        {
            // An unfillable Recon objective must request families MatchSquads can field for it
            // (Recon/AirCombat) — the old Armor default could never satisfy it, looping wasted buys.
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            state.Objectives.Add(new Objective("r", ObjectiveKind.Recon, P(5000, 0), ObjectiveSource.Player, priority: 5f));

            CommanderBrain.Tick(new WorldSnapshot(new List<UnitView>(), new List<EnemyView>(), 0f, null, 0f), state);

            Assert.NotEmpty(state.ProductionNeeds);
            var suitable = Families.SuitableFor(ObjectiveKind.Recon);
            foreach (var need in state.ProductionNeeds)
                foreach (var kv in need.Items)
                    Assert.Contains(kv.Key, suitable);
        }

        [Fact]
        public void Tick_excludes_manually_committed_units()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var snap = new WorldSnapshot(roster, new List<EnemyView> { E("e1", P(5000, 0)) }, 0f, new HashSet<string> { "a1" });
            var tasks = CommanderBrain.Tick(snap, state);
            Assert.Empty(tasks);                                    // manually-owned -> not auto-squadded
            Assert.Empty(state.Squads.Squads);
        }

        [Fact]
        public void AutoFill_off_surfaces_objectives_and_squads_but_opens_no_operations()
        {
            // AI Auto-fill OFF: the brain still forms squads and (AI Commander on) surfaces objectives, but it
            // opens no operations and issues no tasking — the human assigns squads.
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiAutoFill = false };
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var known = new List<EnemyView> { E("e1", P(5000, 0)) };
            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known), state);

            Assert.Empty(tasks);                       // ...no tasking
            Assert.Empty(state.Operations);            // ...no operations opened
            Assert.Empty(state.ProductionNeeds);       // ...the human recruits, not the AI
            Assert.NotEmpty(state.Objectives);         // AI Commander default on -> objectives surfaced
            Assert.NotEmpty(state.Squads.Squads);      // and the force IS organized into squads
        }

        [Fact]
        public void AiCreatesObjectives_off_means_no_auto_objectives()
        {
            var state = new CommanderState(SquadCfg(), null, Cfg()) { AiCreatesObjectives = false };
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var known = new List<EnemyView> { E("e1", P(5000, 0)) };
            CommanderBrain.Tick(new WorldSnapshot(roster, known), state);
            Assert.Empty(state.Objectives);            // only the human creates objectives
            Assert.NotEmpty(state.Squads.Squads);      // but squads still form (mod always on)
        }

        [Fact]
        public void Auto_objectives_get_unique_ids_across_ticks()
        {
            // Regression: a tick-local objective-id counter would re-issue "auto-obj-0" for a NEW cluster on a
            // later tick (the first stays "covered"), colliding ids and hiding the new threat. Ids must be
            // unique + monotonic across ticks so OperationFor / RemoveObjective / LastObjectiveByUnit stay sound.
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };

            CommanderBrain.Tick(new WorldSnapshot(roster, new List<EnemyView> { E("eA", P(5000, 0)) }), state);
            CommanderBrain.Tick(new WorldSnapshot(roster,
                new List<EnemyView> { E("eA", P(5000, 0)), E("eC", P(90000, 90000)) }), state);

            var ids = state.Objectives.Select(o => o.Id).ToList();
            Assert.True(state.Objectives.Count >= 2, "both clusters tracked");
            Assert.Equal(ids.Count, ids.Distinct().Count());   // no duplicate ids
        }

        [Fact]
        public void Tick_yields_a_manual_operation()
        {
            // Per-op Manual override: flip an operation to Manual and the brain stops tasking its squads
            // (the player drives that slice).
            var state = new CommanderState(SquadCfg(), null, Cfg());
            var roster = new List<UnitView> { U("a1", Role.Armor, P(0, 0)) };
            var known = new List<EnemyView> { E("e1", P(5000, 0)) };
            CommanderBrain.Tick(new WorldSnapshot(roster, known), state);
            Assert.Single(state.Operations);

            state.Operations[0].Autonomy = AutonomyLevel.Manual;
            state.LastObjectiveByUnit.Clear();
            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known), state);
            Assert.Empty(tasks);   // AI yielded the manual slice
        }

        [Fact]
        public void MatchSquads_excludes_manual_owned_squads()
        {
            var obj = new Objective("o", ObjectiveKind.DestroyTarget, P(0, 0), ObjectiveSource.Auto);
            var manual = Sq("armorManual", RoleFamily.Armor, 5);
            manual.Autonomy = AutonomyLevel.Manual;                      // player owns it
            var squads = new List<Squad> { manual, Sq("armorAuto", RoleFamily.Armor, 1) };
            var chosen = CommanderBrain.MatchSquads(obj, squads, Cfg());
            Assert.Equal(new[] { "armorAuto" }, chosen.ToArray());       // manual squad never auto-pulled
        }

        [Fact]
        public void MatchSquads_picks_one_squad_per_suitable_family()
        {
            // DestroyTarget suits Armor + Artillery + AirCombat — assign one squad of each (so each combat
            // phase has its squad), regardless of location. Strongest squad per family.
            var obj = new Objective("o", ObjectiveKind.DestroyTarget, P(0, 0), ObjectiveSource.Auto);
            var squads = new List<Squad>
            {
                Sq("armorWeak", RoleFamily.Armor, 1),
                Sq("armorStrong", RoleFamily.Armor, 4),
                Sq("arty", RoleFamily.Artillery, 1),
                Sq("air", RoleFamily.AirCombat, 1),
                Sq("supply", RoleFamily.Supply, 1),   // not suitable -> excluded
            };
            var chosen = CommanderBrain.MatchSquads(obj, squads, Cfg()).ToHashSet();
            Assert.Contains("armorStrong", chosen);   // strongest of the family
            Assert.DoesNotContain("armorWeak", chosen); // one squad per family
            Assert.Contains("arty", chosen);
            Assert.Contains("air", chosen);
            Assert.DoesNotContain("supply", chosen);
        }

        [Fact]
        public void GenerateObjectives_clusters_nearby_enemies_into_one()
        {
            var known = new List<EnemyView> { E("e1", P(0, 0)), E("e2", P(500, 0)), E("e3", P(50000, 0)) };
            var objs = CommanderBrain.GenerateObjectives(known, new List<Objective>(), Cfg());
            Assert.Equal(2, objs.Count);                              // {e1,e2} cluster + e3
            Assert.All(objs, o => Assert.Equal(ObjectiveKind.DestroyTarget, o.Kind));
        }

        [Fact]
        public void GenerateObjectives_ranks_pockets_by_threat_value()
        {
            var known = new List<EnemyView> { E("low", P(5000, 0), 1f), E("high", P(50000, 0), 20f) };
            var objs = CommanderBrain.GenerateObjectives(known, new List<Objective>(), Cfg(), P(0, 0),
                new Doctrine { RiskTolerance = 0.5f });

            Assert.Equal(2, objs.Count);
            Assert.True(objs[0].Priority > objs[1].Priority); // high-value pocket ranked first
        }

        [Fact]
        public void GenerateObjectives_skips_areas_already_covered()
        {
            var known = new List<EnemyView> { E("e1", P(0, 0)) };
            var existing = new List<Objective> { new Objective("x", ObjectiveKind.DestroyTarget, P(1000, 0), ObjectiveSource.Player) };
            Assert.Empty(CommanderBrain.GenerateObjectives(known, existing, Cfg())); // within CoverageRadius
        }

        [Fact]
        public void MatchSquads_picks_suitable_unassigned_strongest_first_capped()
        {
            var obj = new Objective("o", ObjectiveKind.DestroyTarget, P(0, 0), ObjectiveSource.Auto);
            var squads = new List<Squad>
            {
                Sq("armorBig", RoleFamily.Armor, 4),
                Sq("armorSmall", RoleFamily.Armor, 1),
                Sq("art", RoleFamily.Artillery, 2),
                Sq("supply", RoleFamily.Supply, 3),          // wrong family -> excluded
                Sq("busy", RoleFamily.Armor, 5, assignedOp: "other"), // already assigned -> excluded
            };
            var chosen = CommanderBrain.MatchSquads(obj, squads, Cfg());
            Assert.Equal(new[] { "armorBig", "art" }, chosen.ToArray()); // strongest suitable, capped at 2
        }
    }
}
