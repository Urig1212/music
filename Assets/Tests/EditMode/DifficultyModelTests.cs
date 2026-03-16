using NUnit.Framework;
using SongSurvival.Core;

namespace SongSurvival.Tests
{
    public sealed class DifficultyModelTests
    {
        [Test]
        public void Evaluate_IncreasesPressureForHotAudioFrame()
        {
            DifficultyModel model = new DifficultyModel();

            DifficultySnapshot result = model.Evaluate(
                new AudioFeatureFrame
                {
                    Energy = 0.9f,
                    BassEnergy = 0.85f,
                    Brightness = 0.7f,
                    SpectralFlux = 0.65f,
                    PeakDetected = false
                },
                40f,
                0.016f);

            Assert.That(result.WorldSpeed, Is.GreaterThan(5f));
            Assert.That(result.Danger, Is.GreaterThan(0.5f));
            Assert.That(result.BassPressure, Is.GreaterThan(0.6f));
            Assert.That(result.SparkPressure, Is.GreaterThan(0.5f));
        }

        [Test]
        public void Evaluate_RespectsShockCooldown()
        {
            DifficultyModel model = new DifficultyModel();
            AudioFeatureFrame peakFrame = new AudioFeatureFrame { PeakDetected = true, Energy = 0.7f, Brightness = 0.4f };

            DifficultySnapshot first = model.Evaluate(peakFrame, 10f, 0.016f);
            DifficultySnapshot immediateSecond = model.Evaluate(peakFrame, 10.1f, 0.016f);
            DifficultySnapshot afterCooldown = model.Evaluate(peakFrame, 12.5f, 2.0f);

            Assert.That(first.ShockReady, Is.True);
            Assert.That(immediateSecond.ShockReady, Is.False);
            Assert.That(afterCooldown.ShockReady, Is.True);
        }
    }
}
