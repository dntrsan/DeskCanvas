using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using SkiaSharp;
using Button = System.Windows.Controls.Button;
using Image = System.Windows.Controls.Image;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Brush = System.Windows.Media.Brush;

namespace DeskCanvas.App.Windows;

internal static class WidgetTheme
{
    internal static readonly Brush Primary = new SolidColorBrush(Color.FromRgb(250, 246, 240));
    internal static readonly Brush Secondary = new SolidColorBrush(Color.FromRgb(211, 202, 211));
    internal static readonly Brush Tertiary = new SolidColorBrush(Color.FromRgb(173, 162, 177));
    internal static readonly Brush Hairline = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255));
    internal static readonly CornerRadius CardRadius = new(22);
    internal static readonly Thickness CardPadding = new(15);

    internal static LinearGradientBrush PlumSurface() => Gradient(
        Color.FromArgb(242, 65, 31, 51),
        Color.FromArgb(242, 28, 26, 42));

    internal static LinearGradientBrush Gradient(Color first, Color last) => new(
        new GradientStopCollection { new(first, 0), new(last, 1) }, 135);

    internal static Border Card(UIElement child) => new()
    {
        Child = child,
        Background = PlumSurface(),
        BorderBrush = Hairline,
        BorderThickness = new Thickness(1),
        CornerRadius = CardRadius,
        Padding = CardPadding,
        SnapsToDevicePixels = true,
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            ShadowDepth = 4,
            BlurRadius = 20,
            Opacity = .3
        }
    };

    internal static ControlTemplate RoundButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(999));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.Background)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.BorderBrush)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.BorderThickness)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding(nameof(ContentControl.Content)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }
}

