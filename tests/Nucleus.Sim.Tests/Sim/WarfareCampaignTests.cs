using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Nucleus.Core.Persistence;
using Nucleus.Core.War;
using Xunit;

namespace Nucleus.Sim
{
    /// <summary>
    /// North-star proof for the persistent two-faction war: both sides run the real brain over the same
    /// battlefield, the whole campaign saves/resumes losslessly, and a resumed war continues bit-identically
    /// to the unsaved original. Brain-independent battlefield script (enemies drift on a seeded walk) so any
    /// divergence is a persistence defect, not world noise.
    /// </summary>
    [Trait("Category", "Sim")]
    public class WarfareCampaignTests
    {
        private const float SensorRange = 9000f;

        // Each faction sees its own units as roster and the other faction as fog-of-war enemies.
        private static (WorldSnapshot blu, WorldSnapshot op) Views(List<SimUnit> a, List<SimUnit> b, int t)
        {
            return (View(a, b, t), View(b, a, t));
        }

        private static WorldSnapshot View(List<SimUnit> own, List<SimUnit> foe, int t)
        {
            var roster = own.Where(u => u.Alive).Select(u => u.ToUnitView()).ToList();
            var known = new List<EnemyView>();
            foreach (var e in foe.Where(u => u.Alive))
            {
                float nearest = roster.Count == 0 ? float.MaxValue
                    : roster.Min(f => f.Position.HorizontalDistanceTo(e.Pos));
                if (nearest <= SensorRange) known.Add(e.ToEnemyView(nearest <= SensorRange * 0.6f));
            }
            return new WorldSnapshot(roster, known, 5000f, null, t);
        }

        private static (List<SimUnit> a, List<SimUnit> b) Forces()
        {
            var (a, b) = Scenarios.DualForces();
            return (a, b);
        }

        private static string Fingerprint(WarfareCampaign c, WarfareCampaign.StepResult r)
        {
            var sb = new StringBuilder();
            sb.Append("T").Append(c.Turn).Append(" A[");
            foreach (var t in r.Blufor.OrderBy(x => x.UnitId)) sb.Append(t.UnitId).Append(':').Append(t.Verb).Append(',');
            sb.Append("] B[");
            foreach (var t in r.Opfor.OrderBy(x => x.UnitId)) sb.Append(t.UnitId).Append(':').Append(t.Verb).Append(',');
            sb.Append("] objA=").Append(c.Blufor.Objectives.Count).Append(" objB=").Append(c.Opfor.Objectives.Count);
            sb.Append(" opsA=").Append(c.Blufor.Operations.Count).Append(" opsB=").Append(c.Opfor.Operations.Count);
            return sb.ToString();
        }

        [Fact]
        public void Both_factions_run_the_brain_and_generate_objectives()
        {
            var (a, b) = Forces();
            var rng = new Pcg(0xBEEF);
            var war = new WarfareCampaign();
            for (int t = 0; t < 30; t++)
            {
                foreach (var e in a.Concat(b)) { e.X += rng.Range(-20f, 20f); e.Z += rng.Range(-20f, 20f); }
                var (blu, op) = Views(a, b, t);
                war.Step(blu, op);
            }
            Assert.Equal(30, war.Turn);
            Assert.NotEmpty(war.Blufor.Objectives);
            Assert.NotEmpty(war.Opfor.Objectives);
        }

        [Fact]
        public void Whole_war_saves_and_resumes_losslessly()
        {
            var (a, b) = Forces();
            var rng = new Pcg(0x1234);
            var war = new WarfareCampaign();
            for (int t = 0; t < 25; t++)
            {
                foreach (var e in a.Concat(b)) { e.X += rng.Range(-20f, 20f); e.Z += rng.Range(-20f, 20f); }
                var (blu, op) = Views(a, b, t);
                war.Step(blu, op);
            }

            var restored = WarfareSave.Deserialize(WarfareSave.Serialize(war));
            Assert.Equal(war.Turn, restored.Turn);
            Assert.Equal(war.Blufor.Objectives.Count, restored.Blufor.Objectives.Count);
            Assert.Equal(war.Opfor.Objectives.Count, restored.Opfor.Objectives.Count);
            Assert.Equal(war.Blufor.Operations.Count, restored.Blufor.Operations.Count);
            Assert.Equal(war.Opfor.Operations.Count, restored.Opfor.Operations.Count);
            Assert.Equal(war.Blufor.Squads.Squads.Count, restored.Blufor.Squads.Squads.Count);
        }

        [Fact]
        public void Resumed_war_continues_identically_to_the_unsaved_original()
        {
            const int warmup = 30, tail = 40;
            // Precompute a brain-independent battlefield script shared by both branches.
            var (a, b) = Forces();
            var rng = new Pcg(0xC0DE);
            var scriptViews = new List<(WorldSnapshot blu, WorldSnapshot op)>();
            for (int t = 0; t < warmup + tail; t++)
            {
                foreach (var e in a.Concat(b)) { e.X += rng.Range(-20f, 20f); e.Z += rng.Range(-20f, 20f); }
                scriptViews.Add(Views(a, b, t));
            }

            var war = new WarfareCampaign();
            for (int t = 0; t < warmup; t++) war.Step(scriptViews[t].blu, scriptViews[t].op);

            var saved = WarfareSave.Serialize(war); // independent copy at the save point

            var traceOriginal = new StringBuilder();
            for (int t = warmup; t < warmup + tail; t++)
                traceOriginal.AppendLine(Fingerprint(war, war.Step(scriptViews[t].blu, scriptViews[t].op)));

            var resumed = WarfareSave.Deserialize(saved);
            var traceResumed = new StringBuilder();
            for (int t = warmup; t < warmup + tail; t++)
                traceResumed.AppendLine(Fingerprint(resumed, resumed.Step(scriptViews[t].blu, scriptViews[t].op)));

            Assert.Equal(traceOriginal.ToString(), traceResumed.ToString());
        }

