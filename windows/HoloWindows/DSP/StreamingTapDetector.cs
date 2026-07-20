using Holo.Models;

namespace Holo.DSP;

/// <summary>
/// Port of HoloCore.StreamingTapDetector (Swift → C#).
/// Low-allocation streaming onset detector with adaptive noise floor.
/// </summary>
public sealed class StreamingTapDetector
{
    private readonly double _sampleRate;
    private readonly int _channelCount;
    private readonly int _analysisWindowSamples;
    private readonly int _preRollSamples;
    private readonly int _warmUpSamples;
    private readonly double _initialNoiseFloorRMS;

    private double _noiseFloorRMS;
    private long _totalSamples;
    private List<float[]> _preRoll;
    private List<float[]>? _capture;
    private int _captureOnsetOffset;
    private long _captureStreamIndex;
    private double _captureNoiseFloor;
    private int _refractorySamplesRemaining;
    private bool _adaptNoiseDuringRefractory;
    private int _warmUpSamplesRemaining;
    private float[] _onsetFilterState = new float[4];

    public double NoiseFloorRMS => _noiseFloorRMS;
    public long TotalSamples => _totalSamples;

    public StreamingTapDetector(
        double sampleRate,
        int channelCount,
        double analysisDuration = 0.090,
        double preRollDuration = 0.012,
        double warmUpDuration = 0.75,
        double initialNoiseFloorRMS = 0.0005)
    {
        _sampleRate = sampleRate;
        _channelCount = Math.Max(channelCount, 1);
        _analysisWindowSamples = Math.Max((int)(sampleRate * analysisDuration), 1024);
        _preRollSamples = Math.Max((int)(sampleRate * preRollDuration), 128);
        _warmUpSamples = Math.Max((int)(sampleRate * warmUpDuration), 0);
        _initialNoiseFloorRMS = Math.Max(initialNoiseFloorRMS, 0.00001);
        _noiseFloorRMS = Math.Max(initialNoiseFloorRMS, 0.00001);
        _warmUpSamplesRemaining = Math.Max((int)(sampleRate * warmUpDuration), 0);
        _preRoll = new List<float[]>(_channelCount);
        for (int i = 0; i < _channelCount; i++)
            _preRoll.Add([]);
    }

    public void Reset()
    {
        _totalSamples = 0;
        _noiseFloorRMS = _initialNoiseFloorRMS;
        _preRoll = new List<float[]>(_channelCount);
        for (int i = 0; i < _channelCount; i++)
            _preRoll.Add([]);
        _capture = null;
        _refractorySamplesRemaining = 0;
        _adaptNoiseDuringRefractory = false;
        _warmUpSamplesRemaining = _warmUpSamples;
        _onsetFilterState = new float[4];
    }

