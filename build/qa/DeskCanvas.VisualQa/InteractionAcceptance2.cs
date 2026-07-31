using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using DeskCanvas.App.Windows;
using DeskCanvas.Core;

internal static class InteractionAcceptance2
{
    internal static void Run()
    {
        var service = new MutableService(Snapshot(
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
        var host = new Window
        {
            Width = 360,
            Height = 220,
            Left = -10000,
            Top = -10000,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Content = content.View
        };
        try
        {
            content.SetActive(true);
            host.Show();
            var root = (FrameworkElement)content.View;
            root.UpdateLayout();

            Invoke(content, "RecalculateMarquee");
            var transform = Field<TranslateTransform>(content, "titleTransform");
            if (!transform.HasAnimatedProperties)
                throw new InvalidOperationException("overflow title did not start marquee");

            var controls = Descendants<System.Windows.Controls.Button>(root)
                .Where(button => button.ToolTip is not null)
                .Cast<FrameworkElement>()
                .Append(Field<Slider>(content, "seek"))
                .Append(Field<FrameworkElement>(content, "artwork"))
                .Append(Field<FrameworkElement>(content, "titleViewport"))
                .ToArray();
            if (controls.Count(element =>
                    element is System.Windows.Controls.Button) != 3)
            {
                throw new InvalidOperationException("vector transport count changed");
            }
            foreach (var element in controls)
            {
                var point = CenterOf(element, root);
                if (!content.IsInteractiveHit(point))
                    throw new InvalidOperationException(
                        $"{element.GetType().Name} was outside the locked hit region");
            }
            if (content.IsInteractiveHit(new Point(2, 2)))
                throw new InvalidOperationException("card background captured a locked click");

            var timer = Field<DispatcherTimer>(content, "playbackTimer");
            var visualizer = Field<StackPanel>(content, "visualizer");
            if (!timer.IsEnabled)
                throw new InvalidOperationException("playing timer did not start");
            var before = Heights(visualizer);
            Invoke(content, "PlaybackTimer_Tick", null, EventArgs.Empty);
            if (before.SequenceEqual(Heights(visualizer)))
                throw new InvalidOperationException("playing bars did not change");

            service.Current = Snapshot("Short title", NowPlayingState.Paused);
            content.Refresh();
            Invoke(content, "RecalculateMarquee");
            if (transform.HasAnimatedProperties)
                throw new InvalidOperationException("short title kept marquee");
            if (timer.IsEnabled)
                throw new InvalidOperationException("paused timer kept running");

            content.SetActive(false);
            if (timer.IsEnabled)
                throw new InvalidOperationException("hidden timer kept running");
        }
        finally
        {
            host.Close();
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
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static double[] Heights(StackPanel panel) =>
        panel.Children.Cast<FrameworkElement>().Select(element => element.Height).ToArray();

    private static T Field<T>(object owner, string name)
    {
        var field = owner.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(owner.GetType().Name, name);
        return (T)(field.GetValue(owner)
            ?? throw new InvalidOperationException($"{name} was null"));
    }

    private static void Invoke(object owner, string name, params object?[]? arguments)
    {
        var method = owner.GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(owner.GetType().Name, name);
        _ = method.Invoke(owner, arguments);
    }

    private sealed class MutableService(NowPlayingSnapshot snapshot)
        : INowPlayingService
    {
        internal NowPlayingSnapshot Current { get; set; } = snapshot;
        public NowPlayingSnapshot Snapshot => Current;
        public event EventHandler<NowPlayingSnapshot>? SnapshotChanged
        {
            add { }
            remove { }
        }
        public IDisposable Acquire() => Lease.Instance;
        public Task<bool> PreviousAsync() => Task.FromResult(true);
        public Task<bool> PlayPauseAsync() => Task.FromResult(true);
        public Task<bool> NextAsync() => Task.FromResult(true);
        public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
        public void Dispose() { }
    }

    private sealed class Lease : IDisposable
    {
        internal static readonly Lease Instance = new();
        public void Dispose() { }
    }
}
