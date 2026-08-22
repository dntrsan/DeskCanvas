using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
{
    Console.Error.WriteLine("Pass one absolute output directory.");
    return 2;
}

Directory.CreateDirectory(args[0]);
Console.WriteLine("QA constructing fixed snapshots");

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
    false,
    DateTimeOffset.UtcNow));
var metricsService = new FixedMetricsService(new SystemMetricsSnapshot(
    MetricValue.From(65, "65%"),
    MetricValue.From(72, "72%  11.5 GB / 16 GB"),
    MetricValue.From(31, "31%"),
    MetricValue.From(139 * 1024, "139 KB/秒"),
    MetricValue.From(14.5 * 1024, "14.5 KB/秒")));

Console.WriteLine("QA constructing content");
var now = new NowPlayingItemContent(
    new CanvasItem
    {
        ContentKind = CanvasContentKinds.NowPlaying,
        Width = 360,
        Height = 220,
        Opacity = 1,
        Theme = WidgetThemeKind.Rose
    },
    nowService);
var nowGlass = new NowPlayingItemContent(
    new CanvasItem
    {
        ContentKind = CanvasContentKinds.NowPlaying,
        Width = 360,
        Height = 220,
        Opacity = 1,
        Theme = WidgetThemeKind.Rose,
        NowPlaying = new NowPlayingOptions { SurfaceStyle = WidgetSurfaceStyle.MinimalGlass }
    },
    nowService);
var system = new SystemMonitorItemContent(
    new CanvasItem
    {
        ContentKind = CanvasContentKinds.SystemMonitor,
        Width = 300,
        Height = 180,
        Opacity = 1,
        Theme = WidgetThemeKind.Ocean
    },
    metricsService);
var systemCurrent = SystemAt(317.34072022160666, 361, metricsService);
var systemCompact = SystemAt(240, 260, metricsService);
var systemStress = SystemAt(317.34072022160666, 361, metricsService);
EnsureMinimalSystem(systemCurrent.View);
EnsureMinimalSystem(systemCompact.View);
var clock = new ClockItemContent(
    new CanvasItem
    {
        ContentKind = CanvasContentKinds.Clock,
        Width = 300,
        Height = 150,
        Opacity = 1,
        Theme = WidgetThemeKind.Mint,
        Clock = new ClockOptions
        {
            Style = ClockStyle.Split,
            ShowSeconds = true,
            ShowMonthDay = true,
            ShowYear = true
        }
    });
var clockGlass = new ClockItemContent(
    new CanvasItem
    {
        ContentKind = CanvasContentKinds.Clock,
        Width = 300,
        Height = 150,
        Opacity = 1,
        Theme = WidgetThemeKind.Mint,
        Clock = new ClockOptions
        {
            Style = ClockStyle.Split,
            SurfaceStyle = WidgetSurfaceStyle.MinimalGlass,
            ShowSeconds = true,
            ShowMonthDay = true,
            ShowYear = true
        }
    });

try
{
    RenderAt150(now.View, 360, 220, Path.Combine(args[0], "now-playing-150.png"));
    EnsureMinimalGlass(nowGlass.View);
    RenderAtScale(nowGlass.View, 360, 220, 1, Path.Combine(args[0], "now-playing-glass-100.png"));
    RenderAtScale(nowGlass.View, 360, 220, 1.5, Path.Combine(args[0], "now-playing-glass-150.png"));
    RenderAt150(system.View, 300, 180, Path.Combine(args[0], "system-300x180-150.png"));
    RenderAtScale(systemCurrent.View, 317.34072022160666, 361, 1, Path.Combine(args[0], "system-current-317x361-100.png"));
    RenderAtScale(systemCurrent.View, 317.34072022160666, 361, 1.5, Path.Combine(args[0], "system-current-317x361-150.png"));
    RenderAtScale(systemCompact.View, 240, 260, 1, Path.Combine(args[0], "system-compact-240x260-100.png"));
    RenderAtScale(systemCompact.View, 240, 260, 1.5, Path.Combine(args[0], "system-compact-240x260-150.png"));
    StressResize(systemStress.View);
    RenderAt150(clock.View, 300, 150, Path.Combine(args[0], "clock-150.png"));
    EnsureMinimalGlass(clockGlass.View);
    RenderAtScale(clockGlass.View, 300, 150, 1, Path.Combine(args[0], "clock-glass-100.png"));
    RenderAtScale(clockGlass.View, 300, 150, 1.5, Path.Combine(args[0], "clock-glass-150.png"));
}
finally
{
    now.Dispose();
    nowGlass.Dispose();
    system.Dispose();
    systemCurrent.Dispose();
    systemCompact.Dispose();
    systemStress.Dispose();
    clock.Dispose();
    clockGlass.Dispose();
}

