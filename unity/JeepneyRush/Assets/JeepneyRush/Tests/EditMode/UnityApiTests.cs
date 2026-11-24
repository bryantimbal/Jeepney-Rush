using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    /// <summary>
    /// The compile check uses older Unity reference assemblies, so it can't see APIs that newer
    /// Unity 6 versions turned into errors (GetInstanceID became an error in 6000.6). This scans
    /// the scripts for them so they never reach the editor.
    /// </summary>
    public class UnityApiTests
    {
        private static readonly (string pattern, string why)[] Banned =
        {
            (@"\.GetInstanceID\s*\(", "error in Unity 6000.6 (use a serial number or GetEntityId)"),
            (@"InstanceIDToObject\s*\(", "obsolete in Unity 6"),
            (@"\bFindFirstObjectByType\s*<", "deprecated in Unity 6 (use FindAnyObjectByType)"),
            (@"\bFindObjectOfType\s*<", "obsolete in Unity 6 (use FindAnyObjectByType)"),
            (@"\bFindObjectsOfType\s*<", "obsolete in Unity 6 (use FindObjectsByType)"),
        };

        [Test]
        public void ScriptsAvoidApisRemovedInNewerUnity()
        {
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "unity", "JeepneyRush", "Assets", "JeepneyRush", "Scripts"))) d = d.Parent;
            if (d == null) Assert.Ignore("scripts folder not found (running inside Unity)");
            var scripts = Path.Combine(d.FullName, "unity", "JeepneyRush", "Assets", "JeepneyRush", "Scripts");
            int files = 0;
            foreach (var f in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                files++;
                var lines = File.ReadAllLines(f);
                for (int i = 0; i < lines.Length; i++)
                {
                    var code = lines[i].Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    foreach (var (pattern, why) in Banned)
                        if (Regex.IsMatch(code, pattern)) Assert.Fail($"{Path.GetFileName(f)}:{i + 1}: {why}\n  {lines[i].Trim()}");
                }
            }
            Assert.Greater(files, 50);
        }
    }
}
