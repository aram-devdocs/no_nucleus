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

    /// <summary>The attrition board as fractions (0..1 of the starting pool) + a worded status + the rules line
    /// that explains how the score is won/lost.</summary>
    public readonly struct ScoreboardVm
    {
        public readonly string BluforLine, OpforLine, Status, Rules;
        public readonly float BluforFraction, OpforFraction;
        public readonly UiColor StatusColor;

        public ScoreboardVm(string bluforLine, float bluforFraction, string opforLine, float opforFraction,
            string status, UiColor statusColor, string rules)
        {
            BluforLine = bluforLine; BluforFraction = bluforFraction;
            OpforLine = opforLine; OpforFraction = opforFraction;
            Status = status; StatusColor = statusColor; Rules = rules;
        }
    }

    /// <summary>One row of the command-center order tree: a parent order (indent 0) or a child node (indent 1),
    /// pre-resolved to label + colors + an owner badge, with selection + reachability flags.</summary>
    public readonly struct OrderRowVm
    {
        public readonly string Id;            // objective id for a node; order id for a parent
        public readonly bool IsParent;
        public readonly int Indent;
        public readonly string Label;
        public readonly UiColor LabelColor;
        public readonly ObjectiveKind Kind;
        public readonly bool ShowKindDot;
        public readonly string Badge;         // "AI" / "YOU"
        public readonly UiColor BadgeColor;
        public readonly bool Selected;
        public readonly bool Unreachable;

        public OrderRowVm(string id, bool isParent, int indent, string label, UiColor labelColor,
            ObjectiveKind kind, bool showKindDot, string badge, UiColor badgeColor, bool selected, bool unreachable)
        {
            Id = id; IsParent = isParent; Indent = indent; Label = label; LabelColor = labelColor;
            Kind = kind; ShowKindDot = showKindDot; Badge = badge; BadgeColor = badgeColor;
            Selected = selected; Unreachable = unreachable;
        }
    }

    /// <summary>The selection-detail pane for the picked order node: status, force, live phase, and the primary
    /// take-over/release action. <see cref="HasSelection"/> is false when nothing is picked.</summary>
    public readonly struct NodeDetailVm
    {
        public readonly bool HasSelection;
        public readonly string Title;
        public readonly UiColor TitleColor;
        public readonly ObjectiveKind Kind;
        public readonly string Status;        // "Active · SEAD" / "Blocked · waiting on prerequisites" / "Complete"
        public readonly string Force;         // "2 squads" / "no force yet"
        public readonly string Action;        // "Take Over" / "Release"
        public readonly UiColor ActionColor;
        public readonly bool Unreachable;

        public NodeDetailVm(bool hasSelection, string title, UiColor titleColor, ObjectiveKind kind, string status,
            string force, string action, UiColor actionColor, bool unreachable)
        {
            HasSelection = hasSelection; Title = title; TitleColor = titleColor; Kind = kind; Status = status;
            Force = force; Action = action; ActionColor = actionColor; Unreachable = unreachable;
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
