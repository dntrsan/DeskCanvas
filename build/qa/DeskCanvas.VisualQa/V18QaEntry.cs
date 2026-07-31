using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V18QaEntry
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
        var artwork = CreateArtwork();
        RenderNowPlaying(args[0], artwork, 1d, "now-playing-auto-100.png");
        RenderNowPlaying(args[0], artwork, 1.5d, "now-playing-auto-150.png");
        RenderSystemStatus(args[0], 1d, "system-full-100.png");
        RenderSystemStatus(args[0], 1.5d, "system-full-150.png");
        Console.WriteLine("PASS v1.8 100%/150% isolated visual QA");
        return 0;
    }

    private static void RenderNowPlaying(
        string outputDirectory,
        byte[] artwork,
        double scale,
        string fileName)
    {
        var service = new V18NowPlayingService(new NowPlayingSnapshot(
            true,
            "MusicPlayer.exe",
            "A deliberately long title that verifies the artwork palette and clipped marquee viewport",
            "DeskCanvas QA Artist",
            "Auto palette acceptance album",
            artwork,
            NowPlayingState.Paused,
            TimeSpan.FromSeconds(31),
            TimeSpan.FromSeconds(60),
            true,
            true,
            true,
            true,
            DateTimeOffset.UtcNow));
        var content = new NowPlayingItemContent(
            new CanvasItem
            {
                ContentKind = CanvasContentKinds.NowPlaying,
                Width = 360,
                Height = 220,
                Opacity = 1,
                Theme = WidgetThemeKind.Auto
            },
            service);
        try
        {
            var root = (FrameworkElement)content.View;
            root.Measure(new Size(360, 220));
            root.Arrange(new Rect(0, 0, 360, 220));
            root.UpdateLayout();
            AssertTransportLayout(content, root);
            Render(root, 360, 220, scale, Path.Combine(outputDirectory, fileName));
        }
        finally
        {
            content.Dispose();
        }
    }

    private static void RenderSystemStatus(
        string outputDirectory,
        double scale,
        string fileName)
    {
        var service = new V18MetricsService(new SystemMetricsSnapshot(
            MetricValue.From(65, "65%"),
            MetricValue.From(72, "72%  11.5 GB / 16 GB"),
            MetricValue.From(31, "31%"),
            MetricValue.From(139 * 1024, "139 KB/秒"),
            MetricValue.From(14.5 * 1024, "14.5 KB/秒")));
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
            if (Math.Abs(item.Width - 320) > .1 || Math.Abs(item.Height - 390) > .1)
                throw new InvalidOperationException("full System Status size changed unexpectedly");
            Render(content.View, 320, 390, scale, Path.Combine(outputDirectory, fileName));
        }
        finally
        {
            content.Dispose();
        }
    }

    private static void AssertTransportLayout(
        NowPlayingItemContent content,
        FrameworkElement root)
    {
        var buttons = Descendants<Button>(root).ToArray();
        if (buttons.Length != 3)
            throw new InvalidOperationException($"expected 3 transport buttons, found {buttons.Length}");
        var bounds = buttons.Select(button => Bounds(button, root)).ToArray();
        var center = (bounds.Min(bounds => bounds.Left) + bounds.Max(bounds => bounds.Right)) / 2;
        if (Math.Abs(center - root.ActualWidth / 2) > .75)
            throw new InvalidOperationException($"transport center {center:0.##} was not card center {root.ActualWidth / 2:0.##}");

        var visualizer = PrivateField<StackPanel>(content, "visualizer");
        var spectrum = Bounds(visualizer, root);
        if (spectrum.Left < bounds.Max(bounds => bounds.Right))
            throw new InvalidOperationException("spectrum was not right-attached after transport controls");
    }

    private static void Render(
        UIElement content,
        double width,
        double height,
        double scale,
        string outputPath)
    {
        Console.WriteLine($"QA rendering {Path.GetFileName(outputPath)}");
        var host = new Grid
        {
            Width = width,
            Height = height,
            LayoutTransform = new ScaleTransform(scale, scale)
        };
        host.Children.Add(content);
        host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var expected = new Size(width * scale, height * scale);
        if (Math.Abs(host.DesiredSize.Width - expected.Width) > .5 ||
            Math.Abs(host.DesiredSize.Height - expected.Height) > .5)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(outputPath)} expected {expected}, actual {host.DesiredSize}");
        }
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
        bitmap.CopyPixels(
            new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1),
            center,
            4,
            0);
        if (center[3] != byte.MaxValue)
            throw new InvalidOperationException(
                $"{Path.GetFileName(outputPath)} center alpha was {center[3]}");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    private static byte[] CreateArtwork()
    {
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawRectangle(
                new SolidColorBrush(Color.FromRgb(224, 28, 82)),
                null,
                new Rect(0, 0, 96, 96));
            context.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(255, 166, 47)),
                null,
                new Point(70, 30),
                24,
                24);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromRgb(57, 36, 110)),
                null,
                new Point(34, 70),
                34,
                26);
        }
        var bitmap = new RenderTargetBitmap(96, 96, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static Rect Bounds(FrameworkElement element, FrameworkElement root) =>
        element.TransformToAncestor(root).TransformBounds(
            new Rect(new Point(), element.RenderSize));

    private static T PrivateField<T>(object owner, string name) =>
        (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(owner)
            ?? throw new MissingFieldException(owner.GetType().Name, name));

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (var nested in Descendants<T>(child))
                yield return nested;
        }
    }
}

internal sealed class V18NowPlayingService(NowPlayingSnapshot snapshot) : INowPlayingService
{
    public NowPlayingSnapshot Snapshot { get; } = snapshot;
    public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add { } remove { } }
    public IDisposable Acquire() => V18EmptyLease.Instance;
    public Task<bool> PreviousAsync() => Task.FromResult(true);
    public Task<bool> PlayPauseAsync() => Task.FromResult(true);
    public Task<bool> NextAsync() => Task.FromResult(true);
    public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
    public void Dispose() { }
}

internal sealed class V18MetricsService(SystemMetricsSnapshot snapshot) : ISystemMetricsService
{
    public SystemMetricsSnapshot Snapshot { get; } = snapshot;
    public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged { add { } remove { } }
    public IDisposable Acquire() => V18EmptyLease.Instance;
    public void Dispose() { }
}

internal sealed class V18EmptyLease : IDisposable
{
    internal static readonly V18EmptyLease Instance = new();
    public void Dispose() { }
}
