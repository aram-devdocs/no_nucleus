using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Generated;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Planning;
using CommanderLayer.Core.Roles;
using Xunit;

namespace CommanderLayer.Tests
{
    public class CommanderPlanningTests
    {
        // ---------- builders ----------
        private static UnitView Ground(string id, VehicleType t, Vec3 pos, bool commandable = true, bool disabled = false)
        {
            float aS = t == VehicleType.MBT || t == VehicleType.AFV || t == VehicleType.LCV || t == VehicleType.ART ? 1f : 0f;
            float aA = t == VehicleType.R_SAM || t == VehicleType.IR_SAM || t == VehicleType.AAA ? 1f : 0f;
            var d = new UnitDescriptor(UnitClass.GroundVehicle, aS, aA, 0f, 0f,
                hasRadar: t == VehicleType.RDR, hasTroops: false, hasCargo: t == VehicleType.TRUCK,
                captureStrength: (t == VehicleType.MBT || t == VehicleType.AFV) ? 10f : 0f,
                armorTier: t == VehicleType.MBT ? 3 : 1, commandable: commandable, vehicle: t);
            var cap = RoleClassifier.Classify(d);
            return new UnitView(id, t.ToString(), pos, UnitClass.GroundVehicle, disabled, commandable, cap, aS, aA, d.ArmorTier);
        }

        private static UnitView Ship(string id, ShipType t, Vec3 pos)
        {
            float aS = t == ShipType.FFL || t == ShipType.DDG ? 1f : 0f;
            float aA = t == ShipType.DDG || t == ShipType.FFG ? 1f : 0f;
            var d = new UnitDescriptor(UnitClass.Ship, aS, aA, 0f, 0f, false, false, false, 0f, 2, true, ship: t);
            var cap = RoleClassifier.Classify(d);
            return new UnitView(id, t.ToString(), pos, UnitClass.Ship, false, true, cap, aS, aA, 2);
        }

        private static UnitView Aircraft(string id, Vec3 pos)
        {
            var d = new UnitDescriptor(UnitClass.Aircraft, 1f, 1f, 0f, 0f, false, false, false, 0f, 0, commandable: false);
            var cap = RoleClassifier.Classify(d);
            return new UnitView(id, "jet", pos, UnitClass.Aircraft, false, false, cap, 1f, 1f, 0);
        }

        private static UnitView Missile(string id, Vec3 pos)
        {
            var d = new UnitDescriptor(UnitClass.Missile, 1f, 0f, 0f, 0f, false, false, false, 0f, 0, commandable: true);
            return new UnitView(id, "missile", pos, UnitClass.Missile, false, true, RoleClassifier.Classify(d), 1f, 0f, 0);
        }

        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);
        private static CommanderOrder Attack(Vec3 p, string target = null) => new CommanderOrder("o1", OrderKind.Attack, p, 0f, DomainSet.All, target);
        private static CommanderConfig Cfg() => new CommanderConfig { MaxUnitsPerOrder = 3, SelectionRadius = 5000f };

        // ---------- RoleClassifier ----------
        [Fact]
        public void Classifier_maps_generated_enums_to_roles()
        {
            Assert.Equal(Role.Armor, Ground("a", VehicleType.MBT, P(0, 0)).Role);
            var sam = Ground("s", VehicleType.R_SAM, P(0, 0));
            Assert.Equal(Role.GroundAirDefense, sam.Role);
            Assert.True(sam.Cap.IsAirDefense && sam.Cap.CanEngageAir && !sam.Cap.CanEngageGround);
            var truck = Ground("t", VehicleType.TRUCK, P(0, 0));
            Assert.Equal(Role.Supply, truck.Role);
            Assert.True(truck.Cap.IsSupply && !truck.Cap.CanEngageGround);
            Assert.Equal(Role.AirDefenseShip, Ship("d", ShipType.DDG, P(0, 0)).Role);
            Assert.Equal(Role.Carrier, Ship("c", ShipType.CV, P(0, 0)).Role);
        }

        [Fact]
        public void Classifier_capture_requires_manned_capable_ground()
        {
            Assert.True(Ground("mbt", VehicleType.MBT, P(0, 0)).Cap.CanCapture);
            Assert.False(Ground("ugv", VehicleType.UGV, P(0, 0)).Cap.CanCapture); // UGV cannot capture
            Assert.False(Ground("truck", VehicleType.TRUCK, P(0, 0)).Cap.CanCapture);
        }

