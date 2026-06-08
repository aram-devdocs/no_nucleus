using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nucleus.Core.Command;

namespace Nucleus.Presentation
{
    /// <summary>Turns the engine-free <see cref="HqSnapshot"/> + UI interaction state into render-ready
    /// <see cref="PanelVm"/> rows (text + semantic color). This is where the panel's display logic lives now —
    /// pure and unit-testable; the Ui layer only maps rows to widgets and resolves <see cref="UiColor"/>.</summary>
    public static class PresentationBuilder
    {
        public const int MaxObjectives = 8, MaxOps = 5, MaxSquads = 6, MaxBuild = 6, MaxAssign = 4;

        public static PanelVm Build(HqSnapshot hq, PanelInteraction interaction, ConvoyCatalog catalog, float funds)
        {
            var ops = hq?.Operations;
            string sel = interaction.SelectedObjectiveId;

            // --- Objective list ---
            var objRows = new List<RowVm>();
            if (ops != null)
                foreach (var op in ops.Take(MaxObjectives))
                {
                    bool s = op.ObjectiveId == sel;
                    string owner = op.PlayerOwned ? "you" : "AI";
                    string label = $"{(s ? "▸ " : "")}{ObjectiveText.Name(op.Kind)} · {ObjectiveText.PhaseLabel(op.Phase)} · {op.SquadCount} sq [{owner}]";
                    objRows.Add(new RowVm(op.ObjectiveId, label, s ? UiColor.Active : UiColor.Text,
                        "SELECT", s ? UiColor.Active : UiColor.Idle, kind: op.Kind, showKindDot: true));
                }

            // --- Selected-objective editor ---
            string editor = null;
            if (sel != null)
            {
                editor = "Editing selected objective.";
                if (ops != null)
                    foreach (var o in ops)
                        if (o.ObjectiveId == sel)
                        {
                            string owner = o.PlayerOwned ? "yours" : "AI";
                            editor = $"{ObjectiveText.Name(o.Kind)} · {ObjectiveText.PhaseLabel(o.Phase)} · {o.SquadCount} squad{(o.SquadCount == 1 ? "" : "s")} · {owner} · Priority {o.Priority:0.#} (PRIO -/+)";
                            break;
                        }
            }

            // --- Assign-force list (free, suitable squads for the selected objective) ---
            string assignHeader = "";
            var assignRows = new List<RowVm>();
            ObjectiveKind? selKind = null;
            if (sel != null && ops != null)
                foreach (var o in ops) if (o.ObjectiveId == sel) { selKind = o.Kind; break; }
            if (selKind != null && hq?.Squads != null)
            {
                var suitable = Families.SuitableFor(selKind.Value);
                var candidates = hq.Squads
                    .Where(sq => string.IsNullOrEmpty(sq.AssignedOperationId) && suitable.Contains(sq.Family))
                    .ToList();
                assignHeader = candidates.Count > 0 ? "ASSIGN FORCE → selected objective" : "ASSIGN FORCE — no free suitable squads";
                foreach (var sq in candidates.Take(MaxAssign))
                {
                    string comp = !string.IsNullOrEmpty(sq.Composition) ? sq.Composition : $"{sq.Family} ×{sq.Strength}";
                    assignRows.Add(new RowVm(sq.Id, $"{sq.Name} · {comp}", UiColor.Text, "ASSIGN", UiColor.Active));
                }
            }

            // --- Operations ---
            var opRows = new List<RowVm>();
            if (ops != null)
                foreach (var op in ops.Take(MaxOps))
                {
                    bool manual = op.Autonomy == AutonomyLevel.Manual;
                    string label = $"{ObjectiveText.Name(op.Kind)} — {ObjectiveText.PhaseLabel(op.Phase)} [{OperationText.StatusLabel(op.Status)}]";
                    opRows.Add(new RowVm(op.Id, label, UiColor.Text, manual ? "YOU" : "AI",
                        manual ? UiColor.Accent : UiColor.Active, kind: op.Kind, showKindDot: true));
                }

            // --- Squads ---
            var squadRows = new List<RowVm>();
            if (hq?.Squads != null)
                foreach (var sq in hq.Squads.Take(MaxSquads))
                {
                    string comp = !string.IsNullOrEmpty(sq.Composition) ? sq.Composition : $"{sq.Family} ×{sq.Strength}";
                    string need = sq.TargetStrength > sq.Strength ? $" ({sq.Strength}/{sq.TargetStrength})" : "";
                    bool manual = sq.Autonomy == AutonomyLevel.Manual;
                    squadRows.Add(new RowVm(sq.Id, $"{sq.Name} · {comp}{need} — {sq.Activity}",
                        sq.Depleted ? UiColor.Warn : UiColor.Text, manual ? "YOU" : "AI",
                        manual ? UiColor.Accent : UiColor.Active));
                }

            // --- Build (convoys) ---
            var buildRows = new List<RowVm>();
            var opts = catalog?.Options;
            float queued = hq?.QueuedCost ?? 0f;
            if (opts != null)
                foreach (var o in opts.Take(MaxBuild))
                {
                    string contents = string.IsNullOrEmpty(o.Contents) ? "" : $" [{o.Contents}]";
                    bool afford = (funds - queued) >= o.Cost;
                    buildRows.Add(new RowVm(o.Name, $"{o.Name}{contents} · {o.Cost:0}", UiColor.Text,
                        "BUY", afford ? UiColor.Active : UiColor.Idle, buttonEnabled: afford));
                }

            // --- Build funds line + order echo ---
            float after = funds - queued;
            string buildFunds = $"Funds: {funds:0}  ·  Queued: {queued:0}  ·  After: {after:0}";
            string buildStatus = null;
            if (hq != null)
            {
                var sb = new StringBuilder();
                foreach (var line in hq.Production.Take(3)) sb.AppendLine(line);
                foreach (var e in hq.Recent)
                    if (e.Kind == ReportKind.ProductionQueued) { sb.AppendLine("· " + e.Text); break; }
                if (sb.Length > 0) buildStatus = sb.ToString().TrimEnd();
            }

            // --- Feed ---
            string feed = "";
            if (hq != null)
            {
                var sb = new StringBuilder();
                foreach (var line in hq.Production.Take(3)) sb.AppendLine(line);
                foreach (var e in hq.Recent.Take(5)) sb.AppendLine($"· {e.Text}");
                feed = sb.ToString().TrimEnd();
            }

            return new PanelVm(objRows, editor, assignHeader, assignRows, opRows, squadRows, buildRows,
                buildFunds, after < 0f ? UiColor.Warn : UiColor.Accent, buildStatus, feed,
                hq?.AiCreatesObjectives ?? true, hq?.AiAutoFill ?? true,
                (ops?.Count ?? 0) == 0, (hq?.Squads?.Count ?? 0) == 0, (opts?.Count ?? 0) == 0);
        }

        /// <summary>The attrition board: bars as a fraction of the 1000-point starting pool (score only falls),
        /// so a half-full bar means half the war's attrition budget is spent.</summary>
        public static ScoreboardVm BuildScoreboard(WarfareCampaign.Scoreboard b)
        {
            const float denom = 1000f;
            string blu = $"{b.BluforName} [{(b.BluforAi ? "AI" : "YOU")}]  {b.BluforScore:0}  ·  ${b.BluforFunds:0}  ·  -{b.BluforUnitsLost}u/-{b.BluforBasesLost}b";
            string op = $"{b.OpforName} [{(b.OpforAi ? "AI" : "YOU")}]  {b.OpforScore:0}  ·  ${b.OpforFunds:0}  ·  -{b.OpforUnitsLost}u/-{b.OpforBasesLost}b";
            string status; UiColor statusColor;
            if (b.Over) { status = b.WinnerName != null ? $"WAR OVER — {b.WinnerName} WINS" : "WAR OVER — DRAW"; statusColor = UiColor.Active; }
            else { status = "War in progress — drive a faction to zero to win."; statusColor = UiColor.Muted; }
            return new ScoreboardVm(blu, b.BluforScore / denom, op, b.OpforScore / denom, status, statusColor);
        }
    }
}
