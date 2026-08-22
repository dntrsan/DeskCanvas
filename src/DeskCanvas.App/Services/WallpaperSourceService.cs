using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DeskCanvas.Core;
using Microsoft.Win32;
using SkiaSharp;

namespace DeskCanvas.App.Services;

internal readonly record struct WallpaperBackdrop(
    SKBitmap Image,
    double DestLeft,
    double DestTop,
    double DestWidth,
    double DestHeight,
    WallpaperFit Fit,
    SKColor Background);

/// <summary>
/// Reads the current desktop wallpaper (per-monitor when Windows reports it).
/// Frosting happens after refraction on the glass plate.
/// </summary>
internal sealed class WallpaperSourceService : IDisposable
{
    internal static WallpaperSourceService Shared { get; } = new();

    private const uint SpiGetDeskWallpaper = 0x0073;
    private const int ClsctxAll = 23;
    private static readonly Guid DesktopWallpaperClsid = new("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD");
    private static readonly Guid DesktopWallpaperIid = new("B92B56A8-8B5C-4B9C-B89B-3586DE19B3A1");

    private readonly object sync = new();
    private readonly Dictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private IDesktopWallpaper? desktop;
    private DispatcherTimer? poll;
    private string fingerprint = "";
    private bool listening;
    private bool disposed;

    internal event EventHandler? Changed;

    internal bool TryGet(double screenX, double screenY, out WallpaperBackdrop backdrop)
    {
        EnsureListening();
        backdrop = default;
        try
        {
            if (TryGetFromCom(screenX, screenY, out backdrop)) return true;
            return TryGetFallback(screenX, screenY, out backdrop);
        }
        catch (Exception)
        {
            return TryGetFallback(screenX, screenY, out backdrop);
        }
    }

    private bool TryGetFromCom(double screenX, double screenY, out WallpaperBackdrop backdrop)
    {
        backdrop = default;
        var api = EnsureCom();
        if (api is null) return false;
        if (api.GetMonitorDevicePathCount(out var count) != 0 || count == 0) return false;
        if (api.GetPosition(out var position) != 0) position = (int)WallpaperFit.Fill;
        var fit = Enum.IsDefined(typeof(WallpaperFit), position) ? (WallpaperFit)position : WallpaperFit.Fill;
        var background = ReadBackground(api);

        for (uint index = 0; index < count; index++)
        {
            if (api.GetMonitorDevicePathAt(index, out var idPtr) != 0 || idPtr == IntPtr.Zero) continue;
            var monitorId = PtrToString(idPtr);
            if (api.GetMonitorRECT(monitorId, out var rect) != 0) continue;
            if (screenX < rect.Left || screenX >= rect.Right || screenY < rect.Top || screenY >= rect.Bottom) continue;

            var path = ReadWallpaperPath(api, monitorId);
            var bitmap = Load(path);
            if (bitmap is null) return false;
            backdrop = new WallpaperBackdrop(
                bitmap,
                rect.Left,
                rect.Top,
                Math.Max(1, rect.Right - rect.Left),
                Math.Max(1, rect.Bottom - rect.Top),
                fit,
                background);
            return true;
        }

        return false;
    }

    private bool TryGetFallback(double screenX, double screenY, out WallpaperBackdrop backdrop)
    {
        backdrop = default;
        var path = ReadSpiWallpaper() ?? TranscodedWallpaperPath();
        var bitmap = Load(path);
        if (bitmap is null) return false;
        var bounds = FallbackMonitor(screenX, screenY);
        backdrop = new WallpaperBackdrop(bitmap, bounds.Left, bounds.Top, bounds.Width, bounds.Height, WallpaperFit.Fill, new SKColor(28, 28, 30));
        return true;
    }

