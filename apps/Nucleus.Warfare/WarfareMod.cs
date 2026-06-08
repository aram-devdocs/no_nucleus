using System.Linq;
using Nucleus.Abstractions;
using Nucleus.Core.Command;
using Nucleus.Core.Persistence;
using Nucleus.Ui;

namespace Nucleus.Warfare
{
    /// <summary>Nucleus Dynamic Warfare: a persistent two-faction war where both sides run the autonomous
    /// commander. Owns the <see cref="WarfareCampaign"/> and its save/resume. Per-faction battlefield views come
    /// from the "Nucleus Dynamic Warfare" mission, which grants both sides' rosters.</summary>
    public sealed class WarfareMod : IMod
    {
        private readonly string _savePath;
        private WarfareCampaign _campaign;
        private IModContext _ctx;
        private CommanderPanel _panel;
        // Live attrition feed: faction-name -> (alive units, airbases) from the last census, to diff for losses.
        private readonly System.Collections.Generic.Dictionary<string, (int units, int bases)> _lastCensus
            = new System.Collections.Generic.Dictionary<string, (int, int)>();
        private string _bluforFaction, _opforFaction;  // which mission faction maps to each war side
        private float _attritionClock;                 // throttle: census diff runs ~1 Hz, not every frame

        public WarfareMod(string savePath) { _savePath = savePath; }

        public ModInfo Info { get; } = new ModInfo
        {
            Id = "warfare",
            DisplayName = "Warfare",
            Version = "0.1.0",
            Author = "Nucleus",
            Description = "Persistent two-faction dynamic war (both sides run the AI commander); save and resume.",
        };

        public void Initialize(IModContext ctx)
        {
            _ctx = ctx;
            _campaign = WarfareSave.Load(_savePath) ?? new WarfareCampaign();
            // The mod feeds exact losses from the live census, so the roster-shrink heuristic (sim-only) is off.
            _campaign.UseRosterAttrition = false;

            ctx.Log.Info("[NUCLEUS:SELFTEST] PASS warfare-mod-loaded");
            ctx.Log.Info($"[NUCLEUS:METRIC] warfareTurn={_campaign.Turn}");

            ctx.Buttons.RegisterMapButton(new MapButtonSpec
            {
                ModId = Info.Id,
                Label = "WAR",
                BuildContent = parent =>
                {
                    _panel = new CommanderPanel(parent, ctx.Ui.Theme,
                        onToggleOpManual: id => ctx.Campaign?.ToggleOperationManual(id),
                        sections: CommanderPanel.PanelSections.Scoreboard | CommanderPanel.PanelSections.Operations
                                | CommanderPanel.PanelSections.Feed);
                    UiFactory.Stretch(_panel.Root);
                },
                OnClick = ReportStatus,
            });
        }

        /// <summary>The live campaign (for the mission driver).</summary>
        public WarfareCampaign Campaign => _campaign;

        /// <summary>Persist the current war so it can be resumed next session.</summary>
        public void Save() => WarfareSave.Save(_savePath, _campaign);

        private void ReportStatus()
        {
            _ctx?.Log.Info($"[Warfare] turn {_campaign.Turn} — Blufor: {_campaign.Blufor.Objectives.Count} obj / "
                + $"{_campaign.Blufor.Operations.Count} ops · Opfor: {_campaign.Opfor.Objectives.Count} obj / "
                + $"{_campaign.Opfor.Operations.Count} ops");
        }

        private bool _setupApplied;
        private string _lastMission;

