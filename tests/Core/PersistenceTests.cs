using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Nucleus.Core.Persistence;
using Xunit;

namespace Nucleus.Core.Tests
{
    /// <summary>
    /// Save/resume tests for the campaign persistence seam: capture→restore is lossless on the object model,
    /// the text format round-trips losslessly (including the tricky cases — null vs empty, tabs/newlines in
    /// names, empty campaign, forward-compat unknown records), and the id counters survive so resumed play
    /// can't collide with restored ids.
    /// </summary>
    public class PersistenceTests
    {
        private static CommanderState BuildPopulated()
        {
            var state = new CommanderState(
                new SquadConfig { FormRadius = 1234f, MaxSquadSize = 7, DepletedFraction = 0.4f },
                new Doctrine { RiskTolerance = 0.7f, ForceRatio = 2.2f },
                new BrainConfig { ClusterRadius = 2500f, CoverageRadius = 3500f, MaxSquadsPerOperation = 3 })
            {
                AiCreatesObjectives = false,
                AiAutoFill = true,
                HomeBase = new Vec3(10f, 20f, 30f),
            };
            state.Squads.BatchSeed = 4;

            // Drive the op-id counter so continuity can be asserted after restore.
            state.NextOperationId();
            state.NextOperationId(); // seed = 2

            var obj = new Objective("obj-1", ObjectiveKind.CapturePoint, new Vec3(100f, 0f, 200f),
                ObjectiveSource.Player, targetId: null, priority: 3.5f);
            var obj2 = new Objective("obj-2", ObjectiveKind.DestroyTarget, new Vec3(400f, 0f, 500f),
                ObjectiveSource.Auto, targetId: "enemy-9", priority: 1.25f);
            state.Objectives.Add(obj);
            state.Objectives.Add(obj2);

            var comp = new Composition();
            comp.Add(RoleFamily.Armor, 2);
            comp.Add(RoleFamily.Artillery, 1);
            var squad = new Squad("sq-1", "Armor\tAlpha", RoleFamily.Armor, SquadOrigin.Player,
                new[] { "u-1", "u-2", "u\n3" })
            {
                Status = SquadStatus.Engaged,
                Autonomy = AutonomyLevel.Manual,
                AssignedOperationId = "op-1",
                TargetComposition = comp,
            };
            state.Squads.Add(squad);

            var enemies = new List<EnemyView>
            {
                new EnemyView("e-1", new Vec3(410f, 0f, 510f), UnitClass.GroundVehicle,
                    new UnitCapability(Role.GroundAirDefense, false, true, false, false, true), true, 3f, 2),
                new EnemyView("e-2", new Vec3(420f, 0f, 520f), UnitClass.Aircraft,
                    new UnitCapability(Role.Fighter, false, true, false, false, false), false, 2f, 1),
            };
            var op = new Operation("op-1", obj2, new[] { "sq-1" })
            {
                Autonomy = AutonomyLevel.Auto,
                Status = OperationStatus.Active,
                CombatPhase = CombatPhase.Strike,
                InitialThreat = new ThreatPicture(enemies),
            };
            state.Operations.Add(op);

            state.ConfirmedObjectives.Add("obj-2");
            state.LastObjectiveByUnit["u-1"] = "obj-1";
            state.LastObjectiveByUnit["u-2"] = "obj-2";
            return state;
        }

