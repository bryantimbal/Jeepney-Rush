using System;
using System.Globalization;
using System.IO;
using System.Text;
using JeepneyRush.Logic;

// Usage: dotnet run --project unity/tools/ExportCatalog [repoRoot]
// Writes a catalog migration (only if the catalog changed) and supabase/dev-tests/vectors/*.csv
string root = args.Length > 0 ? args[0] : FindRoot();
var inv = CultureInfo.InvariantCulture;
string migrations = Path.Combine(root, "supabase/migrations");
string latest = CatalogExport.LatestFile(migrations);
string sql = CatalogExport.Sql();
if (latest == null) File.WriteAllText(Path.Combine(migrations, CatalogExport.FileName), sql);
else if (File.ReadAllText(latest).Replace("\r\n", "\n") == sql) Console.WriteLine($"catalog unchanged ({Path.GetFileName(latest)})");
else
{
    // never edit a migration that may already be deployed: add a new one
    string name = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + CatalogExport.FileSuffix;
    File.WriteAllText(Path.Combine(migrations, name), sql);
    Console.WriteLine($"catalog changed -> new migration {name} (commit it; Supabase applies it on deploy)");
}
string vec = Path.Combine(root, "supabase/dev-tests/vectors");
Directory.CreateDirectory(vec);

// 1) PRNG (mulberry32) raw outputs
var sb = new StringBuilder();
foreach (long seed in new long[] { 0, 1, 7, 42, 123456789, -5, unchecked(20261121 * 7919 + 17), 2147483647, -2147483648 })
{
    var r = new GameRandom((int)seed);
    for (int i = 0; i < 6; i++) sb.Append($"{seed},{i},{r.Next().ToString("R", inv)}\n");
}
File.WriteAllText(Path.Combine(vec, "rng.csv"), sb.ToString());

// 2) missions for two years of days and ~3 years of weeks
sb.Clear();
var start = new DateTime(2026, 1, 1);
for (int d = 0; d < 730; d++)
{
    int key = Missions.DayKey(start.AddDays(d));
    var defs = Missions.Daily(key);
    for (int s = 0; s < defs.Length; s++) sb.Append($"daily,{key},{s},{defs[s].Type},{defs[s].Target},{defs[s].Reward}\n");
}
for (int w = 100; w < 260; w++)
{
    var defs = Missions.Weekly(w);
    for (int s = 0; s < defs.Length; s++) sb.Append($"weekly,{w},{s},{defs[s].Type},{defs[s].Target},{defs[s].Reward}\n");
}
File.WriteAllText(Path.Combine(vec, "missions.csv"), sb.ToString());

// 3) daily challenges
sb.Clear();
for (int d = 0; d < 730; d++)
{
    int key = Missions.DayKey(start.AddDays(d));
    var c = DailyChallenge.For(key);
    sb.Append($"{key},{c.Seed},{(int)c.Environment}\n");
}
File.WriteAllText(Path.Combine(vec, "daily.csv"), sb.ToString());

// 4) week keys for dates
sb.Clear();
for (int d = 0; d < 800; d += 3) { var dt = start.AddDays(d); sb.Append($"{dt:yyyy-MM-dd},{Missions.DayKey(dt)},{Missions.WeekKey(dt)}\n"); }
File.WriteAllText(Path.Combine(vec, "dates.csv"), sb.ToString());

// 5) levels / xp
sb.Clear();
foreach (int xp in new[] { 0, 1, 119, 120, 479, 480, 1079, 1080, 5000, 123456 }) sb.Append($"{xp},{PlayerLevel.LevelFor(xp)}\n");
File.WriteAllText(Path.Combine(vec, "levels.csv"), sb.ToString());

// 6) run bounds: random reports -> violation or clamped values
sb.Clear();
var g = new GameRandom(99);
for (int i = 0; i < 400; i++)
{
    float dist = g.Chance(0.1) ? -5 : g.Range(0, 9000);
    var rep = new RunReport
    {
        run_id = "vec-" + i, duration = g.Chance(0.1) ? g.Range(0, 6) : g.Range(10, 900), distance = dist, env = g.Chance(0.05) ? 7 : g.Int(0, 2),
        coins = g.Int(-2, 20000), score = g.Int(0, 900000), passengers = g.Int(0, 900), deliveries = g.Int(0, 900),
        power_ups = g.Int(0, 80), smooth_humps = g.Int(0, 120), refuels = g.Int(0, 20), hits = g.Int(0, 5),
        wet_distance = g.Range(0, 9000), clean_distance = g.Range(0, 9000),
    };
    if (g.Chance(0.5)) { rep.deliveries = Math.Min(rep.deliveries, rep.passengers); rep.wet_distance = Math.Min(rep.wet_distance, Math.Max(0, rep.distance)); rep.clean_distance = Math.Min(rep.clean_distance, Math.Max(0, rep.distance)); rep.coins = Math.Abs(rep.coins); }
    string F(float f) => f.ToString("R", inv);
    string input = $"{rep.run_id},{F(rep.duration)},{F(rep.distance)},{rep.env},{rep.coins},{rep.score},{rep.passengers},{rep.deliveries},{rep.power_ups},{rep.smooth_humps},{rep.refuels},{rep.hits},{F(rep.wet_distance)},{F(rep.clean_distance)}";
    string v = RunBounds.HardViolation(rep);
    if (v == null) { RunBounds.Clamp(rep); }
    sb.Append($"{input},{v ?? "ok"},{rep.coins},{rep.score},{rep.passengers},{rep.deliveries},{rep.power_ups},{rep.smooth_humps},{rep.refuels}\n");
}
File.WriteAllText(Path.Combine(vec, "bounds.csv"), sb.ToString());
Console.WriteLine($"catalog + vectors written under {root}");

static string FindRoot()
{
    var d = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (d != null && !Directory.Exists(Path.Combine(d.FullName, "supabase"))) d = d.Parent;
    if (d == null) throw new Exception("run from inside the repository");
    return d.FullName;
}
