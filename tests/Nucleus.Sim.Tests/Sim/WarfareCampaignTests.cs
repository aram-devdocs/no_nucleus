using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Nucleus.Core.Persistence;
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
    }
}
