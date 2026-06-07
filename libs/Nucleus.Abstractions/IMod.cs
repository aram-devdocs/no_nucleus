using System;
using System.Collections.Generic;
using Nucleus.Core.Model;
using Nucleus.Core.Ports;
using Nucleus.Ui;
using UnityEngine;

namespace Nucleus.Abstractions
{
    /// <summary>
    /// The contract a Nucleus mod implements. The host owns lifecycle: it calls <see cref="Initialize"/> once
    /// after registration and the first scene is ready, <see cref="Tick"/> every map frame while the mod is
    /// enabled, and <see cref="OnEnabled"/>/<see cref="OnDisabled"/> on runtime toggles.
    /// </summary>
    public interface IMod
    {
        ModInfo Info { get; }
        void Initialize(IModContext ctx);
        void Tick(IModTickContext t);
        void OnEnabled();
        void OnDisabled();
        void Shutdown();
    }

    /// <summary>Everything the host hands a mod at initialize time: identity, logging, UI surface, shared game
    /// services, the button registry, and config binding.</summary>
    public interface IModContext
    {
        ModInfo Info { get; }
        bool IsEnabled { get; }
        ILogSink Log { get; }
        IModUi Ui { get; }
        IGameServices Game { get; }
        IButtonRegistry Buttons { get; }
        T BindConfig<T>(string section, string key, T def, string description);

        /// <summary>The shared live campaign every mod renders a slice of (null until a provider mod — the
        /// Commander — publishes it via <see cref="ShareCampaign"/>). Build/Squad/Warfare read this.</summary>
        Nucleus.Core.Command.ICampaign Campaign { get; }

        /// <summary>Publish the shared campaign to the host so other mods can read it via <see cref="Campaign"/>.
        /// Called once by the Commander mod, which owns the live campaign service.</summary>
        void ShareCampaign(Nucleus.Core.Command.ICampaign campaign);
    }

    /// <summary>The UI surface the host lends a mod: an isolated layer under the single shared overlay canvas,
    /// the live map icon layer (null when the map is closed), and the faction-themed palette.</summary>
    public interface IModUi
    {
        RectTransform CreateLayer(string name);
        Transform MapIconLayer { get; }
        Theme Theme { get; }
    }

    /// <summary>Per-frame context passed to <see cref="IMod.Tick"/>.</summary>
    public interface IModTickContext
    {
        bool MapOpen { get; }
        float UnscaledDeltaTime { get; }
        bool PointerOverModUi { get; }
    }

    /// <summary>Minimal logging seam (wraps the host's BepInEx ManualLogSource).</summary>
    public interface ILogSink
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    /// <summary>The shared read/command surface over the live game, owned once by the host and injected into
    /// every mod so the plugins don't each enumerate the roster or race the production cooldown.</summary>
    public interface IGameServices
    {
        IReadOnlyList<UnitView> Roster();
        IReadOnlyList<EnemyView> KnownEnemiesNear(Vec3 center, float radius);
        void Execute(UnitTask task);
        float Funds();
        bool TryGetLocalFaction(out FactionInfo faction);
        IMapProjection MapProjection { get; }
        /// <summary>Per-faction live force count (alive units + held airbases) for the attrition scoreboard.
        /// The Warfare mod diffs this tick-over-tick to feed unit/base losses into the war score.</summary>
        IReadOnlyList<Nucleus.Core.War.FactionCensus> WarCensus();
        /// <summary>The names of every faction in the loaded mission (e.g. for the setup screen's side list).</summary>
        IReadOnlyList<string> FactionNames();
        /// <summary>True once the local player has joined a side (so the setup screen can stand down).</summary>
        bool HasLocalFaction { get; }
        /// <summary>Join the named side as the local player (and open the map). False if it can't (no player/HQ).</summary>
        bool JoinFaction(string factionName);
        /// <summary>The loaded mission's name (so a mod can gate behaviour to a specific mission), or null.</summary>
        string CurrentMissionName { get; }
    }

    /// <summary>How a mod claims its in-game buttons: a map-bezel button (CMD/BLD/SQD/...) and/or a row in the
    /// main-menu loader. The host arbitrates blank VirtualMFD slots so two mods never collide.</summary>
    public interface IButtonRegistry
    {
        void RegisterMapButton(MapButtonSpec spec);
        void RegisterMainMenuItem(MenuItemSpec spec);
    }
}
