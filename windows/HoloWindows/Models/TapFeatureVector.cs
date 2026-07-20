namespace Holo.Models;

/// <summary>
/// Maps to HoloCore.TapFeatureVector on macOS.
/// </summary>
public class TapFeatureVector
{
    public SensingStrategy Strategy { get; set; }
    public List<string> Names { get; set; } = [];
    public List<double> Values { get; set; } = [];
    public SignalQuality? Quality { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maps to HoloCore.DetectedTap on macOS.
/// A raw tap event from the streaming detector.
/// </summary>
public class DetectedTap
{
    public List<float[]> Channels { get; set; } = [];
    public int OnsetOffset { get; set; }
    public long StreamSampleIndex { get; set; }
    public double NoiseFloorRMS { get; set; }
}
