using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DeskCanvas.Core;
using Microsoft.Win32;
using SkiaSharp;

namespace DeskCanvas.App.Services;

internal readonly record struct GlassBackdrop(
    SKBitmap Image,
    double OriginX,
    double OriginY,
    double DestWidth,
    double DestHeight,
    WallpaperFit Fit,
    bool PixelMapped,
    SKColor Background,
    long Generation);

/// <summary>
/// Captures Progman/WorkerW (wallpaper + desktop icons) as a 1:1 bitmap.
/// Widgets are separate top-level windows, so they are not in the capture.
/// Falls back to the wallpaper file when PrintWindow returns a blank frame.
/// </summary>
internal sealed class DesktopBackdropService : IDisposable
{
    internal static DesktopBackdropService Shared { get; } = new();

    private const uint PwRenderFullContent = 2;
    private const uint SrcCopy = 0x00CC0020;
    private readonly object sync = new();
    private SKBitmap? capture;
    private double originX;
    private double originY;
    private long generation = 1;
    private DispatcherTimer? poll;
    private bool listening;
    private bool disposed;
    private string fingerprint = "";

    internal event EventHandler? Changed;

    internal long Generation
    {
        get { lock (sync) return generation; }
    }

    internal bool Use(double screenX, double screenY, Action<GlassBackdrop> consume)
    {
        EnsureListening();
        lock (sync)
        {
            EnsureCapture();
            if (capture is not null)
            {
                var (imageX, imageY) = BackdropMapping.ToImage(screenX, screenY, originX, originY);
                if (BackdropMapping.IsInside(imageX, imageY, capture.Width, capture.Height))
                {
                    consume(new GlassBackdrop(
                        capture,
                        originX,
                        originY,
                        capture.Width,
                        capture.Height,
                        WallpaperFit.Fill,
                        true,
                        new SKColor(28, 28, 30),
                        generation));
                    return true;
                }
            }
        }

        if (WallpaperSourceService.Shared.TryGet(screenX, screenY, out var wallpaper))
        {
            consume(new GlassBackdrop(
                wallpaper.Image,
                wallpaper.DestLeft,
                wallpaper.DestTop,
                wallpaper.DestWidth,
                wallpaper.DestHeight,
                wallpaper.Fit,
                false,
                wallpaper.Background,
                generation));
            return true;
        }

        return false;
    }

    private void EnsureListening()
    {
        if (listening || disposed) return;
        listening = true;
        SystemEvents.UserPreferenceChanged += OnPreference;
        SystemEvents.DisplaySettingsChanged += OnDisplay;
        WallpaperSourceService.Shared.Changed += (_, _) => RequestRefresh();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        void Start()
        {
            if (poll is not null) return;
            poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            poll.Tick += (_, _) => RequestRefresh();
            poll.Start();
        }

        if (dispatcher.CheckAccess()) Start();
        else dispatcher.BeginInvoke(Start);
    }

