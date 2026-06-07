using System;
using System.Globalization;
using System.IO;
using System.Text;
using Nucleus.Core.Command;
using Nucleus.Core.War;

namespace Nucleus.Core.Persistence
{
    /// <summary>
    /// Save/resume for a whole two-faction <see cref="WarfareCampaign"/> in one file: a small header (version
    /// + turn + roster baselines), the attrition <see cref="WarState"/>, then each faction's
    /// <see cref="CampaignSave"/> text, separated by marker lines the per-faction format never emits.
    /// Dependency-free and deterministic, like <see cref="CampaignSave"/>. v2 adds the war scoreboard; v1 files
    /// still load (war defaults).
    /// </summary>
    public static class WarfareSave
    {
        private const string Header = "NUCLEUS-WARFARE";
        private const int Version = 2;
        private const string Marker = "@@NUCLEUS-FACTION@@";
        private const string WarMarker = "@@NUCLEUS-WAR@@";

        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        // Strip the field delimiters so a faction name with a tab/newline can't shift or truncate the record.
        private static string Clean(string s) =>
            string.IsNullOrEmpty(s) ? "Faction" : s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

        // One WarSide as a tab record: name, commander, funds, score, basesLost, unitsLost, totalSpent.
        private static string Side(WarSide s) => string.Join("\t",
            Clean(s.FactionName), (int)s.Commander, F(s.Funds),
            F(s.Score.Score), s.Score.BasesLost.ToString(CultureInfo.InvariantCulture),
            s.Score.UnitsLost.ToString(CultureInfo.InvariantCulture), F(s.Score.TotalSpent));

        public static string Serialize(WarfareCampaign c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            var sb = new StringBuilder();
            sb.Append(Header).Append('\t').Append(Version).Append('\t').Append(c.Turn)
              .Append('\t').Append(c.BluforRosterBaseline).Append('\t').Append(c.OpforRosterBaseline).Append('\n');
            sb.Append(WarMarker).Append('\t').Append(Side(c.War.Blufor)).Append('\n');
            sb.Append(WarMarker).Append('\t').Append(Side(c.War.Opfor)).Append('\n');
            sb.Append(Marker).Append('\t').Append("Blufor").Append('\n');
            sb.Append(CampaignSave.Serialize(CampaignState.Capture(c.Blufor)));
            sb.Append(Marker).Append('\t').Append("Opfor").Append('\n');
            sb.Append(CampaignSave.Serialize(CampaignState.Capture(c.Opfor)));
            return sb.ToString();
        }

        public static WarfareCampaign Deserialize(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            int turn = 0, bluBaseline = -1, opBaseline = -1;
            string blu = "", op = "";
            WarSide warBlu = null, warOp = null;
            int section = 0; // 0 = header, 1 = blufor, 2 = opfor
            var buf = new StringBuilder();

            foreach (var rawLine in text.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith(Header + "\t", StringComparison.Ordinal))
                {
                    var f = line.Split('\t');
                    if (f.Length > 2) int.TryParse(f[2], out turn);
                    if (f.Length > 3) int.TryParse(f[3], out bluBaseline);
                    if (f.Length > 4) int.TryParse(f[4], out opBaseline);
                    continue;
                }
                if (line.StartsWith(WarMarker, StringComparison.Ordinal))
                {
                    var side = ParseSide(line);
                    if (warBlu == null) warBlu = side; else warOp = side;
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
            var war = warBlu != null && warOp != null ? new WarState(warBlu, warOp) : new WarState();
            return new WarfareCampaign(blufor, opfor, war)
            {
                Turn = turn,
                BluforRosterBaseline = bluBaseline,
                OpforRosterBaseline = opBaseline,
            };
        }

        // Parse a "@@NUCLEUS-WAR@@\tname\tcommander\tfunds\tscore\tbasesLost\tunitsLost\ttotalSpent" line.
        private static WarSide ParseSide(string line)
        {
            var f = line.Split('\t');
            // f[0] = marker; f[1..] = the Side(...) record.
            string name = f.Length > 1 ? f[1] : "Faction";
            var kind = f.Length > 2 && int.TryParse(f[2], out var k) ? (CommanderKind)k : CommanderKind.Ai;
            float funds = f.Length > 3 ? P(f[3]) : 0f;
            var side = new WarSide(name, kind, funds);
            float score = f.Length > 4 ? P(f[4]) : 1000f;
            int basesLost = f.Length > 5 && int.TryParse(f[5], out var bl) ? bl : 0;
            int unitsLost = f.Length > 6 && int.TryParse(f[6], out var ul) ? ul : 0;
            float totalSpent = f.Length > 7 ? P(f[7]) : 0f;
            side.Score.Restore(score, basesLost, unitsLost, totalSpent);
            return side;
        }

        private static float P(string s) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

        public static void Save(string path, WarfareCampaign c)
        {
            var text = Serialize(c);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            // Atomic swap (see CampaignStore.Save): File.Replace avoids the no-file window of Delete-then-Move.
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        /// <summary>Load a saved war, or null if the file does not exist.</summary>
        public static WarfareCampaign Load(string path)
            => File.Exists(path) ? Deserialize(File.ReadAllText(path)) : null;
    }
}
