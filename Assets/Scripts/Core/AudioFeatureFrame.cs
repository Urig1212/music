using UnityEngine;

namespace SongSurvival.Core
{
    public struct AudioFeatureFrame
    {
        public float Energy;
        public float BassEnergy;
        public float Brightness;
        public float SpectralFlux;
        public bool PeakDetected;
        public float Confidence;
        public float NoiseFloor;
        public float Stability;

        public static AudioFeatureFrame Silent => new AudioFeatureFrame
        {
            Energy = 0f,
            BassEnergy = 0f,
            Brightness = 0f,
            SpectralFlux = 0f,
            PeakDetected = false,
            Confidence = 0f,
            NoiseFloor = 0f,
            Stability = 0f
        };
    }
}
