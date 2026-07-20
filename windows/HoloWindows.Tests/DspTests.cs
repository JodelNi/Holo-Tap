using Holo.DSP;
using Holo.Models;
using Xunit;

namespace HoloWindows.Tests;

public class DspTests
{
    [Fact]
    public void Fft_Produces_Expected_Output_Length()
    {
        // A 256-sample sine wave at 1 kHz, sampled at 48 kHz
        var sampleRate = 48000.0;
        var freq = 1000.0;
        var signal = new double[256];
        for (int i = 0; i < signal.Length; i++)
            signal[i] = Math.Sin(2 * Math.PI * freq * i / sampleRate);

        var spectrum = Radix2FFT.PowerSpectrum(signal, 256);

        // Power spectrum should have 129 bins (size/2 + 1 for 256-point FFT)
        Assert.Equal(129, spectrum.Length);
        Assert.All(spectrum, v => Assert.True(v >= 0, "All power spectrum values should be non-negative"));
        Assert.True(spectrum.Any(v => v > 0), "Power spectrum should contain energy");
    }

    [Fact]
    public void FeatureExtractor_Produces_Expected_Feature_Count()
    {
        var sampleRate = 48000.0;
        var extractor = new TapFeatureExtractor(sampleRate, SensingStrategy.Passive);

        // Create a synthetic tap: 90ms window at 48 kHz = 4320 samples
        int windowSamples = (int)(sampleRate * 0.090);
        var channels = new List<float[]>
        {
            new float[windowSamples]
        };

        // Place a sharp impulse at onset offset 100
        int onset = 100;
        for (int i = 0; i < 20 && i + onset < windowSamples; i++)
            channels[0][onset + i] = 0.5f * (1 - i / 20f);

        var tap = new DetectedTap
        {
            Channels = channels,
            OnsetOffset = onset,
            StreamSampleIndex = 0,
            NoiseFloorRMS = 0.001
        };

        var feature = extractor.Extract(tap);

        // Passive feature set: 6 temporal + 4 spectral + 8 MFCC + 10 band + 2 spatial = 30
        Assert.Equal(30, feature.Names.Count);
        Assert.Equal(30, feature.Values.Count);
        Assert.Equal(SensingStrategy.Passive, feature.Strategy);
        Assert.NotNull(feature.Quality);
    }

    [Fact]
    public void Classifier_Can_Train_And_Predict()
    {
        // Create 2 training examples per zone (8 total)
        var examples = new List<LabeledTap>();
        foreach (var zone in new[] { DeskZone.LeftTop, DeskZone.RightTop })
        {
            for (int i = 0; i < 2; i++)
            {
                var values = Enumerable.Range(0, 30).Select(_ => 0.001 + i * 0.1).ToList();
                if (zone == DeskZone.LeftTop)
                    values[0] = -5.0 + i * 0.1; // distinct log_rms for LeftTop
                else
                    values[0] = -3.0 + i * 0.1; // distinct log_rms for RightTop

                examples.Add(new LabeledTap
                {
                    Id = Guid.NewGuid(),
                    Zone = zone,
                    Feature = new TapFeatureVector
                    {
                        Strategy = SensingStrategy.Passive,
                        Names = TapFeatureExtractor.PassiveFeatureNames.ToList(),
                        Values = values,
                        Quality = new SignalQuality
                        {
                            SignalToNoiseDB = 30,
                            PeakAmplitude = 0.5,
                            RmsAmplitude = 0.1,
                            ClippingFraction = 0,
                            NoiseFloorRMS = 0.001,
                            DurationMilliseconds = 90
                        }
                    }
                });
            }
        }

        var classifier = TapClassifier.Train(examples, []);

        Assert.NotNull(classifier);
        Assert.Equal(SensingStrategy.Passive, classifier.Strategy);
        Assert.Equal(30, classifier.FeatureNames.Count);

        // Predict a sample similar to LeftTop training data
        var testValues = Enumerable.Range(0, 30).Select(_ => 0.001).ToList();
        testValues[0] = -4.9; // close to LeftTop log_rms

        var testFeature = new TapFeatureVector
        {
            Strategy = SensingStrategy.Passive,
            Names = TapFeatureExtractor.PassiveFeatureNames.ToList(),
            Values = testValues,
            Quality = new SignalQuality
            {
                SignalToNoiseDB = 30,
                PeakAmplitude = 0.5,
                RmsAmplitude = 0.1,
                ClippingFraction = 0,
                NoiseFloorRMS = 0.001,
                DurationMilliseconds = 90
            }
        };

        var result = TapClassifier.Predict(testFeature, classifier);

        Assert.True(result.WasAccepted, "Similar sample should be accepted");
        Assert.Equal(DeskZone.LeftTop, result.Zone);
    }
}
