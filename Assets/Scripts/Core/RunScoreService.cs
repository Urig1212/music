using UnityEngine;

namespace SongSurvival.Core
{
    public sealed class RunScoreService
    {
        private const string BestScoreKey = "song_survival.best_score";

        public float CurrentScore { get; private set; }
        public float BestScore => PlayerPrefs.GetFloat(BestScoreKey, 0f);

        public void ResetRun()
        {
            CurrentScore = 0f;
        }

        public void Tick(float deltaTime, float danger)
        {
            CurrentScore += deltaTime * (1f + (danger * 0.35f));
        }

        public void CommitIfBest()
        {
            if (CurrentScore <= BestScore)
            {
                return;
            }

            PlayerPrefs.SetFloat(BestScoreKey, CurrentScore);
            PlayerPrefs.Save();
        }
    }
}