Console.WriteLine("PASS 150% WPF render and opacity");
return 0;

static SystemMonitorItemContent SystemAt(double width, double height, ISystemMetricsService metrics) => new(
    new CanvasItem
    {
        ContentKind = CanvasContentKinds.SystemMonitor,
        Width = width,
        Height = height,
        Opacity = 1,
        Theme = WidgetThemeKind.Ocean
    },
    metrics);

static void EnsureMinimalSystem(DependencyObject root)
{
    var labels = Descendants(root)
        .OfType<TextBlock>()
        .Select(text => text.Text)
        .ToHashSet(StringComparer.Ordinal);
    foreach (var required in new[] { "SYSTEM STATUS", "CPU", "RAM", "GPU", "DOWN", "UP" })
        if (!labels.Contains(required))
            throw new InvalidOperationException($"minimal System Status is missing {required}");

    foreach (var current in Descendants(root))
    {
        if (current is System.Windows.Shapes.Ellipse or System.Windows.Shapes.Path)
            throw new InvalidOperationException("minimal System Status contains a decorative gauge or icon");
        if (current is System.Windows.Controls.Border border &&
            (border.Background is GradientBrush or DrawingBrush || border.BorderBrush is GradientBrush or DrawingBrush))
            throw new InvalidOperationException("minimal System Status contains a decorative gradient/noise layer");
    }
}

static void EnsureMinimalGlass(UIElement content)
{
    if (content is not System.Windows.Controls.Border
        {
            Background: SolidColorBrush { Color: var background },
            BorderBrush: SolidColorBrush { Color: var border },
            CornerRadius.TopLeft: var radius,
            Effect: null
        } || background != Color.FromArgb(196, 26, 28, 33) || border != Color.FromArgb(54, 255, 255, 255) || radius != 20)
        throw new InvalidOperationException("minimal glass surface was not applied exactly");
}

static IEnumerable<DependencyObject> Descendants(DependencyObject root)
{
    yield return root;
    for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index)))
            yield return child;
}

static void StressResize(UIElement content)
{
    var host = new Grid();
    host.Children.Add(content);
    var initialCount = CountVisuals(content);
    for (var index = 0; index < 50; index++)
    {
        var size = index % 2 == 0 ? new Size(317.34072022160666, 361) : new Size(240, 260);
        host.Width = size.Width;
        host.Height = size.Height;
        host.Measure(size);
        host.Arrange(new Rect(new Point(), size));
        host.UpdateLayout();
    }
    if (CountVisuals(content) != initialCount)
        throw new InvalidOperationException("System Status visual tree grew during repeated resize");
    host.Children.Clear();
}

static int CountVisuals(DependencyObject root)
{
    var count = 1;
    for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        count += CountVisuals(VisualTreeHelper.GetChild(root, index));
    return count;
}

static void RenderAt150(
    UIElement content,
    double width,
    double height,
    string outputPath)
    => RenderAtScale(content, width, height, 1.5, outputPath);

static void RenderAtScale(
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
            $"{Path.GetFileName(outputPath)}: expected {expected}, actual {host.DesiredSize}");
    }
    host.Arrange(new Rect(new Point(), expected));

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
    if (center[3] < 190)
        throw new InvalidOperationException(
            $"{Path.GetFileName(outputPath)}: glass surface alpha was only {center[3]}");

    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = File.Create(outputPath);
    encoder.Save(stream);
    host.Children.Clear();
}

sealed class FixedNowPlayingService(NowPlayingSnapshot snapshot)
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

sealed class FixedMetricsService(SystemMetricsSnapshot snapshot)
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

sealed class EmptyLease : IDisposable
{
    internal static readonly EmptyLease Instance = new();
    public void Dispose() { }
}
