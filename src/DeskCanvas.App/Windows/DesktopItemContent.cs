using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
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
    private readonly TextBlock time = new() { Foreground = WidgetTheme.Primary, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock seconds = new() { Foreground = WidgetTheme.Secondary, HorizontalAlignment = HorizontalAlignment.Center, Opacity = .72, Margin = new Thickness(7, 0, 0, 7), VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock meridiem = new() { Foreground = WidgetTheme.Secondary, FontSize = 11, Opacity = .72, Margin = new Thickness(6, 0, 0, 8), VerticalAlignment = VerticalAlignment.Bottom };
    private readonly TextBlock splitHour = SplitNumber();
    private readonly TextBlock splitMinute = SplitNumber();
    private readonly TextBlock splitSeconds = new() { Foreground = WidgetTheme.Secondary, FontSize = 12, Opacity = .7, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
    private readonly TextBlock digitalDate = CreateDateText();
    private readonly TextBlock splitDate = CreateDateText();
    private readonly TextBlock analogDate = CreateDateText();
    private readonly Canvas dial = new() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
    private readonly Ellipse dialFace = new() { Stroke = Brushes.White, StrokeThickness = 2, Fill = new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)) };
    private readonly List<Line> dialMarks = [];
    private readonly Line hourHand = new() { Stroke = Brushes.White, StrokeThickness = 4, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Line minuteHand = new() { Stroke = WidgetTheme.Secondary, StrokeThickness = 2.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Line secondHand = new() { Stroke = new SolidColorBrush(Color.FromRgb(255, 112, 130)), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    private readonly Ellipse centerDot = new() { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(251, 210, 192)) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly StackPanel digital = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel split = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid analog = new();
    private bool active;

    internal ClockItemContent(CanvasItem item)
    {
        this.item = item;
        card = WidgetTheme.Card(root);
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

    internal void Refresh() => Update();

    private static TextBlock CreateDateText() => new()
    {
        Foreground = WidgetTheme.Secondary,
        HorizontalAlignment = HorizontalAlignment.Center,
        FontSize = 12,
        Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)),
        Padding = new Thickness(9, 3, 9, 3),
        Margin = new Thickness(0, 6, 0, 0),
        TextAlignment = TextAlignment.Center
    };

    private static TextBlock SplitNumber() => new()
    {
        Foreground = WidgetTheme.Primary,
        FontSize = 35,
        FontWeight = FontWeights.SemiBold,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    private void Build()
    {
        dial.Children.Add(dialFace);
        for (var index = 0; index < 12; index++)
        {
            var mark = new Line
            {
                Stroke = WidgetTheme.Primary,
                StrokeThickness = index % 3 == 0 ? 2.4 : 1.2,
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
        splitRow.Children.Add(new TextBlock { Text = ":", Foreground = WidgetTheme.Secondary, FontSize = 27, FontWeight = FontWeights.Light, Margin = new Thickness(6, 4, 6, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = .7 });
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
        Width = 58,
        Height = 50,
        CornerRadius = new CornerRadius(15),
        Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
        BorderBrush = WidgetTheme.Hairline,
        BorderThickness = new Thickness(1),
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
            time.FontSize = 50;
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