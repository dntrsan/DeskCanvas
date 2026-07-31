using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V15AudioContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Equal(5d, AudioPeakMath.BarHeight(0, 3));
        Equal(13.5d, AudioPeakMath.BarHeight(.5, 3));
        Equal(22d, AudioPeakMath.BarHeight(1, 3));
        Equal(0d, AudioPeakMath.Clamp(double.NaN));
        Equal(0d, AudioPeakMath.Clamp(-1));
        Equal(1d, AudioPeakMath.Clamp(2));
        Console.WriteLine("PASS v1.5 audio peak level math and safe failure clamp");
    }
    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > .0001) throw new InvalidOperationException($"expected {expected}, actual {actual}");
    }
}
