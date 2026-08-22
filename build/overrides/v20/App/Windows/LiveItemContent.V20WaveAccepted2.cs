
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
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;

namespace DeskCanvas.App.Windows;

internal readonly record struct WidgetPalette(WpfColor SurfaceA, WpfColor SurfaceB, WpfColor Accent, WpfColor Foreground, WpfColor Muted);
internal static class WidgetTheme
{
    private static readonly WpfColor LightInk = WpfColor.FromRgb(30, 30, 37), DarkInk = WpfColor.FromRgb(250, 248, 246);
    internal static readonly CornerRadius CardRadius = new(25);
    internal static readonly Thickness CardPadding = new(14);
    internal static LinearGradientBrush Gradient(WpfColor a, WpfColor b) => new(new GradientStopCollection { new(Opaque(a), 0), new(Opaque(b), 1) }, 135);
    internal static Border Card(CanvasItem item, UIElement child, byte[]? artwork = null)
    {
        var result = new Border { Child = child, BorderThickness = new Thickness(1), CornerRadius = CardRadius, Padding = CardPadding, ClipToBounds = true, Effect = new DropShadowEffect { Color = Colors.Black, ShadowDepth = 4, BlurRadius = 20, Opacity = .3 } };
        Apply(result, Resolve(item, artwork)); return result;
    }
    internal static void Apply(Border card, WidgetPalette p)
    {
        card.Background = Gradient(p.SurfaceA, p.SurfaceB); card.Foreground = new SolidColorBrush(p.Foreground);
        card.BorderBrush = new SolidColorBrush(p.Foreground == LightInk ? WpfColor.FromArgb(42, 0, 0, 0) : WpfColor.FromArgb(48, 255, 255, 255));
    }
    internal static WidgetPalette Resolve(CanvasItem item, byte[]? artwork = null)
    {
        if (item.Theme == WidgetThemeKind.Auto && artwork is { Length: > 0 } && TryArtwork(artwork, out var art)) return art;
        return item.Theme switch
        {
            WidgetThemeKind.Light => Finish(WpfColor.FromRgb(250, 249, 247), WpfColor.FromRgb(229, 232, 238), WpfColor.FromRgb(100, 91, 176)),
            WidgetThemeKind.Dark => Finish(WpfColor.FromRgb(43, 45, 55), WpfColor.FromRgb(18, 20, 27), WpfColor.FromRgb(242, 173, 154)),
            WidgetThemeKind.Rose => Finish(WpfColor.FromRgb(126, 57, 84), WpfColor.FromRgb(51, 27, 46), WpfColor.FromRgb(255, 184, 170)),
            WidgetThemeKind.Ocean => Finish(WpfColor.FromRgb(39, 103, 139), WpfColor.FromRgb(18, 39, 73), WpfColor.FromRgb(111, 212, 246)),
            WidgetThemeKind.Mint => Finish(WpfColor.FromRgb(48, 121, 101), WpfColor.FromRgb(20, 58, 52), WpfColor.FromRgb(137, 231, 191)),
            _ => WindowsPalette()
        };
    }
    internal static WpfColor Harmonize(WpfColor c, double d) => WpfColor.FromRgb((byte)Math.Clamp(c.R + 255 * d, 0, 255), (byte)Math.Clamp(c.G + 255 * d, 0, 255), (byte)Math.Clamp(c.B + 255 * d, 0, 255));
    internal static WpfColor Blend(WpfColor accent, WpfColor anchor, double weight) => WpfColor.FromRgb((byte)Math.Clamp(accent.R * (1 - weight) + anchor.R * weight, 0, 255), (byte)Math.Clamp(accent.G * (1 - weight) + anchor.G * weight, 0, 255), (byte)Math.Clamp(accent.B * (1 - weight) + anchor.B * weight, 0, 255));
    private static WidgetPalette WindowsPalette()
    {
        var light = false; try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); light = Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 0)) != 0; } catch { }
        var accent = WpfColor.FromRgb(93, 89, 201); try { if (DwmGetColorizationColor(out var raw, out _) == 0) accent = WpfColor.FromRgb((byte)(raw >> 16), (byte)(raw >> 8), (byte)raw); } catch { }
        return light ? Finish(WpfColor.FromRgb(250, 249, 247), WpfColor.FromRgb(228, 231, 237), accent) : Finish(WpfColor.FromRgb(53, 48, 58), WpfColor.FromRgb(23, 24, 32), accent);
    }
    private static bool TryArtwork(byte[] bytes, out WidgetPalette result)
    {
        result = default; try
        {
            using var bitmap = SKBitmap.Decode(bytes); if (bitmap is null || bitmap.Width < 2 || bitmap.Height < 2) return false;
            var pixels = new List<PaletteRgb>(); var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 32);
            for (var y = 0; y < bitmap.Height; y += step) for (var x = 0; x < bitmap.Width; x += step) { var p = bitmap.GetPixel(x, y); if (p.Alpha >= 224) pixels.Add(new PaletteRgb(p.Red, p.Green, p.Blue)); }
            if (!ArtworkPaletteMathV18.TryResolve(pixels, out var palette)) return false;
            result = Finish(WpfColor.FromRgb(palette.SurfaceA.R, palette.SurfaceA.G, palette.SurfaceA.B), WpfColor.FromRgb(palette.SurfaceB.R, palette.SurfaceB.G, palette.SurfaceB.B), WpfColor.FromRgb(palette.Accent.R, palette.Accent.G, palette.Accent.B)); return true;
        }
        catch { return false; }
    }
    private static WidgetPalette Finish(WpfColor a, WpfColor b, WpfColor accent)
    {
        a = Opaque(a); b = Opaque(b); accent = Opaque(accent); var lum = (.2126 * a.R + .7152 * a.G + .0722 * a.B) / 255; var ink = lum > .48 ? LightInk : DarkInk;
        return new(a, b, accent, ink, ink == LightInk ? WpfColor.FromRgb(80, 79, 88) : WpfColor.FromRgb(211, 205, 213));
    }
    private static WpfColor Opaque(WpfColor c) => WpfColor.FromArgb(255, c.R, c.G, c.B);
    [DllImport("dwmapi.dll")] private static extern int DwmGetColorizationColor(out uint colorizationColor, out bool opaqueBlend);
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
        var target = playing ? RunningSpeed : 0d; var duration = playing ? .8 : .6;
        var delta = RunningSpeed * Math.Max(0, elapsedSeconds) / duration;
        return current < target ? Math.Min(target, current + delta) : Math.Max(target, current - delta);
    }
    public static double AdvancePhase(double phase, double speed, double elapsedSeconds)
    {
        var next = phase + Math.Max(0, speed) * Math.Max(0, elapsedSeconds); return next - Math.Floor(next);
    }
    public static IReadOnlyList<(double Offset, bool Accent)> GradientStops(double ratio)
    {
        ratio = ClampRatio(ratio); return [(0, true), (ratio, true), (ratio, false), (1, false)];
    }
}

