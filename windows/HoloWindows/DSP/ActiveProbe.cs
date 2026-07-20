namespace Holo.DSP;

/// <summary>
/// Port of HoloCore.ActiveProbe (Swift → C#).
/// Generates a chirp for active sensing and extracts probe response features.
/// </summary>
public static class ActiveProbe
{
    /// <summary>
    /// Generate a linear chirp in the 15.5–21 kHz range.
    /// </summary>
    public static float[] Chirp(
        double sampleRate,
        double duration = 0.024,
        double startFrequency = 15_500,
        double endFrequency = 21_000,
        double amplitude = 0.035)
    {
        int count = Math.Max((int)(sampleRate * duration), 32);
        var signal = new float[count];
        int ramp = Math.Max(count / 8, 1);
        double slope = (endFrequency - startFrequency) / duration;

        for (int i = 0; i < count; i++)
        {
            double time = (double)i / sampleRate;
            double phase = 2 * Math.PI * (startFrequency * time + 0.5 * slope * time * time);
            double edge = Math.Min((double)i / ramp, (double)(count - 1 - i) / ramp);
            double envelope = Math.Clamp(edge, 0, 1);
            signal[i] = (float)(Math.Sin(phase) * amplitude * envelope);
        }
        return signal;
    }

    /// <summary>
    /// Extract active probe response features from a signal.
    /// Matches Swift ActiveProbe.responseFeatures().
    /// </summary>
    public static (List<string> names, List<double> values) ResponseFeatures(
        double[] signal, double sampleRate, double[]? spectrum = null)
    {
        double[] probe = Chirp(sampleRate).Select(v => (double)v).ToArray();
        if (signal.Length < probe.Length)
            return (ActiveFeatureNames.ToList(), Enumerable.Repeat(0.0, ActiveFeatureNames.Length).ToList());

        int stride = Math.Max(probe.Length / 4, 1);
        var correlations = new List<double>();
        double probeEnergy = Math.Sqrt(probe.Sum(v => v * v)) + 1e-12;

        for (int idx = 0; idx + probe.Length <= signal.Length; idx += stride)
        {
            double dot = 0, energy = 0;
            for (int pi = 0; pi < probe.Length; pi++)
            {
                double sample = signal[idx + pi];
                dot += sample * probe[pi];
                energy += sample * sample;
            }
            correlations.Add(dot / (probeEnergy * Math.Sqrt(energy) + 1e-12));
        }

        double peak = correlations.Count > 0 ? correlations.Max(Math.Abs) : 0;
        int peakIdx = 0;
        double maxVal = 0;
        for (int i = 0; i < correlations.Count; i++)
        {
            if (Math.Abs(correlations[i]) > maxVal) { maxVal = Math.Abs(correlations[i]); peakIdx = i; }
        }
        double mean = correlations.Count > 0 ? correlations.Average() : 0;
        double variance = correlations.Count > 0
            ? correlations.Sum(v => (v - mean) * (v - mean)) / correlations.Count
            : 0;

        double[] responseSpectrum = spectrum ?? Radix2FFT.PowerSpectrum(
            signal, Math.Min(Radix2FFT.NextPowerOfTwo(signal.Length), 4096));
        var responseBands = NormalizedLogBands(responseSpectrum, sampleRate, 8);

        var values = new List<double> { peak, (double)peakIdx / Math.Max(correlations.Count - 1, 1), mean, Math.Sqrt(variance) };
        values.AddRange(responseBands);

        return (ActiveFeatureNames.ToList(), values);
    }

    private static List<double> NormalizedLogBands(double[] spectrum, double sampleRate, int count)
    {
        double minimum = 8000.0;
        double maximum = Math.Min(sampleRate * 0.48, 22000);
        double total = spectrum.Sum() + 1e-15;
        var bands = new List<double>();

        for (int i = 0; i < count; i++)
        {
            double lowRatio = (double)i / count;
            double highRatio = (double)(i + 1) / count;
            double low = minimum * Math.Pow(maximum / minimum, lowRatio);
            double high = minimum * Math.Pow(maximum / minimum, highRatio);
            int start = Math.Max((int)(low / sampleRate * ((spectrum.Length - 1) * 2)), 0);
            int end = Math.Min((int)(high / sampleRate * ((spectrum.Length - 1) * 2)), spectrum.Length - 1);
            if (end < start) { bands.Add(-30); continue; }
            double energy = 0;
            for (int j = start; j <= end; j++) energy += spectrum[j];
            energy /= total;
            bands.Add(Math.Log10(energy + 1e-12));
        }
        return bands;
    }

    public static readonly string[] ActiveFeatureNames =
    [
        "probe_correlation_peak", "probe_correlation_lag", "probe_correlation_mean", "probe_correlation_spread",
        "probe_band_0", "probe_band_1", "probe_band_2", "probe_band_3",
        "probe_band_4", "probe_band_5", "probe_band_6", "probe_band_7"
    ];
}
