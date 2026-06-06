using System.IO;
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
        private static CommanderState Sample(string objId, AutonomyLevel autonomy)
        {
            var state = new CommanderState { Autonomy = autonomy, HomeBase = new Vec3(5f, 0f, 7f) };
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
                CampaignStore.Save(path, Sample("obj-x", AutonomyLevel.Assisted));
                Assert.True(File.Exists(path));

                var loaded = CampaignStore.Load(path);
                Assert.NotNull(loaded);
                Assert.Equal(AutonomyLevel.Assisted, loaded.Autonomy);
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
                CampaignStore.Save(path, Sample("obj-y", AutonomyLevel.Auto));
                Assert.True(File.Exists(path));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void Overwriting_an_existing_save_keeps_the_latest()
        {
            var path = TempPath("ow-" + System.Guid.NewGuid().ToString("N") + ".ncs");
            try
            {
                CampaignStore.Save(path, Sample("first", AutonomyLevel.Auto));
                CampaignStore.Save(path, Sample("second", AutonomyLevel.Manual));
                var loaded = CampaignStore.Load(path);
                Assert.Equal("second", loaded.Objectives[0].Id);
                Assert.Equal(AutonomyLevel.Manual, loaded.Autonomy);
                Assert.False(File.Exists(path + ".tmp")); // temp swapped away, no leftover
            }
            finally { File.Delete(path); }
        }
    }
}
