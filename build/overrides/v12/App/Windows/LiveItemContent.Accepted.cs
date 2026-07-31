using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using Microsoft.Win32;
using SkiaSharp;
using WpfBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;
using WpfCanvas = System.Windows.Controls.Canvas;
using WpfColor = System.Windows.Media.Color;
using WpfImageBrush = System.Windows.Media.ImageBrush;
using WpfPath = System.Windows.Shapes.Path;

namespace DeskCanvas.App.Windows;

internal readonly record struct WidgetPalette(
    WpfColor SurfaceA,
    WpfColor SurfaceB,
    WpfColor Accent,
    WpfColor Foreground,
    WpfColor Muted);

internal static class WidgetTheme
{
    private static readonly WpfColor LightInk = WpfColor.FromRgb(30, 30, 37);
    private static readonly WpfColor DarkInk = WpfColor.FromRgb(250, 248, 246);
    internal static readonly WpfBrush Primary = new SolidColorBrush(DarkInk);
    internal static readonly WpfBrush Secondary = new SolidColorBrush(WpfColor.FromRgb(220, 214, 218));
    internal static readonly WpfBrush Tertiary = new SolidColorBrush(WpfColor.FromRgb(181, 174, 183));
    internal static readonly WpfBrush Hairline = new SolidColorBrush(WpfColor.FromArgb(48, 255, 255, 255));
    internal static readonly CornerRadius CardRadius = new(25);
    internal static readonly Thickness CardPadding = new(14);

    internal static LinearGradientBrush PlumSurface() =>
        Gradient(WpfColor.FromRgb(69, 36, 57), WpfColor.FromRgb(27, 27, 39));

    internal static LinearGradientBrush Gradient(WpfColor a, WpfColor b) =>
        new(
            new GradientStopCollection
            {
                new(ForceOpaque(a), 0),
                new(ForceOpaque(b), 1)
            },
            135);

    internal static Border Card(UIElement child) => Card(new CanvasItem(), child, null);

