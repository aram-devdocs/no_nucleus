using System.IO;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;
using Nucleus.Core.Persistence;
using Xunit;

namespace Nucleus.Core.Tests
{
    /// <summary>
    /// Disk-seam tests for campaign save/resume: a save round-trips through a real file, a missing file loads
    /// as null, missing directories are created, and overwriting an existing save is crash-safe (the temp
    /// swap never leaves a partial file).
    /// </summary>
    public class CampaignStoreTests
    {
        private static CommanderState Sample(string objId, bool aiAutoFill)
        {
            var state = new CommanderState { AiAutoFill = aiAutoFill, HomeBase = new Vec3(5f, 0f, 7f) };
            state.Objectives.Add(new Objective(objId, ObjectiveKind.DefendArea, new Vec3(1f, 0f, 2f),
                ObjectiveSource.Player, priority: 2f));
            var squad = new Squad("sq", "Defenders", RoleFamily.AirDefense, SquadOrigin.Player, new[] { "u1" });
            state.Squads.Add(squad);
            return state;
        }

        private static string TempPath(string name)
            => Path.Combine(Path.GetTempPath(), "nucleus-save-tests", name);

        [Fact]
        public void Save_then_load_round_trips_through_a_file()
        {
            var path = TempPath("rt-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            try
            {
                CampaignStore.Save(path, Sample("obj-x", false));
                Assert.True(File.Exists(path));

                var loaded = CampaignStore.Load(path);
                Assert.NotNull(loaded);
                Assert.False(loaded.AiAutoFill);
                Assert.Single(loaded.Objectives);
                Assert.Equal("obj-x", loaded.Objectives[0].Id);
                Assert.Single(loaded.Squads.Squads);
                Assert.Equal("Defenders", loaded.Squads.Squads[0].Name);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Load_of_a_missing_file_is_null()
        {
            var path = TempPath("does-not-exist-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            Assert.Null(CampaignStore.Load(path));
            Assert.False(CampaignStore.TryLoad(path, out var s));
            Assert.Null(s);
        }

        [Fact]
        public void Save_creates_missing_directories()
        {
            var dir = TempPath("nested-" + System.Guid.NewGuid().ToString("N"));
            var path = Path.Combine(dir, "deep", "campaign.ncs");
            try
            {
                CampaignStore.Save(path, Sample("obj-y", true));
                Assert.True(File.Exists(path));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void Save_writes_hashset_and_dict_collections_in_ordinal_order()
        {
            // ConfirmedObjectives (HashSet) + LastObjectiveByUnit (Dictionary) enumerate in
            // process-randomized order — they must be sorted on write so the save is byte-identical across runs.
            var path = TempPath("sort-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            try
            {
                var state = new CommanderState();
                foreach (var id in new[] { "zulu", "alpha", "mike" }) state.ConfirmedObjectives.Add(id);
                state.LastObjectiveByUnit["u-z"] = "1";
                state.LastObjectiveByUnit["u-a"] = "2";
                state.LastObjectiveByUnit["u-m"] = "3";
                CampaignStore.Save(path, state);

                var lines = File.ReadAllLines(path);
                Assert.Equal(new[] { "alpha", "mike", "zulu" },
                    lines.Where(l => l.StartsWith("CONFIRMED")).Select(l => l.Split('\t')[1]));
                Assert.Equal(new[] { "u-a", "u-m", "u-z" },
                    lines.Where(l => l.StartsWith("LASTOBJ")).Select(l => l.Split('\t')[1]));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Save_load_resave_is_byte_identical()
        {
            // The byte-identity invariant: a saved campaign re-saved after a load must be textually identical.
            var p1 = TempPath("bi1-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            var p2 = TempPath("bi2-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            try
            {
                var state = new CommanderState();
                foreach (var id in new[] { "obj-3", "obj-1", "obj-2" }) state.ConfirmedObjectives.Add(id);
                state.LastObjectiveByUnit["u3"] = "obj-3";
                state.LastObjectiveByUnit["u1"] = "obj-1";
                CampaignStore.Save(p1, state);
                CampaignStore.Save(p2, CampaignStore.Load(p1));
                Assert.Equal(File.ReadAllText(p1), File.ReadAllText(p2));
            }
            finally { File.Delete(p1); File.Delete(p2); }
        }

        [Fact]
        public void Load_tolerates_truncated_known_records_without_throwing()
        {
            // Forward-compat contract: a truncated / older known record (missing trailing columns) must be
            // skipped, not abort the whole load with IndexOutOfRange.
            var path = TempPath("trunc-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            try
            {
                CampaignStore.Save(path, Sample("obj-z", true));
                // Append deliberately short known records (each missing required trailing columns).
                File.AppendAllText(path, "\nOBJ\nSQUAD\tonly-id\nLASTOBJ\tu1\nOPTHREAT\top\nSQUADCOMP\nOPSQUAD\top\nOP\n");
                var loaded = CampaignStore.Load(path);   // before the fix this threw IndexOutOfRangeException
                Assert.NotNull(loaded);
                Assert.Equal("obj-z", loaded.Objectives[0].Id);   // the valid record still round-tripped
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void Overwriting_an_existing_save_keeps_the_latest()
        {
            var path = TempPath("ow-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            try
            {
                CampaignStore.Save(path, Sample("first", true));
                CampaignStore.Save(path, Sample("second", false));
                var loaded = CampaignStore.Load(path);
                Assert.Equal("second", loaded.Objectives[0].Id);
                Assert.False(loaded.AiAutoFill);
                Assert.False(File.Exists(path + ".tmp")); // temp swapped away, no leftover
            }
            finally { File.Delete(path); }
        }
    }
}