        private static void AssertEquivalent(CommanderState a, CommanderState b)
        {
            Assert.Equal(a.AiCreatesObjectives, b.AiCreatesObjectives);
            Assert.Equal(a.AiAutoFill, b.AiAutoFill);
            Assert.Equal(a.HomeBase.X, b.HomeBase.X);
            Assert.Equal(a.HomeBase.Y, b.HomeBase.Y);
            Assert.Equal(a.HomeBase.Z, b.HomeBase.Z);
            Assert.Equal(a.Squads.BatchSeed, b.Squads.BatchSeed);
            Assert.Equal(a.Doctrine.RiskTolerance, b.Doctrine.RiskTolerance);
            Assert.Equal(a.Doctrine.ForceRatio, b.Doctrine.ForceRatio);
            Assert.Equal(a.BrainConfig.ClusterRadius, b.BrainConfig.ClusterRadius);
            Assert.Equal(a.BrainConfig.CoverageRadius, b.BrainConfig.CoverageRadius);
            Assert.Equal(a.BrainConfig.MaxSquadsPerOperation, b.BrainConfig.MaxSquadsPerOperation);
            Assert.Equal(a.Squads.Config.FormRadius, b.Squads.Config.FormRadius);
            Assert.Equal(a.Squads.Config.MaxSquadSize, b.Squads.Config.MaxSquadSize);
            Assert.Equal(a.Squads.Config.DepletedFraction, b.Squads.Config.DepletedFraction);

            Assert.Equal(a.Objectives.Count, b.Objectives.Count);
            for (int i = 0; i < a.Objectives.Count; i++)
            {
                Assert.Equal(a.Objectives[i].Id, b.Objectives[i].Id);
                Assert.Equal(a.Objectives[i].Kind, b.Objectives[i].Kind);
                Assert.Equal(a.Objectives[i].Position.X, b.Objectives[i].Position.X);
                Assert.Equal(a.Objectives[i].Position.Z, b.Objectives[i].Position.Z);
                Assert.Equal(a.Objectives[i].TargetId, b.Objectives[i].TargetId);
                Assert.Equal(a.Objectives[i].Priority, b.Objectives[i].Priority);
                Assert.Equal(a.Objectives[i].Source, b.Objectives[i].Source);
            }

            Assert.Equal(a.Squads.Squads.Count, b.Squads.Squads.Count);
            for (int i = 0; i < a.Squads.Squads.Count; i++)
            {
                var x = a.Squads.Squads[i];
                var y = b.Squads.Squads[i];
                Assert.Equal(x.Id, y.Id);
                Assert.Equal(x.Name, y.Name);
                Assert.Equal(x.Family, y.Family);
                Assert.Equal(x.Origin, y.Origin);
                Assert.Equal(x.Status, y.Status);
                Assert.Equal(x.Autonomy, y.Autonomy);
                Assert.Equal(x.AssignedOperationId, y.AssignedOperationId);
                Assert.Equal(x.MemberUnitIds, y.MemberUnitIds);
                Assert.Equal(x.TargetComposition?.Items.Count() ?? -1, y.TargetComposition?.Items.Count() ?? -1);
                if (x.TargetComposition != null)
                    foreach (var kv in x.TargetComposition.Items)
                        Assert.Equal(kv.Value, y.TargetComposition.Get(kv.Key));
            }

            Assert.Equal(a.Operations.Count, b.Operations.Count);
            for (int i = 0; i < a.Operations.Count; i++)
            {
                var x = a.Operations[i];
                var y = b.Operations[i];
                Assert.Equal(x.Id, y.Id);
                Assert.Equal(x.Objective.Id, y.Objective.Id);
                Assert.Equal(x.SquadIds, y.SquadIds);
                Assert.Equal(x.Autonomy, y.Autonomy);
                Assert.Equal(x.Status, y.Status);
                Assert.Equal(x.CombatPhase, y.CombatPhase);
                Assert.Equal(x.InitialThreat?.Count ?? -1, y.InitialThreat?.Count ?? -1);
                if (x.InitialThreat != null)
                {
                    Assert.Equal(x.InitialThreat.AirDefenseCount, y.InitialThreat.AirDefenseCount);
                    Assert.Equal(x.InitialThreat.AirCount, y.InitialThreat.AirCount);
                    Assert.Equal(x.InitialThreat.Enemies.Select(e => e.Id),
                                 y.InitialThreat.Enemies.Select(e => e.Id));
                }
            }

            Assert.Equal(a.ConfirmedObjectives.OrderBy(s => s), b.ConfirmedObjectives.OrderBy(s => s));
            Assert.Equal(a.LastObjectiveByUnit.OrderBy(k => k.Key), b.LastObjectiveByUnit.OrderBy(k => k.Key));
        }

        [Fact]
        public void Capture_then_restore_is_lossless()
        {
            var original = BuildPopulated();
            var restored = CampaignState.Restore(CampaignState.Capture(original));
            AssertEquivalent(original, restored);
        }

        [Fact]
        public void Serialize_then_deserialize_then_restore_is_lossless()
        {
            var original = BuildPopulated();
            var snap = CampaignState.Capture(original);
            var text = CampaignSave.Serialize(snap);
            var restored = CampaignState.Restore(CampaignSave.Deserialize(text));
            AssertEquivalent(original, restored);
        }

        [Fact]
        public void Serialized_text_carries_the_version_header()
        {
            var text = CampaignSave.Serialize(CampaignState.Capture(BuildPopulated()));
            Assert.StartsWith("NUCLEUS-CAMPAIGN\t" + CampaignSnapshot.CurrentVersion, text);
        }

        [Fact]
        public void Operation_id_counter_continues_after_restore()
        {
            var original = BuildPopulated(); // seed advanced to 2
            var restored = CampaignState.Restore(CampaignState.Capture(original));
            Assert.Equal("op-3", restored.NextOperationId()); // continues, no collision with op-1
        }

        [Fact]
        public void Null_target_id_survives_distinct_from_empty()
        {
            var snap = new CampaignSnapshot();
            snap.Objectives.Add(new Objective("a", ObjectiveKind.Recon, default, ObjectiveSource.Auto, targetId: null));
            snap.Objectives.Add(new Objective("b", ObjectiveKind.Recon, default, ObjectiveSource.Auto, targetId: ""));
            var back = CampaignSave.Deserialize(CampaignSave.Serialize(snap));
            Assert.Null(back.Objectives[0].TargetId);
            Assert.Equal("", back.Objectives[1].TargetId);
        }

        [Fact]
        public void Empty_campaign_round_trips()
        {
            var snap = new CampaignSnapshot { AiCreatesObjectives = false, AiAutoFill = false, HomeBase = new Vec3(1f, 2f, 3f) };
            var back = CampaignSave.Deserialize(CampaignSave.Serialize(snap));
            Assert.False(back.AiCreatesObjectives);
            Assert.False(back.AiAutoFill);
            Assert.Equal(1f, back.HomeBase.X);
            Assert.Empty(back.Objectives);
            Assert.Empty(back.Squads);
            Assert.Empty(back.Operations);
        }

        [Fact]
        public void Unknown_trailing_records_are_ignored()
        {
            var text = CampaignSave.Serialize(CampaignState.Capture(BuildPopulated()));
            var tampered = text + "FUTURE\tsomething\tnew\n";
            var restored = CampaignState.Restore(CampaignSave.Deserialize(tampered));
            Assert.Equal(2, restored.Objectives.Count); // obj-1, obj-2 (op's objective already in the list — deduped)
            Assert.Single(restored.Operations);          // the real records still parsed fine
        }
    }
}
