using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V12DpiRenderTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunSta();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            throw new InvalidOperationException("150% WPF render contract failed", failure);
        Console.WriteLine("PASS v1.2 150% WPF render and opacity");
    }

    private static void RunSta()
    {
        var nowService = new FixedNowPlayingService(new NowPlayingSnapshot(
            true,
            "MusicPlayer.exe",
            "This deliberately long title must overflow and remain clipped inside the card",
            "DeskCanvas QA Artist",
            "Acceptance Album",
            null,
            NowPlayingState.Paused,
            TimeSpan.FromSeconds(31),
            TimeSpan.FromSeconds(60),
            true,
            true,
            true,
            false));
        var metricsService = new FixedMetricsService(new SystemMetricsSnapshot(
            MetricValue.From(65, "65%"),
            MetricValue.From(72, "72%  11.5 GB / 16 GB"),
            MetricValue.From(31, "31%"),
            MetricValue.From(139 * 1024, "139 KB/秒"),
            MetricValue.From(14.5 * 1024, "14.5 KB/秒")));

        var now = new NowPlayingItemContent(
            new CanvasItem
            {
                ContentKind = CanvasContentKinds.NowPlaying,
                Width = 360,
                Height = 220,
                Opacity = 1
            },
            nowService);
        var system = new SystemMonitorItemContent(
            new CanvasItem
            {
                ContentKind = CanvasContentKinds.SystemMonitor,
                Width = 300,
                Height = 180,
                Opacity = 1
            },
            metricsService);
        var clock = new ClockItemContent(
            new CanvasItem
            {
                ContentKind = CanvasContentKinds.Clock,
                Width = 300,
                Height = 150,
                Opacity = 1,
                Clock = new ClockOptions
                {
                    Style = ClockStyle.Split,
                    ShowSeconds = true,
                    ShowMonthDay = true,
                    ShowYear = true
                }
            });

        try
        {
            RenderAt150(now.View, 360, 220, "now-playing-150.png");
            RenderAt150(system.View, 300, 180, "system-300x180-150.png");
            RenderAt150(clock.View, 300, 150, "clock-150.png");
        }
        finally
        {
            now.Dispose();
            system.Dispose();
            clock.Dispose();
        }
    }

    private static void RenderAt150(
        UIElement content,
        double width,
        double height,
        string fileName)
    {
        var host = new Grid
        {
            Width = width,
            Height = height,
            LayoutTransform = new ScaleTransform(1.5, 1.5)
        };
        host.Children.Add(content);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var expected = new Size(width * 1.5, height * 1.5);
        if (Math.Abs(host.DesiredSize.Width - expected.Width) > .5 ||
            Math.Abs(host.DesiredSize.Height - expected.Height) > .5)
        {
            throw new InvalidOperationException(
                $"{fileName}: expected {expected}, actual {host.DesiredSize}");
        }
        host.Arrange(new Rect(new Point(), expected));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)expected.Width,
            (int)expected.Height,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(host);
        var center = new byte[4];
        bitmap.CopyPixels(
            new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1),
            center,
            4,
            0);
        if (center[3] != byte.MaxValue)
            throw new InvalidOperationException($"{fileName}: center alpha was {center[3]}");

        var outputRoot = Environment.GetEnvironmentVariable("DESKCANVAS_QA_RENDER_ROOT");
        if (string.IsNullOrWhiteSpace(outputRoot)) return;
        Directory.CreateDirectory(outputRoot);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(outputRoot, fileName));
        encoder.Save(stream);
    }

    private sealed class FixedNowPlayingService(NowPlayingSnapshot snapshot)
        : INowPlayingService
    {
        public NowPlayingSnapshot Snapshot { get; } = snapshot;
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }
        public IDisposable Acquire() => EmptyLease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true);
        public Task<bool> PlayPauseAsync() => Task.FromResult(true);
        public Task<bool> NextAsync() => Task.FromResult(true);
        public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(false);
        public void Dispose() { }
    }

    private sealed class FixedMetricsService(SystemMetricsSnapshot snapshot)
        : ISystemMetricsService
    {
        public SystemMetricsSnapshot Snapshot { get; } = snapshot;
        public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }
        public IDisposable Acquire() => EmptyLease.Instance;
        public void Dispose() { }
    }

    private sealed class EmptyLease : IDisposable
    {
        internal static readonly EmptyLease Instance = new();
        public void Dispose() { }
    }
}
