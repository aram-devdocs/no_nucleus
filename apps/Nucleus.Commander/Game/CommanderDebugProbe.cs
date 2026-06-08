using System.Collections.Generic;
using UnityEngine;

namespace Nucleus.Game
{
    /// <summary>Optional runtime probe (behind the <c>CommanderDebug</c> config flag, default off). Logs
    /// structured "[S0:*]" lines so a playtest can resolve runtime unknowns: unit-id stability, kill/track
    /// pruning, and terrain water/land.</summary>
    public sealed class CommanderDebugProbe
    {
        private int _tick;
        private bool _terrainLogged;
        private bool _uiLogged;
        private bool _autoTerrainDone;
        private readonly Dictionary<string, int> _firstSeenTick = new Dictionary<string, int>();

        public void Tick()
        {
            // Auto terrain map: runs ONCE per mission with NO debug flag, so a normal playtest yields the
            // water/land + height ground-truth needed to place ground units + airbases correctly next pass.
            if (!_autoTerrainDone && AutoTerrainDump()) _autoTerrainDone = true;

            if (!CommanderPlugin.CommanderDebug) return;
            _tick++;
            LogRoster();   // UID stability
            LogTracking(); // KILL / prune detection
            if (!_terrainLogged) { _terrainLogged = true; LogTerrain(); } // one-shot grid for the sandbox
            if (!_uiLogged && LogNativeUi()) _uiLogged = true;            // native-UI harvest check (one-shot)
        }

        // One-shot, flag-free terrain map via RAYCAST (the naval map has no Unity Terrain, so SampleHeight is
        // unavailable). Casts straight down at a coarse grid PLUS at every unit's spawn, reporting the surface
        // height + water/land so the next mission pass can place ground/airbases on real land at real heights.
        // Gated on a loaded mission (local HQ present). Lines are prefixed [TERRAIN] for easy capture.
        private bool AutoTerrainDump()
        {
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return false;
            float sea = Datum.SeaLevel.y;
            CommanderPlugin.Log?.LogInfo($"[TERRAIN] === raycast surface map (down-cast) === sea={sea:0.0}");

            for (int gz = -25000; gz <= 25000; gz += 5000)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[TERRAIN] z={gz,6}:");
                for (int gx = -25000; gx <= 25000; gx += 5000)
                {
                    float h = SurfaceHeight(gx, gz, sea);
                    sb.Append(h <= sea + 1f ? "  ~~~~" : $" {h,5:0}"); // ~~~~ = water, number = land height
                }
                CommanderPlugin.Log?.LogInfo(sb.ToString());
            }

            var units = UnitRegistry.allUnits;
            if (units != null)
                foreach (var u in units)
                {
                    if (u == null) continue;
                    var p = u.transform.position;
                    float h = SurfaceHeight(p.x, p.z, sea);
                    string kind = h <= sea + 1f ? "WATER" : "land";
                    CommanderPlugin.Log?.LogInfo($"[TERRAIN] unit {u.unitName,-18} pos=({p.x,7:0},{p.z,7:0}) surfaceH={h,6:0} {kind} (unitY={p.y:0})");
                }
            CommanderPlugin.Log?.LogInfo("[TERRAIN] === end map ===");
            return true;
        }

        // Surface height at (x,z): cast straight down from high up; the first hit is the island/terrain or the
        // water plane. No hit = open sea. (All layers — a rare hit on a unit is acceptable for a coarse map.)
        private static float SurfaceHeight(float x, float z, float sea)
        {
            return Physics.Raycast(new Vector3(x, 12000f, z), Vector3.down, out var hit, 24000f)
                ? hit.point.y : sea;
        }

        // Which native UI components are present & cloneable: counts + scene-vs-asset split + a sample per type.
        // Returns true once it has logged (UI was present).
        private bool LogNativeUi()
        {
            int border = CountNative<NuclearOption.UI.BetterBorder>("BetterBorder");
            int box     = CountNative<NuclearOption.UI.BoxToggle>("BoxToggle");
            int slider  = CountNative<NuclearOption.UI.SliderToggle>("SliderToggle");
            int group   = CountNative<NuclearOption.UI.BetterToggleGroup>("BetterToggleGroup");
            int buttons = CountNative<UnityEngine.UI.Button>("Button");
            // Consider UI "present" once any of the game-specific controls show up.
            return (border + box + slider + group) > 0 || buttons > 0;
        }

        // Log how many instances of T exist, how many are live scene objects (cloneable) vs assets/prefabs,
        // and a representative name/path. Mirrors the harvest filter used by UiFactory.CaptureNativeButtonSprite.
        private static int CountNative<T>(string label) where T : Component
        {
            var all = Resources.FindObjectsOfTypeAll<T>();
            int scene = 0; string sample = null;
            foreach (var c in all)
            {
                if (c == null) continue;
                bool inScene = c.gameObject.scene.IsValid();
                if (inScene) scene++;
                if (sample == null) sample = $"{c.name}{(inScene ? " (scene)" : " (asset)")}";
            }
            CommanderPlugin.Log?.LogInfo($"[S0:UI] {label} total={all.Length} sceneInstances={scene} sample={sample ?? "none"}");
            return all.Length;
        }

        // UID: friendly unit persistent ids over time — are they stable & non-reused after death?
        private void LogRoster()
        {
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return;
            var units = UnitRegistry.allUnits;
            if (units == null) return;
            int n = 0;
            foreach (var u in units)
            {
                if (u == null || u.NetworkHQ != hq) continue;
                string id = u.persistentID.ToString();
                if (!_firstSeenTick.ContainsKey(id)) { _firstSeenTick[id] = _tick; CommanderPlugin.Log?.LogInfo($"[S0:UID] new {id} {u.unitName} t={_tick}"); }
                n++;
            }
            if (_tick % 5 == 0) CommanderPlugin.Log?.LogInfo($"[S0:UID] friendly count={n} distinctSeen={_firstSeenTick.Count} t={_tick}");
        }

        // KILL: known-enemy tracking entries — does destroying one prune it from trackingDatabase, and when?
        private void LogTracking()
        {
            if (_tick % 3 != 0) return;
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return;
            var db = hq.trackingDatabase;
            if (db == null) return;
            int disabled = 0;
            foreach (var kv in db)
                if (kv.Value != null && kv.Value.TryGetUnit(out var u) && u != null && u.disabled) disabled++;
            CommanderPlugin.Log?.LogInfo($"[S0:KILL] tracked={db.Count} disabledStillTracked={disabled} t={_tick}");
        }

        // TERRAIN: water-vs-land across a coarse grid → confirms the sampling API + seeds sandbox coordinates.
        private void LogTerrain()
        {
            var terrain = Terrain.activeTerrain;
            CommanderPlugin.Log?.LogInfo($"[S0:TERRAIN] activeTerrain={(terrain != null ? terrain.name : "null")} sea={Datum.SeaLevel.y:0.0}");
            if (terrain == null) return;
            float baseY = terrain.transform.position.y;
            for (int gz = -30000; gz <= 30000; gz += 10000)
                for (int gx = -30000; gx <= 30000; gx += 10000)
                {
                    var p = new Vector3(gx, 0f, gz);
                    float h = terrain.SampleHeight(p) + baseY;
                    string kind = h <= Datum.SeaLevel.y ? "WATER" : "land";
                    CommanderPlugin.Log?.LogInfo($"[S0:TERRAIN] ({gx},{gz}) h={h:0} {kind}");
                }
        }
    }
}
