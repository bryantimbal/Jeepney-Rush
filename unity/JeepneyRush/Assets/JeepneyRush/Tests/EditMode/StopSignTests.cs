using System.IO;
using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class StopSignTests
    {
        [Test]
        public void EveryStopSignUsesAPictureNamedAfterThatStop()
        {
            for (int i = 0; i < StopNames.All.Length; i++)
            {
                string tex = StopNames.SignTexture(i);
                string slug = StopNames.All[i].ToLowerInvariant().Replace(' ', '_').Replace(".", "").Replace("-", "_");
                StringAssert.Contains(slug.Replace("__", "_").TrimEnd('_').Split('_')[0], tex, $"{StopNames.All[i]} → {tex}");
                Assert.IsTrue(PropBuilder.StopSign(i).SubmeshKeys.Contains("tex:" + tex));
            }
            // names are unique even where Manila and EDSA share a stop (CUBAO, MONUMENTO)
            Assert.AreEqual(StopNames.All.Length, Enumerable.Range(0, StopNames.All.Length).Select(StopNames.SignTexture).Distinct().Count());
            Assert.AreEqual("stop_mtn_sadanga", StopNames.SignTexture(System.Array.IndexOf(StopNames.All, "SADANGA")));
        }

#if !UNITY_5_3_OR_NEWER
        [Test]
        public void EverySignPictureExists()
        {
            var d = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "unity", "JeepneyRush"))) d = d.Parent;
            Assume.That(d, Is.Not.Null, "repo not found");
            string dir = Path.Combine(d.FullName, "unity", "JeepneyRush", "Assets", "JeepneyRush", "Resources", "Textures");
            for (int i = 0; i < StopNames.All.Length; i++)
                Assert.IsTrue(File.Exists(Path.Combine(dir, StopNames.SignTexture(i) + ".png")), StopNames.SignTexture(i));
        }
#endif
    }
}
