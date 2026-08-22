using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskCanvas.App.Media;
using DeskCanvas.Core;
using Microsoft.Win32;

namespace DeskCanvas.App.Windows;

internal interface IDesktopItemContent : IDisposable
{
    UIElement View { get; }
    void SetActive(bool active);
    bool HasInteractiveControls => false;
    bool IsInteractiveHit(Point point) => false;
}

internal sealed class MediaItemContent : IDesktopItemContent
{
    private readonly System.Windows.Controls.Image image = new()
    {
        Stretch = Stretch.Fill,
        SnapsToDevicePixels = true,
        RenderTransformOrigin = new Point(0.5, 0.5)
    };
    private readonly DecodedMedia media;
    private DispatcherTimer? timer;
    private int frameIndex;
    private bool active;

    internal MediaItemContent(CanvasItem item, DecodedMedia media)
    {
        this.media = media;
        image.Source = media.Frames[0].Image;
        image.RenderTransform = new ScaleTransform(item.IsFlipped ? -1 : 1, 1);
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
    }

    public UIElement View => image;

    public void SetActive(bool value)
    {
        active = value;
        if (!media.IsAnimated)
        {
            return;
        }

        if (active)
        {
            timer ??= new DispatcherTimer(DispatcherPriority.Render);
            timer.Tick -= Tick;
            timer.Tick += Tick;
            timer.Interval = media.Frames[frameIndex].Duration;
            timer.Start();
        }
        else
        {
            timer?.Stop();
        }
    }

    internal void SetFlipped(bool flipped) => image.RenderTransform = new ScaleTransform(flipped ? -1 : 1, 1);

    private void Tick(object? sender, EventArgs e)
    {
        if (!active)
        {
            return;
        }

        frameIndex = (frameIndex + 1) % media.Frames.Count;
        image.Source = media.Frames[frameIndex].Image;
        if (timer is not null)
        {
            timer.Interval = media.Frames[frameIndex].Duration;
        }
    }

    public void Dispose()
    {
        active = false;
        if (timer is not null)
        {
            timer.Stop();
            timer.Tick -= Tick;
        }
    }
}

