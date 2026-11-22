using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class DriverRatingTests
    {
        [Test]
        public void StartsAtFourAndStaysBetweenOneAndFive()
        {
            var r = new DriverRating();
            Assert.AreEqual(4f, r.Value);
            for (int i = 0; i < 40; i++) r.Apply(RatingEvent.Served);
            Assert.AreEqual(5f, r.Value);
            for (int i = 0; i < 40; i++) r.Apply(RatingEvent.Crash);
            Assert.AreEqual(1f, r.Value);
            Assert.IsTrue(r.AtBottom);
            Assert.AreEqual("1.0", r.Display);
        }

        [Test]
        public void ThreeSkippedStopsInARowMakeRidersPanicUntilAStopIsServed()
        {
            var r = new DriverRating();
            r.Apply(RatingEvent.SkippedStop);
            r.Apply(RatingEvent.SkippedStop);
            Assert.IsFalse(r.Panicking);
            r.Apply(RatingEvent.SkippedStop);
            Assert.IsTrue(r.Panicking);
            Assert.AreEqual(4f - 1.2f, r.Value, 1e-4);
            r.Apply(RatingEvent.Crash); // crashes don't reset the count
            Assert.IsTrue(r.Panicking);
            r.Apply(RatingEvent.Served);
            Assert.IsFalse(r.Panicking);
            Assert.AreEqual(0, r.SkippedInARow);
        }

        [Test]
        public void SkippingStopsAloneEventuallyBringsThePolice()
        {
            var r = new DriverRating();
            int skips = 0;
            while (!r.AtBottom && skips < 20) { r.Apply(RatingEvent.SkippedStop); skips++; }
            Assert.IsTrue(r.AtBottom);
            Assert.AreEqual(8, skips, "★4.0 → ★1.0 takes 8 skipped stops in a row");
            r.Pardon();
            Assert.AreEqual(DriverRating.AfterPardon, r.Value);
            Assert.IsFalse(r.Panicking);
        }

        [Test]
        public void RatingScalesFares()
        {
            var r = new DriverRating();
            Assert.Greater(r.FareMultiplier, 1f);           // ★4 = a little extra
            for (int i = 0; i < 10; i++) r.Apply(RatingEvent.Served);
            Assert.AreEqual(1.2f, r.FareMultiplier, 1e-3);  // ★5 = +20 %
            for (int i = 0; i < 40; i++) r.Apply(RatingEvent.Crash);
            Assert.AreEqual(0.7f, r.FareMultiplier, 1e-3);  // floor
        }

        [Test]
        public void PullingOverInTimeEscapesTheChase()
        {
            var c = new PoliceChase();
            c.Start();
            for (int i = 0; i < 100; i++) Assert.AreEqual(PoliceChase.Outcome.Chasing, c.Tick(0.05f, false));
            var o = PoliceChase.Outcome.Chasing;
            for (int i = 0; i < 40 && o == PoliceChase.Outcome.Chasing; i++) o = c.Tick(0.05f, true);
            Assert.AreEqual(PoliceChase.Outcome.PulledOver, o);
            Assert.IsFalse(c.Active);
        }

        [Test]
        public void KeepDrivingAndYoureBusted()
        {
            var c = new PoliceChase();
            c.Start();
            Assert.Greater(PoliceChase.TimeToCatch, 15f, "enough time to brake and pull over");
            var o = PoliceChase.Outcome.Chasing;
            float t = 0;
            while (o == PoliceChase.Outcome.Chasing && t < 60) { o = c.Tick(0.1f, false); t += 0.1f; }
            Assert.AreEqual(PoliceChase.Outcome.Busted, o);
            Assert.AreEqual(PoliceChase.TimeToCatch, t, 0.2f);
            // a brief stop (less than the pull-over time) doesn't count
            c.Start();
            c.Tick(0.5f, true);
            Assert.AreEqual(PoliceChase.Outcome.Chasing, c.Tick(0.1f, false));
        }
    }
}
