using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V19OptionsAndVisualContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyOptionsRoundTrip();
        VerifyImmediateNetworkHistory();
        RunSta(VerifyTimelineAndSpectrum);
        Console.WriteLine("PASS v1.9 options, instant network graph, timeline styles, and spectrum switch");
    }

    private static void VerifyOptionsRoundTrip()
    {
        var defaults = new NowPlayingOptions();
        if (!defaults.ShowSpectrum || defaults.ProgressStyle != NowPlayingProgressStyle.Simple)
            throw new InvalidOperationException("v1.9 defaults changed");

        var custom = new NowPlayingOptions { ShowSpectrum = false, ProgressStyle = NowPlayingProgressStyle.Hearts };
        var clone = custom.Clone();
        if (clone.ShowSpectrum || clone.ProgressStyle != NowPlayingProgressStyle.Hearts)
            throw new InvalidOperationException("v1.9 clone lost options");

        custom.ProgressStyle = (NowPlayingProgressStyle)12345;
        if (custom.Clone().ProgressStyle != NowPlayingProgressStyle.Simple)
            throw new InvalidOperationException("unknown clone enum was not normalized");

        var root = Path.Combine(Path.GetTempPath(), "DeskCanvas-v19-options-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var id = Guid.NewGuid();
            File.WriteAllText(Path.Combine(root, "layout.json"), $$"""
                {"version":2,"settings":{},"items":[
                  {"id":"{{id}}","contentKind":"nowPlaying","nowPlaying":{"showSpectrum":false,"progressStyle":999}},
                  {"id":"{{Guid.NewGuid()}}","contentKind":"nowPlaying","nowPlaying":{}}
                ]}
                """);
            var layout = new LayoutRepository(root).Load();
            if (layout.Items.Count != 2 || layout.Items[0].NowPlaying.ShowSpectrum || layout.Items[0].NowPlaying.ProgressStyle != NowPlayingProgressStyle.Simple)
                throw new InvalidOperationException("unknown persisted enum was not normalized");
            if (!layout.Items[1].NowPlaying.ShowSpectrum || layout.Items[1].NowPlaying.ProgressStyle != NowPlayingProgressStyle.Simple)
                throw new InvalidOperationException("old JSON defaults were not preserved");
            var serialized = JsonSerializer.Serialize(layout);
            if (!serialized.Contains("ShowSpectrum", StringComparison.Ordinal) || !serialized.Contains("ProgressStyle", StringComparison.Ordinal))
                throw new InvalidOperationException("options were absent from JSON round trip");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }
    }

    private static void VerifyImmediateNetworkHistory()
    {
        var history = new NetworkHistory(30);
        history.Add(100);
        history.Add(400);
        var normalized = history.Normalized();
        if (normalized.Count != 2 || Math.Abs(normalized[0] - .25) > .0001 || Math.Abs(normalized[1] - 1) > .0001)
            throw new InvalidOperationException("network graph did not immediately use its new scale");
        for (var i = 0; i < 32; i++) history.Add(i);
        if (history.Values.Count != 30 || history.Values[0] != 2 || history.Values[^1] != 31)
            throw new InvalidOperationException("network history did not retain the latest 30 samples");
        var row = typeof(SystemMonitorItemContent).GetNestedType("NetworkRow", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("NetworkRow missing");
        var fields = row.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(field => field.FieldType).ToArray();
        if (!fields.Contains(typeof(NetworkHistory)) || fields.Any(type => type.Name.Contains("SmoothedNetworkGraph", StringComparison.Ordinal)) || fields.Any(type => type == typeof(System.Windows.Threading.DispatcherTimer)))
            throw new InvalidOperationException("network row still contains smoothing state");
    }

    private static void VerifyTimelineAndSpectrum()
    {
        foreach (var style in Enum.GetValues<NowPlayingProgressStyle>())
            VerifyTimelineStyle(style);

        var factoryCalls = 0;
        var reader = new V19FakeSpectrumReader();
        var item = NewItem(NowPlayingProgressStyle.Simple, showSpectrum: false);
        var content = new NowPlayingItemContent(item, new V19NowPlayingService(), () => { factoryCalls++; return reader; });
        try
        {
            Arrange(content.View);
            content.SetActive(true);
            InvokePlaybackTick(content);
            if (factoryCalls != 0 || reader.ReadCount != 0)
                throw new InvalidOperationException("hidden spectrum created or polled an audio reader");

            item.NowPlaying.ShowSpectrum = true;
            content.Refresh();
            InvokePlaybackTick(content);
            if (factoryCalls != 1 || reader.ReadCount != 1)
                throw new InvalidOperationException("visible spectrum did not create and poll its audio reader");
        }
        finally
        {
            content.Dispose();
        }
        if (!reader.Disposed)
            throw new InvalidOperationException("spectrum reader lease was not released on dispose");
    }

    private static void VerifyTimelineStyle(NowPlayingProgressStyle style)
    {
        var item = NewItem(style, showSpectrum: false);
        using var content = new NowPlayingItemContent(item, new V19NowPlayingService());
        var root = (FrameworkElement)content.View;
        Arrange(root);
        content.Refresh();

        var visualizer = Field<StackPanel>(content, "visualizer");
        if (visualizer.Visibility != Visibility.Collapsed)
            throw new InvalidOperationException("spectrum was not collapsed when disabled");
        var buttons = Descendants<Button>(root).ToArray();
        if (buttons.Length != 3)
            throw new InvalidOperationException($"expected three transport buttons, got {buttons.Length}");
        var bounds = buttons.Select(button => Bounds(button, root)).ToArray();
        var center = (bounds.Min(rect => rect.Left) + bounds.Max(rect => rect.Right)) / 2;
        if (Math.Abs(center - root.ActualWidth / 2) > .75)
            throw new InvalidOperationException("transport trio shifted when spectrum was hidden");

        var fill = Field<Border>(content, "timelineFill");
        var art = Field<Canvas>(content, "timelineArt");
        if (style == NowPlayingProgressStyle.Simple)
        {
            if (fill.Visibility != Visibility.Visible || art.Children.Count != 0 || fill.ActualWidth <= 0)
                throw new InvalidOperationException("simple timeline geometry failed");
            return;
        }
        if (fill.Visibility != Visibility.Collapsed || art.Children.Count != 2)
            throw new InvalidOperationException($"{style} timeline was not custom drawn");
        if (art.Children[1].Clip is not RectangleGeometry { Rect.Width: > 0 } clip || clip.Rect.Width >= art.ActualWidth)
            throw new InvalidOperationException($"{style} progress clip was invalid");
        if (style == NowPlayingProgressStyle.Wave && art.Children[0] is not System.Windows.Shapes.Path)
            throw new InvalidOperationException("wave did not use vector geometry");
        if (style is NowPlayingProgressStyle.Dots or NowPlayingProgressStyle.Hearts && art.Children[0] is not Canvas)
            throw new InvalidOperationException($"{style} did not use repeated vector marks");
    }

    private static CanvasItem NewItem(NowPlayingProgressStyle style, bool showSpectrum) => new()
    {
        ContentKind = CanvasContentKinds.NowPlaying,
        Width = 360,
        Height = 220,
        Theme = WidgetThemeKind.Dark,
        NowPlaying = new NowPlayingOptions { ShowSpectrum = showSpectrum, ProgressStyle = style }
    };

    private static void Arrange(UIElement element)
    {
        var root = (FrameworkElement)element;
        root.Measure(new Size(360, 220));
        root.Arrange(new Rect(0, 0, 360, 220));
        root.UpdateLayout();
    }

    private static void InvokePlaybackTick(NowPlayingItemContent content) =>
        (typeof(NowPlayingItemContent).GetMethod("PlaybackTick", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(nameof(NowPlayingItemContent), "PlaybackTick"))
        .Invoke(content, [null, EventArgs.Empty]);

    private static T Field<T>(object owner, string name) => (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
        ?? throw new MissingFieldException(owner.GetType().Name, name));

    private static Rect Bounds(FrameworkElement element, FrameworkElement root) =>
        element.TransformToAncestor(root).TransformBounds(new Rect(new Point(), element.RenderSize));

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception caught) { error = caught; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class V19NowPlayingService : INowPlayingService
    {
        public NowPlayingSnapshot Snapshot { get; } = new(true, "DeskCanvas.Test!player", "v19 progress test", "artist", "album", null, NowPlayingState.Playing, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), true, true, true, true, DateTimeOffset.UtcNow);
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add { } remove { } }
        public IDisposable Acquire() => V19Lease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true);
        public Task<bool> PlayPauseAsync() => Task.FromResult(true);
        public Task<bool> NextAsync() => Task.FromResult(true);
        public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
        public void Dispose() { }
    }

    private sealed class V19FakeSpectrumReader : IAudioSpectrumReader
    {
        internal int ReadCount { get; private set; }
        internal bool Disposed { get; private set; }
        public IReadOnlyList<double> ReadBands() { ReadCount++; return [0, .1, .2, .3, .2, .1, 0]; }
        public void Dispose() => Disposed = true;
    }

    private sealed class V19Lease : IDisposable
    {
        internal static readonly V19Lease Instance = new();
        public void Dispose() { }
    }
}
