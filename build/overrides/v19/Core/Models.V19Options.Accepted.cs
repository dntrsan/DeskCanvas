using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DeskCanvas.Core;

public static class CanvasContentKinds
{
    public const string Image = "image";
    public const string Gif = "gif";
    public const string Clock = "clock";
    public const string NowPlaying = "nowPlaying";
    public const string SystemMonitor = "systemMonitor";
    public static bool IsSupported(string? kind) => kind is Image or Gif or Clock or NowPlaying or SystemMonitor;
    public static bool IsImplemented(string? kind) => IsSupported(kind);
}

public static class DecorationModes
{
    public const string None = "None";
    public const string WhiteOutline = "WhiteOutline";
    public const string OuterFrame = "OuterFrame";
    public static bool IsSupported(string? value) => value is None or WhiteOutline or OuterFrame;
}

public enum WidgetThemeKind { Auto, Light, Dark, Rose, Ocean, Mint }
public enum ClockStyle { Digital, Split, Analog }
public enum NowPlayingProgressStyle { Simple, Wave, Dots, Hearts }

public sealed class CanvasLayout { public int Version { get; set; } = 2; public CanvasSettings Settings { get; set; } = new(); public List<CanvasItem> Items { get; set; } = []; }
public sealed class CanvasSettings { public bool StartWithWindows { get; set; } [JsonIgnore] public bool HideAll { get; set; } }

public sealed class ClockOptions
{
    public ClockStyle Style { get; set; } = ClockStyle.Digital;
    public bool Use24Hour { get; set; } = true;
    public bool ShowSeconds { get; set; } = true;
    public bool ShowMonthDay { get; set; } = true;
    public bool ShowYear { get; set; }
    public ClockOptions Clone() => new() { Style = Style, Use24Hour = Use24Hour, ShowSeconds = ShowSeconds, ShowMonthDay = ShowMonthDay, ShowYear = ShowYear };
}

/// <summary>Persisted presentation choices only. Playback data is always read live from Windows.</summary>
public sealed class NowPlayingOptions
{
    public bool ShowAlbumArt { get; set; } = true;
    public bool ShowTimeline { get; set; } = true;
    public bool ShowSourceApp { get; set; } = true;
    public bool ShowSpectrum { get; set; } = true;
    public NowPlayingProgressStyle ProgressStyle { get; set; } = NowPlayingProgressStyle.Simple;

    public NowPlayingOptions Clone() => new()
    {
        ShowAlbumArt = ShowAlbumArt,
        ShowTimeline = ShowTimeline,
        ShowSourceApp = ShowSourceApp,
        ShowSpectrum = ShowSpectrum,
        ProgressStyle = Enum.IsDefined(ProgressStyle) ? ProgressStyle : NowPlayingProgressStyle.Simple
    };
}