    internal static Border Card(CanvasItem item, UIElement child, byte[]? artwork = null)
    {
        var card = new Border
        {
            Child = child,
            BorderThickness = new Thickness(1),
            CornerRadius = CardRadius,
            Padding = CardPadding,
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 4,
                BlurRadius = 20,
                Opacity = .3
            }
        };
        Apply(card, Resolve(item, artwork));
        return card;
    }

    internal static void Apply(Border card, WidgetPalette palette)
    {
        card.Background = Gradient(palette.SurfaceA, palette.SurfaceB);
        card.Foreground = new SolidColorBrush(palette.Foreground);
        card.BorderBrush = new SolidColorBrush(
            palette.Foreground == LightInk
                ? WpfColor.FromArgb(42, 0, 0, 0)
                : WpfColor.FromArgb(48, 255, 255, 255));
    }

    internal static WidgetPalette Resolve(CanvasItem item, byte[]? artwork = null)
    {
        if (item.Theme == WidgetThemeKind.Auto && artwork is { Length: > 0 } &&
            TryArtworkPalette(artwork, out var artworkPalette))
        {
            return artworkPalette;
        }

        return item.Theme switch
        {
            WidgetThemeKind.Light => Finish(
                WpfColor.FromRgb(250, 249, 247),
                WpfColor.FromRgb(229, 232, 238),
                WpfColor.FromRgb(100, 91, 176)),
            WidgetThemeKind.Dark => Finish(
                WpfColor.FromRgb(43, 45, 55),
                WpfColor.FromRgb(18, 20, 27),
                WpfColor.FromRgb(242, 173, 154)),
            WidgetThemeKind.Rose => Finish(
                WpfColor.FromRgb(126, 57, 84),
                WpfColor.FromRgb(51, 27, 46),
                WpfColor.FromRgb(255, 184, 170)),
            WidgetThemeKind.Ocean => Finish(
                WpfColor.FromRgb(39, 103, 139),
                WpfColor.FromRgb(18, 39, 73),
                WpfColor.FromRgb(111, 212, 246)),
            WidgetThemeKind.Mint => Finish(
                WpfColor.FromRgb(48, 121, 101),
                WpfColor.FromRgb(20, 58, 52),
                WpfColor.FromRgb(137, 231, 191)),
            _ => WindowsPalette()
        };
    }

    internal static WpfColor Harmonize(WpfColor accent, double lightnessDelta)
    {
        static byte Shift(byte value, double delta) =>
            (byte)Math.Clamp(value + 255 * delta, 0, 255);
        return WpfColor.FromRgb(
            Shift(accent.R, lightnessDelta),
            Shift(accent.G, lightnessDelta),
            Shift(accent.B, lightnessDelta));
    }

    private static WidgetPalette WindowsPalette()
    {
        var light = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) != 0;
        }
        catch (Exception)
        {
            light = false;
        }

        var accent = WpfColor.FromRgb(93, 89, 201);
        try
        {
            if (DwmGetColorizationColor(out var raw, out _) == 0)
            {
                accent = WpfColor.FromRgb(
                    (byte)((raw >> 16) & 0xff),
                    (byte)((raw >> 8) & 0xff),
                    (byte)(raw & 0xff));
            }
        }
        catch (Exception)
        {
            // Windows accent is optional; the deterministic fallback remains.
        }

        return light
            ? Finish(
                WpfColor.FromRgb(250, 249, 247),
                WpfColor.FromRgb(228, 231, 237),
                accent)
            : Finish(
                WpfColor.FromRgb(53, 48, 58),
                WpfColor.FromRgb(23, 24, 32),
                accent);
    }

    private static bool TryArtworkPalette(byte[] bytes, out WidgetPalette palette)
    {
        palette = default;
        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null || bitmap.Width == 0 || bitmap.Height == 0) return false;

            var left = new long[3];
            var right = new long[3];
            var leftCount = 0L;
            var rightCount = 0L;
            var bestSaturation = -1;
            var accent = new SKColor(235, 171, 154);
            var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 28);
            for (var y = 0; y < bitmap.Height; y += step)
            {
                for (var x = 0; x < bitmap.Width; x += step)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    var target = x < bitmap.Width / 2 ? left : right;
                    target[0] += pixel.Red;
                    target[1] += pixel.Green;
                    target[2] += pixel.Blue;
                    if (x < bitmap.Width / 2) leftCount++;
                    else rightCount++;

                    var max = Math.Max(pixel.Red, Math.Max(pixel.Green, pixel.Blue));
                    var min = Math.Min(pixel.Red, Math.Min(pixel.Green, pixel.Blue));
                    var saturation = max - min;
                    var luminance = (pixel.Red * 3 + pixel.Green * 6 + pixel.Blue) / 10;
                    if (saturation > bestSaturation && luminance is > 55 and < 225)
                    {
                        bestSaturation = saturation;
                        accent = pixel;
                    }
                }
            }

            if (leftCount == 0 || rightCount == 0) return false;
            var a = Tone(left, leftCount, .72);
            var b = Tone(right, rightCount, .48);
            var extractedAccent = WpfColor.FromRgb(accent.Red, accent.Green, accent.Blue);
            palette = Finish(a, b, extractedAccent);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static WpfColor Tone(long[] sum, long count, double multiplier) =>
        WpfColor.FromRgb(
            (byte)Math.Clamp(sum[0] / count * multiplier, 20, 170),
            (byte)Math.Clamp(sum[1] / count * multiplier, 20, 160),
            (byte)Math.Clamp(sum[2] / count * multiplier, 24, 175));

    private static WidgetPalette Finish(WpfColor a, WpfColor b, WpfColor accent)
    {
        a = ForceOpaque(a);
        b = ForceOpaque(b);
        accent = ForceOpaque(accent);
        var luminance = (RelativeLuminance(a) + RelativeLuminance(b)) / 2;
        var foreground = luminance > .48 ? LightInk : DarkInk;
        var muted = foreground == LightInk
            ? WpfColor.FromRgb(80, 79, 88)
            : WpfColor.FromRgb(211, 205, 213);
        return new WidgetPalette(a, b, accent, foreground, muted);
    }

    private static double RelativeLuminance(WpfColor color)
    {
        static double Linear(byte value)
        {
            var channel = value / 255d;
            return channel <= .04045
                ? channel / 12.92
                : Math.Pow((channel + .055) / 1.055, 2.4);
        }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }

    private static WpfColor ForceOpaque(WpfColor color) =>
        WpfColor.FromArgb(255, color.R, color.G, color.B);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetColorizationColor(out uint colorizationColor, out bool opaqueBlend);
}

