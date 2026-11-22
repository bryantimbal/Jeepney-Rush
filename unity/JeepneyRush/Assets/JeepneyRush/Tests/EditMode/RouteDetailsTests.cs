using System.Linq;
using JeepneyRush.Logic;
using JeepneyRush.Logic.Audio;
using JeepneyRush.Logic.Geometry;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class RouteDetailsTests
    {
        [Test]
        public void MmdaOnlyInMetroManila()
        {
            Assert.AreEqual(EnforcerKind.Mmda, Enforcers.For(EnvironmentKind.Manila));
            Assert.AreEqual(EnforcerKind.Mmda, Enforcers.For(EnvironmentKind.Edsa));
            foreach (var env in new[] { EnvironmentKind.Provincial, EnvironmentKind.Beach, EnvironmentKind.Mountain })
                Assert.AreEqual(EnforcerKind.Police, Enforcers.For(env), env.ToString());
            Assert.AreEqual(EnforcerKind.CityTraffic, Enforcers.For(EnvironmentKind.Cebu));
            Assert.AreEqual(EnforcerKind.CityTraffic, Enforcers.For(EnvironmentKind.Baguio));
            foreach (EnforcerKind k in System.Enum.GetValues(typeof(EnforcerKind)))
            {
                var m = PropBuilder.Enforcer(k);
                Assert.IsTrue(m.SubmeshKeys.Contains("tex:" + Enforcers.SignTexture(k)), k.ToString());
                StringAssert.Contains("CHECKPOINT", Enforcers.Checkpoint(k));
            }
            Assert.IsFalse(Enforcers.Checkpoint(EnforcerKind.Police).Contains("MMDA"));
        }

        [Test]
        public void PatokHasPullCordSignAndPlainJeepneyDoesNot()
        {
            Assert.IsTrue(JeepneyBuilder.Build(JeepneyCatalog.Get("cogeo")).SubmeshKeys.Contains("utex:hila_sign"));
            Assert.IsFalse(JeepneyBuilder.Build(JeepneyCatalog.Get("manila")).SubmeshKeys.Contains("utex:hila_sign"));
            Assert.Greater(Lines.PullCord.Length, 0);
        }

        [Test]
        public void GasStationIsLabelledAndLitAtNight()
        {
            var m = PropBuilder.GasStation(3.2f, 2.7f, 18f);
            CollectionAssert.IsSupersetOf(m.SubmeshKeys, new[] { "utex:gas_sign", "utex:gas_fascia", "lightpool" });
            Assert.IsTrue(PropBuilder.GasMarker().SubmeshKeys.Contains("utexa:gas_label"));
            Assert.Greater(Lines.GasPrice.Length, 3);
        }

        [Test]
        public void BrakeAndBellSoundsAreAudible()
        {
            foreach (var s in new[] { Sfx.Bell, Sfx.BrakeSqueal, Sfx.AirBrake })
            {
                var buf = SoundBank.Make(s);
                Assert.Greater(buf.Length, Synth.Rate / 4, s.ToString());
                Assert.Greater(buf.Max(v => System.Math.Abs(v)), 0.2f, s.ToString());
                Assert.IsTrue(buf.All(v => !float.IsNaN(v)), s.ToString());
            }
        }
    }
}
