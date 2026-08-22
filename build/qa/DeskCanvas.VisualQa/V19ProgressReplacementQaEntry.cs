using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class V19ProgressReplacementQaEntry
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
        {
            Console.Error.WriteLine("Pass an absolute output directory.");
            return 2;
        }

        Directory.CreateDirectory(args[0]);
        foreach (var scale in new[] { 1d, 1.5d })
        foreach (var style in Enum.GetValues<NowPlayingProgressStyle>())
        foreach (var ratio in new[] { 0d, .5d, 1d })
        {
            VerifyAndRender(args[0], style, ratio, true, scale);
        }

        VerifyAndRender(args[0], NowPlayingProgressStyle.Wave, .5d, false, 1d);
        Console.WriteLine("PASS v1.9 progress replacement STA QA: base line visibility, custom layers, 0/50/100 clips, 16DIP seek, and CanSeek=false");
        return 0;
    }

    private static void VerifyAndRender(string directory, NowPlayingProgressStyle style, double ratio, bool canSeek, double scale)
    {
        var item = new CanvasItem
        {
            ContentKind = CanvasContentKinds.NowPlaying,
            Width = 360,
            Height = 220,
            Opacity = 1,
            Theme = WidgetThemeKind.Ocean,
            NowPlaying = new NowPlayingOptions { ProgressStyle = style, ShowTimeline = true, ShowSpectrum = false }
        };
        var media = new MutableMedia(ratio, canSeek);
        using var content = new NowPlayingItemContent(item, media, () => new SilentSpectrum());
        content.SetActive(true);
        var root = (FrameworkElement)content.View;
        Layout(root, 360, 220);

        var timeline = Field<Grid>(content, "timeline");
        var track = Field<Border>(content, "timelineTrack");
        var fill = Field<Border>(content, "timelineFill");
        var art = Field<Canvas>(content, "timelineArt");
        var seek = Field<Slider>(content, "seek");
        if (timeline.Visibility != Visibility.Visible || seek.Visibility != Visibility.Visible || seek.ActualHeight < 15 || seek.Opacity > .011)
            throw new InvalidOperationException($"{style}/{ratio}: 16DIP transparent seek surface missing");
        if (seek.IsEnabled != canSeek || seek.IsHitTestVisible != canSeek)
            throw new InvalidOperationException($"{style}/{ratio}: CanSeek did not control only seek interaction");

        if (style == NowPlayingProgressStyle.Simple)
        {
            if (track.Visibility != Visibility.Visible || fill.Visibility != Visibility.Visible || art.Children.Count != 0)
                throw new InvalidOperationException("simple style did not keep only the capsule track/fill");
            var expected = timeline.ActualWidth * ratio;
            if (Math.Abs(fill.ActualWidth - expected) > 1.1)
                throw new InvalidOperationException($"simple fill width {fill.ActualWidth} was not {expected}");
        }
        else
        {
            if (track.Visibility != Visibility.Collapsed || fill.Visibility != Visibility.Collapsed)
                throw new InvalidOperationException($"{style}: straight base line remains visible");
            if (art.Children.Count != 2)
                throw new InvalidOperationException($"{style}: expected muted and accent custom layers");
            var muted = art.Children[0];
            var accent = art.Children[1];
            if (muted.Clip is not null || accent.Clip is not RectangleGeometry clip)
                throw new InvalidOperationException($"{style}: custom layers do not have one accent-only clip");
            var expected = timeline.ActualWidth * ratio;
            if (Math.Abs(clip.Rect.Width - expected) > .75 || clip.Rect.Height != 16)
                throw new InvalidOperationException($"{style}/{ratio}: accent clip {clip.Rect.Width} was not {expected}");
            if (style == NowPlayingProgressStyle.Wave && (muted is not Path || accent is not Path))
                throw new InvalidOperationException("wave is not a wave-only path timeline");
            if (style != NowPlayingProgressStyle.Wave && (muted is not Canvas || accent is not Canvas))
                throw new InvalidOperationException($"{style}: custom timeline is not shape-only canvas layers");
        }

        var dpi = scale == 1 ? "100" : "150";
        var seekName = canSeek ? "seek" : "noseek";
        Render(root, 360, 220, scale, Path.Combine(directory, $"v19-progress-replacement-{style.ToString().ToLowerInvariant()}-{(int)(ratio * 100)}-{seekName}-{dpi}.png"));
        content.SetActive(false);
    }

    private static T Field<T>(object instance, string name) where T : class =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new InvalidOperationException($"field {name} missing"));

    private static void Layout(FrameworkElement root, double width, double height)
    {
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
    }

    private static void Render(FrameworkElement root, double width, double height, double scale, string output)
    {
        var host = new Grid { Width = width, Height = height, LayoutTransform = new ScaleTransform(scale, scale) };
        host.Children.Add(root);
        var size = new Size(width * scale, height * scale);
        host.Measure(size);
        host.Arrange(new Rect(new Point(), size));
        host.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Round(size.Width), (int)Math.Round(size.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var center = new byte[4];
        bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth / 2, bitmap.PixelHeight / 2, 1, 1), center, 4, 0);
        if (center[3] != byte.MaxValue) throw new InvalidOperationException($"{Path.GetFileName(output)} alpha {center[3]}");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(output);
        encoder.Save(stream);
    }

    private sealed class MutableMedia : INowPlayingService
    {
        internal MutableMedia(double ratio, bool canSeek) => Snapshot = new NowPlayingSnapshot(true, "DeskCanvas.Qa!player", "Progress QA", "artist", "album", null, NowPlayingState.Paused, TimeSpan.FromSeconds(60 * ratio), TimeSpan.FromSeconds(60), true, true, true, canSeek, DateTimeOffset.UtcNow);
        public NowPlayingSnapshot Snapshot { get; }
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged { add { } remove { } }
        public IDisposable Acquire() => Lease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true);
        public Task<bool> PlayPauseAsync() => Task.FromResult(true);
        public Task<bool> NextAsync() => Task.FromResult(true);
        public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
        public void Dispose() { }
    }

    private sealed class SilentSpectrum : IAudioSpectrumReader
    {
        public IReadOnlyList<double> ReadBands() => [0, 0, 0, 0, 0, 0, 0];
        public void Dispose() { }
    }

    private sealed class Lease : IDisposable
    {
        internal static readonly Lease Instance = new();
        public void Dispose() { }
    }
}
