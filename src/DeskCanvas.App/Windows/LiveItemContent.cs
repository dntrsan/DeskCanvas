using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DeskCanvas.App.Services;
using DeskCanvas.Core;
using SkiaSharp;
using Image = System.Windows.Controls.Image;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace DeskCanvas.App.Windows;

internal readonly record struct WidgetPalette(Color SurfaceA, Color SurfaceB, Color Accent, Color Foreground, Color Muted);

/// <summary>
/// Thin facade over <see cref="Design"/> kept for the widget builders. All values
/// resolve to the shared palette so widgets and the management window stay in step.
/// </summary>
internal static class WidgetTheme
{
    internal static readonly Brush Primary = Design.LabelPrimaryBrush;
    internal static readonly Brush Secondary = Design.LabelSecondaryBrush;
    internal static readonly Brush Tertiary = Design.LabelTertiaryBrush;
    internal static readonly Brush Hairline = Design.Frozen(Design.Argb(0x24, 0xFF, 0xFF, 0xFF));
    internal static readonly CornerRadius CardRadius = Design.CardRadius;
    internal static readonly Thickness CardPadding = Design.CardPadding;

    internal static Brush Surface() => Design.CardSurface();

    /// <summary>
    /// Opaque surfaces for contract tests and the leftover V30 text-clock polish.
    /// The live cards themselves use <see cref="Card"/> / Design tokens, not these gradients.
    /// </summary>
    internal static LinearGradientBrush Gradient(Color a, Color b) => new(
        new GradientStopCollection { new(Color.FromArgb(255, a.R, a.G, a.B), 0), new(Color.FromArgb(255, b.R, b.G, b.B), 1) },
        135);

    internal static WidgetPalette Resolve(CanvasItem? item = null, byte[]? artwork = null)
    {
        var kind = item?.Theme ?? WidgetThemeKind.Auto;
        if (kind == WidgetThemeKind.Auto && artwork is { Length: > 0 } && TryArtwork(artwork, out var art))
        {
            return art;
        }

        return kind switch
        {
            WidgetThemeKind.Light => Finish(Design.Rgb(0xFA, 0xF9, 0xF7), Design.Rgb(0xE5, 0xE8, 0xEE), Design.Rgb(0x64, 0x5B, 0xB0)),
            WidgetThemeKind.Rose => Finish(Design.Rgb(0x7E, 0x39, 0x54), Design.Rgb(0x33, 0x1B, 0x2E), Design.Rgb(0xFF, 0xB8, 0xAA)),
            WidgetThemeKind.Ocean => Finish(Design.Rgb(0x27, 0x67, 0x8B), Design.Rgb(0x12, 0x27, 0x49), Design.Rgb(0x6F, 0xD4, 0xF6)),
            WidgetThemeKind.Mint => Finish(Design.Rgb(0x30, 0x79, 0x65), Design.Rgb(0x14, 0x3A, 0x34), Design.Rgb(0x89, 0xE7, 0xBF)),
            WidgetThemeKind.Dark => Finish(Design.Rgb(0x2B, 0x2D, 0x37), Design.Rgb(0x12, 0x14, 0x1B), Design.Rgb(0xF2, 0xAD, 0x9A)),
            _ => Finish(Design.Surface, Design.WindowBackground, Design.Accent)
        };
    }

