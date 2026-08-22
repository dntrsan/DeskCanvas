using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;

internal static class V21SpectrumAndClockContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var fast = SpectrumFrameSmoothing.Step(0, 1, .075);
        var slow = SpectrumFrameSmoothing.Step(1, 0, .075);
        if (fast <= .60 || slow >= .85 || fast <= slow) throw new InvalidOperationException("spectrum attack/release contract changed");
        var a = SpectrumFrameSmoothing.Step(0, 1, 1d / 60);
        var b = SpectrumFrameSmoothing.Step(a, 1, 1d / 60);
        var c = SpectrumFrameSmoothing.Step(0, 1, 1d / 30);
        if (Math.Abs(b - c) > .012) throw new InvalidOperationException("spectrum interpolation is frame-rate dependent");
        if (SpectrumFrameSmoothing.Step(double.NaN, 2, .02) is < 0 or > 1 || SpectrumFrameSmoothing.Step(.5, double.NaN, .02) is < 0 or > 1) throw new InvalidOperationException("spectrum clamp contract changed");
        if (SpectrumFrameSmoothing.Step(.4, .9, .02) <= .4 || SpectrumFrameSmoothing.Step(.4, .1, .02) >= .4) throw new InvalidOperationException("spectrum retarget contract changed");

        var compact = ClockCompactLayoutMath.Scale(64, 64);
        if (compact is <= 0 or >= 1 || ClockCompactLayoutMath.Scale(240, 160) != 1) throw new InvalidOperationException("clock compact scale contract changed");
        var min = ClockCompactLayoutMath.Clamp(12, 31);
        if (min.Width != 32 || min.Height != 32) throw new InvalidOperationException("clock minimum size was not lowered to 32 DIP");
        Console.WriteLine("PASS v2.1 spectrum frame smoothing and compact clock layout");
    }
}
