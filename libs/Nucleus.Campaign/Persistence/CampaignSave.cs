using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Nucleus.Core.Command;
using Nucleus.Core.Model;

namespace Nucleus.Core.Persistence
{
    /// <summary>
    /// Dependency-free, versioned text (de)serializer for a <see cref="CampaignSnapshot"/>. Uses a simple
    /// line/record, tab-delimited format — no JSON/NuGet dependency, so it is safe inside the game's
    /// Mono/BepInEx runtime and keeps the pure Campaign lib leaf-clean. Invariant-culture and round-trippable
    /// (floats via "R"). Enums are stored by name (tolerant of additions); unknown trailing record types are
    /// ignored (forward-compat). Strings (squad names, ids) are escaped so tabs/newlines survive, and null is
    /// distinguished from empty.
    /// </summary>
    public static class CampaignSave
    {
        private const string Header = "NUCLEUS-CAMPAIGN";
        private const char Sep = '\t';
        private static readonly char[] LineSplit = { '\n' };
        private const string NullToken = "\\0"; // one backslash + '0' — unreachable by an escaped real string

        // ---- Serialize -------------------------------------------------------

        public static string Serialize(CampaignSnapshot s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var sb = new StringBuilder();

            Line(sb, Header, I(s.Version));
            Line(sb, "META", B(s.AiCreatesObjectives), B(s.AiAutoFill), F(s.HomeBase.X), F(s.HomeBase.Y), F(s.HomeBase.Z),
                I(s.OperationIdSeed), I(s.SquadBatchSeed), I(s.ObjectiveIdSeed));
            Line(sb, "TUNE", F(s.RiskTolerance), F(s.ForceRatio), F(s.ClusterRadius), F(s.CoverageRadius),
                I(s.MaxSquadsPerOperation), F(s.FormRadius), I(s.MaxSquadSize), F(s.DepletedFraction));

            foreach (var o in s.Objectives)
                Line(sb, "OBJ", Enc(o.Id), E(o.Kind), F(o.Position.X), F(o.Position.Y), F(o.Position.Z),
                    Enc(o.TargetId), F(o.Priority), E(o.Source));

            foreach (var sq in s.Squads)
            {
                Line(sb, "SQUAD", Enc(sq.Id), Enc(sq.Name), E(sq.Family), E(sq.Origin), E(sq.Status),
                    E(sq.Autonomy), Enc(sq.AssignedOperationId));
                if (sq.TargetComposition != null)
                    foreach (var kv in sq.TargetComposition.Items)
                        Line(sb, "SQUADCOMP", Enc(sq.Id), E(kv.Key), I(kv.Value));
                foreach (var m in sq.MemberUnitIds)
                    Line(sb, "SQUADMEM", Enc(sq.Id), Enc(m));
            }

            foreach (var op in s.Operations)
            {
                Line(sb, "OP", Enc(op.Id), Enc(op.Objective?.Id), E(op.Autonomy), E(op.Status), E(op.Phase),
                    E(op.CombatPhase), Enc(op.OrderId), op.InitialThreat != null ? "1" : "0");
                foreach (var sid in op.SquadIds)
                    Line(sb, "OPSQUAD", Enc(op.Id), Enc(sid));
                if (op.InitialThreat != null)
                    foreach (var e in op.InitialThreat.Enemies)
                        Line(sb, "OPTHREAT", Enc(op.Id), Enc(e.Id), F(e.Position.X), F(e.Position.Y),
                            F(e.Position.Z), E(e.Class), E(e.Cap.Role), B(e.Cap.CanEngageGround),
                            B(e.Cap.CanEngageAir), B(e.Cap.CanCapture), B(e.Cap.IsSupply), B(e.Cap.IsAirDefense),
                            B(e.Accurate), F(e.StrategicPriority), I(e.ArmorTier));
            }

            foreach (var id in s.ConfirmedObjectives) Line(sb, "CONFIRMED", Enc(id));
            foreach (var kv in s.LastObjectiveByUnit) Line(sb, "LASTOBJ", Enc(kv.Key), Enc(kv.Value));

            return sb.ToString();
        }

        // ---- Deserialize -----------------------------------------------------