    private static bool TryArtwork(byte[] bytes, out WidgetPalette result)
    {
        result = default;
        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null || bitmap.Width < 2 || bitmap.Height < 2) return false;
            var pixels = new List<PaletteRgb>();
            var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 32);
            for (var y = 0; y < bitmap.Height; y += step)
            {
                for (var x = 0; x < bitmap.Width; x += step)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.Alpha >= 224) pixels.Add(new PaletteRgb(pixel.Red, pixel.Green, pixel.Blue));
                }
            }
            if (!ArtworkPaletteMathV18.TryResolve(pixels, out var palette)) return false;
            result = Finish(
                Color.FromRgb(palette.SurfaceA.R, palette.SurfaceA.G, palette.SurfaceA.B),
                Color.FromRgb(palette.SurfaceB.R, palette.SurfaceB.G, palette.SurfaceB.B),
                Color.FromRgb(palette.Accent.R, palette.Accent.G, palette.Accent.B));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static WidgetPalette Finish(Color a, Color b, Color accent)
    {
        a = Color.FromArgb(255, a.R, a.G, a.B);
        b = Color.FromArgb(255, b.R, b.G, b.B);
        accent = Color.FromArgb(255, accent.R, accent.G, accent.B);
        var luminance = (.2126 * a.R + .7152 * a.G + .0722 * a.B) / 255;
        var ink = luminance > .48 ? Color.FromRgb(30, 30, 37) : Color.FromRgb(250, 248, 246);
        var muted = ink.R < 40 ? Color.FromRgb(80, 79, 88) : Color.FromRgb(211, 205, 213);
        return new WidgetPalette(a, b, accent, ink, muted);
    }

    internal static WidgetSurfaceStyle SurfaceOf(CanvasItem? item) => item?.ContentKind switch
    {
        CanvasContentKinds.Clock => item.Clock.SurfaceStyle,
        CanvasContentKinds.NowPlaying => item.NowPlaying.SurfaceStyle,
        CanvasContentKinds.SystemMonitor => item.SystemMonitor.SurfaceStyle,
        _ => WidgetSurfaceStyle.Standard
    };

    internal static bool IsGlass(CanvasItem? item) =>
        SurfaceOf(item) is WidgetSurfaceStyle.MinimalGlass or WidgetSurfaceStyle.LiquidGlass;

    internal static Brush InnerFill(CanvasItem? item) =>
        IsGlass(item) ? Design.GlassControlFillBrush : Design.ControlFillBrush;

    internal static Border Card(UIElement child, CanvasItem? item = null)
    {
        var sheen = new Border
        {
            IsHitTestVisible = false,
            CornerRadius = CardRadius,
            Visibility = Visibility.Collapsed,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Design.Argb(0x3A, 0xFF, 0xFF, 0xFF), 0),
                    new(Design.Argb(0x00, 0xFF, 0xFF, 0xFF), 0.42),
                    new(Design.Argb(0x14, 0xFF, 0xFF, 0xFF), 1)
                },
                90)
        };
        var scrim = new Border
        {
            IsHitTestVisible = false,
            CornerRadius = CardRadius,
            Visibility = Visibility.Collapsed,
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new(Design.Argb(0x00, 0x00, 0x00, 0x00), 0),
                    new(Design.Argb(0x18, 0x00, 0x00, 0x00), 1)
                },
                90)
        };
        var content = new Border
        {
            Background = Brushes.Transparent,
            Padding = CardPadding,
            Child = child
        };
        var host = new Grid();
        host.Children.Add(scrim);
        host.Children.Add(content);
        host.Children.Add(sheen);
        var card = new Border
        {
            Child = host,
            Tag = new CardChrome(sheen, scrim),
            Background = Surface(),
            BorderBrush = Hairline,
            BorderThickness = new Thickness(1),
            CornerRadius = CardRadius,
            Padding = new Thickness(0),
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            Effect = Design.CardShadow()
        };
        ApplySurface(card, item);
        return card;
    }

    internal static void ApplySurface(Border card, CanvasItem? item)
    {
        if (card.Tag is CardChrome chrome)
        {
            chrome.Sheen.Visibility = SurfaceOf(item) == WidgetSurfaceStyle.MinimalGlass ? Visibility.Visible : Visibility.Collapsed;
            chrome.Scrim.Visibility = SurfaceOf(item) == WidgetSurfaceStyle.LiquidGlass ? Visibility.Visible : Visibility.Collapsed;
        }

        if (item is not null && SurfaceOf(item) == WidgetSurfaceStyle.LiquidGlass && GlassSurface.TryApply(card, item))
        {
            var glassShadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 4,
                BlurRadius = 22,
                Opacity = .22,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality
            };
            glassShadow.Freeze();
            card.Effect = glassShadow;
            card.BorderBrush = Design.Frozen(Design.Argb(0x22, 0xFF, 0xFF, 0xFF));
            return;
        }

        GlassSurface.Release(card);

        if (IsGlass(item))
        {
            card.Background = Design.Frozen(Design.Argb(0x88, 0x1C, 0x1C, 0x1E));
            card.BorderBrush = Design.Frozen(Design.Argb(0x55, 0xFF, 0xFF, 0xFF));
            var shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 4,
                BlurRadius = 22,
                Opacity = .22,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Quality
            };
            shadow.Freeze();
            card.Effect = shadow;
            return;
        }

        card.Background = Surface();
        card.BorderBrush = Hairline;
        card.Effect = Design.CardShadow();
    }

    private sealed record CardChrome(Border Sheen, Border Scrim);

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

