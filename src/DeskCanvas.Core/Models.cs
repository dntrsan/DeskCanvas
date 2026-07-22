using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace DeskCanvas.Core;

public sealed class CanvasLayout
{
    public int Version { get; set; } = 1;
    public CanvasSettings Settings { get; set; } = new();
    public List<CanvasItem> Items { get; set; } = [];
}

public sealed class CanvasSettings
{
    public bool StartWithWindows { get; set; }
}

public sealed class CanvasItem : INotifyPropertyChanged
{
    private string displayName = "";
    private string storedFileName = "";
    private string mediaKind = "image";
    private string monitorDevice = "";
    private double centerX;
    private double centerY;
    private double width = 360;
    private double height = 240;
    private double rotationDegrees;
    private double opacity = 1;
    private bool isFlipped;
    private bool isLocked;
    private int zIndex;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName
    {
        get => displayName;
        set => Set(ref displayName, value);
    }

    public string StoredFileName
    {
        get => storedFileName;
        set => Set(ref storedFileName, value);
    }

    public string MediaKind
    {
        get => mediaKind;
        set => Set(ref mediaKind, value);
    }

    public string MonitorDevice
    {
        get => monitorDevice;
        set => Set(ref monitorDevice, value);
    }

    public double CenterX
    {
        get => centerX;
        set => Set(ref centerX, value);
    }

    public double CenterY
    {
        get => centerY;
        set => Set(ref centerY, value);
    }

    public double Width
    {
        get => width;
        set => Set(ref width, Math.Max(48, value));
    }

    public double Height
    {
        get => height;
        set => Set(ref height, Math.Max(48, value));
    }

    public double RotationDegrees
    {
        get => rotationDegrees;
        set => Set(ref rotationDegrees, Geometry.NormalizeDegrees(value));
    }

    public double Opacity
    {
        get => opacity;
        set => Set(ref opacity, Math.Clamp(value, 0.05, 1));
    }

    public bool IsFlipped
    {
        get => isFlipped;
        set => Set(ref isFlipped, value);
    }

    public bool IsLocked
    {
        get => isLocked;
        set => Set(ref isLocked, value);
    }

    public int ZIndex
    {
        get => zIndex;
        set => Set(ref zIndex, value);
    }

    [JsonIgnore]
    public string KindLabel => MediaKind == "gif" ? "GIF" : "画像";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name == nameof(MediaKind))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KindLabel)));
        }
    }
}

public readonly record struct DisplayArea(
    string DeviceName,
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsPrimary)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;
}

public static class Geometry
{
    private const double MinimumVisibleLength = 64;

    public static double NormalizeDegrees(double value)
    {
        var result = value % 360;
        if (result > 180)
        {
            result -= 360;
        }
        else if (result <= -180)
        {
            result += 360;
        }

        return Math.Round(result, 2);
    }

    public static void ClampToDisplays(CanvasItem item, IReadOnlyList<DisplayArea> displays)
    {
        if (displays.Count == 0)
        {
            return;
        }

        var display = displays.FirstOrDefault(candidate =>
            item.CenterX >= candidate.Left &&
            item.CenterX <= candidate.Right &&
            item.CenterY >= candidate.Top &&
            item.CenterY <= candidate.Bottom);
        if (string.IsNullOrWhiteSpace(display.DeviceName))
        {
            display = displays.FirstOrDefault(candidate =>
                string.Equals(candidate.DeviceName, item.MonitorDevice, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(display.DeviceName))
        {
            display = displays.FirstOrDefault(candidate => candidate.IsPrimary);
        }

        if (string.IsNullOrWhiteSpace(display.DeviceName))
        {
            display = displays[0];
        }

        var radians = item.RotationDegrees * Math.PI / 180;
        var cosine = Math.Abs(Math.Cos(radians));
        var sine = Math.Abs(Math.Sin(radians));
        var boundsWidth = item.Width * cosine + item.Height * sine;
        var boundsHeight = item.Width * sine + item.Height * cosine;
        var visibleWidth = Math.Min(MinimumVisibleLength, boundsWidth);
        var visibleHeight = Math.Min(MinimumVisibleLength, boundsHeight);

        item.CenterX = Math.Clamp(
            item.CenterX,
            display.Left - boundsWidth / 2 + visibleWidth,
            display.Right + boundsWidth / 2 - visibleWidth);
        item.CenterY = Math.Clamp(
            item.CenterY,
            display.Top - boundsHeight / 2 + visibleHeight,
            display.Bottom + boundsHeight / 2 - visibleHeight);
        item.MonitorDevice = display.DeviceName;
    }
}
