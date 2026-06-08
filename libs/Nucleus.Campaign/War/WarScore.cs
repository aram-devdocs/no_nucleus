using System;

namespace Nucleus.Core.War
{
    /// <summary>A faction's attrition score — the dynamic war's win currency. Falls on lost units/bases and on
    /// reinforcement spend, where the spend penalty grows EXPONENTIALLY per base lost (so a baseless side can
    /// still buy with cash, but each buy bleeds score far faster). Defeated at zero.</summary>
    public sealed class WarScore
    {
        // Default attrition knobs, exposed so the scoreboard's "how the score works" line is derived here (SSOT),
        // never re-typed in the UI. The constructor defaults to these.
        public const float DefaultStart = 1000f;
        public const float DefaultUnitValue = 8f;
        public const float DefaultBaseValue = 120f;

        public float Score { get; private set; }
        public int BasesLost { get; private set; }
        public int UnitsLost { get; private set; }
        public float TotalSpent { get; private set; }

        private readonly float _unitValue;       // score lost per unit destroyed
        private readonly float _baseValue;       // score lost per base lost
        private readonly float _spendPenaltyBase; // score lost per $ spent at FULL bases
        private readonly float _falloffPerBaseLost; // exponential growth of the spend penalty per base lost

        public WarScore(float start = DefaultStart, float unitValue = DefaultUnitValue, float baseValue = DefaultBaseValue,
            float spendPenaltyBase = 0.02f, float falloffPerBaseLost = 0.6f)
        {
            // Same-typed float knobs — guard each so a transposed/negative argument fails loudly, not silently.
            if (start < 0f) throw new ArgumentOutOfRangeException(nameof(start));
            if (unitValue < 0f) throw new ArgumentOutOfRangeException(nameof(unitValue));
            if (baseValue < 0f) throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (spendPenaltyBase < 0f) throw new ArgumentOutOfRangeException(nameof(spendPenaltyBase));
            if (falloffPerBaseLost < 0f) throw new ArgumentOutOfRangeException(nameof(falloffPerBaseLost));
            Score = start;
            _unitValue = unitValue;
            _baseValue = baseValue;
            _spendPenaltyBase = spendPenaltyBase;
            _falloffPerBaseLost = falloffPerBaseLost;
        }

        /// <summary>Score lost per $1 of reinforcement spend right now (grows as bases are lost).</summary>
        public float SpendPenalty => _spendPenaltyBase * (float)Math.Exp(_falloffPerBaseLost * BasesLost);

        /// <summary>True once the faction is out of the war.</summary>
        public bool Defeated => Score <= 0f;

        public void UnitLost(int count = 1)
        {
            if (count <= 0) return;
            UnitsLost += count;
            Reduce(_unitValue * count);
        }

        public void BaseLost(int count = 1)
        {
            if (count <= 0) return;
            BasesLost += count;
            Reduce(_baseValue * count);
        }

        /// <summary>Spend on reinforcement (off-map convoy/fleet, build at base). Debits attrition by the cost
        /// times the current (falloff-weighted) spend penalty.</summary>
        public void Spend(float cost)
        {
            if (cost <= 0f) return;
            TotalSpent += cost;
            Reduce(cost * SpendPenalty);
        }

        private void Reduce(float amount) => Score = Math.Max(0f, Score - amount);

        /// <summary>Restore persisted state verbatim (the falloff is order-dependent, so we save the raw
        /// counters rather than replaying events). Persistence-only.</summary>
        internal void Restore(float score, int basesLost, int unitsLost, float totalSpent)
        {
            Score = score;
            BasesLost = basesLost;
            UnitsLost = unitsLost;
            TotalSpent = totalSpent;
        }
    }
}