internal sealed class NowPlayingItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly INowPlayingService service;
    private readonly Border root;
    private readonly Border artworkFrame;
    private readonly Image art = new() { Stretch = Stretch.UniformToFill, ClipToBounds = true };
    private readonly TextBlock artworkFallback = Label("♪", 30, WidgetTheme.Tertiary, FontWeights.Light);
    private readonly TextBlock source = Label("", 10, WidgetTheme.Tertiary, FontWeights.SemiBold);
    private readonly TextBlock title = Label("", 18, WidgetTheme.Primary, FontWeights.SemiBold);
    private readonly TextBlock artist = Label("", 13, WidgetTheme.Secondary);
    private readonly TextBlock album = Label("", 11, WidgetTheme.Tertiary);
    private readonly TextBlock elapsed = Label("", 10, WidgetTheme.Secondary);
    private readonly TextBlock remaining = Label("", 10, WidgetTheme.Secondary);
    private readonly Button previous = CreateButton("⏮", false, "前へ");
    private readonly Button toggle = CreateButton("▶", true, "再生/一時停止");
    private readonly Button next = CreateButton("⏭", false, "次へ");
    private readonly Slider seek = new() { Minimum = 0, Maximum = 1, Background = Brushes.Transparent, Opacity = .01, Cursor = System.Windows.Input.Cursors.Hand };
    private readonly Grid timelineHost = new() { Height = 18, Margin = new Thickness(0, 7, 0, 0), ClipToBounds = false };
    private readonly Border timelineFill = new() { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromRgb(251, 198, 181)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private IDisposable? lease;
    private bool active;
    private bool dragging;
    private double timelineRatio;

    internal NowPlayingItemContent(CanvasItem item, INowPlayingService service)
    {
        this.item = item;
        this.service = service;
        previous.Click += async (_, _) => await service.PreviousAsync();
        toggle.Click += async (_, _) => await service.PlayPauseAsync();
        next.Click += async (_, _) => await service.NextAsync();
        seek.PreviewMouseLeftButtonDown += (_, _) => dragging = true;
        seek.PreviewMouseLeftButtonUp += async (_, _) =>
        {
            dragging = false;
            if (service.Snapshot.CanSeek) await service.SeekAsync(TimeSpan.FromSeconds(seek.Value));
        };
        seek.ValueChanged += (_, _) =>
        {
            if (dragging && seek.Maximum > 0)
            {
                timelineRatio = Math.Clamp(seek.Value / seek.Maximum, 0, 1);
                UpdateTimelineFill();
            }
        };

        var artworkGrid = new Grid();
        artworkFallback.HorizontalAlignment = HorizontalAlignment.Center;
        artworkFallback.VerticalAlignment = VerticalAlignment.Center;
        artworkGrid.Children.Add(artworkFallback);
        artworkGrid.Children.Add(art);
        artworkFrame = new Border
        {
            Width = 92,
            Height = 92,
            CornerRadius = new CornerRadius(17),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 14, 0),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
            BorderBrush = WidgetTheme.Hairline,
            BorderThickness = new Thickness(1),
            Child = artworkGrid
        };

        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        source.Margin = new Thickness(0, 0, 0, 3);
        title.Margin = new Thickness(0, 0, 0, 2);
        info.Children.Add(source);
        info.Children.Add(title);
        info.Children.Add(artist);
        info.Children.Add(album);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(artworkFrame);
        Grid.SetColumn(info, 1);
        header.Children.Add(info);

        var timelineTrack = new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
        timelineHost.Children.Add(timelineTrack);
        timelineHost.Children.Add(timelineFill);
        timelineHost.Children.Add(seek);
        timelineHost.SizeChanged += (_, _) => UpdateTimelineFill();

        var timeRow = new Grid { Margin = new Thickness(1, -1, 1, 0) };
        timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        timeRow.Children.Add(elapsed);
        Grid.SetColumn(remaining, 2);
        timeRow.Children.Add(remaining);

        var controls = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0)
        };
        controls.Children.Add(previous);
        controls.Children.Add(toggle);
        controls.Children.Add(next);

        var panel = new StackPanel();
        panel.Children.Add(header);
        panel.Children.Add(timelineHost);
        panel.Children.Add(timeRow);
        panel.Children.Add(controls);
        root = WidgetTheme.Card(panel);
        Update(service.Snapshot);
    }

    public UIElement View => root;
    public bool HasInteractiveControls => true;

    public bool IsInteractiveHit(Point point)
    {
        var hit = root.InputHitTest(point) as DependencyObject;
        while (hit is not null)
        {
            if (hit is Button or Slider) return true;
            hit = VisualTreeHelper.GetParent(hit);
        }
        return false;
    }

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        if (value)
        {
            service.SnapshotChanged += Service_SnapshotChanged;
            lease = service.Acquire();
            Update(service.Snapshot);
        }
        else
        {
            service.SnapshotChanged -= Service_SnapshotChanged;
            lease?.Dispose();
            lease = null;
        }
    }

    private void Service_SnapshotChanged(object? sender, NowPlayingSnapshot snapshot) => root.Dispatcher.BeginInvoke(() => Update(snapshot));

    private void Update(NowPlayingSnapshot snapshot)
    {
        title.Text = snapshot.Title;
        artist.Text = snapshot.Artist;
        album.Text = snapshot.Album;
        source.Text = item.NowPlaying.ShowSourceApp ? FriendlySource(snapshot.SourceApp) : "";
        source.Visibility = string.IsNullOrWhiteSpace(source.Text) ? Visibility.Collapsed : Visibility.Visible;
        artist.Visibility = string.IsNullOrWhiteSpace(artist.Text) ? Visibility.Collapsed : Visibility.Visible;
        album.Visibility = string.IsNullOrWhiteSpace(album.Text) ? Visibility.Collapsed : Visibility.Visible;
        artworkFrame.Visibility = item.NowPlaying.ShowAlbumArt ? Visibility.Visible : Visibility.Collapsed;

        root.Background = WidgetTheme.PlumSurface();
        art.Source = null;
        art.Visibility = Visibility.Collapsed;
        artworkFallback.Visibility = Visibility.Visible;
        if (snapshot.Artwork is not null)
        {
            try
            {
                using var stream = new MemoryStream(snapshot.Artwork);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                art.Source = image;
                art.Visibility = Visibility.Visible;
                artworkFallback.Visibility = Visibility.Collapsed;
                root.Background = ArtworkSurface(snapshot.Artwork);
            }
            catch (Exception)
            {
                art.Source = null;
            }
        }

        previous.IsEnabled = snapshot.CanPrevious;
        toggle.IsEnabled = snapshot.CanPlayPause;
        next.IsEnabled = snapshot.CanNext;
        toggle.Content = snapshot.State == NowPlayingState.Playing ? "Ⅱ" : "▶";

        var duration = Math.Max(0, snapshot.End.TotalSeconds);
        var showTimeline = item.NowPlaying.ShowTimeline && duration > 0;
        timelineHost.Visibility = showTimeline ? Visibility.Visible : Visibility.Collapsed;
        elapsed.Visibility = remaining.Visibility = showTimeline ? Visibility.Visible : Visibility.Collapsed;
        seek.Maximum = Math.Max(1, duration);
        seek.IsEnabled = snapshot.CanSeek;
        seek.IsHitTestVisible = snapshot.CanSeek;
        if (!dragging && duration > 0)
        {
            seek.Value = Math.Clamp(snapshot.Position.TotalSeconds, 0, duration);
            timelineRatio = Math.Clamp(seek.Value / duration, 0, 1);
            UpdateTimelineFill();
        }
        elapsed.Text = snapshot.Position.ToString(@"m\:ss");
        remaining.Text = "−" + (snapshot.End > snapshot.Position ? snapshot.End - snapshot.Position : TimeSpan.Zero).ToString(@"m\:ss");
    }

    private void UpdateTimelineFill() => timelineFill.Width = Math.Max(0, timelineHost.ActualWidth * timelineRatio);

    internal void Refresh() => Update(service.Snapshot);
    public void Dispose() => SetActive(false);

    private static TextBlock Label(string text, double size, Brush foreground, FontWeight? weight = null) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = foreground,
        FontWeight = weight ?? FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
        Margin = new Thickness(0, 1, 0, 1)
    };

    private static Button CreateButton(string content, bool primary, string tooltip)
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty, WidgetTheme.RoundButtonTemplate()));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 1d));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.FocusVisualStyleProperty, null));
        style.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(UIElement.OpacityProperty, .82d) } });
        style.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, .3d) } });
        return new Button
        {
            Content = content,
            Width = primary ? 44 : 36,
            Height = primary ? 44 : 36,
            MinWidth = 32,
            MinHeight = 32,
            Margin = new Thickness(primary ? 8 : 5, 0, primary ? 8 : 5, 0),
            Padding = new Thickness(0),
            FontSize = primary ? 16 : 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = primary ? new SolidColorBrush(Color.FromRgb(55, 30, 44)) : WidgetTheme.Primary,
            Background = primary ? new SolidColorBrush(Color.FromRgb(251, 208, 190)) : new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            BorderBrush = primary ? Brushes.Transparent : WidgetTheme.Hairline,
            BorderThickness = primary ? new Thickness(0) : new Thickness(1),
            ToolTip = tooltip,
            Style = style,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static string FriendlySource(string source) => string.IsNullOrWhiteSpace(source)
        ? ""
        : source.Length > 42 ? source[..42] + "…" : source;

    private static Brush ArtworkSurface(byte[] bytes)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null) return WidgetTheme.PlumSurface();
            long r = 0, g = 0, b = 0, count = 0;
            var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 24);
            for (var y = 0; y < bitmap.Height; y += step)
            {
                for (var x = 0; x < bitmap.Width; x += step)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    r += pixel.Red;
                    g += pixel.Green;
                    b += pixel.Blue;
                    count++;
                }
            }
            var average = Color.FromArgb(
                242,
                (byte)Math.Clamp(r / count, 48, 104),
                (byte)Math.Clamp(g / count, 24, 78),
                (byte)Math.Clamp(b / count, 42, 104));
            return WidgetTheme.Gradient(average, Color.FromArgb(242, 26, 24, 38));
        }
        catch (Exception)
        {
            return WidgetTheme.PlumSurface();
        }
    }
}

