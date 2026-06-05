using CommanderLayer.Core.Model;

namespace CommanderLayer.Game
{
    /// <summary>Executes a Core UnitTask on the real unit (host-side). The proven surface-unit path.</summary>
    public sealed class GameUnitCommands
    {
        public void Execute(UnitTask task)
        {
            var u = Find(task.UnitId);
            if (u == null) return;

            switch (task.Verb)
            {
                case TaskVerb.MoveTo:
                case TaskVerb.AttackTarget: // P1: drive into engagement range; the unit auto-engages. Focus-fire = P4.
                    if (u is ICommandable c && c.UnitCommand != null)
                    {
                        c.UnitCommand.SetDestination(GameConvert.ToGlobal(task.Position), playerCommand: true);
                    }
                    break;
                case TaskVerb.Hold:
                    if (u is Ship s) s.SetHoldPosition(true);
                    else if (u is GroundVehicle g) g.SetHoldPosition(true);
                    break;
            }
        }

        private static Unit Find(string id)
        {
            foreach (var u in UnitRegistry.allUnits)
            {
                if (u != null && u.GetInstanceID().ToString() == id) return u;
            }
            return null;
        }
    }
}
