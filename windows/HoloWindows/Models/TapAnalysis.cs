namespace Holo.Models;

public class TapAnalysis
{
    public TapFeatureVector? Feature { get; set; }
    public List<SpectrumBand> Spectrum { get; set; } = [];
}
