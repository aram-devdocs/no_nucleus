namespace Nucleus.Ui
{
    /// <summary>
    /// One source of truth for UI sizing/spacing so the panels are even and consistent instead of using ad-hoc
    /// magic numbers scattered across the layout code. The atomic kit (ButtonFixed, rows, sections) sizes off
    /// these; tuning the look is a one-place change. Pure constants — no Unity dependency.
    /// </summary>
    public static class UiTokens
    {
        // Heights
        public const float ButtonHeight = 26f;        // every standalone button — consistent, never jitters
        public const float RowHeight = 20f;           // list/entity rows
        public const float SectionHeaderHeight = 20f; // section headers
        public const float DividerHeight = 2f;        // section rule

        // Spacing / padding
        public const float SpacingTight = 3f;
        public const float SpacingNormal = 6f;
        public const float PadEdge = 10f;             // panel content inset

        // Common button widths (row action buttons)
        public const float ActionButtonWidth = 64f;   // SELECT / ASSIGN / BUY / AI-YOU
        public const float SmallButtonWidth = 24f;    // X / icon buttons

        // Font sizes
        public const float FontTitle = 18f;
        public const float FontHeader = 13f;
        public const float FontBody = 12f;
        public const float FontHint = 11f;

        // Panel footprints (fixed so they never reflow as content changes)
        public const float ModPanelWidth = 460f;
        public const float ModPanelHeight = 880f;
        public const float SetupPanelWidth = 480f;
        public const float SetupPanelHeight = 520f;
        public const float HudWidth = 360f;
        public const float HudHeight = 168f;
        public const float BadgeWidth = 330f;
        public const float BadgeHeight = 30f;

        // Map overlay sizes
        public const float MarkerSize = 16f;          // objective marker with the native sprite
        public const float MarkerSizeFallback = 11f;  // procedural marker when no sprite captured
        public const float SelectionRingSize = 42f;   // ring around the selected objective
        public const float MapLabelOffsetX = 10f;     // label gap from its marker
        public const float RingThickness = 3f;        // range-ring band (px in the 128px sprite)
        public const float DashedRingThickness = 4f;
        public const int RingDashes = 32;
        public const float LineOpacity = 0.6f;        // squad status lines — translucent so they don't block the map
    }
}
