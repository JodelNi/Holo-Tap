using Holo.Models;

namespace Holo.DSP;

/// <summary>
/// Full port of HoloCore.TapFeatureExtractor (Swift → C#).
/// Extracts temporal, spectral, mel-cepstral, broad-band, and spatial features
/// from a detected tap event. Supports passive, active, and hybrid strategies.
/// </summary>
public class TapFeatureExtractor
{
    private readonly double _sampleRate;
    private readonly SensingStrategy _strategy;

    public TapFeatureExtractor(double sampleRate, SensingStrategy strategy)
    {
        _sampleRate = sampleRate;
        _strategy = strategy;
    }

    public TapFeatureVector Extract(DetectedTap event_tap, DateTime? capturedAt = null)
    {
        var prepared = Prepare(event_tap);
        return MakeFeature(event_tap, prepared.channels, prepared.mono, prepared.powerSpectrum, capturedAt);
    }

    public TapAnalysis Analyze(DetectedTap event_tap, DateTime? capturedAt = null, int bandCount = 16)
    {
        var prepared = Prepare(event_tap);
        return new TapAnalysis
        {
            Feature = MakeFeature(event_tap, prepared.channels, prepared.mono, prepared.powerSpectrum, capturedAt),
            Spectrum = SpectrumBands(prepared.powerSpectrum, bandCount)
        };
    }

    private TapFeatureVector MakeFeature(
        DetectedTap event_tap,
        List<float[]> channels,
        double[] mono,
        double[] powerSpectrum,
        DateTime? capturedAt)
    {
        var (names, values) = _strategy switch
        {
            SensingStrategy.Passive => PassiveFeatures(mono, channels, event_tap.OnsetOffset, powerSpectrum),
            SensingStrategy.Active => ActiveProbe.ResponseFeatures(mono, _sampleRate, powerSpectrum),
            SensingStrategy.Hybrid =>
                MergeFeatures(
                    PassiveFeatures(mono, channels, event_tap.OnsetOffset, powerSpectrum),
                    ActiveProbe.ResponseFeatures(mono, _sampleRate, powerSpectrum)),
            _ => (new List<string>(), new List<double>())
        };

        double rms = RootMeanSquare(mono);
        double peak = mono.Length > 0 ? mono.Max(Math.Abs) : 0;
        double clipping = mono.Length > 0
            ? (double)mono.Count(v => Math.Abs(v) >= 0.995) / Math.Max(mono.Length, 1)
            : 0;
        double snr = 20 * Math.Log10(Math.Max(rms, 1e-12) / Math.Max(event_tap.NoiseFloorRMS, 1e-12));

        var quality = new SignalQuality
        {
            SignalToNoiseDB = snr,
            PeakAmplitude = peak,
            RmsAmplitude = rms,
            ClippingFraction = clipping,
            NoiseFloorRMS = event_tap.NoiseFloorRMS,
            DurationMilliseconds = (double)(mono.Length) / _sampleRate * 1000
        };

        return new TapFeatureVector
        {
            Strategy = _strategy,
            Names = names,
            Values = values.Select(v => double.IsFinite(v) ? v : 0.0).ToList(),
            Quality = quality,
            CapturedAt = capturedAt ?? DateTime.UtcNow
        };
    }

    public List<SpectrumBand> SpectrumBands(DetectedTap event_tap, int count = 16)
    {
        double[] mono = MixDown(event_tap.Channels).Select(v => (double)v).ToArray();
        double[] spectrum = Radix2FFT.PowerSpectrum(mono, Math.Min(Radix2FFT.NextPowerOfTwo(mono.Length), 4096));
        return SpectrumBands(spectrum, count);
    }

    private List<SpectrumBand> SpectrumBands(double[] spectrum, int count)
    {
        double minimum = 80.0;
        double maximum = Math.Min(_sampleRate * 0.46, 18000);
        var bands = new List<SpectrumBand>();
        for (int i = 0; i < count; i++)
        {
            double low = minimum * Math.Pow(maximum / minimum, (double)i / count);
            double high = minimum * Math.Pow(maximum / minimum, (double)(i + 1) / count);
            double center = Math.Sqrt(low * high);
            int start = FrequencyBin(low, spectrum.Length);
            int end = Math.Max(start, FrequencyBin(high, spectrum.Length));
            double energy = 0;
            int span = Math.Max(end - start + 1, 1);
            for (int j = start; j <= Math.Min(end, spectrum.Length - 1); j++)
                energy += spectrum[j];
            energy /= span;
            bands.Add(new SpectrumBand
            {
                CenterFrequency = center,
                LevelDB = 10 * Math.Log10(energy + 1e-15)
            });
        }
        return bands;
    }

    private (List<float[]> channels, double[] mono, double[] powerSpectrum) Prepare(DetectedTap event_tap)
    {
        var channels = event_tap.Channels.Where(ch => ch.Length > 0).ToList();
        double[] mono = MixDown(channels).Select(v => (double)v).ToArray();
        var spectrum = Radix2FFT.PowerSpectrum(mono, Math.Min(Radix2FFT.NextPowerOfTwo(mono.Length), 4096));
        return (channels, mono, spectrum);
    }