        public static CampaignSnapshot Deserialize(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var snap = new CampaignSnapshot();

            var objList = new List<Objective>();
            var squadList = new List<Squad>();
            var squadById = new Dictionary<string, Squad>();
            var compById = new Dictionary<string, Composition>();
            var membersById = new Dictionary<string, List<string>>();

            var opOrder = new List<string>();
            var opFields = new Dictionary<string, string[]>();
            var opSquads = new Dictionary<string, List<string>>();
            var opThreat = new Dictionary<string, List<EnemyView>>();

            foreach (var rawLine in text.Split(LineSplit))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0) continue;
                var f = line.Split(Sep);
                switch (f[0])
                {
                    case Header:
                        snap.Version = PI(f, 1);
                        break;
                    case "META":
                        snap.AiCreatesObjectives = PB(f, 1);
                        snap.AiAutoFill = PB(f, 2);
                        snap.HomeBase = new Vec3(PF(f, 3), PF(f, 4), PF(f, 5));
                        snap.OperationIdSeed = PI(f, 6);
                        snap.SquadBatchSeed = PI(f, 7);
                        snap.ObjectiveIdSeed = PI(f, 8);
                        break;
                    case "TUNE":
                        snap.RiskTolerance = PF(f, 1);
                        snap.ForceRatio = PF(f, 2);
                        snap.ClusterRadius = PF(f, 3);
                        snap.CoverageRadius = PF(f, 4);
                        snap.MaxSquadsPerOperation = PI(f, 5);
                        snap.FormRadius = PF(f, 6);
                        snap.MaxSquadSize = PI(f, 7);
                        snap.DepletedFraction = PF(f, 8);
                        break;
                    case "OBJ":
                        if (f.Length < 2) break;   // need at least the id
                        objList.Add(new Objective(Dec(f[1]), PE(f, 2, ObjectiveKind.DestroyTarget),
                            new Vec3(PF(f, 3), PF(f, 4), PF(f, 5)), PE(f, 8, ObjectiveSource.Auto),
                            PS(f, 6), PF(f, 7)));
                        break;
                    case "SQUAD":
                    {
                        if (f.Length < 3) break;   // need id + name
                        var sq = new Squad(Dec(f[1]), Dec(f[2]), PE(f, 3, RoleFamily.Other),
                            PE(f, 4, SquadOrigin.Auto))
                        {
                            Status = PE(f, 5, SquadStatus.Forming),
                            Autonomy = PE(f, 6, AutonomyLevel.Auto),
                            AssignedOperationId = PS(f, 7),
                        };
                        squadList.Add(sq);
                        squadById[sq.Id] = sq;
                        break;
                    }
                    case "SQUADCOMP":
                    {
                        if (f.Length < 2) break;   // need the squad id (dict key)
                        var id = Dec(f[1]);
                        if (!compById.TryGetValue(id, out var c)) { c = new Composition(); compById[id] = c; }
                        c.Set(PE(f, 2, RoleFamily.Other), PI(f, 3));
                        break;
                    }
                    case "SQUADMEM":
                    {
                        if (f.Length < 3) break;   // need squad id (dict key) + member id
                        var id = Dec(f[1]);
                        if (!membersById.TryGetValue(id, out var list)) { list = new List<string>(); membersById[id] = list; }
                        list.Add(Dec(f[2]));
                        break;
                    }
                    case "OP":
                        if (f.Length < 2) break;   // need the op id (dict key)
                        opOrder.Add(Dec(f[1]));
                        opFields[Dec(f[1])] = f;
                        break;
                    case "OPSQUAD":
                    {
                        if (f.Length < 3) break;   // need op id (dict key) + squad id
                        var id = Dec(f[1]);
                        if (!opSquads.TryGetValue(id, out var list)) { list = new List<string>(); opSquads[id] = list; }
                        list.Add(Dec(f[2]));
                        break;
                    }
                    case "OPTHREAT":
                    {
                        if (f.Length < 3) break;   // need op id (dict key) + enemy id
                        var id = Dec(f[1]);
                        if (!opThreat.TryGetValue(id, out var list)) { list = new List<EnemyView>(); opThreat[id] = list; }
                        var cap = new UnitCapability(PE(f, 7, Role.Unknown), PB(f, 8), PB(f, 9), PB(f, 10),
                            PB(f, 11), PB(f, 12));
                        list.Add(new EnemyView(Dec(f[2]), new Vec3(PF(f, 3), PF(f, 4), PF(f, 5)),
                            PE(f, 6, UnitClass.Other), cap, PB(f, 13), PF(f, 14), PI(f, 15)));
                        break;
                    }
                    case "CONFIRMED":
                        if (f.Length < 2) break;
                        snap.ConfirmedObjectives.Add(Dec(f[1]));
                        break;
                    case "LASTOBJ":
                        if (f.Length < 3) break;   // need unit-id key + value
                        snap.LastObjectiveByUnit.Add(new KeyValuePair<string, string>(Dec(f[1]), Dec(f[2])));
                        break;
                    // Unknown record types are ignored (forward-compat).
                }
            }

