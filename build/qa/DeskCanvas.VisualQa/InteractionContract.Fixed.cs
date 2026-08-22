using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class InteractionContract
{
    [ModuleInitializer]
    internal static void Run()
    {
        var service = new MutableNowPlayingService(Snapshot(
            "A title deliberately long enough to overflow the measured title viewport",
            NowPlayingState.Playing));
        var content = new NowPlayingItemContent(
            new CanvasItem
            {
                ContentKind = CanvasContentKinds.NowPlaying,
                Width = 360,
                Height = 220,
                Theme = WidgetThemeKind.Dark
            },
            service);
        try
        {
            content.SetActive(true);
            var root = (FrameworkElement)content.View;
            root.Measure(new Size(360, 220));
            root.Arrange(new Rect(0, 0, 360, 220));

            Invoke(content, "RecalculateMarquee");
            var transform = Field<TranslateTransform>(content, "titleTransform");
            if (!transform.HasAnimatedProperties)
                throw new InvalidOperationException("overflow title did not start marquee");

            var artwork = Field<FrameworkElement>(content, "artwork");
            var title = Field<FrameworkElement>(content, "title");
            var slider = Field<Slider>(content, "seek");
            var buttons = Descendants<System.Windows.Controls.Button>(root).ToArray();
            if (buttons.Length != 3)
                throw new InvalidOperationException(
                    $"expected 3 transport buttons, actual {buttons.Length}");
            foreach (var element in buttons.Cast<FrameworkElement>()
                         .Append(slider)
                         .Append(artwork)
                         .Append(title))
            {
                if (!content.IsInteractiveHit(CenterOf(element, root)))
                    throw new InvalidOperationException(
                        $"{element.GetType().Name} was outside the locked hit region");
            }
            if (content.IsInteractiveHit(new Point(2, 2)))
                throw new InvalidOperationException("card background captured a locked click");

            var timer = Field<DispatcherTimer>(content, "playbackTimer");
            if (!timer.IsEnabled)
                throw new InvalidOperationException("playing decoration timer did not start");
            var visualizer = Field<StackPanel>(content, "visualizer");
            var frozen = Heights(visualizer);
            Invoke(content, "PlaybackTimer_Tick", null, EventArgs.Empty);
            if (frozen.SequenceEqual(Heights(visualizer)))
                throw new InvalidOperationException("playing decoration bars did not change");

            service.Current = Snapshot("Short title", NowPlayingState.Paused);
            content.Refresh();
            Invoke(content, "RecalculateMarquee");
            if (transform.HasAnimatedProperties)
                throw new InvalidOperationException(
                    "non-overflow title kept marquee animation");
            if (timer.IsEnabled)
                throw new InvalidOperationException("paused decoration timer kept running");

            content.SetActive(false);
            if (timer.IsEnabled)
                throw new InvalidOperationException("hidden decoration timer kept running");
        }
        finally
        {
            content.Dispose();
        }
        Console.WriteLine("PASS marquee, hit regions, and decoration timer lifecycle");
    }

    private static NowPlayingSnapshot Snapshot(string title, NowPlayingState state) =>
        new(
            true,
            "MusicPlayer.exe",
            title,
            "QA Artist",
            "QA Album",
            null,
            state,
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(60),
            true,
            true,
            true,
            true,
            DateTimeOffset.UtcNow);

    private static Point CenterOf(FrameworkElement element, FrameworkElement root)
    {
        var bounds = element.TransformToAncestor(root).TransformBounds(
            new Rect(new Point(), element.RenderSize));
        return new Point(
            bounds.Left + bounds.Width / 2,
            bounds.Top + bounds.Height / 2);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static double[] Heights(StackPanel panel) =>
        panel.Children.Cast<FrameworkElement>().Select(child => child.Height).ToArray();

    private static T Field<T>(object owner, string name) =>
        (T)(owner.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(owner)
            ?? throw new MissingFieldException(owner.GetType().Name, name));

    private static void Invoke(
        object owner,
        string name,
        params object?[]? arguments)
    {
        _ = owner.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(owner, arguments)
            ?? throw new MissingMethodException(owner.GetType().Name, name);
    }

    private sealed class MutableNowPlayingService(NowPlayingSnapshot snapshot)
        : INowPlayingService
    {
        internal NowPlayingSnapshot Current { get; set; } = snapshot;
        public NowPlayingSnapshot Snapshot => Current;
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }
        public IDisposable Acquire() => EmptyLease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true);
        public Task<bool> PlayPauseAsync() => Task.FromResult(true);
        public Task<bool> NextAsync() => Task.FromResult(true);
        public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
        public void Dispose() { }
    }

    private sealed class EmptyLease : IDisposable
    {
        internal static readonly EmptyLease Instance = new();
        public void Dispose() { }
    }
}
