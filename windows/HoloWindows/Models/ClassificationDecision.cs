namespace Holo.Models;

public enum RejectionReason
{
    WeakSignal,
    LowSignalToNoise,
    ClippedSignal,
    OutOfDistribution,
    AmbiguousZone,
    ResemblesNegativeExample,
    SchemaMismatch,
    Paused
}

public class ClassificationDecision
{
    public DeskZone? Zone { get; set; }
    public double Confidence { get; set; }
    public double SignalStrength { get; set; }
    public List<double> ZoneDistances { get; set; } = [];
    public RejectionReason? RejectionReason { get; set; }
    public double ProcessingLatencyMs { get; set; }

    public bool WasAccepted => Zone != null && RejectionReason == null;
}