            // Assemble squads (attach composition + members in recorded order).
            foreach (var sq in squadList)
            {
                if (compById.TryGetValue(sq.Id, out var c)) sq.TargetComposition = c;
                if (membersById.TryGetValue(sq.Id, out var mem)) sq.MemberUnitIds.AddRange(mem);
                snap.Squads.Add(sq);
            }

            foreach (var o in objList) snap.Objectives.Add(o);

            // Build a temporary objective index so operations reattach by id.
            var objIndex = new Dictionary<string, Objective>();
            foreach (var o in objList) objIndex[o.Id] = o;

            foreach (var opId in opOrder)
            {
                var f = opFields[opId];
                var objId = PS(f, 2);
                if (objId == null || !objIndex.TryGetValue(objId, out var obj)) continue;
                var squadIds = opSquads.TryGetValue(opId, out var sl) ? sl : new List<string>();
                var op = new Operation(opId, obj, squadIds)
                {
                    Autonomy = PE(f, 3, AutonomyLevel.Auto),
                    Status = PE(f, 4, OperationStatus.Planning),
                    Phase = PE(f, 5, OrderPhase.Forming),
                    CombatPhase = PE(f, 6, CombatPhase.Recon),
                    OrderId = PS(f, 7),
                };
                bool hasThreat = f.Length > 8 && f[8] == "1";
                if (hasThreat)
                    op.InitialThreat = new ThreatPicture(opThreat.TryGetValue(opId, out var en) ? en : new List<EnemyView>());
                snap.Operations.Add(op);
            }

            return snap;
        }

        // ---- Field helpers ---------------------------------------------------

        private static void Line(StringBuilder sb, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(Sep);
                sb.Append(fields[i]);
            }
            sb.Append('\n');
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        private static string B(bool v) => v ? "1" : "0";
        private static string E<T>(T v) where T : struct => v.ToString();

        // Encode a possibly-null string field: null → sentinel, else escape control chars.
        private static string Enc(string s)
        {
            if (s == null) return NullToken;
            return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        private static string Dec(string f)
        {
            if (f == NullToken) return null;
            if (f.IndexOf('\\') < 0) return f;
            var sb = new StringBuilder(f.Length);
            for (int i = 0; i < f.Length; i++)
            {
                if (f[i] == '\\' && i + 1 < f.Length)
                {
                    char n = f[++i];
                    sb.Append(n == 't' ? '\t' : n == 'n' ? '\n' : n == 'r' ? '\r' : n);
                }
                else sb.Append(f[i]);
            }
            return sb.ToString();
        }

        // Bounds-checked string field (decoded): null when the column is missing, so a truncated/older known
        // record degrades gracefully instead of throwing IndexOutOfRange and aborting the whole load.
        private static string PS(string[] f, int i) => i < f.Length ? Dec(f[i]) : null;

        private static int PI(string[] f, int i)
            => i < f.Length && int.TryParse(f[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static float PF(string[] f, int i)
            => i < f.Length && float.TryParse(f[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

        private static bool PB(string[] f, int i) => i < f.Length && f[i] == "1";

        private static T PE<T>(string[] f, int i, T fallback) where T : struct
            => i < f.Length && Enum.TryParse<T>(f[i], out var v) ? v : fallback;
    }
}
