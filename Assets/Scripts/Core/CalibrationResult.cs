namespace SongSurvival.Core
{
    public enum CalibrationQuality
    {
        Usable,
        WeakPlayable,
        FallbackRequired
    }

    public struct CalibrationResult
    {
        public CalibrationQuality Quality;
        public float AverageEnergy;
        public float AverageConfidence;
        public float PeakRate;
        public string Message;
    }
}
