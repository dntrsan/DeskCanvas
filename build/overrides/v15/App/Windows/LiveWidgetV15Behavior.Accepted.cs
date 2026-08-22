using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskCanvas.App.Services;

namespace DeskCanvas.App.Windows;

internal static class LiveWidgetV15Behavior
{
    private static readonly ConditionalWeakTable<Border, WidgetState> States = new();
    internal static Func<IAudioPeakReader> PeakReaderFactory { get; set; } = static () => new CoreAudioPeakReader();
    [ModuleInitializer] internal static void Install() => EventManager.RegisterClassHandler(typeof(Border), FrameworkElement.LoadedEvent, new RoutedEventHandler(Loaded));

    private static void Loaded(object sender, RoutedEventArgs args)
    {
        if (sender is not Border card || States.TryGetValue(card, out var existing)) return;
        if (card.Child is Viewbox { Child: StackPanel rows } && rows.Children.Count >= 7)
        {
            card.Child = rows;
            card.UseLayoutRounding = card.SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(card, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(card, TextRenderingMode.ClearType);
            return;
        }
        var viewport = Descendants<Grid>(card).FirstOrDefault(grid => grid.ClipToBounds && grid.Height is > 24 and < 30 && grid.Children.OfType<TextBlock>().Any(text => text.FontSize >= 17));
        var bars = Descendants<StackPanel>(card).FirstOrDefault(panel => panel.Orientation == Orientation.Horizontal && panel.Children.Count == 7 && panel.Children.OfType<Border>().All(border => Math.Abs(border.Width - 3) < .1));
        var title = viewport?.Children.OfType<TextBlock>().FirstOrDefault();
        if (viewport is null || bars is null || title is null) return;
        var state = new WidgetState(card, viewport, title, bars);
        States.Add(card, state);
        card.Unloaded += (_, _) => state.Dispose();
        viewport.SizeChanged += (_, _) => state.RecalculateMarquee();
        state.Start();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private sealed class WidgetState : IDisposable
    {
        private readonly Border card; private readonly Grid viewport; private readonly TextBlock title; private readonly StackPanel bars;
        private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
        private IAudioPeakReader? reader; private string lastTitle = ""; private double lastWidth = -1; private bool disposed;
        internal WidgetState(Border card, Grid viewport, TextBlock title, StackPanel bars) { this.card = card; this.viewport = viewport; this.title = title; this.bars = bars; }
        internal void Start() { timer.Tick += Tick; timer.Start(); card.Dispatcher.BeginInvoke(RecalculateMarquee, DispatcherPriority.Loaded); }
        internal void RecalculateMarquee()
        {
            if (disposed) return;
            StopMarquee(); lastTitle = title.Text ?? ""; lastWidth = viewport.ActualWidth;
            if (!card.IsVisible || lastWidth <= 0 || lastTitle.Length == 0) return;
            var formatted = new FormattedText(lastTitle, System.Globalization.CultureInfo.CurrentUICulture, title.FlowDirection, new Typeface(title.FontFamily, title.FontStyle, title.FontWeight, title.FontStretch), title.FontSize, title.Foreground, VisualTreeHelper.GetDpi(title).PixelsPerDip);
            var fullWidth = Math.Ceiling(formatted.WidthIncludingTrailingWhitespace);
            title.Width = fullWidth;
            var overflow = fullWidth - lastWidth;
            if (overflow <= .5) return;
            viewport.Clip = new RectangleGeometry(new Rect(0, 0, lastWidth, viewport.ActualHeight));
            if (title.RenderTransform is not TranslateTransform transform) return;
            var movingEnd = 1.2 + overflow / 24d; var holdEnd = movingEnd + .8; var end = holdEnd + .45;
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(end), RepeatBehavior = RepeatBehavior.Forever };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.2))));
            animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(movingEnd))));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(holdEnd))));
            animation.KeyFrames.Add(new CubicEaseDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(end))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }
        private void Tick(object? sender, EventArgs args)
        {
            if (disposed) return;
            if (!string.Equals(lastTitle, title.Text, StringComparison.Ordinal) || Math.Abs(lastWidth - viewport.ActualWidth) > .5) RecalculateMarquee();
            if (!card.IsVisible || !IsPlaying(card)) { reader?.Dispose(); reader = null; SetBars(0); return; }
            reader ??= PeakReaderFactory();
            double peak;
            try { peak = reader.ReadPeak(); } catch { reader.Dispose(); reader = null; peak = 0; }
            SetBars(peak);
        }
        private static bool IsPlaying(DependencyObject root) => Descendants<Button>(root).Any(button => button.ToolTip?.ToString() == "再生 / 一時停止" && button.Content is Viewbox { Child: System.Windows.Shapes.Path path } && path.Data.ToString()?.Contains("M5,3 L8,3") == true);
        private void SetBars(double peak) { for (var i = 0; i < bars.Children.Count; i++) if (bars.Children[i] is Border bar) bar.Height = AudioPeakMath.BarHeight(peak, i); }
        private void StopMarquee() { if (title.RenderTransform is TranslateTransform transform) { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.X = 0; } }
        public void Dispose() { if (disposed) return; disposed = true; timer.Stop(); timer.Tick -= Tick; reader?.Dispose(); reader = null; StopMarquee(); SetBars(0); }
    }
}
