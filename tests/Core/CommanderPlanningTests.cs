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

        private static Vec3 P(float x, float z) => new Vec3(x, 0, z);
        private static CommanderOrder Attack(Vec3 p, string target = null) => new CommanderOrder("o1", OrderKind.Attack, p, 0f, target);
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

            var mgr2 = new AssignmentManager(Cfg());
            mgr2.AddOrder(new CommanderOrder("o2", OrderKind.Attack, P(0, 0), 0f),
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