        // Apply pre-mission setup (per-side commander kinds + the local toggles) once sides are name-bound.
        // Latches only when a side actually matched, else retries next tick — a transient START-tick census
        // can't silently drop the config.
        private void ApplySetup()
        {
            if (_setupApplied || !Nucleus.Core.War.WarSetup.Configured || _campaign == null) return;
            if (_bluforFaction == null) return; // sides not bound yet — wait, don't latch

            bool matched = false;
            foreach (var kv in Nucleus.Core.War.WarSetup.Commanders)
            {
                if (kv.Key == _campaign.War.Blufor.FactionName) { _campaign.War.Blufor.Commander = kv.Value; matched = true; }
                else if (kv.Key == _campaign.War.Opfor.FactionName) { _campaign.War.Opfor.Commander = kv.Value; matched = true; }
            }
            if (!matched) return; // bound, but the setup names don't line up yet — retry next tick

            // Seed the local commander's toggles (the player's side).
            _ctx?.Campaign?.SetAiCreatesObjectives(Nucleus.Core.War.WarSetup.PlayerSideAiCommander);
            _ctx?.Campaign?.SetAiAutoFill(Nucleus.Core.War.WarSetup.AiAutoFill);
            _setupApplied = true;
            _ctx?.Log.Info($"[NUCLEUS:SELFTEST] PASS war-setup-applied player='{Nucleus.Core.War.WarSetup.PlayerFaction}'");
        }

        // A new mission/war means fresh setup + attrition binding — clear the latched state so it re-applies.
        private void ResetForNewMissionIfNeeded()
        {
            var mission = _ctx?.Game?.CurrentMissionName;
            if (mission == _lastMission) return;
            _lastMission = mission;
            _setupApplied = false;
            _enemyLogged = false;
            _bluforFaction = _opforFaction = null;
            _lastCensus.Clear();
        }

        private float _enemyClock;

        // Drive the ENEMY (non-local) faction with our brain, reading its own roster + fog-of-war intel and
        // tasking its units. The local faction is driven by the Commander mod; this handles the other side.
        private void DriveEnemyAi()
        {
            if (_ctx?.Game == null || _campaign == null || _bluforFaction == null) return;
            if (!_ctx.Game.TryGetLocalFaction(out var local) || local == null) return;

            // The enemy is the bound side that isn't the local player's faction.
            string enemy = local.Name == _bluforFaction ? _opforFaction
                : local.Name == _opforFaction ? _bluforFaction : null;
            if (enemy == null) return;

            // Only drive it if that side is AI-commanded (default; the setup screen can make it human too).
            if (Nucleus.Core.War.WarSetup.Configured
                && Nucleus.Core.War.WarSetup.Commanders.TryGetValue(enemy, out var kind)
                && kind == Nucleus.Core.War.CommanderKind.Human) return;

            var state = enemy == _bluforFaction ? _campaign.Blufor : _campaign.Opfor;

            // Deterministic personality from the faction name (only drives persisted RiskTolerance/ForceRatio,
            // so resume stays identical).
            if (!_enemyGenomeApplied)
            {
                _enemyGenomeApplied = true;
                var genome = Nucleus.Core.Command.GenomeFactory.ForCommander("nucleus-war", enemy);
                state.Doctrine.ApplyGenome(genome);
                _ctx.Log.Info($"[NUCLEUS:METRIC] enemy-commander faction='{enemy}' archetype='{genome.Archetype}' aggression={genome.Aggression:0.00} caution={genome.Caution:0.00} forceRatio={state.Doctrine.ForceRatio:0.00}");
                _ctx.Log.Info("[NUCLEUS:SELFTEST] PASS enemy-personality-assigned");
            }

            var roster = _ctx.Game.RosterFor(enemy);
            var intel = _ctx.Game.KnownEnemiesFor(enemy, new Nucleus.Core.Model.Vec3(0, 0, 0), 5_000_000f);
            // Home base = roster centroid, else HomeBase stays at origin and the AI's home defence never triggers.
            state.HomeBase = Nucleus.Core.Model.RosterGeometry.Centroid(roster);
            var snapshot = new Nucleus.Core.Command.WorldSnapshot(roster, intel, 0f, null, 0f);
            var tasks = Nucleus.Core.Command.CommanderBrain.Tick(snapshot, state);
            foreach (var task in tasks) _ctx.Game.Execute(task);

            if (!_enemyLogged && tasks.Count > 0)
            {
                _enemyLogged = true;
                _ctx.Log.Info($"[NUCLEUS:SELFTEST] PASS enemy-ai-driving faction='{enemy}' roster={roster.Count} tasks={tasks.Count}");
            }
        }
        private bool _enemyLogged;
        private bool _enemyGenomeApplied;

