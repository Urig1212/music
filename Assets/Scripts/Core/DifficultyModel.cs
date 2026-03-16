using UnityEngine;

namespace SongSurvival.Core
{
    public sealed class DifficultyModel
    {
        private float shockCooldown;

        public DifficultySnapshot Evaluate(AudioFeatureFrame frame, float elapsedSeconds, float deltaTime)
        {
            shockCooldown = Mathf.Max(0f, shockCooldown - deltaTime);

            float timeRamp = Mathf.Clamp01(elapsedSeconds / 75f);
            float baseDanger = Mathf.Clamp01((frame.Energy * 0.55f) + (frame.Brightness * 0.15f) + (timeRamp * 0.35f));
            float bassPressure = Mathf.Clamp01((frame.BassEnergy * 0.75f) + (timeRamp * 0.2f));
            float sparkPressure = Mathf.Clamp01((frame.Brightness * 0.75f) + (frame.SpectralFlux * 0.4f) + (timeRamp * 0.15f));
            bool shockReady = frame.PeakDetected && shockCooldown <= 0f;

            if (shockReady)
            {
                shockCooldown = Mathf.Lerp(2.1f, 0.9f, timeRamp);
            }

            return new DifficultySnapshot
            {
                WorldSpeed = Mathf.Lerp(2.7f, 7.8f, baseDanger),
                Danger = baseDanger,
                BassPressure = bassPressure,
                SparkPressure = sparkPressure,
                ShockReady = shockReady
            };
        }
    }
}
