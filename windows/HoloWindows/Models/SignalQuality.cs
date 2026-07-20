namespace Holo.Models;

/// <summary>
/// Maps to HoloCore.SignalQuality on macOS.
/// Quality metrics for a captured tap signal.
/// </summary>
public class SignalQuality
{
    public double SignalToNoiseDB { get; set; }
    public double PeakAmplitude { get; set; }
    public double RmsAmplitude { get; set; }
    public double ClippingFraction { get; set; }
    public double NoiseFloorRMS { get; set; }
    public double DurationMilliseconds { get; set; }

    public double Score
    {
        get
        {
            var snr = Math.Clamp((SignalToNoiseDB - 4) / 30, 0, 1);
            var strength = Math.Clamp((PeakAmplitude - 0.003) / 0.15, 0, 1);
            var clean = 1 - Math.Clamp(ClippingFraction * 4, 0, 1);
            return 0.55 * snr + 0.25 * strength + 0.20 * clean;
        }
    }

    public string Summary
    {
        get
        {
            if (ClippingFraction > 0.20) return "Clipped";
            if (PeakAmplitude < 0.003) return "Weak";
            if (SignalToNoiseDB < 6.0) return "Noisy";
            if (Score > 0.72) return "Excellent";
            if (Score > 0.48) return "Good";
            return "Fair";
        }
    }
}
