using UnityEngine;

namespace SongSurvival.Core
{
    public static class GameConstants
    {
        public const int SampleRate = 44100;
        public const int ClipLengthSeconds = 2;
        public const float CalibrationDurationSeconds = 3f;
        public const float PlayerMinX = -4.1f;
        public const float PlayerMaxX = 4.1f;
        public const float PlayerY = -6.2f;
        public const float TargetFrameRate = 60f;

        public static readonly Color CameraBackground = new Color(0.08f, 0.09f, 0.16f);
        public static readonly Color BackgroundCalm = new Color(0.12f, 0.15f, 0.20f, 0.96f);
        public static readonly Color BackgroundLoud = new Color(0.23f, 0.13f, 0.21f, 0.98f);
        public static readonly Color PlayerColor = new Color(1f, 0.72f, 0.77f, 1f);
        public static readonly Color ButtonColor = new Color(0.97f, 0.60f, 0.40f, 0.94f);
        public static readonly Color ButtonTextColor = new Color(0.18f, 0.12f, 0.15f, 1f);
    }
}
