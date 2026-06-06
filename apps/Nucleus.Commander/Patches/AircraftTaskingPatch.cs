using CommanderLayer.Game;
using CommanderLayer.Game.Generated;
using HarmonyLib;

namespace CommanderLayer.Patches
{
    /// <summary>
    /// Steers IDLE player aircraft toward commander air-intent zones by overriding the pilot's no-target
    /// destination. Aircraft-only by construction — it patches the aircraft pilot's own combat state, so
    /// idle ground/ships are untouched (no faction Objective, no stampede). When the aircraft detects an
    /// enemy it leaves the no-target state and the game's normal target selection takes over. Inert unless
    /// AircraftIntent.Enabled (the EXPERIMENTAL "EnableAircraftTasking" config); needs in-game tuning.
    /// </summary>
    [HarmonyPatch(typeof(AIPilotCombatModes), "NoTarget")]
    internal static class AircraftTaskingPatch
    {
        [HarmonyPostfix]
        private static void Postfix(AIPilotCombatModes __instance)
        {
            if (!AircraftIntent.Enabled) return;

            var ac = GameSdk.PilotBaseState_aircraft(__instance);
            if (ac == null) return;
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return;
            if (!ReferenceEquals(ac.NetworkHQ, hq)) return;   // only the player's faction aircraft

            var pos = GameConvert.ToVec3(ac.GlobalPosition());
            if (!AircraftIntent.TryNearest(pos, out var center)) return;

            GameSdk.PilotBaseState_destination_Set(__instance, GameConvert.ToGlobal(center));
        }
    }
}
