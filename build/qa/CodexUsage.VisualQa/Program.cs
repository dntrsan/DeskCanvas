using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;

if (args is ["--probe"])
{
    using var service = new CodexUsageService();
    using var lease = service.Acquire();
    var deadline = DateTime.UtcNow.AddSeconds(20);
    while (!service.Snapshot.HasUsage && service.Snapshot.Error is null && DateTime.UtcNow < deadline)
        Thread.Sleep(100);
    var current = service.Snapshot;
    Console.WriteLine($"source={current.Source}; remaining={current.Primary?.RemainingPercent}; plan={current.PlanType}; error={current.Error}");
    return current.HasUsage ? 0 : 1;
}

if (args is ["--history-probe"])
{
    Console.WriteLine($"USERPROFILE={Environment.GetEnvironmentVariable("USERPROFILE")}; specialUser={Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}; local={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
    var profile = Environment.GetEnvironmentVariable("USERPROFILE")!;
    var newest = new DirectoryInfo(Path.Combine(profile, ".codex", "sessions")).EnumerateFiles("*.jsonl", SearchOption.AllDirectories).OrderByDescending(file => file.LastWriteTimeUtc).First();
    var direct = CodexUsageService.ReadLatestRateLimit(newest);
    Console.WriteLine($"newest={newest.FullName}; direct={direct?.Primary?.RemainingPercent}");
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var current = CodexUsageService.ReadFromHistory("probe");
    Console.WriteLine($"elapsed={watch.Elapsed}; source={current.Source}; remaining={current.Primary?.RemainingPercent}; plan={current.PlanType}; error={current.Error}");
    return current.HasUsage ? 0 : 1;
}

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) return 2;
Directory.CreateDirectory(args[0]);

RenderSnapshot("healthy", new CodexUsageSnapshot(
    new CodexLimitWindow(27, 10_080, DateTimeOffset.Now.AddDays(4).AddHours(7)),
    null,
    "plus",
    null,
    false,
    DateTimeOffset.Now,
    CodexUsageSource.Preview,
    null));
RenderSnapshot("warning", new CodexUsageSnapshot(
    new CodexLimitWindow(84, 300, DateTimeOffset.Now.AddHours(2).AddMinutes(18)),
    new CodexLimitWindow(42, 10_080, DateTimeOffset.Now.AddDays(3)),
    "plus",
    "12.5",
    false,
    DateTimeOffset.Now.AddMinutes(-2),
    CodexUsageSource.Live,
    null));
RenderSnapshot("offline", new CodexUsageSnapshot(
    new CodexLimitWindow(93, 10_080, DateTimeOffset.Now.AddHours(6)),
    null,
    "plus",
    "0",
    false,
    DateTimeOffset.Now.AddMinutes(-8),
    CodexUsageSource.LocalHistory,
    "Codex App Serverへ接続できませんでした。"));

Console.WriteLine("PASS Codex Usage liquid-glass render at 100% and 150% DPI");
return 0;

void RenderSnapshot(string name, CodexUsageSnapshot snapshot)
{
    using var content = new CodexUsageItemContent(new FixedService(snapshot));
    content.SetActive(true);
    EnsureMinimalGlass(content.View);
    foreach (var scale in new[] { 1d, 1.5d })
        Render(content.View, 360, 160, scale, Path.Combine(args[0], $"codex-{name}-{(int)(scale * 100)}.png"));
    content.SetActive(false);
}

static void EnsureMinimalGlass(DependencyObject root)
{
    var progressBars = 0;
    foreach (var current in Descendants(root))
    {
        if (current is System.Windows.Shapes.Ellipse or System.Windows.Shapes.Path)
            throw new InvalidOperationException("Codex Usage must not contain a decorative ring or gauge");
        if (current is System.Windows.Controls.Border border &&
            (border.Background is GradientBrush or DrawingBrush || border.BorderBrush is GradientBrush or DrawingBrush))
            throw new InvalidOperationException("Codex Usage contains decorative gradient/noise layers");
        if (current is System.Windows.Controls.Border { Effect: System.Windows.Media.Effects.DropShadowEffect shadow } &&
            (shadow.BlurRadius > 8 || shadow.ShadowDepth > 1 || shadow.Opacity > .14))
            throw new InvalidOperationException("Codex Usage shadow is stronger than the minimal-glass contract");
        if (current is System.Windows.Controls.Border { Height: 2 }) progressBars++;
    }
    if (progressBars < 4) throw new InvalidOperationException("Codex Usage thin progress indicators are missing");
}

static IEnumerable<DependencyObject> Descendants(DependencyObject root)
{
    yield return root;
    for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        foreach (var child in Descendants(VisualTreeHelper.GetChild(root, index)))
            yield return child;
}

static void Render(UIElement content, double width, double height, double scale, string output)
{
    var host = new Grid
    {
        Width = width,
        Height = height,
        Background = new LinearGradientBrush(
            System.Windows.Media.Color.FromRgb(73, 91, 120),
            System.Windows.Media.Color.FromRgb(28, 38, 61),
            35),
        LayoutTransform = new ScaleTransform(scale, scale)
    };
    host.Children.Add(content);
    var size = new Size(width * scale, height * scale);
    host.Measure(size);
    host.Arrange(new Rect(new Point(), size));
    host.UpdateLayout();
    var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width), (int)Math.Round(size.Height), 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(host);
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = File.Create(output);
    encoder.Save(stream);
    host.Children.Clear();
}

sealed class FixedService(CodexUsageSnapshot snapshot) : ICodexUsageService
{
    public CodexUsageSnapshot Snapshot { get; } = snapshot;
    public event EventHandler<CodexUsageSnapshot>? SnapshotChanged { add { } remove { } }
    public IDisposable Acquire() => Lease.Instance;
    public void Dispose() { }
}

sealed class Lease : IDisposable
{
    internal static Lease Instance { get; } = new();
    public void Dispose() { }
}
