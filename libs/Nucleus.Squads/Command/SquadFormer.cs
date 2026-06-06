using System.Collections.Generic;
using System.Linq;
using CommanderLayer.Core.Model;

namespace CommanderLayer.Core.Command
{
    /// <summary>Squad-forming tunables.</summary>
    public sealed class SquadConfig
    {
        /// <summary>Units within this distance (m) of a squad seed join it.</summary>
        public float FormRadius { get; set; } = 4000f;
        public int MaxSquadSize { get; set; } = 5;
        /// <summary>A squad below this fraction of its target make-up is marked Depleted.</summary>
        public float DepletedFraction { get; set; } = 0.5f;
    }

    /// <summary>
    /// Pure auto-forming: clusters loose commandable units into squads by role family + proximity, with
    /// deterministic ids/names (so tests and reconciliation are stable). Munitions/buildings (the Other
    /// family) are never squadable.
    /// </summary>
    public static class SquadFormer
    {
        private static readonly string[] Phonetic =
            { "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliet" };

        public static IReadOnlyList<Squad> Form(IReadOnlyList<UnitView> units, SquadConfig cfg, string batch = "auto")
        {
            var result = new List<Squad>();
            var squadable = (units ?? new List<UnitView>())
                .Where(u => u != null && !u.Disabled && u.Commandable && Families.IsSquadable(Families.Of(u.Role)));

            foreach (var grp in squadable.GroupBy(u => Families.Of(u.Role)).OrderBy(g => g.Key))
            {
                var pool = grp.OrderBy(u => u.Id).ToList(); // deterministic seed order
                int idx = 0;
                while (pool.Count > 0)
                {
                    var seed = pool[0];
                    pool.RemoveAt(0);
                    var members = new List<UnitView> { seed };
                    foreach (var c in pool
                                 .Where(u => u.Position.HorizontalDistanceTo(seed.Position) <= cfg.FormRadius)
                                 .OrderBy(u => u.Position.HorizontalDistanceTo(seed.Position))
                                 .ToList())
                    {
                        if (members.Count >= cfg.MaxSquadSize) break;
                        members.Add(c);
                        pool.Remove(c);
                    }
                    result.Add(new Squad($"{batch}-{grp.Key}-{idx}", $"{grp.Key} {Name(idx)}",
                        grp.Key, SquadOrigin.Auto, members.Select(m => m.Id))
                    { Status = SquadStatus.Ready });
                    idx++;
                }
            }
            return result;
        }

        private static string Name(int i) => i < Phonetic.Length ? Phonetic[i] : (i + 1).ToString();
    }
}
