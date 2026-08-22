using System.Runtime.CompilerServices;
using DeskCanvas.App.Windows;

internal static class V20WaveProgressMathTestsAccepted2
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (WaveProgressMath.Envelope(2, 120) != 0 || WaveProgressMath.Envelope(118, 120) != 0 || WaveProgressMath.Envelope(60, 120) < .99)
            throw new InvalidOperationException("wave envelope does not round its ends down to the centre line");
        if (Math.Abs(WaveProgressMath.NextSpeed(0, true, .8) - WaveProgressMath.RunningSpeed) > .0001 || WaveProgressMath.NextSpeed(WaveProgressMath.RunningSpeed, false, .6) != 0)
            throw new InvalidOperationException("wave acceleration/deceleration duration changed");
        var phase = WaveProgressMath.AdvancePhase(.12, WaveProgressMath.RunningSpeed, .3);
        var wrapped = WaveProgressMath.AdvancePhase(.97, WaveProgressMath.RunningSpeed, .3);
        if (Math.Abs(phase - .15) > .0001 || Math.Abs(wrapped) > .0001 || WaveProgressMath.ClampRatio(double.NaN) != 0)
            throw new InvalidOperationException("wave phase or ratio normalization failed");
        var stops = WaveProgressMath.GradientStops(.5);
        if (stops.Count != 4 || !stops[1].Accent || stops[2].Accent || stops[1].Offset != stops[2].Offset)
            throw new InvalidOperationException("wave progress is not represented as a continuous hard-stop gradient");
        Console.WriteLine("PASS v2.0 wave progress math");
    }
}
