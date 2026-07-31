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
        VerifyOptions();
        VerifyNetworkIsImmediate();
        RunSta(VerifyVisualAndSpectrum);
        Console.WriteLine("PASS v1.9 options, instant network graph, timeline styles, and spectrum switch");
    }

    private static void VerifyOptions()
    {
        var defaults = new NowPlayingOptions();
        if (!defaults.ShowSpectrum || defaults.ProgressStyle != NowPlayingProgressStyle.Simple) throw new InvalidOperationException("v1.9 option defaults changed");
        var custom = new NowPlayingOptions { ShowSpectrum = false, ProgressStyle = NowPlayingProgressStyle.Hearts };
        if (custom.Clone().ShowSpectrum || custom.Clone().ProgressStyle != NowPlayingProgressStyle.Hearts) throw new InvalidOperationException("option clone lost values");
        custom.ProgressStyle = (NowPlayingProgressStyle)999;
        if (custom.Clone().ProgressStyle != NowPlayingProgressStyle.Simple) throw new InvalidOperationException("unknown enum was not normalized");
        var root = Path.Combine(Path.GetTempPath(), "DeskCanvas-v19-options-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var json = "{\"version\":2,\"settings\":{},\"items\":[{\"id\":\"" + Guid.NewGuid() + "\",\"contentKind\":\"nowPlaying\",\"nowPlaying\":{\"showSpectrum\":false,\"progressStyle\":999}},{\"id\":\"" + Guid.NewGuid() + "\",\"contentKind\":\"nowPlaying\",\"nowPlaying\":{}}]}";
            File.WriteAllText(Path.Combine(root, "layout.json"), json);
            var layout = new LayoutRepository(root).Load();
            if (layout.Items.Count != 2 || layout.Items[0].NowPlaying.ShowSpectrum || layout.Items[0].NowPlaying.ProgressStyle != NowPlayingProgressStyle.Simple) throw new InvalidOperationException("persisted unknown enum was not normalized");
            if (!layout.Items[1].NowPlaying.ShowSpectrum || layout.Items[1].NowPlaying.ProgressStyle != NowPlayingProgressStyle.Simple) throw new InvalidOperationException("old JSON defaults were not preserved");
            var roundTrip = JsonSerializer.Serialize(layout);
            if (!roundTrip.Contains("ShowSpectrum", StringComparison.Ordinal) || !roundTrip.Contains("ProgressStyle", StringComparison.Ordinal)) throw new InvalidOperationException("JSON round trip omitted options");
        }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    private static void VerifyNetworkIsImmediate()
    {
        var history = new NetworkHistory(30); history.Add(100); history.Add(400);
        var normalized = history.Normalized().ToArray();
        if (normalized.Length != 2 || Math.Abs(normalized[0] - .25) > .0001 || Math.Abs(normalized[1] - 1) > .0001) throw new InvalidOperationException("network scale was interpolated");
        for (var i = 0; i < 32; i++) history.Add(i);
        if (history.Values.Count() != 30 || history.Values.First() != 2 || history.Values.Last() != 31) throw new InvalidOperationException("network history is not latest 30 samples");
        var row = typeof(SystemMonitorItemContent).GetNestedType("NetworkRow", BindingFlags.NonPublic) ?? throw new InvalidOperationException("NetworkRow missing");
        var fields = row.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(field => field.FieldType).ToArray();
        if (!fields.Contains(typeof(NetworkHistory)) || fields.Any(type => type.Name.Contains("SmoothedNetworkGraph", StringComparison.Ordinal)) || fields.Any(type => type == typeof(System.Windows.Threading.DispatcherTimer))) throw new InvalidOperationException("network row still has smoothing state");
    }

    private static void VerifyVisualAndSpectrum()
    {
        foreach (var style in Enum.GetValues<NowPlayingProgressStyle>()) VerifyStyle(style);
        var calls = 0; var reader = new Reader(); var item = NewItem(NowPlayingProgressStyle.Simple, false);
        using (var content = new NowPlayingItemContent(item, new Service(), () => { calls++; return reader; }))
        {
            Arrange(content.View); content.SetActive(true); Tick(content);
            if (calls != 0 || reader.ReadCount != 0) throw new InvalidOperationException("hidden spectrum allocated reader");
            item.NowPlaying.ShowSpectrum = true; content.Refresh(); Tick(content);
            if (calls != 1 || reader.ReadCount != 1) throw new InvalidOperationException("visible spectrum did not poll reader");
        }
        if (!reader.Disposed) throw new InvalidOperationException("spectrum reader not disposed");
    }

    private static void VerifyStyle(NowPlayingProgressStyle style)
    {
        using var content = new NowPlayingItemContent(NewItem(style, false), new Service());
        var root = (FrameworkElement)content.View; Arrange(root); content.Refresh();
        if (Field<StackPanel>(content, "visualizer").Visibility != Visibility.Collapsed) throw new InvalidOperationException("hidden spectrum visible");
        var buttons = Descendants<Button>(root).ToArray(); if (buttons.Length != 3) throw new InvalidOperationException("transport count");
        var rects = buttons.Select(button => Bounds(button, root)).ToArray();
        if (Math.Abs((rects.Min(rect => rect.Left) + rects.Max(rect => rect.Right)) / 2 - root.ActualWidth / 2) > .75) throw new InvalidOperationException("transport not centered");
        var fill = Field<System.Windows.Controls.Border>(content, "timelineFill"); var art = Field<Canvas>(content, "timelineArt");
        if (style == NowPlayingProgressStyle.Simple) { if (fill.Visibility != Visibility.Visible || art.Children.Count != 0 || fill.ActualWidth <= 0) throw new InvalidOperationException("simple style failed"); return; }
        if (fill.Visibility != Visibility.Collapsed || art.Children.Count != 2) throw new InvalidOperationException("custom style missing");
        if (art.Children[1].Clip is not RectangleGeometry { Rect.Width: > 0 } clip || clip.Rect.Width >= art.ActualWidth) throw new InvalidOperationException("progress clip invalid");
        if (style == NowPlayingProgressStyle.Wave && art.Children[0] is not System.Windows.Shapes.Path) throw new InvalidOperationException("wave geometry missing");
        if (style is NowPlayingProgressStyle.Dots or NowPlayingProgressStyle.Hearts && art.Children[0] is not Canvas) throw new InvalidOperationException("repeated marks missing");
    }

    private static CanvasItem NewItem(NowPlayingProgressStyle style, bool showSpectrum) => new() { ContentKind = CanvasContentKinds.NowPlaying, Width = 360, Height = 220, Theme = WidgetThemeKind.Dark, NowPlaying = new NowPlayingOptions { ShowSpectrum = showSpectrum, ProgressStyle = style } };
    private static void Arrange(UIElement element) { var root = (FrameworkElement)element; root.Measure(new Size(360, 220)); root.Arrange(new Rect(0, 0, 360, 220)); root.UpdateLayout(); }
    private static void Tick(NowPlayingItemContent content) => (typeof(NowPlayingItemContent).GetMethod("PlaybackTick", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException()).Invoke(content, [null, EventArgs.Empty]);
    private static T Field<T>(object owner, string name) => (T)(owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) ?? throw new MissingFieldException(owner.GetType().Name, name));
    private static Rect Bounds(FrameworkElement element, FrameworkElement root) => element.TransformToAncestor(root).TransformBounds(new Rect(new Point(), element.RenderSize));
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var nested in Descendants<T>(child)) yield return nested; } }
    private static void RunSta(Action action) { Exception? error = null; var thread = new Thread(() => { try { action(); } catch (Exception caught) { error = caught; } }); thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join(); if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw(); }
    private sealed class Service : INowPlayingService { public NowPlayingSnapshot Snapshot { get; } = new(true, "DeskCanvas.Test!player", "v19 test", "artist", "album", null, NowPlayingState.Playing, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), true, true, true, true, DateTimeOffset.UtcNow); public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add { } remove { } } public IDisposable Acquire() => Lease.Instance; public Task<bool> PreviousAsync() => Task.FromResult(true); public Task<bool> PlayPauseAsync() => Task.FromResult(true); public Task<bool> NextAsync() => Task.FromResult(true); public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true); public void Dispose() { } }
    private sealed class Reader : IAudioSpectrumReader { internal int ReadCount { get; private set; } internal bool Disposed { get; private set; } public IReadOnlyList<double> ReadBands() { ReadCount++; return [0, .1, .2, .3, .2, .1, 0]; } public void Dispose() => Disposed = true; }
    private sealed class Lease : IDisposable { internal static readonly Lease Instance = new(); public void Dispose() { } }
}
