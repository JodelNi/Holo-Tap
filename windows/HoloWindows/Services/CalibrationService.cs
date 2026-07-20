using Holo.DSP;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Manages the guided calibration workflow:
/// 1. User starts calibration → session created
/// 2. Each zone is armed in sequence (LR → LF → RR → RF)
/// 3. 10 quality-checked taps collected per zone
/// 4. Classifier trained and profile saved
/// </summary>
public sealed class CalibrationService
{
    private readonly AudioCaptureService _audioCapture;
    private readonly ProfileService _profileService;
    private TapFeatureExtractor? _featureExtractor;

    // Stored delegate references for clean unsubscription (fixes -= null! bug)
    private Action<string>? _errorHandler;
    private bool _isSubscribed;

    public event Action<string>? ZoneChanged;
    public event Action<int, int>? ProgressUpdated;
    public event Action<string>? TapFeedback;
    public event Action<string>? GuidanceMessage;
    public event Action<HoloProfile>? CalibrationComplete;
    public event Action<string>? ErrorOccurred;

    public CalibrationSession? Session { get; private set; }
    public bool IsActive => Session != null;

    public CalibrationService(AudioCaptureService audioCapture, ProfileService profileService)
    {
        _audioCapture = audioCapture;
        _profileService = profileService;
    }

    public void Start(CalibrationDraft? draft = null)
    {
        // Guard: prevent double-subscription from repeated Start() calls
        if (IsActive)
        {
            ErrorOccurred?.Invoke("Calibration is already in progress.");
            return;
        }

        Session = new CalibrationSession(draft);
        _featureExtractor = new TapFeatureExtractor(
            _audioCapture.SampleRate,
            Session.Draft.Strategy);

        // Store delegate reference for clean unsubscription later
        _errorHandler = e => ErrorOccurred?.Invoke(e);
        _audioCapture.TapDetected += OnTapDetected;
        _audioCapture.ErrorOccurred += _errorHandler;
        _isSubscribed = true;

        ArmCurrentZone();
    }

    public void Cancel()
    {
        Unsubscribe();
        Session = null;
        _featureExtractor = null;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed) return;
        _audioCapture.TapDetected -= OnTapDetected;
        if (_errorHandler != null)
            _audioCapture.ErrorOccurred -= _errorHandler;
        _errorHandler = null;
        _isSubscribed = false;
    }

    public void ToggleArm()
    {
        if (Session == null) return;
        Session.IsArmed = !Session.IsArmed;
        if (Session.IsArmed)
        {
            GuidanceMessage?.Invoke("Ready! Tap the highlighted zone.");
        }
        else
        {
            GuidanceMessage?.Invoke("Disarmed. Tap to arm again.");
        }
    }

    private void ArmCurrentZone()
    {
        if (Session == null) return;
        var zone = Session.CurrentZone;
        if (zone == null)
        {
            FinishCalibration();
            return;
        }
        Session.IsArmed = false;
        ZoneChanged?.Invoke(zone.Value.DisplayName());
        GuidanceMessage?.Invoke($"Arm to start collecting taps for {zone.Value.DisplayName()}");
        ProgressUpdated?.Invoke(Session.PositiveSamples.Count, Session.TotalRequired);
    }

    private void OnTapDetected(DetectedTap tap)
    {
        if (Session == null || _featureExtractor == null) return;

        if (!Session.IsArmed)
        {
            if (Session.CurrentZone != null)
            {
                Session.IsArmed = true;
                GuidanceMessage?.Invoke("Armed! Keep tapping the highlighted zone.");
            }
            return;
        }

        var feature = _featureExtractor.Extract(tap);
        var quality = feature.Quality!;
        var currentZone = Session.CurrentZone;

        if (currentZone == null)
        {
            FinishCalibration();
            return;
        }

        // Quality gate (matches macOS GuidedCaptureQuality)
        if (quality.ClippingFraction > 0.20)
        {
            TapFeedback?.Invoke("⚠️ Clipped — use a lighter tap");
            return;
        }
        if (quality.PeakAmplitude < 0.003)
        {
            TapFeedback?.Invoke("⚠️ Too weak — tap more firmly");
            return;
        }
        if (quality.SignalToNoiseDB < 7.0)
        {
            TapFeedback?.Invoke("⚠️ Too noisy — wait for quiet");
            return;
        }

        // Accept the tap
        Session.PositiveSamples.Add(new LabeledTap
        {
            Id = Guid.NewGuid(),
            Zone = currentZone,
            Feature = feature
        });

        int collected = Session.Count(currentZone.Value);
        TapFeedback?.Invoke($"✅ {currentZone.Value.DisplayName()}: {collected}/{Session.TargetPerZone}");
        ProgressUpdated?.Invoke(Session.PositiveSamples.Count, Session.TotalRequired);

        if (collected >= Session.TargetPerZone)
        {
            GuidanceMessage?.Invoke($"✅ {currentZone.Value.DisplayName()} complete!");
            ArmCurrentZone();
        }
    }

    private void FinishCalibration()
    {
        if (Session == null) return;

        try
        {
            Unsubscribe();

            var positives = Session.PositiveSamples;
            if (positives.Count < 4)
            {
                ErrorOccurred?.Invoke("Not enough taps collected. Need at least 4.");
                Session = null;
                return;
            }

            var classifier = TapClassifier.Train(positives, Session.NegativeSamples);

            var profile = new HoloProfile
            {
                Id = Guid.NewGuid(),
                Name = Session.Draft.Name,
                SurfaceDescription = Session.Draft.SurfaceDescription,
                LaptopPositionNote = Session.Draft.LaptopPositionNote,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Classifier = classifier,
                Calibration = new CalibrationSummary
                {
                    SampleCount = positives.Count,
                    SamplesPerZone = Enum.GetValues<DeskZone>()
                        .Select(z => positives.Count(p => p.Zone == z))
                        .ToList(),
                    LeaveOneOutAccuracy = null,
                    CapturedAt = DateTime.UtcNow
                },
                Zones = Enum.GetValues<DeskZone>()
                    .Select(z => new ZoneConfiguration { Zone = z })
                    .ToList()
            };

            _profileService.Save(profile);
            CalibrationComplete?.Invoke(profile);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Calibration failed: {ex.Message}");
        }
        finally
        {
            Session = null;
            _featureExtractor = null;
        }
    }
}