internal sealed class NowPlayingItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly INowPlayingService service;
    private readonly ActivationService activation = new();
    private readonly Border root;
    private readonly Rectangle artwork = new()
    {
        Width = 94,
        Height = 94,
        RadiusX = 17,
        RadiusY = 17,
        Stretch = Stretch.Fill,
        Cursor = Cursors.Hand
    };
    private readonly Grid artworkHost = new() { Width = 94, Height = 94 };
    private readonly WpfPath artworkFallback = new()
    {
        Data = Geometry.Parse("M28,20 L68,20 L68,68 L28,68 Z M40,36 A7,7 0 1 0 40.1,36 M59,48 A6,6 0 1 0 59.1,48"),
        Stretch = Stretch.Uniform,
        Margin = new Thickness(25),
        Opacity = .52
    };
    private readonly Grid titleViewport = new()
    {
        ClipToBounds = true,
        Cursor = Cursors.Hand,
        Height = 27
    };
    private readonly TextBlock title = new()
    {
        FontSize = 18,
        FontWeight = FontWeights.SemiBold,
        TextWrapping = TextWrapping.NoWrap,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TranslateTransform titleTransform = new();
    private readonly TextBlock artist = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock source = new() { FontSize = 9, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock elapsed = new() { FontSize = 10 };
    private readonly TextBlock remaining = new() { FontSize = 10 };
    private readonly Slider seek = new()
    {
        Minimum = 0,
        Maximum = 1,
        Opacity = .01,
        Cursor = Cursors.Hand
    };
    private readonly Border timelineFill = new()
    {
        Height = 4,
        CornerRadius = new CornerRadius(2),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Grid timeline = new() { Height = 16, Margin = new Thickness(0, 4, 0, 0) };
    private readonly WpfButton previous;
    private readonly WpfButton playPause;
    private readonly WpfButton next;
    private readonly StackPanel visualizer = new()
    {
        Orientation = Orientation.Horizontal,
        Margin = new Thickness(6, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly DispatcherTimer playbackTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly double[] barPhase = [0, .7, 1.6, 2.2, 3.0, 3.7, 4.5];
    private IDisposable? lease;
    private NowPlayingSnapshot shown = NowPlayingSnapshot.Empty;
    private bool active;
    private bool dragging;
    private int pulse;
    private string marqueeIdentity = "";

    internal NowPlayingItemContent(CanvasItem item, INowPlayingService service)
    {
        this.item = item;
        this.service = service;
        previous = TransportButton(PreviousGeometry(), 40, "前へ");
        playPause = TransportButton(PlayGeometry(), 46, "再生 / 一時停止", true);
        next = TransportButton(NextGeometry(), 40, "次へ");

        artworkHost.Margin = new Thickness(0, 0, 14, 0);
        artworkHost.Clip = new RectangleGeometry(new Rect(0, 0, 94, 94), 17, 17);
        artworkHost.Children.Add(artworkFallback);
        artworkHost.Children.Add(artwork);
        artworkHost.MouseLeftButtonUp += ActivateSource;
        titleViewport.MouseLeftButtonUp += ActivateSource;
        title.RenderTransform = titleTransform;
        titleViewport.Children.Add(title);
        titleViewport.SizeChanged += (_, _) => RecalculateMarquee();

        var info = new Grid { VerticalAlignment = VerticalAlignment.Center };
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        source.Margin = new Thickness(0, 0, 0, 2);
        info.Children.Add(source);
        Grid.SetRow(titleViewport, 1);
        info.Children.Add(titleViewport);
        Grid.SetRow(artist, 2);
        info.Children.Add(artist);

        var header = new Grid { Height = 94 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.Children.Add(artworkHost);
        Grid.SetColumn(info, 1);
        header.Children.Add(info);

        timeline.Children.Add(new Border
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(WpfColor.FromArgb(62, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center
        });
        timeline.Children.Add(timelineFill);
        timeline.Children.Add(seek);
        timeline.SizeChanged += (_, _) => PaintTimeline();
        seek.PreviewMouseLeftButtonDown += (_, _) => dragging = true;
        seek.PreviewMouseLeftButtonUp += async (_, _) =>
        {
            dragging = false;
            if (shown.CanSeek) await service.SeekAsync(TimeSpan.FromSeconds(seek.Value));
        };
        seek.ValueChanged += (_, _) =>
        {
            if (dragging) PaintTimeline();
        };

        previous.Click += async (_, _) => await service.PreviousAsync();
        playPause.Click += async (_, _) => await service.PlayPauseAsync();
        next.Click += async (_, _) => await service.NextAsync();

        for (var index = 0; index < 7; index++)
        {
            visualizer.Children.Add(new Border
            {
                Width = 3,
                Height = 5,
                Margin = new Thickness(1, 0, 1, 0),
                CornerRadius = new CornerRadius(1.5),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

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
            Height = 46,
            Margin = new Thickness(0, 3, 0, 0)
        };
        controls.Children.Add(previous);
        controls.Children.Add(playPause);
        controls.Children.Add(next);
        controls.Children.Add(visualizer);

        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(94) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(header);
        Grid.SetRow(timeline, 1);
        panel.Children.Add(timeline);
        Grid.SetRow(timeRow, 2);
        panel.Children.Add(timeRow);
        Grid.SetRow(controls, 3);
        panel.Children.Add(controls);

        root = WidgetTheme.Card(item, panel);
        playbackTimer.Tick += PlaybackTimer_Tick;
        Update(service.Snapshot);
    }

    public UIElement View => root;
    public bool HasInteractiveControls => true;

    public bool IsInteractiveHit(Point point)
    {
        var hit = root.InputHitTest(point) as DependencyObject;
        while (hit is not null)
        {
            if (hit is WpfButton or Slider ||
                ReferenceEquals(hit, artwork) ||
                ReferenceEquals(hit, artworkHost) ||
                ReferenceEquals(hit, title) ||
                ReferenceEquals(hit, titleViewport))
            {
                return true;
            }
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
            playbackTimer.Stop();
            StopMarquee();
            FreezeVisualizer();
        }
    }

    internal void Refresh() => Update(service.Snapshot);

    public void Dispose()
    {
        SetActive(false);
        playbackTimer.Tick -= PlaybackTimer_Tick;
        titleTransform.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private void Service_SnapshotChanged(object? sender, NowPlayingSnapshot snapshot) =>
        root.Dispatcher.BeginInvoke(() => Update(snapshot));

    private void Update(NowPlayingSnapshot snapshot)
    {
        var identity = string.Join("\u001f", snapshot.SourceApp, snapshot.Title, snapshot.Artist, snapshot.Album);
        var mediaChanged = !string.Equals(identity, marqueeIdentity, StringComparison.Ordinal);
        marqueeIdentity = identity;
        shown = snapshot;

        title.Text = snapshot.Title;
        artist.Text = snapshot.Artist;
        source.Text = item.NowPlaying.ShowSourceApp ? FriendlySource(snapshot.SourceApp) : "";
        source.Visibility = string.IsNullOrWhiteSpace(source.Text) ? Visibility.Collapsed : Visibility.Visible;
        artist.Visibility = string.IsNullOrWhiteSpace(artist.Text) ? Visibility.Collapsed : Visibility.Visible;
        artworkHost.Visibility = item.NowPlaying.ShowAlbumArt ? Visibility.Visible : Visibility.Collapsed;

        ApplyArtwork(snapshot.Artwork);
        ApplyPalette(WidgetTheme.Resolve(item, snapshot.Artwork));

        previous.IsEnabled = snapshot.CanPrevious;
        playPause.IsEnabled = snapshot.CanPlayPause;
        next.IsEnabled = snapshot.CanNext;
        playPause.Content = Icon(
            snapshot.State == NowPlayingState.Playing ? PauseGeometry() : PlayGeometry(),
            19);

        var duration = Math.Max(0, snapshot.End.TotalSeconds);
        var showTimeline = item.NowPlaying.ShowTimeline && duration > 0;
        timeline.Visibility = showTimeline ? Visibility.Visible : Visibility.Collapsed;
        elapsed.Visibility = remaining.Visibility = showTimeline ? Visibility.Visible : Visibility.Collapsed;
        seek.Maximum = Math.Max(1, duration);
        seek.IsEnabled = snapshot.CanSeek;
        seek.IsHitTestVisible = snapshot.CanSeek;
        if (!dragging)
        {
            seek.Value = Math.Clamp(snapshot.Position.TotalSeconds, 0, seek.Maximum);
        }
        elapsed.Text = snapshot.Position.ToString(@"m\:ss");
        remaining.Text = "−" +
            (snapshot.End > snapshot.Position ? snapshot.End - snapshot.Position : TimeSpan.Zero)
            .ToString(@"m\:ss");
        PaintTimeline();

        if (active && snapshot.State == NowPlayingState.Playing)
        {
            if (!playbackTimer.IsEnabled) playbackTimer.Start();
        }
        else
        {
            playbackTimer.Stop();
            FreezeVisualizer();
        }

        if (mediaChanged)
        {
            titleTransform.X = 0;
            root.Dispatcher.BeginInvoke(RecalculateMarquee, DispatcherPriority.Loaded);
        }
        else
        {
            RecalculateMarquee();
        }
    }

    private void ApplyArtwork(byte[]? bytes)
    {
        artwork.Fill = Brushes.Transparent;
        artworkFallback.Visibility = Visibility.Visible;
        if (bytes is not { Length: > 0 }) return;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            artwork.Fill = new WpfImageBrush(image)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            artworkFallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception)
        {
            artwork.Fill = Brushes.Transparent;
        }
    }

    private void ApplyPalette(WidgetPalette palette)
    {
        WidgetTheme.Apply(root, palette);
        var foreground = new SolidColorBrush(palette.Foreground);
        var muted = new SolidColorBrush(palette.Muted);
        var accent = new SolidColorBrush(palette.Accent);
        title.Foreground = foreground;
        artist.Foreground = muted;
        source.Foreground = muted;
        elapsed.Foreground = muted;
        remaining.Foreground = muted;
        timelineFill.Background = accent;
        artworkFallback.Fill = muted;
        SetIconBrush(previous, foreground);
        SetIconBrush(next, foreground);
        SetIconBrush(playPause, foreground);
        for (var index = 0; index < visualizer.Children.Count; index++)
        {
            ((Border)visualizer.Children[index]).Background =
                new SolidColorBrush(WidgetTheme.Harmonize(palette.Accent, (index - 3) * .018));
        }
    }

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        var projected = service.Snapshot;
        shown = projected;
        if (!dragging)
        {
            seek.Value = Math.Clamp(projected.Position.TotalSeconds, 0, seek.Maximum);
        }
        elapsed.Text = projected.Position.ToString(@"m\:ss");
        remaining.Text = "−" +
            (projected.End > projected.Position ? projected.End - projected.Position : TimeSpan.Zero)
            .ToString(@"m\:ss");
        PaintTimeline();

        pulse++;
        for (var index = 0; index < visualizer.Children.Count; index++)
        {
            var wave = (Math.Sin(pulse * .42 + barPhase[index]) + 1) / 2;
            ((Border)visualizer.Children[index]).Height = 5 + wave * 17;
        }
    }

    private void FreezeVisualizer()
    {
        for (var index = 0; index < visualizer.Children.Count; index++)
        {
            ((Border)visualizer.Children[index]).Height = 5 + (index % 3) * 2;
        }
    }

    private void PaintTimeline()
    {
        var denominator = Math.Max(1, seek.Maximum);
        timelineFill.Width = Math.Max(0, timeline.ActualWidth * Math.Clamp(seek.Value / denominator, 0, 1));
    }

    private void RecalculateMarquee()
    {
        StopMarquee();
        if (!active || titleViewport.ActualWidth <= 0) return;
        title.Measure(new Size(double.PositiveInfinity, titleViewport.ActualHeight));
        var overflow = title.DesiredSize.Width - titleViewport.ActualWidth;
        if (overflow <= .5) return;

        titleViewport.Clip = new RectangleGeometry(
            new Rect(0, 0, titleViewport.ActualWidth, titleViewport.ActualHeight));
        var travelSeconds = Math.Max(.25, overflow / 24d);
        var travelEnd = 1.2 + travelSeconds;
        var holdEnd = travelEnd + .8;
        var returnEnd = holdEnd + .45;
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(returnEnd),
            RepeatBehavior = RepeatBehavior.Forever
        };
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.2))));
        animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(travelEnd))));
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(holdEnd))));
        animation.KeyFrames.Add(new CubicEaseDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(returnEnd)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });
        titleTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private void StopMarquee()
    {
        titleTransform.BeginAnimation(TranslateTransform.XProperty, null);
        titleTransform.X = 0;
    }

    private void ActivateSource(object? sender, MouseButtonEventArgs e)
    {
        activation.TryActivate(shown.SourceApp);
        e.Handled = true;
    }

    private static WpfButton TransportButton(Geometry geometry, double size, string tooltip, bool primary = false)
    {
        var button = new WpfButton
        {
            Width = size,
            Height = size,
            MinWidth = size,
            MinHeight = size,
            Margin = new Thickness(3, 0, 3, 0),
            Padding = new Thickness(0),
            Background = primary
                ? new SolidColorBrush(WpfColor.FromArgb(36, 255, 255, 255))
                : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FocusVisualStyle = null,
            ToolTip = tooltip,
            Cursor = Cursors.Hand,
            RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = new ScaleTransform(1, 1),
            Template = RoundButtonTemplate(),
            Content = Icon(geometry, primary ? 19 : 17)
        };
        button.MouseEnter += (_, _) =>
        {
            if (button.IsEnabled) button.Opacity = .82;
        };
        button.MouseLeave += (_, _) =>
        {
            button.Opacity = button.IsEnabled ? 1 : .32;
            SetScale(button, 1);
        };
        button.PreviewMouseLeftButtonDown += (_, _) => SetScale(button, .94);
        button.PreviewMouseLeftButtonUp += (_, _) => SetScale(button, 1);
        button.IsEnabledChanged += (_, _) =>
        {
            button.Opacity = button.IsEnabled ? 1 : .32;
            if (!button.IsEnabled) SetScale(button, 1);
        };
        return button;
    }

    private static ControlTemplate RoundButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(99));
        border.SetBinding(
            Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(
            ContentPresenter.ContentProperty,
            new System.Windows.Data.Binding("Content")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        return new ControlTemplate(typeof(WpfButton)) { VisualTree = border };
    }

    private static Viewbox Icon(Geometry geometry, double size) =>
        new()
        {
            Width = size,
            Height = size,
            Child = new WpfPath
            {
                Data = geometry,
                Fill = Brushes.White,
                Stretch = Stretch.Uniform
            }
        };

    private static void SetIconBrush(WpfButton button, WpfBrush brush)
    {
        if (button.Content is Viewbox { Child: WpfPath path }) path.Fill = brush;
    }

    private static void SetScale(WpfButton button, double value)
    {
        if (button.RenderTransform is ScaleTransform transform)
        {
            transform.ScaleX = value;
            transform.ScaleY = value;
        }
    }

    private static Geometry PreviousGeometry() =>
        Geometry.Parse("M3,3 L5,3 L5,13 L3,13 Z M14,3 L14,13 L5,8 Z");

    private static Geometry PlayGeometry() =>
        Geometry.Parse("M5,3 L15,8 L5,13 Z");

    private static Geometry PauseGeometry() =>
        Geometry.Parse("M5,3 L8,3 L8,13 L5,13 Z M11,3 L14,3 L14,13 L11,13 Z");

    private static Geometry NextGeometry() =>
        Geometry.Parse("M13,3 L15,3 L15,13 L13,13 Z M4,3 L4,13 L13,8 Z");

    private static string FriendlySource(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var separator = value.IndexOf('!');
        var label = separator > 0 ? value[..separator] : value;
        return label.Length > 36 ? label[..36] + "…" : label;
    }
}

