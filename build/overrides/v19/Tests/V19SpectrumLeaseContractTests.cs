using System.Runtime.CompilerServices;
using DeskCanvas.App.Services;

internal static class V19SpectrumLeaseContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var factories = 0; var first = new Reader(); var second = new Reader();
        var controller = new AudioSpectrumLeaseController(() => ++factories == 1 ? first : second);
        controller.Poll(false, _ => { });
        if (factories != 0) throw new InvalidOperationException("stopped spectrum created a reader");
        controller.Poll(true, _ => { });
        if (factories != 1 || first.Reads != 1) throw new InvalidOperationException("playing spectrum did not read");
        controller.Suspend();
        if (!first.Disposed) throw new InvalidOperationException("spectrum suspend did not dispose");
        controller.Poll(true, _ => { });
        if (factories != 2 || second.Reads != 1) throw new InvalidOperationException("spectrum did not recreate after suspend");
        controller.Dispose();
        if (!second.Disposed) throw new InvalidOperationException("spectrum dispose did not release reader");
        Console.WriteLine("PASS v1.9 audio spectrum lease lifecycle");
    }
    private sealed class Reader : IAudioSpectrumReader
    {
        internal int Reads; internal bool Disposed;
        public IReadOnlyList<double> ReadBands() { Reads++; return new double[7]; }
        public void Dispose() => Disposed = true;
    }
}