/// <summary>
/// Transport icons as filled vector geometry. Drawn rather than typeset because the
/// media control code points render as boxed symbol-font glyphs on Windows, which
/// breaks the flat, monochrome look of the card.
/// </summary>
internal static class TransportGlyph
{
    internal const string Previous = "M 1,1 H 2.6 V 11 H 1 Z M 11,1 L 3.6,6 L 11,11 Z";
    internal const string Next = "M 11,1 H 9.4 V 11 H 11 Z M 1,1 L 8.4,6 L 1,11 Z";
    internal const string Play = "M 2.8,1 L 11,6 L 2.8,11 Z";
    internal const string Pause = "M 2,1 H 4.5 V 11 H 2 Z M 7.5,1 H 10 V 11 H 7.5 Z";

    internal static System.Windows.Shapes.Path Create(string data, Brush fill, double size) => new()
    {
        Data = System.Windows.Media.Geometry.Parse(data),
        Fill = fill,
        Stretch = Stretch.Uniform,
        Width = size,
        Height = size,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
}

internal sealed class NowPlayingItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly INowPlayingService service;
    private readonly Border root;
    private readonly Border artworkFrame;
    private readonly Image art = new() { Stretch = Stretch.UniformToFill, ClipToBounds = true };
    private readonly TextBlock artworkFallback = Label("♪", 28, WidgetTheme.Tertiary, FontWeights.Light);
    private readonly TextBlock source = Label("", 10, WidgetTheme.Tertiary, FontWeights.SemiBold);
    private readonly TextBlock title = Label("", 18, WidgetTheme.Primary, FontWeights.SemiBold);
    private readonly TextBlock artist = Label("", 13, WidgetTheme.Secondary);
    private readonly TextBlock album = Label("", 11, WidgetTheme.Tertiary);
    private readonly TextBlock elapsed = Label("", 10, WidgetTheme.Secondary);
    private readonly TextBlock remaining = Label("", 10, WidgetTheme.Secondary);
    private readonly Button previous = CreateButton(TransportGlyph.Previous, false, "前へ");
    private readonly Button toggle = CreateButton(TransportGlyph.Play, true, "再生/一時停止");
    private readonly Button next = CreateButton(TransportGlyph.Next, false, "次へ");
    private readonly Slider seek = new() { Minimum = 0, Maximum = 1, Background = Brushes.Transparent, Opacity = .01, Cursor = System.Windows.Input.Cursors.Hand };
    private readonly Grid timelineHost = new() { Height = 16, Margin = new Thickness(0, 10, 0, 0), ClipToBounds = false };
    private readonly Border timelineFill = new() { Height = 3, CornerRadius = new CornerRadius(1.5), Background = Design.Frozen(Design.Argb(0xE0, 0xFF, 0xFF, 0xFF)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
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
            Width = 88,
            Height = 88,
            CornerRadius = new CornerRadius(13),
            ClipToBounds = true,
            Margin = new Thickness(0, 0, 15, 0),
            Background = WidgetTheme.InnerFill(item),
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
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Background = Design.Frozen(Design.Argb(0x3D, 0xFF, 0xFF, 0xFF)),
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
        root = WidgetTheme.Card(panel, item);
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

        WidgetTheme.ApplySurface(root, item);
        artworkFrame.Background = WidgetTheme.InnerFill(item);
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
                if (!WidgetTheme.IsGlass(item)) root.Background = ArtworkSurface(snapshot.Artwork);
            }
            catch (Exception)
            {
                art.Source = null;
            }
        }

        previous.IsEnabled = snapshot.CanPrevious;
        toggle.IsEnabled = snapshot.CanPlayPause;
        next.IsEnabled = snapshot.CanNext;
        toggle.Content = TransportGlyph.Create(
            snapshot.State == NowPlayingState.Playing ? TransportGlyph.Pause : TransportGlyph.Play,
            toggle.Foreground,
            13);

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

    private static TextBlock Label(string text, double size, Brush foreground, FontWeight? weight = null)
    {
        var block = Design.Text(text, size, foreground, weight);
        block.Margin = new Thickness(0, 1, 0, 1);
        return Design.Tabular(block);
    }

