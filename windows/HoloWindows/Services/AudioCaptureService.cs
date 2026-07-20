using NAudio.Wave;
using Holo.DSP;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Windows-native audio capture service.
/// Maps to macOS HoloApp.AudioCaptureService / AVAudioEngine.
/// Uses NAudio (WaveInEvent) for microphone input.
/// </summary>
public sealed class AudioCaptureService : IDisposable
{
    private WaveInEvent? _waveIn;
    private StreamingTapDetector? _detector;

    public event Action<DetectedTap>? TapDetected;
    public event Action<float>? LevelUpdated;
    public event Action<string>? ErrorOccurred;

    public bool IsListening { get; private set; }
    public int SampleRate { get; private set; }
    public int ChannelCount { get; private set; }
    public float LiveLevel { get; private set; }
    public string? DeviceName { get; private set; }

    public AudioCaptureService()
    {
        SampleRate = 48000;
        ChannelCount = 1;
    }

    /// <summary>
    /// Start capturing from the default microphone.
    /// Enumerates available devices and fails gracefully if none found.
    /// </summary>
    public bool Start()
    {
        if (IsListening) return true;

        try
        {
            // Enumerate available input devices
            int deviceCount = WaveInEvent.DeviceCount;
            if (deviceCount == 0)
            {
                ErrorOccurred?.Invoke("No microphone input devices found. Connect a microphone and check your privacy settings.");
                return false;
            }

            // Use the default (device 0) if available
            int deviceId = 0;
            if (deviceId >= deviceCount)
            {
                ErrorOccurred?.Invoke("Default microphone device is not available.");
                return false;
            }

            var capabilities = WaveInEvent.GetCapabilities(deviceId);
            DeviceName = capabilities.ProductName;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceId,
                WaveFormat = new WaveFormat(SampleRate, 16, ChannelCount),
                BufferMilliseconds = 30
            };

            _detector = new StreamingTapDetector(SampleRate, ChannelCount);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();

            IsListening = true;
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to start audio capture: {ex.Message}");
            Cleanup();
            return false;
        }
    }

    public void Stop()
    {
        if (!IsListening) return;
        Cleanup();
        IsListening = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            // Convert 16-bit PCM to float samples
            int sampleCount = e.BytesRecorded / 2;
            var buffer = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short sample = (short)(e.Buffer[i * 2] | (e.Buffer[i * 2 + 1] << 8));
                buffer[i] = sample / 32768f;
            }

            // Compute RMS level for UI meter
            double sumSq = 0;
            for (int i = 0; i < buffer.Length; i++)
                sumSq += buffer[i] * buffer[i];
            LiveLevel = (float)Math.Sqrt(sumSq / buffer.Length) * 10;
            LevelUpdated?.Invoke(LiveLevel);

            // Feed through the tap detector
            var taps = _detector?.Process([buffer]) ?? [];
            foreach (var tap in taps)
                TapDetected?.Invoke(tap);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Audio processing error: {ex.Message}");
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsListening = false;
        if (e.Exception != null)
            ErrorOccurred?.Invoke($"Recording stopped due to error: {e.Exception.Message}");
    }

    private void Cleanup()
    {
        if (_waveIn != null)
        {
            try { _waveIn.StopRecording(); } catch { }
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }
        _detector = null;
    }

    public void Dispose() => Cleanup();
}
