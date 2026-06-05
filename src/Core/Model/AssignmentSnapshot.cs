using System.Collections.Generic;

namespace CommanderLayer.Core.Model
{
    public enum AssignmentState
    {
        EnRoute,
        Arrived
    }

    /// <summary>One friendly unit's relation to the current objective.</summary>
    public sealed class UnitAssignment
    {
        public string UnitName { get; }
        public string TypeName { get; }
        public Vec3 Position { get; }
        public float DistanceToObjective { get; }
        public bool Commandable { get; }
        public AssignmentState State { get; }

        public UnitAssignment(string unitName, string typeName, Vec3 position, float distance, bool commandable, AssignmentState state)
        {
            UnitName = unitName;
            TypeName = typeName;
            Position = position;
            DistanceToObjective = distance;
            Commandable = commandable;
            State = state;
        }
    }

    /// <summary>
    /// The computed readback for the current objective: friendly units ranked by distance, with how many
    /// are commandable (directly steerable) versus air units that divert toward it autonomously.
    /// </summary>
    public sealed class AssignmentSnapshot
    {
        public static readonly AssignmentSnapshot Empty = new AssignmentSnapshot(new List<UnitAssignment>());

        public IReadOnlyList<UnitAssignment> Units { get; }
        public int Total => Units.Count;

        public int CommandableCount
        {
            get
            {
                int n = 0;
                foreach (var u in Units)
                {
                    if (u.Commandable) n++;
                }
                return n;
            }
        }

        public AssignmentSnapshot(IReadOnlyList<UnitAssignment> units)
        {
            Units = units;
        }
    }
}