    /// <summary>
    /// Transport controls: bare white glyphs for skip, a solid white disc for
    /// play/pause. Only the primary action carries a fill.
    /// </summary>
    private static Button CreateButton(string glyph, bool primary, string tooltip)
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.TemplateProperty, WidgetTheme.RoundButtonTemplate()));
        style.Setters.Add(new Setter(UIElement.OpacityProperty, 1d));
        style.Setters.Add(new Setter(System.Windows.Controls.Control.FocusVisualStyleProperty, null));
        style.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true, Setters = { new Setter(UIElement.OpacityProperty, .72d) } });
        style.Triggers.Add(new Trigger { Property = UIElement.IsEnabledProperty, Value = false, Setters = { new Setter(UIElement.OpacityProperty, .28d) } });
        var foreground = primary ? Design.Frozen(Design.WindowBackground) : WidgetTheme.Primary;
        return new Button
        {
            Content = TransportGlyph.Create(glyph, foreground, primary ? 13 : 12),
            Width = primary ? 38 : 32,
            Height = primary ? 38 : 32,
            MinWidth = 28,
            MinHeight = 28,
            Margin = new Thickness(primary ? 12 : 4, 0, primary ? 12 : 4, 0),
            Padding = new Thickness(0),
            Foreground = foreground,
            Background = primary ? Design.Frozen(Design.LabelPrimary) : Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ToolTip = tooltip,
            Style = style,
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private static string FriendlySource(string source) => string.IsNullOrWhiteSpace(source)
        ? ""
        : source.Length > 42 ? source[..42] + "…" : source;

    /// <summary>
    /// Samples the artwork and nudges the neutral card toward it. The tint is kept
    /// deliberately weak and dark so white text keeps its contrast ratio.
    /// </summary>
    private static Brush ArtworkSurface(byte[] bytes)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null) return WidgetTheme.Surface();
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
            if (count == 0) return WidgetTheme.Surface();
            var average = Color.FromRgb(
                (byte)Math.Clamp(r / count, 24, 96),
                (byte)Math.Clamp(g / count, 24, 96),
                (byte)Math.Clamp(b / count, 24, 96));
            return Design.TintedCardSurface(average);
        }
        catch (Exception)
        {
            return WidgetTheme.Surface();
        }
    }
}

internal sealed class SystemMonitorItemContent : IDesktopItemContent
{
    private readonly CanvasItem item;
    private readonly ISystemMetricsService service;
    private readonly Border root;
    private readonly Grid body = new();
    private readonly StackPanel metersPanel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid detailPanel = new();
    private readonly MeterRow cpu = new("CPU", Design.MeterCpu);
    private readonly MeterRow memory = new("RAM", Design.MeterMemory);
    private readonly MeterRow gpu = new("GPU", Design.MeterGpu);
    private readonly TextBlock heading = Design.Text("SYSTEM", 10, WidgetTheme.Tertiary, FontWeights.SemiBold);
    private readonly TextBlock down = NetworkText(Design.NetworkDown);
    private readonly TextBlock up = NetworkText(Design.NetworkUp);
    private readonly Border downPill;
    private readonly Border upPill;
    private readonly Grid networkRow = new() { Margin = new Thickness(0, 8, 0, 0) };
    private readonly DetailRow cpuRow = new("CPU", SystemMonitorFocus.Cpu);
    private readonly DetailRow gpuRow = new("GPU", SystemMonitorFocus.Gpu);
    private readonly DetailRow vramRow = new("VRAM", SystemMonitorFocus.Vram);
    private readonly DetailRow ramRow = new("RAM", SystemMonitorFocus.Memory);
    private readonly DetailRow netRow = new("NET", SystemMonitorFocus.Network);
    private readonly TextBlock headline = Design.Tabular(Design.Text("", 42, WidgetTheme.Primary, FontWeights.SemiBold, display: true));
    private readonly TextBlock secondary = Design.Tabular(Design.Text("", 13, WidgetTheme.Secondary));
    private readonly TextBlock scaleLabel = Design.Tabular(Design.Text("100", 10, WidgetTheme.Tertiary));
    private readonly Polygon sparkFill = new() { StrokeThickness = 0 };
    private readonly Polyline sparkLine = new() { StrokeThickness = 2.0, StrokeLineJoin = PenLineJoin.Round };
    private readonly Canvas sparkHost = new() { ClipToBounds = true, Margin = new Thickness(0, 6, 0, 0) };
    private IDisposable? lease;
    private bool active;
    private SystemMetricsSnapshot snapshot;

    internal Action? Persist { get; set; }

