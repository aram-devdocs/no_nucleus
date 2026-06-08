using System;
using System.Globalization;
using System.IO;
using System.Text;
using Nucleus.Sim;

// Deterministic genome self-play. Usage: Nucleus.Evolve [seed] [generations] [outDir]
// Writes <outDir>/genepool.tsv + <outDir>/report.txt. Same args ⇒ identical output.
ulong seed = args.Length > 0 && ulong.TryParse(args[0], out var s) ? s : 1337UL;
int generations = args.Length > 1 && int.TryParse(args[1], out var g) ? g : 6;
string outDir = args.Length > 2 ? args[2] : Path.Combine("artifacts", "genomes");
Directory.CreateDirectory(outDir);

Console.WriteLine($"[evolve] seed={seed} generations={generations} -> {outDir}");
var result = Evolver.Run(seed, generations);

string F(float v) => v.ToString("0.000", CultureInfo.InvariantCulture);

var tsv = new StringBuilder();
tsv.AppendLine("# Nucleus genepool v1 — deterministic self-play (NOT auto-applied; review-only)");
tsv.AppendLine("archetype\tfitness\taggression\tcaution\treconBias\tdefenseBias\teconomyBias\tairGroundPref\tfocusBroad\toverextension\ttempo");
foreach (var gs in result.Final)
{
    var gn = gs.Genome;
    tsv.Append(gn.Archetype).Append('\t').Append(F(gs.Fitness)).Append('\t')
       .Append(F(gn.Aggression)).Append('\t').Append(F(gn.Caution)).Append('\t').Append(F(gn.ReconBias)).Append('\t')
       .Append(F(gn.DefenseBias)).Append('\t').Append(F(gn.EconomyBias)).Append('\t').Append(F(gn.AirGroundPref)).Append('\t')
       .Append(F(gn.FocusBroad)).Append('\t').Append(F(gn.Overextension)).Append('\t').Append(F(gn.Tempo)).Append('\n');
}
File.WriteAllText(Path.Combine(outDir, "genepool.tsv"), tsv.ToString());

var report = new StringBuilder();
report.AppendLine($"Nucleus self-play report  (seed={seed}, generations={generations})");
report.AppendLine("Deterministic genetic self-play over the headless DualSimWorld. Fitness = round-robin");
report.AppendLine("survival score (more units alive than the opponent at match end). REVIEW ONLY — the coarse");
report.AppendLine("sim's fitness is a proxy; shipped gameplay still uses the hand-authored archetypes.");
report.AppendLine();
foreach (var line in result.GenerationLog) report.AppendLine("  " + line);
report.AppendLine();
report.AppendLine("Final ranking:");
foreach (var gs in result.Final)
    report.AppendLine($"  {gs.Genome.Archetype,-14} fitness={gs.Fitness,6:0.0}  agg={gs.Genome.Aggression:0.00} cau={gs.Genome.Caution:0.00}");
File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());

Console.WriteLine($"[evolve] wrote genepool.tsv + report.txt ({result.Final.Count} genomes)");
Console.WriteLine("[NUCLEUS:METRIC] evolve seed=" + seed + " gens=" + generations + " best=" +
    (result.Final.Count > 0 ? result.Final[0].Genome.Archetype : "none"));
