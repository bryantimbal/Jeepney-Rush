#if !UNITY_5_3_OR_NEWER
// .NET-only test (System.Text.Json is not part of Unity): the JSON produced by the real
// Supabase SQL functions (saved by supabase/tests/run.sh) must contain every field the C#
// client reads, with exactly the same names – JsonUtility silently ignores mismatches.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class OnlineContractTests
    {
        private static string Dir()
        {
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "supabase", "tests", "contract"))) d = d.Parent;
            return d == null ? null : Path.Combine(d.FullName, "supabase");
        }

        private static void AssertHasFields(JsonElement json, Type t, string path, params string[] optional)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (optional.Contains(f.Name)) continue;
                Assert.IsTrue(json.TryGetProperty(f.Name, out var v), $"{path}: server JSON has no '{f.Name}' (C# {t.Name}.{f.Name})");
                var ft = f.FieldType;
                if (v.ValueKind == JsonValueKind.Object && ft.IsClass && ft != typeof(string)) AssertHasFields(v, ft, path + "." + f.Name, optional);
                if (v.ValueKind == JsonValueKind.Array && ft.IsArray && ft.GetElementType().IsClass && ft.GetElementType() != typeof(string))
                    foreach (var item in v.EnumerateArray()) AssertHasFields(item, ft.GetElementType(), path + "." + f.Name + "[]", optional);
            }
        }

        private static JsonElement Load(string name)
        {
            string dir = Dir();
            if (dir == null) Assert.Ignore("supabase/tests/contract not found");
            return JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "tests", "contract", name))).RootElement;
        }

        [Test] public void SnapshotMatches() => AssertHasFields(Load("get_state.json"), typeof(ServerSnapshot), "get_state");
        [Test] public void SubmitResultMatches() => AssertHasFields(Load("submit_run.json"), typeof(SubmitResult), "submit_run", "duplicate", "reason");
        [Test] public void OpResultMatches() => AssertHasFields(Load("buy.json"), typeof(OpResult), "buy", "reason", "reward", "friend");
        [Test] public void LeaderboardMatches() => AssertHasFields(Load("leaderboard.json"), typeof(LeaderboardPage), "leaderboard");
        [Test] public void FriendsMatch() => AssertHasFields(Load("friends.json"), typeof(FriendList), "friends");
        [Test] public void DailyMatches() => AssertHasFields(Load("daily.json"), typeof(DailyInfo), "daily");

        [Test]
        public void ServerReadsEveryReportField()
        {
            string dir = Dir();
            if (dir == null) Assert.Ignore("supabase not found");
            string sql = string.Join("\n", Directory.GetFiles(Path.Combine(dir, "migrations"), "*.sql").Select(File.ReadAllText));
            var stored = new[] { "jeepney", "client_version" }; // kept in the run record only
            foreach (var f in typeof(RunReport).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (stored.Contains(f.Name)) continue;
                Assert.IsTrue(Regex.IsMatch(sql, $"->>'{f.Name}'"), $"SQL never reads report field '{f.Name}'");
            }
        }

        [Test]
        public void EveryRpcTheClientCallsExists()
        {
            string dir = Dir();
            if (dir == null) Assert.Ignore("supabase not found");
            string sql = File.ReadAllText(Path.Combine(dir, "migrations", "20261121000000_jr_online.sql"));
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !File.Exists(Path.Combine(d.FullName, "unity", "JeepneyRush", "Assets", "JeepneyRush", "Scripts", "Runtime", "Online", "OnlineService.cs"))) d = d.Parent;
            string client = File.ReadAllText(Path.Combine(d.FullName, "unity", "JeepneyRush", "Assets", "JeepneyRush", "Scripts", "Runtime", "Online", "OnlineService.cs"));
            foreach (Match m in Regex.Matches(client, "\"(jr_[a-z_]+)\""))
            {
                string fn = m.Groups[1].Value;
                Assert.IsTrue(sql.Contains($"function public.{fn}("), $"client calls {fn} but the migration doesn't define it");
                // and every argument name the client sends is a real parameter
            }
            foreach (Match m in Regex.Matches(client, "\\\\\"(p_[a-z_]+)\\\\\""))
                Assert.IsTrue(sql.Contains(m.Groups[1].Value + " "), $"argument {m.Groups[1].Value} not found in SQL");
        }
    }
}
#endif