        [Fact]
        public void Battlefield_losses_drive_each_factions_attrition_score()
        {
            var (a, b) = Forces();
            var rng = new Pcg(0xA77);
            var war = new WarfareCampaign();
            float bluStart = war.War.Blufor.Score.Score, opStart = war.War.Opfor.Score.Score;

            for (int t = 0; t < 40; t++)
            {
                foreach (var e in a.Concat(b)) { e.X += rng.Range(-20f, 20f); e.Z += rng.Range(-20f, 20f); }
                // Kill one unit from each side on a couple of ticks — the roster shrinks ⇒ attrition.
                if (t == 10) { a.First(u => u.Alive).Hp = 0f; }
                if (t == 15) { b.First(u => u.Alive).Hp = 0f; b.First(u => u.Alive).Hp = 0f; }
                var (blu, op) = Views(a, b, t);
                war.Step(blu, op);
            }

            Assert.True(war.War.Blufor.Score.Score < bluStart, "blufor took losses ⇒ score must drop");
            Assert.True(war.War.Opfor.Score.Score < opStart, "opfor took losses ⇒ score must drop");
            Assert.Equal(1, war.War.Blufor.Score.UnitsLost);
            Assert.True(war.War.Opfor.Score.UnitsLost >= 1);
        }

        [Fact]
        public void Reinforcement_spend_and_base_loss_persist_across_save_resume()
        {
            var war = new WarfareCampaign();
            war.War.Blufor.Commander = CommanderKind.Human;     // a human-led side
            war.RecordBaseLost(blufor: false, count: 2);        // opfor loses 2 bases
            Assert.True(war.Reinforce(blufor: true, cost: 500f)); // blufor buys reinforcements
            float bluScore = war.War.Blufor.Score.Score;
            float bluFunds = war.War.Blufor.Funds;

            var restored = WarfareSave.Deserialize(WarfareSave.Serialize(war));
            Assert.Equal(CommanderKind.Human, restored.War.Blufor.Commander);
            Assert.Equal(2, restored.War.Opfor.Score.BasesLost);
            Assert.Equal(bluScore, restored.War.Blufor.Score.Score, 2);
            Assert.Equal(bluFunds, restored.War.Blufor.Funds, 2);
            Assert.Equal(500f, restored.War.Blufor.Score.TotalSpent, 2);
        }

        [Fact]
        public void A_side_attrited_to_zero_ends_the_war_with_a_winner()
        {
            var war = new WarfareCampaign();
            Assert.False(war.IsOver);
            for (int i = 0; i < 30; i++) war.RecordBaseLost(blufor: false); // grind opfor down
            Assert.True(war.IsOver);
            var board = war.SnapshotBoard();
            Assert.True(board.Over);
            Assert.Equal(war.War.Blufor.FactionName, board.WinnerName);
        }

        [Fact]
        public void Explicit_loss_reporting_counts_every_kill_even_when_reinforcements_arrive()
        {
            // The live driver feeds exact kills (roster heuristic off), so a reinforcing side is not under-bled.
            var war = new WarfareCampaign { UseRosterAttrition = false };
            war.RecordUnitLost(blufor: true, count: 3);   // 3 died this tick...
            // ...even though the roster also grew by 2 reinforcements (net -1) — all 3 must still count.
            Assert.Equal(3, war.War.Blufor.Score.UnitsLost);
        }

        [Fact]
        public void A_faction_name_with_delimiters_does_not_corrupt_the_save()
        {
            var war = new WarfareCampaign(
                war: new WarState(new WarSide("Red\tForce\nNorth"), new WarSide("Blue")));
            war.RecordBaseLost(blufor: true, count: 1);
            var restored = WarfareSave.Deserialize(WarfareSave.Serialize(war));
            // Name is sanitized (delimiters stripped) but the record stays intact and aligned.
            Assert.DoesNotContain('\t', restored.War.Blufor.FactionName);
            Assert.Equal(1, restored.War.Blufor.Score.BasesLost);
            Assert.Equal("Blue", restored.War.Opfor.FactionName);
        }

        [Fact]
        public void V1_save_without_a_war_section_still_loads_with_a_default_war()
        {
            // A legacy v1 file: header without baselines, no @@NUCLEUS-WAR@@ lines.
            var legacy =
                "NUCLEUS-WARFARE\t1\t7\n" +
                "@@NUCLEUS-FACTION@@\tBlufor\n" +
                "@@NUCLEUS-FACTION@@\tOpfor\n";
            var c = WarfareSave.Deserialize(legacy);
            Assert.Equal(7, c.Turn);
            Assert.NotNull(c.War);
            Assert.False(c.War.IsOver);
        }
    }
}
