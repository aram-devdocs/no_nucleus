using System.Collections.Generic;
using System.Linq;
using Nucleus.Core.Model;

namespace Nucleus.Core.Command
{
    /// <summary>
    /// Stateful (engine-free) owner of the faction's squads. Each tick it reconciles squads against the live
    /// roster: prunes dead members, disbands empty auto squads (player squads persist as Reserve), marks
    /// depleted, and auto-forms loose units into new squads. Mirrors <c>AssignmentManager</c>'s style — pure
    /// snapshots in, no Unity types. The brain reads <see cref="Squads"/> as its available force.
    /// </summary>
    public sealed class SquadRoster
    {
        private readonly SquadConfig _cfg;
        private readonly List<Squad> _squads = new List<Squad>();
        private int _batch;

        public SquadRoster(SquadConfig cfg) { _cfg = cfg ?? new SquadConfig(); }

        /// <summary>The auto-form batch counter (drives auto squad ids "auto{n}-..."). Exposed so persistence
        /// can save/restore it, keeping restored auto-squad ids from colliding with new ones.</summary>
        public int BatchSeed { get => _batch; set => _batch = value; }

        /// <summary>The squad-forming config (so persistence can round-trip the same tunables).</summary>
        public SquadConfig Config => _cfg;

        public IReadOnlyList<Squad> Squads => _squads;
        public Squad ById(string id) => _squads.FirstOrDefault(s => s.Id == id);
        public void Add(Squad squad) => _squads.Add(squad); // player-created

        /// <summary><paramref name="excludeIds"/> = units owned by the manual layer; treated as unavailable
        /// (pruned from squads, never auto-formed) so the autonomous brain and manual orders don't fight.</summary>
        public void Reconcile(IReadOnlyList<UnitView> roster, IReadOnlyCollection<string> excludeIds = null)
        {
            var alive = new HashSet<string>();
            foreach (var u in roster ?? new List<UnitView>())
                if (u != null && !u.Disabled && u.Commandable && (excludeIds == null || !excludeIds.Contains(u.Id)))
                    alive.Add(u.Id);

            // Prune dead members; disband empty AUTO squads (player squads survive as Reserve).
            foreach (var s in _squads) s.MemberUnitIds.RemoveAll(id => !alive.Contains(id));
            _squads.RemoveAll(s => s.Origin == SquadOrigin.Auto && s.IsEmpty);

            var inSquad = new HashSet<string>();
            foreach (var s in _squads)
            {
                foreach (var id in s.MemberUnitIds) inSquad.Add(id);
                s.Status = StatusFor(s);
            }

            // Auto-form whatever's left loose into fresh squads (alive already excludes committed/dead units).
            var loose = (roster ?? new List<UnitView>())
                .Where(u => u != null && alive.Contains(u.Id) && !inSquad.Contains(u.Id)).ToList();
            if (loose.Count > 0)
                _squads.AddRange(SquadFormer.Form(loose, _cfg, "auto" + _batch++));
        }

        private SquadStatus StatusFor(Squad s)
        {
            if (s.IsEmpty) return SquadStatus.Reserve;
            int target = s.TargetComposition?.Total ?? 0;
            if (target > 0 && s.Strength < target * _cfg.DepletedFraction) return SquadStatus.Depleted;
            // Below full strength but not yet committed → still forming up (recruiting toward its target). Once
            // assigned it reads Engaged even if under strength (the op state matters more than the headcount then).
            if (s.AssignedOperationId == null && target > 0 && s.Strength < target) return SquadStatus.Forming;
            return s.AssignedOperationId != null ? SquadStatus.Engaged : SquadStatus.Ready;
        }
    }
}
