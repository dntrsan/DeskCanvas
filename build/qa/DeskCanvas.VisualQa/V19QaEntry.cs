using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V19QaEntry
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0])) { Console.Error.WriteLine("Pass an absolute output directory."); return 2; }
        Directory.CreateDirectory(args[0]);
        foreach (var scale in new[] { 1d, 1.5d })
        foreach (var style in Enum.GetValues<NowPlayingProgressStyle>())
        {
            RenderNowPlaying(args[0], style, false, scale);
            RenderNowPlaying(args[0], style, true, scale);
        }
        RenderSystem(args[0], 1d); RenderSystem(args[0], 1.5d);
        Console.WriteLine("PASS v1.9 direct STA QA: styles, spectrum, seek, center, system, alpha");
        return 0;
    }

    private static void RenderNowPlaying(string directory, NowPlayingProgressStyle style, bool spectrum, double scale)
    {
        var item = new CanvasItem { ContentKind = CanvasContentKinds.NowPlaying, Width = 360, Height = 220, Opacity = 1, Theme = WidgetThemeKind.Dark, NowPlaying = new NowPlayingOptions { ShowSpectrum = spectrum, ProgressStyle = style } };
        using var content = new NowPlayingItemContent(item, new Media(), () => new Spectrum());
        content.SetActive(true);
        var root = (FrameworkElement)content.View;
        AssertNowPlaying(root, spectrum, style);
        Render(root, 360, 220, scale, Path.Combine(directory, $"v19-{style.ToString().ToLowerInvariant()}-spectrum-{(spectrum ? "on" : "off")}-{(scale == 1 ? "100" : "150")}.png"));
        content.SetActive(false);
    }

    private static void AssertNowPlaying(FrameworkElement root, bool spectrum, NowPlayingProgressStyle style)
    {
        Layout(root, 360, 220);
        var buttons = Descendants<Button>(root).ToArray();
        if (buttons.Length != 3) throw new InvalidOperationException("transport button count");
        var rects = buttons.Select(button => button.TransformToAncestor(root).TransformBounds(new Rect(new Point(), button.RenderSize))).ToArray();
        var center = (rects.Min(rect => rect.Left) + rects.Max(rect => rect.Right)) / 2;
        if (Math.Abs(center - 180) > .75) throw new InvalidOperationException("transport trio moved off centre");
        var slider = Descendants<Slider>(root).SingleOrDefault() ?? throw new InvalidOperationException("seek slider missing");
        if (!slider.IsEnabled || slider.Maximum < 59 || slider.ActualHeight < 15) throw new InvalidOperationException("seek hit target missing");
        var visualizer = Descendants<StackPanel>(root).FirstOrDefault(panel => panel.Children.Count == 7);
        if (visualizer is null || (spectrum ? visualizer.Visibility != Visibility.Visible : visualizer.Visibility != Visibility.Collapsed)) throw new InvalidOperationException("spectrum visibility incorrect");
        if (style != NowPlayingProgressStyle.Simple && !Descendants<Canvas>(root).Any(canvas => canvas.Children.Count == 2)) throw new InvalidOperationException("custom timeline missing");
    }

    private static void RenderSystem(string directory, double scale)
    {
        var snapshot = new SystemMetricsSnapshot(MetricValue.From(64, "64%"), MetricValue.From(55, "8.8 GB / 16 GB"), MetricValue.From(37, "37%"), MetricValue.From(12000, "12 KB/秒"), MetricValue.From(3000, "3 KB/秒"));
        using var content = new SystemMonitorItemContent(new CanvasItem { ContentKind = CanvasContentKinds.SystemMonitor, Width = 320, Height = 390, Opacity = 1, Theme = WidgetThemeKind.Ocean }, new Metrics(snapshot));
        content.SetActive(true);
        var root = (FrameworkElement)content.View; Layout(root, 320, 390);
        if (Descendants<TextBlock>(root).Count() < 8) throw new InvalidOperationException("system content incomplete");
        Render(root, 320, 390, scale, Path.Combine(directory, $"v19-system-{(scale == 1 ? "100" : "150")}.png"));
    }

    private static void Layout(FrameworkElement root, double width, double height) { root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); }
    private static void Render(FrameworkElement root, double width, double height, double scale, string output)
    {
        var host = new Grid { Width = width, Height = height, LayoutTransform = new ScaleTransform(scale, scale) }; host.Children.Add(root);
        var size = new Size(width * scale, height * scale); host.Measure(size); host.Arrange(new Rect(new Point(), size)); host.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width), (int)Math.Round(size.Height), 96, 96, PixelFormats.Pbgra32); bitmap.Render(host);
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0); if (pixel[3] != 255) throw new InvalidOperationException($"{Path.GetFileName(output)} alpha {pixel[3]}");
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(output); png.Save(stream);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject { for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++) { var child = VisualTreeHelper.GetChild(parent, index); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; } }
    private sealed class Media : INowPlayingService { public NowPlayingSnapshot Snapshot { get; } = new(true, "DeskCanvas.Qa!player", "QA title", "artist", "album", null, NowPlayingState.Playing, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), true, true, true, true, DateTimeOffset.UtcNow); public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add { } remove { } } public IDisposable Acquire() => Lease.Instance; public Task<bool> PreviousAsync() => Task.FromResult(true); public Task<bool> PlayPauseAsync() => Task.FromResult(true); public Task<bool> NextAsync() => Task.FromResult(true); public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true); public void Dispose() { } }
    private sealed class Spectrum : IAudioSpectrumReader { public IReadOnlyList<double> ReadBands() => [0, .2, .4, .6, .4, .2, 0]; public void Dispose() { } }
    private sealed class Metrics(SystemMetricsSnapshot snapshot) : ISystemMetricsService { public SystemMetricsSnapshot Snapshot { get; } = snapshot; public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged { add { } remove { } } public IDisposable Acquire() => Lease.Instance; public void Dispose() { } }
    private sealed class Lease : IDisposable { internal static readonly Lease Instance = new(); public void Dispose() { } }
}
