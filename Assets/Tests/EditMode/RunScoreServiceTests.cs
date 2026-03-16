using NUnit.Framework;
using SongSurvival.Core;
using UnityEngine;

namespace SongSurvival.Tests
{
    public sealed class RunScoreServiceTests
    {
        private const string BestScoreKey = "song_survival.best_score";

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(BestScoreKey);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(BestScoreKey);
        }

        [Test]
        public void Tick_AddsDangerWeightedScore()
        {
            RunScoreService service = new RunScoreService();

            service.ResetRun();
            service.Tick(2f, 1f);

            Assert.That(service.CurrentScore, Is.EqualTo(2.7f).Within(0.001f));
        }

        [Test]
        public void CommitIfBest_PersistsHigherScoreOnly()
        {
            RunScoreService firstRun = new RunScoreService();
            firstRun.ResetRun();
            firstRun.Tick(3f, 0f);
            firstRun.CommitIfBest();

            RunScoreService secondRun = new RunScoreService();
            secondRun.ResetRun();
            secondRun.Tick(1f, 0f);
            secondRun.CommitIfBest();

            Assert.That(secondRun.BestScore, Is.EqualTo(3f).Within(0.001f));
        }
    }
}
