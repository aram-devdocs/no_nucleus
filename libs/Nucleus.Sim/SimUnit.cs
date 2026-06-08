using Nucleus.Core.Model;

namespace Nucleus.Sim
{
    /// <summary>A mutable battlefield unit. Projects to the Domain read-models (UnitView/EnemyView) the brain
    /// decides over, and carries the current order the brain issued plus simple combat stats.</summary>
    public sealed class SimUnit
    {
        public string Id;
        public UnitClass Class;
        public UnitCapability Cap;
        public float X, Z;
        public float Hp = 100f;
        public float Speed = 200f;          // m/tick
        public float AntiSurface;
        public float AntiAir;
        public int ArmorTier = 1;
        public float StrategicPriority = 1f; // used when projected as an enemy

        // Current order (set from the brain's UnitTask).
        public TaskVerb Order = TaskVerb.Hold;
        public float TgtX, TgtZ;
        public string TargetId;

        public bool Alive => Hp > 0f;
        public Vec3 Pos => new Vec3(X, 0f, Z);

        public UnitView ToUnitView() =>
            new UnitView(Id, Id, Pos, Class, disabled: !Alive, commandable: true, Cap, AntiSurface, AntiAir, ArmorTier);

        public EnemyView ToEnemyView(bool accurate) =>
            new EnemyView(Id, Pos, Class, Cap, accurate, StrategicPriority, ArmorTier);
    }
}
