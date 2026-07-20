namespace Holo.Models;

public class SpectrumBand
{
    public int Id => (int)Math.Round(CenterFrequency);
    public double CenterFrequency { get; set; }
    public double LevelDB { get; set; }
}