    internal SystemMonitorItemContent(CanvasItem item, ISystemMetricsService service)
    {
        this.item = item;
        this.service = service;
        snapshot = service.Snapshot;
        headline.TextAlignment = TextAlignment.Right;
        headline.HorizontalAlignment = HorizontalAlignment.Right;
        secondary.TextAlignment = TextAlignment.Right;
        secondary.HorizontalAlignment = HorizontalAlignment.Right;
        secondary.Margin = new Thickness(0, 2, 0, 0);
        scaleLabel.HorizontalAlignment = HorizontalAlignment.Right;
        sparkHost.Children.Add(sparkFill);
        sparkHost.Children.Add(sparkLine);
        sparkHost.SizeChanged += (_, _) => DrawSparkline();

        heading.Margin = new Thickness(3, 0, 0, 8);
        downPill = NetworkPill(down, new Thickness(0, 0, 3, 0));
        upPill = NetworkPill(up, new Thickness(3, 0, 0, 0));
        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        networkRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        networkRow.Children.Add(downPill);
        Grid.SetColumn(upPill, 1);
        networkRow.Children.Add(upPill);
        metersPanel.Children.Add(heading);
        metersPanel.Children.Add(cpu.View);
        metersPanel.Children.Add(memory.View);
        metersPanel.Children.Add(gpu.View);
        metersPanel.Children.Add(networkRow);

        foreach (var row in new[] { cpuRow, gpuRow, vramRow, ramRow, netRow })
        {
            row.View.MouseLeftButtonDown += (_, _) => Select(row.Focus);
        }

        var list = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        list.Children.Add(cpuRow.View);
        list.Children.Add(gpuRow.View);
        list.Children.Add(vramRow.View);
        list.Children.Add(ramRow.View);
        list.Children.Add(netRow.View);

        var headlineBox = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = headline, HorizontalAlignment = HorizontalAlignment.Right };
        var detail = new Grid { Margin = new Thickness(8, 0, 0, 0) };
        detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(0.7, GridUnitType.Star), MinHeight = 28 });
        detail.Children.Add(scaleLabel);
        Grid.SetRow(headlineBox, 1);
        detail.Children.Add(headlineBox);
        Grid.SetRow(secondary, 2);
        detail.Children.Add(secondary);
        Grid.SetRow(sparkHost, 3);
        detail.Children.Add(sparkHost);

        detailPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 86, MaxWidth = 150 });
        detailPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        detailPanel.Children.Add(list);
        Grid.SetColumn(detail, 1);
        detailPanel.Children.Add(detail);

        body.Children.Add(metersPanel);
        body.Children.Add(detailPanel);
        body.SizeChanged += (_, _) =>
        {
            ScaleDetail();
            ScaleMeters();
        };
        root = WidgetTheme.Card(body, item);
        Update(service.Snapshot);
    }

    public UIElement View => root;
    public bool HasInteractiveControls => item.SystemMonitor.Style == SystemMonitorStyle.Detail;

    public bool IsInteractiveHit(Point point)
    {
        var hit = root.InputHitTest(point) as DependencyObject;
        while (hit is not null)
        {
            if (hit is FrameworkElement element && Equals(element.Tag, "metric-row")) return true;
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

    private void Changed(object? sender, SystemMetricsSnapshot next) => root.Dispatcher.BeginInvoke(() => Update(next));

    private void Update(SystemMetricsSnapshot next)
    {
        snapshot = next;
        WidgetTheme.ApplySurface(root, item);
        var fill = WidgetTheme.InnerFill(item);
        downPill.Background = fill;
        upPill.Background = fill;
        var detail = item.SystemMonitor.Style == SystemMonitorStyle.Detail;
        metersPanel.Visibility = detail ? Visibility.Collapsed : Visibility.Visible;
        detailPanel.Visibility = detail ? Visibility.Visible : Visibility.Collapsed;
        cpu.Update(next.Cpu);
        memory.Update(next.Memory);
        gpu.Update(next.Gpu);
        down.Text = "↓  " + next.Download.Text;
        up.Text = "↑  " + next.Upload.Text;
        cpu.View.Visibility = item.SystemMonitor.ShowCpu ? Visibility.Visible : Visibility.Collapsed;
        memory.View.Visibility = item.SystemMonitor.ShowMemory ? Visibility.Visible : Visibility.Collapsed;
        gpu.View.Visibility = item.SystemMonitor.ShowGpu ? Visibility.Visible : Visibility.Collapsed;
        networkRow.Visibility = item.SystemMonitor.ShowNetwork ? Visibility.Visible : Visibility.Collapsed;
        if (detail) UpdateDetail();
        else ScaleMeters();
    }

    internal void Refresh() => Update(service.Snapshot);
    public void Dispose() => SetActive(false);

    private void Select(SystemMonitorFocus focus)
    {
        if (item.SystemMonitor.Focus == focus) return;
        item.SystemMonitor.Focus = focus;
        UpdateDetail();
        Persist?.Invoke();
    }

    private void UpdateDetail()
    {
        var options = item.SystemMonitor;
        var focus = ResolveFocus();
        var selectedFill = WidgetTheme.InnerFill(item);
        cpuRow.Bind(snapshot.Cpu, PercentText(snapshot.Cpu), options.ShowCpu, focus, selectedFill);
        gpuRow.Bind(snapshot.Gpu, PercentText(snapshot.Gpu), options.ShowGpu, focus, selectedFill);
        var vramVisible = options.ShowVram && snapshot.Vram.Availability != MetricAvailability.Unavailable;
        vramRow.Bind(snapshot.Vram, PercentText(snapshot.Vram), vramVisible, focus, selectedFill);
        ramRow.Bind(snapshot.Memory, PercentText(snapshot.Memory), options.ShowMemory, focus, selectedFill);
        netRow.Bind(snapshot.Download, CompactRate(snapshot.Download), options.ShowNetwork, focus, selectedFill);

        var (metric, history, accent, caption, floor) = focus switch
        {
            SystemMonitorFocus.Gpu => (snapshot.Gpu, snapshot.GpuHistory, Design.MeterGpu, "", 100d),
            SystemMonitorFocus.Vram => (snapshot.Vram, snapshot.VramHistory, Design.MeterVram, AfterPercent(snapshot.Vram.Text), 100d),
            SystemMonitorFocus.Memory => (snapshot.Memory, snapshot.MemoryHistory, Design.MeterMemory, AfterPercent(snapshot.Memory.Text), 100d),
            SystemMonitorFocus.Network => (snapshot.Download, snapshot.DownloadHistory, Design.NetworkDown, $"↓  {snapshot.Download.Text}   ↑  {snapshot.Upload.Text}", 0d),
            _ => (snapshot.Cpu, snapshot.CpuHistory, Design.MeterCpu, snapshot.Clock.Text, 100d)
        };

        headline.Text = focus == SystemMonitorFocus.Network ? CompactRate(metric) : PercentText(metric);
        secondary.Text = caption;
        secondary.Visibility = string.IsNullOrWhiteSpace(caption) ? Visibility.Collapsed : Visibility.Visible;
        scaleLabel.Text = focus == SystemMonitorFocus.Network ? "" : "100";
        sparkLine.Stroke = Design.Frozen(accent);
        var stops = new GradientStopCollection
        {
            new(Design.Argb(0x40, accent.R, accent.G, accent.B), 0),
            new(Design.Argb(0x0A, accent.R, accent.G, accent.B), 1)
        };
        stops.Freeze();
        var fillBrush = new LinearGradientBrush(stops, 90);
        fillBrush.Freeze();
        sparkFill.Fill = fillBrush;
        DrawSparkline(history, floor);
        ScaleDetail();
    }

    private SystemMonitorFocus ResolveFocus()
    {
        var focus = Enum.IsDefined(item.SystemMonitor.Focus) ? item.SystemMonitor.Focus : SystemMonitorFocus.Cpu;
        if (RowVisible(focus)) return focus;
        foreach (var candidate in new[] { SystemMonitorFocus.Cpu, SystemMonitorFocus.Gpu, SystemMonitorFocus.Vram, SystemMonitorFocus.Memory, SystemMonitorFocus.Network })
        {
            if (RowVisible(candidate)) return candidate;
        }
        return SystemMonitorFocus.Cpu;
    }

    private bool RowVisible(SystemMonitorFocus focus) => focus switch
    {
        SystemMonitorFocus.Cpu => item.SystemMonitor.ShowCpu,
        SystemMonitorFocus.Gpu => item.SystemMonitor.ShowGpu,
        SystemMonitorFocus.Vram => item.SystemMonitor.ShowVram && snapshot.Vram.Availability != MetricAvailability.Unavailable,
        SystemMonitorFocus.Memory => item.SystemMonitor.ShowMemory,
        SystemMonitorFocus.Network => item.SystemMonitor.ShowNetwork,
        _ => false
    };

    private void DrawSparkline() => DrawSparkline(HistoryFor(ResolveFocus()), ResolveFocus() == SystemMonitorFocus.Network ? 0 : 100);

    private IReadOnlyList<double> HistoryFor(SystemMonitorFocus focus) => focus switch
    {
        SystemMonitorFocus.Gpu => snapshot.GpuHistory,
        SystemMonitorFocus.Vram => snapshot.VramHistory,
        SystemMonitorFocus.Memory => snapshot.MemoryHistory,
        SystemMonitorFocus.Network => snapshot.DownloadHistory,
        _ => snapshot.CpuHistory
    };

    private void DrawSparkline(IReadOnlyList<double> history, double floor)
    {
        var width = Math.Max(1, sparkHost.ActualWidth);
        var height = Math.Max(1, sparkHost.ActualHeight);
        var line = SparklineGeometry.Build(history, width, height, floor);
        sparkLine.Points = new PointCollection(line.Select(point => new Point(point.X, point.Y)));
        sparkFill.Points = new PointCollection(SparklineGeometry.Area(line, height).Select(point => new Point(point.X, point.Y)));
    }

    private void ScaleDetail()
    {
        if (item.SystemMonitor.Style != SystemMonitorStyle.Detail) return;
        var scale = Math.Clamp(Math.Min(Math.Max(root.ActualWidth, 1) / 360d, Math.Max(root.ActualHeight, 1) / 200d), 0.78, 1.45);
        headline.FontSize = 42 * scale;
        secondary.FontSize = 13 * scale;
        foreach (var row in new[] { cpuRow, gpuRow, vramRow, ramRow, netRow }) row.SetScale(scale);
    }

    private void ScaleMeters()
    {
        if (item.SystemMonitor.Style != SystemMonitorStyle.Meters) return;
        var scale = Math.Clamp(Math.Min(Math.Max(root.ActualWidth, 1) / 360d, Math.Max(root.ActualHeight, 1) / 200d), 0.78, 1.45);
        heading.FontSize = 10 * scale;
        heading.Margin = new Thickness(3 * scale, 0, 0, 8 * scale);
        down.FontSize = 11 * scale;
        up.FontSize = 11 * scale;
        downPill.Height = 26 * scale;
        upPill.Height = 26 * scale;
        downPill.CornerRadius = new CornerRadius(13 * scale);
        upPill.CornerRadius = new CornerRadius(13 * scale);
        networkRow.Margin = new Thickness(0, 8 * scale, 0, 0);
        var meterRows = (item.SystemMonitor.ShowCpu ? 1 : 0) + (item.SystemMonitor.ShowMemory ? 1 : 0) + (item.SystemMonitor.ShowGpu ? 1 : 0);
        var extra = MeterLayoutMath.RowPadding(body.ActualHeight, MeterLayoutMath.NaturalHeight(scale, meterRows, item.SystemMonitor.ShowNetwork), meterRows);
        cpu.SetScale(scale, extra);
        memory.SetScale(scale, extra);
        gpu.SetScale(scale, extra);
    }

    private static string PercentText(MetricValue metric) =>
        metric.Availability == MetricAvailability.Available && metric.Value is { } value ? $"{value:0}%" : metric.Text;

    private static string CompactRate(MetricValue metric) => metric.Text.Replace("/秒", "");

    private static string AfterPercent(string text)
    {
        var index = text.IndexOf("%", StringComparison.Ordinal);
        return index >= 0 && index + 1 < text.Length ? text[(index + 1)..].Trim() : "";
    }

    private static TextBlock NetworkText(Color color)
    {
        var block = Design.Text("", 11, Design.Frozen(color), FontWeights.SemiBold);
        block.TextAlignment = TextAlignment.Center;
        return Design.Tabular(block);
    }

    private static Border NetworkPill(TextBlock text, Thickness margin) => new()
    {
        Height = 26,
        Margin = margin,
        CornerRadius = new CornerRadius(13),
        Background = Brushes.Transparent,
        BorderBrush = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Child = text
    };

    private sealed class DetailRow
    {
        private readonly TextBlock name;
        private readonly TextBlock value;
        internal SystemMonitorFocus Focus { get; }
        internal Border View { get; }

        internal DetailRow(string label, SystemMonitorFocus focus)
        {
            Focus = focus;
            name = Design.Text(label, 11, WidgetTheme.Primary, FontWeights.SemiBold);
            value = Design.Tabular(Design.Text("", 11, WidgetTheme.Secondary));
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.TextAlignment = TextAlignment.Right;
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(name);
            Grid.SetColumn(value, 1);
            grid.Children.Add(value);
            View = new Border
            {
                Tag = "metric-row",
                Child = grid,
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 1, 0, 1),
                CornerRadius = Design.WellRadius,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
        }

        internal void Bind(MetricValue metric, string display, bool visible, SystemMonitorFocus selected, Brush selectedFill)
        {
            View.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            value.Text = display;
            View.Background = selected == Focus ? selectedFill : Brushes.Transparent;
            View.Opacity = metric.Availability == MetricAvailability.Unavailable ? .62 : 1;
        }

        internal void SetScale(double scale)
        {
            name.FontSize = 11 * scale;
            value.FontSize = 11 * scale;
            View.Padding = new Thickness(8 * scale, 5 * scale, 8 * scale, 5 * scale);
        }
    }

    private sealed class MeterRow
    {
        private readonly TextBlock value;
        private readonly TextBlock label;
        private readonly Border track = new()
        {
            Height = 4,
            CornerRadius = new CornerRadius(2),
            Background = Design.Frozen(Design.Argb(0x24, 0xFF, 0xFF, 0xFF)),
            ClipToBounds = true,
            Margin = new Thickness(12, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        private readonly Border fill;
        private double ratio;
        internal Border View { get; }

        internal MeterRow(string name, Color accent)
        {
            value = Design.Tabular(Design.Text("", 11, WidgetTheme.Secondary));
            value.HorizontalAlignment = HorizontalAlignment.Right;
            value.MinWidth = 46;
            value.TextAlignment = TextAlignment.Right;

            fill = new Border
            {
                Height = 4,
                CornerRadius = new CornerRadius(2),
                Background = Design.Frozen(accent),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var trackGrid = new Grid();
            trackGrid.Children.Add(fill);
            track.Child = trackGrid;
            track.SizeChanged += (_, _) => UpdateFill();

            label = Design.Text(name, 11, WidgetTheme.Primary, FontWeights.SemiBold);
            label.MinWidth = 32;
            label.VerticalAlignment = VerticalAlignment.Center;

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
                Padding = new Thickness(3, 6, 3, 6),
                Margin = new Thickness(0, 1, 0, 1),
                CornerRadius = new CornerRadius(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
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

        internal void SetScale(double scale, double extraPadding)
        {
            value.FontSize = 11 * scale;
            label.FontSize = 11 * scale;
            value.MinWidth = 46 * scale;
            label.MinWidth = 32 * scale;
            track.Height = 4 * scale;
            fill.Height = 4 * scale;
            track.CornerRadius = new CornerRadius(2 * scale);
            fill.CornerRadius = new CornerRadius(2 * scale);
            track.Margin = new Thickness(12 * scale, 0, 10 * scale, 0);
            View.Padding = new Thickness(3 * scale, 6 * scale + extraPadding, 3 * scale, 6 * scale + extraPadding);
            View.Margin = new Thickness(0, scale, 0, scale);
            UpdateFill();
        }

        private void UpdateFill() => fill.Width = Math.Max(0, track.ActualWidth * ratio);
    }
}

/// <summary>Pure wave math. The phase unit is one wavelength; 0.1 is one period per ten seconds.</summary>
public static class WaveProgressMath
{
    public const double Wavelength = 18;
    public const double RunningSpeed = .1;

    public static double ClampRatio(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

    public static double Envelope(double x, double width)
    {
        if (width <= 4 || x <= 2 || x >= width - 2) return 0;
        return Math.Sin(Math.PI * Math.Clamp((x - 2) / (width - 4), 0, 1));
    }

    public static double NextSpeed(double current, bool playing, double elapsedSeconds)
    {
        var target = playing ? RunningSpeed : 0d;
        var duration = playing ? .8 : .6;
        var delta = RunningSpeed * Math.Max(0, elapsedSeconds) / duration;
        return current < target ? Math.Min(target, current + delta) : Math.Max(target, current - delta);
    }

    public static double AdvancePhase(double phase, double speed, double elapsedSeconds)
    {
        var next = phase + Math.Max(0, speed) * Math.Max(0, elapsedSeconds);
        return next - Math.Floor(next);
    }

    public static IReadOnlyList<(double Offset, bool Accent)> GradientStops(double ratio)
    {
        ratio = ClampRatio(ratio);
        return [(0, true), (ratio, true), (ratio, false), (1, false)];
    }
}