using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V18QaEntry2
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
        {
            Console.Error.WriteLine("Pass one absolute output directory.");
            return 2;
        }

        Directory.CreateDirectory(args[0]);
        var artwork = (byte[])InvokeV18("CreateArtwork")!;
        InvokeV18("RenderNowPlaying", args[0], artwork, 1d, "now-playing-auto-100.png");
        InvokeV18("RenderNowPlaying", args[0], artwork, 1.5d, "now-playing-auto-150.png");
        RenderStreamingSystem(args[0], 1d, "system-full-100.png");
        RenderStreamingSystem(args[0], 1.5d, "system-full-150.png");
        Console.WriteLine("PASS v1.8 streaming System Status 100%/150% isolated visual QA");
        return 0;
    }

    private static object? InvokeV18(string name, params object?[] arguments)
    {
        var method = typeof(V18QaEntry).GetMethod(
            name,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(V18QaEntry), name);
        try
        {
            return method.Invoke(null, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo
                .Capture(error.InnerException)
                .Throw();
            throw;
        }
    }

    private static void RenderStreamingSystem(
        string outputDirectory,
        double scale,
        string fileName)
    {
        var initial = Snapshot(20 * 1024, 9 * 1024);
        var service = new V18StreamingMetricsService(initial);
        var item = new CanvasItem
        {
            ContentKind = CanvasContentKinds.SystemMonitor,
            Width = 320,
            Height = 390,
            Opacity = 1,
            Theme = WidgetThemeKind.Ocean
        };
        var content = new SystemMonitorItemContent(item, service);
        try
        {
            content.SetActive(true);
            var down = new[] { 20d, 80d, 45d, 130d, 70d, 160d, 100d, 139d, 92d, 148d };
            var up = new[] { 8d, 26d, 14d, 48d, 22d, 56d, 31d, 43d, 18d, 37d };
            for (var index = 0; index < down.Length; index++)
            {
                service.Emit(Snapshot(down[index] * 1024, up[index] * 1024));
                Pump(TimeSpan.FromMilliseconds(42));
            }
            Pump(TimeSpan.FromMilliseconds(760));
            Render((FrameworkElement)content.View, 320, 390, scale, Path.Combine(outputDirectory, fileName));
        }
        finally
        {
            content.Dispose();
        }
    }

    private static SystemMetricsSnapshot Snapshot(double down, double up) => new(
        MetricValue.From(65, "65%"),
        MetricValue.From(72, "72%  11.5 GB / 16 GB"),
        MetricValue.From(31, "31%"),
        MetricValue.From(down, $"{down / 1024d:0} KB/秒"),
        MetricValue.From(up, $"{up / 1024d:0} KB/秒"));

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var stop = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration
        };
        stop.Tick += (_, _) =>
        {
            stop.Stop();
            frame.Continue = false;
        };
        stop.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Render(
        FrameworkElement content,
        double width,
        double height,
        double scale,
        string outputPath)
    {
        Console.WriteLine($"QA rendering {Path.GetFileName(outputPath)}");
        var host = new System.Windows.Controls.Grid
        {
            Width = width,
            Height = height,
            LayoutTransform = new ScaleTransform(scale, scale)
        };
        host.Children.Add(content);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var expected = new Size(width * scale, height * scale);
        host.Arrange(new Rect(new Point(), expected));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap(
            (int)Math.Round(expected.Width),
            (int)Math.Round(expected.Height),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(host);
        var center = new byte[4];
        bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), center, 4, 0);
        if (center[3] != byte.MaxValue)
            throw new InvalidOperationException($"{Path.GetFileName(outputPath)} center alpha was {center[3]}");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}

internal sealed class V18StreamingMetricsService(SystemMetricsSnapshot initial) : ISystemMetricsService
{
    public SystemMetricsSnapshot Snapshot { get; private set; } = initial;
    public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged;
    public IDisposable Acquire() => V18EmptyLease.Instance;
    internal void Emit(SystemMetricsSnapshot snapshot)
    {
        Snapshot = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }
    public void Dispose() { }
}