internal sealed class ClockItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly Grid root = new();
    private readonly Border card;
    private readonly TextBlock time = Design.Tabular(Design.Text("", 52, WidgetTheme.Primary, FontWeights.SemiBold, display: true));
    private readonly TextBlock seconds = Design.Tabular(Design.Text("", 15, WidgetTheme.Tertiary, FontWeights.Medium));
    private readonly TextBlock meridiem = Design.Text("", 12, WidgetTheme.Tertiary, FontWeights.Medium);
    private readonly TextBlock splitHour = SplitNumber();
    private readonly TextBlock splitMinute = SplitNumber();
    private readonly TextBlock splitSeconds = Design.Tabular(Design.Text("", 12, WidgetTheme.Tertiary, FontWeights.Medium));
    private readonly TextBlock digitalDate = CreateDateText();
    private readonly TextBlock splitDate = CreateDateText();
    private readonly TextBlock analogDate = CreateDateText();
    private readonly Canvas dial = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly Ellipse dialFace = new() { Stroke = Design.Frozen(Design.Argb(0x2E, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1, Fill = Design.Frozen(Design.Argb(0x14, 0xFF, 0xFF, 0xFF)) };
    private readonly List<Line> dialMarks = [];
    private readonly Line hourHand = new() { Stroke = Brushes.White, StrokeThickness = 3.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Line minuteHand = new() { Stroke = Brushes.White, StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Line secondHand = new() { Stroke = Design.AccentBrush, StrokeThickness = 1.4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Ellipse centerDot = new() { Width = 6, Height = 6, Fill = Design.AccentBrush };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly StackPanel digital = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel split = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid analog = new();
    private bool active;

    internal ClockItemContent(CanvasItem item)
    {
        this.item = item;
        card = WidgetTheme.Card(root, item);
        timer.Tick += Timer_Tick;
        Build();
        Update();
    }

    public UIElement View => card;

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        if (value)
        {
            SystemEvents.TimeChanged += SystemTimeChanged;
            SystemEvents.UserPreferenceChanged += SystemPreferenceChanged;
            Update();
            timer.Start();
        }
        else
        {
            timer.Stop();
            SystemEvents.TimeChanged -= SystemTimeChanged;
            SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
        }
    }

    internal void Refresh()
    {
        WidgetTheme.ApplySurface(card, item);
        Update();
    }

    /// <summary>
    /// Plain secondary caption rather than a chip, so the card stays quiet. Left at the
    /// regular weight because the date is Japanese, and the CJK fallback face has no
    /// 500-weight cut to render a Medium request with.
    /// </summary>
    private static TextBlock CreateDateText()
    {
        var block = Design.Tabular(Design.Text("", 12, WidgetTheme.Secondary));
        block.HorizontalAlignment = HorizontalAlignment.Center;
        block.TextAlignment = TextAlignment.Center;
        block.Margin = new Thickness(0, 7, 0, 0);
        return block;
    }

    private static TextBlock SplitNumber()
    {
        var block = Design.Tabular(Design.Text("", 34, WidgetTheme.Primary, FontWeights.SemiBold, display: true));
        block.TextAlignment = TextAlignment.Center;
        block.HorizontalAlignment = HorizontalAlignment.Center;
        block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    private void Build()
    {
        // Seconds and AM/PM ride the baseline of the large time, as on a lock screen.
        seconds.VerticalAlignment = VerticalAlignment.Bottom;
        seconds.Margin = new Thickness(8, 0, 0, 9);
        meridiem.VerticalAlignment = VerticalAlignment.Bottom;
        meridiem.Margin = new Thickness(7, 0, 0, 10);
        splitSeconds.HorizontalAlignment = HorizontalAlignment.Center;
        splitSeconds.Margin = new Thickness(0, 6, 0, 0);

        dial.Children.Add(dialFace);
        for (var index = 0; index < 12; index++)
        {
            // Quarter marks are emphasised; the rest recede.
            var quarter = index % 3 == 0;
            var mark = new Line
            {
                Stroke = quarter ? WidgetTheme.Primary : WidgetTheme.Tertiary,
                StrokeThickness = quarter ? 2.2 : 1.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            dialMarks.Add(mark);
            dial.Children.Add(mark);
        }
        dial.Children.Add(hourHand);
        dial.Children.Add(minuteHand);
        dial.Children.Add(secondHand);
        dial.Children.Add(centerDot);
        dial.SizeChanged += (_, _) => UpdateAnalog(DateTime.Now);

        var timeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        timeRow.Children.Add(time);
        timeRow.Children.Add(seconds);
        timeRow.Children.Add(meridiem);
        digital.Children.Add(timeRow);
        digital.Children.Add(digitalDate);

        var splitRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        splitRow.Children.Add(SplitCell(splitHour));
        var colon = Design.Text(":", 26, WidgetTheme.Tertiary, FontWeights.Light, display: true);
        colon.Margin = new Thickness(7, 3, 7, 0);
        colon.VerticalAlignment = VerticalAlignment.Center;
        splitRow.Children.Add(colon);
        splitRow.Children.Add(SplitCell(splitMinute));
        split.Children.Add(splitRow);
        split.Children.Add(splitSeconds);
        split.Children.Add(splitDate);

        analog.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        analog.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        analog.Children.Add(dial);
        Grid.SetRow(analogDate, 1);
        analogDate.Margin = new Thickness(0, 2, 0, 0);
        analog.Children.Add(analogDate);

        root.Children.Add(digital);
        root.Children.Add(split);
        root.Children.Add(analog);
    }

    private static Border SplitCell(TextBlock number) => new()
    {
        Width = 60,
        Height = 54,
        CornerRadius = new CornerRadius(14),
        Background = Design.ControlFillBrush,
        BorderBrush = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Child = number
    };

    private void Timer_Tick(object? sender, EventArgs e) => Update();

    private void Update()
    {
        var now = DateTime.Now;
        var options = item.Clock;
        var dateText = ClockFormatting.FormatDate(now, options);
        digitalDate.Text = dateText;
        splitDate.Text = dateText;
        analogDate.Text = dateText;
        digitalDate.Visibility = string.IsNullOrEmpty(dateText) ? Visibility.Collapsed : Visibility.Visible;
        splitDate.Visibility = string.IsNullOrEmpty(dateText) ? Visibility.Collapsed : Visibility.Visible;
        analogDate.Visibility = string.IsNullOrEmpty(dateText) ? Visibility.Collapsed : Visibility.Visible;

        digital.Visibility = options.Style == ClockStyle.Digital ? Visibility.Visible : Visibility.Collapsed;
        split.Visibility = options.Style == ClockStyle.Split ? Visibility.Visible : Visibility.Collapsed;
        analog.Visibility = options.Style == ClockStyle.Analog ? Visibility.Visible : Visibility.Collapsed;

        if (options.Style == ClockStyle.Digital)
        {
            time.Text = now.ToString(options.Use24Hour ? "HH:mm" : "h:mm");
            seconds.Text = options.ShowSeconds ? now.ToString("ss") : "";
            seconds.Visibility = options.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
            meridiem.Text = options.Use24Hour ? "" : now.ToString("tt");
            meridiem.Visibility = options.Use24Hour ? Visibility.Collapsed : Visibility.Visible;
        }
        else if (options.Style == ClockStyle.Split)
        {
            splitHour.Text = now.ToString(options.Use24Hour ? "HH" : "hh");
            splitMinute.Text = now.ToString("mm");
            splitSeconds.Text = options.ShowSeconds
                ? $"{now:ss}{(options.Use24Hour ? "" : "  " + now.ToString("tt"))}"
                : options.Use24Hour ? "" : now.ToString("tt");
            splitSeconds.Visibility = string.IsNullOrEmpty(splitSeconds.Text) ? Visibility.Collapsed : Visibility.Visible;
        }

        UpdateAnalog(now);
    }

    private void UpdateAnalog(DateTime now)
    {
        if (item.Clock.Style != ClockStyle.Analog)
        {
            return;
        }

        var width = Math.Max(1, dial.ActualWidth);
        var height = Math.Max(1, dial.ActualHeight);
        var radius = Math.Max(12, Math.Min(width, height) / 2 - 6);
        var centerX = width / 2;
        var centerY = height / 2;

        dialFace.Width = radius * 2;
        dialFace.Height = radius * 2;
        Canvas.SetLeft(dialFace, centerX - radius);
        Canvas.SetTop(dialFace, centerY - radius);

        for (var index = 0; index < dialMarks.Count; index++)
        {
            var angle = (index * 30 - 90) * Math.PI / 180;
            var outerRadius = radius - 7;
            var innerRadius = outerRadius - (index % 3 == 0 ? 8 : 4);
            var mark = dialMarks[index];
            mark.X1 = centerX + Math.Cos(angle) * innerRadius;
            mark.Y1 = centerY + Math.Sin(angle) * innerRadius;
            mark.X2 = centerX + Math.Cos(angle) * outerRadius;
            mark.Y2 = centerY + Math.Sin(angle) * outerRadius;
        }

        SetHand(hourHand, centerX, centerY, radius * 0.48, (now.Hour % 12 + now.Minute / 60d) * 30);
        SetHand(minuteHand, centerX, centerY, radius * 0.73, (now.Minute + now.Second / 60d) * 6);
        secondHand.Visibility = item.Clock.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
        if (item.Clock.ShowSeconds)
        {
            SetHand(secondHand, centerX, centerY, radius * 0.82, now.Second * 6);
        }
        Canvas.SetLeft(centerDot, centerX - centerDot.Width / 2);
        Canvas.SetTop(centerDot, centerY - centerDot.Height / 2);
    }

    private static void SetHand(Line hand, double centerX, double centerY, double length, double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        hand.X1 = centerX;
        hand.Y1 = centerY;
        hand.X2 = centerX + Math.Cos(radians) * length;
        hand.Y2 = centerY + Math.Sin(radians) * length;
    }

    private void SystemTimeChanged(object? sender, EventArgs e) => root.Dispatcher.BeginInvoke(Update);
    private void SystemPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e) => root.Dispatcher.BeginInvoke(Update);

    public void Dispose()
    {
        SetActive(false);
        timer.Tick -= Timer_Tick;
        SystemEvents.TimeChanged -= SystemTimeChanged;
        SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged;
    }
}