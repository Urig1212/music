using NUnit.Framework;
using SongSurvival.Core;

namespace SongSurvival.Tests
{
    public sealed class AudioAnalysisServiceTests
    {
        [Test]
        public void Tick_ProducesSilentFrameWhenInputUnavailable()
        {
            FakeAudioInputService fakeInput = new FakeAudioInputService(false, null);
            AudioAnalysisService service = new AudioAnalysisService(fakeInput, 16, 8);

            service.Tick(0.016f);

            Assert.That(service.CurrentFrame.Energy, Is.EqualTo(0f));
            Assert.That(service.CurrentFrame.Confidence, Is.EqualTo(0f));
        }

        [Test]
        public void CalibrationResult_ClassifiesStrongSignalAsUsable()
        {
            float[] hotSignal = new float[32];
            for (int i = 0; i < hotSignal.Length; i++)
            {
                hotSignal[i] = (i % 2 == 0) ? 0.45f : -0.45f;
            }

            FakeAudioInputService fakeInput = new FakeAudioInputService(true, hotSignal);
            AudioAnalysisService service = new AudioAnalysisService(fakeInput, hotSignal.Length, 12);

            for (int i = 0; i < 12; i++)
            {
                service.Tick(0.016f);
            }

            CalibrationResult result = service.BuildCalibrationResult();

            Assert.That(result.Quality, Is.EqualTo(CalibrationQuality.Usable));
            Assert.That(result.AverageEnergy, Is.GreaterThan(0.16f));
        }

        private sealed class FakeAudioInputService : IAudioInputService
        {
            private readonly bool canRead;
            private readonly float[] window;

            public FakeAudioInputService(bool canRead, float[] window)
            {
                this.canRead = canRead;
                this.window = window;
            }

            public bool HasPermission => true;
            public bool IsCapturing => canRead;
            public bool IsInputAvailable => canRead;
            public bool HasDevices => true;
            public string ActiveDevice => "Fake";

            public void RefreshDevices()
            {
            }

            public bool TryFillLatestWindow(float[] destination)
            {
                if (!canRead || window == null || destination.Length != window.Length)
                {
                    return false;
                }

                for (int i = 0; i < window.Length; i++)
                {
                    destination[i] = window[i];
                }

                return true;
            }

            public void StartCapture(int sampleRate, int clipLengthSeconds)
            {
            }

            public void StopCapture()
            {
            }
        }
    }
}
