using System.IO;
using Nucleus.Core.Command;

namespace Nucleus.Core.Persistence
{
    /// <summary>
    /// Disk seam for campaign save/resume: writes/reads a <see cref="CommanderState"/> to a file via
    /// <see cref="CampaignSave"/>. Pure framework IO (System.IO only — no Unity/game refs, so it stays inside
    /// the architecture rules). Writes are crash-safe: the text is written to a sibling temp file and then
    /// ATOMICALLY swapped into place via File.Replace (atomic on NTFS), so neither a partial write nor a crash
    /// mid-swap can corrupt or lose an existing save.
    /// </summary>
    public static class CampaignStore
    {
        public static void Save(string path, CommanderState state)
        {
            var text = CampaignSave.Serialize(CampaignState.Capture(state));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            // Atomic swap: File.Replace is atomic on NTFS and never leaves a no-file window (Delete-then-Move
            // could lose the prior good save if it crashed between the two). Move covers the first-time case.
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        /// <summary>Load a saved campaign, or null if the file does not exist.</summary>
        public static CommanderState Load(string path)
        {
            if (!File.Exists(path)) return null;
            return CampaignState.Restore(CampaignSave.Deserialize(File.ReadAllText(path)));
        }

        /// <summary>True (with the restored state) when a save exists and loaded; false otherwise.</summary>
        public static bool TryLoad(string path, out CommanderState state)
        {
            state = Load(path);
            return state != null;
        }
    }
}
