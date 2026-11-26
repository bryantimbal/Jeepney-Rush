using System.Collections.Generic;
using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class CareerTests
    {
        private static RunSummary Run(CareerShift s, float dist, int coins, int goalValue = 0)
        {
            var r = new RunSummary { Career = s.Id, Environment = s.Env, Distance = dist, Coins = coins, Score = 100 };
            switch (s.Goal)
            {
                case ShiftGoal.Deliveries: r.Deliveries = goalValue; r.Passengers = goalValue; break;
                case ShiftGoal.MaxHits: r.Hits = goalValue; break;
                case ShiftGoal.NearMisses: r.NearMisses = goalValue; break;
                case ShiftGoal.Overtakes: r.Overtakes = goalValue; break;
                case ShiftGoal.Streak: r.BestStreak = goalValue; break;
                case ShiftGoal.CleanDistance: r.CleanDistance = goalValue; break;
                case ShiftGoal.Passengers: r.Passengers = goalValue; break;
                default: r.SmoothHumps = goalValue; break;
            }
            return r;
        }

        [Test]
        public void ShiftsAreWellFormed()
        {
            var ids = new HashSet<string>();
            int prevChapter = 0;
            foreach (var s in Career.Shifts)
            {
                Assert.IsTrue(ids.Add(s.Id), "unique id " + s.Id);
                Assert.That(s.Id.Length, Is.LessThanOrEqualTo(16));
                Assert.That(s.Chapter, Is.InRange(prevChapter, prevChapter + 1), "chapters in order");
                prevChapter = s.Chapter;
                Assert.Greater(s.Length, 0);
                Assert.Greater(s.Boundary, 0);
                // the boundary must be collectable on that distance (the server clamps coins to this bound)
                Assert.Less(s.Boundary, RunBounds.MaxCoins(s.Length, 0), s.Id);
            }
            Assert.AreEqual(Career.Licenses.Length - 1, prevChapter, "one chapter per license");
            for (int i = 1; i < Career.Licenses.Length; i++)
            {
                Assert.Greater(Career.Licenses[i].StarsNeeded, Career.Licenses[i - 1].StarsNeeded);
                // stars of the chapters before must be enough to unlock it
                int before = 0;
                foreach (var s in Career.Shifts) if (s.Chapter < i) before += 3;
                Assert.LessOrEqual(Career.Licenses[i].StarsNeeded, before, Career.Licenses[i].Name);
            }
        }

        [Test]
        public void StarsNeedTheTerminal()
        {
            var s = Career.Shifts[0];
            Assert.AreEqual(0, Career.Evaluate(s, Run(s, s.Length - 10, 9999, 99)), "crashed before the terminal");
            Assert.AreEqual(1, Career.Evaluate(s, Run(s, s.Length, 0, 0)));
            Assert.AreEqual(3, Career.Evaluate(s, Run(s, s.Length, s.Boundary, 0)));
            Assert.AreEqual(7, Career.Evaluate(s, Run(s, s.Length, s.Boundary, s.Target)));
            var wrongRoute = Run(s, s.Length, s.Boundary, s.Target);
            wrongRoute.Environment = s.Env == EnvironmentKind.Manila ? EnvironmentKind.Beach : EnvironmentKind.Manila;
            Assert.AreEqual(0, Career.Evaluate(s, wrongRoute));
        }

        [Test]
        public void MaxHitsGoalIsAnUpperBound()
        {
            var s = Career.Get("c1s2");
            Assert.AreEqual(ShiftGoal.MaxHits, s.Goal);
            Assert.AreEqual(7, Career.Evaluate(s, Run(s, s.Length, s.Boundary, s.Target)));
            Assert.AreEqual(3, Career.Evaluate(s, Run(s, s.Length, s.Boundary, s.Target + 1)));
        }

        [Test]
        public void UnlockOrderAndLicenses()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            Assert.IsTrue(Career.IsUnlocked(0, d.careerMasks));
            Assert.IsFalse(Career.IsUnlocked(1, d.careerMasks), "needs a star on the shift before");
            Assert.IsFalse(Career.IsUnlocked(4, d.careerMasks), "next chapter needs the license");
            var s0 = Career.Shifts[0];
            var res = Career.Apply(d, Run(s0, s0.Length, 0));
            Assert.AreEqual(1, res.Stars);
            Assert.AreEqual(Career.StarReward(0), res.StarCoins);
            Assert.IsTrue(Career.IsUnlocked(1, d.careerMasks));
            // replaying with the same result pays nothing more
            int coins = d.coins;
            res = Career.Apply(d, Run(s0, s0.Length, 0));
            Assert.AreEqual(0, res.NewMask);
            Assert.AreEqual(coins, d.coins);
            // three stars on the first two shifts = 6 stars = non-pro license
            res = Career.Apply(d, Run(s0, s0.Length, s0.Boundary, s0.Target));
            Assert.AreEqual(6, res.NewMask, "only the two new stars");
            var s1 = Career.Shifts[1];
            res = Career.Apply(d, Run(s1, s1.Length, s1.Boundary, s1.Target));
            Assert.AreEqual(1, res.NewRank);
            Assert.AreEqual(Career.Licenses[1].RewardCoins, res.RankCoins);
            Assert.AreEqual(Career.Licenses[1].RewardGems, d.gems);
            Assert.AreEqual(1, d.careerRank);
            Assert.IsTrue(Career.IsUnlocked(4, d.careerMasks), "chapter 2 opens with the license");
        }

        [Test]
        public void LockedShiftRecordsNothing()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            var s = Career.Shifts[5];
            var res = Career.Apply(d, Run(s, s.Length, s.Boundary, s.Target));
            Assert.AreEqual(0, res.Mask);
            Assert.AreEqual(0, d.coins);
        }

        [Test]
        public void FreeRideIsNotCareer()
        {
            var d = SaveValidation.Sanitize(new SaveData());
            var rewards = Progress.Apply(d, new RunSummary { Distance = 500, Coins = 10 });
            Assert.IsNull(rewards.Career);
        }

        [Test]
        public void ReportCarriesTheShift()
        {
            var s = Career.Shifts[0];
            var rep = RunReport.From(Run(s, s.Length, 10), 60, 1, false, 0, "pilipinas");
            Assert.AreEqual("c1s1", rep.career);
            Assert.AreEqual("", RunReport.From(new RunSummary(), 60, 1, false, 0, "pilipinas").career);
        }
    }
}