public sealed class SystemMonitorOptions
{
    public bool ShowCpu { get; set; } = true;
    public bool ShowMemory { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowNetwork { get; set; } = true;
    public SystemMonitorOptions Clone() => new() { ShowCpu = ShowCpu, ShowMemory = ShowMemory, ShowGpu = ShowGpu, ShowNetwork = ShowNetwork };
}

public static class ClockFormatting
{
    public static string DateFormat(ClockOptions options) => (options.ShowYear, options.ShowMonthDay) switch { (true, true) => "yyyy年 M月d日", (true, false) => "yyyy年", (false, true) => "M月d日", _ => "" };
    public static string FormatDate(DateTime value, ClockOptions options) { var format = DateFormat(options); return string.IsNullOrEmpty(format) ? "" : value.ToString(format, CultureInfo.InvariantCulture); }
}
public static class CanvasVisibility { public static bool IsEffectivelyVisible(CanvasSettings settings, CanvasItem item) => !settings.HideAll && !item.IsTemporarilyHidden; }

public sealed class CanvasItem : INotifyPropertyChanged
{
    private string displayName = "", storedFileName = "", contentKind = CanvasContentKinds.Image, decorationMode = DecorationModes.None, monitorDevice = "";
    private double centerX, centerY, width = 360, height = 240, rotationDegrees, opacity = 1;
    private bool isFlipped, isLocked, isTemporarilyHidden;
    private int zIndex;
    private WidgetThemeKind theme = WidgetThemeKind.Auto;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get => displayName; set => Set(ref displayName, value ?? ""); }
    public string StoredFileName { get => storedFileName; set => Set(ref storedFileName, value ?? ""); }
    public string ContentKind { get => contentKind; set => Set(ref contentKind, value ?? CanvasContentKinds.Image); }
    [JsonPropertyName("mediaKind")] public string LegacyMediaKind { set => ContentKind = value; }
    [JsonIgnore] public string MediaKind { get => ContentKind; set => ContentKind = value; }
    public string DecorationMode { get => decorationMode; set => Set(ref decorationMode, value ?? DecorationModes.None); }
    public WidgetThemeKind Theme { get => theme; set => Set(ref theme, Enum.IsDefined(value) ? value : WidgetThemeKind.Auto); }
    public ClockOptions Clock { get; set; } = new();
    public NowPlayingOptions NowPlaying { get; set; } = new();
    public SystemMonitorOptions SystemMonitor { get; set; } = new();
    public string MonitorDevice { get => monitorDevice; set => Set(ref monitorDevice, value ?? ""); }
    public double CenterX { get => centerX; set => Set(ref centerX, value); }
    public double CenterY { get => centerY; set => Set(ref centerY, value); }
    public double Width { get => width; set => Set(ref width, Math.Max(48, value)); }
    public double Height { get => height; set => Set(ref height, Math.Max(48, value)); }
    public double RotationDegrees { get => rotationDegrees; set => Set(ref rotationDegrees, Geometry.NormalizeDegrees(value)); }
    public double Opacity { get => opacity; set => Set(ref opacity, Math.Clamp(value, .05, 1)); }
    public bool IsFlipped { get => isFlipped; set => Set(ref isFlipped, value); }
    public bool IsLocked { get => isLocked; set => Set(ref isLocked, value); }
    public int ZIndex { get => zIndex; set => Set(ref zIndex, value); }
    [JsonIgnore] public bool IsTemporarilyHidden { get => isTemporarilyHidden; set => Set(ref isTemporarilyHidden, value); }
    [JsonIgnore] public string KindLabel => ContentKind switch { CanvasContentKinds.Gif => "GIF", CanvasContentKinds.Clock => "時計", CanvasContentKinds.NowPlaying => "再生中", CanvasContentKinds.SystemMonitor => "システムモニター", _ => "画像" };
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return; field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); if (name == nameof(ContentKind)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KindLabel))); }
}
public readonly record struct DisplayArea(string DeviceName, double Left, double Top, double Width, double Height, bool IsPrimary) { public double Right => Left + Width; public double Bottom => Top + Height; }
public static class Geometry
{
    private const double MinimumVisibleLength = 64;
    public static double NormalizeDegrees(double value) { var result = value % 360; if (result > 180) result -= 360; else if (result <= -180) result += 360; return Math.Round(result, 2); }
    public static void ClampToDisplays(CanvasItem item, IReadOnlyList<DisplayArea> displays)
    {
        if (displays.Count == 0) return;
        var display = displays.FirstOrDefault(candidate => item.CenterX >= candidate.Left && item.CenterX <= candidate.Right && item.CenterY >= candidate.Top && item.CenterY <= candidate.Bottom);
        if (string.IsNullOrWhiteSpace(display.DeviceName)) display = displays.FirstOrDefault(candidate => string.Equals(candidate.DeviceName, item.MonitorDevice, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(display.DeviceName)) display = displays.FirstOrDefault(candidate => candidate.IsPrimary);
        if (string.IsNullOrWhiteSpace(display.DeviceName)) display = displays[0];
        var radians = item.RotationDegrees * Math.PI / 180; var boundsWidth = item.Width * Math.Abs(Math.Cos(radians)) + item.Height * Math.Abs(Math.Sin(radians)); var boundsHeight = item.Width * Math.Abs(Math.Sin(radians)) + item.Height * Math.Abs(Math.Cos(radians));
        item.CenterX = Math.Clamp(item.CenterX, display.Left - boundsWidth / 2 + Math.Min(MinimumVisibleLength, boundsWidth), display.Right + boundsWidth / 2 - Math.Min(MinimumVisibleLength, boundsWidth));
        item.CenterY = Math.Clamp(item.CenterY, display.Top - boundsHeight / 2 + Math.Min(MinimumVisibleLength, boundsHeight), display.Bottom + boundsHeight / 2 - Math.Min(MinimumVisibleLength, boundsHeight)); item.MonitorDevice = display.DeviceName;
    }
}
