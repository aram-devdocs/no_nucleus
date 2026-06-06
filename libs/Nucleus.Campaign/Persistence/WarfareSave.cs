using System;
using System.IO;
using System.Text;
using Nucleus.Core.Command;

namespace Nucleus.Core.Persistence
{
    /// <summary>
    /// Save/resume for a whole two-faction <see cref="WarfareCampaign"/> in one file: a small header (version
    /// + turn) followed by each faction's <see cref="CampaignSave"/> text, separated by a marker line that the
    /// per-faction format never emits. Dependency-free and deterministic, like <see cref="CampaignSave"/>.
    /// </summary>
    public static class WarfareSave
    {
        private const string Header = "NUCLEUS-WARFARE";
        private const int Version = 1;
        private const string Marker = "@@NUCLEUS-FACTION@@";

        public static string Serialize(WarfareCampaign c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var sb = new StringBuilder();
            sb.Append(Header).Append('\t').Append(Version).Append('\t').Append(c.Turn).Append('\n');
            sb.Append(Marker).Append('\t').Append("Blufor").Append('\n');
            sb.Append(CampaignSave.Serialize(CampaignState.Capture(c.Blufor)));
            sb.Append(Marker).Append('\t').Append("Opfor").Append('\n');
            sb.Append(CampaignSave.Serialize(CampaignState.Capture(c.Opfor)));
            return sb.ToString();
        }

        public static WarfareCampaign Deserialize(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            int turn = 0;
            string blu = "", op = "";
            int section = 0; // 0 = header, 1 = blufor, 2 = opfor
            var buf = new StringBuilder();

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith(Header + "\t", StringComparison.Ordinal))
                {
                    var f = line.Split('\t');
                    if (f.Length > 2) int.TryParse(f[2], out turn);
                    continue;
                }
                if (line.StartsWith(Marker, StringComparison.Ordinal))
                {
                    if (section == 1) blu = buf.ToString();
                    else if (section == 2) op = buf.ToString();
                    buf.Clear();
                    var f = line.Split('\t');
                    section = f.Length > 1 && f[1] == "Opfor" ? 2 : 1;
                    continue;
                }
                if (section >= 1 && line.Length > 0) buf.Append(line).Append('\n');
            }
            if (section == 1) blu = buf.ToString();
            else if (section == 2) op = buf.ToString();

            var blufor = CampaignState.Restore(CampaignSave.Deserialize(blu));
            var opfor = CampaignState.Restore(CampaignSave.Deserialize(op));
            return new WarfareCampaign(blufor, opfor) { Turn = turn };
        }

        public static void Save(string path, WarfareCampaign c)
        {
            var text = Serialize(c);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        /// <summary>Load a saved war, or null if the file does not exist.</summary>
        public static WarfareCampaign Load(string path)
            => File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null;
    }
}