        // ---------- OrderPlanner (the anti-stampede core) ----------
        [Fact]
        public void Attack_selects_only_ground_capable_commandable_units_in_range()
        {
            var roster = new List<UnitView>
            {
                Ground("mbt", VehicleType.MBT, P(100, 0)),     // suitable, closest
                Ground("art", VehicleType.ART, P(500, 0)),     // suitable
                Ground("sam", VehicleType.R_SAM, P(50, 0)),    // AA only → excluded from Attack
                Ground("truck", VehicleType.TRUCK, P(10, 0)),  // supply → excluded
                Aircraft("jet", P(20, 0)),                      // not commandable → excluded
                Ground("far", VehicleType.MBT, P(99999, 0)),   // out of range → excluded
            };
            var plan = OrderPlanner.Plan(Attack(P(0, 0)), roster, ThreatPicture.Empty, Cfg());
            var ids = plan.Tasks.Select(t => t.UnitId).ToList();
            Assert.Equal(new[] { "mbt", "art" }, ids);            // only suitable+in-range, sorted by distance
            Assert.All(plan.Tasks, t => Assert.Equal(TaskVerb.MoveTo, t.Verb));
        }

        [Fact]
        public void Attack_with_target_emits_attack_target_verb()
        {
            var roster = new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) };
            var plan = OrderPlanner.Plan(Attack(P(0, 0), target: "enemy7"), roster, ThreatPicture.Empty, Cfg());
            Assert.Single(plan.Tasks);
            Assert.Equal(TaskVerb.AttackTarget, plan.Tasks[0].Verb);
            Assert.Equal("enemy7", plan.Tasks[0].TargetId);
        }

        [Fact]
        public void Defend_includes_air_defense_and_ground_combat()
        {
            var roster = new List<UnitView>
            {
                Ground("sam", VehicleType.R_SAM, P(100, 0)),
                Ground("mbt", VehicleType.MBT, P(200, 0)),
                Ground("truck", VehicleType.TRUCK, P(50, 0)), // excluded
            };
            var order = new CommanderOrder("d1", OrderKind.Defend, P(0, 0), 0f);
            var plan = OrderPlanner.Plan(order, roster, ThreatPicture.Empty, Cfg());
            var ids = plan.Tasks.Select(t => t.UnitId).OrderBy(x => x).ToList();
            Assert.Equal(new[] { "mbt", "sam" }, ids);
        }

        [Fact]
        public void Attack_excludes_missiles_and_buildings()
        {
            var roster = new List<UnitView>
            {
                Ground("mbt", VehicleType.MBT, P(100, 0)),
                Missile("msl", P(50, 0)), // commandable + antiSurface, but a munition — must be excluded
            };
            var plan = OrderPlanner.Plan(Attack(P(0, 0)), roster, ThreatPicture.Empty, Cfg());
            Assert.Equal(new[] { "mbt" }, plan.Tasks.Select(t => t.UnitId).ToArray());
        }

        [Fact]
        public void Domain_filter_limits_selection()
        {
            var roster = new List<UnitView>
            {
                Ground("mbt", VehicleType.MBT, P(100, 0)),  // Land
                Ship("ffl", ShipType.FFL, P(150, 0)),       // Sea, CombatShip (can engage ground)
            };
            var landOnly = new CommanderOrder("o", OrderKind.Attack, P(0, 0), 0f, DomainSet.Land);
            Assert.Equal(new[] { "mbt" }, OrderPlanner.Plan(landOnly, roster, ThreatPicture.Empty, Cfg()).Tasks.Select(t => t.UnitId).ToArray());
            var seaOnly = new CommanderOrder("o", OrderKind.Attack, P(0, 0), 0f, DomainSet.Sea);
            Assert.Equal(new[] { "ffl" }, OrderPlanner.Plan(seaOnly, roster, ThreatPicture.Empty, Cfg()).Tasks.Select(t => t.UnitId).ToArray());
        }

        [Fact]
        public void Order_radius_overrides_config_selection_radius()
        {
            var roster = new List<UnitView>
            {
                Ground("near", VehicleType.MBT, P(200, 0)),
                Ground("far", VehicleType.MBT, P(2000, 0)),
            };
            var tight = new CommanderOrder("o", OrderKind.Attack, P(0, 0), radius: 500f);
            Assert.Equal(new[] { "near" }, OrderPlanner.Plan(tight, roster, ThreatPicture.Empty, Cfg()).Tasks.Select(t => t.UnitId).ToArray());
        }

        [Fact]
        public void Preview_reports_assignable_and_can_place()
        {
            var roster = new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)), Missile("m", P(10, 0)) };
            var prev = OrderPlanner.Preview(Attack(P(0, 0)), roster, ThreatPicture.Empty, Cfg());
            Assert.True(prev.CanPlace);
            Assert.Equal(1, prev.Count);
            var empty = OrderPlanner.Preview(Attack(P(99999, 0)), roster, ThreatPicture.Empty, Cfg());
            Assert.False(empty.CanPlace);
        }

        [Fact]
        public void Capture_selects_only_capture_capable_units()
        {
            var roster = new List<UnitView>
            {
                Ground("mbt", VehicleType.MBT, P(100, 0)),   // manned, captureStrength>0
                Ground("ugv", VehicleType.UGV, P(50, 0)),    // unmanned — cannot capture
                Ground("truck", VehicleType.TRUCK, P(60, 0)),// no capture strength
            };
            var order = new CommanderOrder("c", OrderKind.Capture, P(0, 0), 0f);
            Assert.Equal(new[] { "mbt" }, OrderPlanner.Plan(order, roster, ThreatPicture.Empty, Cfg()).Tasks.Select(t => t.UnitId).ToArray());
        }

        [Fact]
        public void Resupply_selects_only_supply_units()
        {
            var roster = new List<UnitView>
            {
                Ground("truck", VehicleType.TRUCK, P(100, 0)),
                Ground("mbt", VehicleType.MBT, P(50, 0)),
            };
            var order = new CommanderOrder("r", OrderKind.Resupply, P(0, 0), 0f);
            Assert.Equal(new[] { "truck" }, OrderPlanner.Plan(order, roster, ThreatPicture.Empty, Cfg()).Tasks.Select(t => t.UnitId).ToArray());
        }

        [Fact]
        public void Planner_respects_max_units()
        {
            var roster = Enumerable.Range(0, 10).Select(i => Ground("u" + i, VehicleType.MBT, P(i, 0))).Cast<UnitView>().ToList();
            var plan = OrderPlanner.Plan(Attack(P(0, 0)), roster, ThreatPicture.Empty, new CommanderConfig { MaxUnitsPerOrder = 3, SelectionRadius = 5000f });
            Assert.Equal(3, plan.Tasks.Count);
        }

        // ---------- AssignmentManager ----------
        [Fact]
        public void AddOrder_records_assignment_or_fails_when_none()
        {
            var mgr = new AssignmentManager(Cfg());
            var ok = mgr.AddOrder(Attack(P(0, 0)), new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) }, ThreatPicture.Empty);
            Assert.False(ok.IsEmpty);
            Assert.Equal(OrderStatus.Active, mgr.Orders[0].Status);

            // Ground-only order (no Air domain) with no combat-capable units truly fails.
            var mgr2 = new AssignmentManager(Cfg());
            mgr2.AddOrder(new CommanderOrder("o2", OrderKind.Attack, P(0, 0), 0f, DomainSet.Land),
                new List<UnitView> { Ground("truck", VehicleType.TRUCK, P(100, 0)) }, ThreatPicture.Empty);
            Assert.Equal(OrderStatus.Failed, mgr2.Orders[0].Status);
        }

        [Fact]
        public void Tick_completes_attack_when_area_clear()
        {
            var mgr = new AssignmentManager(Cfg());
            mgr.AddOrder(Attack(P(0, 0)), new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) }, ThreatPicture.Empty);
            mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(50, 0)) }, _ => ThreatPicture.Empty);
            Assert.Equal(OrderStatus.Complete, mgr.Orders[0].Status);
        }

        [Fact]
        public void Attack_does_not_complete_until_a_unit_reaches_the_area()
        {
            var mgr = new AssignmentManager(Cfg());
            mgr.AddOrder(Attack(P(0, 0)), new List<UnitView> { Ground("mbt", VehicleType.MBT, P(3000, 0)) }, ThreatPicture.Empty);

            // No known threat, but the assigned unit is still far away -> NOT complete (don't "clear" on intel loss).
            mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(3000, 0)) }, _ => ThreatPicture.Empty);
            Assert.Equal(OrderStatus.Active, mgr.Orders[0].Status);

            // Unit arrives in the area with no threat -> secure -> complete.
            mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) }, _ => ThreatPicture.Empty);
            Assert.Equal(OrderStatus.Complete, mgr.Orders[0].Status);
        }

        [Fact]
        public void Defend_issues_hold_on_arrival_once()
        {
            var mgr = new AssignmentManager(new CommanderConfig { MaxUnitsPerOrder = 3, SelectionRadius = 5000f, ArriveRadius = 250f });
            var order = new CommanderOrder("d", OrderKind.Defend, P(0, 0), 0f);
            mgr.AddOrder(order, new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) }, ThreatPicture.Empty);

            var t1 = mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(50, 0)) }, _ => ThreatPicture.Empty);
            Assert.Contains(t1, x => x.UnitId == "mbt" && x.Verb == TaskVerb.Hold);

            var t2 = mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(40, 0)) }, _ => ThreatPicture.Empty);
            Assert.DoesNotContain(t2, x => x.UnitId == "mbt" && x.Verb == TaskVerb.Hold);
        }

        [Fact]
        public void Move_selects_any_commandable_unit_and_completes_on_arrival()
        {
            var mgr = new AssignmentManager(Cfg());
            // A supply truck (no combat capability) is still movable.
            var order = new CommanderOrder("m", OrderKind.Move, P(0, 0), 5000f, DomainSet.Land);
            var plan = mgr.AddOrder(order, new List<UnitView> { Ground("truck", VehicleType.TRUCK, P(3000, 0)) }, ThreatPicture.Empty);
            Assert.Equal(new[] { "truck" }, plan.Tasks.Select(t => t.UnitId).ToArray());
            Assert.All(plan.Tasks, t => Assert.Equal(TaskVerb.MoveTo, t.Verb));

            mgr.Tick(new List<UnitView> { Ground("truck", VehicleType.TRUCK, P(3000, 0)) }, _ => ThreatPicture.Empty);
            Assert.Equal(OrderStatus.Active, mgr.Orders[0].Status);     // still en route
            mgr.Tick(new List<UnitView> { Ground("truck", VehicleType.TRUCK, P(80, 0)) }, _ => ThreatPicture.Empty);
            Assert.Equal(OrderStatus.Complete, mgr.Orders[0].Status);   // arrived
        }

        [Fact]
        public void Sead_pending_holds_aircraft_until_air_defenses_cleared()
        {
            var airOrder = new CommanderOrder("a", OrderKind.Attack, P(0, 0), 5000f, DomainSet.All);
            var sam = new EnemyView("sam", P(0, 0), UnitClass.GroundVehicle,
                new UnitCapability(Role.GroundAirDefense, false, true, false, false, true), true, 2f, 0);
            var withAD = ThreatAssessor.Assess(new List<EnemyView> { sam });

            Assert.True(OrderPlanner.SeadPending(airOrder, withAD));         // SAM present -> hold aircraft
            Assert.False(OrderPlanner.SeadPending(airOrder, ThreatPicture.Empty)); // cleared -> release
            // A ground-only order is never SEAD-gated (it has no aircraft to hold).
            var groundOrder = new CommanderOrder("g", OrderKind.Attack, P(0, 0), 5000f, DomainSet.Land);
            Assert.False(OrderPlanner.SeadPending(groundOrder, withAD));
        }

        [Fact]
        public void Air_order_is_active_even_with_no_commandable_units()
        {
            var mgr = new AssignmentManager(Cfg());
            var order = new CommanderOrder("a", OrderKind.Attack, P(0, 0), 5000f, DomainSet.Air);
            var plan = mgr.AddOrder(order, new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) }, ThreatPicture.Empty);
            Assert.True(plan.IsEmpty);                                  // ground unit excluded by Air domain
            Assert.Equal(OrderStatus.Active, mgr.Orders[0].Status);    // not Failed — aircraft steered by the AI patch
        }

        [Fact]
        public void Capture_completes_when_objective_held()
        {
            var mgr = new AssignmentManager(Cfg());
            var order = new CommanderOrder("c", OrderKind.Capture, P(0, 0), 0f);
            mgr.AddOrder(order, new List<UnitView> { Ground("mbt", VehicleType.MBT, P(100, 0)) }, ThreatPicture.Empty);

            // Not captured yet → stays active.
            mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(50, 0)) }, _ => ThreatPicture.Empty, _ => false);
            Assert.Equal(OrderStatus.Active, mgr.Orders[0].Status);

            // Objective flips to us → complete.
            mgr.Tick(new List<UnitView> { Ground("mbt", VehicleType.MBT, P(20, 0)) }, _ => ThreatPicture.Empty, _ => true);
            Assert.Equal(OrderStatus.Complete, mgr.Orders[0].Status);
        }

        [Fact]
        public void Tick_reassigns_when_all_units_lost()
        {
            var enemies = new List<EnemyView> { new EnemyView("e1", P(0, 0), UnitClass.GroundVehicle, default, true, 1f, 1) };
            var threat = ThreatAssessor.Assess(enemies);
            var mgr = new AssignmentManager(Cfg());
            mgr.AddOrder(Attack(P(0, 0)), new List<UnitView> { Ground("mbt1", VehicleType.MBT, P(100, 0)) }, threat);

            // mbt1 gone; a fresh mbt2 is available → reassign (threat still present so not complete)
            var reissued = mgr.Tick(new List<UnitView> { Ground("mbt2", VehicleType.MBT, P(120, 0)) }, _ => threat);
            Assert.Contains(mgr.Orders[0].AssignedUnitIds, id => id == "mbt2");
            Assert.Contains(reissued, t => t.UnitId == "mbt2");
            Assert.Equal(OrderStatus.Active, mgr.Orders[0].Status);
        }
    }
}
