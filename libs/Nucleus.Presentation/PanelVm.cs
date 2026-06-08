using System.Collections.Generic;
using Nucleus.Core.Command;

namespace Nucleus.Presentation
{
    /// <summary>A semantic color role the UI resolves to a concrete palette color — so display logic decides
    /// MEANING (active / warn / kind-colored) here, and the Ui layer owns the actual RGB.</summary>
    public enum UiColor
    {
        Text, Muted, Active, Idle, Accent, Danger, Warn, Kind,
    }

    /// <summary>One render-ready list row: a label + an optional action button, both pre-resolved to text and a
    /// color role. <see cref="Kind"/> carries the objective kind for kind-colored elements (label color + dot).</summary>
    public readonly struct RowVm
    {
        public readonly string Id;
        public readonly string Label;
        public readonly UiColor LabelColor;
        public readonly ObjectiveKind Kind;
        public readonly bool ShowKindDot;
        public readonly string Button;
        public readonly UiColor ButtonColor;
        public readonly bool ButtonEnabled;

        public RowVm(string id, string label, UiColor labelColor, string button, UiColor buttonColor,
            bool buttonEnabled = true, ObjectiveKind kind = default, bool showKindDot = false)
        {
            Id = id; Label = label; LabelColor = labelColor;
            Button = button; ButtonColor = buttonColor; ButtonEnabled = buttonEnabled;
            Kind = kind; ShowKindDot = showKindDot;
        }
    }

    /// <summary>The attrition board as fractions (0..1 of the starting pool) + a worded status.</summary>
    public readonly struct ScoreboardVm
    {
        public readonly string BluforLine, OpforLine, Status;
        public readonly float BluforFraction, OpforFraction;
        public readonly UiColor StatusColor;

        public ScoreboardVm(string bluforLine, float bluforFraction, string opforLine, float opforFraction,
            string status, UiColor statusColor)
        {
            BluforLine = bluforLine; BluforFraction = bluforFraction;
            OpforLine = opforLine; OpforFraction = opforFraction;
            Status = status; StatusColor = statusColor;
        }
    }

    /// <summary>What the player's current selection/arming is — supplied by the Ui (UI-local interaction state)
    /// so the builder can mark the selected objective and prompt the armed drop.</summary>
    public readonly struct PanelInteraction
    {
        public readonly ObjectiveKind? ArmedKind;
        public readonly string SelectedObjectiveId;
        public PanelInteraction(ObjectiveKind? armedKind, string selectedObjectiveId)
        { ArmedKind = armedKind; SelectedObjectiveId = selectedObjectiveId; }
    }

    /// <summary>The whole panel as render-ready data: the Ui maps each row/field to widgets and resolves
    /// <see cref="UiColor"/> to the theme. No display logic remains in the Unity layer.</summary>
    public sealed class PanelVm
    {
        public IReadOnlyList<RowVm> ObjectiveRows { get; }
        /// <summary>Editor line for the selected objective, or null when nothing is selected.</summary>
        public string ObjectiveEditor { get; }
        public string AssignHeader { get; }
        public IReadOnlyList<RowVm> AssignRows { get; }
        public IReadOnlyList<RowVm> OperationRows { get; }
        public IReadOnlyList<RowVm> SquadRows { get; }
        public IReadOnlyList<RowVm> BuildRows { get; }
        public string BuildFunds { get; }
        public UiColor BuildFundsColor { get; }
        public string BuildStatus { get; }
        public string Feed { get; }
        public bool AiCommanderOn { get; }
        public bool AiAutoFillOn { get; }
        public bool OpsEmpty { get; }
        public bool SquadsEmpty { get; }
        public bool BuildEmpty { get; }

        public PanelVm(IReadOnlyList<RowVm> objectiveRows, string objectiveEditor, string assignHeader,
            IReadOnlyList<RowVm> assignRows, IReadOnlyList<RowVm> operationRows, IReadOnlyList<RowVm> squadRows,
            IReadOnlyList<RowVm> buildRows, string buildFunds, UiColor buildFundsColor, string buildStatus,
            string feed, bool aiCommanderOn, bool aiAutoFillOn, bool opsEmpty, bool squadsEmpty, bool buildEmpty)
        {
            ObjectiveRows = objectiveRows; ObjectiveEditor = objectiveEditor;
            AssignHeader = assignHeader; AssignRows = assignRows;
            OperationRows = operationRows; SquadRows = squadRows; BuildRows = buildRows;
            BuildFunds = buildFunds; BuildFundsColor = buildFundsColor; BuildStatus = buildStatus;
            Feed = feed; AiCommanderOn = aiCommanderOn; AiAutoFillOn = aiAutoFillOn;
            OpsEmpty = opsEmpty; SquadsEmpty = squadsEmpty; BuildEmpty = buildEmpty;
        }
    }
}