internal sealed class SystemMonitorItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly ISystemMetricsService service;
    private readonly Border root;
    private readonly WpfPath brandMark = new()
    {
        Width = 18,
        Height = 18,
        Stretch = Stretch.Uniform,
        Data = Geometry.Parse("M9,1 L16,5 L16,13 L9,17 L2,13 L2,5 Z M9,5 L12,7 L12,11 L9,13 L6,11 L6,7 Z")
    };
    private readonly TextBlock heading = new()
    {
        Text = "SYSTEM STATUS",
        FontSize = 10,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(6, 0, 0, 0)
    };
    private readonly MetricRing cpu = new("CPU");
    private readonly MetricRing ram = new("RAM");
    private readonly MetricRing gpu = new("GPU");
    private readonly Grid networkRow = new() { Height = 32, Margin = new Thickness(0, 14, 0, 0) };
    private readonly TextBlock download = new() { FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock upload = new() { FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
    private IDisposable? lease;
    private bool active;

    internal SystemMonitorItemContent(CanvasItem item, ISystemMetricsService service)
    {
        this.item = item;
        this.service = service;
        var header = new StackPanel { Orientation = Orientation.Horizontal, Height = 18 };
        header.Children.Add(brandMark);
        header.Children.Add(heading);

        var rings = new UniformGrid
        {
            Columns = 3,
            Rows = 1,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        rings.Children.Add(cpu.View);
        rings.Children.Add(ram.View);
        rings.Children.Add(gpu.View);

        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        networkRow.Children.Add(NetworkPill(download));
        var up = NetworkPill(upload);
        Grid.SetColumn(up, 2);
        networkRow.Children.Add(up);

        var panel = new Grid();
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        panel.Children.Add(header);
        Grid.SetRow(rings, 1);
        panel.Children.Add(rings);
        Grid.SetRow(networkRow, 2);
        panel.Children.Add(networkRow);

        root = WidgetTheme.Card(item, panel);
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
            SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
            Update(service.Snapshot);
        }
        else
        {
            service.SnapshotChanged -= Changed;
            SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
            lease?.Dispose();
            lease = null;
        }
    }

    internal void Refresh() => Update(service.Snapshot);

    public void Dispose() => SetActive(false);

    private void Changed(object? sender, SystemMetricsSnapshot snapshot) =>
        root.Dispatcher.BeginInvoke(() => Update(snapshot));

    private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        root.Dispatcher.BeginInvoke(() => Update(service.Snapshot));

    private void Update(SystemMetricsSnapshot snapshot)
    {
        var palette = WidgetTheme.Resolve(item);
        WidgetTheme.Apply(root, palette);
        brandMark.Fill = new SolidColorBrush(palette.Accent);
        heading.Foreground = new SolidColorBrush(palette.Muted);

        var accents = new[]
        {
            WidgetTheme.Harmonize(palette.Accent, .08),
            WidgetTheme.Harmonize(palette.Accent, -.01),
            WidgetTheme.Harmonize(palette.Accent, -.09)
        };
        cpu.Update(snapshot.Cpu, ProcessorClock.Read(), accents[0], palette);
        ram.Update(snapshot.Memory, MemoryAux(snapshot.Memory), accents[1], palette);
        gpu.Update(snapshot.Gpu, GpuAux(snapshot.Gpu), accents[2], palette);

        download.Text = "↓  Download   " + snapshot.Download.Text;
        upload.Text = "↑  Upload   " + snapshot.Upload.Text;
        download.Foreground = new SolidColorBrush(accents[0]);
        upload.Foreground = new SolidColorBrush(accents[2]);

        cpu.View.Visibility = item.SystemMonitor.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        ram.View.Visibility = item.SystemMonitor.ShowMemory ? Visibility.Visible : Visibility.Collapsed;
        gpu.View.Visibility = item.SystemMonitor.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        networkRow.Visibility = item.SystemMonitor.ShowNetwork ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Border NetworkPill(TextBlock text) =>
        new()
        {
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(WpfColor.FromArgb(24, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(36, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(7, 0, 7, 0),
            Child = text
        };

    private static string MemoryAux(MetricValue metric)
    {
        var separator = metric.Text.IndexOf("  ", StringComparison.Ordinal);
        return separator >= 0 ? metric.Text[(separator + 2)..] : metric.Text;
    }

    private static string GpuAux(MetricValue metric) =>
        metric.Availability switch
        {
            MetricAvailability.Loading => "…",
            MetricAvailability.Unavailable => "--",
            _ => metric.Text
        };

    private sealed class MetricRing
    {
        private readonly WpfPath arc = new()
        {
            StrokeThickness = 5,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent
        };
        private readonly Ellipse track = new() { Width = 60, Height = 60, StrokeThickness = 5 };
        private readonly TextBlock value = new()
        {
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        private readonly TextBlock label;
        private readonly TextBlock auxiliary = new()
        {
            FontSize = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 86
        };
        internal Grid View { get; }

        internal MetricRing(string name)
        {
            label = new TextBlock
            {
                Text = name,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var dial = new Grid
            {
                Width = 60,
                Height = 60,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            dial.Children.Add(track);
            dial.Children.Add(arc);
            dial.Children.Add(value);

            View = new Grid { MinWidth = 72 };
            View.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            View.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            View.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            View.Children.Add(dial);
            Grid.SetRow(label, 1);
            View.Children.Add(label);
            Grid.SetRow(auxiliary, 2);
            View.Children.Add(auxiliary);
        }

        internal void Update(
            MetricValue metric,
            string aux,
            WpfColor accent,
            WidgetPalette palette)
        {
            value.Text = metric.Availability switch
            {
                MetricAvailability.Loading => "…",
                MetricAvailability.Unavailable => "--",
                _ => $"{metric.Value:0}%"
            };
            auxiliary.Text = aux;
            value.Foreground = new SolidColorBrush(palette.Foreground);
            label.Foreground = new SolidColorBrush(palette.Muted);
            auxiliary.Foreground = new SolidColorBrush(palette.Muted);
            track.Stroke = new SolidColorBrush(
                palette.Foreground == WpfColor.FromRgb(30, 30, 37)
                    ? WpfColor.FromArgb(36, 0, 0, 0)
                    : WpfColor.FromArgb(48, 255, 255, 255));
            arc.Stroke = metric.Availability == MetricAvailability.Available
                ? new SolidColorBrush(accent)
                : new SolidColorBrush(WpfColor.FromRgb(115, 115, 124));

            var percent = Math.Clamp(metric.Value ?? 0, 0, 100);
            arc.Data = ArcGeometry(percent);
        }

        private static Geometry ArcGeometry(double percent)
        {
            if (percent <= 0) return Geometry.Empty;
            var angle = Math.Min(359.99, percent * 3.6) * Math.PI / 180;
            var center = new Point(30, 30);
            const double radius = 27.5;
            var start = new Point(center.X, center.Y - radius);
            var end = new Point(
                center.X + radius * Math.Sin(angle),
                center.Y - radius * Math.Cos(angle));
            var figure = new PathFigure { StartPoint = start, IsClosed = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                IsLargeArc = angle > Math.PI,
                SweepDirection = SweepDirection.Clockwise
            });
            return new PathGeometry([figure]);
        }
    }
}

internal static class ProcessorClock
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessorPowerInformation
    {
        public uint Number;
        public uint MaxMhz;
        public uint CurrentMhz;
        public uint MhzLimit;
        public uint MaxIdleState;
        public uint CurrentIdleState;
    }

    [DllImport("powrprof.dll")]
    private static extern uint CallNtPowerInformation(
        int informationLevel,
        IntPtr inputBuffer,
        int inputBufferLength,
        [Out] ProcessorPowerInformation[] outputBuffer,
        int outputBufferLength);

    internal static string Read()
    {
        try
        {
            var values = new ProcessorPowerInformation[Math.Max(1, Environment.ProcessorCount)];
            var status = CallNtPowerInformation(
                11,
                IntPtr.Zero,
                0,
                values,
                Marshal.SizeOf<ProcessorPowerInformation>() * values.Length);
            return status == 0
                ? SystemMetricMath.FormatProcessorClock(values.Select(value => value.CurrentMhz))
                : "-- GHz";
        }
        catch (Exception)
        {
            return "-- GHz";
        }
    }
}
