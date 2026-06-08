namespace Nucleus.Ui
{
    /// <summary>One place for the panel's static user-facing copy (section headers, hints, empty states), so the
    /// wording lives in a single, scannable, localizable file instead of inline across the layout code. Dynamic
    /// per-row text is built by the renderers from the read-models.</summary>
    public static class UiStrings
    {
        // Section headers
        public const string ObjectivesHeader = "OBJECTIVES — drop on the map";
        public const string CommanderHeader = "COMMANDER";
        public const string OperationsHeader = "OPERATIONS";
        public const string SquadsHeader = "SQUADS";
        public const string BuildHeader = "BUILD — reinforce";
        public const string AttritionHeader = "ATTRITION";
        public const string FeedHeader = "FEED";
        public const string OrdersHeader = "ORDERS";

        // Hints
        public const string ObjectivesHint = "Pick a kind, then click the map to drop an objective.";
        public const string ObjectivesHintArmedPrompt = "Pick a kind, then click the map. Click a marker to select & edit it.";
        public const string OrdersTreeHint = "Parent = the goal; indented = its prerequisites. Tap a row to select; take it over to drive it yourself.";
        public const string ModeHint = "AI COMMANDER: AI creates objectives. AI AUTO-FILL: AI forms & assigns squads.";
        public const string OperationsHint = "Each operation runs on AI or YOU (manual). Tap AI/YOU to switch.";
        public const string SquadsHint = "Each squad runs on AI or is YOURS (manual). Tap AI/YOU to switch.";
        public const string BuildAircraftNote = "AIRCRAFT — spawn from your airbases (not bought here).";
        public const string BuildHint = "Spend faction funds on reinforcement convoys (they arrive off-map and drive to the front). Aircraft are flown from your airbases via the game's spawn menu. Every purchase also costs attrition — more so once your bases are lost.";
        public const string AttritionHint = "Drive the enemy's score to zero. It falls as a side loses units and bases — and as it spends on reinforcement (faster once bases are lost).";

        // Empty / placeholder states
        public const string FundsPlaceholder = "Funds: —";
        public const string OpsEmpty = "No operations running. Drop an objective on the map (or enable AI COMMANDER) and the squads will form up and fight.";
        public const string SquadsEmpty = "No squads yet. Squads form automatically from your forces as the war starts.";
        public const string BuildEmpty = "No convoys offered for this faction/map. Aircraft still spawn from your airbases.";
        public const string NoOrders = "No orders yet. Pick a convoy above to reinforce.";
        public const string NoOrdersInProgress = "No orders in progress. Pick a convoy above to reinforce (it arrives off-map and drives in).";
        public const string NoObjectiveSelected = "Select an objective to edit.";
        public const string OrdersEmpty = "No orders yet. Drop an objective on the map (or enable AI COMMANDER) and orders will form.";
        public const string NoNodeSelected = "Select an order row to see its status and take it over.";
        public const string SetupHeader = "Pick your faction. Other sides are AI-controlled.";

        // War status
        public const string WarInProgress = "War in progress — drive a faction to zero to win.";
        public const string WarOverDraw = "WAR OVER — DRAW";

        // In-flight banner shown once while the AI commands your side (dismissible).
        public const string AiCommandingBanner = "AI is commanding your side — fly freely, or Take Over any node";
        public const string AiCommandingBannerDismiss = "   (click to dismiss)";
    }
}