internal sealed class SystemMonitorItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly ISystemMetricsService service;
    private readonly Border root;
    private readonly MeterRow cpu = new("CPU", Color.FromRgb(118, 197, 250));
    private readonly MeterRow memory = new("RAM", Color.FromRgb(137, 226, 187));
    private readonly MeterRow gpu = new("GPU", Color.FromRgb(199, 165, 245));
    private readonly TextBlock down = NetworkText(Color.FromRgb(116, 201, 250));
    private readonly TextBlock up = NetworkText(Color.FromRgb(250, 155, 178));
    private readonly Grid networkRow = new() { Margin = new Thickness(0, 7, 0, 0) };
    private IDisposable? lease;
    private bool active;

    internal SystemMonitorItemContent(CanvasItem item, ISystemMetricsService service)
    {
        this.item = item;
        this.service = service;
        var heading = new TextBlock
        {
            Text = "SYSTEM STATUS",
            Foreground = WidgetTheme.Tertiary,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(2, 0, 0, 5)
        };

        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        networkRow.Children.Add(NetworkPill(down, new Thickness(0, 0, 3, 0)));
        var upPill = NetworkPill(up, new Thickness(3, 0, 0, 0));
        Grid.SetColumn(upPill, 1);
        networkRow.Children.Add(upPill);

        var panel = new StackPanel();
        panel.Children.Add(heading);
        panel.Children.Add(cpu.View);
        panel.Children.Add(memory.View);
        panel.Children.Add(gpu.View);
        panel.Children.Add(networkRow);
        root = WidgetTheme.Card(panel);
        Update(service.Snapshot);
    }

    public UIElement View => root;

    public void SetActive(bool value)
    {
        if (active == value) return;
        active = value;
        if (value)
        {
            service.SnapshotChanged += Changed;
            lease = service.Acquire();
            Update(service.Snapshot);
        }
        else
        {
            service.SnapshotChanged -= Changed;
            lease?.Dispose();
            lease = null;
        }
    }

    private void Changed(object? sender, SystemMetricsSnapshot snapshot) => root.Dispatcher.BeginInvoke(() => Update(snapshot));

    private void Update(SystemMetricsSnapshot snapshot)
    {
        cpu.Update(snapshot.Cpu);
        memory.Update(snapshot.Memory);
        gpu.Update(snapshot.Gpu);
        down.Text = "↓  " + snapshot.Download.Text;
        up.Text = "↑  " + snapshot.Upload.Text;
        cpu.View.Visibility = item.SystemMonitor.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        memory.View.Visibility = item.SystemMonitor.ShowMemory ? Visibility.Visible : Visibility.Collapsed;
        gpu.View.Visibility = item.SystemMonitor.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        networkRow.Visibility = item.SystemMonitor.ShowNetwork ? Visibility.Visible : Visibility.Collapsed;
    }

    internal void Refresh() => Update(service.Snapshot);
    public void Dispose() => SetActive(false);

    private static TextBlock NetworkText(Color color) => new()
    {
        Foreground = new SolidColorBrush(color),
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        TextAlignment = TextAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    private static Border NetworkPill(TextBlock text, Thickness margin) => new()
    {
        Height = 28,
        Margin = margin,
        CornerRadius = new CornerRadius(14),
        Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)),
        BorderBrush = WidgetTheme.Hairline,
        BorderThickness = new Thickness(1),
        Child = text
    };

    private sealed class MeterRow
    {
        private readonly TextBlock value = new()
        {
            Foreground = WidgetTheme.Primary,
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 47,
            TextAlignment = TextAlignment.Right
        };
        private readonly Border track = new()
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)),
            ClipToBounds = true,
            Margin = new Thickness(9, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        private readonly Border fill;
        private double ratio;
        internal Border View { get; }

        internal MeterRow(string name, Color accent)
        {
            fill = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var trackGrid = new Grid();
            trackGrid.Children.Add(fill);
            track.Child = trackGrid;
            track.SizeChanged += (_, _) => UpdateFill();

            var label = new TextBlock
            {
                Text = name,
                Foreground = WidgetTheme.Primary,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                MinWidth = 33
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(label);
            Grid.SetColumn(track, 1);
            grid.Children.Add(track);
            Grid.SetColumn(value, 2);
            grid.Children.Add(value);
            View = new Border
            {
                Child = grid,
                Padding = new Thickness(9, 5, 9, 5),
                Margin = new Thickness(0, 2, 0, 2),
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(19, 255, 255, 255)),
                BorderBrush = WidgetTheme.Hairline,
                BorderThickness = new Thickness(1)
            };
        }

        internal void Update(MetricValue metric)
        {
            value.Text = metric.Text;
            ratio = Math.Clamp((metric.Value ?? 0) / 100d, 0, 1);
            fill.Opacity = metric.Availability == MetricAvailability.Available ? 1 : .25;
            View.Opacity = metric.Availability == MetricAvailability.Unavailable ? .62 : 1;
            UpdateFill();
        }

        private void UpdateFill() => fill.Width = Math.Max(0, track.ActualWidth * ratio);
    }
}