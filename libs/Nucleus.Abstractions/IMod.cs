using System;
using System.Collections.Generic;
using CommanderLayer.Core.Model;
using CommanderLayer.Core.Ports;
using CommanderLayer.Ui;
using UnityEngine;

namespace CommanderLayer.Abstractions
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
    }

    /// <summary>How a mod claims its in-game buttons: a map-bezel button (CMD/BLD/SQD/...) and/or a row in the
    /// main-menu loader. The host arbitrates blank VirtualMFD slots so two mods never collide.</summary>
    public interface IButtonRegistry
    {
        void RegisterMapButton(MapButtonSpec spec);
        void RegisterMainMenuItem(MenuItemSpec spec);
    }
}