    private (List<string> names, List<double> values) PassiveFeatures(
        double[] signal, List<float[]> channels, int onset, double[] spectrum)
    {
        if (signal.Length == 0)
            return (PassiveFeatureNames.ToList(), Enumerable.Repeat(0.0, PassiveFeatureNames.Length).ToList());

        double rms = RootMeanSquare(signal);
        double peak = signal.Max(Math.Abs);
        double crest = peak / Math.Max(rms, 1e-12);

        int zeroCrossings = 0;
        for (int i = 1; i < signal.Length; i++)
            if ((signal[i - 1] >= 0) != (signal[i] >= 0))
                zeroCrossings++;
        double zeroCrossingRate = (double)zeroCrossings / Math.Max(signal.Length - 1, 1);

        int peakIndex = 0;
        double maxAbs = 0;
        for (int i = 0; i < signal.Length; i++)
        {
            if (Math.Abs(signal[i]) > maxAbs) { maxAbs = Math.Abs(signal[i]); peakIndex = i; }
        }
        double attackPosition = (double)Math.Max(peakIndex - onset, 0) / Math.Max(signal.Length - onset, 1);

        double[] energies = signal.Select(v => v * v).ToArray();
        double totalEnergy = energies.Sum() + 1e-15;
        double temporalCentroid = 0;
        for (int i = 0; i < energies.Length; i++)
            temporalCentroid += i * energies[i];
        temporalCentroid = temporalCentroid / totalEnergy / Math.Max(signal.Length - 1, 1);

        int split = Math.Clamp(onset + (int)(_sampleRate * 0.025), 1, signal.Length - 1);
        double early = 0, late = 0;
        for (int i = 0; i < split && i < energies.Length; i++) early += energies[i];
        for (int i = split; i < energies.Length; i++) late += energies[i];
        double earlyLateRatio = Math.Log10((early + 1e-12) / (late + 1e-12));

        var temporal = new List<double> {
            Math.Log10(rms + 1e-12), crest, zeroCrossingRate, attackPosition, temporalCentroid, earlyLateRatio
        };
        var spectral = SpectralFeatures(spectrum);
        var mel = MelCepstralFeatures(spectrum, 18, 8);
        var bands = BroadBandFeatures(spectrum, 10);
        var spatial = SpatialFeatures(channels);

        var allValues = new List<double>();
        allValues.AddRange(temporal);
        allValues.AddRange(spectral);
        allValues.AddRange(mel);
        allValues.AddRange(bands);
        allValues.AddRange(spatial);

        return (PassiveFeatureNames.ToList(), allValues);
    }

    private List<double> SpectralFeatures(double[] spectrum)
    {
        var usable = spectrum.Select((val, idx) => (idx, val))
            .Where(item =>
            {
                double freq = item.idx * _sampleRate / Math.Max((spectrum.Length - 1) * 2.0, 1);
                return freq >= 60 && freq <= Math.Min(18000, _sampleRate * 0.48);
            })
            .ToList();

        if (usable.Count == 0) return [0, 0, 0, 0];

        double total = usable.Sum(item => item.val) + 1e-15;
        double centroid = usable.Sum(item =>
        {
            double freq = item.idx * _sampleRate / Math.Max((spectrum.Length - 1) * 2.0, 1);
            return freq * item.val;
        }) / total;

        double bandwidth = Math.Sqrt(usable.Sum(item =>
        {
            double freq = item.idx * _sampleRate / Math.Max((spectrum.Length - 1) * 2.0, 1);
            return Math.Pow(freq - centroid, 2) * item.val;
        }) / total);

        double cumulative = 0;
        double rolloff = 0;
        foreach (var item in usable)
        {
            cumulative += item.val;
            if (cumulative >= total * 0.85)
            {
                rolloff = item.idx * _sampleRate / Math.Max((spectrum.Length - 1) * 2.0, 1);
                break;
            }
        }

        double arithmetic = total / Math.Max(usable.Count, 1);
        double geometric = Math.Exp(usable.Sum(item => Math.Log(item.val + 1e-15)) / Math.Max(usable.Count, 1));
        double flatness = geometric / Math.Max(arithmetic, 1e-15);

        return
        [
            centroid / _sampleRate,
            bandwidth / _sampleRate,
            rolloff / _sampleRate,
            flatness
        ];
    }

