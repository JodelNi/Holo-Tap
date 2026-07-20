namespace Holo.DSP;

/// <summary>
/// Port of HoloCore.Radix2FFT (Swift → C#).
/// Power-of-two FFT with Hann window, matching the Swift implementation exactly.
/// </summary>
public static class Radix2FFT
{
    public static int NextPowerOfTwo(int value)
    {
        int n = 1;
        while (n < value) n <<= 1;
        return n;
    }

    /// <summary>
    /// Returns the power spectrum (magnitude²) with Hann window applied.
    /// Matches the Swift implementation: (0...size/2) bins, each normalized by size.
    /// </summary>
    public static double[] PowerSpectrum(double[] input, int? requestedSize = null)
    {
        int desired = requestedSize ?? input.Length;
        int size = NextPowerOfTwo(Math.Max(desired, 2));
        var real = new double[size];
        var imag = new double[size];
        int copied = Math.Min(input.Length, size);

        // Apply Hann window (matching Swift: 0.5 - 0.5 * cos(2πn/(N-1)))
        if (copied > 1)
        {
            for (int i = 0; i < copied; i++)
            {
                double window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (copied - 1));
                real[i] = input[i] * window;
            }
        }

        // Bit-reversal permutation
        int j = 0;
        for (int i = 1; i < size; i++)
        {
            int bit = size >> 1;
            while ((j & bit) != 0)
            {
                j ^= bit;
                bit >>= 1;
            }
            j ^= bit;
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }
        }

        // Cooley-Tukey radix-2 in-place FFT
        int length = 2;
        while (length <= size)
        {
            double angle = -2 * Math.PI / length;
            double baseReal = Math.Cos(angle);
            double baseImag = Math.Sin(angle);
            for (int start = 0; start < size; start += length)
            {
                double wReal = 1.0;
                double wImag = 0.0;
                int half = length / 2;
                for (int offset = 0; offset < half; offset++)
                {
                    int even = start + offset;
                    int odd = even + half;
                    double oddReal = real[odd] * wReal - imag[odd] * wImag;
                    double oddImag = real[odd] * wImag + imag[odd] * wReal;
                    real[odd] = real[even] - oddReal;
                    imag[odd] = imag[even] - oddImag;
                    real[even] += oddReal;
                    imag[even] += oddImag;
                    double nextReal = wReal * baseReal - wImag * baseImag;
                    wImag = wReal * baseImag + wImag * baseReal;
                    wReal = nextReal;
                }
            }
            length <<= 1;
        }

        // Return power spectrum: magnitude² / size for bins 0...size/2
        int halfCount = size / 2;
        var spectrum = new double[halfCount + 1];
        for (int i = 0; i <= halfCount; i++)
            spectrum[i] = (real[i] * real[i] + imag[i] * imag[i]) / size;
        return spectrum;
    }

    public static double[] PowerSpectrum(float[] input, int? requestedSize = null)
    {
        var d = Array.ConvertAll(input, v => (double)v);
        return PowerSpectrum(d, requestedSize);
    }
}
