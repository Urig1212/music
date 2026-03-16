namespace SongSurvival.Core
{
    public interface IAudioAnalysisService
    {
        AudioFeatureFrame CurrentFrame { get; }

        void Tick(float deltaTime);
        CalibrationResult BuildCalibrationResult();
        void ResetCalibrationWindow();
    }
}
