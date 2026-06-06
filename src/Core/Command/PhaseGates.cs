using System;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Command
{
    // CombatPhase + ForceState live in Nucleus.Domain (Command/CombatPhase.cs) — same namespace, pure leaf —
    // so the Domain-level RoleFamily can reference CombatPhase without depending up into this gating logic.

    /// <summary>
    /// Pure combined-arms gating: whether an operation may advance PAST a given phase, thresholded by
    /// doctrine over the current/initial <see cref="ThreatPicture"/> + force. Generalizes the old
    /// <c>OrderPlanner.SeadPending</c> (Sead gate = air defenses suppressed). "Soften before assault,
    /// SEAD before strike, air superiority before SEAD" all fall out of the ordered sequence + these gates.
    /// </summary>
    public static class PhaseGates
    {
        /// <summary>The default airbase-assault sequence.</summary>
        public static readonly CombatPhase[] Sequence =
        {
            CombatPhase.Recon, CombatPhase.AirSuperiority, CombatPhase.Sead,
            CombatPhase.Strike, CombatPhase.Assault, CombatPhase.Capture, CombatPhase.Hold
        };

        /// <summary>True = this phase's objective is met, so the operation may advance to the next phase.</summary>
        public static bool Satisfied(CombatPhase phase, ThreatPicture current, ThreatPicture initial,
            ForceState force, Doctrine doctrine)
        {
            switch (phase)
            {
                case CombatPhase.Recon:
                    return current != null;                                   // we have a picture of the area
                case CombatPhase.AirSuperiority:
                    return current.AirCount == 0
                        || force.Fighters >= (int)Math.Ceiling(current.AirCount * doctrine.AirSuperiorityRatio);
                case CombatPhase.Sead:
                    return current.AirDefenseCount <= doctrine.MaxResidualAirDefense;
                case CombatPhase.Strike:
                    return Softened(initial, current, doctrine);
                case CombatPhase.Assault:
                    return Satisfied(CombatPhase.Sead, current, initial, force, doctrine)
                        && Satisfied(CombatPhase.Strike, current, initial, force, doctrine);
                case CombatPhase.Capture:
                    return current.Count == 0;                               // area clear enough to take it
                case CombatPhase.Hold:
                    return false;                                            // terminal — hold the ground
                default:
                    return true;
            }
        }

        /// <summary>The first phase in the sequence whose gate is NOT yet satisfied (the operation's active phase).</summary>
        public static CombatPhase ActivePhase(ThreatPicture current, ThreatPicture initial, ForceState force, Doctrine doctrine)
        {
            foreach (var p in Sequence)
                if (!Satisfied(p, current, initial, force, doctrine)) return p;
            return CombatPhase.Hold;
        }

        private static bool Softened(ThreatPicture initial, ThreatPicture current, Doctrine doctrine)
        {
            int init = (initial?.ArmorCount ?? 0) + (initial?.AirDefenseCount ?? 0);
            if (init == 0) return true;                                       // nothing to soften
            int cur = current.ArmorCount + current.AirDefenseCount;
            float removed = (init - cur) / (float)init;
            return removed >= doctrine.SoftenThreshold;
        }
    }
}
