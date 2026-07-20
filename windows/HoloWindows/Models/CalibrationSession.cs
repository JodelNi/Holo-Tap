namespace Holo.Models;

/// <summary>
/// Port of HoloCore.CalibrationSession + CalibrationDraft + CalibrationGuidance.
/// </summary>
public static class CalibrationGuidance
{
    public const int TargetTapsPerZone = 10;
    public const double MinimumCleanAgreement = 0.80;
}

public class CalibrationDraft
{
    public string Name { get; set; } = "My Desk";
    public string SurfaceDescription { get; set; } = "Rigid desk";
    public string LaptopPositionNote { get; set; } = "Laptop centered; position unchanged";
    public SensingStrategy Strategy { get; set; } = SensingStrategy.Passive;
}

public class CalibrationSession
{
    public int TargetPerZone { get; }
    public CalibrationDraft Draft { get; set; }
    public List<LabeledTap> PositiveSamples { get; set; } = [];
    public List<LabeledTap> NegativeSamples { get; set; } = [];
    public string? NegativeLabel { get; set; }
    public bool IsArmed { get; set; }

    public CalibrationSession(CalibrationDraft? draft = null, int targetPerZone = CalibrationGuidance.TargetTapsPerZone)
    {
        TargetPerZone = targetPerZone;
        Draft = draft ?? new CalibrationDraft();
    }

    public DeskZone? CurrentZone
    {
        get
        {
            foreach (var zone in Enum.GetValues<DeskZone>())
                if (Count(zone) < TargetPerZone) return zone;
            return null;
        }
    }

    public bool ZonesComplete => CurrentZone == null;
    public int TotalRequired => TargetPerZone * 4;
    public double Progress => (double)PositiveSamples.Count / TotalRequired;

    public int Count(DeskZone zone) => PositiveSamples.Count(s => s.Zone == zone);
    public int NegativeCount(string label) => NegativeSamples.Count(s => s.NegativeLabel == label);
}

public class EvaluationSession
{
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int TargetPerZone { get; set; } = 15;
    public List<EvaluationRecord> Records { get; set; } = [];
    public bool IsArmed { get; set; }

    public DeskZone? CurrentZone
    {
        get
        {
            foreach (var zone in Enum.GetValues<DeskZone>())
                if (Records.Count(r => r.ExpectedZone == zone) < TargetPerZone) return zone;
            return null;
        }
    }

    public double Progress => (double)Records.Count / (TargetPerZone * 4);
}

public class EvaluationRecord
{
    public DeskZone ExpectedZone { get; set; }
    public ClassificationDecision? Decision { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}