    private List<double> MelCepstralFeatures(double[] spectrum, int filterCount, int coefficientCount)
    {
        double minMel = HzToMel(80);
        double maxMel = HzToMel(Math.Min(16000, _sampleRate * 0.46));

        var points = new double[filterCount + 2];
        for (int i = 0; i < filterCount + 2; i++)
            points[i] = MelToHz(minMel + (maxMel - minMel) * (double)i / (filterCount + 1));

        var bins = points.Select(p => FrequencyBin(p, spectrum.Length)).ToArray();

        var logEnergies = new double[filterCount];
        for (int f = 0; f < filterCount; f++)
        {
            int left = bins[f];
            int center = Math.Max(bins[f + 1], left + 1);
            int right = Math.Max(bins[f + 2], center + 1);
            double energy = 0;
            if (left < spectrum.Length)
            {
                for (int j = left; j < Math.Min(center, spectrum.Length); j++)
                {
                    double weight = (double)(j - left) / Math.Max(center - left, 1);
                    energy += spectrum[j] * weight;
                }
                if (center < spectrum.Length)
                {
                    for (int j = center; j < Math.Min(right, spectrum.Length); j++)
                    {
                        double weight = (double)(right - j) / Math.Max(right - center, 1);
                        energy += spectrum[j] * weight;
                    }
                }
            }
            logEnergies[f] = Math.Log(energy + 1e-15);
        }

        var coefficients = new List<double>();
        for (int c = 1; c <= coefficientCount; c++)
        {
            double sum = 0;
            for (int f = 0; f < filterCount; f++)
                sum += logEnergies[f] * Math.Cos(Math.PI * c * (f + 0.5) / filterCount);
            coefficients.Add(sum / filterCount);
        }
        return coefficients;
    }

    private List<double> BroadBandFeatures(double[] spectrum, int count)
    {
        double minimum = 80.0;
        double maximum = Math.Min(_sampleRate * 0.46, 18000);
        double total = spectrum.Sum() + 1e-15;
        var bands = new List<double>();
        for (int i = 0; i < count; i++)
        {
            double low = minimum * Math.Pow(maximum / minimum, (double)i / count);
            double high = minimum * Math.Pow(maximum / minimum, (double)(i + 1) / count);
            int start = FrequencyBin(low, spectrum.Length);
            int end = Math.Max(start, FrequencyBin(high, spectrum.Length));
            double energy = 0;
            for (int j = start; j <= Math.Min(end, spectrum.Length - 1); j++)
                energy += spectrum[j];
            energy /= total;
            bands.Add(Math.Log10(energy + 1e-12));
        }
        return bands;
    }

    private List<double> SpatialFeatures(List<float[]> channels)
    {
        if (channels.Count < 2 || channels[0].Length == 0 || channels[1].Length == 0)
            return [0, 0];

        double[] first = channels[0].Select(v => (double)v).ToArray();
        double[] second = channels[1].Select(v => (double)v).ToArray();
        double firstRMS = RootMeanSquare(first);
        double secondRMS = RootMeanSquare(second);
        double energyDelta = Math.Log10((firstRMS + 1e-12) / (secondRMS + 1e-12));

        int maxLag = Math.Min((int)(_sampleRate * 0.00075), 36);
        int bestLag = 0;
        double bestCorrelation = double.NegativeInfinity;
        for (int lag = -maxLag; lag <= maxLag; lag++)
        {
            double correlation = 0;
            int count = 0;
            for (int i = 0; i < first.Length; i++)
            {
                int si = i + lag;
                if (si >= 0 && si < second.Length)
                {
                    correlation += first[i] * second[si];
                    count++;
                }
            }
            if (count > 0 && correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
                bestLag = lag;
            }
        }
        return [energyDelta, (double)bestLag / Math.Max(maxLag, 1)];
    }

    private static float[] MixDown(List<float[]> channels)
    {
        if (channels.Count == 0) return [];
        if (channels.Count == 1) return channels[0];
        int count = channels.Min(ch => ch.Length);
        var result = new float[count];
        for (int c = 0; c < channels.Count; c++)
            for (int i = 0; i < count; i++)
                result[i] += channels[c][i] / channels.Count;
        return result;
    }

    private double RootMeanSquare(double[] values)
    {
        if (values.Length == 0) return 0;
        double sum = values.Sum(v => v * v);
        return Math.Sqrt(sum / values.Length);
    }

    private int FrequencyBin(double frequency, int spectrumCount)
    {
        int fftSize = Math.Max((spectrumCount - 1) * 2, 1);
        return Math.Clamp((int)(frequency / _sampleRate * fftSize), 0, spectrumCount - 1);
    }

    private double HzToMel(double hz) => 2595 * Math.Log10(1 + hz / 700);
    private double MelToHz(double mel) => 700 * (Math.Pow(10, mel / 2595) - 1);

    private static (List<string>, List<double>) MergeFeatures(
        (List<string>, List<double>) a,
        (List<string>, List<double>) b) => (a.Item1.Concat(b.Item1).ToList(), a.Item2.Concat(b.Item2).ToList());

    public static readonly string[] PassiveFeatureNames =
    [
        "log_rms", "crest_factor", "zero_crossing_rate", "attack_position", "temporal_centroid", "early_late_ratio",
        "spectral_centroid", "spectral_bandwidth", "spectral_rolloff_85", "spectral_flatness",
        "mfcc_1", "mfcc_2", "mfcc_3", "mfcc_4", "mfcc_5", "mfcc_6", "mfcc_7", "mfcc_8",
        "band_0", "band_1", "band_2", "band_3", "band_4", "band_5", "band_6", "band_7", "band_8", "band_9",
        "channel_energy_delta", "interchannel_delay"
    ];
}
