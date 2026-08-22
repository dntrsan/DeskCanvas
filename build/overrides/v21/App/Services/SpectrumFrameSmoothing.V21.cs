namespace DeskCanvas.App.Services;

/// <summary>Frame-rate-independent attack/release interpolation for visualizer bars.</summary>
internal static class SpectrumFrameSmoothing
{
    internal const double AttackSeconds = .075;
    internal const double ReleaseSeconds = .32;

    internal static double Step(double displayed, double target, double elapsedSeconds)
    {
        displayed = Normalize(displayed);
        target = Normalize(target);
        if (elapsedSeconds <= 0) return displayed;
        var seconds = Math.Min(elapsedSeconds, .12);
        var timeConstant = target > displayed ? AttackSeconds : ReleaseSeconds;
        var blend = 1d - Math.Exp(-seconds / timeConstant);
        return Normalize(displayed + (target - displayed) * blend);
    }

    internal static void Step(ReadOnlySpan<double> displayed, ReadOnlySpan<double> targets, Span<double> destination, double elapsedSeconds)
    {
        for (var index = 0; index < destination.Length; index++)
            destination[index] = Step(index < displayed.Length ? displayed[index] : 0, index < targets.Length ? targets[index] : 0, elapsedSeconds);
    }

    private static double Normalize(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
}
