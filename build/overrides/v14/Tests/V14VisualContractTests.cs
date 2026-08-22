using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;
using SkiaSharp;

internal static class V14VisualContractTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        Test("v1.4 system reference rows and default size", SystemReferenceRows);
        Test("v1.4 Apple-style transport corners", TransportCorners);
        Test("v1.4 same-media updates do not restart marquee", MarqueeStability);
        Test("v1.4 artwork palette keeps the vivid accent", ArtworkAccent);
    }

    private static void SystemReferenceRows()
    {
        var item = new CanvasItem
        {
            ContentKind = CanvasContentKinds.SystemMonitor,
            Width = 300,
            Height = 180
        };
        using var service = new FakeSystemMetricsService();
        using var content = new SystemMonitorItemContent(item, service);
        Equal(320d, item.Width);
        Equal(390d, item.Height);
        var card = content.View as Border
            ?? throw new InvalidOperationException("system card is not a Border");
        var viewbox = card.Child as Viewbox
            ?? throw new InvalidOperationException("system rows are not responsive");
        var rows = viewbox.Child as StackPanel
            ?? throw new InvalidOperationException("system row stack missing");
        Equal(7, rows.Children.Count);
        True(rows.Children[0] is StackPanel);
        True(rows.Children[1] is Grid);
        True(rows.Children[2] is Grid);
        True(rows.Children[3] is Grid);
        True(rows.Children[4] is Border);
        True(rows.Children[5] is Grid);
        True(rows.Children[6] is Grid);
    }

    private static void TransportCorners()
    {
        using var service = new FakeNowPlayingService(Snapshot("Short title"));
        using var content = new NowPlayingItemContent(
            new CanvasItem { ContentKind = CanvasContentKinds.NowPlaying },
            service);
        var buttons = Descendants(content.View).OfType<Button>().ToArray();
        Equal(3, buttons.Length);
        foreach (var button in buttons)
        {
            var factory = button.Template.VisualTree
                ?? throw new InvalidOperationException("transport template missing");
            Equal(new CornerRadius(11), factory.GetValue(Border.CornerRadiusProperty));
            Equal(new Thickness(0), button.BorderThickness);
        }
    }

    private static void MarqueeStability()
    {
        var initial = Snapshot(
            "A title long enough to overflow the dedicated marquee viewport");
        using var service = new FakeNowPlayingService(initial);
        using var content = new NowPlayingItemContent(
            new CanvasItem { ContentKind = CanvasContentKinds.NowPlaying },
            service);
        var transform = Field<TranslateTransform>(content, "titleTransform");
        transform.X = -12;
        var update = typeof(NowPlayingItemContent).GetMethod(
            "Update",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("marquee update method missing");
        update.Invoke(content, [initial with { Position = TimeSpan.FromSeconds(5) }]);
        Equal(-12d, transform.X);
    }

    private static void ArtworkAccent()
    {
        using var bitmap = new SKBitmap(2, 1);
        bitmap.SetPixel(0, 0, new SKColor(128, 128, 128));
        bitmap.SetPixel(1, 0, new SKColor(255, 0, 0));
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var palette = WidgetTheme.Resolve(
            new CanvasItem { Theme = WidgetThemeKind.Auto },
            encoded.ToArray());
        Equal((byte)255, palette.Accent.R);
        Equal((byte)0, palette.Accent.G);
        Equal((byte)0, palette.Accent.B);
        Equal((byte)255, palette.SurfaceA.A);
        Equal((byte)255, palette.SurfaceB.A);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject dependency)
                continue;
            yield return dependency;
            foreach (var nested in Descendants(dependency))
                yield return nested;
        }
    }

    private static T Field<T>(object instance, string name) where T : class =>
        typeof(NowPlayingItemContent).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance) as T
        ?? throw new InvalidOperationException($"field {name} missing");

    private static NowPlayingSnapshot Snapshot(string title) =>
        new(
            true,
            "MusicPlayer.exe",
            title,
            "Artist",
            "Album",
            null,
            NowPlayingState.Paused,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(3),
            true,
            true,
            true,
            true,
            DateTimeOffset.UtcNow);

    private static void Test(string name, Action action)
    {
        action();
        Console.WriteLine($"PASS {name}");
    }

    private static void True(bool value)
    {
        if (!value)
            throw new InvalidOperationException("expected true");
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"expected {expected}, actual {actual}");
    }

    private sealed class FakeNowPlayingService(NowPlayingSnapshot snapshot)
        : INowPlayingService
    {
        public NowPlayingSnapshot Snapshot { get; } = snapshot;
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged;
        public IDisposable Acquire() => EmptyLease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true);
        public Task<bool> PlayPauseAsync() => Task.FromResult(true);
        public Task<bool> NextAsync() => Task.FromResult(true);
        public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
        public void Dispose() => SnapshotChanged = null;
    }

    private sealed class FakeSystemMetricsService : ISystemMetricsService
    {
        public SystemMetricsSnapshot Snapshot => SystemMetricsSnapshot.Loading;
        public event EventHandler<SystemMetricsSnapshot>? SnapshotChanged;
        public IDisposable Acquire() => EmptyLease.Instance;
        public void Dispose() => SnapshotChanged = null;
    }

    private sealed class EmptyLease : IDisposable
    {
        internal static readonly EmptyLease Instance = new();
        public void Dispose() { }
    }
}
