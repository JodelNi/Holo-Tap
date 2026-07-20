using System.Text.Json;

namespace Holo.Models;

/// <summary>
/// Maps to HoloCore.HoloProfile on macOS.
/// Stores trained classifier data, calibration summary, and zone actions.
/// </summary>
public class HoloProfile
{
    public static readonly int CurrentVersion = 3;

    public int Version { get; set; } = CurrentVersion;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string SurfaceDescription { get; set; } = "";
    public string LaptopPositionNote { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public TrainedTapClassifier? Classifier { get; set; }
    public CalibrationSummary? Calibration { get; set; }
    public List<ZoneConfiguration> Zones { get; set; } = [];
}

public class TrainedTapClassifier
{
    public SensingStrategy Strategy { get; set; }
    public List<string> FeatureNames { get; set; } = [];
    public List<double> Center { get; set; } = [];
    public List<double> Scales { get; set; } = [];
    public List<double> FeatureWeights { get; set; } = [];
    public double OutlierThreshold { get; set; }
    public double MinimumConfidence { get; set; } = 0.36;
    public double? PositiveNoveltyThreshold { get; set; }
    public RegularizedLinearZoneModel? LinearZoneModel { get; set; }
    public List<LabeledTap> PositiveExamples { get; set; } = [];
    public List<LabeledTap> NegativeExamples { get; set; } = [];
}

public class RegularizedLinearZoneModel
{
    public List<List<double>> Coefficients { get; set; } = [];
}

public class LabeledTap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DeskZone? Zone { get; set; }
    public string? NegativeLabel { get; set; }
    public TapFeatureVector? Feature { get; set; }
}

public class CalibrationSummary
{
    public int SampleCount { get; set; }
    public List<int> SamplesPerZone { get; set; } = [];
    public double? LeaveOneOutAccuracy { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maps to HoloCore.EvaluationReport on macOS.
/// </summary>
public class EvaluationReport
{
    public int TopologyZoneCount { get; set; }
    public Guid ProfileId { get; set; }
    public SensingStrategy Strategy { get; set; }
    public DateTime CompletedAt { get; set; }
    public int TotalTaps { get; set; }
    public int Correct { get; set; }
    public int Rejected { get; set; }
    public double OverallAccuracy { get; set; }
    public double MedianLatencyMs { get; set; }
    public List<ZoneResult> ZoneResults { get; set; } = [];
    public List<List<int>> ConfusionMatrix { get; set; } = [];
    public List<TapResult> TapResults { get; set; } = [];
}

public class ZoneResult
{
    public DeskZone Zone { get; set; }
    public int Total { get; set; }
    public int Correct { get; set; }
    public int Rejected { get; set; }
    public double Accuracy { get; set; }
}

public class TapResult
{
    public DeskZone Expected { get; set; }
    public DeskZone? Predicted { get; set; }
    public double Confidence { get; set; }
    public double LatencyMs { get; set; }
    public bool WasRejected { get; set; }
}
