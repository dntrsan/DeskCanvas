using System.Runtime.InteropServices;

namespace DeskCanvas.App.Services;

internal interface IAudioPeakReader : IDisposable
{
    double ReadPeak();
}

internal static class AudioPeakMath
{
    internal static double Clamp(double peak) => double.IsFinite(peak) ? Math.Clamp(peak, 0, 1) : 0;

    internal static double BarHeight(double peak, int index)
    {
        var level = Clamp(peak);
        if (level <= .002) return 5;
        var contour = new[] { .58, .74, .91, 1d, .84, .68, .52 }[Math.Clamp(index, 0, 6)];
        return Math.Clamp(5 + level * contour * 17, 5, 22);
    }
}

/// <summary>Default-render peak reader. It intentionally reports level, not an FFT spectrum.</summary>
internal sealed class CoreAudioPeakReader : IAudioPeakReader
{
    private IMMDevice? device;
    private IAudioMeterInformation? meter;
    private bool disposed;

    public double ReadPeak()
    {
        if (disposed) return 0;
        try
        {
            if (meter is null)
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                try
                {
                    ThrowIfFailed(enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var next));
                    device = next;
                    var iid = typeof(IAudioMeterInformation).GUID;
                    ThrowIfFailed(device.Activate(ref iid, 23, IntPtr.Zero, out var endpoint));
                    meter = (IAudioMeterInformation)endpoint;
                }
                finally
                {
                    Release(enumerator);
                }
            }
            ThrowIfFailed(meter.GetPeakValue(out var value));
            return AudioPeakMath.Clamp(value);
        }
        catch
        {
            Release(meter); meter = null;
            Release(device); device = null;
            return 0;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Release(meter); meter = null;
        Release(device); device = null;
    }

    private static void ThrowIfFailed(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }
    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    private enum EDataFlow { eRender, eCapture, eAll }
    private enum ERole { eConsole, eMultimedia, eCommunications }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(EDataFlow dataFlow, int stateMask, out IntPtr devices);
        int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }
    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        int GetPeakValue(out float peak);
    }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }
}



internal sealed class AudioPeakLeaseController : IDisposable
{
    private readonly Func<IAudioPeakReader> factory;
    private IAudioPeakReader? reader;
    internal AudioPeakLeaseController(Func<IAudioPeakReader> factory) => this.factory = factory;
    internal void Poll(bool playing, Action<double> sink)
    {
        if (!playing) { Suspend(); sink(0); return; }
        try { reader ??= factory(); sink(AudioPeakMath.Clamp(reader.ReadPeak())); }
        catch { Suspend(); sink(0); }
    }
    internal void Suspend() { reader?.Dispose(); reader = null; }
    public void Dispose() => Suspend();
}
