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
    /// The headless proof for save/resume: run the real brain for N ticks, save the campaign to a
    /// string, continue the original for M more ticks, then restore from the saved string and continue the
    /// SAME M ticks — and assert the two traces are byte-for-byte identical. Proves resuming a saved campaign
    /// changes nothing about how the war unfolds, which is the core guarantee of a persistent dynamic war.
    /// </summary>
    [Trait("Category", "Sim")]
    public class PersistenceContinuationTests
    {
        // A deterministic, brain-INDEPENDENT battlefield script: enemies drift on a seeded walk, the friendly
        // roster holds position. Both the original and restored runs consume the identical snapshots, so any
        // divergence is purely a persistence defect.
        private static List<WorldSnapshot> Script(int ticks, ulong seed)
        {
            var (friendly, enemy) = Scenarios.CombinedArms();
            var rng = new Pcg(seed);
            var snaps = new List<WorldSnapshot>(ticks);
            for (int t = 0; t < ticks; t++)
            {
                foreach (var e in enemy) { e.X += rng.Range(-30f, 30f); e.Z += rng.Range(-30f, 30f); }
                var roster = friendly.Select(u => u.ToUnitView()).ToList();
                var known = enemy.Select(e => e.ToEnemyView(accurate: true)).ToList();
                snaps.Add(new WorldSnapshot(roster, known, 5000f, null, t));
            }
            return snaps;
        }

        private static string Trace(WorldSnapshot snap, CommanderState state)
        {
            var tasks = CommanderBrain.Tick(snap, state);
            var sb = new StringBuilder();
            foreach (var t in tasks.OrderBy(x => x.UnitId))
                sb.Append(t.UnitId).Append('>').Append(t.Verb).Append('@')
                  .Append(t.Position.X.ToString("0")).Append(',').Append(t.Position.Z.ToString("0")).Append(';');
            sb.Append("|obj=").Append(state.Objectives.Count);
            sb.Append("|ops=");
            foreach (var op in state.Operations.OrderBy(o => o.Id))
                sb.Append(op.Id).Append(':').Append(op.Status).Append(':').Append(op.CombatPhase).Append(',');
            sb.Append("|sq=").Append(state.Squads.Squads.Count);
            return sb.ToString();
        }

        [Fact]
        public void Resumed_campaign_continues_identically_to_the_unsaved_original()
        {
            const int warmup = 40, tail = 60;
            var snaps = Script(warmup + tail, seed: 0xC0FFEE);

            var state = new CommanderState();
            for (int t = 0; t < warmup; t++) CommanderBrain.Tick(snaps[t], state);

            // Save NOW (string is an independent copy) before mutating the original further.
            var saved = CampaignSave.Serialize(CampaignState.Capture(state));

            // Branch A: the unsaved original continues.
            var traceOriginal = new StringBuilder();
            for (int t = warmup; t < warmup + tail; t++) traceOriginal.AppendLine(Trace(snaps[t], state));

            // Branch B: restore from the saved string and continue the same ticks.
            var restored = CampaignState.Restore(CampaignSave.Deserialize(saved));
            var traceRestored = new StringBuilder();
            for (int t = warmup; t < warmup + tail; t++) traceRestored.AppendLine(Trace(snaps[t], restored));

            Assert.Equal(traceOriginal.ToString(), traceRestored.ToString());
        }

        [Fact]
        public void Warmed_campaign_is_non_trivial_so_the_proof_has_teeth()
        {
            const int warmup = 40;
            var snaps = Script(warmup, seed: 0xC0FFEE);
            var state = new CommanderState();
            for (int t = 0; t < warmup; t++) CommanderBrain.Tick(snaps[t], state);

            // The warmed campaign must actually hold state worth persisting (otherwise the continuation test
            // would pass vacuously on an empty campaign).
            Assert.NotEmpty(state.Objectives);
            Assert.NotEmpty(state.Squads.Squads);
            var snap = CampaignState.Capture(state);
            Assert.NotEmpty(snap.Objectives);
        }
    }
}
