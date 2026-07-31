using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskCanvas.App.Media;
using DeskCanvas.Core;
using Microsoft.Win32;
using WpfCanvas = System.Windows.Controls.Canvas;

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
        RenderTransformOrigin = new Point(.5, .5)
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
        if (!media.IsAnimated) return;
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

    internal void SetFlipped(bool flipped) =>
        image.RenderTransform = new ScaleTransform(flipped ? -1 : 1, 1);

    private void Tick(object? sender, EventArgs e)
    {
        if (!active) return;
        frameIndex = (frameIndex + 1) % media.Frames.Count;
        image.Source = media.Frames[frameIndex].Image;
        if (timer is not null) timer.Interval = media.Frames[frameIndex].Duration;
    }

    public void Dispose()
    {
        active = false;
        if (timer is null) return;
        timer.Stop();
        timer.Tick -= Tick;
    }
}

internal sealed class ClockItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly Grid root = new();
    private readonly Border card;
    private readonly StackPanel digital = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly StackPanel split = new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Grid analog = new();
    private readonly TextBlock time = new()
    {
        FontSize = 50,
        FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center
    };
    private readonly TextBlock seconds = new()
    {
        FontSize = 13,
        Margin = new Thickness(7, 0, 0, 8),
        VerticalAlignment = VerticalAlignment.Bottom
    };
    private readonly TextBlock meridiem = new()
    {
        FontSize = 11,
        Margin = new Thickness(6, 0, 0, 8),
        VerticalAlignment = VerticalAlignment.Bottom
    };
    private readonly TextBlock digitalDate = DateLabel();
    private readonly TextBlock splitDate = DateLabel();
    private readonly TextBlock analogDate = DateLabel();
    private readonly TextBlock splitHour = SplitNumber();
    private readonly TextBlock splitMinute = SplitNumber();
    private readonly TextBlock splitSecond = new()
    {
        FontSize = 12,
        HorizontalAlignment = HorizontalAlignment.Center,
        Margin = new Thickness(0, 3, 0, 0)
    };
    private readonly Border splitHourCard;
    private readonly Border splitMinuteCard;
    private readonly WpfCanvas dial = new();
    private readonly Ellipse face = new() { StrokeThickness = 2 };
    private readonly List<Line> marks = [];
    private readonly Line hourHand = Hand(4);
    private readonly Line minuteHand = Hand(2.5);
    private readonly Line secondHand = Hand(1.5);
    private readonly Ellipse center = new() { Width = 7, Height = 7 };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool active;

    internal ClockItemContent(CanvasItem item)
    {
        this.item = item;
        card = WidgetTheme.Card(item, root);
        splitHourCard = SplitCard(splitHour);
        splitMinuteCard = SplitCard(splitMinute);
        Build();
        timer.Tick += TimerTick;
        Update();
    }

    public UIElement View => card;

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        if (value)
        {
            SystemEvents.TimeChanged += TimeChanged;
            SystemEvents.UserPreferenceChanged += PreferenceChanged;
            Update();
            timer.Start();
        }
        else
        {
            timer.Stop();
            SystemEvents.TimeChanged -= TimeChanged;
            SystemEvents.UserPreferenceChanged -= PreferenceChanged;
        }
    }

    internal void Refresh() => Update();

    public void Dispose()
    {
        SetActive(false);
        timer.Tick -= TimerTick;
    }

    private void Build()
    {
        var timeRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        timeRow.Children.Add(time);
        timeRow.Children.Add(seconds);
        timeRow.Children.Add(meridiem);
        digital.Children.Add(timeRow);
        digital.Children.Add(digitalDate);

        var splitRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        splitRow.Children.Add(splitHourCard);
        splitRow.Children.Add(new TextBlock
        {
            Text = ":",
            FontSize = 27,
            FontWeight = FontWeights.Light,
            Margin = new Thickness(6, 4, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = .7
        });
        splitRow.Children.Add(splitMinuteCard);
        split.Children.Add(splitRow);
        split.Children.Add(splitSecond);
        split.Children.Add(splitDate);

        analog.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        analog.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dial.Children.Add(face);
        for (var index = 0; index < 12; index++)
        {
            var mark = Hand(index % 3 == 0 ? 2.4 : 1.2);
            marks.Add(mark);
            dial.Children.Add(mark);
        }
        dial.Children.Add(hourHand);
        dial.Children.Add(minuteHand);
        dial.Children.Add(secondHand);
        dial.Children.Add(center);
        dial.SizeChanged += (_, _) => UpdateAnalog(DateTime.Now);
        analog.Children.Add(dial);
        Grid.SetRow(analogDate, 1);
        analogDate.Margin = new Thickness(0, 2, 0, 0);
        analog.Children.Add(analogDate);

        root.Children.Add(digital);
        root.Children.Add(split);
        root.Children.Add(analog);
    }

    private void Update()
    {
        var now = DateTime.Now;
        var options = item.Clock;
        var date = ClockFormatting.FormatDate(now, options);
        foreach (var label in new[] { digitalDate, splitDate, analogDate })
        {
            label.Text = date;
            label.Visibility = string.IsNullOrEmpty(date) ? Visibility.Collapsed : Visibility.Visible;
        }

        digital.Visibility = options.Style == ClockStyle.Digital ? Visibility.Visible : Visibility.Collapsed;
        split.Visibility = options.Style == ClockStyle.Split ? Visibility.Visible : Visibility.Collapsed;
        analog.Visibility = options.Style == ClockStyle.Analog ? Visibility.Visible : Visibility.Collapsed;

        time.Text = now.ToString(options.Use24Hour ? "HH:mm" : "h:mm");
        seconds.Text = options.ShowSeconds ? now.ToString("ss") : "";
        seconds.Visibility = options.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
        meridiem.Text = options.Use24Hour ? "" : now.ToString("tt");
        meridiem.Visibility = options.Use24Hour ? Visibility.Collapsed : Visibility.Visible;

        splitHour.Text = now.ToString(options.Use24Hour ? "HH" : "hh");
        splitMinute.Text = now.ToString("mm");
        splitSecond.Text = options.ShowSeconds
            ? $"{now:ss}{(options.Use24Hour ? "" : "  " + now.ToString("tt"))}"
            : options.Use24Hour ? "" : now.ToString("tt");
        splitSecond.Visibility = string.IsNullOrEmpty(splitSecond.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;

        ApplyPalette();
        UpdateAnalog(now);
    }

    private void ApplyPalette()
    {
        var palette = WidgetTheme.Resolve(item);
        WidgetTheme.Apply(card, palette);
        var foreground = new SolidColorBrush(palette.Foreground);
        var muted = new SolidColorBrush(palette.Muted);
        var accent = new SolidColorBrush(palette.Accent);
        time.Foreground = foreground;
        splitHour.Foreground = foreground;
        splitMinute.Foreground = foreground;
        seconds.Foreground = muted;
        meridiem.Foreground = muted;
        splitSecond.Foreground = muted;
        digitalDate.Foreground = muted;
        splitDate.Foreground = muted;
        analogDate.Foreground = muted;
        face.Stroke = foreground;
        face.Fill = new SolidColorBrush(Color.FromArgb(18, palette.Foreground.R, palette.Foreground.G, palette.Foreground.B));
        foreach (var mark in marks) mark.Stroke = foreground;
        hourHand.Stroke = foreground;
        minuteHand.Stroke = muted;
        secondHand.Stroke = accent;
        center.Fill = accent;
        splitHourCard.BorderBrush = splitMinuteCard.BorderBrush =
            new SolidColorBrush(Color.FromArgb(42, palette.Foreground.R, palette.Foreground.G, palette.Foreground.B));
        splitHourCard.Background = splitMinuteCard.Background =
            new SolidColorBrush(Color.FromArgb(24, palette.Foreground.R, palette.Foreground.G, palette.Foreground.B));
    }

    private void UpdateAnalog(DateTime now)
    {
        if (item.Clock.Style != ClockStyle.Analog) return;
        var width = Math.Max(1, dial.ActualWidth);
        var height = Math.Max(1, dial.ActualHeight);
        var radius = Math.Max(12, Math.Min(width, height) / 2 - 6);
        var x = width / 2;
        var y = height / 2;
        face.Width = radius * 2;
        face.Height = radius * 2;
        WpfCanvas.SetLeft(face, x - radius);
        WpfCanvas.SetTop(face, y - radius);

        for (var index = 0; index < marks.Count; index++)
        {
            var angle = (index * 30 - 90) * Math.PI / 180;
            var outer = radius - 7;
            var inner = outer - (index % 3 == 0 ? 8 : 4);
            var mark = marks[index];
            mark.X1 = x + Math.Cos(angle) * inner;
            mark.Y1 = y + Math.Sin(angle) * inner;
            mark.X2 = x + Math.Cos(angle) * outer;
            mark.Y2 = y + Math.Sin(angle) * outer;
        }

        SetHand(hourHand, x, y, radius * .48, (now.Hour % 12 + now.Minute / 60d) * 30);
        SetHand(minuteHand, x, y, radius * .73, (now.Minute + now.Second / 60d) * 6);
        secondHand.Visibility = item.Clock.ShowSeconds ? Visibility.Visible : Visibility.Collapsed;
        if (item.Clock.ShowSeconds) SetHand(secondHand, x, y, radius * .82, now.Second * 6);
        WpfCanvas.SetLeft(center, x - center.Width / 2);
        WpfCanvas.SetTop(center, y - center.Height / 2);
    }

    private static void SetHand(Line hand, double x, double y, double length, double degrees)
    {
        var radians = (degrees - 90) * Math.PI / 180;
        hand.X1 = x;
        hand.Y1 = y;
        hand.X2 = x + Math.Cos(radians) * length;
        hand.Y2 = y + Math.Sin(radians) * length;
    }

    private static TextBlock DateLabel() =>
        new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 12,
            Padding = new Thickness(9, 3, 9, 3),
            Margin = new Thickness(0, 6, 0, 0),
            TextAlignment = TextAlignment.Center
        };

    private static TextBlock SplitNumber() =>
        new()
        {
            FontSize = 35,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

    private static Border SplitCard(TextBlock number) =>
        new()
        {
            Width = 58,
            Height = 50,
            CornerRadius = new CornerRadius(15),
            BorderThickness = new Thickness(1),
            Child = number
        };

    private static Line Hand(double thickness) =>
        new()
        {
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

    private void TimerTick(object? sender, EventArgs e) => Update();
    private void TimeChanged(object? sender, EventArgs e) => root.Dispatcher.BeginInvoke(Update);
    private void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        root.Dispatcher.BeginInvoke(Update);
}
