using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using WpfBorder = System.Windows.Controls.Border;

/// <summary>
/// Offline STA acceptance renders for the active v2.0 WPF widget content.
/// This entry has no Application, window, user-data, or process dependencies.
/// </summary>
internal static class V20AdaptiveQaEntry
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
        var manifest = new List<string>
        {
            "v20 adaptive content STA render manifest",
            "No live DeskCanvas process, installation, registry, or user data was opened.",
            "System: 300DIP wide; Now Playing: 360 x 220DIP. Scale is RenderTargetBitmap scale."
        };

        foreach (var scale in new[] { 1d, 1.5d })
        {
            RenderSystems(args[0], manifest, scale);
            RenderProgressStyles(args[0], manifest, scale);
            RenderWaveMotion(args[0], manifest, scale);
            RenderSpectrum(args[0], manifest, scale);
            RenderNoSeek(args[0], manifest, scale);
        }

        File.WriteAllLines(Path.Combine(args[0], "manifest.txt"), manifest);
        Console.WriteLine($"PASS v2.0 adaptive STA QA; {manifest.Count - 3} render records at {args[0}");
        return 0;
    }

    private static void RenderSystems(string directory, List<string> manifest, double scale)
    {
        var configurations = new[]
        {
            ("all", true, true, true, true),
            ("cpu-only", true, false, false, false),
            ("network-only", false, false, false, true),
            ("metrics-only", true, true, true, false),
            ("all-hidden", false, false, false, false)
        };

        foreach (var configuration in configurations)
        {
            var item = new CanvasItem
            {
                ContentKind = CanvasContentKinds.SystemMonitor,
                Width = 300,
                Height = 390,
                CenterY = 600,
                Opacity = 1,
                Theme = WidgetThemeKind.Ocean,
                SystemMonitor = new SystemMonitorOptions
                {
                    ShowCpu = configuration.Item2,
                    ShowMemory = configuration.Item3,
                    ShowGpu = configuration.Item4,
                    ShowNetwork = configuration.Item5
                }
            };
            var initialTop = item.CenterY - item.Height / 2;
            using var content = new SystemMonitorItemContent(item, new FixedMetrics());
            content.SetActive(true);
            var root = (FrameworkElement)content.View;
            Layout(root, item.Width, item.Height);
            var divider = Field<WpfBorder>(content, "divider");
            var hasMetric = configuration.Item2 || configuration.Item3 || configuration.Item4;
            var expectedDivider = hasMetric && configuration.Item5;
            if ((divider.Visibility == Visibility.Visible) != expectedDivider)
                throw new InvalidOperationException($"system/{configuration.Item1}: divider state was {divider.Visibility}");
            var top = item.CenterY - item.Height / 2;
            if (Math.Abs(top - initialTop) > .01)
                throw new InvalidOperationException($"system/{configuration.Item1}: top edge moved {initialTop:0.##} -> {top:0.##}");
            if (item.Height < 96)
                throw new InvalidOperationException($"system/{configuration.Item1}: natural height {item.Height:0.##} under 96");
            var dpi = Dpi(scale);
            Render(root, item.Width, item.Height, scale, Path.Combine(directory, $"v20-system-{configuration.Item1}-{dpi}.png"));
            manifest.Add($"system {configuration.Item1} {dpi}% width={item.Width:0.##} desired={root.DesiredSize.Height:0.##} height={item.Height:0.##} top={top:0.##} divider={(divider.Visibility == Visibility.Visible ? "visible" : "collapsed")}");
            content.SetActive(false);
        }
    }

    private static void RenderProgressStyles(string directory, List<string> manifest, double scale)
    {
        foreach (var style in new[] { NowPlayingProgressStyle.Simple, NowPlayingProgressStyle.Wave })
        foreach (var ratio in new[] { 0d, .5d, 1d })
        {
            using var content = CreateNowPlaying(style, ratio, NowPlayingState.Paused, true, false, out _);
            content.SetActive(true);
            var root = (FrameworkElement)content.View;
            Layout(root, 360, 220);
            VerifyTimeline(content, style, true);
            Render(root, 360, 220, scale, Path.Combine(directory, $"v20-now-{style.ToString().ToLowerInvariant()}-{(int)(ratio * 100)}-{Dpi(scale)}.png"));
            manifest.Add($"now {style} ratio={(int)(ratio * 100)} {Dpi(scale)}% seek=true spectrum=false state=Paused");
            content.SetActive(false);
        }
    }

    private static void RenderWaveMotion(string directory, List<string> manifest, double scale)
    {
        using var playing = CreateNowPlaying(NowPlayingProgressStyle.Wave, .5d, NowPlayingState.Playing, true, false, out _);
        playing.SetActive(true);
        var root = (FrameworkElement)playing.View;
        Layout(root, 360, 220);
        SetField(playing, "wavePhase", 0d);
        Invoke(playing, "PaintTimeline");
        Render(root, 360, 220, scale, Path.Combine(directory, $"v20-wave-playing-phase0-{Dpi(scale)}.png"));
        SetField(playing, "wavePhase", WaveProgressMath.AdvancePhase(0, WaveProgressMath.RunningSpeed, 1));
        Invoke(playing, "PaintTimeline");
        Render(root, 360, 220, scale, Path.Combine(directory, $"v20-wave-playing-after-ticks-{Dpi(scale)}.png"));
        manifest.Add($"wave Playing {Dpi(scale)}% phase=0 -> phase={GetField<double>(playing, "wavePhase"):0.###}; geometry rendered from active WPF content");
        playing.SetActive(false);

        using var paused = CreateNowPlaying(NowPlayingProgressStyle.Wave, .5d, NowPlayingState.Paused, true, false, out _);
        paused.SetActive(true);
        var pausedRoot = (FrameworkElement)paused.View;
        Layout(pausedRoot, 360, 220);
        Render(pausedRoot, 360, 220, scale, Path.Combine(directory, $"v20-wave-paused-{Dpi(scale)}.png"));
        manifest.Add($"wave Paused {Dpi(scale)}% static frame");
        paused.SetActive(false);
    }

    private static void RenderSpectrum(string directory, List<string> manifest, double scale)
    {
        foreach (var spectrumOn in new[] { false, true })
        {
            using var content = CreateNowPlaying(NowPlayingProgressStyle.Wave, .5d, NowPlayingState.Playing, true, spectrumOn, out var readerFactory);
            content.SetActive(true);
            var root = (FrameworkElement)content.View;
            Layout(root, 360, 220);
            Invoke(content, "PlaybackTick");
            var visualizer = Field<StackPanel>(content, "visualizer");
            if ((visualizer.Visibility == Visibility.Visible) != spectrumOn)
                throw new InvalidOperationException($"spectrum {spectrumOn}: visualizer visibility mismatch");
            if (!spectrumOn && readerFactory.Created != 0)
                throw new InvalidOperationException("spectrum off created an FFT reader");
            if (spectrumOn && readerFactory.Created != 1)
                throw new InvalidOperationException("spectrum on did not create exactly one injected reader");
            Render(root, 360, 220, scale, Path.Combine(directory, $"v20-wave-spectrum-{(spectrumOn ? "on" : "off")}-{Dpi(scale)}.png"));
            manifest.Add($"spectrum {(spectrumOn ? "on" : "off")} {Dpi(scale)}% readerCreated={readerFactory.Created} visualizer={visualizer.Visibility}");
            content.SetActive(false);
        }
    }

    private static void RenderNoSeek(string directory, List<string> manifest, double scale)
    {
        using var content = CreateNowPlaying(NowPlayingProgressStyle.Wave, .5d, NowPlayingState.Paused, false, false, out _);
        content.SetActive(true);
        var root = (FrameworkElement)content.View;
        Layout(root, 360, 220);
        VerifyTimeline(content, NowPlayingProgressStyle.Wave, false);
        Render(root, 360, 220, scale, Path.Combine(directory, $"v20-wave-canseek-false-{Dpi(scale)}.png"));
        manifest.Add($"now Wave ratio=50 {Dpi(scale)}% seek=false spectrum=false state=Paused");
        content.SetActive(false);
    }

    private static NowPlayingItemContent CreateNowPlaying(NowPlayingProgressStyle style, double ratio, NowPlayingState state, bool canSeek, bool spectrum, out CountingSpectrumFactory readerFactory)
    {
        readerFactory = new CountingSpectrumFactory();
        var item = new CanvasItem
        {
            ContentKind = CanvasContentKinds.NowPlaying,
            Width = 360,
            Height = 220,
            Opacity = 1,
            Theme = WidgetThemeKind.Rose,
            NowPlaying = new NowPlayingOptions { ShowTimeline = true, ShowSpectrum = spectrum, ProgressStyle = style }
        };
        var media = new MutableMedia(ratio, state, canSeek);
        return new NowPlayingItemContent(item, media, readerFactory.Create);
    }

    private static void VerifyTimeline(object content, NowPlayingProgressStyle style, bool canSeek)
    {
        var seek = Field<Slider>(content, "seek");
        var track = Field<WpfBorder>(content, "timelineTrack");
        var fill = Field<WpfBorder>(content, "timelineFill");
        var art = Field<Canvas>(content, "timelineArt");
        if (seek.ActualHeight < 15 || seek.Opacity > .011 || seek.IsEnabled != canSeek || seek.IsHitTestVisible != canSeek)
            throw new InvalidOperationException($"{style}: transparent 16DIP seek surface is wrong");
        if (style == NowPlayingProgressStyle.Simple)
        {
            if (track.Visibility != Visibility.Visible || fill.Visibility != Visibility.Visible || art.Children.Count != 0)
                throw new InvalidOperationException("simple timeline was not a capsule-only track");
        }
        else
        {
            if (track.Visibility != Visibility.Collapsed || fill.Visibility != Visibility.Collapsed || art.Children.Count != 1)
                throw new InvalidOperationException("wave timeline left a capsule track or did not use one continuous path");
        }
    }

    private static T Field<T>(object value, string name) where T : class => (T)(value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(value) ?? throw new InvalidOperationException($"{name} field missing"));
    private static T GetField<T>(object value, string name) => (T)(value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(value) ?? throw new InvalidOperationException($"{name} field missing"));
    private static void SetField(object value, string name, object fieldValue) => (value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"{name} field missing")).SetValue(value, fieldValue);
    private static void Invoke(object value, string name) => (value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException($"{name} method missing")).Invoke(value, [null, EventArgs.Empty]);
    private static void Layout(FrameworkElement root, double width, double height) { root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout(); }
    private static string Dpi(double scale) => scale == 1 ? "100" : "150";

    private static void Render(FrameworkElement root, double width, double height, double scale, string output)
    {
        var host = new Grid { Width = width, Height = height, LayoutTransform = new ScaleTransform(scale, scale) };
        host.Children.Add(root);
        var size = new Size(width * scale, height * scale);
        host.Measure(size); host.Arrange(new Rect(new Point(), size)); host.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width), (int)Math.Round(size.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), pixel, 4, 0);
        if (pixel[3] != byte.MaxValue) throw new InvalidOperationException($"{Path.GetFileName(output)} center alpha was {pixel[3]}");
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(output); png.Save(stream);
    }

    private sealed class MutableMedia(double ratio, NowPlayingState state, bool canSeek) : INowPlayingService
    {
        public NowPlayingSnapshot Snapshot { get; } = new(true, "DeskCanvas.Qa!player", "Adaptive QA title", "QA Artist", "QA Album", null, state, TimeSpan.FromSeconds(60 * ratio), TimeSpan.FromSeconds(60), true, true, true, canSeek, DateTimeOffset.UtcNow);
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add { } remove { } }
        public IDisposable Acquire() => Lease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true); public Task<bool> PlayPauseAsync() => Task.FromResult(true); public Task<bool> NextAsync() => Task.FromResult(true); public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true); public void Dispose() { }
    }

    private sealed class FixedMetrics : ISystemMetricsService
    {
        public SystemMetricsSnapshot Snapshot { get; } = new(MetricValue.From(64, "64%"), MetricValue.From(55, "8.8 GB / 16 GB"), MetricValue.From(37, "37%"), MetricValue.From(12000, "12 KB/秒"), MetricValue.From(3000, "3 KB/秒"));
        public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged { add { } remove { } }
        public IDisposable Acquire() => Lease.Instance; public void Dispose() { }
    }

    private sealed class CountingSpectrumFactory
    {
        internal int Created { get; private set; }
        internal IAudioSpectrumReader Create() { Created++; return new Spectrum(); }
    }

    private sealed class Spectrum : IAudioSpectrumReader { public IReadOnlyList<double> ReadBands() => [0, .18, .55, 1, .46, .2, .08]; public void Dispose() { } }
    private sealed class Lease : IDisposable { internal static readonly Lease Instance = new(); public void Dispose() { } }
}
