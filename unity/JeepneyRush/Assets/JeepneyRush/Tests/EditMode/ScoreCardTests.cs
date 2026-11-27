using JeepneyRush.Logic;
using NUnit.Framework;

namespace JeepneyRush.Tests
{
    public class ScoreCardTests
    {
        [Test]
        public void TextsFitEachRun()
        {
            var free = new ScoreCardInfo { Score = 12480, Distance = 2340, Route = EnvironmentKind.Edsa, Jeepney = "pilipinas", NewBest = true };
            Assert.AreEqual("BAGONG RECORD!", ScoreCard.Headline(free));
            StringAssert.Contains("EDSA", ScoreCard.Detail(free));
            string text = ScoreCard.ShareText(free, "ph.jeepneyrush.game");
            StringAssert.Contains("12,480", text);
            StringAssert.Contains("2.3 km", text);
            StringAssert.Contains("play.google.com/store/apps/details?id=ph.jeepneyrush.game", text);

            var shift = new ScoreCardInfo { Score = 900, Distance = 820, Shift = "Unang Biyahe", Stars = 3, ShiftDone = true };
            Assert.AreEqual("BIYAHE COMPLETE!", ScoreCard.Headline(shift));
            StringAssert.Contains("3/3 stars", ScoreCard.ShareText(shift, "x"));

            var ghost = new ScoreCardInfo { Score = 5000, Distance = 1200, Ghost = "Driver-K7QP", BeatGhost = true, Mode = GameMode.Ghost };
            Assert.AreEqual("PANALO SA GHOST RACE!", ScoreCard.Headline(ghost));
            Assert.AreEqual("SINO ANG SUSUNOD?", ScoreCard.Challenge(ghost));
            StringAssert.Contains("tinalo ko si Driver-K7QP", ScoreCard.ShareText(ghost, "x"));

            Assert.AreEqual("RUSH HOUR SURVIVOR!", ScoreCard.Headline(new ScoreCardInfo { Mode = GameMode.RushHour }));
            StringAssert.Contains("450 m", ScoreCard.ShareText(new ScoreCardInfo { Distance = 450 }, "x"));
        }
    }
}
