using CommanderLayer.Core.Model;
using UnityEngine;

namespace CommanderLayer.Ui
{
    /// <summary>Distinct, order-type-coded colors for the map overlay + panel rows. These are
    /// MOD-OWNED — the game has no per-order-kind palette, so they are deliberately the mod's own values
    /// (not copies of any game color). Native game colors live in <see cref="NativeColors"/>.</summary>
    public static class OrderColors
    {
        public static readonly Color Attack = new Color(1f, 0.70f, 0.20f);   // amber
        public static readonly Color Defend = new Color(0.30f, 0.80f, 1f);   // cyan
        public static readonly Color Resupply = new Color(0.40f, 0.95f, 0.55f); // green
        public static readonly Color Build = new Color(0.72f, 0.52f, 1f);    // violet
        public static readonly Color Capture = new Color(1f, 0.55f, 0.30f);  // orange
        public static readonly Color Move = new Color(0.80f, 0.84f, 0.90f);  // pale steel

        public static Color For(OrderKind k)
        {
            switch (k)
            {
                case OrderKind.Attack: return Attack;
                case OrderKind.Defend: return Defend;
                case OrderKind.Resupply: return Resupply;
                case OrderKind.Build: return Build;
                case OrderKind.Capture: return Capture;
                case OrderKind.Move: return Move;
                default: return Attack;
            }
        }
    }
}
