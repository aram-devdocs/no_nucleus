using System.Collections.Generic;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>
    /// The shared live campaign the host owns and every mod renders a slice of: Commander (objectives +
    /// toggles), Build (buy menu), Squad (squads), Warfare (operations + feed + save/resume). Pure read models +
    /// commands (no Unity, no game refs) so it lives in the Campaign lib and crosses the host/mod boundary
    /// through <c>IModContext.Campaign</c>. Implemented by the Commander runtime's service and published once
    /// to the host; the other mods consume it. The mod is always on; two toggles replace the old mode ladder.
    /// </summary>
    public interface ICampaign
    {
        // ---- read models ----
        IReadOnlyList<UnitView> LastRoster { get; }
        HqSnapshot Hq();
        ConvoyCatalog Catalog();
        float Funds();

        // ---- the two command toggles ----
        bool AiCreatesObjectives { get; }
        bool AiAutoFill { get; }
        void SetAiCreatesObjectives(bool on);
        void SetAiAutoFill(bool on);

        // ---- objectives (the single command primitive) ----
        /// <summary>Drop a player objective at a world point (the AI auto-fills squads if Auto-fill is on).</summary>
        string CreateObjective(ObjectiveKind kind, Vec3 world, string? targetId = null);
        void EditObjective(string id, ObjectiveKind? kind = null, float? priority = null);
        void RemoveObjective(string id);
        void MoveObjective(string id, Vec3 world);
        /// <summary>Assign a squad to an objective (the human path when Auto-fill is off): opens/extends its operation.</summary>
        void AssignSquad(string objectiveId, string squadId);

        // ---- commands ----
        void ToggleSquadManual(string squadId);
        void ToggleOperationManual(string operationId);
        void BuyConvoy(string name);
        void SaveCampaign(string path);
        bool LoadCampaign(string path);
    }
}