internal sealed class NowPlayingItemContent : IDesktopItemContent
{
    private readonly CanvasItem item; private readonly INowPlayingService service; private readonly ActivationService activation = new(); private readonly Border root;
    private readonly Rectangle artwork = new() { Width = 94, Height = 94, RadiusX = 17, RadiusY = 17, Stretch = Stretch.Fill, Cursor = Cursors.Hand };
    private readonly Grid artworkHost = new() { Width = 94, Height = 94 };
    private readonly Canvas titleViewport = new() { ClipToBounds = true, Cursor = Cursors.Hand, Height = 27 };
    private readonly TextBlock title = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.NoWrap, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly TranslateTransform titleTransform = new();
    private readonly TextBlock artist = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis }, source = new() { FontSize = 9, TextTrimming = TextTrimming.CharacterEllipsis }, elapsed = new() { FontSize = 10 }, remaining = new() { FontSize = 10 };
    private readonly Slider seek = new() { Minimum = 0, Maximum = 1, Opacity = .01, Cursor = Cursors.Hand };
    private readonly Border timelineTrack = new() { Height = 4, CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Center };
    private readonly Border timelineFill = new() { Height = 4, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
    private readonly Canvas timelineArt = new() { IsHitTestVisible = false };
    private readonly Grid timeline = new() { Height = 16, Margin = new Thickness(0, 4, 0, 0) };
    private readonly WpfButton previous, playPause, next;
    private readonly StackPanel visualizer = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer playbackTimer = new() { Interval = BackgroundLoadPolicy.PlaybackSpectrumInterval };
    private readonly DispatcherTimer waveTimer = new() { Interval = BackgroundLoadPolicy.WaveAnimationInterval };
    private readonly AudioSpectrumLeaseController spectrumLevel; private IDisposable? lease; private NowPlayingSnapshot shown = NowPlayingSnapshot.Empty;
    private bool active, dragging, disposed; private string marqueeIdentity = ""; private double marqueeViewportWidth = -1, wavePhase, waveSpeed; private DateTime waveTickAt; private WidgetPalette palette; private int artworkFingerprint = int.MinValue; private WidgetSurfaceStyle appliedSurfaceStyle = (WidgetSurfaceStyle)(-1);

    internal NowPlayingItemContent(CanvasItem item, INowPlayingService service, Func<IAudioSpectrumReader>? spectrumReaderFactory = null)
    {
        this.item = item; this.service = service; spectrumLevel = new AudioSpectrumLeaseController(spectrumReaderFactory ?? (static () => new WasapiLoopbackSpectrumReader()));
        previous = Transport(PreviousGeometry(), 40, "前へ"); playPause = Transport(PlayGeometry(), 46, "再生 / 一時停止", true); next = Transport(NextGeometry(), 40, "次へ");
        artworkHost.Margin = new Thickness(0, 0, 14, 0); artworkHost.Clip = new RectangleGeometry(new Rect(0, 0, 94, 94), 17, 17); artworkHost.Children.Add(artwork);
        artworkHost.MouseLeftButtonUp += Activate; titleViewport.MouseLeftButtonUp += Activate; title.RenderTransform = titleTransform; titleViewport.Children.Add(title);
        titleViewport.SizeChanged += (_, _) => { if (active && Math.Abs(titleViewport.ActualWidth - marqueeViewportWidth) > .5) RecalculateMarquee(); };
        var info = new Grid { VerticalAlignment = VerticalAlignment.Center }; for (var i = 0; i < 3; i++) info.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); source.Margin = new Thickness(0, 0, 0, 2);
        info.Children.Add(source); Grid.SetRow(titleViewport, 1); info.Children.Add(titleViewport); Grid.SetRow(artist, 2); info.Children.Add(artist);
        var header = new Grid { Height = 94 }; header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); header.Children.Add(artworkHost); Grid.SetColumn(info, 1); header.Children.Add(info);
        timeline.Children.Add(timelineTrack); timeline.Children.Add(timelineFill); timeline.Children.Add(timelineArt); timeline.Children.Add(seek); timeline.SizeChanged += (_, _) => PaintTimeline();
        seek.PreviewMouseLeftButtonDown += (_, _) => dragging = true; seek.PreviewMouseLeftButtonUp += async (_, _) => { dragging = false; if (shown.CanSeek) await service.SeekAsync(TimeSpan.FromSeconds(seek.Value)); }; seek.ValueChanged += (_, _) => { if (dragging) PaintTimeline(); };
        previous.Click += async (_, _) => await service.PreviousAsync(); playPause.Click += async (_, _) => await service.PlayPauseAsync(); next.Click += async (_, _) => await service.NextAsync();
        for (var i = 0; i < 7; i++) visualizer.Children.Add(new Border { Width = 3, Height = 5, Margin = new Thickness(1, 0, 1, 0), CornerRadius = new CornerRadius(1.5), VerticalAlignment = VerticalAlignment.Center });
        var time = new Grid { Margin = new Thickness(1, -1, 1, 0) }; time.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); time.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); time.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); time.Children.Add(elapsed); Grid.SetColumn(remaining, 2); time.Children.Add(remaining);
        var controls = new Grid { Height = 46, Margin = new Thickness(0, 3, 0, 0) }; controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
        var trio = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; trio.Children.Add(previous); trio.Children.Add(playPause); trio.Children.Add(next); controls.Children.Add(trio); Grid.SetColumn(trio, 1); visualizer.Width = 35; visualizer.HorizontalAlignment = HorizontalAlignment.Right; controls.Children.Add(visualizer); Grid.SetColumn(visualizer, 2);
        var panel = new Grid(); foreach (var h in new[] { new GridLength(94), GridLength.Auto, GridLength.Auto, GridLength.Auto }) panel.RowDefinitions.Add(new RowDefinition { Height = h }); panel.Children.Add(header); Grid.SetRow(timeline, 1); panel.Children.Add(timeline); Grid.SetRow(time, 2); panel.Children.Add(time); Grid.SetRow(controls, 3); panel.Children.Add(controls);
        root = WidgetTheme.Card(item, panel); playbackTimer.Tick += PlaybackTick; waveTimer.Tick += WaveTick; root.IsVisibleChanged += RootVisibilityChanged; Update(service.Snapshot);
    }
    public UIElement View => root; public bool HasInteractiveControls => true;
    public bool IsInteractiveHit(Point p)
    {
        var hit = root.InputHitTest(p) as DependencyObject; while (hit is not null) { if (hit is WpfButton or Slider || ReferenceEquals(hit, artwork) || ReferenceEquals(hit, artworkHost) || ReferenceEquals(hit, title) || ReferenceEquals(hit, titleViewport)) return true; hit = VisualTreeHelper.GetParent(hit); } return false;
    }
    public void SetActive(bool value)
    {
        if (disposed || active == value) return; active = value;
        if (value) { service.SnapshotChanged += Changed; lease = service.Acquire(); Update(service.Snapshot); root.Dispatcher.BeginInvoke(RecalculateMarquee, DispatcherPriority.Loaded); }
        else { service.SnapshotChanged -= Changed; lease?.Dispose(); lease = null; playbackTimer.Stop(); StopWaveImmediately(); spectrumLevel.Dispose(); StopMarquee(); Freeze(); }
    }
    internal void Refresh() => Update(service.Snapshot);
    public void Dispose()
    {
        if (disposed) return; SetActive(false); disposed = true; playbackTimer.Tick -= PlaybackTick; waveTimer.Tick -= WaveTick; root.IsVisibleChanged -= RootVisibilityChanged; StopWaveImmediately(); spectrumLevel.Dispose(); StopMarquee();
    }
    private void Changed(object? _, NowPlayingSnapshot snapshot) => root.Dispatcher.BeginInvoke(() => Update(snapshot));
    private void RootVisibilityChanged(object? _, DependencyPropertyChangedEventArgs __)
    {
        if (!root.IsVisible) StopWaveImmediately(); else UpdateWaveMotion();
    }
    private void Update(NowPlayingSnapshot snapshot)
    {
        var identity = string.Join("\u001f", snapshot.SourceApp, snapshot.Title, snapshot.Artist, snapshot.Album); var mediaChanged = !string.Equals(identity, marqueeIdentity, StringComparison.Ordinal); marqueeIdentity = identity; shown = snapshot;
        title.Text = snapshot.Title; artist.Text = snapshot.Artist; source.Text = item.NowPlaying.ShowSourceApp ? Friendly(snapshot.SourceApp) : ""; source.Visibility = string.IsNullOrWhiteSpace(source.Text) ? Visibility.Collapsed : Visibility.Visible; artist.Visibility = string.IsNullOrWhiteSpace(artist.Text) ? Visibility.Collapsed : Visibility.Visible; artworkHost.Visibility = item.NowPlaying.ShowAlbumArt ? Visibility.Visible : Visibility.Collapsed;
        var nextArtworkFingerprint = ArtworkFingerprint(snapshot.Artwork); var surfaceChanged = appliedSurfaceStyle != item.NowPlaying.SurfaceStyle; if (mediaChanged || surfaceChanged || nextArtworkFingerprint != artworkFingerprint) { artworkFingerprint = nextArtworkFingerprint; appliedSurfaceStyle = item.NowPlaying.SurfaceStyle; ApplyArtwork(snapshot.Artwork); ApplyPalette(item.NowPlaying.SurfaceStyle == WidgetSurfaceStyle.MinimalGlass ? MinimalGlassStyle.Palette : WidgetTheme.Resolve(item, snapshot.Artwork)); } previous.IsEnabled = snapshot.CanPrevious; playPause.IsEnabled = snapshot.CanPlayPause; next.IsEnabled = snapshot.CanNext; playPause.Content = Icon(snapshot.State == NowPlayingState.Playing ? PauseGeometry() : PlayGeometry(), 19);
        var duration = Math.Max(0, snapshot.End.TotalSeconds); var showTimeline = item.NowPlaying.ShowTimeline && duration > 0; timeline.Visibility = showTimeline ? Visibility.Visible : Visibility.Collapsed; elapsed.Visibility = remaining.Visibility = showTimeline ? Visibility.Visible : Visibility.Collapsed; seek.Maximum = Math.Max(1, duration); seek.IsEnabled = seek.IsHitTestVisible = snapshot.CanSeek;
        if (!dragging) seek.Value = Math.Clamp(snapshot.Position.TotalSeconds, 0, seek.Maximum); elapsed.Text = snapshot.Position.ToString(@"m\:ss"); remaining.Text = "−" + (snapshot.End > snapshot.Position ? snapshot.End - snapshot.Position : TimeSpan.Zero).ToString(@"m\:ss"); visualizer.Visibility = item.NowPlaying.ShowSpectrum ? Visibility.Visible : Visibility.Collapsed; PaintTimeline();
        var needsPlaybackTicks = BackgroundLoadPolicy.NeedsPlaybackTicks(active, snapshot.State == NowPlayingState.Playing, item.NowPlaying.ShowTimeline, item.NowPlaying.ShowSpectrum); if (needsPlaybackTicks) { playbackTimer.Interval = BackgroundLoadPolicy.PlaybackInterval(item.NowPlaying.ShowSpectrum); if (!playbackTimer.IsEnabled) playbackTimer.Start(); } else { playbackTimer.Stop(); spectrumLevel.Suspend(); Freeze(); }
        if (!item.NowPlaying.ShowSpectrum) { spectrumLevel.Suspend(); Freeze(); } UpdateWaveMotion();
        if (mediaChanged) { StopMarquee(); root.Dispatcher.BeginInvoke(RecalculateMarquee, DispatcherPriority.Loaded); }
    }
    private void ApplyArtwork(byte[]? bytes)
    {
        artwork.Fill = Brushes.Transparent; if (bytes is not { Length: > 0 }) return; try { using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); artwork.Fill = new ImageBrush(image) { Stretch = Stretch.UniformToFill }; } catch { }
    }
    private static int ArtworkFingerprint(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return 0;
        var hash = new HashCode(); hash.Add(bytes.Length); var samples = Math.Min(16, bytes.Length);
        for (var index = 0; index < samples; index++) hash.Add(bytes[index * (bytes.Length - 1) / Math.Max(1, samples - 1)]);
        return hash.ToHashCode();
    }
    private void ApplyPalette(WidgetPalette p)
    {
        palette = p; if (item.NowPlaying.SurfaceStyle == WidgetSurfaceStyle.MinimalGlass) MinimalGlassStyle.Apply(root); else WidgetTheme.Apply(root, p); var foreground = new SolidColorBrush(p.Foreground); var muted = new SolidColorBrush(p.Muted); title.Foreground = foreground; artist.Foreground = muted; source.Foreground = muted; elapsed.Foreground = muted; remaining.Foreground = muted; timelineTrack.Background = new SolidColorBrush(p.Muted) { Opacity = .55 }; timelineFill.Background = new SolidColorBrush(p.Accent); SetIcon(previous, foreground); SetIcon(playPause, foreground); SetIcon(next, foreground); for (var i = 0; i < 7; i++) ((Border)visualizer.Children[i]).Background = new SolidColorBrush(WidgetTheme.Harmonize(p.Accent, (i - 3) * .018)); PaintTimeline();
    }
    private void PlaybackTick(object? _, EventArgs __)
    {
        var snapshot = service.Snapshot; shown = snapshot; if (!dragging) seek.Value = Math.Clamp(snapshot.Position.TotalSeconds, 0, seek.Maximum); elapsed.Text = snapshot.Position.ToString(@"m\:ss"); remaining.Text = "−" + (snapshot.End > snapshot.Position ? snapshot.End - snapshot.Position : TimeSpan.Zero).ToString(@"m\:ss"); if (item.NowPlaying.ProgressStyle == NowPlayingProgressStyle.Simple) PaintTimeline();
        if (item.NowPlaying.ShowSpectrum) spectrumLevel.Poll(active && shown.State == NowPlayingState.Playing, bands => { for (var i = 0; i < 7; i++) ((Border)visualizer.Children[i]).Height = 5 + Math.Clamp(i < bands.Count ? bands[i] : 0, 0, 1) * 17; }); else spectrumLevel.Suspend();
    }
    private void Freeze() { for (var i = 0; i < 7; i++) ((Border)visualizer.Children[i]).Height = 5 + i % 3 * 2; }
    private void PaintTimeline()
    {
        var ratio = WaveProgressMath.ClampRatio(seek.Value / Math.Max(1, seek.Maximum)); var simple = item.NowPlaying.ProgressStyle == NowPlayingProgressStyle.Simple; timelineTrack.Visibility = timelineFill.Visibility = simple ? Visibility.Visible : Visibility.Collapsed; timelineArt.Children.Clear();
        if (simple) { timelineFill.Width = Math.Max(0, timeline.ActualWidth * ratio); return; }
        if (timeline.ActualWidth <= 4) return; var geometry = WaveGeometry(timeline.ActualWidth, wavePhase); var brush = WaveBrush(ratio); timelineArt.Children.Add(new WpfPath { Data = geometry, Stroke = brush, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, SnapsToDevicePixels = true });
    }
    private StreamGeometry WaveGeometry(double width, double phase)
    {
        var geometry = new StreamGeometry(); const double center = 8, amplitude = 2; var start = 2d;
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(start, center), false, false);
            for (var x = start; x <= width - 2; x += 1.25) { var y = center + Math.Sin(2 * Math.PI * (x / WaveProgressMath.Wavelength + phase)) * amplitude * WaveProgressMath.Envelope(x, width); context.LineTo(new Point(Math.Min(x, width - 2), y), true, false); }
            context.LineTo(new Point(width - 2, center), true, false);
        }
        geometry.Freeze(); return geometry;
    }
    private Brush WaveBrush(double ratio)
    {
        if (item.NowPlaying.SurfaceStyle == WidgetSurfaceStyle.MinimalGlass) return new SolidColorBrush(palette.Accent);
        var brush = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5), MappingMode = BrushMappingMode.RelativeToBoundingBox };
        foreach (var stop in WaveProgressMath.GradientStops(ratio)) brush.GradientStops.Add(new GradientStop(stop.Accent ? palette.Accent : palette.Muted, stop.Offset)); return brush;
    }
    private void UpdateWaveMotion()
    {
        var customVisible = active && root.IsVisible && item.NowPlaying.ShowTimeline && item.NowPlaying.ProgressStyle == NowPlayingProgressStyle.Wave && timeline.Visibility == Visibility.Visible;
        if (!customVisible) { StopWaveImmediately(); return; }
        waveTickAt = DateTime.UtcNow; if (!waveTimer.IsEnabled) waveTimer.Start();
    }
    private void WaveTick(object? _, EventArgs __)
    {
        if (!active || !root.IsVisible || item.NowPlaying.ProgressStyle != NowPlayingProgressStyle.Wave || timeline.Visibility != Visibility.Visible) { StopWaveImmediately(); return; }
        var now = DateTime.UtcNow; var seconds = Math.Clamp((now - waveTickAt).TotalSeconds, 0, .2); waveTickAt = now; var playing = shown.State == NowPlayingState.Playing; waveSpeed = WaveProgressMath.NextSpeed(waveSpeed, playing, seconds); wavePhase = WaveProgressMath.AdvancePhase(wavePhase, waveSpeed, seconds); PaintTimeline();
        if (!playing && waveSpeed <= 0) waveTimer.Stop();
    }
    private void StopWaveImmediately() { waveTimer.Stop(); waveSpeed = 0; }
    private void RecalculateMarquee()
    {
        StopMarquee(); marqueeViewportWidth = titleViewport.ActualWidth; if (!active || marqueeViewportWidth <= 0) return; var formatted = new FormattedText(title.Text ?? "", System.Globalization.CultureInfo.CurrentUICulture, title.FlowDirection, new Typeface(title.FontFamily, title.FontStyle, title.FontWeight, title.FontStretch), title.FontSize, title.Foreground, VisualTreeHelper.GetDpi(title).PixelsPerDip); var fullWidth = Math.Ceiling(formatted.WidthIncludingTrailingWhitespace); title.Width = fullWidth; var overflow = fullWidth - marqueeViewportWidth; if (overflow <= .5) return;
        titleViewport.Clip = new RectangleGeometry(new Rect(0, 0, marqueeViewportWidth, titleViewport.ActualHeight)); var travel = overflow / 24d; var end = 1.2 + travel; var hold = end + .8; var back = hold + .45; var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(back), RepeatBehavior = RepeatBehavior.Forever }; animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero))); animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.2)))); animation.KeyFrames.Add(new LinearDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(end)))); animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(-overflow, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(hold)))); animation.KeyFrames.Add(new CubicEaseDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(back))) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } }); titleTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }
    private void StopMarquee() { titleTransform.BeginAnimation(TranslateTransform.XProperty, null); titleTransform.X = 0; titleViewport.Clip = null; }
    private void Activate(object? _, MouseButtonEventArgs e) { activation.TryActivate(shown.SourceApp, shown.Title, shown.Artist); e.Handled = true; }
    private static WpfButton Transport(Geometry geometry, double size, string tooltip, bool play = false)
    {
        var button = new WpfButton { Width = size, Height = size, MinWidth = size, MinHeight = size, Margin = new Thickness(3, 0, 3, 0), Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), FocusVisualStyle = null, ToolTip = tooltip, Cursor = Cursors.Hand, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(1, 1), Template = ButtonTemplate(), Content = Icon(geometry, play ? 19 : 17) };
        button.MouseEnter += (_, _) => { if (button.IsEnabled) button.Opacity = .82; }; button.MouseLeave += (_, _) => { button.Opacity = button.IsEnabled ? 1 : .32; Scale(button, 1); }; button.PreviewMouseLeftButtonDown += (_, _) => Scale(button, .94); button.PreviewMouseLeftButtonUp += (_, _) => Scale(button, 1); button.IsEnabledChanged += (_, _) => button.Opacity = button.IsEnabled ? 1 : .32; return button;
    }
    private static ControlTemplate ButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(11)); border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) }); var presenter = new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetBinding(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("Content") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) }); presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(presenter); return new ControlTemplate(typeof(WpfButton)) { VisualTree = border };
    }
    private static Viewbox Icon(Geometry geometry, double size) => new() { Width = size, Height = size, Child = new WpfPath { Data = geometry, Fill = Brushes.White, Stretch = Stretch.Uniform } };
    private static void SetIcon(WpfButton button, Brush brush) { if (button.Content is Viewbox { Child: WpfPath path }) path.Fill = brush; }
    private static void Scale(WpfButton button, double scale) { if (button.RenderTransform is ScaleTransform transform) { transform.ScaleX = scale; transform.ScaleY = scale; } }
    private static Geometry PreviousGeometry() => Geometry.Parse("M3,3 L5,3 L5,13 L3,13 Z M14,3 L14,13 L5,8 Z"); private static Geometry PlayGeometry() => Geometry.Parse("M5,3 L15,8 L5,13 Z"); private static Geometry PauseGeometry() => Geometry.Parse("M5,3 L8,3 L8,13 L5,13 Z M11,3 L14,3 L14,13 L11,13 Z"); private static Geometry NextGeometry() => Geometry.Parse("M13,3 L15,3 L15,13 L13,13 Z M4,3 L4,13 L13,8 Z");
    private static string Friendly(string value) { var separator = value.IndexOf('!'); var result = separator > 0 ? value[..separator] : value; return result.Length > 36 ? result[..36] + "…" : result; }
}
