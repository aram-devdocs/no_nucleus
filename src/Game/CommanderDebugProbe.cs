using System.Collections.Generic;
using UnityEngine;

namespace CommanderLayer.Game
{
    /// <summary>
    /// S0 de-risk instrumentation (behind the <c>CommanderDebug</c> config flag, default off). Logs
    /// structured "[S0:*]" lines to the BepInEx console so ONE playtest resolves the runtime unknowns:
    /// unit-id stability, kill/track pruning, and terrain water/land (which also seeds the P0.5 sandbox).
    /// Throwaway / foldable once findings are recorded in PROGRESS.md.
    /// </summary>
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

            if (!Plugin.CommanderDebug) return;
            _tick++;
            LogRoster();   // UID stability
            LogTracking(); // KILL / prune detection
            if (!_terrainLogged) { _terrainLogged = true; LogTerrain(); } // one-shot grid for the sandbox
            if (!_uiLogged && LogNativeUi()) _uiLogged = true;            // P6.2 harvest de-risk (one-shot)
        }

        // One-shot, flag-free terrain map: a coarse water/land + height grid PLUS the terrain result at every
        // unit's spawn — so the next mission pass can place ground/airbases on real land at real heights.
        // Returns true once it has logged (terrain present). Lines are prefixed [TERRAIN] for easy capture.
        private bool AutoTerrainDump()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return false;
            float baseY = terrain.transform.position.y;
            float sea = Datum.SeaLevel.y;
            Plugin.Log?.LogInfo($"[TERRAIN] === Terrain map for mission placement === active={terrain.name} sea={sea:0.0}");

            // Coarse grid over the playable area.
            for (int gz = -20000; gz <= 20000; gz += 5000)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"[TERRAIN] z={gz,6}:");
                for (int gx = -20000; gx <= 20000; gx += 5000)
                {
                    float h = terrain.SampleHeight(new Vector3(gx, 0f, gz)) + baseY;
                    sb.Append(h <= sea ? "  ~~~~" : $" {h,5:0}"); // ~~~~ = water, number = land height
                }
                Plugin.Log?.LogInfo(sb.ToString());
            }

            // Exact terrain at every unit's spawn position (tells us who is mis-placed and the real height).
            var units = UnitRegistry.allUnits;
            if (units != null)
                foreach (var u in units)
                {
                    if (u == null) continue;
                    var p = u.transform.position;
                    float h = terrain.SampleHeight(new Vector3(p.x, 0f, p.z)) + baseY;
                    string kind = h <= sea ? "WATER" : "land";
                    Plugin.Log?.LogInfo($"[TERRAIN] unit {u.unitName,-18} pos=({p.x,7:0},{p.z,7:0}) terrainH={h,5:0} {kind} (unitY={p.y:0})");
                }
            Plugin.Log?.LogInfo("[TERRAIN] === end terrain map ===");
            return true;
        }

        // P6.2 UI-HARVEST: which native UI components are present & cloneable? Logs counts + scene-vs-asset
        // split + a sample for each game UI type we want to clone, so the real NativeUi re-base targets
        // what actually exists in a live mission. Returns true once it has logged (UI was present).
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
            Plugin.Log?.LogInfo($"[S0:UI] {label} total={all.Length} sceneInstances={scene} sample={sample ?? "none"}");
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
                if (!_firstSeenTick.ContainsKey(id)) { _firstSeenTick[id] = _tick; Plugin.Log?.LogInfo($"[S0:UID] new {id} {u.unitName} t={_tick}"); }
                n++;
            }
            if (_tick % 5 == 0) Plugin.Log?.LogInfo($"[S0:UID] friendly count={n} distinctSeen={_firstSeenTick.Count} t={_tick}");
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
            Plugin.Log?.LogInfo($"[S0:KILL] tracked={db.Count} disabledStillTracked={disabled} t={_tick}");
        }

        // TERRAIN: water-vs-land across a coarse grid → confirms the sampling API + seeds sandbox coordinates.
        private void LogTerrain()
        {
            var terrain = Terrain.activeTerrain;
            Plugin.Log?.LogInfo($"[S0:TERRAIN] activeTerrain={(terrain != null ? terrain.name : "null")} sea={Datum.SeaLevel.y:0.0}");
            if (terrain == null) return;
            float baseY = terrain.transform.position.y;
            for (int gz = -30000; gz <= 30000; gz += 10000)
                for (int gx = -30000; gx <= 30000; gx += 10000)
                {
                    var p = new Vector3(gx, 0f, gz);
                    float h = terrain.SampleHeight(p) + baseY;
                    string kind = h <= Datum.SeaLevel.y ? "WATER" : "land";
                    Plugin.Log?.LogInfo($"[S0:TERRAIN] ({gx},{gz}) h={h:0} {kind}");
                }
        }
    }
}
