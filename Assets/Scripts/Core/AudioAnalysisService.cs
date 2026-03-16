using System.Collections.Generic;
using UnityEngine;

namespace SongSurvival.Core
{
    public sealed class AudioAnalysisService : IAudioAnalysisService
    {
        private readonly IAudioInputService audioInputService;
        private readonly float[] sampleWindow;
        private readonly Queue<AudioFeatureFrame> calibrationFrames = new Queue<AudioFeatureFrame>();
        private readonly int calibrationCapacity;
        private float smoothedEnergy;
        private float smoothedBass;
        private float smoothedBrightness;
        private float rollingNoiseFloor;
        private float peakCooldown;
        private AudioFeatureFrame currentFrame;

        public AudioAnalysisService(IAudioInputService audioInputService, int windowSize = 1024, int calibrationCapacity = 180)
        {
            this.audioInputService = audioInputService;
            this.calibrationCapacity = calibrationCapacity;
            sampleWindow = new float[windowSize];
            currentFrame = AudioFeatureFrame.Silent;
        }

        public AudioFeatureFrame CurrentFrame => currentFrame;

        public void Tick(float deltaTime)
        {
            peakCooldown = Mathf.Max(0f, peakCooldown - deltaTime);

            if (!audioInputService.TryFillLatestWindow(sampleWindow))
            {
                currentFrame = AudioFeatureFrame.Silent;
                PushCalibrationFrame(currentFrame);
                return;
            }

            float sumSquares = 0f;
            float lowBand = 0f;
            float highBand = 0f;
            float positiveDelta = 0f;
            float runningLow = 0f;
            float previousAbs = Mathf.Abs(sampleWindow[0]);
            const float lowPassAmount = 0.08f;

            for (int i = 0; i < sampleWindow.Length; i++)
            {
                float sample = sampleWindow[i];
                float absSample = Mathf.Abs(sample);
                sumSquares += sample * sample;

                runningLow = Mathf.Lerp(runningLow, sample, lowPassAmount);
                lowBand += Mathf.Abs(runningLow);
                highBand += Mathf.Abs(sample - runningLow);

                float delta = absSample - previousAbs;
                if (delta > 0f)
                {
                    positiveDelta += delta;
                }

                previousAbs = absSample;
            }

            float rawEnergy = Mathf.Sqrt(sumSquares / sampleWindow.Length);
            float rawBass = lowBand / sampleWindow.Length;
            float rawBrightness = highBand / sampleWindow.Length;
            float rawFlux = positiveDelta / sampleWindow.Length;

            smoothedEnergy = Mathf.Lerp(smoothedEnergy, rawEnergy, 0.18f);
            smoothedBass = Mathf.Lerp(smoothedBass, rawBass, 0.18f);
            smoothedBrightness = Mathf.Lerp(smoothedBrightness, rawBrightness, 0.18f);
            rollingNoiseFloor = Mathf.Lerp(rollingNoiseFloor, Mathf.Min(rollingNoiseFloor <= 0f ? rawEnergy : rollingNoiseFloor, rawEnergy), 0.02f);

            float stability = Mathf.Clamp01((smoothedEnergy - rollingNoiseFloor) * 12f);
            float confidence = Mathf.Clamp01((smoothedEnergy * 9f) + (rawFlux * 20f) + (stability * 0.4f));

            bool peakDetected = peakCooldown <= 0f && rawEnergy > Mathf.Max(rollingNoiseFloor * 2.4f, 0.05f) && rawFlux > 0.009f;
            if (peakDetected)
            {
                peakCooldown = 0.3f;
            }

            currentFrame = new AudioFeatureFrame
            {
                Energy = Mathf.Clamp01(smoothedEnergy * 10f),
                BassEnergy = Mathf.Clamp01(smoothedBass * 14f),
                Brightness = Mathf.Clamp01(smoothedBrightness * 18f),
                SpectralFlux = Mathf.Clamp01(rawFlux * 42f),
                PeakDetected = peakDetected,
                Confidence = confidence,
                NoiseFloor = rollingNoiseFloor,
                Stability = stability
            };

            PushCalibrationFrame(currentFrame);
        }

        public void ResetCalibrationWindow()
        {
            calibrationFrames.Clear();
        }

        public CalibrationResult BuildCalibrationResult()
        {
            if (calibrationFrames.Count == 0)
            {
                return new CalibrationResult
                {
                    Quality = CalibrationQuality.FallbackRequired,
                    Message = "No microphone signal found."
                };
            }

            float totalEnergy = 0f;
            float totalConfidence = 0f;
            float peakCount = 0f;

            foreach (AudioFeatureFrame frame in calibrationFrames)
            {
                totalEnergy += frame.Energy;
                totalConfidence += frame.Confidence;
                if (frame.PeakDetected)
                {
                    peakCount += 1f;
                }
            }

            float averageEnergy = totalEnergy / calibrationFrames.Count;
            float averageConfidence = totalConfidence / calibrationFrames.Count;
            float peakRate = peakCount / calibrationFrames.Count;

            CalibrationQuality quality;
            string message;
            if (averageEnergy >= 0.16f && averageConfidence >= 0.34f)
            {
                quality = CalibrationQuality.Usable;
                message = "Audio looks strong. Start surviving.";
            }
            else if (averageEnergy >= 0.08f && averageConfidence >= 0.18f)
            {
                quality = CalibrationQuality.WeakPlayable;
                message = "Signal is weak but playable. Raise volume if the run feels flat.";
            }
            else
            {
                quality = CalibrationQuality.FallbackRequired;
                message = "Signal is too weak. Raise volume, move closer, or use an external speaker.";
            }

            return new CalibrationResult
            {
                Quality = quality,
                AverageEnergy = averageEnergy,
                AverageConfidence = averageConfidence,
                PeakRate = peakRate,
                Message = message
            };
        }

        private void PushCalibrationFrame(AudioFeatureFrame frame)
        {
            calibrationFrames.Enqueue(frame);
            while (calibrationFrames.Count > calibrationCapacity)
            {
                calibrationFrames.Dequeue();
            }
        }
    }
}