        public void Tick(IModTickContext t)
        {
            _attritionClock += t.UnscaledDeltaTime;
            if (_attritionClock >= 1f) { _attritionClock = 0f; ResetForNewMissionIfNeeded(); FeedAttrition(); ApplySetup(); }
            _enemyClock += t.UnscaledDeltaTime;
            if (_enemyClock >= 1.5f) { _enemyClock = 0f; DriveEnemyAi(); }

            // Throttle the panel render to ~7Hz like CommanderRuntime — rebuilding the HQ snapshot every frame
            // over hundreds of units was the in-mission lag.
            _renderClock += t.UnscaledDeltaTime;
            if (_renderClock < RenderInterval) return;
            _renderClock = 0f;
            var c = _ctx?.Campaign;
            if (_panel != null && c != null) _panel.RenderHq(c.Hq(), c.Catalog(), c.Funds());
            if (_panel != null && _campaign != null) _panel.RenderScoreboard(_campaign.SnapshotBoard());
        }
        private float _renderClock;
        private const float RenderInterval = 0.14f;

        // Diff the live per-faction census against last tick; feed unit/base drops into the attrition score.
        // The two biggest combatants bind to Blufor/Opfor on first sight, name-sorted for stability.
        private void FeedAttrition()
        {
            if (_ctx?.Game == null || _campaign == null) return;
            var census = _ctx.Game.WarCensus();
            if (census == null || census.Count == 0) return;

            // First sight: bind the two factions with the most forces (units+bases), name-sorted for stability.
            if (_bluforFaction == null && census.Count >= 2)
            {
                var ranked = census.OrderByDescending(f => f.AliveUnits + f.Airbases).Take(2)
                    .Select(f => f.FactionName).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
                _bluforFaction = ranked[0];
                _opforFaction = ranked[1];
                _campaign.War.Blufor.FactionName = _bluforFaction;
                _campaign.War.Opfor.FactionName = _opforFaction;
                _ctx.Log.Info($"[NUCLEUS:SELFTEST] PASS warfare-factions-bound blufor={_bluforFaction} opfor={_opforFaction}");
            }
            if (_bluforFaction == null) return; // not yet bound (only one faction present so far)

            // Iterate the bound names (a WIPED faction leaves the census), treating a missing faction as zero
            // forces, so the decisive final losses still feed instead of being dropped.
            FeedSide(census, _bluforFaction, blufor: true);
            FeedSide(census, _opforFaction, blufor: false);
        }

        private void FeedSide(System.Collections.Generic.IReadOnlyList<Nucleus.Core.War.FactionCensus> census,
            string faction, bool blufor)
        {
            int units = 0, bases = 0;
            foreach (var f in census)
                if (f.FactionName == faction) { units = f.AliveUnits; bases = f.Airbases; break; }

            if (_lastCensus.TryGetValue(faction, out var prev))
            {
                int unitDrop = prev.units - units;
                int baseDrop = prev.bases - bases;
                if (unitDrop > 0) _campaign.RecordUnitLost(blufor, unitDrop);
                if (baseDrop > 0) _campaign.RecordBaseLost(blufor, baseDrop);
            }
            _lastCensus[faction] = (units, bases);
        }

        public void OnEnabled() { }
        public void OnDisabled() { }
        public void Shutdown()
        {
            // Persist on shutdown so a multi-hour war survives quitting the game.
            if (_campaign != null && _campaign.Turn > 0) Save();
        }
    }
}