    private void OnPreference(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.Color)
        {
            RequestRefresh();
        }
    }

    private void OnDisplay(object? sender, EventArgs e) => RequestRefresh();

    private void RequestRefresh()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            lock (sync) EnsureCapture(force: true);
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            lock (sync) EnsureCapture(force: true);
            Changed?.Invoke(this, EventArgs.Empty);
        }, DispatcherPriority.Background);
    }

    private void EnsureCapture(bool force = false)
    {
        var next = CurrentFingerprint();
        if (!force && capture is not null && next == fingerprint) return;
        fingerprint = next;

        var host = DesktopWindowService.FindDesktopHost();
        SKBitmap? nextBitmap = null;
        var left = 0d;
        var top = 0d;
        if (host != IntPtr.Zero)
        {
            nextBitmap = CaptureHost(host, out left, out top);
        }

        if (nextBitmap is not null && IsUniform(nextBitmap))
        {
            nextBitmap.Dispose();
            nextBitmap = null;
        }

        capture?.Dispose();
        capture = nextBitmap;
        originX = left;
        originY = top;
        generation++;
    }

    private static SKBitmap? CaptureHost(IntPtr host, out double left, out double top)
    {
        left = 0;
        top = 0;
        if (!GetWindowRect(host, out var rect)) return null;
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width < 16 || height < 16) return null;
        left = rect.Left;
        top = rect.Top;

        try
        {
            using var bitmap = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            try
            {
                if (!PrintWindow(host, hdc, PwRenderFullContent) && !PrintWindow(host, hdc, 0))
                {
                    BitBltFromWindow(host, hdc, width, height);
                }
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            var copied = CopyBitmap(bitmap);
            if (copied is not null && IsUniform(copied))
            {
                copied.Dispose();
                copied = CopyAfterBitBlt(host, width, height);
            }

            return copied;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static SKBitmap? CopyAfterBitBlt(IntPtr host, int width, int height)
    {
        try
        {
            using var bitmap = new System.Drawing.Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            try
            {
                BitBltFromWindow(host, hdc, width, height);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }

            return CopyBitmap(bitmap);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void BitBltFromWindow(IntPtr host, IntPtr dest, int width, int height)
    {
        var source = GetWindowDC(host);
        if (source == IntPtr.Zero) return;
        try
        {
            BitBlt(dest, 0, 0, width, height, source, 0, 0, SrcCopy);
        }
        finally
        {
            _ = ReleaseDC(host, source);
        }
    }

    private static SKBitmap? CopyBitmap(System.Drawing.Bitmap source)
    {
        var rect = new System.Drawing.Rectangle(0, 0, source.Width, source.Height);
        var bits = source.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var info = new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            var result = new SKBitmap(info);
            var dest = result.GetPixels();
            if (dest == IntPtr.Zero)
            {
                result.Dispose();
                return null;
            }

            var rowBytes = source.Width * 4;
            var row = new byte[rowBytes];
            for (var y = 0; y < source.Height; y++)
            {
                Marshal.Copy(bits.Scan0 + y * bits.Stride, row, 0, rowBytes);
                Marshal.Copy(row, 0, dest + y * result.RowBytes, rowBytes);
            }

            return result;
        }
        finally
        {
            source.UnlockBits(bits);
        }
    }

    private static bool IsUniform(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width < 2 || height < 2) return true;
        var pixels = bitmap.GetPixels();
        if (pixels == IntPtr.Zero) return true;
        var stride = bitmap.RowBytes;
        var b0 = Marshal.ReadByte(pixels, 0);
        var g0 = Marshal.ReadByte(pixels, 1);
        var r0 = Marshal.ReadByte(pixels, 2);
        var samples = new (int X, int Y)[]
        {
            (0, 0), (width - 1, 0), (0, height - 1), (width - 1, height - 1),
            (width / 2, height / 2), (width / 4, height / 2), (3 * width / 4, height / 2),
            (width / 2, height / 4), (width / 8, height / 8), (7 * width / 8, 7 * height / 8)
        };
        foreach (var (x, y) in samples)
        {
            var offset = y * stride + x * 4;
            if (Math.Abs(Marshal.ReadByte(pixels, offset) - b0) > 4 ||
                Math.Abs(Marshal.ReadByte(pixels, offset + 1) - g0) > 4 ||
                Math.Abs(Marshal.ReadByte(pixels, offset + 2) - r0) > 4)
            {
                return false;
            }
        }

        return true;
    }

    private static string CurrentFingerprint()
    {
        var host = DesktopWindowService.FindDesktopHost();
        var rectText = "0";
        if (host != IntPtr.Zero && GetWindowRect(host, out var rect))
        {
            rectText = $"{rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";
        }

        var transcoded = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        var ticks = File.Exists(transcoded) ? File.GetLastWriteTimeUtc(transcoded).Ticks : 0;
        return $"{host.ToInt64()}|{rectText}|{ticks}";
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        SystemEvents.UserPreferenceChanged -= OnPreference;
        SystemEvents.DisplaySettingsChanged -= OnDisplay;
        poll?.Stop();
        lock (sync)
        {
            capture?.Dispose();
            capture = null;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr window, IntPtr hdcBlt, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint raster);
}
