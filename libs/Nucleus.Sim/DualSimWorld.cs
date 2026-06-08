using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Sim
{
    /// <summary>The recorded outcome of a headless two-commander match: per-tick alive counts for each side,
    /// task/objective tallies, and a NaN guard. <see cref="Fingerprint"/> yields a stable string for
    /// determinism regression tests.</summary>
    public sealed class DualSimResult
    {
        public readonly List<int> AAlive = new List<int>();
        public readonly List<int> BAlive = new List<int>();
        public int ATasks, BTasks;
        public int AObjectivesMax, BObjectivesMax;
        public bool AnyNaN;

        public int Ticks => AAlive.Count;
        public int TotalStart => (AAlive.Count > 0 ? AAlive[0] : 0) + (BAlive.Count > 0 ? BAlive[0] : 0);
        public int TotalEnd => (AAlive.Count > 0 ? AAlive[^1] : 0) + (BAlive.Count > 0 ? BAlive[^1] : 0);

        public string Fingerprint()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < AAlive.Count; i++) sb.Append(AAlive[i]).Append(':').Append(BAlive[i]).Append(';');
            return sb.Append('A').Append(ATasks).Append('B').Append(BTasks).ToString();
        }
    }

    /// <summary>Headless model where both factions run their own <see cref="CommanderBrain"/> over the same
    /// battlefield — each sees the other as fog-of-war, tasks its own units, and they move + fight. Proves a
    /// self-running dynamic war end-to-end without the game. Deterministic: same seed ⇒ identical trace.</summary>
    public sealed class DualSimWorld
    {
        private const float Dt = 1f;
        private const float SensorRange = 9000f;
        private const float EngageRange = 1800f;

        private readonly List<SimUnit> _a;
        private readonly List<SimUnit> _b;
        private readonly Pcg _rng;
        private readonly CommanderState _sa;
        private readonly CommanderState _sb;
        private float _time;

        /// <summary>Optionally inject each side's CommanderState (e.g. with a genome-derived Doctrine) so two
        /// personalities can play each other for self-play / evolution. Null ⇒ a default commander (so the
        /// existing 3-arg call sites — and the determinism tests — are unchanged).</summary>
        public DualSimWorld(IEnumerable<SimUnit> a, IEnumerable<SimUnit> b, ulong seed,
            CommanderState sa = null, CommanderState sb = null)
        {
            _a = a.ToList();
            _b = b.ToList();
            _rng = new Pcg(seed);
            _sa = sa ?? new CommanderState();
            _sb = sb ?? new CommanderState();
        }

        public DualSimResult Run(int ticks)
        {
            var r = new DualSimResult();
            for (int i = 0; i < ticks; i++)
            {
                r.ATasks += StepSide(_a, _b, _sa);
                r.BTasks += StepSide(_b, _a, _sb);
                MoveSide(_a);
                MoveSide(_b);
                Attrition();
                _time += Dt;

                r.AAlive.Add(_a.Count(u => u.Alive));
                r.BAlive.Add(_b.Count(u => u.Alive));
                if (_sa.Objectives.Count > r.AObjectivesMax) r.AObjectivesMax = _sa.Objectives.Count;
                if (_sb.Objectives.Count > r.BObjectivesMax) r.BObjectivesMax = _sb.Objectives.Count;
                if (NonFinite(_a) || NonFinite(_b)) r.AnyNaN = true;
            }
            return r;
        }

        // Run one faction's brain: its own units are the roster, the other faction is fog-of-war intel.
        private int StepSide(List<SimUnit> own, List<SimUnit> foe, CommanderState state)
        {
            var roster = own.Where(u => u.Alive).Select(u => u.ToUnitView()).ToList();
            var known = new List<EnemyView>();
            foreach (var e in foe.Where(u => u.Alive))
            {
                float nearest = roster.Count == 0 ? float.MaxValue
                    : roster.Min(f => Dist(e.X, e.Z, f.Position.X, f.Position.Z));
                if (nearest <= SensorRange) known.Add(e.ToEnemyView(nearest <= SensorRange * 0.6f));
            }
            state.HomeBase = Centroid(roster);
            var tasks = CommanderBrain.Tick(new WorldSnapshot(roster, known, 5000f, null, _time), state);
            foreach (var t in tasks) ApplyTask(t, own, foe);
            return tasks.Count;
        }

        private static void ApplyTask(UnitTask t, List<SimUnit> own, List<SimUnit> foe)
        {
            var u = own.FirstOrDefault(x => x.Id == t.UnitId && x.Alive);
            if (u == null) return;
            u.Order = t.Verb;
            if (t.Verb == TaskVerb.AttackTarget && t.TargetId != null)
            {
                var tgt = foe.FirstOrDefault(e => e.Id == t.TargetId && e.Alive);
                if (tgt != null) { u.TgtX = tgt.X; u.TgtZ = tgt.Z; return; }
            }
            u.TgtX = t.Position.X; u.TgtZ = t.Position.Z;
        }

        private static void MoveSide(List<SimUnit> side)
        {
            foreach (var u in side.Where(x => x.Alive && x.Order != TaskVerb.Hold))
            {
                float dx = u.TgtX - u.X, dz = u.TgtZ - u.Z;
                float d = (float)System.Math.Sqrt(dx * dx + dz * dz);
                if (d <= 0.001f) continue;
                float step = System.Math.Min(u.Speed * Dt, d);
                u.X += dx / d * step; u.Z += dz / d * step;
            }
        }

        private void Attrition()
        {
            foreach (var u in _a.Where(x => x.Alive)) { var e = Nearest(u, _b); if (e != null) e.Hp -= Damage(u, e); }
            foreach (var u in _b.Where(x => x.Alive)) { var e = Nearest(u, _a); if (e != null) e.Hp -= Damage(u, e); }
        }

        private static SimUnit Nearest(SimUnit attacker, List<SimUnit> foes)
        {
            SimUnit best = null; float bestD = EngageRange;
            foreach (var t in foes)
            {
                if (!t.Alive || !CanEngage(attacker, t)) continue;
                float d = Dist(attacker.X, attacker.Z, t.X, t.Z);
                if (d <= bestD) { bestD = d; best = t; }
            }
            return best;
        }

        private static bool CanEngage(SimUnit a, SimUnit t)
        {
            bool air = t.Class == UnitClass.Aircraft;
            return air ? (a.Cap.CanEngageAir && a.AntiAir > 0f) : (a.Cap.CanEngageGround && a.AntiSurface > 0f);
        }

        private float Damage(SimUnit a, SimUnit t)
        {
            float anti = t.Class == UnitClass.Aircraft ? a.AntiAir : a.AntiSurface;
            return anti * 22f / System.Math.Max(1, t.ArmorTier) * _rng.Range(0.6f, 1.4f);
        }

        private static Vec3 Centroid(List<UnitView> roster)
        {
            if (roster.Count == 0) return new Vec3(0f, 0f, 0f);
            float x = 0f, z = 0f;
            foreach (var u in roster) { x += u.Position.X; z += u.Position.Z; }
            return new Vec3(x / roster.Count, 0f, z / roster.Count);
        }

        private static bool NonFinite(List<SimUnit> side)
            => side.Any(u => float.IsNaN(u.X) || float.IsInfinity(u.X) || float.IsNaN(u.Z) || float.IsInfinity(u.Z) || float.IsNaN(u.Hp));

        private static float Dist(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx, dz = az - bz;
            return (float)System.Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
