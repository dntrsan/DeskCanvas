using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V20ActiveSpectrumMathTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var loud = Sine(440d, 1d);
        var quiet = Sine(440d, .01d);
        var loudBands = SpectrumMath.AnalyzeSamples(loud, 48000);
        var quietBands = SpectrumMath.AnalyzeSamples(quiet, 48000);
        for (var band = 0; band < SpectrumMath.BandCount; band++)
        {
            if (Math.Abs(loudBands[band] - quietBands[band]) > .02)
                throw new InvalidOperationException("active WASAPI spectrum shape depends on volume");
        }

        if (loudBands.Max() < .95)
            throw new InvalidOperationException("strongest active spectrum band is too weak");

        var highBands = SpectrumMath.AnalyzeSamples(Sine(6000d, .01d), 48000);
        if (Array.IndexOf(highBands, highBands.Max()) <= Array.IndexOf(loudBands, loudBands.Max()))
            throw new InvalidOperationException("active spectrum peak did not move with frequency");

        if (SpectrumMath.AnalyzeSamples(new double[2048], 48000).Any(static value => value != 0))
            throw new InvalidOperationException("active digital silence did not gate to zero");

        Console.WriteLine("PASS v2.0 active WASAPI relative spectrum math");
    }

    private static double[] Sine(double frequency, double amplitude)
    {
        var samples = new double[2048];
        for (var index = 0; index < samples.Length; index++)
            samples[index] = amplitude * Math.Sin(2d * Math.PI * frequency * index / 48000d);
        return samples;
    }
}
