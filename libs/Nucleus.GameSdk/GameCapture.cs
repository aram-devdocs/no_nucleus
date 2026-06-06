using CommanderLayer.Core.Model;
using UnityEngine;

namespace CommanderLayer.Game
{
    /// <summary>
    /// Game-side poll for Capture-order completion: an objective (Airbase) near the order point now owned by
    /// the local faction's HQ. Engine-only; feeds the pure AssignmentManager via a predicate.
    /// </summary>
    public sealed class GameCapture
    {
        // How close the order point must be to the matched objective for it to count as "this objective".
        private const float MatchRadius = 4000f;

        // Airbases are fixed map features — scan once and reuse (ownership changes, the set doesn't).
        private Airbase[] _airbases;

        public bool IsHeldByUs(Vec3 world)
        {
            if (!GameManager.GetLocalHQ(out var hq) || hq == null) return false;

            if (_airbases == null || _airbases.Length == 0) _airbases = Object.FindObjectsOfType<Airbase>();

            var p = new Vector3(world.X, 0f, world.Z);
            Airbase nearest = null;
            float best = float.MaxValue;
            foreach (var ab in _airbases)
            {
                if (ab == null || ab.center == null) continue;
                var c = ab.center.position; c.y = 0f;
                float d = (c - p).sqrMagnitude;
                if (d < best) { best = d; nearest = ab; }
            }
            if (nearest == null) return false;
            if (Mathf.Sqrt(best) > MatchRadius) return false;   // clicked away from any objective
            return ReferenceEquals(nearest.CurrentHQ, hq);
        }
    }
}
