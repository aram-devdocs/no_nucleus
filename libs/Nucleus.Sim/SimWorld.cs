using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Sim
{
    /// <summary>Per-tick record of the simulated campaign, for asserting emergent invariants.</summary>
    public sealed class SimResult
    {
        public readonly List<int> FriendlyAlive = new List<int>();
        public readonly List<int> EnemyAlive = new List<int>();
        public readonly List<int> Objectives = new List<int>();
        public readonly List<int> Operations = new List<int>();
        public int TasksTotal;
        public bool AnyNaN;

        public int MaxPhase;

        public int Ticks => FriendlyAlive.Count;
        public int EnemyStart => EnemyAlive.Count > 0 ? EnemyAlive[0] : 0;
        public int EnemyEnd => EnemyAlive.Count > 0 ? EnemyAlive[EnemyAlive.Count - 1] : 0;
        public int MaxObjectives => Objectives.Count > 0 ? Objectives.Max() : 0;
        public int MaxOperations => Operations.Count > 0 ? Operations.Max() : 0;

        /// <summary>A compact deterministic fingerprint of the whole run (for same-seed determinism checks).</summary>
        public string Fingerprint()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < FriendlyAlive.Count; i++)
                sb.Append(FriendlyAlive[i]).Append(',').Append(EnemyAlive[i]).Append(',')
                  .Append(Objectives[i]).Append(',').Append(Operations[i]).Append(';');
            sb.Append("T").Append(TasksTotal);
            return sb.ToString();
        }
    }

    /// <summary>
    /// A deterministic headless battlefield that steps the pure <see cref="CommanderBrain"/>: project the live
    /// units into the Domain read-models, run the brain, apply its per-unit tasks, move units, resolve
    /// attrition, advance fog/time. Reuses Vec3/UnitView/EnemyView so the brain sees exactly what it sees in
    /// game. Same seed ⇒ identical trace.
    /// </summary>
    public sealed class SimWorld
    {
        private const float Dt = 1f;
        private const float SensorRange = 9000f;
        private const float EngageRange = 1800f;

        private readonly List<SimUnit> _friendly;
        private readonly List<SimUnit> _enemy;
        private readonly Pcg _rng;
        private readonly CommanderState _state = new CommanderState();
        private float _time;

        public SimWorld(IEnumerable<SimUnit> friendly, IEnumerable<SimUnit> enemy, ulong seed)
        {
            _friendly = friendly.ToList();
            _enemy = enemy.ToList();
            _rng = new Pcg(seed);
        }

        public SimResult Run(int ticks)
        {
            var r = new SimResult();
            for (int i = 0; i < ticks; i++)
            {
                Step(r);
                r.FriendlyAlive.Add(_friendly.Count(u => u.Alive));
                r.EnemyAlive.Add(_enemy.Count(u => u.Alive));
                r.Objectives.Add(_state.Objectives.Count);
                r.Operations.Add(_state.Operations.Count);
                int ph = _state.Operations.Count > 0 ? _state.Operations.Max(o => (int)o.CombatPhase) : 0;
                if (ph > r.MaxPhase) r.MaxPhase = ph;
                if (AnyNonFinite()) r.AnyNaN = true;
            }
            return r;
        }

        private void Step(SimResult r)
        {
            var roster = _friendly.Where(u => u.Alive).Select(u => u.ToUnitView()).ToList();
            var known = KnownEnemies(roster);
            _state.HomeBase = Centroid(roster);
            var snap = new WorldSnapshot(roster, known, funds: 5000f, committedUnitIds: null, time: _time);

            var tasks = CommanderBrain.Tick(snap, _state);
            r.TasksTotal += tasks.Count;
            foreach (var t in tasks) ApplyTask(t);

            Move();
            Attrition();
            _time += Dt;
        }

        private List<EnemyView> KnownEnemies(List<UnitView> roster)
        {
            var known = new List<EnemyView>();
            foreach (var e in _enemy.Where(u => u.Alive))
            {
                float nearest = float.MaxValue;
                foreach (var f in roster)
                    nearest = System.Math.Min(nearest, Dist(e.X, e.Z, f.Position.X, f.Position.Z));
                if (nearest <= SensorRange) known.Add(e.ToEnemyView(accurate: nearest <= SensorRange * 0.6f));
            }
            return known;
        }

        private void ApplyTask(UnitTask t)
        {
            var u = _friendly.FirstOrDefault(x => x.Id == t.UnitId && x.Alive);
            if (u == null) return;
            u.Order = t.Verb;
            if (t.Verb == TaskVerb.AttackTarget && t.TargetId != null)
            {
                var tgt = _enemy.FirstOrDefault(e => e.Id == t.TargetId && e.Alive);
                if (tgt != null) { u.TgtX = tgt.X; u.TgtZ = tgt.Z; }
                else { u.TgtX = t.Position.X; u.TgtZ = t.Position.Z; }
            }
            else { u.TgtX = t.Position.X; u.TgtZ = t.Position.Z; }
        }

        private void Move()
        {
            foreach (var u in _friendly.Where(x => x.Alive && x.Order != TaskVerb.Hold))
            {
                float dx = u.TgtX - u.X, dz = u.TgtZ - u.Z;
                float d = (float)System.Math.Sqrt(dx * dx + dz * dz);
                if (d <= 0.001f) continue;
                float step = System.Math.Min(u.Speed * Dt, d);
                u.X += dx / d * step;
                u.Z += dz / d * step;
            }
        }

        private void Attrition()
        {
            // Friendly fire on the nearest engageable enemy in range; enemies return fire on the nearest friendly.
            foreach (var f in _friendly.Where(x => x.Alive))
            {
                var e = NearestEngageable(f, _enemy);
                if (e != null) e.Hp -= Damage(f, e);
            }
            foreach (var e in _enemy.Where(x => x.Alive))
            {
                var f = NearestEngageable(e, _friendly);
                if (f != null) f.Hp -= Damage(e, f);
            }
        }

        private SimUnit NearestEngageable(SimUnit attacker, List<SimUnit> targets)
        {
            SimUnit best = null;
            float bestD = EngageRange;
            foreach (var t in targets)
            {
                if (!t.Alive || !CanEngage(attacker, t)) continue;
                float d = Dist(attacker.X, attacker.Z, t.X, t.Z);
                if (d <= bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private static bool CanEngage(SimUnit a, SimUnit t)
        {
            bool targetIsAir = t.Class == UnitClass.Aircraft;
            return targetIsAir ? (a.Cap.CanEngageAir && a.AntiAir > 0f) : (a.Cap.CanEngageGround && a.AntiSurface > 0f);
        }

        private float Damage(SimUnit a, SimUnit t)
        {
            float anti = t.Class == UnitClass.Aircraft ? a.AntiAir : a.AntiSurface;
            float baseDmg = anti * 22f / System.Math.Max(1, t.ArmorTier);
            return baseDmg * _rng.Range(0.6f, 1.4f);
        }

        private Vec3 Centroid(List<UnitView> roster)
        {
            if (roster.Count == 0) return new Vec3(0f, 0f, 0f);
            float x = 0f, z = 0f;
            foreach (var u in roster) { x += u.Position.X; z += u.Position.Z; }
            return new Vec3(x / roster.Count, 0f, z / roster.Count);
        }

        private bool AnyNonFinite()
        {
            foreach (var u in _friendly.Concat(_enemy))
                if (!IsFinite(u.X) || !IsFinite(u.Z) || !IsFinite(u.Hp)) return true;
            return false;
        }

        private static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));
        private static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
