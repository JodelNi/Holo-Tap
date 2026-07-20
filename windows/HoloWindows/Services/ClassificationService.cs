using Holo.DSP;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Coordinates audio capture → tap detection → feature extraction → classification → action dispatch.
/// </summary>
public sealed class ClassificationService : IDisposable
{
    private readonly AudioCaptureService _audioCapture;
    private readonly ActionDispatcherService _actionDispatcher;
    private TapFeatureExtractor? _featureExtractor;
    private TrainedTapClassifier? _classifier;

    public event Action<ClassificationDecision>? TapClassified;
    public event Action<DetectedTap>? TapDetected;
    public event Action<string>? ErrorOccurred;
    public event Action<float>? LevelUpdated;

    public TrainedTapClassifier? ActiveProfile => _classifier;
    public bool IsListening => _audioCapture.IsListening;

    public ClassificationService(AudioCaptureService audioCapture, ActionDispatcherService actionDispatcher)
    {
        _audioCapture = audioCapture;
        _actionDispatcher = actionDispatcher;

        _audioCapture.TapDetected += OnTapDetected;
        _audioCapture.LevelUpdated += l => LevelUpdated?.Invoke(l);
        _audioCapture.ErrorOccurred += e => ErrorOccurred?.Invoke(e);
    }

    public void SetProfile(HoloProfile profile)
    {
        _classifier = profile.Classifier;
        _featureExtractor = new TapFeatureExtractor(
            _audioCapture.SampleRate,
            profile.Classifier?.Strategy ?? SensingStrategy.Passive);
    }

    public void ClearProfile()
    {
        _classifier = null;
        _featureExtractor = null;
    }

    public bool StartListening() => _audioCapture.Start();

    public void StopListening() => _audioCapture.Stop();

    public bool ExecuteAction(ClassificationDecision decision)
    {
        // TODO: Look up zone action from active profile
        return false;
    }

    private void OnTapDetected(DetectedTap tap)
    {
        TapDetected?.Invoke(tap);

        if (_featureExtractor == null || _classifier == null)
            return;

        var feature = _featureExtractor.Extract(tap);
        var decision = TapClassifier.Predict(feature, _classifier);

        TapClassified?.Invoke(decision);

        if (decision.WasAccepted && decision.Zone.HasValue)
        {
            // TODO: Execute zone action
        }
    }

    public void Dispose() => _audioCapture.Dispose();
}
