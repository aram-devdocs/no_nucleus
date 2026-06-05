using CommanderLayer.Core.Ports;
using UnityEngine;

namespace CommanderLayer.Game
{
    /// <summary>IClock over Unity's unscaled time (so it ticks even when the game is paused).</summary>
    public sealed class UnityClock : IClock
    {
        public float Now => Time.unscaledTime;
    }
}
