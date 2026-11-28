using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ContractTests
    {
        [Test]
        public void HospitalOnTime()
        {
            var c = new ContractRun();
            c.Start(ContractKind.Hospital, 1000f, 10f);
            Assert.AreEqual(0, c.OnStopServed(1200f, 20f, out _), "not there yet");
            Assert.AreEqual(1, c.OnStopServed(1550f, 40f, out var k));
            Assert.AreEqual(ContractKind.Hospital, k);
            Assert.AreEqual(1, c.Completed);
            Assert.IsFalse(c.Active);
        }

        [Test]
        public void HospitalTooLate()
        {
            var c = new ContractRun();
            c.Start(ContractKind.Hospital, 0f, 0f);
            Assert.AreEqual(-1, c.OnStopServed(600f, ContractRun.TimeLimit(ContractKind.Hospital) + 1f, out _));
        }

        [Test]
        public void PakyawMeansNoOtherStops()
        {
            var c = new ContractRun();
            c.Start(ContractKind.Pakyaw, 0f, 0f);
            Assert.AreEqual(-1, c.OnStopServed(300f, 20f, out _), "stopped for others");
            c.Start(ContractKind.Pakyaw, 0f, 0f);
            Assert.AreEqual(1, c.OnStopServed(750f, 60f, out _));
        }

        [Test]
        public void LechonAndWedding()
        {
            var c = new ContractRun();
            c.Start(ContractKind.Lechon, 0f, 0f);
            c.OnRoughHump();
            Assert.AreEqual(-1, c.OnStopServed(700f, 30f, out _));
            c.Start(ContractKind.Wedding, 0f, 0f);
            c.OnRoughHump();
            Assert.AreEqual(1, c.OnStopServed(900f, 30f, out _), "a hump doesn't hurt the wedding");
            c.Start(ContractKind.Wedding, 0f, 0f);
            c.OnCrash();
            Assert.AreEqual(-1, c.OnStopServed(900f, 30f, out _));
        }

        [Test]
        public void OffersAreLimited()
        {
            var c = new ContractRun();
            var rng = new GameRandom(3);
            int offered = 0;
            for (int i = 0; i < 400; i++)
            {
                var k = c.Roll(rng, i, false);
                if (k == ContractKind.None) continue;
                c.Start(k, 0, 0);
                offered++;
                c.OnStopServed(99999f, 1f, out _);
            }
            Assert.AreEqual(ContractRun.MaxPerRun, offered);
            Assert.AreEqual(ContractKind.None, new ContractRun().Roll(new GameRandom(1), 0, false), "not at the first stops");
        }
    }
}
