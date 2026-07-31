using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;

internal static class V15IntegrationContractTests
{
    [ModuleInitializer] internal static void Run()
    {
        Equal(540d, MediaWindowDpiMath.PhysicalSide(360, 1.5));
        var shift = MediaWindowDpiMath.ResizeCenterShift(60, 0, 0, 1.5, 1.5);
        Equal(45d, shift.X); Equal(0d, shift.Y);
        var reader = new FakeReader(.5); var controller = new AudioPeakLeaseController(() => reader);
        var peak = -1d; controller.Poll(true, value => peak = value); Equal(.5, peak); Equal(1, reader.Reads);
        controller.Poll(false, value => peak = value); Equal(0d, peak); Equal(1, reader.Disposals);
        var broken = new FakeReader(1) { Throw = true }; var failures = new AudioPeakLeaseController(() => broken);
        failures.Poll(true, value => peak = value); Equal(0d, peak); Equal(1, broken.Disposals);
        controller.Dispose(); failures.Dispose();
        Console.WriteLine("PASS v1.5 DPI conversion and audio peak lease lifecycle");
    }
    private static void Equal(double expected,double actual){if(Math.Abs(expected-actual)>.001)throw new InvalidOperationException($"expected {expected}, actual {actual}");}
    private sealed class FakeReader(double value) : IAudioPeakReader
    { internal int Reads; internal int Disposals; internal bool Throw; public double ReadPeak(){Reads++;if(Throw)throw new InvalidOperationException();return value;}public void Dispose()=>Disposals++; }
}
