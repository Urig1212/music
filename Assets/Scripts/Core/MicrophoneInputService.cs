using UnityEngine;

namespace SongSurvival.Core
{
    public sealed class MicrophoneInputService : IAudioInputService
    {
        private AudioClip microphoneClip;
        private string activeDevice = string.Empty;
        private float[] wrapBuffer = new float[0];
        private float[] headBuffer = new float[0];

        public bool HasPermission => Application.HasUserAuthorization(UserAuthorization.Microphone);
        public bool IsCapturing { get; private set; }
        public bool IsInputAvailable => HasDevices && HasPermission;
        public bool HasDevices => Microphone.devices != null && Microphone.devices.Length > 0;
        public string ActiveDevice => activeDevice;

        public void RefreshDevices()
        {
            activeDevice = HasDevices ? Microphone.devices[0] : string.Empty;
        }

        public void StartCapture(int sampleRate, int clipLengthSeconds)
        {
            if (IsCapturing)
            {
                return;
            }

            RefreshDevices();
            if (!IsInputAvailable)
            {
                return;
            }

            microphoneClip = Microphone.Start(activeDevice, true, clipLengthSeconds, sampleRate);
            IsCapturing = microphoneClip != null;
        }

        public void StopCapture()
        {
            if (!IsCapturing)
            {
                return;
            }

            if (!string.IsNullOrEmpty(activeDevice))
            {
                Microphone.End(activeDevice);
            }

            microphoneClip = null;
            IsCapturing = false;
        }

        public bool TryFillLatestWindow(float[] destination)
        {
            if (!IsCapturing || microphoneClip == null || destination == null || destination.Length == 0)
            {
                return false;
            }

            int clipSamples = microphoneClip.samples;
            int microphonePosition = Microphone.GetPosition(activeDevice);
            if (microphonePosition <= 0 || clipSamples <= destination.Length)
            {
                return false;
            }

            int startSample = microphonePosition - destination.Length;
            if (startSample < 0)
            {
                startSample += clipSamples;
            }

            if (startSample + destination.Length <= clipSamples)
            {
                microphoneClip.GetData(destination, startSample);
                return true;
            }

            int firstSegment = clipSamples - startSample;
            EnsureBufferCapacity(ref wrapBuffer, firstSegment);
            microphoneClip.GetData(wrapBuffer, startSample);
            for (int i = 0; i < firstSegment; i++)
            {
                destination[i] = wrapBuffer[i];
            }

            int secondSegment = destination.Length - firstSegment;
            EnsureBufferCapacity(ref headBuffer, secondSegment);
            microphoneClip.GetData(headBuffer, 0);
            for (int i = 0; i < secondSegment; i++)
            {
                destination[firstSegment + i] = headBuffer[i];
            }

            return true;
        }

        private static void EnsureBufferCapacity(ref float[] buffer, int length)
        {
            if (buffer.Length < length)
            {
                buffer = new float[length];
            }
        }
    }
}
