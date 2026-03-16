namespace SongSurvival.Core
{
    public interface IAudioInputService
    {
        bool HasPermission { get; }
        bool IsCapturing { get; }
        bool IsInputAvailable { get; }
        bool HasDevices { get; }
        string ActiveDevice { get; }

        void RefreshDevices();
        bool TryFillLatestWindow(float[] destination);
        void StartCapture(int sampleRate, int clipLengthSeconds);
        void StopCapture();
    }
}