    public List<DetectedTap> Process(List<float[]> incoming)
    {
        if (incoming.Count == 0) return [];

        int frameCount = incoming.Min(c => c.Length);
        if (frameCount <= 0) return [];

        var channels = NormalizeChannels(incoming, frameCount);
        var mono = MixDown(channels);
        var onsetSignal = LowPassForOnset(mono);
        long prevTotal = _totalSamples;
        _totalSamples += frameCount;

        double rms = RootMeanSquare(onsetSignal);
        double peak = onsetSignal.Max(v => Math.Abs((double)v));

        // Warm-up: learn the room noise floor
        if (_warmUpSamplesRemaining > 0)
        {
            AdaptNoiseFloor(rms, isWarmUp: true);
            _warmUpSamplesRemaining = Math.Max(0, _warmUpSamplesRemaining - frameCount);
            AppendToPreRoll(channels);
            return [];
        }

        // Refractory: waiting after a detection
        if (_refractorySamplesRemaining > 0)
        {
            if (_adaptNoiseDuringRefractory)
                AdaptNoiseFloor(rms, isWarmUp: false);
            _refractorySamplesRemaining = Math.Max(0, _refractorySamplesRemaining - frameCount);
            if (_refractorySamplesRemaining == 0)
                _adaptNoiseDuringRefractory = false;
            AppendToPreRoll(channels);
            return [];
        }

        // Filling an active capture window
        if (_capture != null)
        {
            AppendToCapture(channels);
            var completed = CompleteCaptureIfReady();
            if (completed != null) return [completed];
            return [];
        }

        // Detection logic
        double rmsThreshold = Math.Max(_noiseFloorRMS * 1.18, 0.0008);
        double peakThreshold = Math.Max(_noiseFloorRMS * 4.0, 0.007);
        double crest = peak / Math.Max(rms, 0.000001);
        double strongSampleThreshold = Math.Max(peakThreshold, peak * 0.55);
        int strongCount = onsetSignal.Count(v => Math.Abs((double)v) >= strongSampleThreshold);
        double strongSampleFraction = (double)strongCount / Math.Max(frameCount, 1);
        bool isImpulse = rms > rmsThreshold
                      && peak > peakThreshold
                      && crest > 2.0
                      && strongSampleFraction < 0.20;

        if (isImpulse)
        {
            int crossing = Array.FindIndex(onsetSignal, v => Math.Abs((double)v) >= peakThreshold);
            if (crossing < 0) crossing = 0;
            _capture = new List<float[]>(_preRoll.Select(a => a.ToArray()));
            _captureOnsetOffset = (_preRoll[0].Length) + crossing;
            _captureStreamIndex = prevTotal + crossing;
            _captureNoiseFloor = _noiseFloorRMS;
            AppendToCapture(channels);
            var completed = CompleteCaptureIfReady();
            if (completed != null) return [completed];
        }
        else
        {
            AdaptNoiseFloor(rms, isWarmUp: false);
            AppendToPreRoll(channels);
        }

        return [];
    }

    private float[][] NormalizeChannels(List<float[]> incoming, int frameCount)
    {
        var result = new float[_channelCount][];
        for (int c = 0; c < _channelCount; c++)
        {
            var src = incoming[Math.Min(c, incoming.Count - 1)];
            result[c] = src.Take(frameCount).ToArray();
            if (result[c].Length < frameCount)
                Array.Resize(ref result[c], frameCount);
        }
        return result;
    }

    private float[] MixDown(float[][] channels)
    {
        if (channels.Length <= 1) return channels[0];
        int len = channels[0].Length;
        var mono = new float[len];
        float scale = 1.0f / channels.Length;
        for (int c = 0; c < channels.Length; c++)
            for (int i = 0; i < len; i++)
                mono[i] += channels[c][i] * scale;
        return mono;
    }

    private double RootMeanSquare(float[] values)
    {
        if (values.Length == 0) return 0;
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
            sum += (double)values[i] * values[i];
        return Math.Sqrt(sum / values.Length);
    }

    private double RootMeanSquare(double[] values)
    {
        if (values.Length == 0) return 0;
        double sum = 0;
        for (int i = 0; i < values.Length; i++)
            sum += values[i] * values[i];
        return Math.Sqrt(sum / values.Length);
    }

    private void AdaptNoiseFloor(double measuredRMS, bool isWarmUp)
    {
        if (!double.IsFinite(measuredRMS)) return;
        double measured = Math.Max(measuredRMS, 0.00001);
        if (isWarmUp)
        {
            _noiseFloorRMS = 0.86 * _noiseFloorRMS + 0.14 * measured;
            return;
        }
        double capped = Math.Min(measured, Math.Max(_noiseFloorRMS * 3.5, 0.020));
        double alpha = capped > _noiseFloorRMS ? 0.06 : 0.025;
        _noiseFloorRMS = (1 - alpha) * _noiseFloorRMS + alpha * capped;
    }

