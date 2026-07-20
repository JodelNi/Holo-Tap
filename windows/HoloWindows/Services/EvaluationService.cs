using Holo.DSP;
using Holo.Models;

namespace Holo.Services;

/// <summary>
/// Manages a guided 60-tap evaluation session (15 taps × 4 zones).
/// Matches macOS HoloCore.EvaluationSession + ClassifierEvaluator.
/// </summary>
public sealed class EvaluationService
{
    private readonly ClassificationService _classificationService;

    public event Action<DeskZone>? ZoneChanged;
    public event Action<int, int>? ProgressUpdated; // collected, total
    public event Action<string>? TapFeedback;
    public event Action<string>? GuidanceMessage;
    public event Action<EvaluationReport>? EvaluationComplete;
    public event Action<string>? ErrorOccurred;

    public EvaluationSession? Session { get; private set; }
    public HoloProfile? ActiveProfile { get; private set; }
    public bool IsActive => Session != null;

    public EvaluationService(ClassificationService classificationService)
    {
        _classificationService = classificationService;
    }

    public void Start(HoloProfile profile)
    {
        if (profile.Classifier == null)
        {
            ErrorOccurred?.Invoke("Profile has no trained classifier. Complete calibration first.");
            return;
        }

        ActiveProfile = profile;
        _classificationService.SetProfile(profile);

        if (!_classificationService.IsListening)
            _classificationService.StartListening();

        Session = new EvaluationSession
        {
            StartedAt = DateTime.UtcNow,
            TargetPerZone = 15,
            Records = [],
            IsArmed = false
        };

        _classificationService.TapClassified += OnTapClassified;
        ArmCurrentZone();
    }

    public void Cancel()
    {
        if (Session != null)
        {
            _classificationService.TapClassified -= OnTapClassified;
            Session = null;
        }
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
            GuidanceMessage?.Invoke("Disarmed. Click Arm to continue.");
        }
    }

    private void ArmCurrentZone()
    {
        if (Session == null) return;

        var zone = Session.CurrentZone;
        if (zone == null)
        {
            FinishEvaluation();
            return;
        }

        Session.IsArmed = false;
        ZoneChanged?.Invoke(zone.Value);
        GuidanceMessage?.Invoke($"Arm to start tapping {zone.Value.DisplayName()} (15 required)");
        ProgressUpdated?.Invoke(Session.Records.Count, Session.TargetPerZone * 4);
    }

    private void OnTapClassified(ClassificationDecision decision)
    {
        if (Session == null || ActiveProfile == null) return;

        var currentZone = Session.CurrentZone;
        if (currentZone == null)
        {
            FinishEvaluation();
            return;
        }

        if (!Session.IsArmed)
        {
            // Auto-arm on first tap (matches calibration UX)
            if (currentZone != null)
            {
                Session.IsArmed = true;
                GuidanceMessage?.Invoke("Armed! Keep tapping.");
            }
            return;
        }

        // Record every tap — rejections count as incorrect (matches macOS behavior)
        var record = new EvaluationRecord
        {
            ExpectedZone = currentZone.Value,
            Decision = decision,
            CapturedAt = DateTime.UtcNow
        };
        Session.Records.Add(record);

        int zoneCount = Session.Records.Count(r => r.ExpectedZone == currentZone.Value);
        string status = decision.WasAccepted
            ? $"📍 {currentZone.Value.DisplayName()}: {zoneCount}/{Session.TargetPerZone}"
            : $"❌ Rejected: {decision.RejectionReason}";

        TapFeedback?.Invoke(status);
        ProgressUpdated?.Invoke(Session.Records.Count, Session.TargetPerZone * 4);

        if (zoneCount >= Session.TargetPerZone)
        {
            GuidanceMessage?.Invoke($"✅ {currentZone.Value.DisplayName()} complete! Moving to next zone.");
            ArmCurrentZone();
        }
    }

    private void FinishEvaluation()
    {
        if (Session == null || ActiveProfile == null) return;
        _classificationService.TapClassified -= OnTapClassified;

        try
        {
            var report = BuildReport(Session, ActiveProfile);
            EvaluationComplete?.Invoke(report);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Failed to build evaluation report: {ex.Message}");
        }
        finally
        {
            Session = null;
        }
    }

    private static EvaluationReport BuildReport(EvaluationSession session, HoloProfile profile)
    {
        var records = session.Records;
        int correct = records.Count(r =>
            r.Decision != null && r.Decision.WasAccepted && r.Decision.Zone == r.ExpectedZone);
        int rejected = records.Count(r =>
            r.Decision != null && !r.Decision.WasAccepted);
        int totalTaps = records.Count;

        var latencies = records
            .Where(r => r.Decision != null && r.Decision.ProcessingLatencyMs >= 0)
            .Select(r => r.Decision!.ProcessingLatencyMs)
            .OrderBy(v => v)
            .ToList();

        double medianLatency = latencies.Count > 0
            ? latencies.Count % 2 == 0
                ? (latencies[latencies.Count / 2 - 1] + latencies[latencies.Count / 2]) / 2
                : latencies[latencies.Count / 2]
            : 0;

        var zoneResults = new List<ZoneResult>();
        foreach (var zone in Enum.GetValues<DeskZone>())
        {
            var zoneRecords = records.Where(r => r.ExpectedZone == zone).ToList();
            int zoneCorrect = zoneRecords.Count(r =>
                r.Decision != null && r.Decision.WasAccepted && r.Decision.Zone == zone);
            zoneResults.Add(new ZoneResult
            {
                Zone = zone,
                Total = zoneRecords.Count,
                Correct = zoneCorrect,
                Rejected = zoneRecords.Count(r => r.Decision != null && !r.Decision.WasAccepted),
                Accuracy = zoneRecords.Count > 0 ? (double)zoneCorrect / zoneRecords.Count : 0
            });
        }

        // Confusion matrix: [expected][predicted]
        var confusion = new List<List<int>>();
        var zoneList = Enum.GetValues<DeskZone>().Cast<DeskZone>().ToList();
        foreach (var expected in zoneList)
        {
            var row = new List<int>();
            foreach (var predicted in zoneList)
            {
                int count = records.Count(r =>
                    r.ExpectedZone == expected &&
                    r.Decision != null && r.Decision.Zone == predicted);
                row.Add(count);
            }
            confusion.Add(row);
        }

        var tapResults = records.Select(r => new TapResult
        {
            Expected = r.ExpectedZone,
            Predicted = r.Decision?.Zone,
            Confidence = r.Decision?.Confidence ?? 0,
            LatencyMs = r.Decision?.ProcessingLatencyMs ?? 0,
            WasRejected = r.Decision == null || !r.Decision.WasAccepted
        }).ToList();

        var report = new EvaluationReport
        {
            TopologyZoneCount = 4,
            ProfileId = profile.Id,
            Strategy = profile.Classifier?.Strategy ?? SensingStrategy.Passive,
            CompletedAt = DateTime.UtcNow,
            TotalTaps = totalTaps,
            Correct = correct,
            Rejected = rejected,
            OverallAccuracy = totalTaps > 0 ? (double)correct / totalTaps : 0,
            MedianLatencyMs = medianLatency,
            ZoneResults = zoneResults,
            ConfusionMatrix = confusion,
            TapResults = tapResults
        };

        return report;
    }
}