    private SKBitmap? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            path = TranscodedWallpaperPath();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        }

        var writeTime = File.GetLastWriteTimeUtc(path);
        var key = path + "|" + writeTime.Ticks;
        lock (sync)
        {
            if (cache.TryGetValue(key, out var hit)) return hit.Bitmap;
            try
            {
                var decoded = SKBitmap.Decode(path);
                if (decoded is null) return null;
                cache[key] = new CacheEntry(decoded, writeTime);
                return decoded;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    private IDesktopWallpaper? EnsureCom()
    {
        if (desktop is not null) return desktop;
        try
        {
            var clsid = DesktopWallpaperClsid;
            var iid = DesktopWallpaperIid;
            if (CoCreateInstance(in clsid, IntPtr.Zero, ClsctxAll, in iid, out var instance) == 0)
            {
                desktop = instance;
            }
        }
        catch (Exception)
        {
            desktop = null;
        }

        return desktop;
    }

    private void EnsureListening()
    {
        if (listening || disposed) return;
        listening = true;
        SystemEvents.UserPreferenceChanged += OnPreference;
        SystemEvents.DisplaySettingsChanged += OnDisplay;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        void Start()
        {
            if (poll is not null) return;
            poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            poll.Tick += (_, _) => RefreshIfChanged();
            poll.Start();
        }

        if (dispatcher.CheckAccess()) Start();
        else dispatcher.BeginInvoke(Start);
    }

    private void OnPreference(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.Color)
        {
            RefreshIfChanged();
        }
    }

    private void OnDisplay(object? sender, EventArgs e) => RefreshIfChanged();

    private void RefreshIfChanged()
    {
        try
        {
            var next = CurrentFingerprint();
            if (next == fingerprint) return;
            fingerprint = next;
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
        }
    }

    private string CurrentFingerprint()
    {
        var path = ReadSpiWallpaper() ?? TranscodedWallpaperPath() ?? "";
        var ticks = File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
        return path + "|" + ticks;
    }

    private static string? ReadWallpaperPath(IDesktopWallpaper api, string monitorId)
    {
        if (api.GetWallpaper(monitorId, out var pointer) != 0 || pointer == IntPtr.Zero) return null;
        var path = PtrToString(pointer);
        if (string.IsNullOrWhiteSpace(path) || Directory.Exists(path) || !File.Exists(path))
        {
            return TranscodedWallpaperPath();
        }

        return path;
    }

    private static SKColor ReadBackground(IDesktopWallpaper api)
    {
        if (api.GetBackgroundColor(out var color) != 0) return new SKColor(28, 28, 30);
        return new SKColor((byte)(color & 0xFF), (byte)((color >> 8) & 0xFF), (byte)((color >> 16) & 0xFF));
    }

    private static string? ReadSpiWallpaper()
    {
        var buffer = new StringBuilder(520);
        return SystemParametersInfo(SpiGetDeskWallpaper, (uint)buffer.Capacity, buffer, 0) && buffer.Length > 0
            ? buffer.ToString()
            : null;
    }

    private static string? TranscodedWallpaperPath()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        return File.Exists(path) ? path : null;
    }

    private static (double Left, double Top, double Width, double Height) FallbackMonitor(double x, double y)
    {
        foreach (var screen in System.Windows.Forms.Screen.AllScreens)
        {
            var b = screen.Bounds;
            if (x >= b.Left && x < b.Right && y >= b.Top && y < b.Bottom)
            {
                return (b.Left, b.Top, b.Width, b.Height);
            }
        }

        var primary = System.Windows.Forms.Screen.PrimaryScreen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
        return (primary.Left, primary.Top, primary.Width, primary.Height);
    }

    private static string PtrToString(IntPtr pointer)
    {
        try { return Marshal.PtrToStringUni(pointer) ?? ""; }
        finally { Marshal.FreeCoTaskMem(pointer); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        SystemEvents.UserPreferenceChanged -= OnPreference;
        SystemEvents.DisplaySettingsChanged -= OnDisplay;
        poll?.Stop();
        if (desktop is not null) Marshal.ReleaseComObject(desktop);
        desktop = null;
        lock (sync)
        {
            foreach (var entry in cache.Values) entry.Bitmap.Dispose();
            cache.Clear();
        }
    }

    private sealed record CacheEntry(SKBitmap Bitmap, DateTimeOffset Written);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [ComImport]
    [Guid("B92B56A8-8B5C-4B9C-B89B-3586DE19B3A1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        [PreserveSig] int SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [PreserveSig] int GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, out IntPtr wallpaper);
        [PreserveSig] int GetMonitorDevicePathAt(uint monitorIndex, out IntPtr monitorID);
        [PreserveSig] int GetMonitorDevicePathCount(out uint count);
        [PreserveSig] int GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out NativeRect displayRect);
        [PreserveSig] int SetBackgroundColor(uint color);
        [PreserveSig] int GetBackgroundColor(out uint color);
        [PreserveSig] int SetPosition(int position);
        [PreserveSig] int GetPosition(out int position);
        [PreserveSig] int SetSlideshow(IntPtr items);
        [PreserveSig] int GetSlideshow(out IntPtr items);
        [PreserveSig] int SetSlideshowOptions(int options, uint slideshowTick);
        [PreserveSig] int GetSlideshowOptions(out int options, out uint slideshowTick);
        [PreserveSig] int AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, int direction);
        [PreserveSig] int GetStatus(out int state);
        [PreserveSig] int Enable(int enable);
    }

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, int context, in Guid iid, [MarshalAs(UnmanagedType.Interface)] out IDesktopWallpaper instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, StringBuilder value, uint winIni);
}