    private float[] LowPassForOnset(float[] values)
    {
        if (values.Length == 0) return [];
        double cutoff = Math.Min(6000.0, _sampleRate * 0.20);
        float alpha = (float)(1 - Math.Exp(-2 * Math.PI * cutoff / _sampleRate));
        var result = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float filtered = values[i];
            for (int s = 0; s < _onsetFilterState.Length; s++)
            {
                _onsetFilterState[s] += alpha * (filtered - _onsetFilterState[s]);
                filtered = _onsetFilterState[s];
            }
            result[i] = filtered;
        }
        return result;
    }

    private void AppendToPreRoll(float[][] channels)
    {
        for (int c = 0; c < _channelCount; c++)
        {
            var list = _preRoll[c].ToList();
            list.AddRange(channels[c]);
            if (list.Count > _preRollSamples)
                list.RemoveRange(0, list.Count - _preRollSamples);
            _preRoll[c] = list.ToArray();
        }
    }

    private void AppendToCapture(float[][] channels)
    {
        if (_capture == null) return;
        for (int c = 0; c < _channelCount; c++)
        {
            var list = _capture[c].ToList();
            list.AddRange(channels[c]);
            _capture[c] = list.ToArray();
        }
    }

    private DetectedTap? CompleteCaptureIfReady()
    {
        if (_capture == null || _capture[0].Length < _analysisWindowSamples)
            return null;

        var trimmed = _capture.Select(ch => ch.Take(_analysisWindowSamples).ToArray()).ToArray();
        var tap = new DetectedTap
        {
            Channels = [.. trimmed],
            OnsetOffset = Math.Min(_captureOnsetOffset, _analysisWindowSamples - 1),
            StreamSampleIndex = _captureStreamIndex,
            NoiseFloorRMS = _captureNoiseFloor
        };

        // Impact event gate (simplified for now)
        bool accepted = ImpactEventGate.Accepts(tap, _sampleRate);
        _capture = null;
        _preRoll = new List<float[]>(_channelCount);
        for (int i = 0; i < _channelCount; i++)
            _preRoll.Add([]);
        _refractorySamplesRemaining = (int)(_sampleRate * 0.14);
        _adaptNoiseDuringRefractory = !accepted;
        return accepted ? tap : null;
    }
}

/// <summary>
/// Port of HoloCore.ImpactEventGate — rejects sustained sounds (speech, background).
/// </summary>
internal static class ImpactEventGate
{
    public static bool Accepts(DetectedTap tap, double sampleRate)
    {
        if (tap.Channels.Count == 0 || tap.Channels[0].Length == 0) return false;
        var mono = MixDownToDouble(tap.Channels);
        int onset = tap.OnsetOffset;

        // Early energy concentration
        double totalEnergy = mono.Sum(v => v * v) + 1e-15;
        int earlyEnd = Math.Min(onset + (int)(sampleRate * 0.025), mono.Length);
        double earlyEnergy = 0;
        for (int i = onset; i < earlyEnd && i < mono.Length; i++)
            earlyEnergy += mono[i] * mono[i];
        double earlyFraction = earlyEnergy / totalEnergy;

        // Late energy: sustained sound check
        int lateStart = Math.Min(onset + (int)(sampleRate * 0.050), mono.Length);
        double lateEnergy = 0;
        for (int i = lateStart; i < mono.Length; i++)
            lateEnergy += mono[i] * mono[i];
        double lateFraction = lateEnergy / totalEnergy;

        // Duration check via envelope
        double peak = mono.Max(v => Math.Abs(v));
        double threshold = peak * 0.40;
        int aboveCount = mono.Count(v => Math.Abs(v) >= threshold);
        double aboveDuration = (double)aboveCount / sampleRate;

        return earlyFraction > 0.15
            && lateFraction < 0.50
            && aboveDuration < 0.080;
    }

    private static double[] MixDownToDouble(List<float[]> channels)
    {
        if (channels.Count == 0) return [];
        int len = channels[0].Length;
        var mono = new double[len];
        float scale = 1.0f / channels.Count;
        for (int c = 0; c < channels.Count; c++)
            for (int i = 0; i < len && i < channels[c].Length; i++)
                mono[i] += channels[c][i] * scale;
        return mono;
    }
}
